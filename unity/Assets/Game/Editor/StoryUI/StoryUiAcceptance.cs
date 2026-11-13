using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Story;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.StoryUI
{
    // Contract verification against the shipped native prefab. This is not a
    // full chapter, combat, mask, visual or save-recovery acceptance.
    public static class StoryUiAcceptance
    {
        private const string Key = "Zjcs.StoryUI.Contract";
        private static bool started;
        private static int runtimeErrors;
        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 120);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod] private static void Hook()
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
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Story UI contract timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Verify();
        }

        private static void Verify()
        {
            GameObject root = null;
            Application.logMessageReceived += Observe;
            try
            {
                AudioListener.volume = 0;
                var legacy = JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map10/stories").text);
                var restore = typeof(OfficialMainlineMapRuntime).GetMethod("RestoreStoryNodeParameters",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                foreach (Map10RuntimeStory story in legacy.stories)
                {
                    restore.Invoke(null, new object[] { story });
                    Require(Array.TrueForAll(story.nodes, node => !string.IsNullOrEmpty(node.sourceJson)),
                        "legacy source graph supplies every original node's parameters");
                }
                root = new GameObject("Story UI contract scope", typeof(RectTransform));
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1920);
                root.SetActive(false);
                GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/884_156692/DailyDungeonDialogPrivateMap");
                Require(prefab, "native private dungeon prefab is packaged");
                GameObject window = UnityEngine.Object.Instantiate(prefab, root.transform);
                var binding = new NativeStoryUiBindings(root.transform);
                binding.BindNativeWindow(window, "DailyDungeonDialogPrivateMap");
                typeof(NativeStoryUiBindings).Assembly.GetType("Zjcs.Presentation.World.OfficialDungeonUiBindingRuntime", true)
                    .GetMethod("Apply").Invoke(null, new object[] { window, "DailyDungeonDialogPrivateMap",
                        new Action<AudioClip, GameObject>((clip, emitter) => { }) });
                typeof(NativeStoryUiBindings).Assembly.GetType("Zjcs.Presentation.World.OfficialDungeonUiBindingRuntime", true)
                    .GetMethod("RefreshPrivateDungeonDetails").Invoke(null, new object[] { window, 21, 0, 3, 9 });
                var ui = new StoryUiModule(binding.FindWindow, binding.Nodes);
                IEnumerator opened = ui.WaitWindow("DailyDungeon/DailyDungeonDialogPrivateMap", true);
                Require(opened.MoveNext(), "inactive window cannot satisfy opening");
                root.SetActive(true);
                Require(!opened.MoveNext(), "native prefab name satisfies original window-open predicate");
                GameObject nativeBattle = binding.FindNode("DailyDungeonDialogPrivateMap.BattleBtn");
                Require(nativeBattle && nativeBattle.name == "BattleBtn", "use serialized native battle registration");
                Require(binding.FindNode("DailyDungeonDialogPrivateMap.TeamBtn")?.name == "TeamBtn", "use serialized native team registration");
                // Isolate the node contract from the unconfigured window's locks.
                // This changes fixture state only, never the packaged prefab.
                for (Transform node = nativeBattle.transform; node != root.transform; node = node.parent)
                    node.gameObject.SetActive(true);
                var battle = nativeBattle.GetComponent<Button>();
                Require(battle, "registered target is an original Unity Button");
                battle.interactable = true;
                // The production private-map entry also disables online matching.
                foreach (Transform child in window.GetComponentsInChildren<Transform>(true))
                    if (child.name == "GameMatch") child.gameObject.SetActive(false);
                var events = new GameObject("Simulated story input", typeof(EventSystem));
                events.transform.SetParent(root.transform);
                CaptureNativeWindow(root, window, battle, events.GetComponent<EventSystem>());
                int businessClicks = 0, completed = 0;
                battle.onClick.AddListener(() => businessClicks++);
                IEnumerator waiting = null;
                var graph = new StoryModule<object, Node>(_ => 1, (_, id) => new Node { Id = id }, (_, node, resume) =>
                {
                    waiting = Continue(node.Id == 1
                        ? ui.WaitClick("DailyDungeonDialogPrivateMap.BattleBtn", true, false, 2)
                        : ui.WaitWindow("DailyDungeon/DailyDungeonDialogPrivateMap", false), () => resume(node.Id == 1 ? 2 : 0));
                    return StoryTransition<object>.Wait;
                });
                graph.Play(new object(), () => completed++);
                Require(waiting.MoveNext() && graph.IsRunning && businessClicks == 0, "entering guide never synthesizes battle input");
                PointerClick(battle, events.GetComponent<EventSystem>());
                Require(waiting.MoveNext() && businessClicks == 1 && completed == 0, "original ClickTimes is respected");
                PointerClick(battle, events.GetComponent<EventSystem>());
                IEnumerator previous = waiting;
                Require(!previous.MoveNext() && graph.IsRunning && completed == 0, "second click advances to window-close wait");
                Require(waiting.MoveNext(), "open native window cannot satisfy closing");
                window.SetActive(false);
                Require(!waiting.MoveNext() && !graph.IsRunning && completed == 1, "native close finishes the graph once");
                Require(!binding.FindNode("DailyDungeonDialogPrivateMap.BattleBtn"), "disabled registration is unavailable");

                IEnumerator optional = ui.WaitClick("missing", false, false, 1);
                Require(!optional.MoveNext(), "optional absent registration follows original skip");
                IEnumerator required = ui.WaitClick("missing", true, false, 1);
                Require(required.MoveNext(), "required absent registration stays pending");
                (required as IDisposable)?.Dispose();

                window.SetActive(true);
                IEnumerator cancelled = ui.WaitClick("DailyDungeonDialogPrivateMap.BattleBtn", true, false, 1);
                Require(cancelled.MoveNext(), "click wait is attached");
                (cancelled as IDisposable)?.Dispose();
                IEnumerator newWait = ui.WaitClick("DailyDungeonDialogPrivateMap.BattleBtn", true, true, 2);
                Require(newWait.MoveNext(), "new guide can use same native button after cancellation");
                PointerClick(battle, events.GetComponent<EventSystem>());
                Require(newWait.MoveNext(), "a click cannot count twice after cancelled listener cleanup");
                window.SetActive(false);
                Require(!newWait.MoveNext(), "EndOnUnregister ends wait when original registration disables");
                window.SetActive(true);
                IEnumerator transient = ui.WaitClick("DailyDungeonDialogPrivateMap.BattleBtn", true, true, 1);
                Require(transient.MoveNext(), "transient unregister fixture starts pending");
                window.SetActive(false);
                window.SetActive(true);
                Require(!transient.MoveNext(), "same-frame unregister and re-register still ends original wait");
                IEnumerator active = ui.WaitActive("DailyDungeonDialogPrivateMap.BattleBtn", false);
                Require(active.MoveNext(), "active target cannot satisfy inactive predicate");
                nativeBattle.SetActive(false);
                Require(!active.MoveNext(), "activeSelf change satisfies original predicate");

                var tabObject = new GameObject("Original tab contract", typeof(RectTransform), typeof(Zjcs.Official.WorldUI.WorldTabItem));
                tabObject.transform.SetParent(root.transform);
                var tab = tabObject.GetComponent<Zjcs.Official.WorldUI.WorldTabItem>();
                binding.Nodes.RegisterNode("contract.tab", tabObject);
                IEnumerator tabWait = ui.WaitTab("contract.tab", true);
                Require(tabWait.MoveNext(), "tab wait uses actual initial state");
                ExecuteEvents.Execute(tabObject, new PointerEventData(events.GetComponent<EventSystem>())
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Require(!tabWait.MoveNext() && tab.isOn, "simulated pointer changes original TabItem and releases wait");
                Require(!ui.WaitTab("missing", true).MoveNext(), "original missing-tab skip is preserved");
                bool? barState = null;
                Require(!ui.WaitBottomBar(() => barState, false, true).MoveNext(), "optional absent bottom bar skips");
                IEnumerator bottom = ui.WaitBottomBar(() => barState, true, true);
                Require(bottom.MoveNext(), "required absent bottom bar stays pending");
                barState = false;
                Require(bottom.MoveNext(), "hidden bottom bar cannot satisfy shown predicate");
                barState = true;
                Require(!bottom.MoveNext(), "bottom wait finishes only in original target state");
                Require(runtimeErrors == 0, "native fixture has no runtime errors");
                Debug.Log("Story UI contract passed: native dungeon registrations, real Button/TabItem pointer events, required/optional waits, ClickTimes, window open/close, active/bottom/tab state, cancellation, unregister and camera-rendered native window. Full chapters, guide masks and save recovery remain pending.");
                SessionState.SetInt(Key + ".result", 0);
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (root) UnityEngine.Object.DestroyImmediate(root);
                Application.logMessageReceived -= Observe;
                EditorApplication.isPlaying = false;
            }
        }

        private static void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++; }

        private static void CaptureNativeWindow(GameObject root, GameObject window, Button battle, EventSystem events)
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace)) throw new InvalidOperationException("Story UI validation workspace is required.");
            var host = new GameObject("Background validation camera", typeof(Camera));
            host.transform.SetParent(root.transform);
            Camera camera = host.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.orthographic = true;
            camera.orthographicSize = 960;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            Canvas canvas = window.GetComponent<Canvas>();
            canvas.worldCamera = camera;
            var target = new RenderTexture(720, 1280, 24);
            var pixels = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(workspace, "story-native-window.png"), pixels.EncodeToPNG());
                RectTransform rect = battle.GetComponent<RectTransform>();
                Require(rect.rect.width > 0, "native button retains its layout");
                var hits = new List<RaycastResult>();
                window.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(events)
                    { position = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)) }, hits);
                Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == battle,
                    "native battle button is the first pointer raycast hit; actual="
                    + string.Join(",", hits.Select(hit => hit.gameObject.name))
                    + "; position=" + camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)));
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static IEnumerator Continue(IEnumerator wait, Action completed)
        {
            try { while (wait.MoveNext()) yield return wait.Current; }
            finally { (wait as IDisposable)?.Dispose(); }
            completed();
        }

        private static void PointerClick(Button button, EventSystem system) => ExecuteEvents.Execute(button.gameObject,
            new PointerEventData(system) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Story UI: " + message); }
        private sealed class Node { public int Id; }
    }
}
