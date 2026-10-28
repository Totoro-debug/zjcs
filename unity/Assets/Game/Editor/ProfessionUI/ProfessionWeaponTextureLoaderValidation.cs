using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionWeaponTextureLoaderValidation
    {
        [Serializable] private sealed class Row { public int index; public string name, main, dye, material; }
        [Serializable] private sealed class Mip { public int level, width, height, size; public string sha256; }
        [Serializable] private sealed class TextureRow { public string path; public Mip[] mips; }
        [Serializable] private sealed class FloatRow { public string name; public float value; }
        [Serializable] private sealed class ColorRow { public string name; public float x, y, z, w; }
        [Serializable] private sealed class MaterialRow { public string path, name; public string[] keywords; public FloatRow[] floats; public ColorRow[] colors; }
        [Serializable] private sealed class Fixture { public string loaderPath, loaderGuid, atlasPath, atlasGuid; public Row[] rows; public TextureRow[] textures; public MaterialRow[] materials; }
        [Serializable] private sealed class FileRow { public string path; }
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; }
        [Serializable] private sealed class ShaderRow { public FileRow output; public Program[] programs; }
        [Serializable] private sealed class Proof { public ShaderRow[] shaders; }
        private sealed class Subscription : IDisposable { public void Dispose() { } }
        private const string Active = "Zjcs.ProfessionWeaponTextureLoader.Active";
        private static bool ready;
        private static readonly List<Object> owned = new List<Object>();
        static ProfessionWeaponTextureLoaderValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 240));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native weapon loader validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("weapon-texture-loader-validation.json")));
            var resources = Resources.Load<ProfessionNativeWeaponTextureResources>("ProfessionUI/weapon-texture-resources");
            var loader = resources.Loader;
            Require(resources && loader && resources.Atlas && resources.ManagerConfig, "explicit native resource graph");
            Require(resources.LoaderPath == fixture.loaderPath && resources.LoaderGuid == fixture.loaderGuid && resources.Atlas.SourcePath == fixture.atlasPath && resources.Atlas.SourceGuid == fixture.atlasGuid, "original source paths and GUIDs");
            Require(resources.Atlas.OriginalMetadata && resources.Atlas.OriginalMetadata.text.Contains("\"m_Name\":\"primaryWeapon_Atlas\"") && loader.atlasAsset == resources.Atlas && !loader.skeletonDataAsset, "full original atlas metadata, pointer identities and null skeleton pointer");
            Require(loader.placeholderMap.Length == 398 && resources.Atlas.MaterialCount == 398 && fixture.rows.Length == 398 && loader.maxPlaceholderSize == 32 && loader.unloadAfterSecondsUnused == 60 && loader.LevelOfResolution == LevelOfResolution.High && loader.PlaceholderPath == "Atlas/Placeholder" && loader.TargetTexturePath == "Atlas/High" && loader.DyeTexturePath == "", "complete original loader settings/order");
            foreach (var row in fixture.rows)
            {
                var mapping = loader.placeholderMap[row.index]; var material = resources.Atlas.MaterialAt(row.index);
                Require(mapping.placeholderTexture && mapping.placeholderTexture.name == row.name && mapping.targetTextureReference.AssetReference != null && mapping.targetTextureReference.AssetReference.Guid == row.main && mapping.dyeTextureReference.AssetReference != null && mapping.dyeTextureReference.AssetReference.Guid == row.dye, "original full placeholder and main/empty-dye proxy identity " + row.index);
                Require(material && material.name == row.material && material.mainTexture == mapping.placeholderTexture, "original atlas material/page index " + row.index);
            }
            int materialValues = 0;
            foreach (var expected in fixture.materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(expected.path); var serialized = new SerializedObject(material);
                Require(material && material.name == expected.name && new HashSet<string>(material.shaderKeywords).SetEquals(expected.keywords), "original material name/keyword set " + expected.name);
                foreach (var row in expected.floats)
                {
                    var value = Pair(serialized.FindProperty("m_SavedProperties.m_Floats"), row.name).floatValue;
                    Require(value.Equals(row.value), "exact original material float " + expected.name + "/" + row.name); materialValues++;
                }
                foreach (var row in expected.colors)
                {
                    Color value = Pair(serialized.FindProperty("m_SavedProperties.m_Colors"), row.name).colorValue;
                    Require(value.r.Equals(row.x) && value.g.Equals(row.y) && value.b.Equals(row.z) && value.a.Equals(row.w), "exact original material vector " + expected.name + "/" + row.name); materialValues++;
                }
            }
            int mipCount = 0; long byteCount = 0;
            foreach (var expected in fixture.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(expected.path); Require(texture, "native original placeholder texture");
                foreach (var mip in expected.mips)
                {
                    Require(Math.Max(1, texture.width >> mip.level) == mip.width && Math.Max(1, texture.height >> mip.level) == mip.height, "original placeholder dimensions");
                    var request = AsyncGPUReadback.Request(texture, mip.level); request.WaitForCompletion(); Require(!request.hasError, "native placeholder GPU readback");
                    var bytes = request.GetData<byte>().ToArray();
                    using (var sha = SHA256.Create()) Require(bytes.Length == mip.size && BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == mip.sha256, "exact original placeholder mip bytes " + texture.name);
                    byteCount += bytes.Length; mipCount++;
                }
            }
            var proof = JsonUtility.FromJson<Proof>(File.ReadAllText(Path.Combine(ProfessionValidationWorkspace.Repository, "assets/reference/profession-ui/weapon-texture-loader-source-manifest.json")));
            int stages = 0; bool oldAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var expected in proof.shaders)
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(expected.output.path.Substring("unity/".Length));
                    Require(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "native original shader binding " + expected.output.path);
                    foreach (var program in expected.programs)
                    foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        var compiled = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(program.pass_index).CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                        Require(compiled.Success, "new original shader stage " + stage + "/" + program.pass_index); stages++;
                    }
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = oldAsync; }
            var appearanceConfig = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            using (var manager = new AppearanceAssetManager(resources.ManagerConfig, callback => new Subscription()))
            {
                var bindings = new OfflineProfessionWeaponTextureResources(appearanceConfig, manager, resources);
                ProfessionAppearanceTextureBindings.Current = bindings;
                var a = bindings.LoadAsset(fixture.loaderPath); var b = bindings.LoadAsset(fixture.loaderPath); a.Dispose();
                Throws<ObjectDisposedException>(() => { var ignored = a.Result; }); Require(b.Result == loader, "independent native loader request ownership"); b.Dispose();
                Throws<NotSupportedException>(() => bindings.LoadAsset(fixture.loaderPath + ".missing"));
                Throws<ArgumentOutOfRangeException>(() => resources.Atlas.MaterialAt(398));
                Throws<NotSupportedException>(() => bindings.GetAtlasMaterial(resources.ManagerConfig, 0));
                var weapon = Resources.Load<ProfessionNativeWeaponCatalog>("ProfessionUI/weapon-resources");
                foreach (string name in new[] { "pWeapon_100_007", "pWeapon_200_007" })
                {
                    var page = weapon.FindPage(name); var row = fixture.rows.Single(r => r.name == name + "_placeholder");
                    Require(loader.placeholderMap[row.index].placeholderTexture == page.Placeholder, "previously packaged default placeholder identity reused");
                    var handle = bindings.LoadAssetSyncByGuid<Texture2D>(row.main);
                    Require(handle.IsValid && handle.Status == 2 && handle.Result == page.Main, "real typed native high texture GUID load");
                    int callbacks = 0; handle.Completed += h => { Require(h.Result == page.Main, "synchronous completed request result"); callbacks++; };
                    handle.WaitForAsyncComplete(); handle.Release(); Require(callbacks == 1 && !handle.IsValid && !handle.Result, "typed native handle release and completed callback");
                    Throws<ObjectDisposedException>(() => handle.WaitForAsyncComplete());
                    var first = Endpoint(bindings, page.Placeholder); var second = Endpoint(bindings, page.Placeholder);
                    first.gameObject.SetActive(true); second.gameObject.SetActive(true);
                    Require(first.OverrideTexture == page.Main && second.OverrideTexture == page.Main && Data(loader)[row.index].referenceCounter == 2, "original UILite with actual native loader/atlas/catalog");
                    var dye = Data(loader)[row.index].textureRequest.DyeTextureHandle;
                    Require(row.dye == "" && dye.IsValid && dye.WasSuccessfullyLoaded && !dye.Result, "original empty dye proxy still executes null-result request branch");
                    first.gameObject.SetActive(false); Require(second.OverrideTexture == page.Main && Data(loader)[row.index].referenceCounter == 1, "native owner isolation");
                    Capture(weapon, "pWeapon_" + (name.Contains("100") ? "100" : "200") + "_G_007", second.OverrideTexture, page.Material);
                    second.gameObject.SetActive(false);
                    Require(!Data(loader)[row.index].textureRequest.WasRequested && resources.Atlas.MaterialAt(row.index).mainTexture == page.Placeholder, "actual native request unload and original indexed atlas restoration");
                    IAppearanceAssetManager assets = manager; var source = resources.Atlas.MaterialAt(row.index); var shared = new Material[1];
                    assets.GetLoaderSharedMaterials(loader, new[] { source }, ref shared);
                    Require(shared[0] != source && shared[0].mainTexture == page.Main && source.mainTexture == page.Placeholder, "original real atlas material clone loads high page without mutating source");
                    assets.ReturnLoaderSharedMaterials(loader, new[] { source }, true);
                    Require(!Data(loader)[row.index].textureRequest.WasRequested && shared[0].mainTexture == null && source.mainTexture == page.Placeholder, "native source shared clone return and empty dye release");
                }
                Throws<NotSupportedException>(() => resources.TextureByGuid(fixture.rows.First(r => r.name != "pWeapon_100_007_placeholder" && r.name != "pWeapon_200_007_placeholder").main));
                Require(Data(loader).All(d => d.referenceCounter == 0 && !d.textureRequest.WasRequested), "complete original loader returns to zero outstanding references");
            }
            Require(fixture.textures.Length == 400 && fixture.materials.Length == 399 && mipCount == 400 && stages > 0, "full source graph and new executable validation coverage");
            Debug.Log("Native weapon loader Play Mode passed: all 398 original mappings/placeholders/atlas materials and source manager config; " + materialValues + " exact original material values, " + mipCount + " GPU mip hashes (" + byteCount + " bytes), " + stages + " new D3D shader stages; real native requests, original UILite single-owner sharing, empty dye proxy branch, indexed unload and shared material return; 2 real GPU weapon captures. Graphic endpoint/low-memory event remain fixtures; production prefab and full profession page/input/save/Windows release remain incomplete.");
        }
        private static SerializedProperty Pair(SerializedProperty list, string name)
        {
            for (int i = 0; i < list.arraySize; i++) { var pair = list.GetArrayElementAtIndex(i); if (pair.FindPropertyRelative("first").stringValue == name) return pair.FindPropertyRelative("second"); }
            throw new Exception("Missing original serialized material value: " + name);
        }
        private static ProfessionAppearanceUILiteValidation.GraphicEndpoint Endpoint(IProfessionAppearanceUILiteBindings bindings, Texture placeholder)
        {
            var node = Own(new GameObject("Native weapon texture lifecycle diagnostic", typeof(RectTransform))); node.SetActive(false);
            var graph = node.AddComponent<ProfessionAppearanceUILiteValidation.GraphicEndpoint>(); graph.texture = placeholder;
            var appearance = node.AddComponent<AppearanceUILite>(); appearance.SkeletonGraphic = graph; appearance.Configure(bindings); return graph;
        }
        private static AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>.MaterialOnDemandData[] Data(YooAssetTextureLoader loader)
        { return (AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>.MaterialOnDemandData[])typeof(AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>).GetField("loadedDataAtMaterial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader); }
        private static void Capture(ProfessionNativeWeaponCatalog catalog, string skin, Texture texture, Material source)
        {
            var state = new ProfessionNativeAppearanceSkeleton(catalog.Model.Data); state.SetSkin(catalog.Model.Data.SkinCatalog.FindSkin(skin)); state.SetToSetupPose();
            state.EvaluateUnmixedWeaponAnimation(skin.Contains("100") ? "100/100_loop" : "200/200_loop", .5f);
            using (var batches = new ProfessionNativeMeshBatches(catalog.Model.Data.SlotCount))
            {
                batches.Build(state.Pose, state.CreateDrawSlots()); var batch = batches.Batches.Single(); var material = Own(new Material(source)); material.mainTexture = texture;
                var bounds = batch.Mesh.bounds; float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
                var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
                var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); var old = RenderTexture.active; Texture2D capture = null;
                try
                {
                    using (var commands = new CommandBuffer()) { commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear); commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity); commands.DrawMesh(batch.Mesh, fit, material); Graphics.ExecuteCommandBuffer(commands); }
                    RenderTexture.active = target; capture = new Texture2D(640, 640, TextureFormat.RGBA32, false); capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                    Require(capture.GetPixels32().Count(p => p.a > 32) > 5000, "native texture binding actual weapon GPU coverage");
                    File.WriteAllBytes(ProfessionValidationWorkspace.Output("weapon-loader-" + skin + ".png"), capture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; if (capture) Object.DestroyImmediate(capture); RenderTexture.ReleaseTemporary(target); }
            }
        }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false); ProfessionAppearanceTextureBindings.Current = null;
            foreach (Object value in owned) if (value) Object.DestroyImmediate(value);
            owned.Clear(); EditorApplication.Exit(code);
        }
    }
}
