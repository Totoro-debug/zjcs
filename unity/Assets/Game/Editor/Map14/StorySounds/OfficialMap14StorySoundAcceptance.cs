using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySounds;

namespace Zjcs.Editor.Map14
{
    // Dispatch only. The probe deliberately records native event IDs instead of
    // claiming the still-unbound original mixer, scene/UI or map flow works.
    public static class OfficialMap14StorySoundAcceptance
    {
        private const string Key = "Zjcs.Map14.StorySoundAcceptance";
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
            { Debug.LogError("Map14 original story sound verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original story sound dispatch verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var previous = OfficialWwiseEvent.Host; var steps = Steps();
            try
            {
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
                    Debug.Log("Map14 original story sound dispatch Play Mode passed: native script bindings, original loop threshold/no catch-up, target fallback, stop/reset/reuse, event IDs/playing IDs, Timeline Play transition and no pause/clip-end stop, original source/target binding and manual scaled clock/duration/stop lifecycle. Original audio mixer DSP/RTPC, full native Timeline/UI/map flow and saves remain pending.");
                }
                EditorApplication.isPlaying = false;
            }
            finally { OfficialWwiseEvent.Host = previous; }
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original story sound: " + message); }
        private sealed class Probe : IProfessionSoundHost
        {
            public readonly List<uint> Ids = new List<uint>();
            public readonly List<GameObject> Emitters = new List<GameObject>();
            public uint PostEvent(uint id, GameObject emitter) { Ids.Add(id); Emitters.Add(emitter); return (uint)Ids.Count; }
        }
        private static OfficialWwiseEvent Event(string name, uint id, List<UnityEngine.Object> cleanup)
        {
            var reference = ScriptableObject.CreateInstance<WwiseEventReference>();
            reference.objectName = name; reference.id = id; cleanup.Add(reference);
            return new OfficialWwiseEvent { WwiseObjectReference = reference };
        }
        private static void Drive(MyPlayableCenter center, float delta) { center.Update(delta); center.LateUpdate(); }
        private static IEnumerator Steps()
        {
            foreach (Type type in new[] { typeof(MyPlayableSound), typeof(MyPlayableTimeLine), typeof(WwiseEventPlayableAsset), typeof(WwiseEventPlayableBehaviour), typeof(WwiseEventTrack) })
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Runtime/Official/StorySounds/" + type.Name + ".cs");
                Require(script != null && script.GetClass() == type, type.Name + " original native MonoScript binding");
            }
            var cleanup = new List<UnityEngine.Object>(); var probe = new Probe(); OfficialWwiseEvent.Host = probe;
            var root = new GameObject("Official sound dispatch fixtures"); cleanup.Add(root);
            var target = new GameObject("Official sound target"); cleanup.Add(target);
            var center = new MyPlayableCenter(); var sound = root.AddComponent<MyPlayableSound>();
            sound.MyPlayableCenter = center; sound.myEndDestroy = false;
            sound.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
            sound.myFadeoutTrigerList = new List<MyPlayableTrigger>(); sound.myEndTrigerList = new List<MyPlayableTrigger>();
            sound.SoundEvent = Event("original-play-probe", 3438568527, cleanup);
            sound.StopSoundEvent = Event("original-stop-probe", 902, cleanup);
            sound.Duration = 20; sound.LoopInterval = 1; sound.myFadeOutDuration = 10;
            sound.Init(null, target); sound.TryPlay();
            Require(probe.Ids.SequenceEqual(new uint[] { 3438568527 }) && probe.Emitters[0] == target && sound.SoundEvent.PlayingId == 1, "post uses original reference ID, target and returned playing ID");
            yield return null; Drive(center, .5f); yield return null; Drive(center, .5f);
            Require(probe.Ids.Count == 1 && sound.Time == 1, "original loop compares against Time minus delta, not current Time");
            yield return null; Drive(center, .25f);
            Require(probe.Ids.Count == 2 && sound.Time == 1.25f, "original loop threshold posts on following frame");
            yield return null; Drive(center, 3);
            Require(probe.Ids.Count == 2, "large frame crossing does not prematurely post");
            yield return null; Drive(center, .125f);
            Require(probe.Ids.Count == 3, "missed intervals post once without catch-up");
            sound.Stop(false);
            Require(sound.myStatus == MyPlayable.PlayableStatus.Stop && probe.Ids.Last() == 902 && probe.Ids.Count == 4 && sound.Time == 0,
                "original sound Stop performs immediate DoStop even with nonzero fade duration");
            sound.Stop(true); Require(probe.Ids.Count == 4, "repeated Stop is idempotent");
            sound.ResetData(); sound.LoopInterval = 0; sound.Init(null, null); sound.TryPlay();
            Require(probe.Emitters.Last() == root && sound.SoundEvent.PlayingId == 5, "pooled reuse falls back to own GameObject and retains actual playing ID");
            yield return null; Drive(center, 2); Require(probe.Ids.Count == 5, "zero loop interval disables repeat");
            sound.Stop(true); sound.ResetData(); sound.LoopInterval = -1; sound.Duration = .25f;
            sound.Init(null, null); sound.TryPlay(); yield return null; Drive(center, .25f);
            Require(sound.GetDuration() == .25f && sound.myStatus == MyPlayable.PlayableStatus.FadeOut && probe.Ids.Count == 7, "configured duration enters original fade state without repeat");
            sound.Stop(true); Require(probe.Ids.Count == 8 && probe.Ids.Last() == 902, "duration lifecycle posts original stop event once");

            var invalid = new OfficialWwiseEvent(); Require(invalid.Post(root) == 0 && probe.Ids.Count == 8, "SDK invalid reference posts nothing");
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); cleanup.Add(timeline);
            var track = timeline.CreateTrack<WwiseEventTrack>(null, "chapter_5_main_36_17");
            var first = track.CreateClip<WwiseEventPlayableAsset>(); first.start = 5.516666666666667; first.duration = 2.0166666666666666;
            ((WwiseEventPlayableAsset)first.asset).wwiseEvent = Event("chapter_5_main_36_17_1", 3438568527, cleanup);
            var second = track.CreateClip<WwiseEventPlayableAsset>(); second.start = 7.733333333333333; second.duration = 2.0166666666666586;
            ((WwiseEventPlayableAsset)second.asset).wwiseEvent = Event("chapter_5_main_36_17_2", 3438568524, cleanup);
            var director = root.AddComponent<PlayableDirector>(); director.timeUpdateMode = DirectorUpdateMode.Manual; director.playableAsset = timeline;
            director.Play(); director.time = 5; director.Evaluate(); Require(probe.Ids.Count == 8, "original Timeline remains silent before first clip");
            director.time = 5.6; director.Evaluate(); Require(probe.Ids.Count == 9 && probe.Ids.Last() == 3438568527 && probe.Emitters.Last() == root, "first original Timeline event posts using director owner");
            director.time = 7.6; director.Evaluate(); Require(probe.Ids.Count == 9, "clip end does not issue a stop event");
            director.time = 7.8; director.Evaluate(); Require(probe.Ids.Count == 10 && probe.Ids.Last() == 3438568524, "second original Timeline event posts independently");
            director.Pause(); Require(probe.Ids.Count == 10, "original Timeline pause has no implicit sound stop");
            director.Stop(); Require(probe.Ids.Count == 10, "original Timeline graph stop has no invented sound stop");
            var targetTrack = timeline.CreateTrack<WwiseEventTrack>(null, "target-only-track");
            var wrapper = root.AddComponent<MyPlayableTimeLine>(); wrapper.MyPlayableCenter = center; wrapper.myEndDestroy = false;
            wrapper.myTriggerList = new List<MyPlayableTriggerTimeCfg>(); wrapper.myFadeoutTrigerList = new List<MyPlayableTrigger>(); wrapper.myEndTrigerList = new List<MyPlayableTrigger>();
            wrapper.myDirector = director; wrapper.mySourceBindings = new List<string> { track.name };
            wrapper.myTargetBindings = new List<string> { track.name, targetTrack.name }; wrapper.mySpeed = 2;
            director.extrapolationMode = DirectorWrapMode.None; wrapper.Init(root, target); wrapper.TryPlay();
            Require(director.GetGenericBinding(track) == root && director.GetGenericBinding(targetTrack) == target,
                "original source binding wins when both lists name the track, target-only track binds target");
            Require(director.time == 0 && director.timeUpdateMode == DirectorUpdateMode.Manual && wrapper.GetDuration() == (float)director.duration,
                "original wrapper resets director, selects manual mode and finite duration only for None extrapolation");
            yield return null; Drive(center, 2.8f);
            Require(Math.Abs(director.time - 5.6) < .000001 && probe.Ids.Count == 11 && probe.Ids.Last() == 3438568527,
                "original wrapper consumes scaled delta and evaluates the native Timeline");
            center.PauseAllPlayable(); yield return null; Drive(center, 1);
            Require(Math.Abs(director.time - 5.6) < .000001 && probe.Ids.Count == 11, "paused original center leaves manual Timeline clock unchanged");
            center.ResumeAllPlayable(); yield return null; Drive(center, 1.1f);
            Require(Math.Abs(director.time - 7.8) < .000001 && probe.Ids.Count == 12 && probe.Ids.Last() == 3438568524,
                "resuming original center advances to second split voice");
            director.extrapolationMode = DirectorWrapMode.Hold; Require(wrapper.GetDuration() == -1, "Hold extrapolation uses original indefinite duration");
            director.extrapolationMode = DirectorWrapMode.Loop; Require(wrapper.GetDuration() == -1, "Loop extrapolation uses original indefinite duration");
            wrapper.Stop(true); yield return null; Drive(center, .1f);
            Require(wrapper.myStatus == MyPlayable.PlayableStatus.Stop && director.state != PlayState.Playing && director.time == 0 && probe.Ids.Count == 12,
                "original wrapper stops director through scheduler without an invented sound stop event");
            center.RemoveAll();
            foreach (var item in cleanup) UnityEngine.Object.Destroy(item);
            yield return null;
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
