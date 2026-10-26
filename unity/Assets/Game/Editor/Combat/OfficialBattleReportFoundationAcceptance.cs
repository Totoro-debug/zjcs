using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UGUIJszx;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleReportFoundationAcceptance
    {
        [Serializable] private sealed class Receipt { public Binding[] bindings; }
        [Serializable] private sealed class Binding { public string prefab, path, kind; public Check[] checks; }
        [Serializable] private sealed class Check { public string path, kind, text; public double number; public int count; public Reference reference; }
        [Serializable] private sealed class Reference { public long fileID; public string guid; }
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "LayoutElementClamper", typeof(WorldLayoutElementClamper) },
            { "MaintainGlobalScaleUI", typeof(WorldMaintainGlobalScaleUI) },
            { "GridLayoutGroupJSZX", typeof(GridLayoutGroupJSZX) },
            { "HorizontalLayoutGroupJSZX", typeof(HorizontalLayoutGroupJSZX) },
            { "ScrollRect", typeof(ScrollRect) }, { "LoopVerticalScrollRect", typeof(LoopVerticalScrollRect) },
            { "Button", typeof(Button) }, { "Slider", typeof(Slider) }, { "Mask", typeof(Mask) }
        };

        public static IEnumerator Run()
        {
            Require(Application.isPlaying, "Report layout input requires Play Mode.");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/battle-hud/report-foundation-source-manifest.json"));
            var canvasRoot = new GameObject("ReportLayoutFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRoot.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            int components = 0, fields = 0, drags = 0;
            try
            {
                foreach (var panel in receipt.bindings.GroupBy(value => value.prefab))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(panel.Key.Substring("unity/".Length));
                    Require(asset != null, "Original report prefab missing: " + panel.Key);
                    var root = UnityEngine.Object.Instantiate(asset, canvasRoot.transform, false);
                    root.name = asset.name;
                    try
                    {
                        var nodes = new Dictionary<string, Transform>();
                        Index(root.transform, "", nodes);
                        var localReferences = new Dictionary<long, UnityEngine.Object>();
                        MapReferences(asset.transform, root.transform, localReferences);
                        foreach (Binding binding in panel)
                        {
                            Require(nodes.TryGetValue(binding.path, out Transform node), "Report layout hierarchy changed: " + binding.path);
                            Component component = node.GetComponent(Types[binding.kind]);
                            Require(component != null, "Report layout script not bound: " + binding.kind + " at " + binding.path);
                            var serialized = new SerializedObject(component);
                            foreach (Check check in binding.checks)
                            {
                                SerializedProperty property = serialized.FindProperty(check.path);
                                Require(property != null, "Original serialized field missing: " + binding.kind + "." + check.path);
                                Match(property, check, localReferences);
                                fields++;
                            }
                            components++;
                        }
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        if (asset.name == "FightEndReportSkillTabView")
                        {
                            var header = root.GetComponentsInChildren<ScrollRect>(true).First(value => value.horizontal && !value.vertical);
                            for (Transform parent = header.transform; parent != root.transform.parent; parent = parent.parent) parent.gameObject.SetActive(true);
                            yield return null;
                            Canvas.ForceUpdateCanvases();
                            var grid = header.content.GetComponent<GridLayoutGroupJSZX>();
                            Require(grid != null && grid.cellSize.x == 130 && grid.spacing.x == 30,
                                "Original report column widths/spacing were lost.");
                            var children = header.content.Cast<Transform>().Where(value => value.gameObject.activeInHierarchy).ToArray();
                            Require(children.Length > 3 && children[1].localPosition.x > children[0].localPosition.x
                                && header.content.rect.width > header.viewport.rect.width, "Report headers still overlap or fail to overflow their viewport.");
                            var list = root.GetComponentInChildren<LoopVerticalScrollRect>(true);
                            Require(list != null && list.DefaultPrefabSource.Prefab != null, "Original report row prefab reference is missing.");
                            // Control fixture only: native row pooling/layout, without inventing report data.
                            list.totalCount = 30;
                            list.RefillCells();
                            yield return null;
                            Canvas.ForceUpdateCanvases();
                            Require(list.content.childCount > 0, "Original report list failed to instantiate its native row prefab.");
                            Vector2 before = list.content.anchoredPosition;
                            Drag(list.gameObject, list.viewport, new Vector2(-150f, 80f));
                            Require(Vector2.Distance(list.content.anchoredPosition, before) > 1f, "Simulated drag did not move the original report list.");
                            drags++;
                            string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
                            if (!string.IsNullOrEmpty(output))
                            {
                                yield return new WaitForEndOfFrame();
                                var image = ScreenCapture.CaptureScreenshotAsTexture();
                                try { Directory.CreateDirectory(output); File.WriteAllBytes(System.IO.Path.Combine(output, "report-layout.png"), image.EncodeToPNG()); }
                                finally { UnityEngine.Object.DestroyImmediate(image); }
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Require(components == 117 && drags == 1, "Original report foundation validation scope changed.");
                Debug.Log("Original report foundation passed: " + components + " components, " + fields
                    + " original serialized fields, native column geometry, row pooling and simulated list drag. Dropdown provider, report data and header synchronization remain incomplete.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvasRoot); }
        }

        private static void Match(SerializedProperty property, Check check, Dictionary<long, UnityEngine.Object> localReferences)
        {
            string name = property.propertyPath;
            if (check.kind == "reference")
            {
                UnityEngine.Object value = property.objectReferenceValue;
                if (check.reference.fileID == 0) { Require(value == null, "Unexpected reference: " + name); return; }
                if (string.IsNullOrEmpty(check.reference.guid))
                {
                    Require(localReferences.TryGetValue(check.reference.fileID, out UnityEngine.Object expected) && expected != null && value == expected,
                        "Original local report reference differs: " + name + ", value=" + value + ", fileID=" + check.reference.fileID);
                    return;
                }
                Require(value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string id, out long fileId)
                    && fileId == check.reference.fileID && id == check.reference.guid,
                    "Original report control reference differs: " + name);
            }
            else if (check.kind == "array") Require(property.isArray && property.arraySize == check.count, "Original array differs: " + name);
            else if (check.kind == "string") Require(property.stringValue == check.text, "Original text field differs: " + name);
            else
            {
                double actual = property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1 : 0)
                    : property.propertyType == SerializedPropertyType.Float ? property.doubleValue : property.intValue;
                Require(Math.Abs(actual - check.number) < 0.0001, "Original field differs: " + name + ", actual=" + actual + ", expected=" + check.number);
            }
        }

        private static void MapReferences(Transform source, Transform clone, Dictionary<long, UnityEngine.Object> result)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.gameObject, out _, out long goId)) result[goId] = clone.gameObject;
            Component[] originals = source.GetComponents<Component>(), copies = clone.GetComponents<Component>();
            Require(originals.Length == copies.Length, "Instantiated report component order changed.");
            for (int i = 0; i < originals.Length; i++)
                if (originals[i] != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originals[i], out _, out long id)) result[id] = copies[i];
            Require(source.childCount == clone.childCount, "Instantiated report child order changed.");
            for (int i = 0; i < source.childCount; i++) MapReferences(source.GetChild(i), clone.GetChild(i), result);
        }

        private static void Drag(GameObject target, RectTransform viewport, Vector2 delta)
        {
            var events = EventSystem.current;
            Require(events != null, "Report drag requires the active EventSystem.");
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(viewport.rect.center));
            var pointer = new PointerEventData(events) { position = position, pressPosition = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IBeginDragHandler>(hits[0].gameObject) == target,
                "Another graphic obstructs the report scroll viewport: " + (hits.Count > 0 ? hits[0].gameObject.name : "no hit"));
            pointer.pointerCurrentRaycast = hits[0]; pointer.pointerPressRaycast = hits[0]; pointer.pointerDrag = target;
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
            for (int i = 1; i <= 5; i++)
            {
                pointer.position = position + delta * i / 5f;
                pointer.delta = delta / 5f;
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
        }

        private static void Index(Transform node, string parent, Dictionary<string, Transform> result)
        {
            string name = node.name;
            int repeated = 0;
            if (parent.Length > 0)
                for (int i = 0; i < node.GetSiblingIndex(); i++) if (node.parent.GetChild(i).name == name) repeated++;
            string path = parent + "/" + name + (repeated > 0 ? " (" + repeated + ")" : "");
            result.Add(path, node);
            foreach (Transform child in node) Index(child, path, result);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
