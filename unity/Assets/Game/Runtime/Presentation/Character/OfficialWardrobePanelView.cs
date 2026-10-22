using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    [DisallowMultipleComponent]
    public sealed class OfficialWardrobePanelView : MonoBehaviour
    {
        private sealed class WardrobeEntry
        {
            public int ItemId;
            public int ItemType;
            public bool IsTitle;
        }

        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> nodeActive = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<OfficialCharacterPanelNode> sourceOrder = new List<OfficialCharacterPanelNode>();
        private readonly List<GameObject> pooledCards = new List<GameObject>();
        private Dictionary<string, object> templates;
        private Dictionary<string, object> texts;
        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private CharacterModelPreview model;
        private Action close;
        private Action<string> requestModule;
        private RectTransform itemScrollRoot;
        private RectTransform itemViewport;
        private RectTransform itemContent;
        private ScrollRect itemScroll;
        private Toggle showAllToggle;
        private List<WardrobeEntry> visibleEntries = new List<WardrobeEntry>();
        private int itemType = 319;
        private int columns = 4;
        private float cardWidth = 158f;
        private float cardHeight = 158f;
        private float cardSpacing = 8f;
        private bool honorMode;
        private bool poolHonorMode;
        private bool poolInitialized;
        private long lastTimeRefreshUnixSeconds = -1;
        private Text statusText;
        private GameObject activeDialog;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression, CharacterModelPreview characterModel,
            Action closeCallback, Action<string> moduleCallback = null)
        {
            assets = assetCatalog != null ? assetCatalog : throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression != null ? characterProgression
                : throw new ArgumentNullException(nameof(characterProgression));
            model = characterModel;
            close = closeCallback;
            requestModule = moduleCallback;
            assets.ValidateBindings();
            Dictionary<string, object> root = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.WardrobeUi.text), "original wardrobe UI");
            templates = CharacterModelPackage.AsObject(root["templates"], "original wardrobe templates");
            texts = CharacterModelPackage.AsObject(root["texts"], "original wardrobe text");

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var canvasRect = transform as RectTransform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            OfficialCharacterPanelNode wardrobeRoot = ReadNode(TemplateRoot("wardrobe"));
            BuildNode(wardrobeRoot, transform);
            BindControls(wardrobeRoot);
            InitializeGrid();
            string initialGroup = FindGroupPathForCategory(itemType);
            if (!string.IsNullOrEmpty(initialGroup)) SelectFirstTab(initialGroup);
            RefreshWardrobe();
        }

        private void Update()
        {
            if (progression == null) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now == lastTimeRefreshUnixSeconds) return;
            lastTimeRefreshUnixSeconds = now;
            if (progression.Player.Wardrobe.Expiries.Count == 0) return;
            if (progression.Player.Wardrobe.Expiries.Any(value => now > value.ExpiresAtUnixSeconds))
            {
                RefreshWardrobe();
                if (model != null) model.Refresh();
            }
            else
            {
                RefreshGrid();
            }
        }

        public bool HandleEscape()
        {
            if (activeDialog == null) return false;
            Destroy(activeDialog);
            activeDialog = null;
            return true;
        }

        private void BindControls(OfficialCharacterPanelNode root)
        {
            BindSuffix("CloseBtn_dark", () => close?.Invoke());
            BindSuffix("CloseBtn", () => close?.Invoke());

            foreach (OfficialCharacterPanelNode node in Walk(root).Where(value => HasScript(value, "AppendAppearanceFirstTabItem")))
            {
                if (!TryBinding(node, "AppendAppearanceFirstTabItem", out Dictionary<string, object> binding)
                    || !binding.TryGetValue("mTabGroupObj", out object groupRaw)) continue;
                string groupPath = Convert.ToString(groupRaw, CultureInfo.InvariantCulture);
                if (!buttons.TryGetValue(node.path, out Button button)) continue;
                button.onClick.AddListener(() => SelectFirstTab(groupPath));
            }

            foreach (OfficialCharacterPanelNode node in Walk(root).Where(value => HasScript(value, "AppendAppearanceSecondTabItem")))
            {
                if (!TryBinding(node, "AppendAppearanceSecondTabItem", out Dictionary<string, object> binding)
                    || !binding.TryGetValue("mItemType", out object typeRaw)
                    || !buttons.TryGetValue(node.path, out Button button)) continue;
                int type = Convert.ToInt32(typeRaw, CultureInfo.InvariantCulture);
                button.onClick.AddListener(() => SelectCategory(type));
            }

            RectTransform toggleRect = NodeEnding("/ShowAll/Toggle");
            if (toggleRect != null)
            {
                showAllToggle = toggleRect.GetComponent<Toggle>();
                if (showAllToggle == null) showAllToggle = toggleRect.gameObject.AddComponent<Toggle>();
                showAllToggle.isOn = false;
                showAllToggle.onValueChanged.AddListener(_ => RefreshGrid());
            }

            Button ruleButton = ButtonEnding("/RuleBtn") ?? ButtonTrimmed("RuleBtn");
            if (ruleButton != null) ruleButton.onClick.AddListener(() => ShowRuleDialog());
            Button propertiesButton = ButtonEnding("/PropBtn");
            if (propertiesButton != null) propertiesButton.onClick.AddListener(() => ShowPropertiesDialog());
            Button upgradeButton = ButtonEnding("/UpgradeBtn");
            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeAppearanceLevel);

            List<Button> bottomTabs = buttons.Where(pair => pair.Key.Contains("/Content/BottomGo/TabBottom/TabGroup/"))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList();
            for (int i = 0; i < bottomTabs.Count; i++)
            {
                int selected = i;
                bottomTabs[i].onClick.AddListener(() =>
                {
                    if (selected == 0) return;
                    requestModule?.Invoke(selected == 1 ? "skill-appearance" : "decoration-appearance");
                });
            }

            statusText = CreateStatusText();
        }

        private void SelectFirstTab(string groupPath)
        {
            foreach (KeyValuePair<string, RectTransform> pair in nodes)
            {
                if (!sourceNodes.TryGetValue(pair.Key, out OfficialCharacterPanelNode source)
                    || !TryBinding(source, "AppendAppearanceFirstTabItem", out Dictionary<string, object> binding)
                    || !binding.TryGetValue("mTabGroupObj", out object rawGroup)) continue;
                SetTabSelected(pair.Value, string.Equals(Convert.ToString(rawGroup, CultureInfo.InvariantCulture),
                    groupPath, StringComparison.Ordinal));
            }
            foreach (KeyValuePair<string, RectTransform> node in nodes)
            {
                if (node.Key.Contains("/SubTabGroupGo/"))
                {
                    bool selected = node.Key == groupPath || node.Key.StartsWith(groupPath + "/", StringComparison.Ordinal);
                    bool active = nodeActive.TryGetValue(node.Key, out bool defaultActive) && defaultActive;
                    node.Value.gameObject.SetActive(selected && (node.Key == groupPath || active));
                }
            }
            foreach (OfficialCharacterPanelNode source in sourceOrder.Where(value =>
                value.path.StartsWith(groupPath + "/TabGroup/", StringComparison.Ordinal)))
            {
                if (!nodes.TryGetValue(source.path, out RectTransform tab)) continue;
                Button button = tab.GetComponent<Button>();
                if (button == null) continue;
                int type = FindItemType(source.path);
                if (type > 0)
                {
                    SelectCategory(type);
                    break;
                }
            }
        }

        private int FindItemType(string path)
        {
            foreach (KeyValuePair<string, RectTransform> pair in nodes)
            {
                if (!string.Equals(pair.Key, path, StringComparison.Ordinal)) continue;
                OfficialCharacterPanelNode source = sourceNodes.TryGetValue(path, out OfficialCharacterPanelNode value) ? value : null;
                if (source != null && TryBinding(source, "AppendAppearanceSecondTabItem", out Dictionary<string, object> binding)
                    && binding.TryGetValue("mItemType", out object type))
                    return Convert.ToInt32(type, CultureInfo.InvariantCulture);
            }
            return 0;
        }

        private readonly Dictionary<string, OfficialCharacterPanelNode> sourceNodes = new Dictionary<string, OfficialCharacterPanelNode>(StringComparer.Ordinal);

        private void SelectCategory(int nextItemType)
        {
            if (nextItemType <= 0) return;
            itemType = nextItemType;
            foreach (KeyValuePair<string, RectTransform> pair in nodes)
            {
                if (!sourceNodes.TryGetValue(pair.Key, out OfficialCharacterPanelNode source)
                    || !TryBinding(source, "AppendAppearanceSecondTabItem", out Dictionary<string, object> binding)
                    || !binding.TryGetValue("mItemType", out object rawType)) continue;
                SetTabSelected(pair.Value,
                    Convert.ToInt32(rawType, CultureInfo.InvariantCulture) == itemType);
            }
            honorMode = itemType == 339;
            if (showAllToggle != null) showAllToggle.gameObject.SetActive(!honorMode);
            if (nodes.TryGetValue("/AppearancePanel/BgPanel1/Content/MiddleGo/AppendAppearanceContent/AppearanceListContent/GridContent", out RectTransform grid))
                grid.gameObject.SetActive(!honorMode);
            if (nodes.TryGetValue("/AppearancePanel/BgPanel1/Content/MiddleGo/AppendAppearanceContent/AppearanceListContent/HonorContent", out RectTransform honor))
                honor.gameObject.SetActive(honorMode);
            string scrollPath = honorMode ? "/HonorContent/HonorScrollRect" : "/GridContent/GridScrollRect";
            itemScrollRoot = NodeEnding(scrollPath);
            itemViewport = NodeEnding(scrollPath + "/Viewport");
            if (itemViewport != null) ConfigureScrollRect();
            RefreshGrid();
        }

        private void InitializeGrid()
        {
            RectTransform toggleRect = NodeEnding("/ShowAll/Toggle");
            if (toggleRect != null) showAllToggle = toggleRect.GetComponent<Toggle>();
            itemScrollRoot = NodeEnding("/GridContent/GridScrollRect");
            itemViewport = NodeEnding("/GridContent/GridScrollRect/Viewport");
            if (itemScrollRoot == null || itemViewport == null)
                throw new FormatException("Original wardrobe item ScrollRect or Viewport is missing.");
            ConfigureScrollRect();
            StartCoroutine(PrepareGridAfterLayout());
        }

        private string FindGroupPathForCategory(int targetItemType)
        {
            foreach (KeyValuePair<string, OfficialCharacterPanelNode> pair in sourceNodes)
            {
                if (!HasScript(pair.Value, "AppendAppearanceSecondTabItem")
                    || !TryBinding(pair.Value, "AppendAppearanceSecondTabItem", out Dictionary<string, object> binding)
                    || !binding.TryGetValue("mItemType", out object rawType)
                    || Convert.ToInt32(rawType, CultureInfo.InvariantCulture) != targetItemType) continue;
                int tabGroupIndex = pair.Key.LastIndexOf("/TabGroup/", StringComparison.Ordinal);
                return tabGroupIndex < 0 ? string.Empty : pair.Key.Substring(0, tabGroupIndex);
            }
            return string.Empty;
        }

        private static void SetTabSelected(RectTransform tab, bool selected)
        {
            RectTransform selectedNode = FindDirectChild(tab, "Selected");
            if (selectedNode != null) selectedNode.gameObject.SetActive(selected);
        }

        private IEnumerator PrepareGridAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ConfigurePool();
            UpdatePool();
        }

        private void ConfigureScrollRect()
        {
            if (itemScrollRoot == null || itemViewport == null) return;
            if (itemScroll != null) itemScroll.onValueChanged.RemoveListener(OnScrollChanged);
            itemScroll = itemScrollRoot.GetComponent<ScrollRect>();
            if (itemScroll == null) itemScroll = itemScrollRoot.gameObject.AddComponent<ScrollRect>();
            itemScroll.horizontal = false;
            itemScroll.vertical = true;
            itemScroll.movementType = ScrollRect.MovementType.Clamped;
            itemScroll.scrollSensitivity = 36f;
            RectMask2D viewportMask = itemViewport.GetComponent<RectMask2D>();
            if (viewportMask == null) itemViewport.gameObject.AddComponent<RectMask2D>();
            itemContent = FindDirectChild(itemViewport, "Grid");
            if (itemContent == null) throw new FormatException("Original wardrobe Viewport/Grid is missing.");
            LayoutGroup layoutGroup = itemContent.GetComponent<LayoutGroup>();
            if (layoutGroup != null) layoutGroup.enabled = false;
            ContentSizeFitter sizeFitter = itemContent.GetComponent<ContentSizeFitter>();
            if (sizeFitter != null) sizeFitter.enabled = false;
            itemContent.anchorMin = new Vector2(0f, 1f);
            itemContent.anchorMax = new Vector2(1f, 1f);
            itemContent.pivot = new Vector2(0.5f, 1f);
            itemContent.anchoredPosition = Vector2.zero;
            itemScroll.viewport = itemViewport;
            itemScroll.content = itemContent;
            itemScroll.onValueChanged.AddListener(OnScrollChanged);
        }

        private void ConfigurePool()
        {
            if (itemViewport == null || itemContent == null) return;
            honorMode = itemType == 339;
            if (poolInitialized && poolHonorMode != honorMode)
            {
                foreach (GameObject card in pooledCards) if (card != null) Destroy(card);
                pooledCards.Clear();
            }
            poolInitialized = true;
            poolHonorMode = honorMode;
            float width = itemViewport.rect.width;
            float height = itemViewport.rect.height;
            if (width < 100f) width = honorMode ? 650f : 640f;
            if (height < 100f) height = 520f;
            cardHeight = honorMode ? 172f : 158f;
            cardWidth = honorMode ? (width - 12f) * 0.5f : 158f;
            columns = honorMode ? 2 : Mathf.Clamp(Mathf.FloorToInt((width + cardSpacing) / (cardWidth + cardSpacing)), 1, 6);
            int rows = Mathf.CeilToInt(height / (cardHeight + cardSpacing)) + 2;
            int poolCount = columns * rows;
            while (pooledCards.Count < poolCount)
                pooledCards.Add(CreateCard(honorMode));
            while (pooledCards.Count > poolCount)
            {
                GameObject excess = pooledCards[pooledCards.Count - 1];
                pooledCards.RemoveAt(pooledCards.Count - 1);
                Destroy(excess);
            }
            for (int i = 0; i < pooledCards.Count; i++)
            {
                RectTransform rect = pooledCards[i].GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            }
            itemScroll.onValueChanged.RemoveListener(OnScrollChanged);
            itemScroll.onValueChanged.AddListener(OnScrollChanged);
        }

        private GameObject CreateCard(bool title)
        {
            string templateName = title ? "wardrobe_honor" : "wardrobe_item";
            Dictionary<string, object> template = CharacterModelPackage.AsObject(templates[templateName], templateName);
            Dictionary<string, object> root = CharacterModelPackage.AsObject(template["root"], templateName + " root");
            if (title)
            {
                List<object> children = CharacterModelPackage.AsArray(root["children"], "honor template children");
                root = CharacterModelPackage.AsObject(children.First(), "honor item template");
            }
            OfficialCharacterPanelNode data = ReadNode(root);
            RectTransform card = BuildNode(data, itemContent);
            card.gameObject.name = title ? "OfficialWardrobeHonorCard" : "OfficialWardrobeItemCard";
            Button button = card.GetComponent<Button>();
            if (button == null)
            {
                Graphic target = card.GetComponent<Graphic>();
                if (target == null) target = card.gameObject.AddComponent<Image>();
                target.raycastTarget = true;
                button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = target;
            }
            button.onClick.RemoveAllListeners();
            return card.gameObject;
        }

        private void RefreshWardrobe()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (progression.Player.Wardrobe.Expiries.Any(value => now > value.ExpiresAtUnixSeconds))
            {
                ProgressionResult result = progression.SynchronizeExpiredItems(now,
                    "wardrobe-ui-expiry-" + now.ToString(CultureInfo.InvariantCulture));
                if (result.Status == ProgressionResultStatus.InvalidState)
                    ShowStatus(result.Message ?? "限时外观状态无法同步。");
            }
            RefreshFashionHeader();
            RefreshGrid();
        }

        private void RefreshFashionHeader()
        {
            int level = progression.Player.Wardrobe.AppearanceLevel;
            Text levelText = FindTextByPath("/AppearanceLevelcontent/ExpSlider/Info/EvaluationtTitleText/EvaluationText");
            SetText(levelText, level.ToString(CultureInfo.InvariantCulture));
            Text expText = FindTextByPath("/AppearanceLevelcontent/ExpSlider/Info/ExpText");
            if (expText != null)
            {
                if (progression.IsMaxAppearanceLevel)
                {
                    expText.text = "已满级";
                }
                else
                {
                    long current = progression.Player.Wardrobe.FashionExperience
                        - progression.Catalog.AppearanceLevelExperience(level);
                    long required = progression.Catalog.AppearanceLevelNextExperience(level)
                        - progression.Catalog.AppearanceLevelExperience(level);
                    expText.text = Math.Max(0, current).ToString("N0", CultureInfo.InvariantCulture) + "/"
                        + Math.Max(0, required).ToString("N0", CultureInfo.InvariantCulture);
                }
            }
            RectTransform sliderRect = NodeEnding("/AppearanceLevelcontent/ExpSlider/Slider");
            if (sliderRect != null)
            {
                Slider slider = sliderRect.GetComponent<Slider>();
                if (slider == null) slider = sliderRect.gameObject.AddComponent<Slider>();
                Transform fill = sliderRect.Find("SliderImg");
                if (fill != null)
                {
                    slider.fillRect = (RectTransform)fill;
                    slider.targetGraphic = fill.GetComponent<Graphic>();
                }
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.interactable = false;
                slider.value = progression.AppearanceLevelFill();
            }
        }

        private void OnUpgradeAppearanceLevel()
        {
            if (progression.IsMaxAppearanceLevel)
            {
                ShowStatus("潮流等级已达上限。");
                return;
            }
            if (!progression.CanUpgradeAppearanceLevel(out int level, out int newLevel))
            {
                ShowStatus("风尚经验不足。");
                return;
            }
            ProgressionResult result = progression.UpgradeAppearanceLevel(EventId("wardrobe-level-up"));
            ShowResult(result);
            if (!result.IsApplied) return;
            RefreshWardrobe();
            IReadOnlyDictionary<int, long> gained = progression.AppearancePropertiesGained(level, newLevel);
            if (gained.Count > 0) ShowUpgradePropsDialog(level, newLevel, gained);
        }

        private Button ButtonTrimmed(string name)
        {
            return buttons.Where(pair => pair.Key.TrimEnd().EndsWith("/" + name, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private void RefreshGrid()
        {
            if (itemViewport == null) return;
            bool isHonor = itemType == 339;
            honorMode = isHonor;
            if (isHonor)
            {
                List<int> ownedTitles = progression.Player.Inventory.Where(value => value.Count > 0
                    && progression.Catalog.ItemType(value.ItemId) == 339).Select(value => value.ItemId)
                    .Distinct().OrderByDescending(Quality).ThenByDescending(value => value).ToList();
                visibleEntries = new List<WardrobeEntry> { new WardrobeEntry { ItemType = 339, IsTitle = true } };
                visibleEntries.AddRange(ownedTitles.Select(value => new WardrobeEntry
                    { ItemId = value, ItemType = 339, IsTitle = true }));
            }
            else
            {
                int skinType = OfflineWardrobeRules.SkinTypeForItemType(itemType);
                List<int> owned = progression.Player.Inventory.Where(value => value.Count > 0
                    && progression.Catalog.ItemType(value.ItemId) == itemType)
                    .Select(value => value.ItemId).Distinct()
                    .Where(value => IsNotExpired(value) && (showAllToggle == null || showAllToggle.isOn
                        || progression.CanWearAppearance(value, DateTimeOffset.UtcNow.ToUnixTimeSeconds())))
                    .OrderByDescending(Quality).ThenByDescending(value => value).ToList();
                visibleEntries = new List<WardrobeEntry> { new WardrobeEntry { ItemType = itemType } };
                visibleEntries.AddRange(owned.Select(value => new WardrobeEntry { ItemId = value, ItemType = itemType }));
                if (skinType == 0) visibleEntries.Clear();
            }
            ConfigurePool();
            UpdatePool();
        }

        private void OnScrollChanged(Vector2 _) => UpdatePool();

        private void UpdatePool()
        {
            if (itemContent == null || pooledCards.Count == 0) return;
            bool title = honorMode;
            int rowCount = Mathf.CeilToInt((float)visibleEntries.Count / Mathf.Max(1, columns));
            float stride = cardHeight + cardSpacing;
            itemContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(itemViewport.rect.height, rowCount * stride));
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(-itemContent.anchoredPosition.y / stride) - 1);
            for (int i = 0; i < pooledCards.Count; i++)
            {
                int row = firstRow + i / columns;
                int col = i % columns;
                int index = row * columns + col;
                GameObject card = pooledCards[i];
                if (index >= visibleEntries.Count)
                {
                    card.SetActive(false);
                    continue;
                }
                card.SetActive(true);
                RectTransform rect = (RectTransform)card.transform;
                rect.anchoredPosition = new Vector2(col * (cardWidth + cardSpacing), -row * stride);
                BindCard(card, visibleEntries[index], title);
            }
        }

        private void BindCard(GameObject card, WardrobeEntry entry, bool title)
        {
            Button button = card.GetComponent<Button>();
            if (button == null)
            {
                Graphic target = card.GetComponent<Graphic>() ?? card.AddComponent<Image>();
                button = card.AddComponent<Button>();
                button.targetGraphic = target;
            }
            button.onClick.RemoveAllListeners();
            if (entry.IsTitle)
                button.onClick.AddListener(() => SelectTitle(entry.ItemId));
            else
                button.onClick.AddListener(() => SelectAppearance(entry.ItemId, entry.ItemType));

            Transform slot = FindDeep(card.transform, "Slot");
            Image icon = null;
            if (slot != null)
            {
                icon = slot.GetComponent<Image>();
                if (icon == null) icon = slot.gameObject.AddComponent<Image>();
                RectTransform iconRect = slot as RectTransform;
                iconRect.anchorMin = new Vector2(0.18f, 0.24f);
                iconRect.anchorMax = new Vector2(0.82f, 0.84f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                icon.raycastTarget = false;
                icon.preserveAspect = true;
            }

            string name = entry.ItemId == 0 ? LookupText("ui_append_appearance_null_text") : ItemName(entry.ItemId);
            Sprite sprite = entry.ItemId == 0 ? null : ItemSprite(entry.ItemId);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            Text label = EnsureLabel(card.transform, title ? 16 : 15, title);
            label.text = name;

            bool selected = entry.ItemId == 0
                ? title ? progression.Player.Wardrobe.WornTitleId == 0
                    : !progression.Player.Wardrobe.WornItems.Any(value =>
                        value.Type == OfflineWardrobeRules.SkinTypeForItemType(entry.ItemType))
                : title ? progression.Player.Wardrobe.WornTitleId == entry.ItemId
                    : progression.Player.Wardrobe.WornItems.Any(value => value.Id == entry.ItemId);
            RectTransform takeOff = FindNodeInCard(card.transform, "TakeOffGo");
            if (takeOff != null) takeOff.gameObject.SetActive(entry.ItemId == 0 && !title);
            Transform selectedIcon = FindDeep(card.transform, "SelectedImg");
            if (selectedIcon != null) selectedIcon.gameObject.SetActive(selected);

            Text expiry = EnsureExpiryText(card.transform, title);
            expiry.text = entry.ItemId == 0 ? string.Empty : Remaining(entry.ItemId, title);
        }

        private void SelectAppearance(int id, int type)
        {
            if (id == 0)
            {
                int skinType = OfflineWardrobeRules.SkinTypeForItemType(type);
                ProgressionResult takeOff = progression.TakeOffAppearance(skinType,
                    EventId("wardrobe-takeoff"));
                ShowResult(takeOff);
            }
            else if (!progression.Player.Inventory.Any(value => value.ItemId == id && value.Count > 0))
            {
                ShowStatus("尚未拥有该幻装。");
                return;
            }
            else
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                WardrobeConflictTip conflict = progression.PreviewAppearanceConflict(id, now);
                if (conflict.HasConflict) ShowStatus(ConflictText(conflict));
                ProgressionResult wear = progression.WearAppearance(id, now, EventId("wardrobe-wear"));
                if (!conflict.HasConflict) ShowResult(wear);
            }
            if (model != null) model.Refresh();
            RefreshWardrobe();
        }

        private string ConflictText(WardrobeConflictTip conflict)
        {
            string target = SkinTypeName(conflict.TargetType);
            string lost = string.Join("\u3001", conflict.LostTypes.Distinct()
                .Select(SkinTypeName).Where(value => !string.IsNullOrEmpty(value)));
            if (string.IsNullOrEmpty(lost)) return "\u7a7f\u6234" + target + "\u540e\u90e8\u5206\u5916\u89c2\u5c06\u88ab\u906e\u6321\u3002";
            if (conflict.IsCycle) return "\u7a7f\u6234" + target + "\u4e0e\u5df2\u7a7f\u6234\u7684" + lost
                + "\u4e92\u65a5\uff0c\u7a7f\u6234\u540e\u5c06\u88ab\u906e\u6321\u3002";
            return "\u7a7f\u6234" + target + "\u5c06\u906e\u6321\uff1a" + lost + "\u3002";
        }

        private string SkinTypeName(int skinType)
        {
            string[] names = { "eye", "brow", "mouth", "hair", "ethnicity", "body", "faceDecoration",
                "pWeapon", "sWeapon", "faceShape", "facePaint", "headDecoration", "backDecoration" };
            if (skinType < 0 || skinType >= names.Length) return string.Empty;
            string value = LookupText("ui_append_appearance_type_" + names[skinType]);
            return string.IsNullOrEmpty(value) ? names[skinType] : value;
        }

        private void SelectTitle(int id)
        {
            if (id == 0)
            {
                ProgressionResult takeOff = progression.TakeOffTitle(EventId("wardrobe-title-takeoff"));
                ShowResult(takeOff);
                RefreshWardrobe();
                return;
            }
            ShowTitleDetail(id);
        }

        private void ShowTitleDetail(int itemId)
        {
            if (activeDialog != null) Destroy(activeDialog);
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            string name = item == null ? itemId.ToString(CultureInfo.InvariantCulture)
                : Convert.ToString(item["Name"], CultureInfo.InvariantCulture);
            activeDialog = CreateDialog("wardrobe_props", name);
            Transform content = FindDeep(activeDialog.transform, "ListDataGo");
            if (content != null)
                for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            Text level = FindTextDeep(activeDialog.transform, "LevelTxt");
            SetText(level, name);
            var lines = new List<string>();
            if (item != null)
                lines.Add("品质：" + QualityName(Convert.ToInt32(item["Quality"],
                    CultureInfo.InvariantCulture)));
            long expiry = progression.Player.Wardrobe.Expiries.Where(value => value.ItemId == itemId)
                .Select(value => value.ExpiresAtUnixSeconds).DefaultIfEmpty(0).Max();
            if (expiry > 0) lines.Add("剩余：" + Remaining(itemId, true));
            IReadOnlyDictionary<int, long> props = progression.Catalog.TitleProperties(itemId);
            foreach (KeyValuePair<int, long> prop in props)
            {
                OfficialPropDefinition definition = progression.Catalog.Props.Get(prop.Key);
                if (definition != null)
                    lines.Add(definition.Name + " +" + OfficialPropFormat.Value(definition, prop.Value));
            }
            if (content != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    RectTransform row = BuildDialogRow("wardrobe_prop_row", content);
                    SetText(FindTextDeep(row, "LevelTxt"), lines[i]);
                    SetText(FindTextDeep(row, "PropTxt"), string.Empty);
                    Transform active = FindDeep(row, "ActivedTxt");
                    if (active != null) active.gameObject.SetActive(false);
                    Transform mask = FindDeep(row, "Mask");
                    if (mask != null) mask.gameObject.SetActive(false);
                }
            }
            Button action = EnsureDialogButton(activeDialog.transform, "穿戴");
            action.onClick.AddListener(() =>
            {
                ProgressionResult result = progression.WearTitle(itemId, EventId("wardrobe-title-wear"));
                ShowResult(result);
                if (activeDialog != null) Destroy(activeDialog);
                activeDialog = null;
                RefreshWardrobe();
            });
        }

        private Button EnsureDialogButton(Transform parent, string label)
        {
            Transform found = FindDeep(parent, "WardrobeTitleAction");
            if (found != null) return found.GetComponent<Button>();
            var buttonObject = new GameObject("WardrobeTitleAction", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(240f, 72f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            buttonObject.GetComponent<Image>().color = new Color(0.86f, 0.55f, 0.3f, 0.95f);
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private void ShowResult(ProgressionResult result)
        {
            if (result == null) return;
            ShowStatus(result.IsApplied ? "形象已更新。" : result.Message ?? result.Status.ToString());
        }

        private void ShowRuleDialog()
        {
            if (activeDialog != null) Destroy(activeDialog);
            activeDialog = CreateDialog("wardrobe_rules", "潮流等级规则");
            Transform content = FindDeep(activeDialog.transform, "RewardPreviewGo");
            if (content == null) return;
            List<Dictionary<string, object>> groups = progression.Catalog.WardrobeRows("fashion_exp_group")
                .OrderBy(row => Convert.ToInt32(row["GroupId"], CultureInfo.InvariantCulture)).ToList();
            string titleFormat = LookupText("ui_append_appearance_rule_fashion_title");
            for (int index = 0; index < groups.Count; index++)
            {
                Dictionary<string, object> group = groups[index];
                int groupId = Convert.ToInt32(group["GroupId"], CultureInfo.InvariantCulture);
                RectTransform row = BuildDialogRow("wardrobe_rule_row", content);
                Text title = FindTextDeep(row, "TitleText");
                SetText(title, titleFormat.Replace("{0}", (index + 2).ToString(CultureInfo.InvariantCulture))
                    .Replace("{1}", LookupText("fashion_exp_group_" + groupId)));
                Transform data = FindDeep(row, "ListDataGo");
                if (data == null) continue;
                for (int i = data.childCount - 1; i >= 0; i--) Destroy(data.GetChild(i).gameObject);
                List<object> qualities = CharacterModelPackage.AsArray(group["FashionQualityList"], "qualities");
                List<object> values = CharacterModelPackage.AsArray(group["FashionExpList"], "values");
                for (int i = 0; i < qualities.Count && i < values.Count; i++)
                {
                    int quality = Convert.ToInt32(qualities[i], CultureInfo.InvariantCulture);
                    RectTransform value = BuildDialogRow("wardrobe_rule_value", data);
                    SetText(FindTextDeep(value, "DataText1"), QualityName(quality));
                    SetText(FindTextDeep(value, "DataText2"),
                        Convert.ToInt64(values[i], CultureInfo.InvariantCulture).ToString("N0", CultureInfo.InvariantCulture));
                }
            }
        }

        private void ShowPropertiesDialog()
        {
            if (activeDialog != null) Destroy(activeDialog);
            activeDialog = CreateDialog("wardrobe_props", "潮流等级属性加成");
            PopulateLevelRows(activeDialog, progression.Player.Wardrobe.AppearanceLevel + 20, null);
        }

        private void ShowUpgradePropsDialog(int fromLevel, int toLevel, IReadOnlyDictionary<int, long> gained)
        {
            if (activeDialog != null) Destroy(activeDialog);
            activeDialog = CreateDialog("wardrobe_props",
                "潮流等级达到 " + toLevel.ToString(CultureInfo.InvariantCulture));
            PopulateLevelRows(activeDialog, toLevel, gained, fromLevel);
        }

        private void PopulateLevelRows(GameObject dialog, int limitLevel,
            IReadOnlyDictionary<int, long> gained = null, int fromLevel = -1)
        {
            Text level = FindTextDeep(dialog.transform, "LevelTxt");
            SetText(level, "潮流等级："
                + progression.Player.Wardrobe.AppearanceLevel.ToString(CultureInfo.InvariantCulture));
            Transform data = FindDeep(dialog.transform, "ListDataGo");
            if (data == null) return;
            for (int i = data.childCount - 1; i >= 0; i--) Destroy(data.GetChild(i).gameObject);
            int current = progression.Player.Wardrobe.AppearanceLevel;
            IEnumerable<Dictionary<string, object>> rows = progression.Catalog.WardrobeRows("appearance_prop")
                .Where(row => Convert.ToInt32(row["Level"], CultureInfo.InvariantCulture) <= limitLevel)
                .OrderBy(row => Convert.ToInt32(row["Level"], CultureInfo.InvariantCulture));
            if (gained != null)
                rows = gained.Count == 0 ? Enumerable.Empty<Dictionary<string, object>>() : rows
                    .Where(row => Convert.ToInt32(row["Level"], CultureInfo.InvariantCulture) > fromLevel);
            foreach (Dictionary<string, object> row in rows)
            {
                int levelValue = Convert.ToInt32(row["Level"], CultureInfo.InvariantCulture);
                RectTransform item = BuildDialogRow("wardrobe_prop_row", data);
                SetText(FindTextDeep(item, "LevelTxt"),
                    "等级：" + levelValue.ToString(CultureInfo.InvariantCulture));
                Dictionary<string, object> attributes = CharacterModelPackage.AsObject(row["Attribuate"], "attributes");
                var parts = new List<string>();
                foreach (KeyValuePair<string, object> attribute in attributes)
                {
                    int propType = int.Parse(attribute.Key, CultureInfo.InvariantCulture);
                    long value = Convert.ToInt64(attribute.Value, CultureInfo.InvariantCulture);
                    OfficialPropDefinition definition = progression.Catalog.Props.Get(propType);
                    parts.Add((definition == null ? propType.ToString(CultureInfo.InvariantCulture) : definition.Name)
                        + " +" + OfficialPropFormat.Value(definition, value));
                }
                SetText(FindTextDeep(item, "PropTxt"), string.Join("  ", parts));
                Text active = FindTextDeep(item, "ActivedTxt");
                if (active != null) active.gameObject.SetActive(levelValue <= current);
                Transform mask = FindDeep(item, "Mask");
                if (mask != null) mask.gameObject.SetActive(levelValue > current);
            }
        }

        private RectTransform BuildDialogRow(string templateName, Transform parent)
        {
            Dictionary<string, object> template = CharacterModelPackage.AsObject(templates[templateName], templateName);
            OfficialCharacterPanelNode node = ReadNode(CharacterModelPackage.AsObject(template["root"],
                templateName + " root"));
            return BuildNode(node, parent);
        }

        private static Text FindTextDeep(Transform parent, string name)
        {
            Transform found = FindDeep(parent, name);
            return found == null ? null : found.GetComponent<Text>();
        }

        private string QualityName(int quality)
        {
            string[] names = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            string name = LookupText("Quality." + names[Mathf.Clamp(quality, 0, names.Length - 1)]);
            return string.IsNullOrEmpty(name) ? quality.ToString(CultureInfo.InvariantCulture) : name;
        }

        private GameObject CreateDialog(string templateName, string title)
        {
            var root = new GameObject("WardrobeDialog_" + templateName, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);
            Dictionary<string, object> template = CharacterModelPackage.AsObject(templates[templateName], templateName);
            OfficialCharacterPanelNode dialog = ReadNode(CharacterModelPackage.AsObject(template["root"], templateName + " root"));
            RectTransform dialogRect = BuildNode(dialog, root.transform);
            foreach (Text label in dialogRect.GetComponentsInChildren<Text>(true))
                if (label.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0) label.text = title;
            foreach (Button button in dialogRect.GetComponentsInChildren<Button>(true))
                if (button.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0)
                    button.onClick.AddListener(() =>
                    {
                        if (activeDialog == root) activeDialog = null;
                        Destroy(root);
                    });
            return root;
        }

        private void AddDialogCopy(Transform parent, string value, int size)
        {
            RectTransform container = FindDeep(parent, "Content") as RectTransform ?? parent as RectTransform;
            var textObject = new GameObject("WardrobeDialogContent", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(container, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.1f, 0.18f);
            rect.anchorMax = new Vector2(0.9f, 0.78f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = new Color32(46, 43, 39, 255);
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            text.raycastTarget = false;
        }

        private Text CreateStatusText()
        {
            var go = new GameObject("WardrobeStatus", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.2f, 0.16f);
            rect.anchorMax = new Vector2(0.8f, 0.21f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 20;
            text.color = new Color32(255, 231, 179, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private void ShowStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 2.4f);
        }

        private void ClearStatus()
        {
            if (statusText != null) statusText.text = string.Empty;
        }

        private string Remaining(int itemId, bool title)
        {
            OfflineTimedAppearance expiry = progression.Player.Wardrobe.Expiries.FirstOrDefault(value => value.ItemId == itemId);
            if (expiry == null || expiry.ExpiresAtUnixSeconds <= 0) return string.Empty;
            long seconds = Math.Max(0, expiry.ExpiresAtUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            long days = seconds / 86400;
            long hours = seconds % 86400 / 3600;
            long minutes = seconds % 3600 / 60;
            long remainingSeconds = seconds % 60;
            string time = title
                ? days > 0 ? days + "天" + hours + "小时" + minutes + "分" + remainingSeconds + "秒"
                    : hours > 0 ? hours + "小时" + minutes + "分" + remainingSeconds + "秒"
                    : minutes > 0 ? minutes + "分" + remainingSeconds + "秒" : remainingSeconds + "秒"
                : days > 0 ? days + "天" : hours > 0 ? hours + "小时" : Math.Max(1, minutes) + "分";
            string format = LookupText("ui_honortitle_remain_time_text");
            return string.IsNullOrEmpty(format) ? time : format.Replace("{0}", time);
        }

        private bool IsNotExpired(int itemId)
        {
            OfflineTimedAppearance expiry = progression.Player.Wardrobe.Expiries.FirstOrDefault(value => value.ItemId == itemId);
            return expiry == null || expiry.ExpiresAtUnixSeconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private int Quality(int id)
        {
            return progression.Catalog.Items.TryGetValue(id, out Dictionary<string, object> data)
                ? Number(data, "Quality", Number(data, "QualityType", 0)) : 0;
        }

        private string ItemName(int id)
        {
            string key = "item_" + id.ToString(CultureInfo.InvariantCulture) + "_name";
            string localized = LookupText(key);
            if (!string.IsNullOrEmpty(localized)) return localized;
            if (progression.Catalog.Items.TryGetValue(id, out Dictionary<string, object> data))
            {
                foreach (string field in new[] { "Name", "ItemName", "DisplayName" })
                    if (data.TryGetValue(field, out object raw) && raw != null)
                    {
                        string value = Convert.ToString(raw, CultureInfo.InvariantCulture);
                        if (!string.IsNullOrEmpty(value)) return value;
                    }
            }
            return id.ToString(CultureInfo.InvariantCulture);
        }

        private Sprite ItemSprite(int id)
        {
            if (!progression.Catalog.Items.TryGetValue(id, out Dictionary<string, object> data)) return null;
            foreach (string field in new[] { "RawIcon", "Icon", "IconPath" })
                if (data.TryGetValue(field, out object raw) && raw is string path)
                {
                    Sprite sprite = assets.FindSprite(path);
                    if (sprite != null) return sprite;
                }
            return null;
        }

        private string LookupText(string key)
        {
            return texts != null && texts.TryGetValue(key, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private RectTransform BuildNode(OfficialCharacterPanelNode source, Transform parent)
        {
            var instance = new GameObject(source.name, typeof(RectTransform));
            instance.layer = parent.gameObject.layer;
            instance.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector(source.anchor_min, Vector2.zero);
            rect.anchorMax = Vector(source.anchor_max, Vector2.zero);
            rect.anchoredPosition = Vector(source.anchored_position, Vector2.zero);
            rect.sizeDelta = Vector(source.size_delta, Vector2.zero);
            rect.pivot = Vector(source.pivot, new Vector2(0.5f, 0.5f));
            rect.localScale = new Vector3(Value(source.scale, 0, 1f), Value(source.scale, 1, 1f), Value(source.scale, 2, 1f));
            rect.localRotation = Quaternion.Euler(0f, 0f, source.rotation);
            instance.SetActive(source.active);
            nodes[source.path] = rect;
            sourceNodes[source.path] = source;
            nodeActive[source.path] = source.active;
            sourceOrder.Add(source);

            if (source.image != null)
            {
                Image image = instance.AddComponent<Image>();
                image.sprite = source.image.sprite == null ? null : assets.FindSprite(source.image.sprite.file);
                image.color = ColorValue(source.image.color, Color.white);
                image.type = (Image.Type)Mathf.Clamp(source.image.type, 0, 3);
                image.preserveAspect = source.image.preserve_aspect;
                image.raycastTarget = HasScript(source, "Button") || source.button || HasScript(source, "TabItem");
            }
            if (source.text != null)
            {
                Text text = instance.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                string localized = string.IsNullOrEmpty(source.localization_key)
                    ? string.Empty : LookupText(source.localization_key);
                text.text = string.IsNullOrEmpty(localized) ? source.text.value ?? string.Empty : localized;
                text.color = ColorValue(source.text.color, Color.black);
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(source.text.font_size));
                text.alignment = TextAlignment(source.text.horizontal, source.text.vertical);
                text.horizontalOverflow = source.text.word_wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                text.verticalOverflow = source.text.overflow == 1 || source.text.overflow == 5 || source.text.overflow == 6
                    ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
                text.supportRichText = true;
                text.raycastTarget = false;
            }

            if (HasScript(source, "Button") || source.button || HasScript(source, "TabItem"))
            {
                Graphic graphic = instance.GetComponent<Graphic>();
                if (graphic == null) graphic = instance.AddComponent<Image>();
                graphic.raycastTarget = true;
                Button button = instance.AddComponent<Button>();
                button.targetGraphic = graphic;
                buttons[source.path] = button;
            }
            if (HasScript(source, "Toggle"))
            {
                Graphic graphic = instance.GetComponent<Graphic>();
                if (graphic == null) graphic = instance.AddComponent<Image>();
                Toggle toggle = instance.AddComponent<Toggle>();
                toggle.targetGraphic = graphic;
                Transform mark = FindDeep(instance.transform, "Checkmark");
                if (mark != null) toggle.graphic = mark.GetComponent<Graphic>();
            }
            if (source.mask)
            {
                Mask mask = instance.AddComponent<Mask>();
                mask.showMaskGraphic = instance.GetComponent<Graphic>() != null;
            }
            if (HasScript(source, "RectMask2D")) instance.AddComponent<RectMask2D>();
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
            foreach (OfficialCharacterPanelNode child in source.children ?? Array.Empty<OfficialCharacterPanelNode>())
                BuildNode(child, rect);
            return rect;
        }

        private static void AddLayout(OfficialCharacterPanelNode source, GameObject instance)
        {
            OfficialCharacterPanelGroupLayout data = source.layout;
            HorizontalOrVerticalLayoutGroup group = null;
            if (HasScript(source, "HorizontalLayoutGroup") || data.axis == 0)
                group = instance.AddComponent<HorizontalLayoutGroup>();
            else if (HasScript(source, "VerticalLayoutGroup") || data.axis == 1)
                group = instance.AddComponent<VerticalLayoutGroup>();
            if (group == null) return;
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

        private OfficialCharacterPanelNode ReadNode(Dictionary<string, object> data)
        {
            var node = new OfficialCharacterPanelNode
            {
                name = String(data, "name"), path = String(data, "path"), active = Boolean(data, "active", true),
                anchor_min = Floats(data, "anchor_min"), anchor_max = Floats(data, "anchor_max"),
                anchored_position = Floats(data, "anchored_position"), size_delta = Floats(data, "size_delta"),
                pivot = Floats(data, "pivot"), scale = Floats(data, "scale"), rotation = Number(data, "rotation", 0f),
                scripts = data.TryGetValue("scripts", out object scripts) && scripts is List<object> scriptList
                    ? scriptList.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)).ToArray() : Array.Empty<string>(),
                button = Boolean(data, "button", false), mask = Boolean(data, "mask", false),
                localization_key = String(data, "localization_key"),
                original_bindings = data.TryGetValue("original_bindings", out object bindingRaw)
                    && bindingRaw is Dictionary<string, object> bindings ? bindings : new Dictionary<string, object>()
            };
            if (data.TryGetValue("image", out object imageRaw) && imageRaw is Dictionary<string, object> image)
            {
                node.image = new OfficialCharacterPanelImage
                {
                    color = Floats(image, "color"), type = (int)Number(image, "type", 0),
                    preserve_aspect = Boolean(image, "preserve_aspect", false),
                    sprite = image.TryGetValue("sprite", out object spriteRaw) && spriteRaw is Dictionary<string, object> sprite
                        ? new OfficialCharacterPanelSprite { file = String(sprite, "file"), border = Floats(sprite, "border") } : null
                };
            }
            if (data.TryGetValue("text", out object textRaw) && textRaw is Dictionary<string, object> text)
            {
                node.text = new OfficialCharacterPanelText
                {
                    value = String(text, "value"), color = Floats(text, "color"), font_size = Number(text, "font_size", 16f),
                    horizontal = (int)Number(text, "horizontal", 2), vertical = (int)Number(text, "vertical", 512),
                    word_wrap = Boolean(text, "word_wrap", true), overflow = (int)Number(text, "overflow", 0), margin = Floats(text, "margin")
                };
            }
            if (data.TryGetValue("layout_element", out object layoutElementRaw)
                && layoutElementRaw is Dictionary<string, object> layoutElement)
                node.layout_element = new OfficialCharacterPanelLayoutElement
                {
                    ignore = Boolean(layoutElement, "ignore", false), min = Floats(layoutElement, "min"),
                    preferred = Floats(layoutElement, "preferred"), flexible = Floats(layoutElement, "flexible")
                };
            if (data.TryGetValue("layout", out object groupRaw) && groupRaw is Dictionary<string, object> group)
                node.layout = new OfficialCharacterPanelGroupLayout
                {
                    axis = (int)Number(group, "axis", 0), padding = Ints(group, "padding"),
                    alignment = (int)Number(group, "alignment", 4), spacing = Number(group, "spacing", 0f),
                    expand = Booleans(group, "expand"), control = Booleans(group, "control"),
                    child_scale = Booleans(group, "child_scale"), reverse = Boolean(group, "reverse", false)
                };
            node.children = data.TryGetValue("children", out object childrenRaw) && childrenRaw is List<object> children
                ? children.Select(child => ReadNode(CharacterModelPackage.AsObject(child, "wardrobe child node"))).ToArray()
                : Array.Empty<OfficialCharacterPanelNode>();
            return node;
        }

        private Dictionary<string, object> TemplateRoot(string name)
        {
            Dictionary<string, object> template = CharacterModelPackage.AsObject(templates[name], name);
            return CharacterModelPackage.AsObject(template["root"], name + " root");
        }

        private RectTransform NodeEnding(string suffix)
        {
            return nodes.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private Text FindTextByPath(string suffix)
        {
            return nodes.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value.GetComponent<Text>()).FirstOrDefault(value => value != null);
        }

        private Button ButtonEnding(string suffix)
        {
            return buttons.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private void BindSuffix(string suffix, Action action)
        {
            Button button = ButtonEnding("/" + suffix);
            if (button != null) button.onClick.AddListener(() => action());
        }

        private Text EnsureLabel(Transform parent, int size, bool title)
        {
            Transform found = FindDeep(parent, "WardrobeNameLabel");
            if (found == null)
            {
                var go = new GameObject("WardrobeNameLabel", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                found = go.transform;
                RectTransform rect = (RectTransform)found;
                rect.anchorMin = new Vector2(0.04f, 0.015f);
                rect.anchorMax = new Vector2(0.96f, title ? 0.27f : 0.22f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            Text text = found.GetComponent<Text>();
            if (text == null) text = found.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = new Color32(45, 39, 33, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Text EnsureExpiryText(Transform parent, bool title)
        {
            Transform found = FindDeep(parent, "WardrobeExpiryLabel");
            if (found == null)
            {
                var go = new GameObject("WardrobeExpiryLabel", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                found = go.transform;
                RectTransform rect = (RectTransform)found;
                rect.anchorMin = new Vector2(0.04f, 0.24f);
                rect.anchorMax = new Vector2(0.96f, 0.37f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            Text text = found.GetComponent<Text>();
            if (text == null) text = found.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = title ? 12 : 11;
            text.color = new Color32(118, 91, 61, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform FindNodeInCard(Transform root, string name)
        {
            return FindDeep(root, name) as RectTransform;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static RectTransform FindDirectChild(RectTransform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i) as RectTransform;
            return null;
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static bool TryBinding(OfficialCharacterPanelNode node, string name, out Dictionary<string, object> binding)
        {
            binding = null;
            return node.original_bindings != null && node.original_bindings.TryGetValue(name, out object raw)
                && raw is Dictionary<string, object> result && (binding = result) != null;
        }

        private static IEnumerable<OfficialCharacterPanelNode> Walk(OfficialCharacterPanelNode root)
        {
            yield return root;
            foreach (OfficialCharacterPanelNode child in root.children ?? Array.Empty<OfficialCharacterPanelNode>())
            foreach (OfficialCharacterPanelNode nested in Walk(child)) yield return nested;
        }

        private static bool HasScript(OfficialCharacterPanelNode node, string name)
        {
            return Array.IndexOf(node.scripts ?? Array.Empty<string>(), name) >= 0;
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

        private static float Number(Dictionary<string, object> value, string key, float fallback)
        {
            return value.TryGetValue(key, out object raw) && raw != null
                ? Convert.ToSingle(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static int Number(Dictionary<string, object> value, string key, int fallback)
        {
            return value.TryGetValue(key, out object raw) && raw != null
                ? Convert.ToInt32(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static int Number(Dictionary<string, object> value, string key, int fallback, bool _)
        {
            return Number(value, key, fallback);
        }

        private static string String(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object raw) && raw != null
                ? Convert.ToString(raw, CultureInfo.InvariantCulture) : string.Empty;
        }

        private static bool Boolean(Dictionary<string, object> value, string key, bool fallback)
        {
            return value.TryGetValue(key, out object raw) && raw != null ? Convert.ToBoolean(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static float[] Floats(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object raw) && raw is List<object> list
                ? list.Select(item => Convert.ToSingle(item, CultureInfo.InvariantCulture)).ToArray() : Array.Empty<float>();
        }

        private static int[] Ints(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object raw) && raw is List<object> list
                ? list.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToArray() : Array.Empty<int>();
        }

        private static bool[] Booleans(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object raw) && raw is List<object> list
                ? list.Select(item => Convert.ToBoolean(item, CultureInfo.InvariantCulture)).ToArray() : Array.Empty<bool>();
        }

        private static bool Flag(bool[] values, int index, bool fallback)
        {
            return values != null && index < values.Length ? values[index] : fallback;
        }

        private static Color ColorValue(float[] values, Color fallback)
        {
            return values == null || values.Length < 4 ? fallback : new Color(values[0], values[1], values[2], values[3]);
        }

        private static TextAnchor TextAlignment(int horizontal, int vertical)
        {
            bool left = (horizontal & 1) != 0;
            bool right = (horizontal & 4) != 0;
            bool top = (vertical & 256) != 0;
            bool bottom = (vertical & 1024) != 0;
            if (left && right) left = right = false;
            if (top && bottom) top = bottom = false;
            if (top) return left ? TextAnchor.UpperLeft : right ? TextAnchor.UpperRight : TextAnchor.UpperCenter;
            if (bottom) return left ? TextAnchor.LowerLeft : right ? TextAnchor.LowerRight : TextAnchor.LowerCenter;
            return left ? TextAnchor.MiddleLeft : right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
        }

        private static string EventId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

        private static void SetText(Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }
    }
}
