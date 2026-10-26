using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleHudRenderer
    {
        private readonly OfficialBattleHudCatalog catalog;
        private readonly Func<string, Sprite> spriteFallback;
        private readonly Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>();

        public OfficialBattleHudRenderer(OfficialBattleHudCatalog catalog,
            Func<string, Sprite> spriteFallback = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.spriteFallback = spriteFallback;
        }

        public GameObject Create(string prefabKey, Transform parent, Action<string, GameObject> bind = null)
        {
            BattleHudPrefab prefab = catalog.FindPrefab(prefabKey);
            if (prefab == null || prefab.root == null)
                throw new InvalidOperationException("Official battle HUD prefab is missing: " + prefabKey);
            nodes.Clear();
            return CreateNode(prefab.root, parent, bind);
        }

        public GameObject CreateSubtree(BattleHudNode source, Transform parent,
            Action<string, GameObject> bind = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            nodes.Clear();
            return CreateNode(source, parent, bind);
        }

        public GameObject Find(string path) => nodes.TryGetValue(path, out GameObject value) ? value : null;

        public OfficialBattleHudAnimationPlayer InitializeAnimations(string prefabKey, GameObject instance,
            bool useManualTick = false)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            BattleHudPrefab prefab = catalog.FindPrefab(prefabKey);
            if (prefab == null || prefab.nodes == null || prefab.nodes.Length == 0)
                throw new InvalidOperationException("Official battle HUD animation data is missing: " + prefabKey);

            var player = instance.AddComponent<OfficialBattleHudAnimationPlayer>();
            player.Initialize(prefab.nodes, new Dictionary<string, GameObject>(nodes), useManualTick);
            player.PlayDefaultAnimations();
            return player;
        }

        public GameObject CreateWorldFloat(BattleCustomActionWorldRuntimeData prefab, Transform parent,
            out Dictionary<string, GameObject> worldNodes)
        {
            if (prefab == null || prefab.nodes == null || prefab.nodes.Length == 0 || parent == null)
                throw new ArgumentException("Official world float data or parent is missing.");
            var created = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            Transform[] transforms = new Transform[prefab.nodes.Length];
            GameObject root = null;
            for (int index = 0; index < prefab.nodes.Length; index++)
            {
                BattleHudNode source = prefab.nodes[index];
                if (source == null || source.parentIndex >= index || source.parentIndex < -1)
                    throw new FormatException("Official world float node hierarchy is invalid.");
                GameObject gameObject = source.uses_rect_transform
                    ? new GameObject(source.name, typeof(RectTransform))
                    : new GameObject(source.name);
                gameObject.SetActive(false);
                gameObject.layer = parent.gameObject.layer;
                Transform nodeParent = source.parentIndex < 0 ? parent : transforms[source.parentIndex];
                if (nodeParent == null) throw new FormatException("Official world float parent node is missing.");
                Transform transform = gameObject.transform;
                transform.SetParent(nodeParent, false);
                transform.localPosition = Vector(source.local_position, Vector3.zero);
                transform.localScale = Vector(source.scale, Vector3.one);
                transform.localEulerAngles = new Vector3(0f, 0f, source.rotation);
                if (source.uses_rect_transform)
                {
                    RectTransform rect = (RectTransform)transform;
                    rect.anchorMin = Vector(source.anchor_min, new Vector2(0.5f, 0.5f));
                    rect.anchorMax = Vector(source.anchor_max, new Vector2(0.5f, 0.5f));
                    rect.anchoredPosition = Vector(source.anchored_position, Vector2.zero);
                    rect.sizeDelta = Vector(source.size_delta, Vector2.zero);
                    rect.pivot = Vector(source.pivot, new Vector2(0.5f, 0.5f));
                }
                if (!string.IsNullOrEmpty(source.bitmap_font))
                {
                    var text = gameObject.AddComponent<TextMesh>();
                    text.font = Resources.Load<Font>("Combat/Numbers/" + source.bitmap_font);
                    if (text.font == null) throw new InvalidOperationException("Official bitmap font is missing: " + source.bitmap_font);
                    text.characterSize = source.bitmap_character_size;
                    text.fontSize = source.bitmap_font_size;
                    text.fontStyle = (FontStyle)source.bitmap_style;
                    text.anchor = (TextAnchor)source.bitmap_anchor;
                    text.alignment = (TextAlignment)source.bitmap_alignment;
                    text.lineSpacing = source.bitmap_line_spacing;
                    text.tabSize = source.bitmap_tab_size;
                    text.richText = source.bitmap_rich_text;
                    text.color = ColorValue(source.bitmap_color, Color.white);
                    text.text = string.Empty;
                    var meshRenderer = text.GetComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = text.font.material;
                    meshRenderer.sortingOrder = source.bitmap_sorting_order;
                }
                if (source.has_text)
                {
                    if (catalog.Font == null)
                        throw new InvalidOperationException("Official world float TMP font is missing.");
                    var text = gameObject.AddComponent<TextMeshPro>();
                    // Inactive world text has not run Awake; cache its required renderer before LoadFontAsset.
                    _ = text.renderer;
                    text.font = catalog.Font;
                    text.fontSize = Mathf.Max(1f, source.text_font_size);
                    text.color = ColorValue(source.text_color, Color.white);
                    text.alignment = (TextAlignmentOptions)(source.text_horizontal | source.text_vertical);
                    text.enableWordWrapping = source.text_word_wrap;
                    text.overflowMode = (TextOverflowModes)Mathf.Clamp(source.text_overflow, 0,
                        (int)TextOverflowModes.Page);
                    text.text = source.text_value ?? string.Empty;
                }
                if (source.has_sprite_renderer)
                {
                    var sprite = gameObject.AddComponent<SpriteRenderer>();
                    sprite.sprite = ResolveSprite(source.image_sprite_file);
                    if (sprite.sprite == null)
                        throw new InvalidOperationException("Official world float sprite is missing: "
                            + source.image_sprite_file);
                    sprite.color = ColorValue(source.image_color, Color.white);
                    sprite.sortingOrder = source.image_order;
                }
                if (!string.IsNullOrEmpty(source.path)) created[source.path] = gameObject;
                transforms[index] = transform;
                if (source.parentIndex == -1)
                {
                    if (root != null) throw new FormatException("Official world float has multiple roots.");
                    root = gameObject;
                }
                gameObject.SetActive(source.active);
            }
            if (root == null || root.name != prefab.rootName)
                throw new FormatException("Official world float root does not match its source prefab.");
            worldNodes = created;
            return root;
        }

        public OfficialBattleHudAnimationPlayer InitializeWorldAnimations(BattleHudNode[] sourceNodes,
            GameObject instance, Dictionary<string, GameObject> worldNodes, bool useManualTick = false)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var player = instance.AddComponent<OfficialBattleHudAnimationPlayer>();
            player.Initialize(sourceNodes, worldNodes, useManualTick);
            player.PlayDefaultAnimations(activeNodesOnly: true);
            return player;
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

        private GameObject CreateNode(BattleHudNode source, Transform parent, Action<string, GameObject> bind)
        {
            var gameObject = new GameObject(source.name, typeof(RectTransform));
            gameObject.SetActive(false);
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector(source.anchor_min, Vector2.zero);
            rect.anchorMax = Vector(source.anchor_max, Vector2.zero);
            rect.anchoredPosition = Vector(source.anchored_position, Vector2.zero);
            rect.sizeDelta = Vector(source.size_delta, Vector2.zero);
            rect.pivot = Vector(source.pivot, new Vector2(0.5f, 0.5f));
            rect.localScale = Vector(source.scale, Vector3.one);
            rect.localEulerAngles = new Vector3(0, 0, source.rotation);
            // Both SafeArea components in the verified FightMainPanel prefab have zero
            // padding and both adaptations enabled. Reuse the migrated official implementation.
            if (HasScript(source, "SafeArea") && source.path.StartsWith("/FightMainPanel/",
                StringComparison.Ordinal))
                gameObject.AddComponent<Zjcs.Official.WorldUI.WorldSafeArea>();
            if (source.alpha < 0.999f)
                gameObject.AddComponent<CanvasGroup>().alpha = Mathf.Clamp01(source.alpha);
            if (source.has_canvas_group_flags)
            {
                CanvasGroup group = gameObject.GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
                group.interactable = source.canvas_group_interactable;
                group.blocksRaycasts = source.canvas_group_blocks_raycasts;
            }

            if (source.has_image)
            {
                Image image = gameObject.AddComponent<Image>();
                image.material = OfficialBattleHudMaterialAssets.Load().Find(source.path);
                image.sprite = source.has_image_sprite ? ResolveSprite(source.image_sprite_file) : null;
                image.color = ColorValue(source.image_color, Color.white);
                image.type = (Image.Type)Mathf.Clamp(source.image_type, 0, (int)Image.Type.Filled);
                image.preserveAspect = source.image_preserve_aspect;
                if (source.image_graphic_flags)
                {
                    image.raycastTarget = source.image_raycast_target;
                    image.maskable = source.image_maskable;
                }
                if (source.image_type == (int)Image.Type.Sliced && source.image_sprite_border != null)
                    image.type = Image.Type.Sliced;
            }
            else if (HasScript(source, "RawImage") || HasScript(source, "GridSpriteRawImage"))
            {
                RawImage image = gameObject.AddComponent<RawImage>();
                image.color = Color.white;
                image.raycastTarget = false;
            }
            if (source.has_text)
            {
                TMP_FontAsset font = catalog.Font;
                if (font == null) throw new InvalidOperationException("Official battle HUD font is missing at " + source.path);
                TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
                if (text == null) throw new InvalidOperationException("TMP text component failed to mount at " + source.path);
                try { text.text = source.text_value ?? string.Empty; }
                catch (Exception exception)
                {
                    throw new InvalidOperationException("Official TMP text assignment failed at " + source.path, exception);
                }
                try { text.font = font; }
                catch (Exception exception)
                {
                    throw new InvalidOperationException("Official TMP font assignment failed at " + source.path, exception);
                }
                text.fontSize = Mathf.Max(1, source.text_font_size);
                text.color = ColorValue(source.text_color, Color.white);
                text.alignment = (TextAlignmentOptions)(source.text_horizontal | source.text_vertical);
                text.enableWordWrapping = source.text_word_wrap;
                text.overflowMode = (TextOverflowModes)Mathf.Clamp(source.text_overflow, 0, (int)TextOverflowModes.Page);
                text.margin = Vector4Value(source.text_margin);
                if (source.text_auto_size != null && source.text_auto_size.Length >= 2)
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = source.text_auto_size[0];
                    text.fontSizeMax = source.text_auto_size[1];
                }
                text.raycastTarget = false;
            }
            if (HasScript(source, "NoDrawingRayCast") && gameObject.GetComponent<Graphic>() == null)
            {
                var graphic = gameObject.AddComponent<Zjcs.Official.WorldUI.NoDrawingRayCast>();
                if (source.image_graphic_flags)
                {
                    graphic.raycastTarget = source.image_raycast_target;
                }
            }
            if (source.button)
            {
                if (gameObject.GetComponent<Graphic>() == null) gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                gameObject.AddComponent<Button>();
            }
            if (source.mask) gameObject.AddComponent<Mask>().showMaskGraphic =
                !source.mask_graphic_flags || source.mask_show_graphic;
            if (source.rectmask2d) gameObject.AddComponent<RectMask2D>();
            if (source.has_scroll)
            {
                ScrollRect scroll = gameObject.AddComponent<ScrollRect>();
                scroll.horizontal = source.scroll_horizontal;
                scroll.vertical = source.scroll_vertical;
                scroll.inertia = source.scroll_inertia;
                scroll.movementType = (ScrollRect.MovementType)Mathf.Clamp(source.scroll_movement, 0, 2);
                scroll.scrollSensitivity = source.scroll_sensitivity;
                scroll.elasticity = source.scroll_elasticity;
                scroll.decelerationRate = source.scroll_deceleration;
            }
            if (source.has_layout)
            {
                HorizontalOrVerticalLayoutGroup layout = source.layout_axis == 0
                    ? (HorizontalOrVerticalLayoutGroup)gameObject.AddComponent<HorizontalLayoutGroup>()
                    : gameObject.AddComponent<VerticalLayoutGroup>();
                if (source.layout_padding != null && source.layout_padding.Length >= 4)
                    layout.padding = new RectOffset(Mathf.RoundToInt(source.layout_padding[0]),
                        Mathf.RoundToInt(source.layout_padding[1]), Mathf.RoundToInt(source.layout_padding[2]),
                        Mathf.RoundToInt(source.layout_padding[3]));
                layout.childAlignment = (TextAnchor)Mathf.Clamp(source.layout_alignment, 0, 8);
                layout.spacing = source.layout_spacing;
                layout.reverseArrangement = source.layout_reverse;
                layout.childForceExpandWidth = Flag(source.layout_expand, 0);
                layout.childForceExpandHeight = Flag(source.layout_expand, 1);
                layout.childControlWidth = Flag(source.layout_control, 0);
                layout.childControlHeight = Flag(source.layout_control, 1);
                layout.childScaleWidth = Flag(source.layout_child_scale, 0);
                layout.childScaleHeight = Flag(source.layout_child_scale, 1);
            }
            if (source.has_size_fitter)
            {
                var fitter = gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(source.fitter_horizontal, 0, 2);
                fitter.verticalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(source.fitter_vertical, 0, 2);
            }
            if (source.has_layout_element)
            {
                var element = gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = source.layout_element_ignore;
                element.minWidth = Value(source.layout_element_min, 0, -1);
                element.minHeight = Value(source.layout_element_min, 1, -1);
                element.preferredWidth = Value(source.layout_element_preferred, 0, -1);
                element.preferredHeight = Value(source.layout_element_preferred, 1, -1);
                element.flexibleWidth = Value(source.layout_element_flexible, 0, -1);
                element.flexibleHeight = Value(source.layout_element_flexible, 1, -1);
            }
            if (!string.IsNullOrEmpty(source.path)) nodes[source.path] = gameObject;
            bind?.Invoke(source.path, gameObject);
            if (source.children != null)
                foreach (BattleHudNode child in source.children) CreateNode(child, rect, bind);
            if (source.has_scroll)
            {
                ScrollRect scroll = gameObject.GetComponent<ScrollRect>();
                scroll.viewport = FindDescendant(rect, "Viewport", "ViewPort") as RectTransform;
                scroll.content = FindDescendant(rect, "Content") as RectTransform;
            }
            gameObject.SetActive(source.active);
            return gameObject;
        }

        private Sprite ResolveSprite(string file)
        {
            Sprite sprite = catalog.FindSprite(file);
            return sprite != null || spriteFallback == null ? sprite : spriteFallback(file);
        }

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

        private static bool HasScript(BattleHudNode source, string name)
        {
            if (source.scripts == null) return false;
            foreach (string script in source.scripts)
                if (script == name) return true;
            return false;
        }

        private static Vector2 Vector(float[] v, Vector2 fallback) => v != null && v.Length >= 2 ? new Vector2(v[0], v[1]) : fallback;
        private static Vector3 Vector(float[] v, Vector3 fallback) => v != null && v.Length >= 2
            ? new Vector3(v[0], v[1], v.Length >= 3 ? v[2] : fallback.z) : fallback;
        private static Color ColorValue(float[] v, Color fallback) => v != null && v.Length >= 4 ? new Color(v[0], v[1], v[2], v[3]) : fallback;
        private static Vector4 Vector4Value(float[] v) => v != null && v.Length >= 4 ? new Vector4(v[0], v[1], v[2], v[3]) : Vector4.zero;
        private static bool Flag(bool[] values, int index) => values != null && values.Length > index && values[index];
        private static float Value(float[] values, int index, float fallback) => values != null && values.Length > index ? values[index] : fallback;

    }
}
