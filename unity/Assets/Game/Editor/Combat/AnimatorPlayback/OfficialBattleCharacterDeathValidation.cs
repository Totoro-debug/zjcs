using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleCharacterDeathValidation
    {
        private const string Key = "Zjcs.Battle.CharacterDeathValidation";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original character death verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += (_, __, type) =>
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            new GameObject("Original character death verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            try
            {
                var catalog = OfficialBattleCharacterDeathCatalog.LoadDefault();
                Require(catalog.Find(3100).Normal == catalog.Find(3991).Normal
                    && !catalog.Find(3102).Normal && !catalog.Find(3102).OnStart && catalog.Find(3102).TryOnStart,
                    "exact original role bindings, including null graphs");
                var resources = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
                var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
                var resolver = new ProfessionAppearanceResolver(ProfessionAppearanceTables.Load(
                    Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text));
                using var textures = new OfflineProfessionSceneTextureResources(resources);
                var previous = ProfessionAppearanceTextureBindings.Current;
                ProfessionAppearanceTextureBindings.Current = textures;
                try
                {
                    resources.Loader.TryInit();
                    foreach (var model in resources.Models)
                    foreach (string group in new[] { "100", "200" })
                    {
                        var root = new GameObject("Current original death actor");
                        root.transform.localScale = Vector3.one * .3f;
                        var native = root.AddComponent<ProfessionNativeSceneRenderer>(); native.Configure(resources);
                        native.skeletonDataAsset = model.Asset; native.Initialize(false, false);
                        var materials = new Dictionary<Material, Material>(); var loads = new List<int>();
                        native.OnGetUpdatedSharedMaterials += array =>
                        {
                            for (int i = 0; i < array.Length; i++)
                            {
                                var original = array[i];
                                if (!materials.TryGetValue(original, out var material))
                                {
                                    material = new Material(original);
                                    int index = resources.Loader.PlaceholderTextureIndexDict[material.mainTexture];
                                    Material unused = null; resources.Loader.RequestLoadMaterialTextures(material, ref unused);
                                    resources.Loader.AddLoadedTextureRef(index); loads.Add(index); materials.Add(original, material);
                                    Require(material.mainTexture && !material.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal),
                                        "real current Low texture demand");
                                }
                                array[i] = material;
                            }
                        };
                        var skin = native.State.CreateSkin("Current original default-appearance death probe");
                        var sex = model.Sex == "male" ? Sex.Male : Sex.Female;
                        var profession = (Profession)(group == "100" ? 1 : 10);
                        var appearance = resolver.BuildAppearanceRuntimeData(sex, profession,
                            resolver.BuildDefaultAppearanceData(config, sex, profession), config.GetAppendSkins(AppendSkinType.Game));
                        foreach (string name in appearance.AppendSkins.Concat(appearance.SkinMap
                            .Where(pair => config.GetPartConfig(pair.Key) == null).Select(pair => pair.Value)))
                            skin.AddSkin(native.State.Data.FindSkin(name));
                        native.State.SetSkin(skin); native.State.SetToSetupPose(); native.State.UpdateCache();
                        var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Combat/GameAnimators/Anim/"
                            + (model.Sex == "female" ? "Female_" : "Male_") + (group == "100" ? "Zhanshi" : "Fashi") + "_Controller");
                        var controller = root.AddComponent<AnimationController>(); controller.myAnimator = animator;
                        controller.NotCreateGraph = true; controller.Init(); animator.Update(0);
                        var physics = new ProfessionNativePlayerPhysicsPose(native.State.Source);
                        using var scope = new OfflineBattleNativePlayableScope(root.transform, null);
                        bool dead = false;
                        using var death = new OfficialBattleCharacterDeathPlayback(root, () => dead, scope, catalog.Find(3100));
                        void AdvanceFrames(float delta, int frames) => Advance(scope, animator, delta, frames, () =>
                        {
                            // This death-graph probe samples stable controller motions only.
                            // Keep physics advancing through those frames without pretending to test transition mixing.
                            if (!animator.IsInTransition(0)) Draw(native, animator, physics, delta);
                        });
                        try
                        {
                            AdvanceFrames(.01f, 2);
                            Draw(native, animator, physics);
                            var bounds = native.GetComponent<MeshRenderer>().bounds;
                            byte[] idle = Capture(bounds, model.Sex + "-" + group + "-idle");
                            dead = true; death.OnDie(true);
                            Require(!animator.GetBool("die") && !animator.GetBool("immediately_die"), "death waits for Animator initialization");
                            death.OnAnimatorInited();
                            Require(animator.GetBool("immediately_die") && !animator.GetBool("die"), "initially dead uses the original on-start graph");
                            AdvanceFrames(.02f, 60); Draw(native, animator, physics);
                            Require(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die"), "actual on-start death motion");
                            Require(!idle.SequenceEqual(Capture(bounds, model.Sex + "-" + group + "-on-start")), "current native corpse pixels differ from idle under the same camera");
                            dead = false; death.OnDie(false); AdvanceFrames(.02f, 60);
                            Require(!animator.GetBool("immediately_die") && !animator.GetBool("die"), "revival restores original graph parameters");
                            controller.Pause(); Require(animator.speed == 0, "original pause probe");
                            dead = true; death.OnDie(true);
                            Require(animator.speed > 0 && animator.GetBool("die") && !animator.GetBool("immediately_die"),
                                "normal death resumes all original pauses and selects the normal graph");
                            AdvanceFrames(.02f, 60); Draw(native, animator, physics);
                            Require(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die"), "actual normal death motion");
                            Capture(bounds, model.Sex + "-" + group + "-normal");
                            death.ForcePlayDiePlayable(); AdvanceFrames(.02f, 2);
                            Require(animator.GetBool("die"), "force stop-then-play retains the current original death request");
                            dead = false; death.OnDie(false); AdvanceFrames(.02f, 60);
                            Require(!animator.GetBool("die"), "replaced graph restores parameters after revival");
                            using var empty = new OfficialBattleCharacterDeathPlayback(root, () => true, scope, catalog.Find(3102));
                            empty.OnAnimatorInited(); empty.OnDie(true); empty.ForcePlayDiePlayable(); AdvanceFrames(.02f, 2);
                            Require(!animator.GetBool("die") && !animator.GetBool("immediately_die"), "3102 null bindings stay empty");
                            yield return null;
                        }
                        finally
                        {
                            death.Dispose(); scope.Dispose(); root.SetActive(false); native.Release(); Object.Destroy(root);
                            foreach (var material in materials.Values) Object.Destroy(material);
                            foreach (int index in loads) resources.Loader.RemoveLoadedTextureRef(index);
                        }
                        yield return null;
                    }
                }
                finally { ProfessionAppearanceTextureBindings.Current = previous; }
                Require(errors == 0, "Unity reported runtime errors");
                Debug.Log("Original character death graphs passed: current male/female warrior/mage controllers, original default body/Game append skin geometry and actual Low textures under fixed camera framing, normal/on-start death motions, initialization gate, pause release, force replay, revival parameter restoration and 3102 null bindings. Independent weapon parts/dyes, full production character prefab, transition-frame pose mixing, shared scene lifecycle and Windows Player remain separate.");
                SessionState.SetInt(Key + ".result", 0);
            }
            finally { EditorApplication.isPlaying = false; }
        }
        private static void Advance(OfflineBattleNativePlayableScope scope, Animator animator, float delta, int frames, Action sample)
        { for (int i = 0; i < frames; i++) { scope.Tick(delta); scope.LateTick(); animator.Update(delta); sample(); } }
        private static void Draw(ProfessionNativeSceneRenderer native, Animator animator, ProfessionNativePlayerPhysicsPose physics, float delta = 0)
        {
            Require(!animator.IsInTransition(0), "draw probe requires the actual controller to have completed its transition");
            var clips = animator.GetCurrentAnimatorClipInfo(0); Require(clips.Length == 1, "one actual current motion");
            var state = animator.GetCurrentAnimatorStateInfo(0);
            physics.Apply(native.State, new ProfessionNativeAnimationChannels(native.State.Source, clips[0].clip.name, originalBezierSegments: true),
                Mathf.Max(0, state.normalizedTime * clips[0].clip.length), state.loop, delta);
            native.LateUpdateMesh();
        }
        private static byte[] Capture(Bounds bounds, string name)
        {
            var go = new GameObject("Current native death camera"); var camera = go.AddComponent<Camera>();
            camera.aspect = .75f;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .75f) * 1.8f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var rt = new RenderTexture(384, 512, 24); var texture = new Texture2D(384, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 384, 512), 0, 0); texture.Apply();
                Require(texture.GetPixels32().Count(pixel => pixel.r > 16 || pixel.g > 16 || pixel.b > 16) > 100,
                    "actual current native camera rendered no visible pixels");
                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), bytes);
                return bytes;
            }
            finally { RenderTexture.active = previous; Object.Destroy(go); Object.Destroy(rt); Object.Destroy(texture); }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original character death: " + message); }
        private sealed class Runner : MonoBehaviour { }
    }
}
