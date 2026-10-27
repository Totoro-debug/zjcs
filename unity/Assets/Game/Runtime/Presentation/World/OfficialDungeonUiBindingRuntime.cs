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
        { public string name; public TransformBinding[] transforms; public SafeAreaBinding[] safeAreas; public LayoutBinding[] layouts; public ScrollBinding[] scrolls; public ButtonBinding[] buttons;
          public RoomViewBinding roomView; public RouteWindowBinding routeWindow; public GradeItemBinding gradeItem; public PrivateDetailBinding privateDetail; public PrivateDialogBinding privateDialog;
          public LocalizationBinding[] localizations;
          public GradeGridBinding[] gradeGrids; public string roomButton; public ImageBinding[] images; public TextBinding[] texts; }
        [Serializable] private sealed class LayoutBinding
        {
            public string path, type; public string[] objects; public bool enabled;
            public float[] padding, cellSize, spacing; public int[] fit, softness;
            public int alignment, startCorner, startAxis, constraint, constraintCount;
            public float layoutSpacing; public float[] spacingList; public int[] flags; public LayoutElementBinding element;
        }
        [Serializable] private sealed class PrivateDisplayBinding
        { public int dungeonId, privateDungeonId, privateDifficulty; public DifficultyBinding[] difficulties; public SpriteBinding background, icon; }
        [Serializable] private sealed class DifficultyBinding { public int value; public string name; }
        public static int PrivateDisplayDungeonId => LoadDocument().routeStyles.privateDisplay.dungeonId;
        [Serializable] private sealed class LocalizationBinding
        { public string path, key; public string[] objects; public bool enabled; }
        [Serializable] private sealed class PrivateDialogBinding
        { public string background, icon; }
        [Serializable] private sealed class PrivateDetailBinding
        { public bool isPrivate; public string title, description, gradeRange; }
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
            public string path, content, viewport, prefab; public string[] objects;
            public bool enabled, reverseDirection, reverseIndex, horizontal, vertical, inertia;
            public int totalCount, movementType; public float elasticity, decelerationRate, scrollSensitivity;
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

        public static void Apply(GameObject root, string prefabName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI", "official-bindings.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official dungeon UI bindings are missing.", path);
            Document document = LoadDocument();
            Prefab binding = document?.manifest == "871_156598"
                ? document.prefabs?.FirstOrDefault(value => value.name == prefabName) : null;
            if (binding?.transforms == null || binding.images == null || binding.texts == null)
                throw new InvalidDataException("Official dungeon UI bindings are incomplete: " + prefabName);

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
                else if (item.type == "VerticalLayoutGroup")
                {
                    var group = node.GetComponent<VerticalLayoutGroup>() ?? node.AddComponent<VerticalLayoutGroup>();
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
            PrivateDetailBinding fields = LoadDocument().prefabs.Single(value => value.name == "DailyDungeonDialogPrivateMap").privateDetail;
            PrivateDisplayBinding display = LoadDocument().routeStyles.privateDisplay;
            DifficultyBinding difficultyName = display?.difficulties.SingleOrDefault(value => value.value == difficulty);
            if (fields?.isPrivate != true || display == null || display.dungeonId != dungeonId || difficultyName == null
                || display.privateDungeonId != 121 || display.privateDifficulty != 50)
                throw new InvalidDataException("Original Map12 private dungeon detail binding mismatch.");
            PrivateDialogBinding dialog = LoadDocument().prefabs.Single(value => value.name == "DailyDungeonDialogPrivateMap").privateDialog;
            if (dialog == null || display.background == null || display.icon == null)
                throw new InvalidDataException("Original private dungeon appearance binding is missing.");
            Find(root.transform, dialog.background, null).GetComponent<Image>().sprite = LoadSprite(display.background);
            Find(root.transform, dialog.icon, null).GetComponent<Image>().sprite = LoadSprite(display.icon);
            Find(root.transform, fields.title, null).GetComponent<WorldMultiLanguageText>()
                .SetKey("ui_daily_dungeon_item_title_without_difficulty_" + dungeonId);
            Find(root.transform, fields.description, null).GetComponent<WorldMultiLanguageText>()
                .SetKey("ui_daily_dungeon_dialog_desc_content_" + dungeonId + "_" + difficultyName.name);
            GradeStyleBinding minimum = LoadDocument().routeStyles.grades.Single(value => value.grade == minimumGrade);
            GradeStyleBinding maximum = LoadDocument().routeStyles.grades.Single(value => value.grade == maximumGrade);
            Find(root.transform, fields.gradeRange, null).GetComponent<WorldMultiLanguageText>()
                .Format(RouteText(minimum.key), RouteText(maximum.key));
        }

        public static RouteWindowView ConfigureRouteWindow(GameObject root, bool choosing)
        {
            RouteWindowBinding binding = LoadDocument().prefabs.Single(value => value.name == "DailyDungeonBigMapDialog").routeWindow;
            if (binding?.chooseStates == null) throw new InvalidDataException("Official route window fields are missing.");
            int state = choosing ? 1 : 0;
            string[] selected = binding.chooseStates.Where(value => value.state == state).SelectMany(value => value.objects).ToArray();
            foreach (ObjectStateBinding value in binding.chooseStates)
                if (value.state != state)
                    foreach (string path in value.objects)
                        if (!selected.Contains(path)) Find(root.transform, path, null).gameObject.SetActive(false);
            foreach (string path in selected) Find(root.transform, path, null).gameObject.SetActive(true);
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
            if (names == null || names.Length == 0 || node.name != names[names.Length - 1])
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
