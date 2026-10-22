using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Opening
{
    /// <summary>Official story UI payload: prefab layout trees plus one sprite atlas.</summary>
    public sealed class OpeningUiData
    {
        public string Manifest = string.Empty;
        public readonly List<byte[]> AtlasBytes = new List<byte[]>();
        public readonly List<OpeningUiSprite> AtlasPages = new List<OpeningUiSprite>();
        public readonly Dictionary<string, OpeningUiSprite> Sprites =
            new Dictionary<string, OpeningUiSprite>(StringComparer.Ordinal);
        public readonly Dictionary<string, OpeningUiNode> Prefabs =
            new Dictionary<string, OpeningUiNode>(StringComparer.Ordinal);

        public static OpeningUiData Parse(byte[] bytes, string directory)
        {
            Dictionary<string, object> root = OpeningJson.Object(OpeningJson.Parse(bytes), "ui");
            var data = new OpeningUiData { Manifest = OpeningJson.String(root, "manifest") };
            if (data.Manifest != OpeningAssets.Manifest)
                throw new FormatException("Opening UI manifest is not pinned.");
            var atlases = new List<Dictionary<string, object>>();
            if (root.TryGetValue("atlases", out object pages) && pages is List<object> pageList)
                foreach (object entry in pageList)
                    atlases.Add(OpeningJson.Object(entry, "atlas page"));
            else
                atlases.Add(OpeningJson.Object(root["atlas"], "atlas"));
            foreach (Dictionary<string, object> atlas in atlases)
            {
                string atlasPath = Path.Combine(directory, OpeningJson.String(atlas, "file"));
                if (!File.Exists(atlasPath)) throw new FileNotFoundException("Opening UI atlas is missing.", atlasPath);
                byte[] pageBytes = File.ReadAllBytes(atlasPath);
                data.AtlasBytes.Add(pageBytes);
                data.AtlasPages.Add(new OpeningUiSprite
                {
                    Width = OpeningJson.Int(atlas, "width"),
                    Height = OpeningJson.Int(atlas, "height"),
                    Name = OpeningJson.String(atlas, "file"),
                });
            }
            foreach (KeyValuePair<string, object> entry in OpeningJson.Object(root["sprites"], "sprites"))
            {
                Dictionary<string, object> sprite = OpeningJson.Object(entry.Value, "sprite");
                List<object> pivot = OpeningJson.Array(sprite["pivot"], "pivot");
                List<object> border = null;
                if (sprite.TryGetValue("border", out object borderValue)) border = OpeningJson.Array(borderValue, "border");
                data.Sprites[entry.Key] = new OpeningUiSprite
                {
                    Name = entry.Key,
                    X = OpeningJson.Int(sprite, "x"),
                    Y = OpeningJson.Int(sprite, "y"),
                    Width = OpeningJson.Int(sprite, "w"),
                    Height = OpeningJson.Int(sprite, "h"),
                    Pivot = new Vector2((float)OpeningJson.ToNumber(pivot[0]), (float)OpeningJson.ToNumber(pivot[1])),
                    Border = border == null ? Vector4.zero : new Vector4(
                        (float)OpeningJson.ToNumber(border[0]), (float)OpeningJson.ToNumber(border[1]),
                        (float)OpeningJson.ToNumber(border[2]), (float)OpeningJson.ToNumber(border[3])),
                    PixelsPerUnit = (float)OpeningJson.Number(sprite, "ppu", 100),
                    Atlas = OpeningJson.Int(sprite, "atlas"),
                };
            }
            foreach (object item in OpeningJson.Array(root["prefabs"], "prefabs"))
            {
                Dictionary<string, object> prefab = OpeningJson.Object(item, "prefab");
                data.Prefabs[OpeningJson.String(prefab, "key")] = ParseNode(OpeningJson.Object(prefab["root"], "root"));
            }
            return data;
        }

        private static OpeningUiNode ParseNode(Dictionary<string, object> node)
        {
            var converted = new OpeningUiNode
            {
                Name = OpeningJson.String(node, "name"),
                Path = OpeningJson.String(node, "path"),
                Active = OpeningJson.Bool(node, "active", true),
                Button = OpeningJson.Bool(node, "button"),
                Mask = OpeningJson.Bool(node, "mask"),
                HasImage = OpeningJson.Bool(node, "has_image"),
                HasText = OpeningJson.Bool(node, "has_text"),
                Sprite = OpeningJson.String(node, "sprite"),
                Alpha = (float)OpeningJson.Number(node, "alpha", 1),
                Type = OpeningJson.Int(node, "image_type"),
                PreserveAspect = OpeningJson.Bool(node, "image_preserve_aspect"),
                TextValue = OpeningJson.String(node, "text_value"),
                FontSize = (float)OpeningJson.Number(node, "text_font_size", 32),
                FontWeight = OpeningJson.Int(node, "text_font_weight", 400),
                FontStyle = OpeningJson.Int(node, "text_font_style"),
                Alignment = OpeningJson.Int(node, "text_alignment"),
                Margin = ReadVector4(node, "text_margin"),
                WordWrap = OpeningJson.Bool(node, "text_word_wrap"),
                Overflow = OpeningJson.Int(node, "text_overflow"),
                AutoSizing = OpeningJson.Bool(node, "text_auto_sizing"),
                FontSizeRange = ReadVector(node, "text_font_size_range", Vector2.zero),
                AnchorMin = ReadVector(node, "anchor_min", new Vector2(0.5f, 0.5f)),
                AnchorMax = ReadVector(node, "anchor_max", new Vector2(0.5f, 0.5f)),
                AnchoredPosition = ReadVector(node, "anchored_position", Vector2.zero),
                SizeDelta = ReadVector(node, "size_delta", Vector2.zero),
                Pivot = ReadVector(node, "pivot", new Vector2(0.5f, 0.5f)),
                Scale = ReadVector(node, "scale", Vector2.one),
            };
            converted.ImageColor = ReadColor(node, "image_color", Color.white);
            converted.TextColor = ReadColor(node, "text_color", Color.white);
            if (node.TryGetValue("children", out object children) && children is List<object> list)
                foreach (object item in list) converted.Children.Add(ParseNode(OpeningJson.Object(item, "child")));
            return converted;
        }

        private static Vector2 ReadVector(Dictionary<string, object> node, string key, Vector2 fallback)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 2)
                return fallback;
            return new Vector2((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]));
        }

        private static Vector4 ReadVector4(Dictionary<string, object> node, string key)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 4)
                return Vector4.zero;
            return new Vector4((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]),
                (float)OpeningJson.ToNumber(list[2]), (float)OpeningJson.ToNumber(list[3]));
        }

        private static Color ReadColor(Dictionary<string, object> node, string key, Color fallback)
        {
            if (node == null || !node.TryGetValue(key, out object value) || !(value is List<object> list) || list.Count < 4)
                return fallback;
            return new Color((float)OpeningJson.ToNumber(list[0]), (float)OpeningJson.ToNumber(list[1]),
                (float)OpeningJson.ToNumber(list[2]), (float)OpeningJson.ToNumber(list[3]));
        }
    }

    public sealed class OpeningUiSprite
    {
        public string Name = string.Empty;
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);
        public Vector4 Border;
        public float PixelsPerUnit = 100;
        public int Atlas;
    }

    public sealed class OpeningUiNode
    {
        public string Name = string.Empty;
        public string Path = string.Empty;
        public bool Active = true;
        public bool Button;
        public bool Mask;
        public bool HasImage;
        public bool HasText;
        public string Sprite = string.Empty;
        public Color ImageColor = Color.white;
        public Color TextColor = Color.white;
        public int Type;
        public bool PreserveAspect;
        public string TextValue = string.Empty;
        public float FontSize = 32;
        public int FontWeight = 400;
        public int FontStyle;
        public int Alignment;
        public Vector4 Margin;
        public bool WordWrap;
        public int Overflow;
        public bool AutoSizing;
        public Vector2 FontSizeRange = Vector2.zero;
        public float Alpha = 1;
        public Vector2 AnchorMin = new Vector2(0.5f, 0.5f);
        public Vector2 AnchorMax = new Vector2(0.5f, 0.5f);
        public Vector2 AnchoredPosition = Vector2.zero;
        public Vector2 SizeDelta = Vector2.zero;
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);
        public Vector2 Scale = Vector2.one;
        public readonly List<OpeningUiNode> Children = new List<OpeningUiNode>();
    }

    /// <summary>Instantiates official story panels from the shipped layout payload.</summary>
    public sealed class OpeningUiView : IDisposable
    {
        private readonly OpeningUiData data;
        private readonly TMP_FontAsset font;
        private readonly List<Texture2D> atlases = new List<Texture2D>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> bindings = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly GameObject root;

        public OpeningUiView(OpeningUiData data, TMP_FontAsset font, Transform parent, int sortingOrder)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.font = font;
            for (int index = 0; index < data.AtlasBytes.Count; index++)
            {
                var page = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!page.LoadImage(data.AtlasBytes[index]))
                    throw new InvalidOperationException("Opening UI atlas could not be decoded: page " + index);
                page.name = "OpeningUiAtlas_" + index;
                atlases.Add(page);
            }
            var canvasObject = new GameObject("OpeningCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            this.root = canvasObject;
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public Transform Root => root.transform;

        public GameObject Create(string key, Transform parent = null)
        {
            if (!data.Prefabs.TryGetValue(key, out OpeningUiNode node))
                throw new InvalidOperationException("Official story UI prefab is missing: " + key);
            bindings.Clear();
            GameObject created = Build(node, parent == null ? root.transform : parent);
            created.name = "Opening_" + key;
            return created;
        }

        public GameObject Find(string path)
        {
            return bindings.TryGetValue(path, out GameObject value) ? value : null;
        }

        public bool SetText(string path, string value)
        {
            GameObject node = Find(path);
            if (node != null)
            {
                TMP_Text text = node.GetComponent<TMP_Text>();
                if (text != null) { text.text = value ?? string.Empty; return true; }
            }
            foreach (KeyValuePair<string, GameObject> entry in bindings)
            {
                if (!entry.Key.EndsWith(path, StringComparison.Ordinal)) continue;
                TMP_Text text = entry.Value.GetComponent<TMP_Text>();
                if (text != null) { text.text = value ?? string.Empty; return true; }
            }
            return false;
        }

        public bool SetFirstTextEnding(string suffix, string value)
        {
            foreach (KeyValuePair<string, GameObject> entry in bindings)
            {
                if (!entry.Key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                TMP_Text text = entry.Value.GetComponent<TMP_Text>();
                if (text != null) { text.text = value ?? string.Empty; return true; }
            }
            return false;
        }

        public bool SetTextEnding(GameObject scope, string suffix, string value)
        {
            if (scope == null) return false;
            foreach (Transform child in scope.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.EndsWith(suffix, StringComparison.Ordinal)) continue;
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text == null) continue;
                text.text = value ?? string.Empty;
                return true;
            }
            return false;
        }

        public bool SetImage(GameObject scope, string nodeName, string spriteName, bool enabled, Vector2 offset)
        {
            if (scope == null) return false;
            Transform target = null;
            foreach (Transform child in scope.GetComponentsInChildren<Transform>(true))
                if (child.name == nodeName) { target = child; break; }
            if (target == null) return false;
            Image image = target.GetComponent<Image>();
            if (image == null) return false;
            image.sprite = null;
            image.enabled = false;
            if (enabled && TryGetSprite(spriteName, out Sprite sprite))
            {
                image.sprite = sprite;
                image.enabled = true;
                image.SetNativeSize();
            }
            ((RectTransform)target).anchoredPosition = offset;
            return true;
        }

        public bool SetActive(string path, bool active)
        {
            GameObject node = Find(path);
            if (node == null) return false;
            node.SetActive(active);
            return true;
        }

        public bool SetActiveEnding(string suffix, bool active)
        {
            foreach (KeyValuePair<string, GameObject> entry in bindings)
            {
                if (!entry.Key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                entry.Value.SetActive(active);
                return true;
            }
            return false;
        }

        public GameObject FindEnding(string suffix)
        {
            foreach (KeyValuePair<string, GameObject> entry in bindings)
                if (entry.Key.EndsWith(suffix, StringComparison.Ordinal)) return entry.Value;
            return null;
        }

        private GameObject Build(OpeningUiNode node, Transform parent)
        {
            var gameObject = new GameObject(node.Name, typeof(RectTransform));
            gameObject.SetActive(false);
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            // A few official containers are point-anchored with a zero size and rely on
            // their panel script to stretch them; without that script their stretched
            // children would collapse, so those containers inherit the parent rect.
            bool container = node.Children.Count > 0
                && node.AnchorMin == node.AnchorMax && node.SizeDelta == Vector2.zero;
            rect.anchorMin = container ? Vector2.zero : node.AnchorMin;
            rect.anchorMax = container ? Vector2.one : node.AnchorMax;
            rect.anchoredPosition = container ? Vector2.zero : node.AnchoredPosition;
            rect.sizeDelta = container ? Vector2.zero : node.SizeDelta;
            rect.pivot = container ? new Vector2(0.5f, 0.5f) : node.Pivot;
            rect.localScale = new Vector3(node.Scale.x, node.Scale.y, 1);
            if (node.Alpha < 0.999f) gameObject.AddComponent<CanvasGroup>().alpha = node.Alpha;
            if (node.HasImage)
            {
                Image image = gameObject.AddComponent<Image>();
                TryGetSprite(node.Sprite, out Sprite sprite);
                image.sprite = sprite;
                image.color = node.ImageColor;
                image.type = (Image.Type)Mathf.Clamp(node.Type, 0, (int)Image.Type.Filled);
                image.preserveAspect = node.PreserveAspect;
                if (node.Type == (int)Image.Type.Sliced && node.Sprite.Length > 0) image.type = Image.Type.Sliced;
            }
            if (node.HasText)
            {
                TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) text.font = font;
                text.text = node.TextValue ?? string.Empty;
                text.fontSize = Mathf.Max(1, node.FontSize);
                text.fontWeight = (FontWeight)Mathf.Clamp(node.FontWeight, 100, 900);
                text.fontStyle = (FontStyles)Mathf.Max(0, node.FontStyle);
                text.color = node.TextColor;
                // Alignment, margins, wrapping, overflow and auto-sizing are the
                // official TMP_Text serialized values (0xFFFF alignment resolves
                // through the horizontal/vertical axis fields during extraction).
                text.alignment = (TextAlignmentOptions)node.Alignment;
                text.margin = node.Margin;
                text.enableWordWrapping = node.WordWrap;
                text.overflowMode = (TextOverflowModes)Mathf.Clamp(node.Overflow, 0, 6);
                if (node.AutoSizing && node.FontSizeRange.y > 0f)
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = node.FontSizeRange.x;
                    text.fontSizeMax = node.FontSizeRange.y;
                }
                text.raycastTarget = false;
            }
            if (node.Button)
            {
                if (gameObject.GetComponent<Image>() == null)
                    gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                gameObject.AddComponent<Button>();
            }
            if (node.Mask) gameObject.AddComponent<Mask>().showMaskGraphic = false;
            if (!string.IsNullOrEmpty(node.Path)) bindings[node.Path] = gameObject;
            foreach (OpeningUiNode child in node.Children) Build(child, rect);
            gameObject.SetActive(node.Active);
            return gameObject;
        }

        private bool TryGetSprite(string name, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(name) || !data.Sprites.TryGetValue(name, out OpeningUiSprite entry)) return false;
            if (sprites.TryGetValue(name, out sprite)) return true;
            if (entry.Atlas < 0 || entry.Atlas >= atlases.Count) return false;
            var rect = new Rect(entry.X, entry.Y, entry.Width, entry.Height);
            sprite = Sprite.Create(atlases[entry.Atlas], rect, entry.Pivot, entry.PixelsPerUnit, 0,
                SpriteMeshType.FullRect, entry.Border);
            sprite.name = name;
            sprites[name] = sprite;
            return true;
        }

        public void Dispose()
        {
            foreach (Sprite sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            sprites.Clear();
            foreach (Texture2D atlas in atlases) if (atlas != null) UnityEngine.Object.Destroy(atlas);
            atlases.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
