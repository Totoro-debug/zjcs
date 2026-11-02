using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static class OfficialBattleAssistUiAcceptance
    {
        private const string Key = "Zjcs.Battle.NativeAssistUiProbe";
        private static bool started;
        private static int errors;
        [Serializable] private sealed class Texts { public Entry[] texts; }
        [Serializable] private sealed class Entry { public string key, value; }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            typeof(Skills.SkillPlayModeAcceptance).GetMethod("ConfigurePortraitGameView",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 800 });
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Native assistance UI verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Native assistance UI verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            IEnumerator steps = Steps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0) { SessionState.SetInt(Key + ".result", 0); Debug.Log("Shared assistance view passed native list, selection, confirmation, cancel and disposal input checks."); }
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var host = new GameObject("Native UI host", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var camera = new GameObject("Native invitation UI camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10; camera.backgroundColor = Color.black;
            var hostCanvas = host.GetComponent<Canvas>(); hostCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hostCanvas.worldCamera = camera; hostCanvas.planeDistance = 1; hostCanvas.sortingOrder = 60;
            var underlay = new GameObject("Invitation underlay fixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            underlay.transform.SetParent(host.transform, false);
            underlay.GetComponent<Canvas>().overrideSorting = true;
            underlay.GetComponent<Canvas>().sortingOrder = 80;
            var underlayRect = (RectTransform)underlay.transform; underlayRect.anchorMin = Vector2.zero; underlayRect.anchorMax = Vector2.one;
            underlayRect.offsetMin = Vector2.zero; underlayRect.offsetMax = Vector2.zero;
            var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var settings = JsonUtility.FromJson<Zjcs.Presentation.World.OfficialMap11UiData>(Resources.Load<TextAsset>("Map12/UI/catalog").text).worldBindings.canvasScaler;
            scaler.uiScaleMode = (CanvasScaler.ScaleMode)settings.scaleMode;
            scaler.referenceResolution = new Vector2(settings.referenceResolution[0], settings.referenceResolution[1]);
            scaler.screenMatchMode = (CanvasScaler.ScreenMatchMode)settings.screenMatchMode;
            scaler.matchWidthOrHeight = settings.matchWidthOrHeight;
            var candidates = Enumerable.Range(1, 20).Select(id => new OfficialBattleAssistSelectionView.Candidate {
                Id = id, Name = "Candidate " + id, Available = id != 2,
                FriendshipBlocked = id == 2, Reason = id == 2 ? "Unavailable" : string.Empty }).ToArray();
            int changed = 0, confirmed = 0, cancelled = 0;
            OfficialBattleAssistSelectionView view = null;
            view = new OfficialBattleAssistSelectionView(host.transform, candidates, 3, (id, selected) => {
                candidates.Single(value => value.Id == id).Selected = selected;
                changed++; view.Refresh(candidates, 3);
            }, () => confirmed++, () => cancelled++);
            GameObject panel = view.Root;
            for (int i = 0; i < 60; i++) yield return null;
            Canvas.ForceUpdateCanvases();
            var list = panel.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/Scroll").GetComponent<LoopVerticalScrollRect>();
            Require(list.totalCount == 20, "host candidates supplied to original virtual list");
            Toggle first = panel.GetComponentsInChildren<Transform>().Single(value => value.name == "Npc_1").Find("Content/NormalGo/Option2").GetComponent<Toggle>();
            Toggle blocked = panel.GetComponentsInChildren<Transform>().Single(value => value.name == "Npc_2").Find("Content/NormalGo/Option2").GetComponent<Toggle>();
            Require(!blocked.IsInteractable(), "unavailable candidate cannot be selected");
            Click(first); yield return null;
            Require(changed == 1 && candidates[0].Selected && first.isOn, "selection refresh through public interface");
            Click(first); yield return null;
            Require(changed == 2 && !candidates[0].Selected && !first.isOn, "deselection refresh through public interface");
            view.Refresh(candidates.Take(2).ToArray(), 3); yield return null;
            Require(list.content.GetComponentsInChildren<Toggle>().Length == 2, "shrinking host candidates releases old visible cells");
            view.Refresh(candidates, 3); yield return null;
            Require(list.content.GetComponentsInChildren<Toggle>().Length > 2, "growing host candidates refills original list");
            yield return new WaitForEndOfFrame();
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "shared-assist-ui.png"), screenshot.EncodeToPNG());
            UnityEngine.Object.Destroy(screenshot);
            Button button = panel.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/BottomGo")
                .GetComponentsInChildren<Button>().First(value => value.name == "CommonBtn_12");
            Click(button); Require(confirmed == 1, "original confirmation pointer input");
            Click(panel.GetComponentsInChildren<Button>().First(value => value.name == "CloseBtn_dark"));
            Require(cancelled == 1, "original close pointer input");
            foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>()) Require(graphic.materialForRendering.shader.isSupported, "native UI shader " + graphic.name);
            view.Dispose(); yield return null;
            Require(panel == null, "shared view releases its native window");
            UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(camera.gameObject); yield return null;
        }

        private static void Click(Selectable button)
        {
            Require(button.IsActive() && button.IsInteractable(), "active confirmation button");
            var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
            Camera camera = button.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            var hits = new List<RaycastResult>(); var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            for (int y = 1; y < 10; y++) for (int x = 1; x < 10; x++)
            {
                pointer.position = RectTransformUtility.WorldToScreenPoint(camera, Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], x / 10f), Vector3.Lerp(corners[1], corners[2], x / 10f), y / 10f));
                hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button.gameObject) continue;
                var target = hits[0].gameObject; ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler); return;
            }
            throw new InvalidOperationException("Native invitation confirmation has no visible pointer target.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        public sealed class Runner : MonoBehaviour { }
    }
}
