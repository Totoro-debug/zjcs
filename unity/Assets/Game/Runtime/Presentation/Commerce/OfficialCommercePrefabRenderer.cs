using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;

namespace Zjcs.Presentation.Commerce
{
    public sealed class OfficialCommercePrefabRenderer
    {
        private readonly OfficialCommerceAssetCatalog catalog;
        private readonly Func<string, Sprite> spriteResolver;
        private readonly Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>();

        public OfficialCommercePrefabRenderer(OfficialCommerceAssetCatalog catalog,
            Func<string, Sprite> spriteResolver = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.spriteResolver = spriteResolver;
        }

        public GameObject Create(string prefabKey, Transform parent, Action<string, GameObject> bind = null)
        {
            CommercePrefab prefab = new OfficialCommerceCatalogView(catalog.Load()).Prefab(prefabKey);
            if (prefab == null || prefab.root == null)
                throw new KeyNotFoundException("Official commerce prefab is missing: " + prefabKey);
            nodes.Clear();
            GameObject root = CreateSubtree(prefab.root, parent, bind);
            RebuildLayout(root);
            return root;
        }

        public static void RebuildLayout(GameObject root)
        {
            if (root == null || !root.activeInHierarchy) return;
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.GetComponent<LayoutGroup>() != null || rect.GetComponent<ContentSizeFitter>() != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
        }

        public GameObject CreateSubtree(CommerceUiNode source, Transform parent, Action<string, GameObject> bind = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return CreateNode(source, parent, bind);
        }

        public GameObject Find(string path)
        {
            return nodes.TryGetValue(path, out GameObject value) ? value : null;
        }

        public bool SetText(string path, string value)
        {
            GameObject node = Find(path);
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text == null) return false;
            text.text = value ?? string.Empty;
            return true;
        }

        public bool SetSprite(string path, Sprite sprite)
        {
            GameObject node = Find(path);
            Image image = node == null ? null : node.GetComponent<Image>();
            if (image == null) return false;
            image.sprite = sprite;
            return true;
        }

        public bool SetActive(string path, bool active)
        {
            GameObject node = Find(path);
            if (node == null) return false;
            node.SetActive(active);
            return true;
        }

