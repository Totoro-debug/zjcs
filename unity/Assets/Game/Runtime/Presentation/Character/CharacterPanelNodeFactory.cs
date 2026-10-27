using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Builds original UI prefab node trees (character panels, property rows and property groups)
    /// into Unity objects. Mirrors the conversion used by the wardrobe panel so the same original
    /// layout, layout groups, size fitters, masks and scroll rects are restored.
    /// </summary>
    internal sealed class CharacterPanelNodeFactory
    {
        private readonly OfficialCharacterAssetCatalog assets;
        private readonly Dictionary<string, Sprite> slicedSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private Zjcs.Presentation.Skills.OfficialSkillAssetCatalog skillAssets;

        private static Font uiFont;
        private static bool fontLogged;

        public static void SetUiFont(Font font)
        {
            if (font != null) uiFont = font;
        }

        /// <summary>
        /// UGUI fallback font: the built-in LegacyRuntime font has no CJK glyphs, so prefer an OS CJK
        /// family and fall back to the built-in resource when no system font can be created.
        /// </summary>
        public static Font UiFont
        {
            get
            {
                if (Application.isBatchMode) return null;
                if (uiFont != null) return uiFont;
                try
                {
                    uiFont = Resources.Load<Font>("Character/UiFont");
                }
                catch (Exception) { uiFont = null; }
                if (uiFont == null)
                {
                    try
                    {
                        uiFont = Font.CreateDynamicFontFromOSFont(
                            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 20);
                    }
                    catch (Exception) { uiFont = null; }
                }
                if (uiFont == null)
                {
                    try { uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (Exception) { uiFont = null; }
                }
                if (!fontLogged)
                {
                    fontLogged = true;
                    UnityEngine.Debug.Log("[Character] UI fallback font: "
                        + (uiFont == null ? "<none>" : uiFont.name));
                }
                return uiFont;
            }
        }

        public CharacterPanelNodeFactory(OfficialCharacterAssetCatalog assets)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public RectTransform Build(OfficialCharacterPanelNode source, Transform parent,
            Dictionary<string, RectTransform> nodes, Dictionary<string, Button> buttons)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var instance = new GameObject(source.name, typeof(RectTransform));
            instance.layer = parent.gameObject.layer;
            instance.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector(source.anchor_min, Vector2.zero);
            rect.anchorMax = Vector(source.anchor_max, Vector2.zero);
            rect.anchoredPosition = Vector(source.anchored_position, Vector2.zero);
            rect.sizeDelta = Vector(source.size_delta, Vector2.zero);
            rect.pivot = Vector(source.pivot, new Vector2(0.5f, 0.5f));
            rect.localScale = new Vector3(Value(source.scale, 0, 1f), Value(source.scale, 1, 1f),
                Value(source.scale, 2, 1f));
            rect.localRotation = Quaternion.Euler(0f, 0f, source.rotation * Mathf.Rad2Deg);
            ApplySafeAreaLayout(source, rect);
            instance.SetActive(source.active);
            nodes[source.path] = rect;

            if (source.image != null)
            {
                Image image = instance.AddComponent<Image>();
                image.sprite = source.image.sprite == null ? null : assets.FindSprite(source.image.sprite.file);
                if (image.sprite == null && source.image.sprite != null)
                    image.sprite = assets.FindSkillAppearanceSprite(source.image.sprite.file);
                if (image.sprite == null && source.image.sprite != null)
                {
                    if (skillAssets == null) skillAssets = Zjcs.Presentation.Skills.OfficialSkillAssetCatalog.LoadDefault();
                    image.sprite = skillAssets.FindSprite(source.image.sprite.file);
                }
                if (image.sprite == null && source.image.sprite != null)
                    image.sprite = Resources.Load<Sprite>(source.image.sprite.file);
                image.sprite = RestoreSpriteBorder(image.sprite, source.image.sprite);
                image.color = ImageColor(source.image, ColorValue(source.image.color, Color.white));
                image.type = (Image.Type)Mathf.Clamp(source.image.type, 0, 3);
                image.preserveAspect = source.image.preserve_aspect;
                image.raycastTarget = HasScript(source, "Button") || source.button || HasScript(source, "TabItem");
            }
            if (source.text != null && CharacterPanelNodeFactory.UiFont != null)
            {
                Text text = null;
                try
                {
                    text = instance.AddComponent<Text>();
                    text.font = CharacterPanelNodeFactory.UiFont;
                    text.text = source.text.value ?? string.Empty;
                    text.color = ColorValue(source.text.color, Color.black);
                    text.fontSize = Mathf.Max(1, Mathf.RoundToInt(source.text.font_size));
                    text.alignment = TextAlignment(source.text.horizontal, source.text.vertical);
                    text.horizontalOverflow = source.text.word_wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                    text.verticalOverflow = source.text.overflow == 3
                        ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
                    text.supportRichText = true;
                    text.raycastTarget = false;
                }
                catch (Exception error)
                {
                    if (!fontLogged)
                    {
                        fontLogged = true;
                        UnityEngine.Debug.LogWarning("[Character] UI font could not be applied: " + error.Message);
                    }
                    if (text != null) UnityEngine.Object.Destroy(text);
                }
            }
            if (HasScript(source, "Button") || source.button || HasScript(source, "TabItem"))
            {
                Graphic graphic = EnsureRaycastGraphic(source, instance);
                graphic.raycastTarget = true;
                Button button = instance.AddComponent<Button>();
                button.targetGraphic = graphic;
                buttons[source.path] = button;
            }
            else if (HasScript(source, "NoDrawingRayCast") && instance.GetComponent<Graphic>() == null)
            {
                instance.AddComponent<Zjcs.Official.UI.NoDrawingRayCast>();
            }
            if (HasScript(source, "RectMask2D")) instance.AddComponent<RectMask2D>();
            if (source.mask)
            {
                Mask mask = instance.AddComponent<Mask>();
                mask.showMaskGraphic = instance.GetComponent<Graphic>() != null;
            }
            if (source.layout_element != null)
            {
                LayoutElement element = instance.AddComponent<LayoutElement>();
                element.ignoreLayout = source.layout_element.ignore;
                element.minWidth = Value(source.layout_element.min, 0, -1f);
                element.minHeight = Value(source.layout_element.min, 1, -1f);
                element.preferredWidth = Value(source.layout_element.preferred, 0, -1f);
                element.preferredHeight = Value(source.layout_element.preferred, 1, -1f);
                element.flexibleWidth = Value(source.layout_element.flexible, 0, -1f);
                element.flexibleHeight = Value(source.layout_element.flexible, 1, -1f);
            }
            if (source.layout != null) AddLayout(source, instance);
            if (HasScript(source, "ContentSizeFitter"))
            {
                var fitter = instance.AddComponent<ContentSizeFitter>();
                bool vertical = source.layout == null || source.layout.axis == 1;
                fitter.horizontalFit = (ContentSizeFitter.FitMode)IntValue(source.size_fitter, 0, vertical ? 0 : 2);
                fitter.verticalFit = (ContentSizeFitter.FitMode)IntValue(source.size_fitter, 1, vertical ? 2 : 0);
            }
            if (HasScript(source, "ScrollRect")) AddScrollRect(instance);
            foreach (OfficialCharacterPanelNode child in source.children ?? Array.Empty<OfficialCharacterPanelNode>())
                Build(child, rect, nodes, buttons);
            return rect;
        }

        public static OfficialCharacterPanelNode Read(Dictionary<string, object> data)
        {
            var node = new OfficialCharacterPanelNode
            {
                name = String(data, "name"),
                path = String(data, "path"),
                active = Boolean(data, "active", true),
                anchor_min = Floats(data, "anchor_min"),
                anchor_max = Floats(data, "anchor_max"),
                anchored_position = Floats(data, "anchored_position"),
                size_delta = Floats(data, "size_delta"),
                size_fitter = Ints(data, "size_fitter"),
                pivot = Floats(data, "pivot"),
                scale = Floats(data, "scale"),
                rotation = Number(data, "rotation", 0f),
                scripts = data.TryGetValue("scripts", out object scripts) && scripts is List<object> scriptList
                    ? scriptList.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)).ToArray()
                    : Array.Empty<string>(),
                button = Boolean(data, "button", false),
                mask = Boolean(data, "mask", false),
                localization_key = String(data, "localization_key")
            };
            if (data.TryGetValue("image", out object imageRaw) && imageRaw is Dictionary<string, object> image)
            {
                node.image = new OfficialCharacterPanelImage
                {
                    color = Floats(image, "color"),
                    type = (int)Number(image, "type", 0),
                    preserve_aspect = Boolean(image, "preserve_aspect", false),
                    sprite = image.TryGetValue("sprite", out object spriteRaw)
                        && spriteRaw is Dictionary<string, object> sprite
                        ? new OfficialCharacterPanelSprite { file = String(sprite, "file"), border = Floats(sprite, "border") }
                        : null
                };
            }
            if (data.TryGetValue("text", out object textRaw) && textRaw is Dictionary<string, object> text)
            {
                node.text = new OfficialCharacterPanelText
                {
                    value = String(text, "value"),
                    color = Floats(text, "color"),
                    font_size = Number(text, "font_size", 16f),
                    horizontal = (int)Number(text, "horizontal", 2),
                    vertical = (int)Number(text, "vertical", 512),
                    word_wrap = Boolean(text, "word_wrap", true),
                    overflow = (int)Number(text, "overflow", 0),
                    margin = Floats(text, "margin")
                };
            }
            if (data.TryGetValue("layout_element", out object layoutElementRaw)
                && layoutElementRaw is Dictionary<string, object> layoutElement)
                node.layout_element = new OfficialCharacterPanelLayoutElement
                {
                    ignore = Boolean(layoutElement, "ignore", false),
                    min = Floats(layoutElement, "min"),
                    preferred = Floats(layoutElement, "preferred"),
                    flexible = Floats(layoutElement, "flexible")
                };
            if (data.TryGetValue("layout", out object groupRaw) && groupRaw is Dictionary<string, object> group)
                node.layout = new OfficialCharacterPanelGroupLayout
                {
                    axis = (int)Number(group, "axis", 0),
                    padding = Ints(group, "padding"),
                    alignment = (int)Number(group, "alignment", 4),
                    spacing = Number(group, "spacing", 0f),
                    expand = Booleans(group, "expand"),
                    control = Booleans(group, "control"),
                    child_scale = Booleans(group, "child_scale"),
                    reverse = Boolean(group, "reverse", false)
                };
            node.children = data.TryGetValue("children", out object childrenRaw) && childrenRaw is List<object> children
                ? children.Select(child => Read(CharacterModelPackage.AsObject(child, "panel child"))).ToArray()
                : Array.Empty<OfficialCharacterPanelNode>();
            return node;
        }

        public static OfficialCharacterPanelNode ReadRoot(Dictionary<string, object> template)
        {
            if (template == null || !template.TryGetValue("root", out object root)
                || !(root is Dictionary<string, object> data))
                throw new FormatException("An original character UI template has no root.");
            return Read(data);
        }

        private static void AddLayout(OfficialCharacterPanelNode source, GameObject instance)
        {
            OfficialCharacterPanelGroupLayout data = source.layout;
            HorizontalOrVerticalLayoutGroup group;
            if (HasScript(source, "HorizontalLayoutGroup") || data.axis == 0)
                group = instance.AddComponent<HorizontalLayoutGroup>();
            else if (HasScript(source, "VerticalLayoutGroup") || data.axis == 1)
                group = instance.AddComponent<VerticalLayoutGroup>();
            else
                return;
            int[] padding = data.padding ?? Array.Empty<int>();
            group.padding = new RectOffset(IntValue(padding, 0, 0), IntValue(padding, 1, 0),
                IntValue(padding, 2, 0), IntValue(padding, 3, 0));
            group.childAlignment = (TextAnchor)Mathf.Clamp(data.alignment, 0, 8);
            group.spacing = data.spacing;
            group.childControlWidth = Flag(data.control, 0, false);
            group.childControlHeight = Flag(data.control, 1, false);
            group.childForceExpandWidth = Flag(data.expand, 0, false);
            group.childForceExpandHeight = Flag(data.expand, 1, false);
            group.childScaleWidth = Flag(data.child_scale, 0, false);
            group.childScaleHeight = Flag(data.child_scale, 1, false);
            group.reverseArrangement = data.reverse;
        }

        private void AddScrollRect(GameObject instance)
        {
            ScrollRect scroll = instance.GetComponent<ScrollRect>();
            if (scroll == null) scroll = instance.AddComponent<ScrollRect>();
            Transform viewport = FindDeep(instance.transform, "Viewport") ?? FindDeep(instance.transform, "Viewprot");
            Transform content = viewport == null ? null : FindDeep(viewport, "Content");
            if (content == null && viewport != null && viewport.childCount > 0)
                content = viewport.GetChild(0);
            if (viewport == null || content == null)
            {
                Debug.LogWarning("Original scroll rect has no viewport or content: " + instance.name);
                return;
            }
            scroll.viewport = (RectTransform)viewport;
            scroll.content = (RectTransform)content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool HasScript(OfficialCharacterPanelNode node, string script)
        {
            return Array.IndexOf(node.scripts ?? Array.Empty<string>(), script) >= 0;
        }

        internal static Graphic EnsureRaycastGraphic(OfficialCharacterPanelNode source, GameObject instance)
        {
            Graphic graphic = instance.GetComponent<Graphic>();
            if (graphic != null) return graphic;
            if (HasScript(source, "NoDrawingRayCast"))
                return instance.AddComponent<Zjcs.Official.UI.NoDrawingRayCast>();
            Image hitTarget = instance.AddComponent<Image>();
            hitTarget.color = Color.clear;
            hitTarget.canvasRenderer.cullTransparentMesh = false;
            return hitTarget;
        }

        internal static void ApplySafeAreaLayout(OfficialCharacterPanelNode node, RectTransform rect)
        {
            if (!HasScript(node, "SafeArea")) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private Sprite RestoreSpriteBorder(Sprite sprite, OfficialCharacterPanelSprite source)
        {
            if (sprite == null || source?.border == null || source.border.Length != 4) return sprite;
            var border = new Vector4(source.border[0], source.border[1], source.border[2], source.border[3]);
            if (sprite.border == border) return sprite;
            string key = source.file + ":" + border;
            if (slicedSprites.TryGetValue(key, out Sprite restored)) return restored;
            Rect textureRect = sprite.rect;
            Vector2 pivot = new Vector2(sprite.pivot.x / textureRect.width, sprite.pivot.y / textureRect.height);
            restored = Sprite.Create(sprite.texture, textureRect, pivot, sprite.pixelsPerUnit,
                0, SpriteMeshType.FullRect, border);
            restored.name = sprite.name;
            slicedSprites[key] = restored;
            return restored;
        }

        internal static Color ImageColor(OfficialCharacterPanelImage image, Color color)
        {
            // C_none is the official opaque white tint sprite, including the page background.
            return color;
        }

        private static Vector2 Vector(float[] values, Vector2 fallback)
        {
            return values == null || values.Length < 2 ? fallback : new Vector2(values[0], values[1]);
        }

        private static float Value(float[] values, int index, float fallback)
        {
            return values != null && index < values.Length ? values[index] : fallback;
        }

        private static int IntValue(int[] values, int index, int fallback)
        {
            return values != null && index < values.Length ? values[index] : fallback;
        }

        private static bool Flag(bool[] values, int index, bool fallback)
        {
            return values != null && index < values.Length ? values[index] : fallback;
        }

        internal static Color ColorValue(float[] values, Color fallback)
        {
            return values == null || values.Length < 4
                ? fallback : new Color(values[0], values[1], values[2], values[3]);
        }

        private static TextAnchor TextAlignment(int horizontal, int vertical)
        {
            bool left = (horizontal & 1) != 0;
            bool right = (horizontal & 4) != 0;
            if (left && right) left = right = false;
            bool top = (vertical & 256) != 0;
            bool bottom = (vertical & 1024) != 0;
            if (top && bottom) top = bottom = false;
            if (top) return left ? TextAnchor.UpperLeft : right ? TextAnchor.UpperRight : TextAnchor.UpperCenter;
            if (bottom) return left ? TextAnchor.LowerLeft : right ? TextAnchor.LowerRight : TextAnchor.LowerCenter;
            return left ? TextAnchor.MiddleLeft : right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
        }

        internal static string String(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        internal static float Number(Dictionary<string, object> data, string key, float fallback)
        {
            return data.TryGetValue(key, out object value) && value != null
                ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
        }

        internal static bool Boolean(Dictionary<string, object> data, string key, bool fallback)
        {
            return data.TryGetValue(key, out object value) && value is bool result ? result : fallback;
        }

        internal static float[] Floats(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value is List<object> values
                ? values.Select(entry => Convert.ToSingle(entry, CultureInfo.InvariantCulture)).ToArray()
                : null;
        }

        internal static int[] Ints(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value is List<object> values
                ? values.Select(entry => Convert.ToInt32(entry, CultureInfo.InvariantCulture)).ToArray()
                : null;
        }

        internal static bool[] Booleans(Dictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out object value) && value is List<object> values
                ? values.Select(entry => entry is bool flag && flag).ToArray()
                : null;
        }
    }
}
