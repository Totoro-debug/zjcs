using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativeAnimationBindingAcceptance
    {
        private const string Active = "Zjcs.PartnerNativeAnimationBinding";
        static OfficialPartnerNativeAnimationBindingAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner application animation binding validation").AddComponent<PartnerNativeAnimationBindingRunner>();
            };
        }
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.IsNullOrEmpty(workspace) || !project.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an isolated partner animation binding validation project.");
            OfficialPartnerNativeBattleRuntimeAcceptance.Import();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_ANIMATION_BINDING_PASSED: original entity-node application controller, Awake, original Animator playables and controller lifetime. Full skill graphs and formal battle remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeAnimationBindingRunner : MonoBehaviour
    {
        [Serializable] private sealed class Catalog { public Partner[] partners; public Skill[] skills; }
        [Serializable] private sealed class Partner { public int npc_id; public Selection[] skills; }
        [Serializable] private sealed class Selection { public int EcEntityId; public int[] RandomSkillEntityId; }
        [Serializable] private sealed class Skill { public int skill_entity_id; public Variant[] variants; }
        [Serializable] private sealed class Variant { public string field; public OfficialBattleSkillPlayableCue[] playables; }
        private int errors;
        private IEnumerator Start()
        {
            Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialPartnerNativeAnimationBindingAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeAnimationBindingAcceptance.Finish(errors == 0 ? null : new InvalidDataException("Partner animation binding errors: " + errors));
        }
        private IEnumerator Check()
        {
            var data = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("partner-native-playables").text);
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Bound original partner camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .2f);
            var target = new RenderTexture(768, 1365, 24); camera.targetTexture = target;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            int cues = 0, variants = 0;
            foreach (var partner in data.partners)
            foreach (int key in partner.skills.SelectMany(row => new[] { row.EcEntityId }.Concat(row.RandomSkillEntityId ?? Array.Empty<int>())).Distinct())
            foreach (var variant in data.skills.Single(row => row.skill_entity_id == key).variants)
            {
                using var actor = new PartnerNativeBattleActor(catalog, partner.npc_id, null, "Original bound partner " + partner.npc_id);
                var original = actor.Animator.runtimeAnimatorController;
                using (var binding = new PartnerNativeBattleAnimationBinding(actor))
                {
                    var controller = binding.Controller;
                    if (!controller.myInited || !controller.InitOnAwake || !controller.CreateOverrideController || controller.NotCreateGraph
                        || controller.gameObject != actor.Attachments.Agent.gameObject || controller.myAnimator != actor.Animator
                        || !(actor.Animator.runtimeAnimatorController is AnimatorOverrideController))
                        throw new InvalidDataException("Original application controller was not initialized on its original node.");
                    actor.Root.gameObject.SetActive(false); actor.Root.gameObject.SetActive(true);
                    if (!controller.myInited) throw new InvalidDataException("Original disable branch unexpectedly reset the application controller.");
                    var center = new MyPlayableCenter();
                    var live = new List<MyPlayableAnimator>();
                    float elapsed = 0;
                    try
                    {
                        foreach (var cue in variant.playables)
                        {
                            while (elapsed + .000001f < cue.time)
                            { float delta = Mathf.Min(1f / 60f, cue.time - elapsed); Tick(delta); elapsed += delta; }
                            if (cue.kind != 0 || !OfficialBattleAnimatorPlayback.Supports(actor.Animator, cue))
                                throw new InvalidDataException("Review current partner original Animator cue type or parameters.");
                            var go = new GameObject("Original skill Animator node"); go.transform.SetParent(actor.Root, false);
                            var playable = go.AddComponent<MyPlayableAnimator>(); live.Add(playable);
                            playable.MyPlayableCenter = center; playable.myApplyComponent = controller;
                            playable.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
                            playable.myFadeoutTrigerList = new List<MyPlayableTrigger>(); playable.myEndTrigerList = new List<MyPlayableTrigger>();
                            playable.myBoolParam = cue.my_bool_param; playable.myBoolValue = cue.my_bool_value;
                            playable.myFloatParam = cue.my_float_param; playable.myFloatValue = cue.my_float_value;
                            playable.IntParam = cue.int_param; playable.IntValue = cue.int_value; playable.myTriggerParam = cue.trigger_param;
                            playable.PreStateNameAllowList = cue.pre_state_name_allow_list ?? new List<string>(); playable.myDuration = cue.my_duration;
                            playable.LayerName = cue.layer_name; playable.LayerWeight = cue.layer_weight;
                            playable.myEndResumeParam = cue.end_resume_param; playable.mySpeed = cue.speed;
                            playable.UpdateHashMapString(); playable.Init(binding.Source, binding.Source); playable.TryPlay();
                            actor.Advance(0);
                            var authored = (AnimatorController)original;
                            var expected = authored.layers[0].stateMachine.states.SelectMany(row => row.state.transitions)
                                .Concat(authored.layers[0].stateMachine.anyStateTransitions)
                                .Where(row => row.conditions.Any(condition => condition.parameter == cue.trigger_param))
                                .Select(row => row.destinationState).Where(row => row != null).ToArray();
                            bool entered = false;
                            for (int frame = 0; frame < 12; frame++)
                            {
                                var current = actor.Animator.GetCurrentAnimatorStateInfo(0); var next = actor.Animator.GetNextAnimatorStateInfo(0);
                                if (expected.Any(state => current.shortNameHash == Animator.StringToHash(state.name)
                                    || actor.Animator.IsInTransition(0) && next.shortNameHash == Animator.StringToHash(state.name))) entered = true;
                                Tick(1f / 60f); elapsed += 1f / 60f;
                            }
                            if (!entered) throw new InvalidDataException($"Original bound trigger did not enter authored state: {partner.npc_id}/{key}/{cue.trigger_param}");
                            cues++;
                        }
                        variants++;
                        Bounds? visible = null;
                        foreach (var renderer in actor.Renderers)
                        {
                            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                            Vector3[] vertices = mesh.vertices; Color32[] colors = mesh.colors32;
                            for (int index = 0; index < vertices.Length; index++)
                            {
                                Vector3 point = renderer.transform.TransformPoint(vertices[index]);
                                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                                    throw new InvalidDataException("Bound partner geometry is not finite.");
                                if (colors.Length == vertices.Length && colors[index].a == 0) continue;
                                if (visible.HasValue) { var value = visible.Value; value.Encapsulate(point); visible = value; }
                                else visible = new Bounds(point, Vector3.zero);
                            }
                        }
                        if (!visible.HasValue) throw new InvalidDataException("Bound partner has no geometry.");
                        Bounds bounds = visible.Value;
                        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 10);
                        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.12f;
                        Capture(camera, target, $"bound-{partner.npc_id}-{key}-{variant.field}");
                    }
                    finally { foreach (var playable in live) if (playable) playable.Stop(true); center.RemoveAll(); }
                    void Tick(float delta) { center.Update(delta); center.LateUpdate(); actor.Advance(delta); }
                }
                if (actor.Animator.runtimeAnimatorController != original) throw new InvalidDataException("Application binding did not restore the original controller.");
                actor.Root.gameObject.SetActive(false);
                yield return null;
                if (actor.Attachments.Agent.GetComponent<Zjcs.Official.StoryAnimation.AnimationController>())
                    throw new InvalidDataException("Disposed application controller remained on the original entity.");
            }
            if (data.partners.Length != 34 || variants != 93 || cues != 95) throw new InvalidDataException("Original application binding coverage differs.");
            Debug.Log($"Original application animation binding coverage: actors=34, variants={variants}, cues={cues}.");
            Destroy(target); Destroy(pipeline);
        }
        private static void Capture(Camera camera, RenderTexture target, string name)
        {
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Destroy(pixels); }
        }
    }
}