        private GameObject CreateNode(CommerceUiNode source, Transform parent, Action<string, GameObject> bind)
        {
            var gameObject = new GameObject(source.name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector(source.anchor_min, new Vector2(0.5f, 0.5f));
            rect.anchorMax = Vector(source.anchor_max, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = Vector(source.anchored_position, Vector2.zero);
            rect.sizeDelta = Vector(source.size_delta, Vector2.zero);
            rect.pivot = Vector(source.pivot, new Vector2(0.5f, 0.5f));
            rect.localScale = Vector(source.scale, Vector3.one);
            rect.localEulerAngles = new Vector3(0, 0, source.rotation);
            gameObject.SetActive(source.active);
            string path = string.IsNullOrEmpty(source.path) ? source.name : source.path;
            nodes[path] = gameObject;
            if (IsStateOnlyNode(source.name)) gameObject.SetActive(false);
            if (Array.IndexOf(source.scripts ?? Array.Empty<string>(), "SafeArea") >= 0)
                gameObject.AddComponent<CommerceSafeArea>();

            Image image = source.has_image ? CreateImage(source.image, gameObject) : null;
            if (source.has_text && source.text != null) CreateText(source.text, source.localization_key, gameObject);
            if (source.mask && image != null)
            {
                Mask mask = gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
            }
            if (source.rectmask2d) gameObject.AddComponent<RectMask2D>();
            if (source.has_layout && source.layout != null) CreateLayout(source.layout, gameObject);
            if (source.has_grid && source.grid != null) CreateGridLayout(source.grid, gameObject);
            if (source.has_size_fitter && source.size_fitter != null && source.size_fitter.Length >= 2)
            {
                ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(source.size_fitter[0], 0, 2);
                fitter.verticalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(source.size_fitter[1], 0, 2);
            }
            if (source.has_layout_element && source.layout_element != null) CreateLayoutElement(source.layout_element, gameObject);
            if (source.has_aspect_fitter && source.aspect_fitter != null)
            {
                AspectRatioFitter fitter = gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = (AspectRatioFitter.AspectMode)Mathf.Clamp(source.aspect_fitter.mode, 0, 4);
                fitter.aspectRatio = Mathf.Max(0.001f, source.aspect_fitter.ratio);
            }
            if (source.alpha < 0.999f)
            {
                CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
                group.alpha = Mathf.Clamp01(source.alpha);
            }
            bool tabItem = Array.IndexOf(source.scripts ?? Array.Empty<string>(), "TabItem") >= 0;
            if (source.button || tabItem)
            {
                Button button = gameObject.AddComponent<Button>();
                button.targetGraphic = image != null ? image : gameObject.GetComponentInChildren<Graphic>();
                bind?.Invoke(path, gameObject);
            }

            foreach (CommerceUiNode child in source.children ?? Array.Empty<CommerceUiNode>())
                CreateNode(child, gameObject.transform, bind);
            if (source.has_scroll && source.scroll != null) CreateScroll(source.scroll, gameObject);
            if (Array.IndexOf(source.scripts ?? Array.Empty<string>(), "Slider") >= 0)
                CreateSlider(gameObject);
            return gameObject;
        }

        private Image CreateImage(CommerceUiImage source, GameObject gameObject)
        {
            if (source == null) return null;
            Image image = gameObject.AddComponent<Image>();
            image.sprite = source.sprite == null ? null
                : spriteResolver == null ? catalog.FindSprite(source.sprite.file) : spriteResolver(source.sprite.file);
            image.color = ColorValue(source.color, Color.white);
            image.type = (Image.Type)Mathf.Clamp(source.type, 0, (int)Image.Type.Filled);
            image.preserveAspect = source.preserve_aspect;
            if (image.sprite != null && image.type == Image.Type.Sliced && image.sprite.border == Vector4.zero)
                image.type = Image.Type.Simple;
            return image;
        }

        private void CreateText(CommerceUiTextStyle source, string localizationKey, GameObject gameObject)
        {
            // A Unity GameObject can hold only one Graphic. A few official fragments carry a
            // background Image and a label on the same node; keep the image and add the text
            // as a stretched child so the authored hierarchy stays intact.
            GameObject host = gameObject;
            if (gameObject.GetComponent<Graphic>() != null)
            {
                host = new GameObject("Text", typeof(RectTransform));
                RectTransform child = (RectTransform)host.transform;
                child.SetParent(gameObject.transform, false);
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
            }
            TMP_Text text = host.AddComponent<TextMeshProUGUI>();
            text.font = catalog.ChineseTmpFont;
            string value = catalog.LocalizedText(localizationKey, source.value);
            text.text = RestoreMidsize(value, source.font_size);
            text.color = ColorValue(source.color, Color.white);
            text.fontSize = Mathf.Max(1, source.font_size);
            text.alignment = Alignment(source.horizontal, source.vertical);
            text.enableWordWrapping = true;
            text.overflowMode = source.overflow > 0
                ? (TextOverflowModes)Mathf.Clamp(source.overflow, 0, 6) : TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            if (source.margin != null && source.margin.Length >= 4)
                text.margin = new Vector4(source.margin[0], source.margin[1], source.margin[2], source.margin[3]);
            if (source.auto_size != null && source.auto_size.Length >= 2 && source.auto_size[0] > 0)
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = source.auto_size[0];
                text.fontSizeMax = Mathf.Max(source.auto_size[0], source.auto_size[1]);
            }
        }

        private static string RestoreMidsize(string source, float baseSize)
        {
            return Regex.Replace(source, @"<midsize=([0-9]+(?:\.[0-9]+)?)>", match =>
            {
                float size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                float offset = (baseSize - size) * 0.5f * (38.04f / 40f);
                return "<voffset=" + (offset / Mathf.Max(1f, baseSize)).ToString("0.#####", CultureInfo.InvariantCulture)
                    + "em><size=" + size.ToString("0.###", CultureInfo.InvariantCulture) + ">";
            }).Replace("</midsize>", "</size></voffset>");
        }

        private void CreateScroll(CommerceUiScroll source, GameObject gameObject)
        {
            ScrollRect scroll = gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = source.horizontal;
            scroll.vertical = source.vertical;
            scroll.inertia = source.inertia;
            scroll.movementType = (ScrollRect.MovementType)Mathf.Clamp(source.movement, 0, 2);
            scroll.scrollSensitivity = source.sensitivity;
            scroll.elasticity = source.elasticity;
            scroll.decelerationRate = source.deceleration;
            Transform viewport = FindDescendant(gameObject.transform, "Viewport", "Viewprot");
            Transform content = FindDescendant(gameObject.transform, "Content");
            if (viewport != null) scroll.viewport = viewport as RectTransform;
            if (content != null) scroll.content = content as RectTransform;
        }

        private static void CreateSlider(GameObject gameObject)
        {
            Slider slider = gameObject.AddComponent<Slider>();
            Transform fill = FindDescendant(gameObject.transform, "Fill");
            Transform handle = FindDescendant(gameObject.transform, "Handle");
            Transform handleArea = FindDescendant(gameObject.transform, "Handle Slide Area");
            slider.fillRect = fill as RectTransform;
            slider.handleRect = (handle ?? handleArea) as RectTransform;
            slider.targetGraphic = slider.handleRect == null ? null : slider.handleRect.GetComponent<Graphic>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.wholeNumbers = true;
        }

        private static void CreateLayout(CommerceLayoutGroup source, GameObject gameObject)
        {
            if (gameObject.GetComponent<LayoutGroup>() != null) return;
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

        private static void CreateLayoutElement(CommerceLayoutElement source, GameObject gameObject)
        {
            LayoutElement element = gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = source.ignore;
            element.minWidth = Pair(source.min, 0, -1f);
            element.minHeight = Pair(source.min, 1, -1f);
            element.preferredWidth = Pair(source.preferred, 0, -1f);
            element.preferredHeight = Pair(source.preferred, 1, -1f);
            element.flexibleWidth = Pair(source.flexible, 0, -1f);
            element.flexibleHeight = Pair(source.flexible, 1, -1f);
        }

        private static void CreateGridLayout(CommerceGridLayout source, GameObject gameObject)
        {
            if (gameObject.GetComponent<LayoutGroup>() != null) return;
            GridLayoutGroup group = gameObject.AddComponent<GridLayoutGroup>();
            int[] padding = source.padding ?? Array.Empty<int>();
            if (padding.Length >= 4) group.padding = new RectOffset(padding[0], padding[1], padding[2], padding[3]);
            group.childAlignment = (TextAnchor)Mathf.Clamp(source.alignment, 0, 8);
            group.startCorner = (GridLayoutGroup.Corner)Mathf.Clamp(source.corner, 0, 3);
            group.startAxis = (GridLayoutGroup.Axis)Mathf.Clamp(source.axis, 0, 1);
            group.cellSize = Vector(source.cell, Vector2.zero);
            group.spacing = Vector(source.spacing, Vector2.zero);
            group.constraint = (GridLayoutGroup.Constraint)Mathf.Clamp(source.constraint, 0, 2);
            group.constraintCount = Mathf.Max(1, source.count);
        }

        private static readonly HashSet<string> StateOnlyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "RedDot", "RedDot1", "RedDot2", "RedDotGo", "EmptyRedDot", "BigEmptyRedDot",
            "BlueDot2", "BlueDot3", "EmptyBlueDot", "BigEmptyBlueDot", "LevelUpMark", "NewMark",
            "SoldOutMask", "SoldoutGo", "SoldOutGo", "TakenGo", "NoAwardImg", "HasAwardImg",
            "CannotBuyGo", "CanBuyImg", "PaySoldOutGo", "LockedGo", "LockedText",
            "AwardAvailableImg", "TakenAwardMask", "LevelRequireBg", "CannotBuyImg"
        };

        private static bool IsStateOnlyNode(string name)
        {
            return !string.IsNullOrEmpty(name) && StateOnlyNames.Contains(name);
        }

        private static bool Flag(bool[] flags, int index) => flags != null && flags.Length > index && flags[index];        private static float Pair(float[] values, int index, float fallback) => values != null && values.Length > index ? values[index] : fallback;

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
            // The pinned extraction stores the TMP horizontal enum index (0..2) and the
            // native vertical alignment mask (Top=0x100, Middle=0x200, Bottom=0x400).
            int h = horizontal == 0 ? 1 : horizontal == 1 ? 2 : horizontal == 2 ? 4 : horizontal;
            if (h <= 0) h = 2;
            if (vertical >= 256) return (TextAlignmentOptions)(vertical | h);
            int v = vertical == 0 ? 0x100 : vertical == 1 ? 0x200 : 0x400;
            return (TextAlignmentOptions)(v | h);
        }

        private static Vector2 Vector(float[] values, Vector2 fallback) =>
            values != null && values.Length >= 2 ? new Vector2(values[0], values[1]) : fallback;

        private static Vector3 Vector(float[] values, Vector3 fallback) =>
            values != null && values.Length >= 3 ? new Vector3(values[0], values[1], values[2])
                : values != null && values.Length >= 2 ? new Vector3(values[0], values[1], 1) : fallback;

        private static Color ColorValue(float[] values, Color fallback) =>
            values != null && values.Length >= 4 ? new Color(values[0], values[1], values[2], values[3]) : fallback;
    }
}

