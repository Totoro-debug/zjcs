using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Builds the official home UGUI trees from ``Home/ui.json``.  It follows the
    /// same node schema as the commerce renderer: anchors, layout groups, masks,
    /// scroll rects, buttons and TMP text.
    /// </summary>
    public sealed class HomeUiRenderer
    {
        private readonly HomeAssetCatalog catalog;
        private readonly Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>(StringComparer.Ordinal);

        public HomeUiRenderer(HomeAssetCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string RootName { get; private set; }

        public GameObject Create(string panelKey, Transform parent, Action<string, GameObject> bind = null)
        {
            HomeUiPanel panel = catalog.Panel(panelKey);
            if (panel == null || panel.root == null)
                throw new KeyNotFoundException("Official home panel is missing: " + panelKey);
            nodes.Clear();
            GameObject root = CreateNode(panel.root, parent, bind);
            RootName = root.name;
            return root;
        }

        public GameObject CreateNode(HomeUiNode source, Transform parent, Action<string, GameObject> bind = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Build(source, parent, bind);
        }

        public GameObject Find(string path) => nodes.TryGetValue(path, out GameObject value) ? value : null;

        public T Find<T>(string path) where T : Component
        {
            GameObject node = Find(path);
            return node == null ? null : node.GetComponent<T>();
        }

        public bool SetText(string path, string value)
        {
            TMP_Text text = Find<TMP_Text>(path);
            if (text == null) return false;
            text.text = value ?? string.Empty;
            return true;
        }

        public bool SetSprite(string path, Sprite sprite)
        {
            Image image = Find<Image>(path);
            if (image == null) return false;
            image.sprite = sprite;
            image.enabled = sprite != null;
            return true;
        }

        public bool SetSpriteByPrefix(string name, string file)
        {
            Sprite sprite = catalog.FindSprite(file);
            foreach (KeyValuePair<string, GameObject> pair in nodes)
            {
                if (!pair.Key.EndsWith("/" + name, StringComparison.Ordinal)) continue;
                Image image = pair.Value.GetComponent<Image>();
                if (image == null) continue;
                image.sprite = sprite;
                image.enabled = sprite != null;
                return true;
            }
            return false;
        }

        public bool SetSpriteByName(string name, Sprite sprite)
        {
            foreach (KeyValuePair<string, GameObject> pair in nodes)
            {
                if (!pair.Key.EndsWith("/" + name, StringComparison.Ordinal)) continue;
                Image image = pair.Value.GetComponent<Image>();
                if (image == null) continue;
                image.sprite = sprite;
                image.enabled = sprite != null;
                return true;
            }
            return false;
        }

        public bool SetActive(string path, bool active)
        {
            GameObject node = Find(path);
            if (node == null) return false;
            node.SetActive(active);
            return true;
        }

        /// <summary>Sets the first text node whose name matches, ignoring the full path.</summary>
        public bool SetTextByName(string name, string value)
        {
            foreach (KeyValuePair<string, GameObject> pair in nodes)
            {
                if (!pair.Key.EndsWith("/" + name, StringComparison.Ordinal)) continue;
                TMP_Text text = pair.Value.GetComponent<TMP_Text>();
                if (text == null || text.GetType() != typeof(TextMeshProUGUI)) continue;
                text.text = value ?? string.Empty;
                LayoutElement element = pair.Value.GetComponent<LayoutElement>();
                if (element == null) element = pair.Value.AddComponent<LayoutElement>();
                Vector2 preferred = text.GetPreferredValues(value ?? string.Empty);
                element.preferredWidth = Mathf.Max(24f, preferred.x + 8f);
                element.preferredHeight = Mathf.Max(20f, text.fontSize + 4f);
                element.flexibleWidth = 0f;
                element.minWidth = element.preferredWidth;
                text.ForceMeshUpdate();
                RectTransform parentRect = pair.Value.transform.parent as RectTransform;
                if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                HorizontalOrVerticalLayoutGroup group = pair.Value.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (group != null && group is HorizontalLayoutGroup)
                {
                    group.childControlWidth = false;
                    group.childForceExpandWidth = false;
                }
                RectTransform textRect = (RectTransform)pair.Value.transform;
                if (textRect.rect.width < 1f)
                {
                    textRect.sizeDelta = new Vector2(Mathf.Max(24f, preferred.x + 8f), textRect.sizeDelta.y);
                    text.ForceMeshUpdate();
                }
                return true;
            }
            return false;
        }

        /// <summary>Sets the text of a descendant of the named node.</summary>
        public void ReplaceExactText(string from, string to)
        {
            foreach (GameObject node in nodes.Values)
            {
                TMP_Text text = node.GetComponent<TMP_Text>();
                if (text != null && text.text == from) text.text = to;
            }
        }

        private readonly Dictionary<int, GameObject> valueLabels = new Dictionary<int, GameObject>();

        public bool SetTextUnderPath(string parentName, string childPath, string value)
        {
            string marker = "/" + parentName + "/";
            string suffix = "/" + childPath;
            foreach (KeyValuePair<string, GameObject> pair in nodes)
            {
                if (pair.Key.IndexOf(marker, StringComparison.Ordinal) < 0) continue;
                if (!pair.Key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                Transform valueParent = pair.Value.transform.parent;
                if (valueParent == null) continue;
                // The official Value node controls child width in a layout group, which
                // collapses the cloned TMP text to zero width.  A dedicated label keeps a
                // stretched rect and the official font/colour.
                GameObject label = EnsureValueLabel(valueParent);
                TMP_Text text = label.GetComponent<TMP_Text>();
                text.text = value ?? string.Empty;
                pair.Value.SetActive(false);
                return true;
            }
            return false;
        }

        private GameObject EnsureValueLabel(Transform parent)
        {
            int key = parent.GetInstanceID();
            if (valueLabels.TryGetValue(key, out GameObject cached) && cached != null) return cached;
            var go = new GameObject("ValueLabel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            rect.localScale = Vector3.one;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = catalog.Font;
            label.fontSize = 30f;
            label.color = new Color(0.13f, 0.13f, 0.13f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            valueLabels[key] = go;
            return go;
        }
        public bool SetTextUnder(string parentName, string childName, string value)
        {
            string marker = "/" + parentName + "/";
            foreach (KeyValuePair<string, GameObject> pair in nodes)
            {
                if (pair.Key.IndexOf(marker, StringComparison.Ordinal) < 0) continue;
                if (!pair.Key.EndsWith("/" + childName, StringComparison.Ordinal)) continue;
                TMP_Text text = pair.Value.GetComponent<TMP_Text>();
                if (text == null) continue;
                text.text = value ?? string.Empty;
                LayoutElement element = pair.Value.GetComponent<LayoutElement>();
                if (element == null) element = pair.Value.AddComponent<LayoutElement>();
                Vector2 preferred = text.GetPreferredValues(value ?? string.Empty);
                element.preferredWidth = Mathf.Max(24f, preferred.x + 8f);
                element.preferredHeight = Mathf.Max(20f, text.fontSize + 4f);
                element.flexibleWidth = 0f;
                element.minWidth = element.preferredWidth;
                text.ForceMeshUpdate();
                RectTransform parentRect = pair.Value.transform.parent as RectTransform;
                if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                HorizontalOrVerticalLayoutGroup group = pair.Value.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
                if (group != null && group is HorizontalLayoutGroup)
                {
                    group.childControlWidth = false;
                    group.childForceExpandWidth = false;
                }
                RectTransform textRect = (RectTransform)pair.Value.transform;
                if (textRect.rect.width < 1f)
                {
                    textRect.sizeDelta = new Vector2(Mathf.Max(24f, preferred.x + 8f), textRect.sizeDelta.y);
                    text.ForceMeshUpdate();
                }
                return true;
            }
            return false;
        }
        public bool SetActiveByName(string name, bool active)
        {
            bool changed = false;
            foreach (KeyValuePair<string, GameObject> pair in nodes)
            {
                if (!pair.Key.EndsWith("/" + name, StringComparison.Ordinal)) continue;
                pair.Value.SetActive(active);
                changed = true;
            }
            return changed;
        }

        public IEnumerable<GameObject> AllNodes => nodes.Values;

        private GameObject Build(HomeUiNode source, Transform parent, Action<string, GameObject> bind)
        {
            var gameObject = new GameObject(source.name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2Value(source.anchor_min, new Vector2(0.5f, 0.5f));
            rect.anchorMax = Vector2Value(source.anchor_max, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = Vector2Value(source.anchored_position, Vector2.zero);
            rect.sizeDelta = Vector2Value(source.size_delta, Vector2.zero);
            rect.pivot = Vector2Value(source.pivot, new Vector2(0.5f, 0.5f));
            float[] scale = source.scale;
            rect.localScale = scale != null && scale.Length >= 3
                ? new Vector3(scale[0], scale[1], scale[2])
                : scale != null && scale.Length >= 2 ? new Vector3(scale[0], scale[1], 1) : Vector3.one;
            rect.localEulerAngles = new Vector3(0, 0, source.rotation);
            gameObject.SetActive(source.active);
            string path = string.IsNullOrEmpty(source.path) ? source.name : source.path;
            nodes[path] = gameObject;

            Image image = null;
            if (HasImage(source.image))
            {
                image = gameObject.AddComponent<Image>();
                Sprite sprite = source.image.sprite == null ? null : catalog.FindSprite(source.image.sprite.file);
                image.sprite = sprite;
                image.type = (Image.Type)Mathf.Clamp(source.image.type, 0, (int)Image.Type.Filled);
                image.preserveAspect = source.image.preserve_aspect;
                Color tint = ColorValue(source.image.color, Color.white);
                // A missing official sprite must not render as an opaque white block.
                if (sprite == null) tint.a = 0f;
                image.color = tint;
                image.enabled = true;
                image.raycastTarget = false;
            }
            if (HasText(source)) CreateText(source, gameObject);
            if (source.mask && image != null)
            {
                Mask mask = gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
            }
            if (source.rectmask2d) gameObject.AddComponent<RectMask2D>();
            if (HasLayout(source.layout)) CreateLayout(source.layout, gameObject);
            if (HasGrid(source.grid)) CreateGrid(source.grid, gameObject);
            if (source.size_fitter != null && source.size_fitter.Length >= 2)
            {
                ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(source.size_fitter[0], 0, 2);
                fitter.verticalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(source.size_fitter[1], 0, 2);
            }
            if (HasLayoutElement(source.layout_element))
            {
                LayoutElement element = gameObject.AddComponent<LayoutElement>();
                HomeUiLayoutElement model = source.layout_element;
                element.ignoreLayout = model.ignore;
                element.minWidth = Pair(model.min, 0, -1f);
                element.minHeight = Pair(model.min, 1, -1f);
                element.preferredWidth = Pair(model.preferred, 0, -1f);
                element.preferredHeight = Pair(model.preferred, 1, -1f);
                element.flexibleWidth = Pair(model.flexible, 0, -1f);
                element.flexibleHeight = Pair(model.flexible, 1, -1f);
            }
            if (HasAspect(source.aspect_fitter))
            {
                AspectRatioFitter fitter = gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = (AspectRatioFitter.AspectMode)Mathf.Clamp(source.aspect_fitter.mode, 0, 4);
                fitter.aspectRatio = Mathf.Max(0.001f, source.aspect_fitter.ratio);
            }
            if (source.alpha > 0f && source.alpha < 0.999f)
            {
                CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
                group.alpha = Mathf.Clamp01(source.alpha);
            }
            bool toggle = Array.IndexOf(source.scripts ?? Array.Empty<string>(), "Toggle") >= 0;
            bool tabItem = Array.IndexOf(source.scripts ?? Array.Empty<string>(), "TabItem") >= 0;
            if (source.button || toggle || tabItem)
            {
                Graphic target = image != null ? (Graphic)image : gameObject.GetComponent<Graphic>();
                if (target != null) target.raycastTarget = true;
                if (toggle)
                {
                    Toggle control = gameObject.AddComponent<Toggle>();
                    control.targetGraphic = target;
                    bind?.Invoke(path, gameObject);
                }
                else
                {
                    Button button = gameObject.AddComponent<Button>();
                    button.targetGraphic = target;
                    bind?.Invoke(path, gameObject);
                }
            }

            foreach (HomeUiNode child in source.children ?? Array.Empty<HomeUiNode>())
                Build(child, gameObject.transform, bind);
            if (HasScroll(source.scroll)) CreateScroll(source.scroll, gameObject);
            if (Array.IndexOf(source.scripts ?? Array.Empty<string>(), "Slider") >= 0) CreateSlider(gameObject);
            return gameObject;
        }

        private static bool HasImage(HomeUiImage image)
            => image != null && (image.sprite != null || image.color != null);

        private static bool HasLayout(HomeUiLayout layout)
            => layout != null && (layout.padding != null || layout.expand != null
                || layout.control != null || layout.child_scale != null || layout.spacing > 0f);

        private static bool HasGrid(HomeUiGrid grid)
            => grid != null && (grid.cell != null || grid.spacing != null || grid.padding != null
                || grid.count > 0 || grid.constraint > 0);

        private static bool HasLayoutElement(HomeUiLayoutElement element)
            => element != null && (element.min != null || element.preferred != null
                || element.flexible != null || element.ignore);

        private static bool HasAspect(HomeUiAspect aspect) => aspect != null && aspect.ratio > 0f;

        private static bool HasScroll(HomeUiScroll scroll)
            => scroll != null && (scroll.horizontal || scroll.vertical || scroll.sensitivity > 0f
                || scroll.elasticity > 0f || scroll.deceleration > 0f);
        private static bool HasText(HomeUiNode source)
        {
            if (source.text == null) return false;
            return source.text.font_size > 0f
                || !string.IsNullOrEmpty(source.text.value)
                || !string.IsNullOrEmpty(source.localization_key);
        }
        private void CreateText(HomeUiNode source, GameObject gameObject)
        {
            try
            {
                CreateTextCore(source, gameObject);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Home UI text failed at " + (source.path ?? source.name)
                    + " catalog=" + (catalog != null) + " text=" + (source.text != null)
                    + " font=" + (catalog != null && catalog.Font != null), error);
            }
        }

        private void CreateTextCore(HomeUiNode source, GameObject gameObject)
        {
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = catalog.Font;
            string value = catalog.LocalizedText(source.localization_key, source.text.value);
            text.text = RestoreMidsize(value, source.text.font_size);
            text.fontSize = Mathf.Max(1, source.text.font_size);
            text.color = ColorValue(source.text.color, Color.white);
            text.alignment = Alignment(source.text.horizontal, source.text.vertical);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.richText = true;
            text.raycastTarget = false;
        }

        private static string RestoreMidsize(string source, float baseSize)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf("<midsize=", StringComparison.Ordinal) < 0) return source;
            return Regex.Replace(source, @"<midsize=([0-9]+(?:\.[0-9]+)?)>", match =>
            {
                float size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                float offset = (baseSize - size) * 0.5f * (38.04f / 40f);
                return "<voffset=" + (offset / Mathf.Max(1f, baseSize)).ToString("0.#####", CultureInfo.InvariantCulture)
                    + "em><size=" + size.ToString("0.###", CultureInfo.InvariantCulture) + ">";
            }).Replace("</midsize>", "</size></voffset>");
        }

        private void CreateScroll(HomeUiScroll source, GameObject gameObject)
        {
            ScrollRect scroll = gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = source.horizontal;
            scroll.vertical = source.vertical;
            scroll.inertia = source.inertia;
            scroll.movementType = (ScrollRect.MovementType)Mathf.Clamp(source.movement, 0, 2);
            scroll.scrollSensitivity = source.sensitivity <= 0 ? 20 : source.sensitivity;
            scroll.elasticity = source.elasticity;
            scroll.decelerationRate = source.deceleration <= 0 ? 0.135f : source.deceleration;
            Transform viewport = FindDescendant(gameObject.transform, "Viewport", "ViewPort", "Viewprot");
            Transform content = FindDescendant(gameObject.transform, "Content");
            if (viewport != null) scroll.viewport = viewport as RectTransform;
            if (content != null) scroll.content = content as RectTransform;
        }

        private static void CreateSlider(GameObject gameObject)
        {
            Slider slider = gameObject.AddComponent<Slider>();
            Transform fill = FindDescendant(gameObject.transform, "Fill");
            Transform handle = FindDescendant(gameObject.transform, "Handle");
            Transform area = FindDescendant(gameObject.transform, "Handle Slide Area");
            slider.fillRect = fill as RectTransform;
            slider.handleRect = (handle ?? area) as RectTransform;
            slider.targetGraphic = slider.handleRect == null ? null : slider.handleRect.GetComponent<Graphic>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
        }

        private static void CreateLayout(HomeUiLayout source, GameObject gameObject)
        {
            HorizontalOrVerticalLayoutGroup group = source.axis == 0
                ? (HorizontalOrVerticalLayoutGroup)gameObject.AddComponent<HorizontalLayoutGroup>()
                : gameObject.AddComponent<VerticalLayoutGroup>();
            int[] padding = source.padding ?? Array.Empty<int>();
            if (padding.Length >= 4) group.padding = new RectOffset(padding[0], padding[1], padding[2], padding[3]);
            group.childAlignment = (TextAnchor)Mathf.Clamp(source.alignment, 0, 8);
            group.spacing = source.spacing;
            group.childForceExpandWidth = Flag(source.expand, 0);
            group.childForceExpandHeight = Flag(source.expand, 1);
            group.childControlWidth = Flag(source.control, 0);
            group.childControlHeight = Flag(source.control, 1);
            group.childScaleWidth = Flag(source.child_scale, 0);
            group.childScaleHeight = Flag(source.child_scale, 1);
            group.reverseArrangement = source.reverse;
        }

        private static void CreateGrid(HomeUiGrid source, GameObject gameObject)
        {
            GridLayoutGroup group = gameObject.AddComponent<GridLayoutGroup>();
            int[] padding = source.padding ?? Array.Empty<int>();
            if (padding.Length >= 4) group.padding = new RectOffset(padding[0], padding[1], padding[2], padding[3]);
            group.childAlignment = (TextAnchor)Mathf.Clamp(source.alignment, 0, 8);
            group.startCorner = (GridLayoutGroup.Corner)Mathf.Clamp(source.corner, 0, 3);
            group.startAxis = (GridLayoutGroup.Axis)Mathf.Clamp(source.axis, 0, 1);
            group.cellSize = Vector2Value(source.cell, Vector2.zero);
            group.spacing = Vector2Value(source.spacing, Vector2.zero);
            group.constraint = (GridLayoutGroup.Constraint)Mathf.Clamp(source.constraint, 0, 2);
            group.constraintCount = Mathf.Max(1, source.count);
        }

        private static bool Flag(bool[] flags, int index) => flags != null && flags.Length > index && flags[index];

        private static float Pair(float[] values, int index, float fallback) =>
            values != null && values.Length > index ? values[index] : fallback;

        private static Transform FindDescendant(Transform parent, params string[] names)
        {
            foreach (Transform child in parent)
            {
                foreach (string name in names)
                    if (child.name == name) return child;
                Transform nested = FindDescendant(child, names);
                if (nested != null) return nested;
            }
            return null;
        }

        private static TextAlignmentOptions Alignment(int horizontal, int vertical)
        {
            int h = Mathf.Clamp(horizontal, 0, 2);
            int v = Mathf.Clamp(vertical, 0, 2);
            if (v == 0) return h == 0 ? TextAlignmentOptions.TopLeft : h == 1 ? TextAlignmentOptions.Top : TextAlignmentOptions.TopRight;
            if (v == 1) return h == 0 ? TextAlignmentOptions.Left : h == 1 ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
            return h == 0 ? TextAlignmentOptions.BottomLeft : h == 1 ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomRight;
        }

        private static Vector2 Vector2Value(float[] values, Vector2 fallback) =>
            values != null && values.Length >= 2 ? new Vector2(values[0], values[1]) : fallback;

        private static Color ColorValue(float[] values, Color fallback) =>
            values != null && values.Length >= 4 ? new Color(values[0], values[1], values[2], values[3]) : fallback;
    }
}
