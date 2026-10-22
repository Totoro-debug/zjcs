using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    internal sealed class OfficialMap10DialoguePortraitView : IDisposable
    {
        private const string PortraitDataResource = "Map10/dialogue-portraits";
        private const string LeftParentPath = "/TalkDialog/Content/CharacterAndTalkContent/CharacterGroup/LeftCharacter/Locator/PortraitParent";
        private const string RightParentPath = "/TalkDialog/Content/CharacterAndTalkContent/CharacterGroup/RightCharacter/Mask/Locator/PortraitParent";

        private readonly Dictionary<string, Map10RuntimeDialogPortrait> portraits =
            new Dictionary<string, Map10RuntimeDialogPortrait>(StringComparer.Ordinal);
        private readonly PortraitSlot[] slots = new PortraitSlot[2];
        private readonly Map10RuntimeDialogSpeakerSlot[] speakerStyles;
        private readonly string panelKey;
        private int currentSide = -1;
        private int currentRoleId;
        private bool disposed;

        public OfficialMap10DialoguePortraitView(OfficialMap10UiRenderer ui, string panelKey = "TalkDialog")
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            if (panelKey != "TalkDialog" && panelKey != "OptionTalkDialog")
                throw new ArgumentOutOfRangeException(nameof(panelKey));
            this.panelKey = panelKey;
            TextAsset dataAsset = Resources.Load<TextAsset>(PortraitDataResource);
            Map10RuntimeDialogPortraitData data = dataAsset == null ? null
                : JsonUtility.FromJson<Map10RuntimeDialogPortraitData>(dataAsset.text);
            if (data == null || data.manifest != "854_156383" || data.mapClassId != 10
                || data.portraits == null || data.speakerStyle == null
                || data.speakerStyle.left == null || data.speakerStyle.right == null || data.icons == null)
                throw new InvalidDataException("Official Map10 dialogue portrait data is missing or malformed.");

            speakerStyles = new[] { data.speakerStyle.left, data.speakerStyle.right };
            ValidateStyle(speakerStyles[0], 0);
            ValidateStyle(speakerStyles[1], 1);
            foreach (Map10RuntimeDialogPortrait portrait in data.portraits)
            {
                if (portrait == null || string.IsNullOrWhiteSpace(portrait.portraitPath)
                    || portrait.main == null || portrait.face == null || portrait.face.expressions == null
                    || portraits.ContainsKey(portrait.portraitPath))
                    throw new InvalidDataException("Official Map10 dialogue portrait layout is malformed.");
                RequireSprite(portrait.main.resource);
                foreach (Map10RuntimeDialogExpression expression in portrait.face.expressions)
                {
                    if (expression == null || string.IsNullOrWhiteSpace(expression.type)
                        || string.IsNullOrWhiteSpace(expression.resource)
                        || expression.position == null || expression.position.Length != 2)
                        throw new InvalidDataException("Official Map10 dialogue expression is malformed.");
                    RequireSprite(expression.resource);
                }
                portraits.Add(portrait.portraitPath, portrait);
            }
            foreach (Map10RuntimeDialogIcon icon in data.icons)
            {
                if (icon == null || string.IsNullOrWhiteSpace(icon.uri) || string.IsNullOrWhiteSpace(icon.resource))
                    throw new InvalidDataException("Official Map10 dialogue icon binding is malformed.");
                RequireSprite(icon.resource);
            }
            if (portraits.Count != 3 || !portraits.ContainsKey("UI/Portrait/Npc_Portrait_8402")
                || !portraits.ContainsKey("UI/Portrait/Npc_Portrait_8404")
                || !portraits.ContainsKey("UI/Portrait/Npc_Portrait_8417"))
                throw new InvalidDataException("Official Map10 dialogue portrait bindings are incomplete.");

            Transform left = FindParent(ui, panelKey, LeftParentPath.Replace("TalkDialog", panelKey));
            Transform right = FindParent(ui, panelKey, RightParentPath.Replace("TalkDialog", panelKey));
            slots[0] = new PortraitSlot(CreatePortraitRoot(left, "Map10LeftPortrait"));
            slots[1] = new PortraitSlot(CreatePortraitRoot(right, "Map10RightPortrait"));
        }

        public void SetSpeaker(Map10RuntimeDialogRole role, string faceType)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialMap10DialoguePortraitView));
            if (role == null || !portraits.TryGetValue(role.PortraitPath, out Map10RuntimeDialogPortrait portrait))
                throw new InvalidDataException("Map10 dialogue role has no native portrait layout.");

            if (currentRoleId != role.RoleId)
            {
                if (currentSide < 0)
                {
                    currentSide = 0;
                    slots[currentSide].SetPortrait(portrait, currentSide == 1);
                    slots[currentSide].SetColorImmediate(ToColor(speakerStyles[currentSide].light));
                }
                else
                {
                    int previousSide = currentSide;
                    currentSide = 1 - currentSide;
                    slots[previousSide].Transition(ToColor(speakerStyles[previousSide].dark),
                        speakerStyles[previousSide].colorDuration);
                    slots[currentSide].SetPortrait(portrait, currentSide == 1);
                    slots[currentSide].SetColorImmediate(ToColor(speakerStyles[currentSide].dark));
                    slots[currentSide].Transition(ToColor(speakerStyles[currentSide].light),
                        speakerStyles[currentSide].colorDuration);
                }
                currentRoleId = role.RoleId;
            }
            slots[currentSide].SetExpression(portrait, faceType);
        }

        public void Tick(float deltaTime)
        {
            if (disposed) return;
            slots[0]?.Tick(deltaTime);
            slots[1]?.Tick(deltaTime);
        }

        public void Clear()
        {
            if (disposed) return;
            slots[0]?.Clear();
            slots[1]?.Clear();
            currentRoleId = 0;
            currentSide = -1;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            slots[0]?.Dispose();
            slots[1]?.Dispose();
        }

        private static Transform FindParent(OfficialMap10UiRenderer ui, string panelKey, string path)
        {
            GameObject parent = ui.Find(panelKey, path);
            if (parent == null) throw new InvalidDataException("Official dialogue portrait parent is missing: " + path);
            return parent.transform;
        }

        private static RectTransform CreatePortraitRoot(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);
            root.SetActive(false);
            return rect;
        }

        private static void ValidateStyle(Map10RuntimeDialogSpeakerSlot style, int direction)
        {
            if (style.direction != direction || style.light == null || style.light.Length != 4
                || style.dark == null || style.dark.Length != 4 || style.colorDuration < 0f)
                throw new InvalidDataException("Official Map10 TalkDialog speaker style is invalid.");
        }

        private static Color ToColor(float[] value)
        {
            return new Color(value[0], value[1], value[2], value[3]);
        }

        private static Sprite RequireSprite(string resource)
        {
            Sprite sprite = Resources.Load<Sprite>(resource);
            if (sprite == null) throw new InvalidDataException("Official Map10 dialogue Sprite is missing: " + resource);
            return sprite;
        }

        private sealed class PortraitSlot : IDisposable
        {
            private readonly RectTransform root;
            private readonly Image mainImage;
            private readonly Image faceImage;
            private Color currentColor = Color.white;
            private Color fromColor;
            private Color targetColor;
            private float elapsed;
            private float duration;
            private bool transitioning;
            private string currentPortraitPath;

            public PortraitSlot(RectTransform root)
            {
                this.root = root;
                mainImage = CreateImage(root, "Main");
                faceImage = CreateImage(root, "Face");
                faceImage.enabled = false;
            }

            public void SetPortrait(Map10RuntimeDialogPortrait portrait, bool onRight)
            {
                if (currentPortraitPath != portrait.portraitPath)
                {
                    root.name = portrait.portraitPath.Substring(portrait.portraitPath.LastIndexOf('/') + 1);
                    root.localScale = onRight && !portrait.needFlipOnRight
                        ? new Vector3(-1f, 1f, 1f) : Vector3.one;
                    mainImage.gameObject.name = portrait.main.nodeName;
                    faceImage.gameObject.name = portrait.face.nodeName;
                    Configure(mainImage, portrait.main, RequireSprite(portrait.main.resource));
                    Map10RuntimeDialogPortraitLayer faceLayer = new Map10RuntimeDialogPortraitLayer
                    {
                        nodeName = portrait.face.nodeName,
                        position = portrait.face.position,
                        nativeSize = portrait.face.nativeSize,
                        pivot = portrait.face.pivot
                    };
                    Configure(faceImage, faceLayer, null);
                    faceImage.enabled = false;
                    faceImage.sprite = null;
                    currentPortraitPath = portrait.portraitPath;
                }
                root.gameObject.SetActive(true);
            }

            public void SetExpression(Map10RuntimeDialogPortrait portrait, string faceType)
            {
                if (string.IsNullOrEmpty(faceType))
                {
                    faceImage.enabled = false;
                    faceImage.sprite = null;
                    return;
                }
                Map10RuntimeDialogExpression expression = portrait.face.expressions
                    .FirstOrDefault(value => value.type == faceType);
                if (expression == null)
                    throw new InvalidDataException("Official Map10 portrait expression is missing: " + faceType);
                faceImage.sprite = RequireSprite(expression.resource);
                faceImage.rectTransform.anchoredPosition = ToVector2(expression.position);
                faceImage.SetNativeSize();
                faceImage.enabled = true;
            }

            public void SetColorImmediate(Color color)
            {
                transitioning = false;
                currentColor = color;
                ApplyColor();
            }

            public void Transition(Color color, float seconds)
            {
                targetColor = color;
                fromColor = currentColor;
                elapsed = 0f;
                duration = seconds;
                transitioning = seconds > 0f && fromColor != targetColor;
                if (!transitioning) SetColorImmediate(targetColor);
            }

            public void Tick(float deltaTime)
            {
                if (!transitioning) return;
                elapsed += Mathf.Max(0f, deltaTime);
                float progress = Mathf.Clamp01(elapsed / duration);
                currentColor = Color.Lerp(fromColor, targetColor, progress);
                ApplyColor();
                if (progress >= 1f) transitioning = false;
            }

            public void Clear()
            {
                transitioning = false;
                currentPortraitPath = null;
                mainImage.sprite = null;
                faceImage.sprite = null;
                faceImage.enabled = false;
                root.gameObject.SetActive(false);
                SetColorImmediate(Color.white);
            }

            public void Dispose()
            {
                if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            }

            private void ApplyColor()
            {
                mainImage.color = currentColor;
                faceImage.color = currentColor;
            }

            private static Image CreateImage(RectTransform parent, string name)
            {
                var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(parent, false);
                Image image = child.GetComponent<Image>();
                image.raycastTarget = false;
                image.maskable = true;
                return image;
            }

            private static void Configure(Image image, Map10RuntimeDialogPortraitLayer layer, Sprite sprite)
            {
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = ToVector2(layer.pivot);
                rect.anchoredPosition = ToVector2(layer.position);
                rect.sizeDelta = ToVector2(layer.nativeSize);
                image.sprite = sprite;
                if (sprite != null) image.SetNativeSize();
            }

            private static Vector2 ToVector2(float[] values)
            {
                if (values == null || values.Length != 2)
                    throw new InvalidDataException("Official Map10 portrait Vector2 is malformed.");
                return new Vector2(values[0], values[1]);
            }
        }
    }
}
