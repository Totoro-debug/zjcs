using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.StoryAnimation;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        private const string Key = "Zjcs.Battle.AnimatorPlaybackAcceptance";
        private static bool started;
        private static int errors;

        public static void RunHosts()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            Environment.SetEnvironmentVariable("ZJCS_SKILLS_AUTORUN", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_HOSTS_ACCEPTANCE", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_HOSTS_DIRECT", "1");
            Environment.SetEnvironmentVariable("ZJCS_NATIVE_ANIMATOR_HOSTS", "1");
            HookHosts();
            Type.GetType("Zjcs.Editor.Skills.SkillPlayModeAcceptance, Zjcs.Editor", true).GetMethod("Run").Invoke(null, null);
        }

        [InitializeOnLoadMethod]
        private static void HookHosts()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_NATIVE_ANIMATOR_HOSTS") != "1") return;
            EditorApplication.playModeStateChanged -= HostsMode;
            EditorApplication.playModeStateChanged += HostsMode;
        }

        private static void HostsMode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredPlayMode) return;
            var probe = new GameObject("Native Animator host observer");
            UnityEngine.Object.DontDestroyOnLoad(probe);
            probe.AddComponent<HostObserver>();
        }

        public static void Run()
        {
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetBool(Key + ".gameControllers", false);
            SessionState.SetBool(Key + ".constraints", false);
            SessionState.SetBool(Key + ".deforms", false);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        public static void RunGameControllers()
        {
            Run();
            SessionState.SetBool(Key + ".gameControllers", true);
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }

        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Native battle Animator verification timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Native battle Animator verification").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            var steps = SessionState.GetBool(Key + ".deforms", false) ? DeformSteps()
                : SessionState.GetBool(Key + ".constraints", false) ? ConstraintChannelSteps()
                : SessionState.GetBool(Key + ".gameControllers", false) ? GameControllerSteps() : Steps();
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
                Debug.Log(SessionState.GetBool(Key + ".deforms", false)
                    ? "Original attachment deformation Play Mode passed; complete physics/constraint/mixing and player skill rendering remain outside this check."
                    : SessionState.GetBool(Key + ".constraints", false)
                    ? "Original animation constraint channel Play Mode passed; skeleton solving, physics, mixing and rendering remain outside this check."
                    : SessionState.GetBool(Key + ".gameControllers", false)
                    ? "Official Game controllers Play Mode passed: four original controllers, parameters, idle/skill/damage states, native playable clip overrides and shared-controller restoration. Skeleton motion rendering is not covered."
                    : "Native battle Animator Play Mode passed: official component bindings, infinite/finite/zero duration, speed, ordered parameter restoration, allowed states, original override order and controller isolation/restoration.");
            }
            EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Native battle Animator: " + description);
        }

        private static OfficialBattleSkillPlayableCue Cue(bool flag, float value, int index, float duration = -1f)
        {
            return new OfficialBattleSkillPlayableCue
            {
                my_bool_param = "flag", my_bool_value = flag,
                my_float_param = "value", my_float_value = value,
                int_param = "index", int_value = index,
                trigger_param = "fire", layer_name = "Overlay", layer_weight = .8f,
                my_duration = duration, end_resume_param = true
            };
        }

        private static void Values(Animator animator, bool flag, float value, int index, string label)
        {
            Require(animator.GetBool("flag") == flag && Mathf.Abs(animator.GetFloat("value") - value) < .00001f
                && animator.GetInteger("index") == index, label);
        }

        private static IEnumerator Steps()
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Runtime/Official/StoryAnimation/MyPlayableOverrideAnimator.cs");
            Require(script != null && script.GetClass() == typeof(MyPlayableOverrideAnimator), "native override MonoScript binding");
            var model = new GameObject("Isolated parameter fixture; no replacement game art");
            var animator = model.AddComponent<Animator>();
            var native = new AnimatorController { name = "Isolated Animator parameter fixture" };
            native.AddLayer("Base Layer"); native.AddLayer("Overlay");
            var idle = native.layers[0].stateMachine.AddState("Idle");
            native.layers[0].stateMachine.defaultState = idle;
            var overlay = native.layers[1].stateMachine.AddState("Idle");
            native.layers[1].stateMachine.defaultState = overlay;
            native.parameters = new[] {
                new AnimatorControllerParameter { name = "flag", type = AnimatorControllerParameterType.Bool },
                new AnimatorControllerParameter { name = "value", type = AnimatorControllerParameterType.Float, defaultFloat = .25f },
                new AnimatorControllerParameter { name = "index", type = AnimatorControllerParameterType.Int, defaultInt = 1 },
                new AnimatorControllerParameter { name = "fire", type = AnimatorControllerParameterType.Trigger }
            };
            var originalClip = new AnimationClip { name = "OriginalClip" };
            var preexistingClip = new AnimationClip { name = "PreexistingClip" };
            var replacementClip = new AnimationClip { name = "ReplacementClip" };
            idle.motion = originalClip;
            var original = new AnimatorOverrideController(native);
            original[originalClip.name] = preexistingClip;
            animator.runtimeAnimatorController = original;
            animator.Update(0);
            animator.SetLayerWeight(1, .25f);
            var playback = new OfficialBattleAnimatorPlayback(animator, model.transform);
            var owned = animator.runtimeAnimatorController as AnimatorOverrideController;
            Require(owned != null && owned != original && owned[originalClip.name] == preexistingClip,
                "owned override retains preexisting clip mapping");
            var unsupported = Cue(true, .8f, 10); unsupported.trigger_param = "UnboundSourceParameter";
            Require(!OfficialBattleAnimatorPlayback.Supports(animator, unsupported), "missing original parameter is reported unsupported");
            playback.Play(unsupported, null);
            Values(animator, false, .25f, 1, "unsupported source controller is not partially applied");

            playback.Play(Cue(true, .5f, 2), null);
            Values(animator, true, .5f, 2, "official infinite playable applies original fields");
            yield return null;
            playback.Tick(20);
            Values(animator, true, .5f, 2, "negative duration remains active");
            var upper = Cue(false, .9f, 3, .2f);
            playback.Play(upper, null);
            Values(animator, false, .9f, 3, "later owner applies ordered parameters");
            yield return null;
            playback.Tick(.3f);
            Values(animator, true, .5f, 2, "finite owner resumes earlier active values");
            playback.Stop();
            Values(animator, false, .25f, 1, "stop restores all initial parameter values");

            var finite = Cue(true, .6f, 4, .4f); finite.speed = 2;
            playback.Play(finite, null);
            yield return null; playback.Tick(.11f);
            Values(animator, true, .6f, 4, "speed scales original lifetime before expiration");
            yield return null; playback.Tick(.11f);
            Values(animator, false, .25f, 1, "speed scales original lifetime through expiration");
            var paused = Cue(true, .7f, 5, .2f); paused.speed = 0;
            playback.Play(paused, null);
            yield return null; playback.Tick(100);
            Values(animator, true, .7f, 5, "zero speed preserves current lifetime");
            playback.Stop();
            playback.Play(Cue(true, .8f, 6, 0), null);
            Values(animator, false, .25f, 1, "zero duration ends through official initial-frame flush");

            var rejected = Cue(true, .9f, 7); rejected.end_resume_param = false;
            rejected.kind = 1; rejected.origin_clip_name = originalClip.name;
            rejected.pre_state_name_allow_list.Add("MissingState");
            playback.Play(rejected, replacementClip);
            Values(animator, false, .25f, 1, "original state check rejects parameter changes");
            Require(owned[originalClip.name] == replacementClip && original[originalClip.name] == preexistingClip,
                "official override precedes state check and leaves shared controller unchanged");
            playback.Stop();
            var accepted = Cue(true, .3f, 8); accepted.pre_state_name_allow_list.Add("Idle");
            playback.Play(accepted, null);
            Values(animator, true, .3f, 8, "allowed state applies fields");
            playback.Dispose(); playback.Dispose();
            Values(animator, false, .25f, 1, "disposal restores outstanding parameter layers");
            Require(animator.runtimeAnimatorController == original, "disposal restores original controller");
            yield return null;
            Require(model.GetComponentsInChildren<MyPlayableAnimator>(true).Length == 0,
                "completed/disposed playable instances are released");
            UnityEngine.Object.Destroy(model); UnityEngine.Object.Destroy(original);
            UnityEngine.Object.Destroy(native); UnityEngine.Object.Destroy(originalClip);
            UnityEngine.Object.Destroy(preexistingClip); UnityEngine.Object.Destroy(replacementClip);
            yield return null;
        }

        private sealed class Runner : MonoBehaviour { }

        private static IEnumerator GameControllerSteps()
        {
            foreach (string body in new[] { "Male", "Female" })
                foreach (string profession in new[] { "Zhanshi", "Fashi" })
                {
                    string name = body + "_" + profession + "_Controller";
                    var source = Resources.Load<RuntimeAnimatorController>("Combat/GameAnimators/Anim/" + name);
                    Require(source != null, "original Game controller resource: " + name);
                    var model = new GameObject("Original Game controller fixture: " + name);
                    var animator = model.AddComponent<Animator>();
                    animator.runtimeAnimatorController = source;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Update(0);
                    Require(animator.parameters.Length == (profession == "Zhanshi" ? 22 : 19), "original Game parameter count: " + name);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "original default Idle state: " + name);
                    string group = profession == "Zhanshi" ? "100" : "200";
                    var clips = animator.GetCurrentAnimatorClipInfo(0);
                    Require(clips.Length == 1 && clips[0].clip.name == group + "/idle_01", "original Idle motion: " + name);
                    var cue = new OfficialBattleSkillPlayableCue
                    {
                        kind = 1, origin_clip_name = "Skill", trigger_param = "skill", my_duration = 0,
                        end_resume_param = false, speed = 1
                    };
                    Require(OfficialBattleAnimatorPlayback.Supports(animator, cue), "source skill parameter and override slot: " + name);
                    var clip = Resources.Load<AnimationClip>("Combat/SkillAnimations/" + group + "_skill_06");
                    Require(clip != null, "recorded original skill clip: " + group);
                    var playback = new OfficialBattleAnimatorPlayback(animator, model.transform);
                    playback.Play(cue, clip);
                    animator.Update(.02f);
                    animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Skill"), "original Skill transition: " + name);
                    clips = animator.GetCurrentAnimatorClipInfo(0);
                    Require(clips.Length == 1 && clips[0].clip == clip, "original Skill override consumption: " + name);
                    animator.Rebind(); animator.Update(0);
                    animator.SetTrigger("damage"); animator.Update(.02f); animator.Update(.02f);
                    Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Damage"), "original Damage transition: " + name);
                    clips = animator.GetCurrentAnimatorClipInfo(0);
                    Require(clips.Length == 1 && clips[0].clip.name == group + "/hit_01", "original Damage motion: " + name);
                    playback.Dispose();
                    Require(animator.runtimeAnimatorController == source, "original Game controller restored: " + name);
                    Debug.Log("[Official Game controllers] " + name + ": idle, skill override and damage verified.");
                    UnityEngine.Object.Destroy(model);
                    yield return null;
                }
        }

        private sealed class HostObserver : MonoBehaviour
        {
            private readonly HashSet<string> observed = new HashSet<string>();
            private void LateUpdate()
            {
                string host = null;
                foreach (string name in new[] { "DailyDungeonBattleRunner", "ArenaBattleRunner", "FantasyTowerBattleRunner" })
                    if (GameObject.Find(name) != null) { host = name; break; }
                if (host == null || observed.Contains(host)) return;
                foreach (var playable in UnityEngine.Object.FindObjectsOfType<MyPlayableAnimator>())
                {
                    if (!playable.myInited || playable.mySource == null
                        || !(playable.myApplyComponent is Zjcs.Official.StoryAnimation.AnimationController native)
                        || native.myAnimator == null || !(native.RuntimeAnimatorController is AnimatorOverrideController)) continue;
                    observed.Add(host);
                    Debug.Log("[Native Animator hosts] " + host + ": " + playable.GetType().Name
                        + " source=" + playable.mySource.name + " duration=" + playable.myDuration
                        + " speed=" + playable.Speed + " controller=" + native.RuntimeAnimatorController.name);
                    if (observed.Count == 3)
                        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                            "native-animator-hosts.txt"), observed);
                    break;
                }
                if (observed.Contains(host)) return;
                var player = GameObject.Find("HomePlayer");
                var animator = player == null ? null : player.GetComponentInChildren<Animator>(true);
                if (animator == null) return;
                var skillCue = new OfficialBattleSkillPlayableCue { kind = 1, origin_clip_name = "Skill", trigger_param = "skill" };
                if (OfficialBattleAnimatorPlayback.Supports(animator, skillCue)) return;
                observed.Add(host);
                Debug.Log("[Native Animator hosts] " + host + ": source binding remains unavailable; controller="
                    + animator.runtimeAnimatorController.name + " parameters=" + animator.parameters.Length);
                if (observed.Count == 3)
                    File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        "native-animator-hosts.txt"), observed);
            }
        }
    }
}
