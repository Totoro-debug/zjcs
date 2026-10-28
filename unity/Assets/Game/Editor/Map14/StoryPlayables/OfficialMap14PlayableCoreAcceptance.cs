using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor.Map14
{
    // Logic-only Play Mode fixture: no original visual, map or save coverage.
    public static class OfficialMap14PlayableCoreAcceptance
    {
        private const string Key = "Zjcs.Map14.PlayableCoreAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
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
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                && !message.Contains("expected original playable exception")) errors++;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 playable core verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original playable core verification").AddComponent<Runner>().StartCoroutine(Verify());
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
                Debug.Log("Map14 original playable core Play Mode passed: two frame phases, speed, fade/trigger/callback order, sequence timing, deferred mutation, mutex extension, refcounts, pause/hide/resume, errors and removal. Visuals, original prefab binding, input, map progression and saves remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original playable core: " + message); }
        private static void Bind(MyPlayable playable, MyPlayableCenter center)
        {
            playable.MyPlayableCenter = center;
            playable.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
            playable.myFadeoutTrigerList = new List<MyPlayableTrigger>();
            playable.myEndTrigerList = new List<MyPlayableTrigger>();
            playable.myEndDestroy = false;
        }
        private static Leaf AddLeaf(GameObject root, MyPlayableCenter center, string name, float duration = 1f)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var leaf = go.AddComponent<Leaf>(); Bind(leaf, center); leaf.Duration = duration;
            return leaf;
        }
        private static void Drive(MyPlayableCenter center, float delta)
        { center.Update(delta); center.LateUpdate(); }
        private static Marker Trigger(GameObject root, List<string> events, string value)
        {
            var go = new GameObject(value); go.transform.SetParent(root.transform, false);
            var marker = go.AddComponent<Marker>(); marker.Events = events; marker.Value = value; return marker;
        }
        private static IEnumerator Steps()
        {
            string sourceFolder = "Assets/Game/Runtime/Official/StoryPlayables/";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(sourceFolder + "MyPlayable.cs");
            Require(script != null && script.GetClass() == typeof(MyPlayable), "native MonoScript binding resolves the migrated original class");
            Require(AssetImporter.GetAtPath(sourceFolder + "MyPlayable.cs") is MonoImporter,
                "native original script uses the Unity MonoImporter");
            var root = new GameObject("Official playable core isolated fixtures");
            var center = new MyPlayableCenter();
            var events = new List<string>();
            var leaf = AddLeaf(root, center, "lifecycle");
            leaf.Events = events; leaf.mySpeed = 2; leaf.myFadeInDuration = .25f; leaf.myFadeOutDuration = .5f;
            leaf.OnInit += _ => events.Add("init"); leaf.OnPlay += _ => events.Add("play");
            leaf.OnPostPlay += _ => events.Add("post"); leaf.OnFadeOut += (_, value) => events.Add("fade:" + value);
            leaf.OnStop += _ => events.Add("stop");
            int destroys = 0; leaf.SetDestroyCallback(_ => destroys++);
            leaf.myTriggerList.Add(new MyPlayableTriggerTimeCfg { myTime = .5f, myTrigger = Trigger(root, events, "half") });
            leaf.myTriggerList.Add(new MyPlayableTriggerTimeCfg { myTime = 0, myTrigger = Trigger(root, events, "zero") });
            leaf.myFadeoutTrigerList.Add(Trigger(root, events, "fade-trigger"));
            leaf.myEndTrigerList.Add(Trigger(root, events, "end-trigger"));
            leaf.Init(null, null, .5f); leaf.TryPlay();
            Require(leaf.Speed == 1 && leaf.Time == 0 && leaf.myStatus == MyPlayable.PlayableStatus.FadeIn,
                "speed multiplication and original initial-frame zero delta");
            Require(events.SequenceEqual(new[] { "init", "play", "impl", "post", "zero" }), "initial callback and zero trigger order");
            yield return null; Drive(center, .25f);
            Require(leaf.Time == .25f && leaf.myStatus == MyPlayable.PlayableStatus.Play && leaf.GetPreferWeight() == 1, "fade-in boundary");
            yield return null; Drive(center, .75f);
            Require(leaf.Time == 1 && leaf.myStatus == MyPlayable.PlayableStatus.FadeOut && leaf.GetPreferWeight() == 1, "fixed duration enters fade-out");
            yield return null; Drive(center, .25f);
            Require(leaf.Time == 1.25f && leaf.GetPreferWeight() == .5f, "fade-out applies original weight and elapsed time");
            yield return null; Drive(center, .25f);
            Require(leaf.myStatus == MyPlayable.PlayableStatus.Stop && leaf.Time == 0 && destroys == 0 && !center.AllPlayables.Any(), "stop resets state and unregisters without auto-destroy");
            Require(events.SequenceEqual(new[] { "init", "play", "impl", "post", "zero", "half", "fade-trigger", "fade:0.5", "end-trigger", "stop-impl", "stop" }), "original terminal callback order");
            leaf.Remove(); Require(destroys == 1, "explicit remove invokes original destroy callback once");

            var first = AddLeaf(root, center, "mutex-first"); first.Mutex = true; first.Init(null, null); first.TryPlay();
            yield return null; Drive(center, .5f);
            var rejected = AddLeaf(root, center, "mutex-rejected", .75f); rejected.Mutex = true; rejected.Init(null, null); rejected.TryPlay();
            Require(rejected.myStatus == MyPlayable.PlayableStatus.Stop && first.myStatus == MyPlayable.PlayableStatus.Play, "discard mutex keeps first active");
            yield return null; Drive(center, .5f);
            Require(first.Time == 1 && first.myStatus == MyPlayable.PlayableStatus.Play, "discard extends original remaining duration");
            yield return null; Drive(center, .25f);
            Require(first.myStatus == MyPlayable.PlayableStatus.Stop, "extended original duration stops at 1.25");

            var owner = new EcEntityIdentifier(14, 100);
            var held = AddLeaf(root, center, "refcount", -1); held.Init(null, null); held.TryPlay();
            center.AddPlayableRef(owner, 7, held); center.AddPlayableRef(owner, 7);
            Require(center.GetPlayableRef(owner, 7) == 2, "reference increments");
            center.RemovePlayableRef(owner, 7); Require(center.GetPlayableRef(owner, 7) == 1, "reference decrement retains playback");
            center.RemovePlayableRef(owner, 7);
            yield return null; Drive(center, .1f);
            Require(center.GetPlayableRef(owner, 7) == 0 && held.myStatus == MyPlayable.PlayableStatus.Stop, "last reference requests normal stop");

            var parent = AddLeaf(root, center, "mutating-parent", -1);
            Leaf added = null;
            parent.Step = delta => { if (delta <= 0 || added != null) return; added = AddLeaf(root, center, "deferred-child", -1); added.Init(null, null); added.TryPlay(); parent.Stop(true); };
            parent.Init(null, null); parent.TryPlay();
            yield return null; Drive(center, .1f);
            Require(added != null && added.Time == 0 && parent.myStatus == MyPlayable.PlayableStatus.Stop, "two-phase deferred mutation and child initial frame");
            yield return null; Drive(center, .2f);
            Require(added.Time == .2f, "deferred child advances next frame");
            center.PauseAllPlayable(); yield return null; Drive(center, .3f);
            Require(added.Time == .2f, "paused frame does not advance");
            center.HideAllPlayable(); Require(!added.gameObject.activeSelf, "hide preserves playable state");
            center.ShowAllPlayable(); center.ResumeAllPlayable();
            yield return null; Drive(center, .3f);
            Require(added.gameObject.activeSelf && added.Time == .5f, "show and resume retain clock");
            added.Remove();
            Require(added.myStatus == MyPlayable.PlayableStatus.Stop, "remove stops live playable");

            var sequenceGo = new GameObject("original-sequence"); sequenceGo.transform.SetParent(root.transform, false);
            var sequence = sequenceGo.AddComponent<FixtureSequence>(); Bind(sequence, center);
            var child0 = AddLeaf(root, center, "sequence-zero", .2f); var child1 = AddLeaf(root, center, "sequence-delayed", .1f);
            sequence.myPlayables = new List<MyPlayableSequence.MyPlayableTimeCfg> {
                new MyPlayableSequence.MyPlayableTimeCfg { myTime = 0, myPlayable = child0 },
                new MyPlayableSequence.MyPlayableTimeCfg { myTime = .25f, myPlayable = child1 } };
            sequence.Init(null, null); sequence.TryPlay();
            Require(child0.myStatus == MyPlayable.PlayableStatus.Play && child1.myStatus == MyPlayable.PlayableStatus.Stop, "sequence starts zero-delay child immediately");
            yield return null; Drive(center, .25f);
            Require(child0.myStatus == MyPlayable.PlayableStatus.Stop && child1.myStatus == MyPlayable.PlayableStatus.Play && child1.Time == 0, "sequence crossing starts delayed child without consuming earlier delta");
            yield return null; Drive(center, .1f);
            yield return null; Drive(center, .01f);
            Require(sequence.myStatus == MyPlayable.PlayableStatus.Stop && child1.myStatus == MyPlayable.PlayableStatus.Stop, "indefinite sequence ends only after all children stop");

            var bad = AddLeaf(root, center, "stop-exception", -1); bad.ThrowStop = true; bad.Init(null, null); bad.TryPlay();
            int stops = 0; bad.OnStop += _ => stops++;
            bad.ForceStopOnError(); Require(bad.myStatus == MyPlayable.PlayableStatus.Stop && stops == 1, "StopImpl exception retains official stop transition");
            Require(bad.ShouldReportDriveError() && !bad.ShouldReportDriveError(), "drive error is reported once until reset");
            bad.ResetData(); Require(bad.ShouldReportDriveError() && !bad.myInited && bad.mySource == null, "pool reset restores original error and binding state");
            center.RemoveAll(); Require(!center.AllPlayables.Any(), "clear all removes deferred scheduler state");
            UnityEngine.Object.Destroy(root); yield return null;
        }
        private sealed class Runner : MonoBehaviour { }
        public sealed class Leaf : MyPlayable, IMyPlayableStateMutex
        {
            public float Duration = 1;
            public bool Mutex, ThrowStop;
            public List<string> Events;
            public Action<float> Step;
            public override float GetDuration() => Duration;
            protected override void PlayImpl() { Events?.Add("impl"); }
            protected override bool ProcessFrameImpl(float delta) { Step?.Invoke(delta); return false; }
            protected override void StopImpl() { Events?.Add("stop-impl"); if (ThrowStop) throw new InvalidOperationException("expected original playable exception"); }
            public bool IsOpenStateMutex() => Mutex;
            public string GetStateMutexID() => "original-mutex-fixture";
            public GameObject GetStateMutexGo() => null;
            public void GenerateStateMutexID(bool force) { }
            public ActionBehaviour GetMutexAction() => ActionBehaviour.Discard;
        }
        public sealed class FixtureSequence : MyPlayableSequence
        {
            // Match the original serialized non-null list before OnValidate.
            public FixtureSequence() { myPlayables = new List<MyPlayableTimeCfg>(); }
        }
        public sealed class Marker : MyPlayableTrigger
        {
            public List<string> Events;
            public string Value;
            public override void Play(MyPlayable playable) { Events.Add(Value); }
        }
    }
}
