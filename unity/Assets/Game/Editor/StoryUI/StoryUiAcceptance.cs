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
using Zjcs.Official.StoryMasks;

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
            // Use the existing isolated-scene startup guard. The normal boot
            // splash has its own raycasters and must not enter this contract.
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
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
                Debug.Log("Story UI contract passed: native dungeon registrations, real Button/TabItem pointer events, required/optional waits, ClickTimes, window open/close, active/bottom/tab state, cancellation, unregister and camera-rendered native window. Full chapters, automatic end-of-frame scheduling, bloom composition and save recovery remain pending.");
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
                CheckNativeMasks(root, window, battle, events, camera, target, pixels);
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

        private static void CheckNativeMasks(GameObject root, GameObject window, Button battle, EventSystem events,
            Camera camera, RenderTexture target, Texture2D pixels)
        {
            Canvas rootCanvas = root.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.WorldSpace;
            rootCanvas.worldCamera = camera;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1920);
            var binding = new NativeStoryUiBindings(root.transform);
            binding.Nodes.RegisterNode("contract.native.battle", battle.gameObject);
            NativeStoryMaskHost host = NativeStoryMaskHost.Create(root.transform);
            StoryMaskManager masks = host.Manager;
            StoryMask view = host.GetComponent<StoryMask>();
            var ui = new StoryUiModule(binding.FindWindow, binding.Nodes, masks);
            RectTransform rect = battle.GetComponent<RectTransform>();
            Vector2 center = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            ui.ShowFullMask();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var hits = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = center }, hits);
            Require(hits.Count > 0 && hits[0].gameObject == view.FullMask,
                "native full mask blocks the real button; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name))
                + "; rect=" + view.FullMask.GetComponent<RectTransform>().rect
                + "; position=" + view.FullMask.transform.position + "; depth=" + view.FullMask.GetComponent<Graphic>().depth
                + "; root=" + host.GetComponent<RectTransform>().rect + "; active=" + view.FullMask.activeInHierarchy
                + "; hit-order=" + string.Join(";", hits.Select(hit => hit.gameObject.name + ":" + hit.sortingOrder
                    + ":" + hit.sortingLayer + ":" + hit.distance + ":" + hit.module.eventCamera?.name + ":" + hit.gameObject.transform.parent.name)));
            ui.HideFullMask();
            Type rawSettings = typeof(OfficialMainlineMapRuntime).GetNestedType("StoryMaskSettingFields",
                System.Reflection.BindingFlags.NonPublic);
            object raw = JsonUtility.FromJson("{\"ShowMask\":0,\"Scale\":{\"x\":2,\"y\":3,\"z\":1},\"TipUIKeyId\":7}", rawSettings);
            var settings = (MaskSetting)rawSettings.GetMethod("ToMaskSetting").Invoke(raw, null);
            Require(!settings.ShowMask && settings.Scale == new Vector3(2, 3, 1) && settings.TipUIKeyId == 7,
                "integer graph mask flags retain their original values");
            IEnumerator missing = ui.WaitClick("contract.later", true, false, 1, null, true, new MaskSetting());
            Require(missing.MoveNext() && masks.EnableInvisibleMask, "required registration waits under the native full mask");
            ((IDisposable)missing).Dispose();
            Require(!masks.EnableInvisibleMask, "canceling required registration releases the native full mask");
            int actualClicks = 0;
            UnityEngine.Events.UnityAction clicked = () => actualClicks++;
            battle.onClick.AddListener(clicked);
            IEnumerator wait = ui.WaitClick("contract.native.battle", true, false, 2, null, true, new MaskSetting());
            try
            {
                Require(wait.MoveNext() && masks.EnableRectMask && !masks.EnableInvisibleMask,
                    "button guide opens the native hole after the transparent activation mask");
                StoryMaskGraphic graphic = host.GetComponentInChildren<StoryMaskGraphic>(true);
                Require(graphic && graphic.Radius == 23 && graphic.TriangleNum == 10,
                    "guide uses native serialized rounded geometry");
                Require(masks.EnableColorMask, "native color bridge covers the first guide frame");
                // Batch Editor does not execute WaitForEndOfFrame. Verify the
                // original iterator itself at its two boundaries; the automatic
                // frame scheduling requires a windowed Editor/Player check.
                var bridge = (IEnumerator)typeof(StoryMaskGraphic).GetMethod("WaitToCloseFullMask",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(graphic, new object[] { true });
                Require(bridge.MoveNext() && bridge.Current is WaitForEndOfFrame, "original frame boundary is retained");
                Require(!bridge.MoveNext() && !masks.EnableColorMask, "color bridge closes after the original frame boundary");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                hits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = center }, hits);
                Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == battle,
                    "guide hole passes the real native button raycast; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name)));
                var pointer = new PointerEventData(events) { position = center, button = PointerEventData.InputButton.Left };
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Require(wait.MoveNext() && actualClicks == 1, "first native guided pointer keeps ClickTimes wait");
                hits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = new Vector2(100, 1200) }, hits);
                Require(hits.Count > 0 && hits[0].gameObject == graphic.gameObject,
                    "outside the guide hole the native mask blocks pointer input");
                camera.Render();
                pixels.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                    "story-native-guide-mask.png"), pixels.EncodeToPNG());
                ExecuteEvents.ExecuteHierarchy(battle.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Require(!wait.MoveNext() && actualClicks == 2 && !graphic.gameObject.activeSelf && !masks.EnableRectMask,
                    "second guided click closes native hole and detaches node listener");
                IEnumerator canceled = ui.WaitClick("contract.native.battle", true, false, 1, null, true, new MaskSetting());
                Require(canceled.MoveNext(), "second guide waits for actual input");
                ((IDisposable)canceled).Dispose();
                Require(!masks.EnableRectMask && !masks.EnableInvisibleMask, "canceling a guide clears masks");
                ui.ShowFullMask();
                masks.Reset();
                Require(!view.HasAnyMaskPlaying(), "ending a story clears all story mask kinds");
                Debug.Log("Native story mask contract passed: original prefabs/rounded geometry, full-mask blocking, real native Button pointer passthrough inside the hole, outside blocking, ClickTimes, guide cancellation and camera render. End-of-frame iterator boundaries verified explicitly; automatic windowed frame scheduling and full bloom composition remain pending.");
            }
            finally
            {
                (wait as IDisposable)?.Dispose();
                battle.onClick.RemoveListener(clicked);
                masks.Reset();
                UnityEngine.Object.DestroyImmediate(host.gameObject);
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
