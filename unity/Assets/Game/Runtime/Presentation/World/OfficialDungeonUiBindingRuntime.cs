using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    internal static class OfficialDungeonUiBindingRuntime
    {
        [Serializable] private sealed class Document { public string manifest; public Prefab[] prefabs; public RouteStyles routeStyles; }
        [Serializable] private sealed class Prefab
        { public string name; public PopupBinding popup; public TransformBinding[] transforms; public SafeAreaBinding[] safeAreas; public LayoutBinding[] layouts; public ScrollBinding[] scrolls; public ButtonBinding[] buttons;
          public RoomViewBinding roomView; public RouteWindowBinding routeWindow; public GradeItemBinding gradeItem; public PrivateDetailBinding privateDetail; public PrivateDialogBinding privateDialog; public PrivateUnlockBinding privateUnlock;
          public LocalizationBinding[] localizations;
          public GradeGridBinding[] gradeGrids; public string roomButton; public ImageBinding[] images; public TextBinding[] texts; }
        [Serializable] private sealed class PopupBinding
        { public string path; public string[] objects; public bool enabled, closeByClickMask; public int maskType; public PopupClipBinding[] clips; public PopupMaskBinding mask; public PopupSoundBinding sound; public MatchingBinding matching; }
        [Serializable] private sealed class MatchingBinding
        {
            public string path; public string[] objects;
            public bool enabled, playAutomatically, animatePhysics; public int wrapMode, cullingType;
            public MatchingClipBinding clip; public MatchingSoundBinding sound;
            public MatchingCanvasBinding[] canvasGroups; public MatchingTargetBinding[] targets;
        }
        [Serializable] private sealed class MatchingClipBinding
        { public string name, resource; public float duration, sampleRate; }
        [Serializable] private sealed class MatchingSoundBinding
        { public bool enabled; public PopupSoundEventBinding[] events; }
        [Serializable] private sealed class MatchingCanvasBinding
        { public string path; public string[] objects; public bool enabled, interactable, blocksRaycasts, ignoreParentGroups; public float alpha; }
        [Serializable] private sealed class MatchingTargetBinding
        { public string path, type; }
        [Serializable] private sealed class PopupSoundBinding
        { public string path; public string[] objects; public bool enabled; public PopupSoundEventBinding show, hide; public PopupClickSoundBinding[] clicks; }
        [Serializable] private sealed class PopupClickSoundBinding
        { public string type, path; public string[] objects; public bool enabled; public PopupSoundEventBinding @event; }
        [Serializable] private sealed class PopupSoundEventBinding
        { public string name, resource; public uint id; }
        [Serializable] private sealed class PopupMaskBinding
        { public string canvasResource, path, image, button, materialResource; public bool enabled; public ButtonBinding[] buttons; }
        [Serializable] private sealed class PopupClipBinding
        { public string field, name, resource; public float duration; public bool loop; }
        [Serializable] private sealed class LayoutBinding
        {
            public string path, type; public string[] objects; public bool enabled;
            public float[] padding, cellSize, spacing; public int[] fit, softness;
            public int alignment, startCorner, startAxis, constraint, constraintCount;
            public float layoutSpacing; public float[] spacingList; public int[] flags; public LayoutElementBinding element; public ClamperBinding clamp;
        }
        [Serializable] private sealed class ClamperBinding
        { public int mMaxPreferredWidth, mMaxPreferredHeight; }
        [Serializable] private sealed class PrivateDisplayBinding
        { public int dungeonId, privateDungeonId, privateDifficulty; public DifficultyBinding[] difficulties; public SpriteBinding background, icon; }
        [Serializable] private sealed class DifficultyBinding { public int value; public string name, key; }
        public static int PrivateDisplayDungeonId => LoadDocument().routeStyles.privateDisplay.dungeonId;
        [Serializable] private sealed class LocalizationBinding
        { public string path, key; public string[] objects; public bool enabled; }
        [Serializable] private sealed class PrivateDialogBinding
        { public string background, icon, title; public ObjectStateBinding[] titleStates; }
        [Serializable] private sealed class PrivateDetailBinding
        { public bool isPrivate; public string title, description, gradeRange; public ObjectStateBinding[] lockStates; }
        [Serializable] private sealed class PrivateUnlockBinding
        { public ObjectStateBinding[] buttonStates; public int enterState; public string rewardTitle, rewardTitleLayout; }
        [Serializable] private sealed class LayoutElementBinding
        { public bool ignore; public float[] minimum, preferred, flexible; public int priority; }
        [Serializable] private sealed class RouteWindowBinding
        {
            public string scroll, chooseScroll, grid, chooseGrid, gradeSlot, chooseGradeSlot, gradePrefab, team, tip, countdown;
            public ObjectStateBinding[] chooseStates;
        }
        [Serializable] private sealed class ObjectStateBinding { public int state; public string[] objects; }
        [Serializable] private sealed class GradeItemBinding { public string image, fill, text; }
        [Serializable] private sealed class GradeGridBinding { public string path, scroll, rect; }
        [Serializable] private sealed class GradeStyleBinding { public int grade; public string key; public float[] color; }
        [Serializable] private sealed class TextValueBinding { public string key, value; }
        public sealed class RouteWindowView
        {
            public LoopVerticalScrollRect Scroll;
            public GridLayoutGroup Grid;
            public Transform GradeSlot;
            public string GradePrefab;
        }
        [Serializable] private sealed class ButtonBinding
        {
            public string path, target; public string[] objects, triggers;
            public bool enabled, interactable, wrapAround; public int navigation, transition; public float[] colors;
        }
        [Serializable] private sealed class ScrollBinding
        {
            public string path, type, content, viewport, prefab; public string[] objects;
            public bool enabled, reverseDirection, reverseIndex, horizontal, vertical, inertia;
            public int totalCount, movementType, horizontalScrollbarVisibility, verticalScrollbarVisibility;
            public float elasticity, decelerationRate, scrollSensitivity, horizontalScrollbarSpacing, verticalScrollbarSpacing;
        }
        [Serializable] private sealed class RoomViewBinding
        {
            public string content, icon, frame, select, reward, animation, voteAnimation; public string[] selectedPlayers; public RoadBinding[] roads;
        }
        [Serializable] private sealed class RoadBinding
        { public string path, image, animation; public int offsetType; }
        [Serializable] private sealed class RouteStyles
        { public RoomStyleBinding[] rooms; public RoadStyleBinding[] roads; public GradeStyleBinding[] grades; public TextValueBinding[] texts; public PrivateDisplayBinding privateDisplay; }
        [Serializable] private sealed class RoomStyleBinding
        { public int type; public SpriteBinding passIcon, noPassIcon, passFrame, noPassFrame, select, reward; }
        [Serializable] private sealed class RoadStyleBinding
        { public int status; public float[] color; }
        [Serializable] private sealed class SafeAreaBinding
        {
            public string path; public string[] objects; public bool enabled, disableTopAdapt, disableBottomAdapt;
            public float paddingTop, paddingBottom;
        }
        [Serializable] private sealed class TransformBinding
        {
            public string path; public string[] objects; public bool isRectTransform;
            public float[] localPosition, localRotation, localScale, anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot;
        }
        [Serializable] private sealed class ImageBinding
        {
            public string path; public string[] objects; public bool enabled, preserveAspect, raycastTarget, maskable;
            public int type; public float[] color; public SpriteBinding sprite;
        }
        [Serializable] private sealed class SpriteBinding
        { public string file, name; public float[] pivot, border; public float pixelsPerUnit; public string sha256; }
        [Serializable] private sealed class TextBinding
        {
            public string path; public string[] objects; public string text; public float fontSize;
            public int fontWeight, fontStyle, alignment, overflow; public bool autoSizing, wordWrap;
            public float[] fontSizeRange, margin, color;
        }

        private static Document cachedDocument;
        private static readonly Dictionary<string, Sprite> loadedSprites = new Dictionary<string, Sprite>();

        private static void RestorePopup(GameObject root, PopupBinding binding, Action<AudioClip, GameObject> postSound)
        {
            if (binding == null) return;
            Transform target = Find(root.transform, binding.path, binding.objects);
            var popup = target.GetComponent<WorldPopupWindow>() ?? target.gameObject.AddComponent<WorldPopupWindow>();
            popup.mShowClip = popup.mHideClip = popup.mLoopClip = null;
            foreach (PopupClipBinding source in binding.clips)
            {
                AnimationClip clip = Resources.Load<AnimationClip>(source.resource);
                if (clip == null || clip.name != source.name || clip.isLooping != source.loop
                    || !Mathf.Approximately(clip.length, source.duration))
                    throw new InvalidDataException("Original private dungeon popup animation is missing or mismatched: " + source.resource);
                switch (source.field)
                {
                    case "mShowClip": popup.mShowClip = clip; break;
                    case "mHideClip": popup.mHideClip = clip; break;
                    case "mLoopClip": popup.mLoopClip = clip; break;
                    default: throw new InvalidDataException("Unsupported original popup clip field: " + source.field);
                }
            }
            popup.enabled = binding.enabled;
            popup.CloseByClickMask = binding.closeByClickMask;
            if (binding.maskType != 3 || binding.mask == null || !binding.mask.enabled)
                throw new InvalidDataException("Original private dungeon blur mask binding is missing or unsupported.");
            PopupMaskBinding mask = binding.mask;
            GameObject canvas = Resources.Load<GameObject>(mask.canvasResource);
            Transform nativeMask = canvas == null ? null : canvas.transform.Find(mask.path);
            var material = Resources.Load<Material>(mask.materialResource);
            var driver = target.GetComponent<OfficialDungeonPopupMask>() ?? target.gameObject.AddComponent<OfficialDungeonPopupMask>();
            driver.Initialize(popup, nativeMask == null ? null : nativeMask.gameObject, material, mask.image, mask.button,
                instance => RestoreButtons(instance, new Prefab { buttons = mask.buttons }));
            PopupSoundBinding sound = binding.sound;
            if (sound == null || sound.show != null || sound.hide == null || sound.hide.name != "Main_Cursor_Back"
                || sound.hide.id != 3686182067 || postSound == null)
                throw new InvalidDataException("Original private popup sound service or event binding is missing.");
            GameObject emitter = Find(root.transform, sound.path, sound.objects).gameObject;
            var soundSource = emitter.GetComponent<WorldPopupWindowSound>() ?? emitter.AddComponent<WorldPopupWindowSound>();
            soundSource.ShowSound = null;
            soundSource.HideSound = Resources.Load<AudioClip>(sound.hide.resource);
            if (soundSource.HideSound == null || soundSource.HideSound.name != sound.hide.name)
                throw new InvalidDataException("Original private popup close sound is missing: " + sound.hide.resource);
            soundSource.Post = postSound;
            soundSource.enabled = sound.enabled;
            if (sound.clicks == null || sound.clicks.Length != 19)
                throw new InvalidDataException("Original private UI click sound bindings are incomplete.");
            foreach (PopupClickSoundBinding click in sound.clicks)
            {
                GameObject node = Find(root.transform, click.path, click.objects).gameObject;
                AudioClip clip = Resources.Load<AudioClip>(click.@event.resource);
                if (clip == null || clip.name != click.@event.name)
                    throw new InvalidDataException("Original private click sound is missing: " + click.@event.resource);
                if (click.type == "UIClickSound")
                {
                    var listener = node.GetComponent<WorldUiClickSound>() ?? node.AddComponent<WorldUiClickSound>();
                    listener.AkEvent = clip; listener.Post = postSound; listener.enabled = click.enabled;
                }
                else if (click.type == "ButtonSound")
                {
                    var listener = node.GetComponent<WorldButtonSound>() ?? node.AddComponent<WorldButtonSound>();
                    listener.ClickEvent = clip; listener.Post = postSound; listener.enabled = click.enabled;
                }
                else throw new InvalidDataException("Unsupported original private click sound component: " + click.type);
            }
        }

        private static void RestoreMatching(GameObject root, MatchingBinding binding, Action<AudioClip, GameObject> postSound)
        {
            if (binding?.clip == null || binding.sound?.events == null || binding.sound.events.Length != 1
                || binding.sound.events[0].id != 1386996041 || binding.sound.events[0].name != "TeamMatch_Waiting_1" || postSound == null)
                throw new InvalidDataException("Original private matching animation/event binding is incomplete.");
            Transform host = Find(root.transform, binding.path, binding.objects);
            foreach (MatchingCanvasBinding source in binding.canvasGroups)
            {
                GameObject node = Find(host, source.path, source.objects).gameObject;
                var group = node.GetComponent<CanvasGroup>() ?? node.AddComponent<CanvasGroup>();
                group.alpha = source.alpha; group.interactable = source.interactable;
                group.blocksRaycasts = source.blocksRaycasts; group.ignoreParentGroups = source.ignoreParentGroups;
                group.enabled = source.enabled;
            }
            foreach (MatchingTargetBinding source in binding.targets)
            {
                Transform node = string.IsNullOrEmpty(source.path) ? host : host.Find(source.path);
                Type type;
                switch (source.type)
                {
                    case "Transform": type = typeof(Transform); break;
                    case "RectTransform": type = typeof(RectTransform); break;
                    case "CanvasGroup": type = typeof(CanvasGroup); break;
                    case "Image": type = typeof(Image); break;
                    case "TextMeshProUGUI": type = typeof(TextMeshProUGUI); break;
                    default: throw new InvalidDataException("Unsupported original matching target: " + source.type);
                }
                if (node == null || node.GetComponent(type) == null)
                    throw new InvalidDataException("Original matching animation target is missing: " + source.path + " / " + source.type);
            }
            AnimationClip clip = Resources.Load<AnimationClip>(binding.clip.resource);
            if (clip == null || clip.name != binding.clip.name || !clip.legacy
                || !Mathf.Approximately(clip.length, binding.clip.duration) || !Mathf.Approximately(clip.frameRate, binding.clip.sampleRate))
                throw new InvalidDataException("Original matching Legacy clip is missing or mismatched.");
            AnimationEvent[] events = clip.events;
            if (events.Length != 1 || events[0].functionName != "PostEvent" || events[0].intParameter != 0
                || !Mathf.Approximately(events[0].time, 5f / 60f))
                throw new InvalidDataException("Original matching animation event is missing or mismatched.");
            var receiver = host.GetComponent<WorldAnimationSoundPlayer>() ?? host.gameObject.AddComponent<WorldAnimationSoundPlayer>();
            receiver.mEvents = new List<AudioClip>();
            foreach (PopupSoundEventBinding source in binding.sound.events)
            {
                AudioClip audio = Resources.Load<AudioClip>(source.resource);
                if (audio == null || audio.name != source.name)
                    throw new InvalidDataException("Original matching sound is missing: " + source.resource);
                receiver.mEvents.Add(audio);
            }
            receiver.Post = postSound; receiver.enabled = binding.sound.enabled;
            var player = host.GetComponent<Animation>() ?? host.gameObject.AddComponent<Animation>();
            player.RemoveClip(clip.name); player.AddClip(clip, clip.name); player.clip = clip;
            player.wrapMode = (WrapMode)binding.wrapMode; player.playAutomatically = binding.playAutomatically;
            player.animatePhysics = binding.animatePhysics; player.cullingType = (AnimationCullingType)binding.cullingType;
            player.enabled = binding.enabled;
        }

        public static void Apply(GameObject root, string prefabName, Action<AudioClip, GameObject> postSound = null)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI", "official-bindings.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official dungeon UI bindings are missing.", path);
            Document document = LoadDocument();
            Prefab binding = document?.manifest == "871_156598"
                ? document.prefabs?.FirstOrDefault(value => value.name == prefabName) : null;
            if (binding?.transforms == null || binding.images == null || binding.texts == null)
                throw new InvalidDataException("Official dungeon UI bindings are incomplete: " + prefabName);

            RestorePopup(root, binding.popup, postSound);
            foreach (TransformBinding item in binding.transforms)
            {
                Transform node = Find(root.transform, item.path, item.objects);
                node.localRotation = ReadQuaternion(item.localRotation);
                node.localScale = ReadVector3(item.localScale, UnityEngine.Vector3.one);
                if (item.isRectTransform)
                {
                    RectTransform rect = node as RectTransform;
                    if (rect == null) throw new InvalidDataException("Dungeon UI RectTransform binding mismatch: " + item.path);
                    rect.anchorMin = ReadVector2(item.anchorMin, rect.anchorMin);
                    rect.anchorMax = ReadVector2(item.anchorMax, rect.anchorMax);
                    rect.pivot = ReadVector2(item.pivot, rect.pivot);
                    rect.sizeDelta = ReadVector2(item.sizeDelta, rect.sizeDelta);
                    rect.anchoredPosition = ReadVector2(item.anchoredPosition, rect.anchoredPosition);
                }
                else node.localPosition = ReadVector3(item.localPosition, node.localPosition);
            }
            foreach (SafeAreaBinding item in binding.safeAreas ?? Array.Empty<SafeAreaBinding>())
            {
                GameObject node = Find(root.transform, item.path, item.objects).gameObject;
                WorldSafeArea safe = node.GetComponent<WorldSafeArea>() ?? node.AddComponent<WorldSafeArea>();
                safe.PaddingTop = item.paddingTop;
                safe.PaddingBottom = item.paddingBottom;
                safe.DisableTopAdapt = item.disableTopAdapt;
                safe.DisableBottomAdapt = item.disableBottomAdapt;
                safe.enabled = item.enabled;
            }
            RestoreLayouts(root, binding);
            foreach (ImageBinding item in binding.images)
            {
                Image image = Find(root.transform, item.path, item.objects).GetComponent<Image>();
                if (image == null) throw new InvalidDataException("Dungeon UI Image binding mismatch: " + item.path);
                image.enabled = item.enabled;
                image.color = ReadColor(item.color, image.color);
                image.type = (Image.Type)item.type;
                image.preserveAspect = item.preserveAspect;
                image.raycastTarget = item.raycastTarget;
                image.maskable = item.maskable;
                image.sprite = item.sprite == null ? null : LoadSprite(item.sprite);
            }
            RestoreButtons(root, binding);
            foreach (TextBinding item in binding.texts)
            {
                TMP_Text text = Find(root.transform, item.path, item.objects).GetComponent<TMP_Text>();
                if (text == null) throw new InvalidDataException("Dungeon UI text binding mismatch: " + item.path);
                text.text = item.text ?? string.Empty;
                text.fontSize = item.fontSize;
                text.fontWeight = (FontWeight)item.fontWeight;
                text.fontStyle = (FontStyles)item.fontStyle;
                text.enableAutoSizing = item.autoSizing;
                text.fontSizeMin = item.fontSizeRange != null && item.fontSizeRange.Length > 0 ? item.fontSizeRange[0] : item.fontSize;
                text.fontSizeMax = item.fontSizeRange != null && item.fontSizeRange.Length > 1 ? item.fontSizeRange[1] : item.fontSize;
                text.alignment = (TextAlignmentOptions)item.alignment;
                text.enableWordWrapping = item.wordWrap;
                text.overflowMode = (TextOverflowModes)item.overflow;
                if (item.margin != null && item.margin.Length == 4)
                    text.margin = new Vector4(item.margin[0], item.margin[1], item.margin[2], item.margin[3]);
                text.color = ReadColor(item.color, text.color);
            }
            foreach (LocalizationBinding item in binding.localizations ?? Array.Empty<LocalizationBinding>())
            {
                GameObject node = Find(root.transform, item.path, item.objects).gameObject;
                WorldMultiLanguageText text = node.GetComponent<WorldMultiLanguageText>() ?? node.AddComponent<WorldMultiLanguageText>();
                text.Localizer = RouteText;
                text.myKey = item.key;
                text.enabled = item.enabled;
                if (item.enabled) text.Refresh();
            }
            if (binding.popup != null) RestoreMatching(root, binding.popup.matching, postSound);
        }

        private static Document LoadDocument()
        {
            if (cachedDocument != null) return cachedDocument;
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI", "official-bindings.json");
            Document document = JsonUtility.FromJson<Document>(File.ReadAllText(path));
            if (document?.manifest != "871_156598") throw new InvalidDataException("Official dungeon binding version mismatch.");
            cachedDocument = document;
            return document;
        }

        private static Color ButtonColor(float[] values, int offset) =>
            new Color(values[offset], values[offset + 1], values[offset + 2], values[offset + 3]);

        private static void RestoreButtons(GameObject root, Prefab binding)
        {
            foreach (ButtonBinding item in binding.buttons ?? Array.Empty<ButtonBinding>())
            {
                GameObject node = Find(root.transform, item.path, item.objects).gameObject;
                var button = node.GetComponent<Button>() ?? node.AddComponent<Button>();
                button.navigation = new Navigation { mode = (Navigation.Mode)item.navigation, wrapAround = item.wrapAround };
                button.transition = (Selectable.Transition)item.transition;
                button.colors = new ColorBlock {
                    normalColor = ButtonColor(item.colors, 0), highlightedColor = ButtonColor(item.colors, 4),
                    pressedColor = ButtonColor(item.colors, 8), selectedColor = ButtonColor(item.colors, 12),
                    disabledColor = ButtonColor(item.colors, 16), colorMultiplier = item.colors[20], fadeDuration = item.colors[21] };
                button.animationTriggers = new AnimationTriggers { normalTrigger = item.triggers[0], highlightedTrigger = item.triggers[1],
                    pressedTrigger = item.triggers[2], selectedTrigger = item.triggers[3], disabledTrigger = item.triggers[4] };
                button.interactable = item.interactable;
                button.targetGraphic = item.target == null ? null : Find(root.transform, item.target, null).GetComponent<Graphic>();
                button.enabled = item.enabled;
            }
        }

        private static void RestoreLayouts(GameObject root, Prefab binding)
        {
            foreach (LayoutBinding item in binding.layouts ?? Array.Empty<LayoutBinding>())
            {
                GameObject node = Find(root.transform, item.path, item.objects).gameObject;
                if (item.type == "GridLayoutGroup")
                {
                    var grid = node.GetComponent<GridLayoutGroup>() ?? node.AddComponent<GridLayoutGroup>();
                    grid.padding = new RectOffset((int)item.padding[0], (int)item.padding[1], (int)item.padding[2], (int)item.padding[3]);
                    grid.childAlignment = (TextAnchor)item.alignment;
                    grid.startCorner = (GridLayoutGroup.Corner)item.startCorner;
                    grid.startAxis = (GridLayoutGroup.Axis)item.startAxis;
                    grid.cellSize = ReadVector2(item.cellSize, grid.cellSize);
                    grid.spacing = ReadVector2(item.spacing, grid.spacing);
                    grid.constraint = (GridLayoutGroup.Constraint)item.constraint;
                    grid.constraintCount = item.constraintCount;
                    grid.enabled = item.enabled;
                }
                else if (item.type == "VerticalLayoutGroup" || item.type == "HorizontalLayoutGroup")
                {
                    HorizontalOrVerticalLayoutGroup group = item.type == "VerticalLayoutGroup"
                        ? (HorizontalOrVerticalLayoutGroup)(node.GetComponent<VerticalLayoutGroup>() ?? node.AddComponent<VerticalLayoutGroup>())
                        : node.GetComponent<HorizontalLayoutGroup>() ?? node.AddComponent<HorizontalLayoutGroup>();
                    group.padding = new RectOffset((int)item.padding[0], (int)item.padding[1], (int)item.padding[2], (int)item.padding[3]);
                    group.childAlignment = (TextAnchor)item.alignment;
                    group.spacing = item.layoutSpacing;
                    group.childForceExpandWidth = item.flags[0] != 0; group.childForceExpandHeight = item.flags[1] != 0;
                    group.childControlWidth = item.flags[2] != 0; group.childControlHeight = item.flags[3] != 0;
                    group.childScaleWidth = item.flags[4] != 0; group.childScaleHeight = item.flags[5] != 0;
                    group.reverseArrangement = item.flags[6] != 0;
                    group.enabled = item.enabled;
                }
                else if (item.type == "HorizontalLayoutGroupJSZX")
                {
                    var group = node.GetComponent<UGUIJszx.HorizontalLayoutGroupJSZX>() ?? node.AddComponent<UGUIJszx.HorizontalLayoutGroupJSZX>();
                    group.padding = new RectOffset((int)item.padding[0], (int)item.padding[1], (int)item.padding[2], (int)item.padding[3]);
                    group.childAlignment = (TextAnchor)item.alignment;
                    group.spacing = item.layoutSpacing; group.spacingList = new List<float>(item.spacingList);
                    group.childForceExpandWidth = item.flags[0] != 0; group.childForceExpandHeight = item.flags[1] != 0;
                    group.childControlWidth = item.flags[2] != 0; group.childControlHeight = item.flags[3] != 0;
                    group.childScaleWidth = item.flags[4] != 0; group.childScaleHeight = item.flags[5] != 0;
                    group.reverseArrangement = item.flags[6] != 0;
                    group.enabled = item.enabled;
                }
                else if (item.type == "LayoutElement")
                {
                    var element = node.GetComponent<LayoutElement>() ?? node.AddComponent<LayoutElement>();
                    element.ignoreLayout = item.element.ignore;
                    element.minWidth = item.element.minimum[0]; element.minHeight = item.element.minimum[1];
                    element.preferredWidth = item.element.preferred[0]; element.preferredHeight = item.element.preferred[1];
                    element.flexibleWidth = item.element.flexible[0]; element.flexibleHeight = item.element.flexible[1];
                    element.layoutPriority = item.element.priority;
                    element.enabled = item.enabled;
                }
                else if (item.type == "LayoutElementClamper")
                {
                    if (item.clamp == null) throw new InvalidDataException("Original dungeon layout clamp fields are missing.");
                    var clamper = node.GetComponent<WorldLayoutElementClamper>() ?? node.AddComponent<WorldLayoutElementClamper>();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(item.clamp), clamper);
                    clamper.enabled = item.enabled;
                }
                else if (item.type == "ContentSizeFitter")
                {
                    var fitter = node.GetComponent<ContentSizeFitter>() ?? node.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = (ContentSizeFitter.FitMode)item.fit[0];
                    fitter.verticalFit = (ContentSizeFitter.FitMode)item.fit[1];
                    fitter.enabled = item.enabled;
                }
                else if (item.type == "RectMask2D")
                {
                    var mask = node.GetComponent<RectMask2D>() ?? node.AddComponent<RectMask2D>();
                    mask.padding = ReadVector4(item.padding, mask.padding);
                    mask.softness = new Vector2Int(item.softness[0], item.softness[1]);
                    mask.enabled = item.enabled;
                }
                else throw new InvalidDataException("Unbound official dungeon layout: " + item.type);
            }
            foreach (ScrollBinding item in binding.scrolls ?? Array.Empty<ScrollBinding>())
            {
                GameObject node = Find(root.transform, item.path, item.objects).gameObject;
                if (item.type == "ScrollRect")
                {
                    var normalScroll = node.GetComponent<ScrollRect>() ?? node.AddComponent<ScrollRect>();
                    normalScroll.content = (RectTransform)Find(root.transform, item.content, null);
                    normalScroll.viewport = (RectTransform)Find(root.transform, item.viewport, null);
                    normalScroll.horizontal = item.horizontal; normalScroll.vertical = item.vertical;
                    normalScroll.movementType = (ScrollRect.MovementType)item.movementType;
                    normalScroll.elasticity = item.elasticity; normalScroll.inertia = item.inertia;
                    normalScroll.decelerationRate = item.decelerationRate; normalScroll.scrollSensitivity = item.scrollSensitivity;
                    normalScroll.horizontalScrollbar = null; normalScroll.verticalScrollbar = null;
                    normalScroll.horizontalScrollbarVisibility = (ScrollRect.ScrollbarVisibility)item.horizontalScrollbarVisibility;
                    normalScroll.verticalScrollbarVisibility = (ScrollRect.ScrollbarVisibility)item.verticalScrollbarVisibility;
                    normalScroll.horizontalScrollbarSpacing = item.horizontalScrollbarSpacing;
                    normalScroll.verticalScrollbarSpacing = item.verticalScrollbarSpacing;
                    normalScroll.enabled = item.enabled;
                    continue;
                }
                if (item.type != "LoopVerticalScrollRect")
                    throw new InvalidDataException("Unbound original dungeon scroll: " + item.type);
                var scroll = node.GetComponent<LoopVerticalScrollRect>() ?? node.AddComponent<LoopVerticalScrollRect>();
                scroll.content = (RectTransform)Find(root.transform, item.content, null);
                scroll.viewport = (RectTransform)Find(root.transform, item.viewport, null);
                scroll.totalCount = item.totalCount;
                scroll.reverseDirection = item.reverseDirection;
                scroll.reverseIndex = item.reverseIndex;
                scroll.horizontal = item.horizontal;
                scroll.vertical = item.vertical;
                scroll.movementType = (LoopScrollRectBase.MovementType)item.movementType;
                scroll.elasticity = item.elasticity;
                scroll.inertia = item.inertia;
                scroll.decelerationRate = item.decelerationRate;
                scroll.scrollSensitivity = item.scrollSensitivity;
                var prefab = Resources.Load<GameObject>("Dungeons/UI/" + item.prefab);
                if (prefab == null) throw new InvalidDataException("Official dungeon scroll prefab is missing: " + item.prefab);
                scroll.DefaultPrefabSource = new LoopScrollSinglePrefabSource { Prefab = prefab };
                scroll.enabled = item.enabled;
            }
            foreach (GradeGridBinding item in binding.gradeGrids ?? Array.Empty<GradeGridBinding>())
            {
                GameObject node = Find(root.transform, item.path, null).gameObject;
                var grid = node.GetComponent<WorldDailyDungeonBigMapDialogGradeGrid>() ?? node.AddComponent<WorldDailyDungeonBigMapDialogGradeGrid>();
                grid.Initialize(Find(root.transform, item.scroll, null).GetComponent<LoopVerticalScrollRect>(),
                    (RectTransform)Find(root.transform, item.rect, null));
            }
        }

        private static string RouteText(string key)
        {
            TextValueBinding text = LoadDocument().routeStyles.texts.SingleOrDefault(value => value.key == key);
            if (text == null) throw new InvalidDataException("Unbound original dungeon language key: " + key);
            return text.value;
        }

        public static void RefreshPrivateDungeonDetails(GameObject root, int dungeonId, int difficulty,
            int minimumGrade, int maximumGrade)
        {
            Prefab binding = LoadDocument().prefabs.Single(value => value.name == "DailyDungeonDialogPrivateMap");
            PrivateDetailBinding fields = binding.privateDetail;
            PrivateUnlockBinding unlocked = binding.privateUnlock;
            PrivateDialogBinding dialog = binding.privateDialog;
            PrivateDisplayBinding display = LoadDocument().routeStyles.privateDisplay;
            DifficultyBinding difficultyName = display?.difficulties.SingleOrDefault(value => value.value == difficulty);
            if (fields?.isPrivate != true || display == null || display.dungeonId != dungeonId || difficultyName == null
                || display.privateDungeonId != 121 || display.privateDifficulty != 50 || unlocked == null)
                throw new InvalidDataException("Original Map12 private dungeon detail binding mismatch.");
            Find(root.transform, fields.title, null).GetComponent<WorldMultiLanguageText>()
                .SetKey("ui_daily_dungeon_item_title_without_difficulty_" + dungeonId);
            Find(root.transform, fields.description, null).GetComponent<WorldMultiLanguageText>()
                .SetKey("ui_daily_dungeon_dialog_desc_content_" + dungeonId + "_" + difficultyName.name);
            GradeStyleBinding minimum = LoadDocument().routeStyles.grades.Single(value => value.grade == minimumGrade);
            GradeStyleBinding maximum = LoadDocument().routeStyles.grades.Single(value => value.grade == maximumGrade);
            Find(root.transform, fields.gradeRange, null).GetComponent<WorldMultiLanguageText>()
                .Format(RouteText(minimum.key), RouteText(maximum.key));
            // DailyDungeonDetailContent.Refresh: mIsPrivateMap makes the content unlocked.
            SetObjectState(root, fields.lockStates, 0);
            // GetBtnShowState: the bound world stage is present and mIsPrivateMap selects Enter.
            SetObjectState(root, unlocked.buttonStates, unlocked.enterState);
            Find(root.transform, unlocked.rewardTitle, null).GetComponent<WorldMultiLanguageText>()
                .SetKey("ui_daily_dungeon_dialog_reward_title_without_difficulty");
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Find(root.transform, unlocked.rewardTitleLayout, null));
            if (dialog == null || display.background == null || display.icon == null)
                throw new InvalidDataException("Original private dungeon appearance binding is missing.");
            Find(root.transform, dialog.background, null).GetComponent<Image>().sprite = LoadSprite(display.background);
            Find(root.transform, dialog.icon, null).GetComponent<Image>().sprite = LoadSprite(display.icon);
            bool singleDifficulty = display.difficulties.Length == 1;
            SetObjectState(root, dialog.titleStates, singleDifficulty ? 1 : 0);
            if (singleDifficulty)
                Find(root.transform, dialog.title, null).GetComponent<WorldMultiLanguageText>().SetKey(display.difficulties[0].key);
        }

        private static void SetObjectState(GameObject root, ObjectStateBinding[] states, int state)
        {
            if (states == null) throw new InvalidDataException("Original dungeon object-state binding is missing.");
            var data = new WorldStateGameObjectData {
                GoConfig = states.Select(value => new WorldStateGameObjectData.StateGameObjectKV {
                    State = value.state,
                    GameObjects = value.objects.Select(path => Find(root.transform, path, null).gameObject).ToList()
                }).ToList()
            };
            WorldStateGameObjectConfig.FromBindings(data).SetState(state);
        }

        public static RouteWindowView ConfigureRouteWindow(GameObject root, bool choosing)
        {
            RouteWindowBinding binding = LoadDocument().prefabs.Single(value => value.name == "DailyDungeonBigMapDialog").routeWindow;
            if (binding?.chooseStates == null) throw new InvalidDataException("Official route window fields are missing.");
            SetObjectState(root, binding.chooseStates, choosing ? 1 : 0);
            Find(root.transform, binding.scroll, null).gameObject.SetActive(!choosing);
            // RefreshMember's no-active-faction branch: the offline route is outside a running fight.
            Find(root.transform, binding.team, null).gameObject.SetActive(false);
            Find(root.transform, binding.tip, null).gameObject.SetActive(true);
            return new RouteWindowView {
                Scroll = Find(root.transform, choosing ? binding.chooseScroll : binding.scroll, null).GetComponent<LoopVerticalScrollRect>(),
                Grid = Find(root.transform, choosing ? binding.chooseGrid : binding.grid, null).GetComponent<GridLayoutGroup>(),
                GradeSlot = Find(root.transform, choosing ? binding.chooseGradeSlot : binding.gradeSlot, null),
                GradePrefab = binding.gradePrefab };
        }

        public static void RefreshRouteGrades(GameObject root, RouteWindowView window,
            IEnumerable<KeyValuePair<int, int>> configuredGrades, int rowCount)
        {
            KeyValuePair<int, int>[] grades = configuredGrades.ToArray();
            foreach (Transform child in window.GradeSlot) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
            if (grades.Length == 0) return;
            RouteStyles styles = LoadDocument().routeStyles;
            Prefab binding = LoadDocument().prefabs.Single(value => value.name == window.GradePrefab);
            GradeItemBinding fields = binding.gradeItem;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/" + window.GradePrefab);
            if (fields == null || prefab == null) throw new InvalidDataException("Official route grade item is missing.");
            int minimum = grades.Min(value => value.Key), maximum = grades.Max(value => value.Key);
            foreach (var grade in grades)
            {
                GradeStyleBinding style = styles.grades.FirstOrDefault(value => value.grade == grade.Key);
                if (style == null) continue;
                GameObject item = UnityEngine.Object.Instantiate(prefab, window.GradeSlot, false);
                Apply(item, window.GradePrefab);
                var view = item.AddComponent<WorldDailyDungeonGradeItem>();
                view.Image = Find(item.transform, fields.image, null).GetComponent<Image>();
                view.FillImage = Find(item.transform, fields.fill, null).GetComponent<Image>();
                GameObject text = Find(item.transform, fields.text, null).gameObject;
                view.GradeText = text.GetComponent<WorldMultiLanguageText>() ?? text.AddComponent<WorldMultiLanguageText>();
                view.GradeText.Localizer = key => styles.texts.Single(value => value.key == key).value;
                view.Init(ReadColor(style.color, Color.white), style.key, grade.Key != minimum);
                int rows = grade.Key == maximum ? rowCount - grades.Single(value => value.Key == maximum).Value + 1
                    : grades.Single(value => value.Key == Math.Min(maximum, grade.Key + 1)).Value - grade.Value;
                RectTransform rect = (RectTransform)item.transform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, rows * (window.Grid.spacing.y + window.Grid.cellSize.y));
            }
            foreach (var grid in root.GetComponentsInChildren<WorldDailyDungeonBigMapDialogGradeGrid>()) grid.Refresh();
        }

        public static void BindRouteStyles(WorldDailyDungeonRoute route)
        {
            RouteStyles styles = LoadDocument().routeStyles;
            if (styles?.rooms == null || styles.roads == null)
                throw new InvalidDataException("Official daily dungeon route styles are missing.");
            foreach (RoomStyleBinding item in styles.rooms)
                route.Styles.Add(item.type, new WorldDailyDungeonRoute.RoomStyle {
                    Type = item.type, PassIcon = LoadSprite(item.passIcon), NoPassIcon = LoadSprite(item.noPassIcon),
                    PassFrame = LoadSprite(item.passFrame), NoPassFrame = LoadSprite(item.noPassFrame),
                    Select = LoadSprite(item.select), Reward = item.reward == null ? null : LoadSprite(item.reward) });
            foreach (RoadStyleBinding item in styles.roads)
                route.RoadColors.Add(item.status, ReadColor(item.color, Color.white));
        }

        public static WorldDailyDungeonRoomItem BindRouteRoom(GameObject root)
        {
            WorldDailyDungeonRoomItem existing = root.GetComponent<WorldDailyDungeonRoomItem>();
            if (existing != null) return existing;
            const string prefabName = "DailyDungeonBigMapRoomItem";
            root.name = prefabName;
            Apply(root, prefabName);
            Prefab binding = LoadDocument().prefabs.Single(value => value.name == prefabName);
            RoomViewBinding fields = binding.roomView;
            if (fields?.roads == null) throw new InvalidDataException("Official dungeon room fields are missing.");
            var view = root.AddComponent<WorldDailyDungeonRoomItem>();
            view.Animation = fields.animation == null ? null : Find(root.transform, fields.animation, null).GetComponent<Animation>();
            view.VoteAnimation = fields.voteAnimation == null ? null : Find(root.transform, fields.voteAnimation, null).GetComponent<Animation>();
            view.ShowContentObj = Find(root.transform, fields.content, null).gameObject;
            view.IconImage = Find(root.transform, fields.icon, null).GetComponent<Image>();
            view.FrameImage = Find(root.transform, fields.frame, null).GetComponent<Image>();
            view.SelectImage = Find(root.transform, fields.select, null).GetComponent<Image>();
            view.RewardIconImage = Find(root.transform, fields.reward, null).GetComponent<Image>();
            view.RoomRoadDatas = fields.roads.Select(road => new WorldDailyDungeonRoomItem.Road {
                Object = Find(root.transform, road.path, null).gameObject,
                Image = Find(root.transform, road.image, null).GetComponent<Image>(), OffsetType = road.offsetType,
                Animation = road.animation == null ? null : Find(root.transform, road.animation, null).GetComponent<Animation>()
            }).ToArray();
            GameObject button = Find(root.transform, binding.roomButton, null).gameObject;
            view.ClickBtn = button.GetComponent<Button>() ?? button.AddComponent<Button>();
            foreach (string playerPath in fields.selectedPlayers ?? Array.Empty<string>())
                Find(root.transform, playerPath, null).gameObject.SetActive(false);
            return view;
        }

        private static Transform Find(Transform root, string path, string[] names)
        {
            Transform node = root;
            if (!string.IsNullOrEmpty(path))
                foreach (string part in path.Split('/')) node = node.GetChild(int.Parse(part));
            else if (names != null && names.Length == 1 && node.name == names[0] + "(Clone)")
                node.name = names[0];
            if (names == null) return node;
            if (names.Length == 0 || node.name != names[names.Length - 1])
                throw new InvalidDataException("Official dungeon UI hierarchy changed at " + path);
            if (names.Length > 1)
            {
                Transform parent = node;
                for (int i = names.Length - 2; i >= 0; i--)
                {
                    parent = parent.parent;
                    if (parent == null || parent.name != names[i])
                        throw new InvalidDataException("Official dungeon UI parent hierarchy changed at " + path);
                }
            }
            return node;
        }

        private static Sprite LoadSprite(SpriteBinding binding)
        {
            if (string.IsNullOrWhiteSpace(binding.file) || binding.file.Contains("..") || Path.IsPathRooted(binding.file))
                throw new InvalidDataException("Official dungeon UI Sprite path is invalid.");
            string key = binding.file + ":" + binding.sha256;
            if (loadedSprites.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI",
                binding.file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Official dungeon UI Sprite is missing.", path);
            byte[] bytes = File.ReadAllBytes(path);
            if (!string.Equals(Hash(bytes), binding.sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Official dungeon UI Sprite hash mismatch: " + binding.file);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, true)) throw new InvalidDataException("Official dungeon UI Sprite decode failed.");
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                ReadVector2(binding.pivot, new UnityEngine.Vector2(0.5f, 0.5f)), Mathf.Max(0.01f, binding.pixelsPerUnit),
                0, SpriteMeshType.FullRect, ReadVector4(binding.border, UnityEngine.Vector4.zero));
            sprite.name = binding.name;
            loadedSprites[key] = sprite;
            return sprite;
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static Vector2 ReadVector2(float[] value, Vector2 fallback) => value != null && value.Length >= 2
            ? new Vector2(value[0], value[1]) : fallback;
        private static Vector3 ReadVector3(float[] value, Vector3 fallback) => value != null && value.Length >= 3
            ? new Vector3(value[0], value[1], value[2]) : fallback;
        private static Vector4 ReadVector4(float[] value, Vector4 fallback) => value != null && value.Length >= 4
            ? new Vector4(value[0], value[1], value[2], value[3]) : fallback;
        private static Color ReadColor(float[] value, Color fallback) => value != null && value.Length >= 4
            ? new Color(value[0], value[1], value[2], value[3]) : fallback;
        private static Quaternion ReadQuaternion(float[] value) => value != null && value.Length >= 4
            ? new Quaternion(value[0], value[1], value[2], value[3]) : Quaternion.identity;
    }
}
