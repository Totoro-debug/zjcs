using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAnimation;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    // Isolated native Animator dispatch probe. This does not substitute test
    // controller art for original game controllers or claim model rendering.
    public static class OfficialMap14StoryAnimatorAcceptance
    {
        private const string Key = "Zjcs.Map14.StoryAnimatorAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
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
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 original Animator verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original story Animator dispatch verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original story Animator Play Mode passed: native bindings, ordered keyed state, pre-init buffer replacement/removal, nested pause, resume layers, direct/controller playable paths, state allow-list, fade/stop/pool restoration and override callbacks. Original controller/clip/model rendering, bone/skin physics, map input and saves remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original story Animator: " + message); }
        private static void Values(Animator animator, bool b, float f, int i, string label)
        { Require(animator.GetBool("flag") == b && Math.Abs(animator.GetFloat("value") - f) < .000001f && animator.GetInteger("index") == i, label); }
        private static MyPlayableAnimator Add(GameObject root, MyPlayableCenter center, Component apply, string name, bool b, float f, int i, float weight)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var leaf = go.AddComponent<MyPlayableAnimator>(); leaf.MyPlayableCenter = center;
            leaf.myTriggerList = new List<MyPlayableTriggerTimeCfg>(); leaf.myFadeoutTrigerList = new List<MyPlayableTrigger>(); leaf.myEndTrigerList = new List<MyPlayableTrigger>();
            leaf.myEndDestroy = false; leaf.myApplyComponent = apply; leaf.PreStateNameAllowList = new List<string>();
            leaf.myBoolParam = "flag"; leaf.myBoolValue = b; leaf.myFloatParam = "value"; leaf.myFloatValue = f;
            leaf.IntParam = "index"; leaf.IntValue = i; leaf.myTriggerParam = "fire"; leaf.LayerName = "Overlay"; leaf.LayerWeight = weight;
            leaf.myEndResumeParam = true; leaf.myDuration = -1; leaf.UpdateHashMapString(); leaf.Init(root, root);
            return leaf;
        }
        private static IEnumerator Steps()
        {
            foreach (Type type in new[] { typeof(AnimationController), typeof(MyPlayableAnimator) })
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Runtime/Official/StoryAnimation/" + type.Name + ".cs");
                Require(script != null && script.GetClass() == type, type.Name + " native MonoScript binding");
            }
            var states = new StateMergeList<int>(7); states.Add((1, 10)); states.Add((2, 20)); states.Add((1, 30));
            Require(states.CurState == 30 && states.Remove(1) && states.Count == 2 && states.CurState == 30, "original duplicate owner removes first matching layer");
            Require(states.Remove(1) && states.CurState == 20 && !states.Remove(9), "remaining keyed layer and absent removal");
            states.Remove(2); Require(states.Count == 0 && states.CurState == 7, "empty layer returns initial state");

            var root = new GameObject("Original Animator dispatch fixtures");
            var animator = root.AddComponent<Animator>();
            var native = new AnimatorController { name = "Isolated parameter verification controller" };
            native.AddLayer("Base Layer"); native.AddLayer("Overlay");
            var idle = native.layers[0].stateMachine.AddState("Idle"); native.layers[0].stateMachine.defaultState = idle;
            var overlay = native.layers[1].stateMachine.AddState("Idle"); native.layers[1].stateMachine.defaultState = overlay;
            native.parameters = new[] {
                new AnimatorControllerParameter { name = "flag", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "value", type = AnimatorControllerParameterType.Float, defaultFloat = .25f },
                new AnimatorControllerParameter { name = "index", type = AnimatorControllerParameterType.Int, defaultInt = 1 },
                new AnimatorControllerParameter { name = "fire", type = AnimatorControllerParameterType.Trigger } };
            animator.runtimeAnimatorController = native; animator.Update(0); animator.SetLayerWeight(1, .25f);
            var controller = root.AddComponent<AnimationController>(); controller.myAnimator = animator; controller.NotCreateGraph = true;
            int b = Animator.StringToHash("flag"), f = Animator.StringToHash("value"), i = Animator.StringToHash("index");
            controller.SetBool(1, b, true, true); controller.SetBool(1, b, false, true); controller.SetBool(2, b, true, true); controller.ResumeBool(2, b);
            controller.SetFloat(1, f, .5f, true); controller.SetFloat(1, f, .75f, true);
            controller.SetInteger(1, i, 8, true); controller.SetInteger(2, i, 9, true); controller.ResumeInteger(2, i);
            Values(animator, false, .25f, 1, "pre-init parameters remain buffered");
            controller.Init(); Values(animator, false, .75f, 8, "pre-init same-owner replacement and cancellation preserve final ordered state");
            controller.ResumeBool(1, b); controller.ResumeFloat(1, f); controller.ResumeInteger(1, i);
            Values(animator, false, .25f, 1, "buffered resumable values restore original parameters");
            animator.speed = .75f; controller.Pause(); controller.Pause();
            controller.SetFloat(3, f, 99, true); controller.SetInteger(3, i, 99, true); controller.SetBool(3, b, true, true);
            Values(animator, false, .25f, 1, "paused controller rejects writes");
            controller.Resume(); Require(animator.speed == 0, "nested pause retains zero speed");
            controller.Resume(); Require(animator.speed == .75f, "final resume restores original speed");
            controller.Pause(); controller.Pause(); controller.ForceResumeAllPause(); controller.Resume();
            Require(animator.speed == .75f, "forced resume clears counter and later resume is stable");

            var center = new MyPlayableCenter(); var first = Add(root, center, controller, "layer-first", true, .5f, 2, .8f);
            var second = Add(root, center, controller, "layer-second", false, .9f, 3, .2f);
            first.TryPlay(); Values(animator, true, .5f, 2, "first original playable applies all parameter types");
            second.TryPlay(); Values(animator, false, .9f, 3, "second original playable owns top parameter layer");
            second.Stop(false); Values(animator, true, .5f, 2, "normal stop request immediately resumes original parameter layer");
            Require(Math.Abs(animator.GetLayerWeight(1) - .8f) < .000001f, "normal stop restores captured layer weight");
            second.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();
            first.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();
            Values(animator, false, .25f, 1, "final owner restores initial native Animator values");
            Require(Math.Abs(animator.GetLayerWeight(1) - .25f) < .000001f, "final layer weight restores initial value");

            first.ResetData(); first.myApplyComponent = controller; first.Init(root, root); first.TryPlay();
            second.ResetData(); second.myApplyComponent = controller; second.Init(root, root); second.TryPlay();
            first.Stop(false); Values(animator, false, .9f, 3, "removing earlier owner preserves later keyed parameters");
            Require(Math.Abs(animator.GetLayerWeight(1) - .25f) < .000001f, "original layer weight uses captured value rather than keyed merge");
            second.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();
            Values(animator, false, .25f, 1, "pool reuse resets restoration guard and owner stacks");
            Require(Math.Abs(animator.GetLayerWeight(1) - .8f) < .000001f, "later owner restores its captured layer weight after out-of-order stop");
            first.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();
            animator.SetLayerWeight(1, .25f);

            var direct = Add(root, center, animator, "direct-native-animator", true, .6f, 4, .7f); direct.TryPlay();
            Values(animator, true, .6f, 4, "direct native Animator path applies same official fields");
            direct.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();
            Values(animator, false, .25f, 1, "direct native Animator stop restores snapshot");
            var rejected = Add(root, center, animator, "state-allow-list", true, .8f, 5, 1); rejected.myEndResumeParam = false;
            rejected.PreStateNameAllowList.Add("NoSuchSourceState"); rejected.TryPlay();
            Values(animator, false, .25f, 1, "original state allow-list rejects parameter application");
            rejected.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();
            var accepted = Add(root, center, animator, "allowed-idle", true, .4f, 6, .5f); accepted.PreStateNameAllowList.Add("Idle");
            accepted.TryPlay(); Values(animator, true, .4f, 6, "original allowed state accepts application");
            accepted.Stop(true); yield return null; center.Update(.1f); center.LateUpdate();

            controller.CreateOverrideController = true; int callbacks = 0; controller.OnRuntimeAnimatorControllerChanged += () => callbacks++;
            var replacement = new AnimatorController { name = "Isolated replacement controller" }; replacement.AddLayer("Base Layer");
            controller.SetRuntimeAnimationController(replacement);
            Require(callbacks == 1 && controller.RuntimeAnimatorController is AnimatorOverrideController && controller.RuntimeAnimatorController.name == replacement.name,
                "original controller replacement creates named override and invokes callback once");
            var same = controller.RuntimeAnimatorController; controller.SetRuntimeAnimationController(same);
            Require(callbacks == 1 && controller.RuntimeAnimatorController == same, "same-controller reinit avoids change callback");
            center.RemoveAll(); var generated = controller.RuntimeAnimatorController;
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(native); UnityEngine.Object.Destroy(generated); UnityEngine.Object.Destroy(replacement);
            yield return null;
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
