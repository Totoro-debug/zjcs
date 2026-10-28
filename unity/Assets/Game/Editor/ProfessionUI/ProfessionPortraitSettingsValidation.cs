using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionPortraitSettingsValidation
    {
        private const string Active = "Zjcs.ProfessionPortraitSettings.Active";
        private static bool ready, initialized;
        private static int frames;
        [Serializable] private sealed class Fixture { public string settings; public Parameter[] parameters; public Portrait[] sprites; }
        [Serializable] private sealed class Parameter
        {
            public int[] MyTypes, mExcludeSkinTypes;
            public int TextureWidth, TextureHeight, mCaptureParticle, mDepthBuffer;
            public float Size;
            public Vector3 mOffset;
            public Weights mAppearanceHashWeights;
        }
        [Serializable] private sealed class Weights { public Weight[] mSerializeBuffer; }
        [Serializable] private sealed class Weight { public int Key; public long Value; }
        [Serializable] private sealed class Portrait { public string field, path, texture_path; public Expected expected; public Mip[] mips; }
        [Serializable] private sealed class OriginalRect { public float x, y, width, height; }
        [Serializable] private sealed class Mip { public int level, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class Expected
        {
            public string name;
            public OriginalRect rect;
            public Vector2 pivot;
            public Vector4 border;
            public float ppu;
            public int width, height, mip_count, filter, aniso, wrapU, wrapV;
            public Vector2[] vertices, uv;
            public int[] triangles;
        }
        static ProfessionPortraitSettingsValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Portrait settings fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized)
                {
                    initialized = true;
                    Validate();
                }
                if (++frames > 3)
                {
                    Debug.Log("Profession original portrait settings passed: all four native photography groups, skin weights/exclusions, offset and fallback-texture ownership; three exact original Sprite meshes/UVs and every GPU mip byte. Live character capture and full profession UI are not covered.");
                    Finish(0);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Validate()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("portrait-settings-validation.json")));
            var settings = AssetDatabase.LoadAssetAtPath<PlayerPortraitSettings>(fixture.settings);
            Require(settings != null && fixture.parameters.Length == 4 && fixture.sprites.Length == 3, "native original settings import");
            var dictionary = JsonUtility.FromJson<MonoDictionary<SkinType, long>>("{\"mSerializeBuffer\":[{\"Key\":0,\"Value\":7},{\"Key\":0,\"Value\":9}]}");
            Require(dictionary.Count == 1 && dictionary[SkinType.eye] == 7, "original deserialize retains the first duplicate key");
            Require(!dictionary.Remove(SkinType.eye) && !dictionary.ContainsKey(SkinType.eye), "retain the original one-argument Remove double-removal return behavior");
            dictionary.Add(SkinType.eye, 11);
            Require(dictionary.Remove(SkinType.eye, out long removed) && removed == 11 && dictionary.Count == 0, "original out-value Remove overload");
            foreach (var group in fixture.parameters)
            {
                foreach (int type in group.MyTypes)
                {
                    var actual = settings.GetParameters((ProfessionPortraitKind)type);
                    Require(actual != null && ReferenceEquals(actual, settings.GetParameters((ProfessionPortraitKind)type)), "original lazy type dictionary returns the configured instance");
                    Require(actual.TextureWidth == group.TextureWidth && actual.TextureHeight == group.TextureHeight && actual.Size == group.Size && actual.DepthBuffer == group.mDepthBuffer && actual.CaptureParticle == (group.mCaptureParticle != 0), "original dimensions, camera, depth and particle flags");
                    Require(actual.MyTypes.Select(x => (int)x).SequenceEqual(group.MyTypes) && actual.ExcludeSkinTypes.Select(x => (int)x).SequenceEqual(group.mExcludeSkinTypes) && actual.HasExcludeConfig == (group.mExcludeSkinTypes.Length > 0), "original type groups and skin exclusions");
                    Require(actual.GetOffset() == group.mOffset + new Vector3(0, 0, -5), "original camera offset adjustment");
                    foreach (SkinType skin in Enum.GetValues(typeof(SkinType)))
                    {
                        var weight = group.mAppearanceHashWeights.mSerializeBuffer.FirstOrDefault(x => x.Key == (int)skin);
                        Require(actual.GetHashWeightsBySkinType(skin) == (weight?.Value ?? 0), "original serialized skin hash weights/default zero");
                    }
                }
            }
            foreach (var entry in fixture.sprites)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.path);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.texture_path);
                var e = entry.expected;
                Require(sprite != null && texture != null && sprite.texture == texture && sprite.name == e.name, "original fallback Sprite/texture binding");
                Require(sprite.rect == new Rect(e.rect.x, e.rect.y, e.rect.width, e.rect.height) && sprite.pivot == e.pivot && sprite.border == e.border && sprite.pixelsPerUnit == e.ppu, "original fallback rect, pivot, border and PPU");
                Require(sprite.vertices.Length == e.vertices.Length && sprite.uv.Length == e.uv.Length && sprite.triangles.Select(x => (int)x).SequenceEqual(e.triangles), "original fallback mesh topology");
                for (int i = 0; i < e.vertices.Length; i++) Require(Vector2.Distance(sprite.vertices[i], e.vertices[i]) < .000001f && Vector2.Distance(sprite.uv[i], e.uv[i]) < .000001f, "original fallback mesh vertices and UVs");
                Require(texture.width == e.width && texture.height == e.height && texture.mipmapCount == e.mip_count && (int)texture.filterMode == e.filter && texture.anisoLevel == e.aniso && (int)texture.wrapModeU == e.wrapU && (int)texture.wrapModeV == e.wrapV, "original fallback texture dimensions, mips and samplers");
                foreach (var mip in entry.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    request.WaitForCompletion();
                    Require(!request.hasError, "actual original fallback GPU upload/readback");
                    byte[] bytes = request.GetData<byte>().ToArray();
                    using (var hash = SHA256.Create()) Require(bytes.Length == mip.rgba_size && BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256, "exact original decoded fallback mip bytes");
                    if (mip.level == 0)
                    {
                        var snapshot = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                        try
                        {
                            snapshot.LoadRawTextureData(bytes); snapshot.Apply(false, false);
                            File.WriteAllBytes(ProfessionValidationWorkspace.Output("portrait-settings-" + e.name + ".png"), snapshot.EncodeToPNG());
                        }
                        finally { Object.Destroy(snapshot); }
                    }
                }
                CharacterPortraitTexture2D wrapped = entry.field == "mDefaultPortrait" ? settings.DefaultTexture : entry.field == "mLoadingPortrait" ? settings.LoadingTexture : settings.DisableTexture;
                CharacterPortraitTexture2D repeated = entry.field == "mDefaultPortrait" ? settings.DefaultTexture : entry.field == "mLoadingPortrait" ? settings.LoadingTexture : settings.DisableTexture;
                Require(ReferenceEquals(wrapped, repeated) && wrapped.Tex == texture && wrapped.Width == texture.width && wrapped.Height == texture.height, "source lazily wraps the whole fallback texture once");
                wrapped.DestroyTexture();
                Require(wrapped.Tex == texture, "source don't-destroy ownership retains the imported fallback texture");
            }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Portrait settings: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(code);
        }
    }
}
