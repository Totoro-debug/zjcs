using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeCharacterResourceValidation
    {
        [Serializable] private sealed class Fixture { public Case[] cases; }
        [Serializable] private sealed class Case
        {
            public string sex, path, guid, split_path, split_guid, metadata;
            public float scale, defaultMix;
            public string[] zSlots, fromAnimation, toAnimation, strings, unsplit, actualMain, missingUnsplit;
            public float[] duration;
            public Content[] contents;
        }
        [Serializable] private sealed class Content { public string name, guid; }
        private const string Active = "Zjcs.ProfessionNativeCharacterResources.Active";
        private static readonly List<Object> owned = new List<Object>();
        private static bool ready;
        static ProfessionNativeCharacterResourceValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native character resource validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-character-resources-validation.json")));
            var catalog = Resources.Load<ProfessionNativeCharacterCatalog>("ProfessionUI/character-resources");
            var textures = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures");
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var rules = AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>("Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("ProfessionUI/appearance-tables").text);
            var resolver = new ProfessionAppearanceResolver(tables);
            Require(catalog && catalog.Count == 4 && fixture.cases.Length == 2, "four actual original resource path/GUID bindings");
            int partCalls = 0, splitCount = 0, poolCount = 0, images = 0;
            var resources = new OfflineProfessionCharacterResources(config, catalog, (h, s) => partCalls++, (h, a) => partCalls++);
            var contact = Own(new Texture2D(1024, 1024, TextureFormat.RGBA32, false, true));
            foreach (var expected in fixture.cases)
            {
                var asset = catalog.LoadPath(expected.path) as ProfessionNativeSkeletonAsset;
                var split = catalog.LoadPath(expected.split_path) as SpineAnimationSplitBinarySO;
                Require(asset && split && catalog.LoadGuid(expected.guid) == asset && catalog.LoadGuid(expected.split_guid) == split, "actual original resource identity");
                Require(asset.SourcePath == expected.path && asset.SourceGuid == expected.guid && asset.Sex == expected.sex && asset.OriginalMetadata == expected.metadata, "complete original metadata retained");
                Require(asset.Data.DataScale == expected.scale && asset.DefaultMix == expected.defaultMix && asset.ZSlots.SequenceEqual(expected.zSlots) && asset.FromAnimation.SequenceEqual(expected.fromAnimation) && asset.ToAnimation.SequenceEqual(expected.toAnimation) && asset.MixDurations.SequenceEqual(expected.duration), "original scale/mix/source z-slot settings");
                Require(ReferenceEquals(asset.Data, asset.Data) && asset.Data.Sex == expected.sex, "stable native data behind actual asset");
                Require(split.StringArray.SequenceEqual(expected.strings) && split.ListNoSplitAnimationNames.SequenceEqual(expected.unsplit), "complete original split string pool and unsplit order");
                Require(split.ListAnimationContent.Count == expected.contents.Length, "all original split animation records");
                for (int i = 0; i < expected.contents.Length; i++)
                {
                    var item = expected.contents[i]; var actual = split.ListAnimationContent[i];
                    Require(actual.ContentName == item.name && actual.TextAsset.AssetGuid == item.guid && split.GetAnimationAssetGuid(item.name) == item.guid, "original content order and lazy GUID dictionary lookup");
                }
                Require(new HashSet<string>(expected.contents.Select(c => c.name).Concat(expected.actualMain)).SetEquals(asset.Data.AnimationNames), "actual main and split binary animations match complete native payload");
                Require(expected.unsplit.Where(n => !expected.actualMain.Contains(n)).SequenceEqual(expected.missingUnsplit) && expected.missingUnsplit.All(n => !asset.Data.AnimationNames.Contains(n)), "original declared-but-absent unsplit entries remain distinct from actual animation data");
                Require(split.GetAnimationAssetGuid("missing-original-animation") == null && SpineAnimationSplitBinarySO.GetSoFileName(asset.name) == Path.GetFileName(expected.split_path), "original missing lookup and resource filename");
                splitCount += expected.contents.Length; poolCount += expected.strings.Length;
                var handle = resources.LoadAsset(expected.path); Require(handle.Result == asset, "actual native resource request");
                handle.Dispose(); handle.Dispose(); Reject<ObjectDisposedException>(() => { var ignored = handle.Result; });
                using (var next = resources.LoadAsset(expected.path)) Require(next.Result == asset, "request release preserves catalog-owned asset for other users");
                var go = Own(new GameObject("Original character actual resources " + expected.sex));
                var endpoint = go.AddComponent<AppearanceEndpoint>(); endpoint.Textures = textures;
                var bindings = new AppearanceBindings(endpoint); endpoint.Configure(bindings, resources);
                var character = go.AddComponent<AppearanceCharacter>(); character.Configure(resources);
                Sex sex = expected.sex == "male" ? Sex.Male : Sex.Female;
                foreach (int profession in new[] { 1, 10 })
                {
                    var defaults = resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession);
                    var player = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                    player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = profession;
                    foreach (var p in defaults.SkinMap) player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)p.Key, Id = p.Value.SkinId });
                    foreach (var p in defaults.DyeMap) player.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)p.Key, ColorId = p.Value.ColorId, Hue = p.Value.Hsv.H, Saturation = p.Value.Hsv.S, Value = p.Value.Hsv.V });
                    var graph = new OfflineProfessionAppearanceBindings(17, () => player, _ => profession == 1 ? 200 : 201, tables, config, rules);
                    ProfessionAppearancePreview.SetProfessionAppearance(graph, config, character, sex, profession, graph.AppearanceData, AppendSkinType.UI);
                    Require(endpoint.SkeletonDataAsset == asset && endpoint.SpineAnimationSplitBinarySo == split && endpoint.State.Source == asset.Data, "original character loads both actual native resources and drives real skeleton");
                    Require(character.TryGetSkinName(SkinType.body, out var body) && body == "body_" + (profession == 1 ? "100" : "200") + "_" + (sex == Sex.Male ? "M" : "F") + "_01", "actual preview clothing after original resource loading");
                    endpoint.State.EvaluateUnmixedIdleBeforePhysics(profession == 1 ? "100/idle_01" : "200/idle_01", 0);
                    using (var batches = new ProfessionNativeMeshBatches(asset.Data.SlotCount))
                    {
                        batches.Build(endpoint.State.Pose, endpoint.State.CreateDrawSlots());
                        Raster(endpoint, batches, contact, images++);
                    }
                }
                character.Release();
                Object.DestroyImmediate(go);
            }
            Require(images == 4 && partCalls == 0, "four actual core previews without installing a part handler");
            Reject<InvalidOperationException>(() => catalog.LoadPath("unconverted"));
            Reject<InvalidOperationException>(() => catalog.LoadGuid("unconverted"));
            Reject<ArgumentException>(() => resources.LoadAsset(""));
            resources.SetPartSkins(null, new Dictionary<SkinType, string>()); resources.ReleaseParts(null, true);
            Require(partCalls == 2, "required part callbacks are explicitly forwarded");
            File.WriteAllBytes(ProfessionValidationWorkspace.Output("native-character-resource-previews.png"), contact.EncodeToPNG());
            Debug.Log("Actual original player resources passed Direct3D Play Mode: four path/GUID assets, complete source metadata/settings, " + poolCount + " original pooled strings, " + splitCount + " original split lookup records, original lazy lookup/missing branches, independent request release, four original preview/character/appearance native data flows and four original-texture GPU diagnostics. Full graphic, separate parts, physics/mixing, native profession page/input/save and Windows Player remain required.");
        }
        private static void Raster(AppearanceEndpoint endpoint, ProfessionNativeMeshBatches batches, Texture2D contact, int index)
        {
            var bounds = batches.Batches[0].Mesh.bounds;
            foreach (var batch in batches.Batches) { Require(batch.Blend == 0, "core original normal blend"); bounds.Encapsulate(batch.Mesh.bounds); }
            float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    foreach (var batch in batches.Batches) commands.DrawMesh(batch.Mesh, fit, endpoint.PageMaterial(batch.Page));
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target;
                var capture = Own(new Texture2D(512, 512, TextureFormat.RGBA32, false, true));
                capture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); capture.Apply();
                Require(capture.GetPixels32().Count(p => p.a > 8) > 500, "actual source resource-bound actor raster");
                contact.SetPixels((index % 2) * 512, (1 - index / 2) * 512, 512, 512, capture.GetPixels()); contact.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private sealed class AppearanceBindings : IProfessionAppearanceSpineBindings
        {
            private readonly AppearanceEndpoint endpoint;
            internal AppearanceBindings(AppearanceEndpoint endpoint) { this.endpoint = endpoint; }
            public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner) => endpoint;
            public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet) => ((ProfessionNativeSkeletonAsset)asset).Data.SkinCatalog;
            public IProfessionAppearanceSkin CreateSkin(string name) => endpoint.State.CreateSkin(name);
        }
        public sealed class AppearanceEndpoint : AppearanceSpine, IProfessionAppearanceSkeletonComponent
        {
            internal ProfessionNativeTextureCatalog Textures;
            internal ProfessionNativeAppearanceSkeleton State;
            private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            public IProfessionAppearanceSkeleton Skeleton => State;
            public override YooAssetTextureLoader TextureLoader => null;
            protected override void InitSkeleton(ScriptableObject asset)
            { State = new ProfessionNativeAppearanceSkeleton(((ProfessionNativeSkeletonAsset)asset).Data); }
            internal Material PageMaterial(string page)
            {
                if (materials.TryGetValue(page, out var material)) return material;
                var texture = Textures.Find(State.Source.Sex, page);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
                Require(shader && shader.isSupported, "actual converted original UI shader");
                material = Own(new Material(shader)); material.SetTexture("_MainTex", texture.Main); material.SetTexture("_DyeTex", texture.Dye);
                materials.Add(page, material); return material;
            }
            protected override Material GetMaterial(string skinName)
            {
                var skin = (ProfessionNativeSkin)State.Source.SkinCatalog.FindSkin(skinName);
                var pages = skin.Entries.Select(e => e.Attachment).OfType<ProfessionNativeRenderAttachment>().Select(a => a.Page).Distinct().ToArray();
                Require(pages.Length == 1, "verified single-page original core dye skin");
                return PageMaterial(pages[0]);
            }
        }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native character resources: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            foreach (var item in owned) if (item) Object.DestroyImmediate(item); owned.Clear();
            EditorApplication.Exit(code);
        }
    }
}
