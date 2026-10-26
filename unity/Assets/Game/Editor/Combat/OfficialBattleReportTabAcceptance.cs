using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Combat
{
    /// <summary>Exercises recovered native report tab components. Full report rendering/data remain unfinished.</summary>
    public static class OfficialBattleReportTabAcceptance
    {
        public static IEnumerator Run()
        {
            Require(Application.isPlaying, "Report tab input must run in Play Mode.");
            var canvasRoot = new GameObject("ReportTabFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var events = EventSystem.current;
            GameObject eventRoot = events == null ? new GameObject("ReportTabEventSystem", typeof(EventSystem)) : null;
            if (events == null) events = eventRoot.GetComponent<EventSystem>();
            try
            {
                int groups = 0, clicks = 0;
                foreach (string key in new[] { "FightEndReportPanel", "FightEndAllPlayerReportPanel", "FightEndReportSkillTabView" })
                {
                    GameObject asset = Resources.Load<GameObject>("Combat/BattleReport/" + key);
                    Require(asset != null, "Native report prefab is missing: " + key);
                    var root = UnityEngine.Object.Instantiate(asset, canvasRoot.transform, false);
                    try
                    {
                        yield return null;
                        foreach (WorldTabGroup group in root.GetComponentsInChildren<WorldTabGroup>(true))
                        {
                            for (Transform parent = group.transform; parent != root.transform.parent; parent = parent.parent)
                                parent.gameObject.SetActive(true);
                            yield return null;
                            WorldTabItem[] tabs = group.GetAllTabs();
                            Require(tabs.Length >= 2 && tabs.All(tab => tab != null), "Native tab group lost its original references.");
                            Require(!group.allowSwitchOff, "Native report tab group unexpectedly allows no selection.");
                            var callbacks = new List<string>();
                            for (int index = 0; index < tabs.Length; index++)
                            {
                                int captured = index;
                                tabs[index].OnValueChanged.AddListener(value => callbacks.Add("item:" + captured + ":" + value));
                                var transition = tabs[index].GetComponent<WorldTabAnimatorTransition>();
                                var serialized = new SerializedObject(tabs[index]);
                                Require(serialized.FindProperty("tabTransition").intValue == 1
                                    && serialized.FindProperty("m_TabTransitionScript").objectReferenceValue == transition,
                                    "Original custom transition reference was not restored: mode=" + serialized.FindProperty("tabTransition").intValue
                                    + ", reference=" + serialized.FindProperty("m_TabTransitionScript").objectReferenceValue
                                    + ", expected=" + transition + ", tab=" + Path(tabs[index].transform));
                                Require(transition != null && transition.Anim != null && transition.Anim.runtimeAnimatorController != null,
                                    "Original tab animation binding is missing: " + tabs[index].name);
                            }
                            group.OnTabItemChanged.AddListener((tab, value) => callbacks.Add("group:" + Array.IndexOf(tabs, tab) + ":" + value));
                            group.ResetTabItem(false);
                            Require(tabs[0].isOn && tabs.Count(tab => tab.isOn) == 1, "Original reset tab/order was not preserved.");
                            callbacks.Clear();
                            group.ResetTabItem(true);
                            Require(string.Join("|", callbacks) == "group:0:True|item:0:True", "Reset callback order differs from the official client.");
                            callbacks.Clear();
                            for (int index = 1; index < tabs.Length; index++)
                            {
                                Click(events, tabs[index]);
                                clicks++;
                                Require(tabs[index].isOn && tabs.Count(tab => tab.isOn) == 1, "Simulated click did not select exactly one native tab.");
                                int previous = index - 1;
                                string expected = "item:" + previous + ":False|group:" + previous + ":False|item:" + index + ":True|group:" + index + ":True";
                                Require(string.Join("|", callbacks) == expected, "Tab callback order differs: " + string.Join("|", callbacks));
                                callbacks.Clear();
                                Click(events, tabs[index]);
                                Require(callbacks.Count == 0 && tabs[index].isOn, "Selected native tab did not ignore repeated clicks.");
                                clicks++;
                                var transition = tabs[index].GetComponent<WorldTabAnimatorTransition>();
                                for (int sample = 0; sample < 20; sample++) transition.Anim.Update(0.05f);
                                Require(transition.Anim.GetCurrentAnimatorStateInfo(0).IsName(transition.ToggleOn), "Native ToggleOn animation did not run: current="
                                    + transition.Anim.GetCurrentAnimatorStateInfo(0).shortNameHash + ", next=" + transition.Anim.GetNextAnimatorStateInfo(0).shortNameHash
                                    + ", transition=" + transition.Anim.IsInTransition(0));
                            }
                            ExecuteEvents.Execute(tabs[0].gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
                            Require(tabs[0].isOn && tabs.Count(tab => tab.isOn) == 1, "Submit input did not select the native first tab.");
                            groups++;
                        }
                        string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
                        if (!string.IsNullOrEmpty(output))
                        {
                            yield return new WaitForEndOfFrame();
                            Directory.CreateDirectory(output);
                            var image = ScreenCapture.CaptureScreenshotAsTexture();
                            try { File.WriteAllBytes(System.IO.Path.Combine(output, key + "-tabs.png"), image.EncodeToPNG()); }
                            finally { UnityEngine.Object.DestroyImmediate(image); }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Require(groups == 5, "Expected five original groups across the three report prefabs; got " + groups);
                Debug.Log("Original report tab input passed: groups=" + groups + ", simulated clicks=" + clicks
                    + ", original default selection, event ordering, repeated-click suppression, keyboard submit and native Animator controllers. Full report visuals/data remain unfinished.");
            }
            finally
            {
                if (eventRoot != null) UnityEngine.Object.DestroyImmediate(eventRoot);
                UnityEngine.Object.DestroyImmediate(canvasRoot);
            }
        }

        private static void Click(EventSystem events, WorldTabItem target)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)target.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(events) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No native graphic received simulated pointer input: " + target.name + " at " + position
                + "; hierarchy=" + Hierarchy(target.transform));
            GameObject hit = hits[0].gameObject;
            Require(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == target.gameObject,
                "A native report graphic obstructs the intended tab: " + Path(hit.transform) + " over " + Path(target.transform)
                + " at " + position + "; target scale=" + target.transform.lossyScale + "; rect=" + rect.rect
                + "; hits=" + string.Join("|", hits.Take(8).Select(value => Path(value.gameObject.transform) + ":" + value.sortingOrder)));
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static string Path(Transform node) => node.parent == null ? node.name : Path(node.parent) + "/" + node.name;
        private static string Hierarchy(Transform node)
        {
            if (node == null) return "";
            var rect = node as RectTransform;
            var canvas = node.GetComponent<Canvas>();
            return node.name + ":pos=" + node.position + ":scale=" + node.lossyScale + ":rect=" + (rect == null ? "" : rect.rect.ToString())
                + (canvas == null ? "" : ":canvas=" + canvas.renderMode + ":root=" + canvas.rootCanvas.name + ":pixel=" + canvas.pixelRect)
                + "|" + Hierarchy(node.parent);
        }

        private static void Require(bool value, string message) { if (!value) throw new BuildFailedException(message); }
    }
}
