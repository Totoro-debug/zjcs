using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportDropdownAcceptance
    {
        [Serializable] private sealed class Receipt { public Binding[] bindings; }
        [Serializable] private sealed class Binding
        {
            public string prefab, path, kind;
            public OfficialBattleReportFoundationAcceptance.Check[] checks;
        }
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "UIDropdown", typeof(WorldUIDropdown) }, { "DropdownItem", typeof(WorldDropdownItem) },
            { "MultiLanguageText", typeof(WorldMultiLanguageText) }, { "LoopVerticalScrollRect", typeof(LoopVerticalScrollRect) },
            { "Image", typeof(Image) }, { "TextMeshProUGUI", typeof(TextMeshProUGUI) },
            { "Button", typeof(Button) }, { "LayoutElement", typeof(LayoutElement) },
            { "HorizontalLayoutGroup", typeof(HorizontalLayoutGroup) }, { "LayoutElementClamper", typeof(WorldLayoutElementClamper) },
            { "NoDrawingRayCast", typeof(NoDrawingRayCast) }
        };

        public static IEnumerator Run()
        {
            Require(Application.isPlaying && EventSystem.current != null, "Report dropdown requires Play Mode and an EventSystem.");
            BattleGameViewInput.Focus();
            yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/battle-hud/report-dropdown-source-manifest.json"));
            var canvasRoot = new GameObject("ReportDropdownFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRoot.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            int components = 0, fields = 0, clicks = 0;
            try
            {
                foreach (var panel in receipt.bindings.GroupBy(value => value.prefab))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(panel.Key.Substring("unity/".Length));
                    Require(asset != null, "Original dropdown prefab missing: " + panel.Key);
                    var root = UnityEngine.Object.Instantiate(asset, canvasRoot.transform, false);
                    root.name = asset.name;
                    try
                    {
                        var nodes = new Dictionary<string, Transform>();
                        OfficialBattleReportFoundationAcceptance.Index(root.transform, "", nodes);
                        var references = new Dictionary<long, UnityEngine.Object>();
                        OfficialBattleReportFoundationAcceptance.MapReferences(asset.transform, root.transform, references);
                        foreach (Binding binding in panel)
                        {
                            Require(nodes.TryGetValue(binding.path, out Transform node), "Original dropdown hierarchy missing: " + binding.path);
                            Component component = node.GetComponent(Types[binding.kind]);
                            Require(component != null, "Original dropdown component missing: " + binding.kind + " at " + binding.path);
                            var serialized = new SerializedObject(component);
                            foreach (var check in binding.checks)
                            {
                                OfficialBattleReportFoundationAcceptance.Match(serialized.FindProperty(check.path), check, references);
                                fields++;
                            }
                            components++;
                        }
                        var dropdown = root.GetComponentInChildren<WorldUIDropdown>(true);
                        if (dropdown == null)
                        {
                            var row = root.GetComponent<WorldDropdownItem>();
                            Require(row != null && row.Texts.Count == 1, "Original common row binding changed.");
                            row.Init(0, "1", true, null);
                            Require(row.SelectStatus.State && Mathf.Abs(row.Texts[0].GetComponent<TMP_Text>().color.r - .13333334f) < .0001f,
                                "Original common-row selected color differs.");
                            row.Refresh("1", 0, false);
                            Require(!row.SelectStatus.State && Mathf.Abs(row.Texts[0].GetComponent<TMP_Text>().color.r - .8862745f) < .0001f,
                                "Original common-row unselected color differs.");
                            continue;
                        }
                        for (Transform parent = dropdown.transform; parent != root.transform.parent; parent = parent.parent) parent.gameObject.SetActive(true);
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        Require(dropdown.Animator != null && dropdown.Animator.runtimeAnimatorController != null
                            && dropdown.Animator.parameters.Any(value => value.name == dropdown.AnimationWorkCondition && value.type == AnimatorControllerParameterType.Bool),
                            "Original dropdown Animator/bool binding is missing.");
                        Require(dropdown.ItemsKey.SequenceEqual(new[] { "1", "2", "3", "4", "5" }), "Original report dropdown fixture keys changed.");
                        // One extra control-only key forces the original pool to allocate its common row prefab
                        // after reusing the five preview rows already serialized in the official report panel.
                        dropdown.ItemsKey.Add("6");
                        Require(dropdown.MyScrollRect.DefaultPrefabSource.Prefab.name == "DropdownItem"
                            && dropdown.MyScrollRect.DefaultPrefabSource.Prefab.GetComponent<WorldDropdownItem>() != null,
                            "Original common DropdownItem dependency is missing.");
                        var updates = new List<int>();
                        dropdown.OnDropdownUpdate += updates.Add;
                        yield return Click(dropdown, dropdown.DropdownButton);
                        clicks++;
                        yield return new WaitForSecondsRealtime(.4f);
                        Require(dropdown.Animator.GetBool(dropdown.AnimationWorkCondition), "Simulated header click did not open the original dropdown.");
                        Transform arrow = dropdown.transform.Find("Content/Arrow");
                        Require(arrow != null && Quaternion.Angle(arrow.localRotation, Quaternion.Euler(180f, 0f, 0f)) < .1f
                            && dropdown.MyScrollRect.gameObject.activeInHierarchy, "Original dropdown show animation did not reach its final arrow/list state.");
                        var fitter = dropdown.Viewport.GetComponent<ContentSizeFitter>();
                        Require(fitter != null && fitter.verticalFit == ContentSizeFitter.FitMode.PreferredSize
                            && dropdown.Viewport.drivenByObject == fitter
                            && Mathf.Abs(dropdown.Viewport.rect.height - LayoutUtility.GetPreferredHeight(dropdown.Viewport)) < .1f,
                            "Original dropdown viewport failed its final preferred-height layout.");
                        var rows = dropdown.MyScrollRect.content.GetComponentsInChildren<WorldDropdownItem>(false);
                        Require(rows.Length > 2, "Original dropdown row pool is empty.");
                        WorldDropdownItem third = rows.First(value => value.Texts.Any(text => text.Text == "3"));
                        yield return Click(dropdown, third.Button);
                        clicks++;
                        Require(dropdown.SelectedItemIndex == 2 && dropdown.HeadLabelText.Text == "3"
                            && updates.SequenceEqual(new[] { 2 }) && !dropdown.Animator.GetBool(dropdown.AnimationWorkCondition),
                            "Original row selection callback, head text or hide order differs.");
                        yield return Click(dropdown, dropdown.DropdownButton);
                        clicks++;
                        yield return new WaitForSecondsRealtime(.4f);
                        rows = dropdown.MyScrollRect.content.GetComponentsInChildren<WorldDropdownItem>(false);
                        third = rows.First(value => value.Texts.Any(text => text.Text == "3"));
                        Require(third.SelectStatus.State && third.Texts.Any(text => text.gameObject.activeInHierarchy),
                            "Original pooled preview-row selection state was not restored.");
                        var common = rows.First(value => value.Texts.Count == 1);
                        int commonIndex = dropdown.GetItemIndex(common.Texts[0].Text);
                        Require(commonIndex >= 0 && Mathf.Abs(common.Texts[0].GetComponent<TMP_Text>().color.r
                            - (common.SelectStatus.State ? .13333334f : .8862745f)) < .0001f,
                            "Original allocated common-row state/color was not restored.");
                        yield return Click(dropdown, common.Button);
                        clicks++;
                        Require(dropdown.SelectedItemIndex == commonIndex && dropdown.HeadLabelText.Text == dropdown.ItemsKey[commonIndex]
                            && updates.SequenceEqual(new[] { 2, commonIndex }),
                            "Simulated common-row click did not update the original selection.");
                        yield return Click(dropdown, dropdown.DropdownButton);
                        clicks++;
                        yield return new WaitForSecondsRealtime(.4f);
                        common = dropdown.MyScrollRect.content.GetComponentsInChildren<WorldDropdownItem>(false)
                            .First(value => value.Texts.Count == 1);
                        Require(Mathf.Abs(common.Texts[0].GetComponent<TMP_Text>().color.r
                            - (common.SelectStatus.State ? .13333334f : .8862745f)) < .0001f,
                            "Original recycled common-row state/color was not restored.");
                        yield return Capture("report-dropdown-open.png");
                        // The injected global-input module is represented by the same pointer position/raycast adapter as the port.
                        Vector2 outside = new Vector2(Screen.width * .5f, Screen.height * .1f);
                        dropdown.OnGlobalPointerDown(outside);
                        yield return BattleGameViewInput.ClickScreenPoint(outside, "outside original report dropdown");
                        clicks++;
                        Require(!dropdown.Animator.GetBool(dropdown.AnimationWorkCondition) && updates.Count == 2,
                            "Original outside-pointer hide unexpectedly changed selection.");
                        yield return Click(dropdown, dropdown.DropdownButton);
                        clicks++;
                        yield return new WaitForSecondsRealtime(.4f);
                        yield return Click(dropdown, dropdown.DropdownButton);
                        clicks++;
                        Require(!dropdown.Animator.GetBool(dropdown.AnimationWorkCondition), "Second header click did not close the dropdown.");
                        // Complete the original Hide transition before separately testing a disable/re-enable.
                        // Its default Idle clip only drives the list activation and has no arrow rotation curve.
                        yield return new WaitForSecondsRealtime(.4f);
                        Require(Quaternion.Angle(arrow.localRotation, Quaternion.identity) < .1f
                            && !dropdown.MyScrollRect.gameObject.activeInHierarchy, "Original dropdown hide animation did not reach its final arrow/list state: arrow="
                            + arrow.localEulerAngles + ", list=" + dropdown.MyScrollRect.gameObject.activeInHierarchy);
                        dropdown.gameObject.SetActive(false);
                        dropdown.gameObject.SetActive(true);
                        yield return null;
                        Require(dropdown.SelectedItemIndex == commonIndex, "Original empty HeadKey dropdown lost selection after re-enable.");
                        yield return Capture("report-dropdown-closed.png");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Require(components == 28 && clicks == 8, "Original report dropdown validation scope changed.");
                Debug.Log("Original report dropdown passed: " + components + " components, " + fields
                    + " original fields/references, reused preview rows and allocated common row, managed object/color states, 8 simulated clicks, selection callback and outside/disable hiding. Player report binding, global input host, full language/effects and Wwise audio remain pending.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvasRoot); }
        }

        private static IEnumerator Click(WorldUIDropdown dropdown, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "Another original report graphic obstructs dropdown input: " + (hits.Count > 0 ? hits[0].gameObject.name : "no hit") + " over " + button.name + " at " + point);
            dropdown.OnGlobalPointerDown(point);
            yield return BattleGameViewInput.Click(button);
        }

        private static IEnumerator Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
            if (string.IsNullOrEmpty(output)) yield break;
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { Directory.CreateDirectory(output); File.WriteAllBytes(System.IO.Path.Combine(output, name), image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
