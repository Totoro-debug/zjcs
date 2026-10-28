using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceUILiteValidation
    {
        private const string Active = "Zjcs.ProfessionUILite.Active";
        private static bool ready;
        private static readonly List<Object> owned = new List<Object>();
        private sealed class Subscription : IDisposable
        { public int Disposals; public void Dispose() { Disposals++; } }
        private sealed class Handle<T> : IProfessionUnsafeAssetHandle<T> where T : Object
        {
            private T asset;
            public Handle(T asset) { this.asset = asset; }
            public bool IsValid => asset;
            public int Status => IsValid ? 2 : 0;
            public T Result => asset;
            public int Releases;
            public void WaitForAsyncComplete() { }
            public void Release() { Releases++; asset = null; }
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed
            { add { value(this); } remove { } }
        }
        private sealed class ResourceHandle : IProfessionAppearanceCharacterAsset
        {
            public ScriptableObject Result { get; }
            public int Disposals;
            public ResourceHandle(ScriptableObject result) { Result = result; }
            public void Dispose() { Disposals++; }
        }
        private sealed class Textures : IProfessionAppearanceTextureBindings
        {
            public readonly Dictionary<string, Texture> Values = new Dictionary<string, Texture>();
            public readonly List<Handle<Texture>> Handles = new List<Handle<Texture>>();
            public readonly List<string> Calls = new List<string>();
            public readonly List<int> Restored = new List<int>();
            public Material[] Atlas;
            public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : Object
            {
                Calls.Add(guid);
                var handle = new Handle<Texture>(Values[guid]); Handles.Add(handle);
                return (IProfessionUnsafeAssetHandle<T>)(object)handle;
            }
            public Material GetAtlasMaterial(ScriptableObject atlas, int index)
            { Restored.Add(index); return Atlas[index]; }
        }
        private sealed class Bindings : IProfessionAppearanceUILiteBindings
        {
            public AppearanceConfig Config { get; set; }
            public IProfessionAppearanceUIAssetManager AssetManager { get; set; }
            public YooAssetTextureLoader Loader;
            public readonly List<string> Paths = new List<string>();
            public readonly List<ResourceHandle> Handles = new List<ResourceHandle>();
            public IProfessionAppearanceCharacterAsset LoadAsset(string path)
            { Paths.Add(path); var handle = new ResourceHandle(Loader); Handles.Add(handle); return handle; }
        }
        // Diagnostic endpoint only: original component/manager behavior is tested
        // against real native weapon textures; this is not the production Graphic.
        public sealed class GraphicEndpoint : RawImage, IProfessionAppearanceUILiteGraphic
        {
            public string SkeletonDataName { get; set; } = "primaryWeapon";
            public bool allowMultipleCanvasRenderers { get; set; }
            public Texture OverrideTexture { get; set; }
            public Dictionary<Texture, Texture> CustomTextureOverride { get; } = new Dictionary<Texture, Texture>();
            public Texture[] TexturesMultipleCanvasRenderers { get; set; }
            public int MeshUpdates;
            public void UpdateMesh() { MeshUpdates++; }
        }
        static ProfessionAppearanceUILiteValidation()
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
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("UI lite validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var catalog = Resources.Load<ProfessionNativeWeaponCatalog>("ProfessionUI/weapon-resources");
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var pages = new[] { catalog.FindPage("pWeapon_100_007"), catalog.FindPage("pWeapon_200_007") };
            var loader = Own(ScriptableObject.CreateInstance<YooAssetTextureLoader>());
            loader.LevelOfResolution = LevelOfResolution.High;
            loader.atlasAsset = Own(ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>());
            loader.placeholderMap = pages.Select(p => new AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>.PlaceholderTextureMapping {
                placeholderTexture = p.Placeholder, targetTextureReference = new YooAssetTextureReference { AssetReference = Proxy(p.MainGuid) }
            }).ToArray();
            var textures = new Textures { Atlas = pages.Select(p => Own(new Material(p.Material))).ToArray() };
            foreach (var page in pages) textures.Values.Add(page.MainGuid, page.Main);
            ProfessionAppearanceTextureBindings.Current = textures;
            var managerConfig = Own(ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>());
            managerConfig.UIDyeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            var subscription = new Subscription(); Action lowMemory = null;
            var manager = new AppearanceAssetManager(managerConfig, callback => { lowMemory = callback; return subscription; });
            IAppearanceAssetManager assets = manager;
            var bindings = new Bindings { Config = config, AssetManager = assets, Loader = loader };
            var a = Endpoint(bindings, pages[0].Placeholder, false);
            var b = Endpoint(bindings, pages[0].Placeholder, false);
            a.gameObject.SetActive(true); b.gameObject.SetActive(true);
            Require(bindings.Paths.SequenceEqual(Enumerable.Repeat("Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_TextureLoader_High.asset", 2)), "original high-loader address and one request per component");
            Require(a.OverrideTexture == pages[0].Main && b.OverrideTexture == pages[0].Main && textures.Handles.Count == 1 && Counters(loader).SequenceEqual(new[] { 2, 0 }), "two owners share the actual high texture and count independently");
            Require(a.MeshUpdates == 1 && b.MeshUpdates == 1, "original mesh update before use");
            a.gameObject.SetActive(false);
            Require(!a.OverrideTexture && b.OverrideTexture == pages[0].Main && textures.Handles[0].Releases == 0 && bindings.Handles[0].Disposals == 1, "disable clears first override and releases request without unloading other owner");
            lowMemory(); manager.Update();
            Require(textures.Handles[0].Releases == 0, "positive texture references survive low memory and manager update");
            b.gameObject.SetActive(false);
            Require(textures.Handles[0].Releases == 1 && textures.Restored.SequenceEqual(new[] { 0 }) && textures.Atlas[0].mainTexture == pages[0].Placeholder, "last manager reference forces original texture release and atlas restoration");
            a.gameObject.SetActive(true);
            Require(bindings.Paths.Count == 2 && textures.Handles.Count == 2 && a.OverrideTexture == pages[0].Main, "source cached loader reused after request disposal");
            Capture(catalog, "pWeapon_100_G_007", a.OverrideTexture, pages[0].Material);
            a.GetComponent<IProfessionAppearanceUnloadable>().MarkUnloadTextureImmediately(); a.gameObject.SetActive(false);
            Require(bindings.Handles[0].Disposals == 1 && textures.Handles[1].Releases == 1, "new disposable bag does not re-dispose previous request");
            var multiple = Endpoint(bindings, null, true);
            multiple.TexturesMultipleCanvasRenderers = new Texture[] { pages[0].Placeholder, null, pages[1].Placeholder, pages[0].Placeholder };
            multiple.gameObject.SetActive(true);
            Require(multiple.CustomTextureOverride.Count == 2 && multiple.CustomTextureOverride[pages[1].Placeholder] == pages[1].Main && Counters(loader).SequenceEqual(new[] { 1, 1 }), "multiple renderer branch skips null and duplicate placeholders");
            Capture(catalog, "pWeapon_200_G_007", multiple.CustomTextureOverride[pages[1].Placeholder], pages[1].Material);
            multiple.gameObject.SetActive(false);
            Require(multiple.CustomTextureOverride.Count == 0 && Counters(loader).All(n => n == 0) && textures.Handles.All(h => h.Releases == 1), "multiple overrides cleared and each actual texture released once");
            var original = Own(new Material(managerConfig.UIDyeShader)); original.mainTexture = pages[0].Placeholder;
            var sourceMaterials = new[] { original, original };
            var sharedA = new Material[2]; var sharedB = new Material[2];
            assets.GetLoaderSharedMaterials(loader, sourceMaterials, ref sharedA);
            assets.GetLoaderSharedMaterials(loader, sourceMaterials, ref sharedB);
            Require(sharedA[0] == sharedA[1] && sharedA[0] == sharedB[0] && sharedA[0] != original && sharedA[0].mainTexture == pages[0].Main && sharedA[0].name.EndsWith("_High") && Counters(loader)[0] == 1, "original shared material clone reuse and one page reference across four slots");
            assets.ReturnLoaderSharedMaterials(loader, sourceMaterials, false);
            Require(sharedB[0].mainTexture == pages[0].Main && Counters(loader)[0] == 1, "first material return preserves remaining slots");
            assets.ReturnLoaderSharedMaterials(loader, sourceMaterials, false);
            Require(sharedB[0].mainTexture == null && Counters(loader)[0] == 0 && textures.Handles.All(h => h.Releases == 1), "last material return clears and destroys shared clone before release");
            var clone = assets.GetRendererMaterial(original); Own(clone);
            clone.mainTexture = pages[1].Main; clone.SetFloat("_EnableHairClip", 1); clone.EnableKeyword("_DISSOLVE");
            assets.ReturnRendererMaterial(clone, original);
            Require(clone.mainTexture == original.mainTexture && clone.GetFloat("_EnableHairClip") == 0 && !clone.IsKeywordEnabled("_DISSOLVE") && assets.MaxRendererMaterialCount == 1 && assets.RendererMaterials[original].Count == 1, "renderer pool resets source texture/clipping/keywords and records source maximum");
            Require(assets.GetRendererMaterial(original) == clone && assets.RendererMaterials[original].Count == 0, "renderer pool returns original clone identity");
            assets.ReturnRendererMaterial(clone, original); lowMemory();
            Require(assets.RendererMaterials[original].Count == 0, "low memory drains renderer pool");
            var ui = Own(assets.GetUIMaterial()); Require(ui.shader == managerConfig.UIDyeShader, "source configured UI material creation");
            manager.Dispose();
            Require(subscription.Disposals == 1 && assets.GetUIMaterial() == null && assets.GetRendererMaterial(original) == null, "source manager shutdown disposes subscription and rejects material allocation");
            Require(textures.Calls.Count == 5 && bindings.Paths.Count == 3 && textures.Restored.Count == 5, "all real request/release and source loader address branches covered");
            Debug.Log("AppearanceUILite/material manager Play Mode passed: actual sword/staff texture loads and 2 Direct3D captures; two single owners, cached re-enable, multiple/null/duplicate placeholders, 5 exact handle releases/atlas restores, original shared-material four-slot lifetime, renderer reuse/reset, low memory/update and manager disposal. Graphic/loader endpoints and resource handles are fixtures; production prefab, full-page input/save and Windows release remain incomplete.");
        }
        private static GraphicEndpoint Endpoint(Bindings bindings, Texture placeholder, bool multiple)
        {
            var node = Own(new GameObject("Original UILite lifecycle diagnostic", typeof(RectTransform))); node.SetActive(false);
            var graph = node.AddComponent<GraphicEndpoint>(); graph.texture = placeholder; graph.allowMultipleCanvasRenderers = multiple;
            var appearance = node.AddComponent<AppearanceUILite>(); appearance.SkeletonGraphic = graph; appearance.Configure(bindings);
            return graph;
        }
        private static int[] Counters(YooAssetTextureLoader loader)
        {
            var field = typeof(AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>).GetField("loadedDataAtMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>.MaterialOnDemandData[])field.GetValue(loader)).Select(d => d.referenceCounter).ToArray();
        }
        private static void Capture(ProfessionNativeWeaponCatalog catalog, string skin, Texture texture, Material source)
        {
            var state = new ProfessionNativeAppearanceSkeleton(catalog.Model.Data);
            state.SetSkin(catalog.Model.Data.SkinCatalog.FindSkin(skin)); state.SetToSetupPose();
            state.EvaluateUnmixedWeaponAnimation(skin.Contains("100") ? "100/100_loop" : "200/200_loop", .5f);
            using (var batches = new ProfessionNativeMeshBatches(catalog.Model.Data.SlotCount))
            {
                batches.Build(state.Pose, state.CreateDrawSlots());
                var batch = batches.Batches.Single(); var material = Own(new Material(source)); material.mainTexture = texture;
                var bounds = batch.Mesh.bounds; float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
                var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
                var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var old = RenderTexture.active; Texture2D capture = null;
                try
                {
                    using (var commands = new CommandBuffer())
                    {
                        commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                        commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        commands.DrawMesh(batch.Mesh, fit, material); Graphics.ExecuteCommandBuffer(commands);
                    }
                    RenderTexture.active = target; capture = new Texture2D(640, 640, TextureFormat.RGBA32, false);
                    capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                    Require(capture.GetPixels32().Count(p => p.a > 32) > 5000, "actual UILite-loaded weapon GPU coverage");
                    File.WriteAllBytes(ProfessionValidationWorkspace.Output("ui-lite-" + skin + ".png"), capture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; if (capture) Object.DestroyImmediate(capture); RenderTexture.ReleaseTemporary(target); }
            }
        }
        private static SpineTextureAssetProxy Proxy(string guid)
        { var proxy = new SpineTextureAssetProxy(); JsonUtility.FromJsonOverwrite("{\"mAssetGuid\":\"" + guid + "\"}", proxy); return proxy; }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false); ProfessionAppearanceTextureBindings.Current = null;
            foreach (Object value in owned) if (value) Object.DestroyImmediate(value);
            owned.Clear(); EditorApplication.Exit(code);
        }
    }
}
