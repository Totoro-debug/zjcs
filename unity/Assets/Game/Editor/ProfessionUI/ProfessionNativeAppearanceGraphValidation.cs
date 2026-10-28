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
    public static class ProfessionNativeAppearanceGraphValidation
    {
        [Serializable] private sealed class Fixture { public Frame[] cases; }
        [Serializable] private sealed class Frame { public string sex, animation; public string[] skins; public float time; public World[] expected; }
        [Serializable] private sealed class World { public int bone; public float a, b, c, d, x, y; }
        [Serializable] private sealed class Resolved { public Preview[] previews; }
        [Serializable] private sealed class Preview { public string sex, animation; public int profession; public string[] skins; public Dye[] dyes; }
        [Serializable] internal sealed class Dye { public string skin; public Channel[] channels; }
        [Serializable] internal sealed class Channel { public int channel; public float h, s, v; }
        // This exports the original resolver/preview/character result, including
        // default facial skins, source append filtering and source dye offsets.
        public static void ExportPreview()
        {
            ProfessionValidationWorkspace.AssertIsolatedProject();
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var rules = AssetDatabase.LoadAssetAtPath<AppearanceAnimNameCombineRule>("Assets/Official/ProfessionUI/Portrait/AppearanceAnimNameCombineRule.asset");
            var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("ProfessionUI/appearance-tables").text);
            var resolver = new ProfessionAppearanceResolver(tables);
            var result = new List<Preview>();
            foreach (Sex sex in new[] { Sex.Male, Sex.Female })
            foreach (int profession in new[] { 1, 10 })
            {
                var defaults = resolver.BuildDefaultAppearanceData(config, sex, (Profession)profession);
                var player = new OfflinePlayerProgress { InitialWeaponType = profession == 1 ? 200 : 201 };
                player.Wardrobe.Sex = (int)sex; player.Wardrobe.Profession = profession;
                foreach (var pair in defaults.SkinMap) player.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)pair.Key, Id = pair.Value.SkinId });
                foreach (var pair in defaults.DyeMap) player.Wardrobe.Dyes.Add(new OfflineAppearanceDye { Type = (int)pair.Key, ColorId = pair.Value.ColorId, Hue = pair.Value.Hsv.H, Saturation = pair.Value.Hsv.S, Value = pair.Value.Hsv.V });
                var graph = new OfflineProfessionAppearanceBindings(17, () => player, _ => profession == 1 ? 200 : 201, tables, config, rules);
                var node = new GameObject("Original resolved preview export");
                try
                {
                    var capture = node.AddComponent<PreviewCapture>();
                    var character = node.AddComponent<AppearanceCharacter>(); character.Configure(new PreviewBindings(config));
                    ProfessionAppearancePreview.SetProfessionAppearance(graph, config, character, sex, profession, graph.AppearanceData, AppendSkinType.UI);
                    result.Add(new Preview { sex = sex == Sex.Male ? "male" : "female", profession = profession,
                        animation = profession == 1 ? "100/idle_01" : "200/idle_01", skins = capture.Skins, dyes = capture.Dyes.ToArray() });
                }
                finally { Object.DestroyImmediate(node); }
            }
            File.WriteAllText(ProfessionValidationWorkspace.Output("native-preview-appearance.json"), JsonUtility.ToJson(new Resolved { previews = result.ToArray() }, true));
            Debug.Log("Exported four actual original default profession previews and channel dye offsets.");
        }
        public sealed class PreviewCapture : MonoBehaviour, IAppearanceComponent
        {
            public string[] Skins; internal readonly List<Dye> Dyes = new List<Dye>();
            public bool Valid => true;
            public void Init(ScriptableObject main, ScriptableObject split) { }
            public void SetSkins(List<string> skins) { Skins = skins.ToArray(); }
            public void SetDye(string name, Dictionary<int,HsvChange> values)
            { Dyes.Add(new Dye { skin = name, channels = values.Select(p => new Channel { channel = p.Key, h = p.Value.OffsetH, s = p.Value.OffsetScaleS, v = p.Value.OffsetScaleV }).ToArray() }); }
            public bool TryGetMaterial(string name, out Material material) { material = null; return false; }
            public void Release(bool all = false) { Dyes.Clear(); }
        }
        private sealed class PreviewBindings : IProfessionAppearanceCharacterBindings
        {
            public AppearanceConfig Config { get; }
            internal PreviewBindings(AppearanceConfig config) { Config = config; }
            public IProfessionAppearanceCharacterAsset LoadAsset(string path) => new PreviewAsset(Config);
            public void SetPartSkins(Component handler, Dictionary<SkinType,string> skins) => throw new InvalidOperationException("Separate source part renderer is outside the core preview export.");
            public void ReleaseParts(Component handler, bool all) => throw new InvalidOperationException("Unexpected separate part renderer.");
        }
        private sealed class PreviewAsset : IProfessionAppearanceCharacterAsset
        { public ScriptableObject Result { get; } internal PreviewAsset(ScriptableObject value) { Result = value; } public void Dispose() { } }
        private const string Active = "Zjcs.ProfessionNativeAppearanceGraph.Active";
        private static readonly List<Object> owned = new List<Object>();
        private static bool ready;
        private static int matrices, vertexCount, images;
        private static float maximumError;
        static ProfessionNativeAppearanceGraphValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native appearance graph validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Native application appearance graph passed actual Direct3D Play Mode: original AppearanceSpine owns combined-skin reuse and updates actual packaged skin/bone/slot objects; 96 pre-physics source frames, " + matrices + " world matrices, maximum difference " + maximumError.ToString("R") + "; " + vertexCount + " batch vertices; " + images + " original-texture GPU diagnostic captures. Slot identity, source fallback, draw order and bounding interfaces connect to actual geometry. No physics, mixed animation, original SDK pose, production graphic/native page input/save or Windows Player claim.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            matrices = vertexCount = images = 0; maximumError = 0;
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("idle-transform-validation.json")));
            Require(fixture.cases.Length == 96, "all independently verified combined source frames");
            var textures = Resources.Load<ProfessionNativeTextureCatalog>("ProfessionUI/character-textures"); Require(textures, "actual original texture resources");
            foreach (var group in fixture.cases.GroupBy(c => c.sex))
            {
                var source = ProfessionNativeSkeletonData.LoadResource(group.Key);
                var state = new ProfessionNativeAppearanceSkeleton(source);
                var go = Own(new GameObject("Actual original application to native " + group.Key));
                var probe = go.AddComponent<AppearanceProbe>(); probe.State = state; probe.Configure(new Bindings(probe), null);
                var identity = Own(ScriptableObject.CreateInstance<AppearanceConfig>());
                // This marker exercises the application's existing asset identity
                // branch. Actual skeleton input is the packaged native source;
                // the production asset/resource-loader boundary is still separate.
                probe.Init(identity, null);
                int skinEvents = 0; probe.OnSkinUpdate = () => skinEvents++;
                IProfessionAppearanceSkin combined = null;
                using (var batches = new ProfessionNativeMeshBatches(source.SlotCount))
                {
                    foreach (var frame in group)
                    {
                        probe.SetSkins(frame.skins.ToList());
                        if (combined == null) combined = state.Skin; else Require(ReferenceEquals(combined, state.Skin), "original combined skin object reuse");
                        Require(probe.Skins.SequenceEqual(frame.skins), "actual original application merge/name order");
                        for (int i = 0; i < state.SlotCount; i++)
                            Require(ReferenceEquals(state.GetAttachment(i), source.SkinCatalog.ResolveAttachment((ProfessionNativeSkin)state.Skin, i, state.SetupAttachmentName(i))), "original setup attachment and default-skin fallback identity");
                        state.EvaluateUnmixedIdleBeforePhysics(frame.animation, frame.time);
                        foreach (var expected in frame.expected)
                        {
                            var m = state.Pose.World(expected.bone); matrices++;
                            Near(expected.a, m.m00); Near(expected.b, m.m01); Near(expected.c, m.m10); Near(expected.d, m.m11); Near(expected.x, m.m03); Near(expected.y, m.m13);
                        }
                        var draw = state.DrawOrder;
                        for (int i = 0; i < state.SlotCount; i++)
                        {
                            int slot = state.SlotPose.DrawSlot(i); var view = state.FindSlot(state.SlotPose.Name(slot));
                            Require(ReferenceEquals(draw[i], view) && ReferenceEquals(view.Attachment, state.GetAttachment(slot)), "shared real slot identity in source draw order");
                            Require(ReferenceEquals(((IProfessionNativeAttachmentSlot)view).Pose, state.Pose), "bounding interface uses actual bone pose");
                            Require(ReferenceEquals(view.Attachment, state.GetAttachment(slot, state.SlotPose.Attachment(slot))), "actual timeline name resolves selected/default source skin");
                        }
                        batches.Build(state.Pose, state.CreateDrawSlots());
                        Require(batches.Batches.Count > 0, "actual nonempty source skin geometry");
                        vertexCount += batches.Batches.Sum(b => b.Mesh.vertexCount);

                    }
                    var resolved = JsonUtility.FromJson<Resolved>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-preview-appearance.json")));
                    foreach (var preview in resolved.previews.Where(p => p.sex == group.Key))
                    {
                        probe.SetSkins(preview.skins.ToList());
                        Require(ReferenceEquals(combined, state.Skin), "resolved original preview reuses combined skin");
                        foreach (float time in new[] { 0f, .5f, 1f, 2f })
                        {
                            state.EvaluateUnmixedIdleBeforePhysics(preview.animation, time);
                            batches.Build(state.Pose, state.CreateDrawSlots());
                            if (time == 0) { Raster(probe, batches, textures, preview); images++; }
                        }
                    }
                }
                Require(skinEvents == group.Count() + 2, "original skin event after native setup");
                var wrong = new ProfessionNativeAppearanceSkeleton(ProfessionNativeSkeletonData.LoadResource(group.Key == "male" ? "female" : "male"));
                Reject<ArgumentException>(() => state.SetSkin(wrong.CreateSkin("other source")));
                Reject<NotSupportedException>(() => state.SetSlotAttachment(0, new object()));
                Reject<NotSupportedException>(() => state.EvaluateUnmixedIdleBeforePhysics("home/wakeUp_01", 0));
                Object.DestroyImmediate(go);
            }
            Require(images == 4, "male/female warrior/mage native diagnostic rasters");
        }
        private static void Raster(AppearanceProbe probe, ProfessionNativeMeshBatches batches, ProfessionNativeTextureCatalog textures, Preview preview)
        {
            var state = probe.State;
            string name = preview.sex + "-" + (preview.profession == 1 ? "warrior" : "mage");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            Require(shader && shader.isSupported, "verified converted original UI shader");
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var bounds = batches.Batches[0].Mesh.bounds;
            foreach (var batch in batches.Batches)
            {
                Require(batch.Blend == 0, "verify additional original blend material in native core raster"); bounds.Encapsulate(batch.Mesh.bounds);
                if (materials.ContainsKey(batch.Page)) continue;
                var page = textures.Find(state.Source.Sex, batch.Page); var material = Own(new Material(shader));
                material.SetTexture("_MainTex", page.Main); material.SetTexture("_DyeTex", page.Dye); materials.Add(batch.Page, material);
            }
            probe.SkinMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string skinName in preview.skins)
            {
                var skin = (ProfessionNativeSkin)state.Source.SkinCatalog.FindSkin(skinName);
                var pages = skin.Entries.Select(e => e.Attachment).OfType<ProfessionNativeRenderAttachment>().Select(a => a.Page).Distinct().ToArray();
                Require(pages.Length <= 1, "verify original material binding for multi-page skin " + skinName);
                if (pages.Length == 1 && materials.TryGetValue(pages[0], out var material)) probe.SkinMaterials.Add(skinName, material);
            }
            foreach (var dye in preview.dyes)
            {
                Require(probe.SkinMaterials.ContainsKey(dye.skin), "resolved original dye material " + dye.skin);
                probe.SetDye(dye.skin, dye.channels.ToDictionary(c => c.channel, c => new HsvChange { OffsetH = c.h, OffsetScaleS = c.s, OffsetScaleV = c.v }));
            }
            float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f; Require(extent > 0, "actual native geometry extent");
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    foreach (var batch in batches.Batches) commands.DrawMesh(batch.Mesh, fit, materials[batch.Page]);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; var capture = Own(new Texture2D(640, 640, TextureFormat.RGBA32, false, true));
                capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                int visible = capture.GetPixels32().Count(p => p.a > 8); Require(visible > 500, "actual original raster has visible source pixels");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("native-prephysics-" + name + ".png"), capture.EncodeToPNG());
                Debug.Log("Actual pre-physics native character diagnostic " + name + ": " + visible + " source pixels; " + batches.Batches.Count + " batches, " + bounds.size + " extent.");
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private sealed class Bindings : IProfessionAppearanceSpineBindings
        {
            private readonly AppearanceProbe probe;
            internal Bindings(AppearanceProbe probe) { this.probe = probe; }
            public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner) { Require(owner == probe, "original owner identity"); return probe; }
            public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet) => probe.State.Source.SkinCatalog;
            public IProfessionAppearanceSkin CreateSkin(string name) => probe.State.CreateSkin(name);
        }
        private sealed class AppearanceProbe : AppearanceSpine, IProfessionAppearanceSkeletonComponent
        {
            internal ProfessionNativeAppearanceSkeleton State;
            public IProfessionAppearanceSkeleton Skeleton => State;
            public override YooAssetTextureLoader TextureLoader => null;
            protected override void InitSkeleton(ScriptableObject asset) { }
            internal Dictionary<string, Material> SkinMaterials;
            protected override Material GetMaterial(string skin) => SkinMaterials != null && SkinMaterials.TryGetValue(skin, out var material) ? material : null;
        }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Near(float expected, float actual)
        {
            float error = Mathf.Abs(expected - actual); maximumError = Mathf.Max(maximumError, error);
            Require(!float.IsNaN(actual) && error <= .001f + .000007f * Mathf.Abs(expected), "actual native pre-physics world scalar");
        }
        private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native appearance graph: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            foreach (var value in owned) if (value) Object.DestroyImmediate(value); owned.Clear();
            EditorApplication.Exit(code);
        }
    }
}
