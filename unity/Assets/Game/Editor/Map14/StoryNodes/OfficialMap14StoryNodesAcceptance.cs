using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cinemachine;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.StoryNodes;

namespace Zjcs.Editor.Map14
{
    // Original dictionary, lifecycle, asynchronous waits and native Timeline
    // camera binding only. Full original map/camera visuals are not exercised.
    public static class OfficialMap14StoryNodesAcceptance
    {
        private const string Key = "Zjcs.Map14.StoryNodesAcceptance";
        private static bool started, expectedError;
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
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (expectedError && type == LogType.Error && (message.StartsWith("注册剧情外部节点失败") || message.StartsWith("获取剧情节点失败") || message.StartsWith("Official playable diagnostic"))) return;
            errors++;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 original story nodes verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original story nodes verification").AddComponent<Runner>().StartCoroutine(Verify());
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
                Debug.Log("Map14 original story node Play Mode passed: native bindings, overwrite/event order, missing/null/destroyed lookup, optional async waits/cancellation, register enable/disable count rules, shared profession node host and native Cinemachine Timeline binding/preservation. Full original Timeline/camera/map visuals, UI/input and saves remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original story nodes: " + message); }
        private static IEnumerator Steps()
        {
            foreach (Type type in new[] { typeof(StoryNodeRegisterMgr), typeof(StoryNodeRegister), typeof(CinemachineTrackStoryBinder) })
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Runtime/Official/StoryNodes/" + type.Name + ".cs");
                Require(script != null && script.GetClass() == type, type.Name + " native MonoScript binding");
            }
            var root = new GameObject("Original external nodes fixtures"); var first = new GameObject("first"); first.transform.SetParent(root.transform);
            var second = new GameObject("second"); second.transform.SetParent(root.transform); var marker = second.AddComponent<Marker>();
            var manager = new StoryNodeRegisterMgr(); var records = new List<string>();
            manager.OnRegisterNode += key => { records.Add("+:" + key); Require(manager.TryGetNode(key, out var registered) && registered != null, "register callback runs after dictionary assignment"); };
            manager.OnUnregisterNode += key => { records.Add("-:" + key); Require(!manager.TryGetNode(key, out _), "unregister callback runs after dictionary removal"); };
            manager.RegisterNode("key", first); manager.RegisterNode("key", second);
            Require(manager.GetNode("key") == second && manager.GetNode<Marker>("key") == marker && records.SequenceEqual(new[] { "+:key", "+:key" }), "overwrite emits original register event without synthetic unregister");
            manager.UnregisterNode("key"); manager.UnregisterNode("key"); Require(records.Count == 4 && manager.GetNode("key") == null, "missing unregister still emits original event");
            expectedError = true; manager.RegisterNode("null", null); manager.RegisterNode("component-missing", first);
            Require(manager.GetNode<Marker>("component-missing") == null && !manager.TryGetNode("null", out _), "null registration and missing component preserve original diagnostic/default behavior"); expectedError = false;

            var immediate = manager.GetNodeAsync("component-missing", false, new CancellationToken(true));
            Require(immediate.Status == UniTaskStatus.Succeeded && immediate.GetAwaiter().GetResult() == first, "non-wait lookup ignores unused cancellation token as original");
            var pending = manager.GetNodeAsync("late", true, CancellationToken.None); Require(pending.Status == UniTaskStatus.Pending, "original wait remains pending while absent");
            yield return null; manager.RegisterNode("late", first);
            float deadline = Time.realtimeSinceStartup + 3;
            while (pending.Status == UniTaskStatus.Pending) { Require(Time.realtimeSinceStartup < deadline, "original async GameObject wait"); yield return null; }
            Require(pending.GetAwaiter().GetResult() == first, "original wait returns registered node");
            var typed = manager.GetNodeAsync<Marker>("typed-late", true, CancellationToken.None); manager.RegisterNode("typed-late", second);
            deadline = Time.realtimeSinceStartup + 3;
            while (typed.Status == UniTaskStatus.Pending) { Require(Time.realtimeSinceStartup < deadline, "original typed async wait"); yield return null; }
            Require(typed.GetAwaiter().GetResult() == marker, "original typed wait resolves exact component");
            using (var cancel = new CancellationTokenSource())
            {
                var cancelled = manager.GetNodeAsync("cancelled", true, cancel.Token); cancel.Cancel();
                Require(cancelled.Status == UniTaskStatus.Pending, "cancelImmediately=false does not synchronously cancel waiter");
                deadline = Time.realtimeSinceStartup + 3;
                while (cancelled.Status == UniTaskStatus.Pending) { Require(Time.realtimeSinceStartup < deadline, "original cancellation wait"); yield return null; }
                bool observed = false; try { cancelled.GetAwaiter().GetResult(); } catch (OperationCanceledException) { observed = true; }
                Require(observed, "original async cancellation propagates instead of returning fake node");
            }
            var doomed = new GameObject("destroyed-node"); manager.RegisterNode("destroyed", doomed); UnityEngine.Object.Destroy(doomed); yield return null;
            Require(manager.TryGetNode("destroyed", out var destroyed) && destroyed == null && manager.GetNode<Marker>("destroyed") == null,
                "original dictionary retains destroyed key while Unity object lookup is null");

