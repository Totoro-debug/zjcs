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
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativeBattleAnimatorAcceptance
    {
        private const string Active = "Zjcs.PartnerNativeBattleAnimator";
        static OfficialPartnerNativeBattleAnimatorAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner Animator validation").AddComponent<PartnerNativeBattleAnimatorRunner>();
            };
        }
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !project.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Partner Animator validation requires its isolated project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            OfficialPartnerNativeBattleDataValidation.ImportRuntimeTextures();
            SessionState.SetBool(Active, true);
            EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_BATTLE_ANIMATORS_PASSED: exact original controller/clip bindings and selected native Mecanim poses. "
                + "Blended-tree pose consumption, formal battle and Windows Player remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeBattleAnimatorRunner : MonoBehaviour
    {
        private const string Root = "Assets/Official/Combat/NativePartners/";
        private int errors;
        private IEnumerator Start()
        {
            Application.logMessageReceived += (_, __, type) => {
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors++;
            };
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialPartnerNativeBattleAnimatorAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeBattleAnimatorAcceptance.Finish(errors == 0 ? null : new InvalidDataException("Partner Animator errors: " + errors));
        }
        private IEnumerator Check()
        {
            var library = Resources.Load<PartnerNativeBattleAnimatorCatalog>("Combat/NativePartnerAnimators/catalog");
            if (!library || library.Entries.Count != 34 || library.Find(-1))
                throw new InvalidDataException("Original partner Animator inventory differs.");
            var catalog = Obj(OfficialEquipmentJson.Parse(File.ReadAllText(Root + "catalog.json")));
            var rows = Arr(catalog["fighters"]).Select(Obj).ToDictionary(value => Convert.ToInt32(value["npcId"]));
            int clips = 0, states = 0, blendTrees = 0;
            foreach (var entry in library.Entries)
            {
                var row = rows[entry.NpcId];
                var data = ProfessionNativeSkeletonData.LoadPartnerBattle(File.ReadAllBytes(Root + (string)row["package"]), entry.NpcId);
                var controller = entry.Controller as AnimatorController;
                if (!controller || controller.layers.Length != 1) throw new InvalidDataException("Original partner controller is unavailable.");
                foreach (AnimationClip clip in controller.animationClips)
                {
                    var channels = new ProfessionNativeAnimationChannels(data, clip.name);
                    if (Mathf.Abs(channels.Duration - clip.length) > 1f / 30f + .00001f)
                    {
                        if (entry.NpcId != 11033 || clip.name != "200/die_02" || clip.length != 0
                            || Mathf.Abs(channels.Duration - .3333333432674408f) > .000001f)
                            throw new InvalidDataException("Original partner clip/skeleton duration differs: " + entry.NpcId + "/" + clip.name);
                        Debug.Log("Preserved original 11033/200/die_02: carrier length=0, skeleton duration=" + channels.Duration
                            + "; native pose consumption of this source mismatch remains unbound.");
                    }
                    clips++;
                }
                var go = new GameObject("Original Animator graph " + entry.NpcId);
                try
                {
                    var animator = go.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.runtimeAnimatorController = controller;
                    foreach (var child in controller.layers[0].stateMachine.states)
                    {
                        animator.Rebind(); animator.Update(0);
                        int hash = Animator.StringToHash(controller.layers[0].name + "." + child.state.name);
                        if (!animator.HasState(0, hash)) throw new InvalidDataException("Original partner state was lost.");
                        animator.Play(hash, 0, .25f); animator.Update(0);
                        if (animator.GetCurrentAnimatorClipInfo(0).Length == 0)
                            throw new InvalidDataException("Original partner state produced no motion.");
                        states++;
                        if (!(child.state.motion is BlendTree tree)) continue;
                        if (tree.blendType != BlendTreeType.Simple1D || tree.children.Length != 2)
                            throw new InvalidDataException("Review original partner blend tree.");
                        foreach (float value in new[] { 0f, .5f, 1f })
                        {
                            animator.Rebind(); animator.SetFloat(tree.blendParameter, value);
                            animator.Play(hash, 0, .25f); animator.Update(0);
                            var weights = animator.GetCurrentAnimatorClipInfo(0);
                            foreach (var weight in weights)
                            {
                                int index = Array.FindIndex(tree.children, source => source.motion == weight.clip);
                                if (index < 0 || Mathf.Abs(weight.weight - (index == 0 ? 1 - value : value)) > .0001f)
                                    throw new InvalidDataException("Original partner 1D Animator weights differ.");
                            }
                            if (Mathf.Abs(weights.Sum(weight => weight.weight) - 1) > .0001f)
                                throw new InvalidDataException("Original partner 1D Animator lost its motion weights.");
                        }
                        blendTrees++;
                    }
                }
                finally { Destroy(go); }
                yield return null;
            }
            Debug.Log("Original partner Animators: controllers=34, clips=" + clips + ", states=" + states + ", verified 1D trees=" + blendTrees);

            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Original Mecanim pose camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .18f, .2f);
            var target = new RenderTexture(768, 1365, 24); camera.targetTexture = target;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/PartnerNativeBattleValidation/OriginalSkeletonEffect.shader");
            foreach (int id in new[] { 11001, 11003, 11005, 11007, 11009, 11010, 11042 })
            {
                var row = rows[id]; var root = new GameObject("Original Mecanim partner " + id);
                var actors = new List<PartnerNativeBattlePoseRunner.Actor>();
                try
                {
                    var transforms = new Dictionary<string, Transform>();
                    foreach (var part in new[] { row }.Concat(Arr(row["parts"]).Select(Obj)))
                        actors.Add(PartnerNativeBattlePoseRunner.CreateActor(part, root.transform, transforms, shader));
                    var body = actors[0]; var animator = body.Transform.gameObject.AddComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.runtimeAnimatorController = library.Find(id);
                    animator.enabled = false;
                    var pose = new ProfessionNativeAnimatorPose(animator, ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics,
                        (skeleton, mix) => ProfessionNativeOrderedConstraintPose.EvaluateMixNextBeforePhysics(skeleton,
                            mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                            mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight));
                    var controller = (AnimatorController)animator.runtimeAnimatorController;
                    string idle = "Idle";
                    string skill = controller.layers[0].stateMachine.states.Select(value => value.state.name)
                        .First(value => value.Contains(id == 11010 ? "/skill_02" : "/skill_01", StringComparison.Ordinal));
                    foreach (string state in new[] { idle, skill })
                    {
                        animator.Rebind(); animator.Play(controller.layers[0].name + "." + state, 0, .25f); animator.Update(0);
                        var frame = pose.Apply(body.Skeleton);
                        foreach (var part in actors.Skip(1))
                        {
                            var channels = new ProfessionNativeAnimationChannels(part.Skeleton.Source, (string)part.Renderer["animationName"]);
                            ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(part.Skeleton, channels,
                                frame.Current.MotionTime * Convert.ToSingle(part.Renderer["timeScale"]), Convert.ToBoolean(part.Renderer["loop"]));
                        }
                        foreach (var actor in actors) PartnerNativeBattlePoseRunner.Draw(actor);
                        Bounds bounds = PartnerNativeBattlePoseRunner.VisibleBounds(actors);
                        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 10);
                        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.12f;
                        yield return null;
                        PartnerNativeBattlePoseRunner.Capture(camera, target, "animator-" + id + "-" + state.Replace('/', '-'));
                    }
                    animator.Rebind(); animator.Play(controller.layers[0].name + ".Idle", 0, 0); animator.Update(0);
                    var runParameter = animator.parameters.Single(value => value.name == "run");
                    if (runParameter.type == AnimatorControllerParameterType.Bool) animator.SetBool("run", true);
                    else if (runParameter.type == AnimatorControllerParameterType.Trigger) animator.SetTrigger("run");
                    else throw new InvalidDataException("Review original run parameter type.");
                    bool reachedRun = false; int mixedFrames = 0;
                    for (int step = 0; step < 30; step++)
                    {
                        animator.Update(1f / 60f);
                        var frame = pose.Apply(body.Skeleton);
                        if (frame.Incoming.HasValue) mixedFrames++;
                        reachedRun |= animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Run")
                            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Run"));
                        body.Batches.Build(body.Skeleton.Pose, body.Skeleton.CreateDrawSlots(), true, true, allowConcaveClipping: true);
                        if (body.Batches.Batches.Any(batch => batch.Mesh.vertices.Any(vertex => !float.IsFinite(vertex.x) || !float.IsFinite(vertex.y))))
                            throw new InvalidDataException("Original Animator transition produced a non-finite pose.");
                    }
                    if (!reachedRun) throw new InvalidDataException("Original partner run condition did not transition: " + id);
                    Debug.Log("Original partner run transition: npc=" + id + ", sampled=30, mixed=" + mixedFrames);
                }
                finally { foreach (var actor in actors) actor.Dispose(); Destroy(root); }
                yield return null;
            }
            Destroy(target); Destroy(pipeline);
        }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
    }
}
