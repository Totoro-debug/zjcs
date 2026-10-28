using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionCharacterTextureValidation
    {
        [Serializable] private sealed class Fixture { public Page[] pages; public Expected[] textures; }
        [Serializable] private sealed class Page { public string sex, page, main_guid, dye_guid; }
        [Serializable] private sealed class Mip { public int level, width, height, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class Sampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW; public float m_MipBias; }
        [Serializable] private sealed class Expected { public string path, guid; public int width, height, mip_count, texture_format; public Sampler settings; public Mip[] mips; }
        private const string Active = "Zjcs.ProfessionCharacterTextures.Active";
        private static bool ready;
        static ProfessionCharacterTextureValidation()
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
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 300));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Character texture GPU validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("character-textures-validation.json")));
            var catalog = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            Require(catalog && catalog.Count == fixture.pages.Length && fixture.pages.Length >= 6, "actual local resource catalog and verified selected source page bindings");
            Require(SystemInfo.supportsAsyncGPUReadback, "actual graphics device readback");
            long totalBytes = 0; int levels = 0;
            foreach (var expected in fixture.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(expected.path);
                Require(texture && AssetDatabase.AssetPathToGUID(expected.path) == expected.guid, "exact original asset identity");
                Require(texture.width == expected.width && texture.height == expected.height && texture.mipmapCount == expected.mip_count && (int)texture.format == expected.texture_format, "source dimensions, mips and native format");
                Require((int)texture.filterMode == expected.settings.m_FilterMode && texture.anisoLevel == expected.settings.m_Aniso &&
                    (int)texture.wrapModeU == expected.settings.m_WrapU && (int)texture.wrapModeV == expected.settings.m_WrapV && (int)texture.wrapModeW == expected.settings.m_WrapW &&
                    Math.Abs(texture.mipMapBias - expected.settings.m_MipBias) < .000001f, "source sampler settings");
                foreach (var mip in expected.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level); request.WaitForCompletion(); Require(!request.hasError, "actual GPU upload/readback");
                    var bytes = request.GetData<byte>().ToArray();
                    using (var hash = SHA256.Create()) Require(bytes.Length == mip.rgba_size && BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256, expected.path + " exact original decoded GPU mip bytes");
                    levels++; totalBytes += bytes.LongLength;
                }
            }
            foreach (var expected in fixture.pages)
            {
                var page = catalog.Find(expected.sex, expected.page);
                Require(page.Placeholder && page.Main && page.MainGuid == expected.main_guid && page.DyeGuid == expected.dye_guid, "original loader map main/dye GUIDs and placeholder references");
                Require(catalog.ByGuid(expected.main_guid) == page.Main, "actual main GUID lookup");
                if (!string.IsNullOrEmpty(expected.dye_guid)) Require(page.Dye && catalog.ByGuid(expected.dye_guid) == page.Dye, "actual dye GUID lookup");
                else Require(!page.Dye, "original absent dye remains absent");
            }
            Reject<InvalidOperationException>(() => catalog.Find("male", "unconverted"));
            Reject<InvalidOperationException>(() => catalog.ByGuid("unconverted"));
            Reject<ArgumentException>(() => catalog.ByGuid(""));
            var body = catalog.Find("male", "body_200_M_01");
            var target = RenderTexture.GetTemporary(body.Main.width, body.Main.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active; Texture2D capture = null;
            try
            {
                Graphics.Blit(body.Main, target); RenderTexture.active = target;
                capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); capture.Apply();
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("character-core-atlas.png"), capture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); if (capture) UnityEngine.Object.Destroy(capture); }
            Debug.Log("Original core character textures passed actual Direct3D Play Mode: " + fixture.pages.Length + " page bindings, " + fixture.textures.Length + " distinct original textures, " + levels + " exact GPU mip hashes, " + totalBytes + " decoded bytes. Main/dye/placeholder asset identity and GUID lookup pass. GPU atlas capture is a source texture, not an actor or complete profession page; other skins, physics and Windows Player remain unvalidated.");
        }
        private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Original character texture: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