            var registryGo = new GameObject("Original StoryNodeRegister fixture"); registryGo.SetActive(false); registryGo.transform.SetParent(root.transform);
            var registry = registryGo.AddComponent<StoryNodeRegister>(); registry.NodeRegisterManager = manager;
            registry.StoryNodeKeys = new List<string> { "reg-first", "reg-null", "reg-unmatched" }; registry.StoryNodes = new List<GameObject> { first, null };
            expectedError = true; registryGo.SetActive(true); expectedError = false;
            Require(manager.GetNode("reg-first") == first && !manager.TryGetNode("reg-null", out _) && !manager.TryGetNode("reg-unmatched", out _), "enable registers only matched non-null entries");
            int count = records.Count; registryGo.SetActive(false);
            Require(records.Skip(count).SequenceEqual(new[] { "-:reg-first", "-:reg-null", "-:reg-unmatched" }), "disable unregisters every key including unmatched entries");
            registry.StoryNodes[1] = second; registry.StoryNodeKeys.RemoveAt(2); registryGo.SetActive(true);
            Require(manager.GetNode("reg-first") == first && manager.GetNode("reg-null") == second, "reenable repeats original registration lifecycle");
            var uiGo = new GameObject("Shared profession node fixture"); uiGo.transform.SetParent(root.transform);
            var shared = uiGo.AddComponent<Zjcs.Official.ProfessionUI.StoryNodeRegister>();
            shared.StoryNodeKeys = new List<string> { "shared" }; shared.StoryNodes = new List<GameObject> { second }; shared.Init(manager);
            Require(manager.GetNode("shared") == second, "original profession registry shares the same external node host");

            var camera = new GameObject("Registered story camera"); camera.transform.SetParent(root.transform); camera.AddComponent<Camera>(); var brain = camera.AddComponent<CinemachineBrain>();
            manager.RegisterNode("camera", camera);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Original unbound camera track");
            var retainedTrack = timeline.CreateTrack<CinemachineTrack>(null, "Original prebound camera track");
            var otherCamera = new GameObject("Existing camera binding"); otherCamera.transform.SetParent(root.transform); otherCamera.AddComponent<Camera>(); var otherBrain = otherCamera.AddComponent<CinemachineBrain>();
            var timelineGo = new GameObject("Original camera Timeline binding fixture"); timelineGo.transform.SetParent(root.transform); timelineGo.SetActive(false);
            var director = timelineGo.AddComponent<PlayableDirector>(); director.playableAsset = timeline; director.SetGenericBinding(retainedTrack, otherBrain);
            var binder = timelineGo.AddComponent<CinemachineTrackStoryBinder>(); binder.MainCameraKey = "camera"; binder.NodeRegisterManager = manager; timelineGo.SetActive(true);
            Require(director.GetGenericBinding(cameraTrack) == brain && director.GetGenericBinding(retainedTrack) == otherBrain, "original binder fills empty Cinemachine track and preserves existing binding");
            var missingGo = new GameObject("Absent camera key fixture"); missingGo.SetActive(false); missingGo.transform.SetParent(root.transform);
            var missingDirector = missingGo.AddComponent<PlayableDirector>(); missingDirector.playableAsset = timeline;
            var missingBinder = missingGo.AddComponent<CinemachineTrackStoryBinder>(); missingBinder.MainCameraKey = "missing-camera"; missingBinder.NodeRegisterManager = manager; missingGo.SetActive(true);
            Require(missingDirector.GetGenericBinding(cameraTrack) == null, "original absent key leaves track unbound without substitute camera");
            registryGo.SetActive(false); uiGo.SetActive(false); UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(timeline); yield return null;
        }
        private sealed class Runner : MonoBehaviour { }
        public sealed class Marker : MonoBehaviour { }
    }
}
