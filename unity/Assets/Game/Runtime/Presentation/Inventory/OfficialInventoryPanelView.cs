using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Inventory
{
    public sealed class OfficialInventoryPanelView : MonoBehaviour
    {
        private const string PanelPath = "/PlayerBagPanel";
        private const string ScrollPath = "/PlayerBagPanel/BgPanel1/Content/MiddleGo/ScrollRect ";
        private static readonly string[] CountSuffixes = { "", "万", "亿", "兆", "京" };
        private static readonly Color ProgressEnoughColor = new Color(0.79607844f, 0.81176472f, 0.38039216f, 1f);
        private static readonly Color ProgressLackColor = new Color(0.96862745f, 0.32156864f, 0.21568628f, 1f);
        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfflineInventorySort> sortOptionPaths = new Dictionary<string, OfflineInventorySort>(StringComparer.Ordinal);
        private readonly List<OfflineInventoryItemView> displayedItems = new List<OfflineInventoryItemView>();
        private OfficialInventoryAssetCatalog assets;
        private OfficialEquipmentCatalog rules;
        private OfflineMainlineSave save;
        private IReadOnlyList<OfficialInventoryTab> tabs;
        private OfficialInventoryTab currentTab;
        private OfflineInventorySort sort = OfflineInventorySort.Type;
        private bool reverse;
        private GameObject detailWindow;

        public event Action<OfflineInventoryItemView> ItemSelected;
        public event Action Closed;

        public void Initialize(OfficialInventoryAssetCatalog assetCatalog,
            OfficialEquipmentCatalog equipmentCatalog, OfflineMainlineSave playerSave)
        {
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            rules = equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            save = playerSave ?? throw new ArgumentNullException(nameof(playerSave));
            assets.ValidateBindings();
            ClearView();
            ConfigureCanvas();

            Dictionary<string, object> panel = assets.LoadTemplate("bag");
            Dictionary<string, object> rootData = Object(panel, "root");
            RectTransform canvasRoot = (RectTransform)transform;
            BuildNode(rootData, canvasRoot, nodes);
            tabs = assets.LoadBagTabs();
            if (tabs.Count == 0) throw new FormatException("Original inventory tabs are missing.");
            currentTab = tabs[0];
            BindTabs(rootData);
            BindSortOptions(rootData);
            foreach (RectTransform node in nodes.Values)
            {
                if (node.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Button closeButton = node.GetComponent<Button>();
                if (closeButton != null) closeButton.onClick.AddListener(Close);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (assets == null || save == null || currentTab == null) return;
            if (!nodes.TryGetValue(ScrollPath + "/Viewport/Content", out RectTransform content))
                throw new FormatException("Original inventory scroll content is missing.");

            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            displayedItems.Clear();
            IReadOnlyList<OfflineInventoryItemView> items = OfflineInventoryCatalog.Query(save, rules,
                currentTab.ItemTypes, sort, reverse, DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                assets.FindRandomTreasurePieceRequirements());
            Dictionary<string, object> rowTemplate = assets.LoadRowTemplate("bag", ScrollPath);
            if (rowTemplate == null) throw new FormatException("Original inventory row template is missing.");
            Dictionary<string, object> rowRoot = Object(rowTemplate, "root");

            foreach (OfflineInventoryItemView item in items)
            {
                var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                RectTransform row = BuildNode(rowRoot, content, rowNodes);
                if (rowNodes.TryGetValue("/PlayerBagPanelItem/Slot", out RectTransform slot))
                    BuildItemIcon(item, slot);
                Button button = row.gameObject.GetComponent<Button>();
                if (button == null)
                {
                    Image hitTarget = row.gameObject.AddComponent<Image>();
                    hitTarget.color = new Color(1f, 1f, 1f, 0f);
                    hitTarget.raycastTarget = true;
                    button = row.gameObject.AddComponent<Button>();
                    button.targetGraphic = hitTarget;
                }
                button.onClick.AddListener(() => OpenItem(item));
                displayedItems.Add(item);
            }
        }

        public void Close()
        {
            if (detailWindow != null) Destroy(detailWindow);
            detailWindow = null;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void ClearView()
        {
            if (detailWindow != null) Destroy(detailWindow);
            detailWindow = null;
            nodes.Clear();
            sortOptionPaths.Clear();
            displayedItems.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        }

        private void ConfigureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (GetComponent<RectTransform>() == null) gameObject.AddComponent<RectTransform>();
            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            if (EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
        }

        private void BindTabs(Dictionary<string, object> panelRoot)
        {
            Dictionary<string, object> bindings = Object(Object(panelRoot, "original_bindings"), "PlayerBagPanel");
            string titlePath = Text(bindings, "TypeTitleTxt");
            ToggleGroup group = nodes.Values.Select(value => value.GetComponent<ToggleGroup>())
                .FirstOrDefault(value => value != null);
            foreach (OfficialInventoryTab tab in tabs)
            {
                if (!nodes.TryGetValue(tab.Path, out RectTransform rect)) continue;
                Toggle toggle = rect.GetComponent<Toggle>();
                if (toggle == null)
                {
                    Graphic graphic = rect.GetComponent<Graphic>();
                    if (graphic == null) graphic = rect.gameObject.AddComponent<Image>();
                    toggle = rect.gameObject.AddComponent<Toggle>();
                    toggle.targetGraphic = graphic;
                }
                toggle.group = group;
                Text label = rect.GetComponentsInChildren<Text>(true).FirstOrDefault();
                if (label != null) label.text = assets.FindText(tab.NameKey);
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    currentTab = tab;
                    SetDetailText(nodes, titlePath, assets.FindText(tab.NameKey));
                    Refresh();
                });
            }
            SetDetailText(nodes, titlePath, assets.FindText(currentTab.NameKey));
            Toggle initial = nodes.TryGetValue(currentTab.Path, out RectTransform selected)
                ? selected.GetComponent<Toggle>() : null;
            if (initial != null) initial.isOn = true;
        }

        private void BindSortOptions(Dictionary<string, object> panelRoot)
        {
            Dictionary<string, object> bindings = Object(panelRoot, "original_bindings");
            Dictionary<string, object> playerBag = Object(bindings, "PlayerBagPanel");
            if (!(playerBag.TryGetValue("mSortToggleList", out object rawOptions) && rawOptions is List<object> options))
                return;
            foreach (object rawOption in options)
            {
                if (!(rawOption is Dictionary<string, object> option)) continue;
                string path = Text(option, "toggle");
                if (!nodes.TryGetValue(path, out RectTransform rect)) continue;
                OfflineInventorySort? selectedSort = path.EndsWith("ByType", StringComparison.Ordinal)
                    ? OfflineInventorySort.Type
                    : path.EndsWith("ByQuality", StringComparison.Ordinal)
                        ? OfflineInventorySort.Quality
                        : path.EndsWith("ByTime", StringComparison.Ordinal)
                            ? OfflineInventorySort.GetTime : (OfflineInventorySort?)null;
                if (!selectedSort.HasValue) continue;
                sortOptionPaths[path] = selectedSort.Value;
                Button button = rect.GetComponent<Button>();
                if (button == null)
                {
                    Graphic graphic = rect.GetComponent<Graphic>();
                    if (graphic == null) graphic = rect.gameObject.AddComponent<Image>();
                    button = rect.gameObject.AddComponent<Button>();
                    button.targetGraphic = graphic;
                }
                OfflineInventorySort value = selectedSort.Value;
                string reversePath = path + "/Btn_Sel";
                if (nodes.TryGetValue(reversePath, out RectTransform reverseButtonNode))
                {
                    Button reverseButton = reverseButtonNode.GetComponent<Button>();
                    if (reverseButton != null)
                    {
                        SetSortArrow(reverseButtonNode, false);
                        button.onClick.AddListener(() =>
                        {
                            sort = value;
                            reverse = false;
                            RefreshSortOptionStates();
                            SetSortArrow(reverseButtonNode, false);
                            Refresh();
                        });
                        reverseButton.onClick.AddListener(() =>
                        {
                            reverse = !reverse;
                            SetSortArrow(reverseButtonNode, reverse);
                            Refresh();
                        });
                    }
                }
                else button.onClick.AddListener(() =>
                {
                    sort = value;
                    reverse = false;
                    RefreshSortOptionStates();
                    Refresh();
                });
            }
            RefreshSortOptionStates();
        }

        private void RefreshSortOptionStates()
        {
            foreach (KeyValuePair<string, OfflineInventorySort> option in sortOptionPaths)
            {
                if (!nodes.TryGetValue(option.Key, out RectTransform rect)) continue;
                Transform selected = rect.Find("Btn_Sel");
                Transform normal = rect.Find("Btn_Nor");
                if (selected != null) selected.gameObject.SetActive(option.Value == sort);
                if (normal != null) normal.gameObject.SetActive(option.Value != sort);
                Toggle toggle = rect.GetComponent<Toggle>();
                if (toggle != null) toggle.interactable = false;
            }
        }

        private void BuildItemIcon(OfflineInventoryItemView item, RectTransform parent)
        {
            string templateName = assets.FindItemIconTemplateName(item.ItemId, item.ItemType);
            Dictionary<string, object> template = assets.LoadTemplate(templateName);
            if (template == null || !template.TryGetValue("root", out object rawRoot))
                throw new FormatException("Original item icon template is missing: " + templateName);
            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            RectTransform iconRoot = BuildNode(Object(rawRoot, "item icon root"), parent, iconNodes);
            iconRoot.anchorMin = new Vector2(0.5f, 0.5f);
            iconRoot.anchorMax = new Vector2(0.5f, 0.5f);
            iconRoot.anchoredPosition = Vector2.zero;

            Sprite itemSprite = assets.FindItemSprite(item.ItemId);
            RectTransform imageNode = iconNodes.Values.FirstOrDefault(node => node.name == "icon");
            if (imageNode != null)
            {
                Image image = imageNode.GetComponent<Image>();
                if (image != null) image.sprite = itemSprite;
            }
            RectTransform qualityNode = iconNodes.Values.FirstOrDefault(node => node.name == "QualityImg");
            if (qualityNode != null)
            {
                Image qualityImage = qualityNode.GetComponent<Image>();
                if (qualityImage != null) qualityImage.sprite = assets.FindQualityBackground(item.Quality);
            }
            RectTransform fakeIcon = iconNodes.Values.FirstOrDefault(node => node.name == "FakeIcon");
            if (fakeIcon != null)
            {
                Text label = fakeIcon.GetComponent<Text>();
                if (label != null) label.text = itemSprite == null ? OfficialItemName(item) : string.Empty;
                fakeIcon.gameObject.SetActive(itemSprite == null);
            }
            RectTransform countNode = iconNodes.FirstOrDefault(pair =>
                pair.Key.EndsWith("/BgCountText/CountText", StringComparison.Ordinal)).Value;
            if (countNode != null)
            {
                Text count = countNode.GetComponent<Text>();
                if (count != null) count.text = FormatCount(item.Count);
                bool showCount = item.Count > 0 && item.CanOverlap
                    && item.ItemType != 326 && item.ItemType != 334 && item.ItemType != 340 && item.ItemType != 402
                    && (item.DetailKind != OfflineInventoryDetailKind.Equipment || item.IsEquipped);
                countNode.gameObject.SetActive(showCount);
            }
            ApplyMergeProgress(item, iconNodes);
            AddRedDot(item, iconNodes);
            Button iconButton = iconRoot.GetComponent<Button>();
            if (iconButton != null) iconButton.onClick.AddListener(() => OpenItem(item));
        }

        private static void ApplyMergeProgress(OfflineInventoryItemView item,
            Dictionary<string, RectTransform> iconNodes)
        {
            if (item.MergeRequiredCount <= 0 || item.ItemType != 326 && item.ItemType != 334 && item.ItemType != 340)
                return;
            RectTransform progress = iconNodes.Values.FirstOrDefault(node => node.name == "Progress");
            if (progress == null)
                throw new FormatException("Original item icon merge progress node is missing: " + item.ItemType);
            progress.gameObject.SetActive(true);
            RectTransform currentNode = iconNodes.Values.FirstOrDefault(node => node.name == "LeftTxt"
                && node.parent != null && node.parent.parent == progress);
            RectTransform requiredNode = iconNodes.Values.FirstOrDefault(node => node.name == "RightTxt"
                && node.parent != null && node.parent.parent == progress);
            Text current = currentNode == null ? null : currentNode.GetComponent<Text>();
            Text required = requiredNode == null ? null : requiredNode.GetComponent<Text>();
            if (current == null || required == null)
                throw new FormatException("Original item icon merge progress labels are missing: " + item.ItemType);
            current.text = FormatCount(item.Count);
            current.color = item.Count >= item.MergeRequiredCount ? ProgressEnoughColor : ProgressLackColor;
            required.text = FormatCount(item.MergeRequiredCount);
        }

        private void AddRedDot(OfflineInventoryItemView item, Dictionary<string, RectTransform> iconNodes)
        {
            if (item.CanUseRedDot != true) return;
            int attachmentId = item.RedDotType == 2 ? 8 : item.RedDotType == 1 ? 15 : 0;
            if (attachmentId == 0) return;
            RectTransform attachmentParent = iconNodes.Values.FirstOrDefault(node => node.name == "ItemIconAttachment");
            Dictionary<string, object> template = assets.LoadAttachmentTemplate(attachmentId);
            if (attachmentParent == null || template == null || !template.TryGetValue("root", out object rawRoot))
                throw new FormatException("Official inventory red-dot attachment is incomplete.");
            var attachmentNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            RectTransform root = BuildNode(Object(rawRoot, "item icon attachment"), attachmentParent, attachmentNodes);
            int layer = assets.FindAttachmentLayer(attachmentId);
            root.SetSiblingIndex(Mathf.Clamp(layer, 0, attachmentParent.childCount - 1));
        }

        private void OpenItem(OfflineInventoryItemView item)
        {
            ItemSelected?.Invoke(item);
            ShowDetail(item);
        }

        private void ShowDetail(OfflineInventoryItemView item)
        {
            if (detailWindow != null) Destroy(detailWindow);
            Dictionary<string, object> template = assets.LoadTemplate("item_detail");
            if (template == null || !template.TryGetValue("root", out object rawRoot)) return;
            detailWindow = new GameObject("InventoryItemDetail", typeof(RectTransform));
            detailWindow.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)detailWindow.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var detailNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            BuildNode(Object(rawRoot, "item detail root"), root, detailNodes);
            SetDetailText(detailNodes, "/ItemDetailDialog/BgDialog1/Content/TopGo/NameGo/NameText", OfficialItemName(item));
            SetDetailText(detailNodes, "/ItemDetailDialog/BgDialog1/Content/TopGo/CountGo/CountContent/CountText",
                FormatCount(item.Count));
            Sprite sprite = assets.FindItemSprite(item.ItemId);
            if (detailNodes.TryGetValue("/ItemDetailDialog/BgDialog1/Content/TopGo/IconSlot/Icon", out RectTransform icon))
            {
                Image image = icon.GetComponent<Image>();
                if (image != null) image.sprite = sprite;
            }
            if (detailNodes.TryGetValue("/ItemDetailDialog/BgDialog1/Content/TopGo/IconSlot/FakeIcon", out RectTransform fakeIcon))
            {
                Text label = fakeIcon.GetComponent<Text>();
                if (label != null) label.text = sprite == null ? OfficialItemName(item) : string.Empty;
                fakeIcon.gameObject.SetActive(sprite == null);
            }
            foreach (RectTransform node in detailNodes.Values)
            {
                Button action = node.GetComponent<Button>();
                if (action != null && node.name != "ShareBtn" && node.name != "CommonBtn_11")
                    action.interactable = false;
            }
            RectTransform close = detailNodes.Values.FirstOrDefault(node => node.name == "ShareBtn");
            if (close != null)
            {
                Button button = close.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() =>
                {
                    Destroy(detailWindow);
                    detailWindow = null;
                });
            }
            RectTransform cancel = detailNodes.Values.FirstOrDefault(node => node.name == "CommonBtn_11");
            Button cancelButton = cancel == null ? null : cancel.GetComponent<Button>();
            if (cancelButton != null) cancelButton.onClick.AddListener(() =>
            {
                Destroy(detailWindow);
                detailWindow = null;
            });
        }

        private static void SetDetailText(Dictionary<string, RectTransform> detailNodes, string path, string value)
        {
            if (detailNodes.TryGetValue(path, out RectTransform rect) && rect.GetComponent<Text>() != null)
                rect.GetComponent<Text>().text = value ?? string.Empty;
        }

        private string OfficialItemName(OfflineInventoryItemView item)
        {
            string name = assets.FindText(item.NameKey);
            return string.IsNullOrEmpty(name) ? item.NameKey : name;
        }

        private RectTransform BuildNode(Dictionary<string, object> data, Transform parent,
            Dictionary<string, RectTransform> targetNodes)
        {
            string name = Text(data, "name");
            string path = Text(data, "path");
            GameObject instance = new GameObject(name, typeof(RectTransform));
            instance.layer = parent.gameObject.layer;
            instance.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector(data, "anchor_min", Vector2.zero);
            rect.anchorMax = Vector(data, "anchor_max", Vector2.zero);
            rect.anchoredPosition = Vector(data, "anchored_position", Vector2.zero);
            rect.sizeDelta = Vector(data, "size_delta", Vector2.zero);
            rect.pivot = Vector(data, "pivot", new Vector2(0.5f, 0.5f));
            rect.localScale = Vector3Value(data, "scale", Vector3.one);
            rect.localRotation = Quaternion.Euler(0f, 0f, Number(data, "rotation", 0f));
            instance.SetActive(Boolean(data, "active", true));
            targetNodes[path] = rect;

            if (data.TryGetValue("image", out object rawImage) && rawImage is Dictionary<string, object> imageData)
            {
                Image image = instance.AddComponent<Image>();
                image.color = ColorValue(imageData, Color.white);
                image.type = (Image.Type)Mathf.Clamp(Integer(imageData, "type", 0), 0, 3);
                image.preserveAspect = Boolean(imageData, "preserve_aspect", false);
                if (imageData.TryGetValue("sprite", out object rawSprite) && rawSprite is Dictionary<string, object> spriteData)
                    image.sprite = assets.FindSprite(Text(spriteData, "file"));
                image.raycastTarget = HasScript(data, "Button") || Boolean(data, "button", false);
            }
            if (data.TryGetValue("text", out object rawText) && rawText is Dictionary<string, object> textData)
            {
                Text text = instance.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.text = Text(textData, "value");
                text.color = ColorValue(textData, Color.black);
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(Number(textData, "font_size", 16f)));
                text.alignment = TextAlignment(Integer(textData, "horizontal", 1), Integer(textData, "vertical", 512));
                text.horizontalOverflow = Boolean(textData, "word_wrap", true)
                    ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                text.verticalOverflow = Integer(textData, "overflow", 0) == 1
                    ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
                text.supportRichText = true;
                text.raycastTarget = false;
            }

            foreach (object rawChild in Array(data, "children"))
                if (rawChild is Dictionary<string, object> child)
                    BuildNode(child, rect, targetNodes);

            if (Boolean(data, "mask", false))
            {
                Mask mask = instance.AddComponent<Mask>();
                mask.showMaskGraphic = instance.GetComponent<Graphic>() != null;
            }
            if (Boolean(data, "rectmask2d", false)) instance.AddComponent<RectMask2D>();
            if (HasScript(data, "ToggleGroup")) instance.AddComponent<ToggleGroup>();
            if (HasScript(data, "TabItem") || HasScript(data, "Toggle"))
            {
                Graphic graphic = instance.GetComponent<Graphic>();
                if (graphic == null) graphic = instance.AddComponent<Image>();
                Toggle toggle = instance.AddComponent<Toggle>();
                toggle.targetGraphic = graphic;
                toggle.graphic = instance.GetComponentsInChildren<Graphic>(true)
                    .FirstOrDefault(value => value.gameObject != instance);
            }
            if (HasScript(data, "Button") || Boolean(data, "button", false))
            {
                Graphic graphic = instance.GetComponent<Graphic>();
                if (graphic == null) graphic = instance.AddComponent<Image>();
                Button button = instance.AddComponent<Button>();
                button.targetGraphic = graphic;
            }
            if (data.TryGetValue("grid", out object rawGrid) && rawGrid is Dictionary<string, object> gridData)
                ApplyGrid(instance, gridData);
            if (data.TryGetValue("size_fitter", out object rawFitter) && rawFitter is List<object> fitterData
                && fitterData.Count >= 2)
            {
                ContentSizeFitter fitter = instance.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(Integer(fitterData, 0), 0, 2);
                fitter.verticalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(Integer(fitterData, 1), 0, 2);
            }
            if (data.TryGetValue("scroll", out object rawScroll) && rawScroll is Dictionary<string, object> scrollData)
                ApplyScroll(instance, rect, path, targetNodes, scrollData);
            return rect;
        }

        private static void ApplyGrid(GameObject instance, Dictionary<string, object> data)
        {
            GridLayoutGroup grid = instance.AddComponent<GridLayoutGroup>();
            List<object> padding = Array(data, "padding");
            grid.padding = new RectOffset(Integer(padding, 0), Integer(padding, 1), Integer(padding, 2), Integer(padding, 3));
            grid.cellSize = Vector(data, "cell", Vector2.zero);
            grid.spacing = Vector(data, "spacing", Vector2.zero);
            grid.startCorner = (GridLayoutGroup.Corner)Mathf.Clamp(Integer(data, "corner", 0), 0, 3);
            grid.startAxis = (GridLayoutGroup.Axis)Mathf.Clamp(Integer(data, "axis", 0), 0, 1);
            grid.childAlignment = (TextAnchor)Mathf.Clamp(Integer(data, "alignment", 0), 0, 8);
            int constraint = Integer(data, "constraint", 0);
            grid.constraint = constraint == 1 ? GridLayoutGroup.Constraint.FixedColumnCount
                : constraint == 2 ? GridLayoutGroup.Constraint.FixedRowCount : GridLayoutGroup.Constraint.Flexible;
            grid.constraintCount = Mathf.Max(1, Integer(data, "count", 1));
        }

        private static void SetSortArrow(RectTransform button, bool isReverse)
        {
            RectTransform ascending = button.Find("Sort") as RectTransform;
            RectTransform descending = button.Find("Sort (1)") as RectTransform;
            if (ascending != null) ascending.gameObject.SetActive(isReverse);
            if (descending != null) descending.gameObject.SetActive(!isReverse);
        }

        private static void ApplyScroll(GameObject instance, RectTransform rect, string path,
            Dictionary<string, RectTransform> targetNodes, Dictionary<string, object> data)
        {
            ScrollRect scroll = instance.GetComponent<ScrollRect>();
            if (scroll == null) scroll = instance.AddComponent<ScrollRect>();
            scroll.horizontal = Boolean(data, "horizontal", false);
            scroll.vertical = Boolean(data, "vertical", true);
            scroll.scrollSensitivity = Number(data, "sensitivity", 1f);
            if (targetNodes.TryGetValue(path + "/Viewport", out RectTransform viewport)) scroll.viewport = viewport;
            if (targetNodes.TryGetValue(path + "/Viewport/Content", out RectTransform content)) scroll.content = content;
        }

        private static bool HasScript(Dictionary<string, object> data, string name) =>
            Array(data, "scripts").Any(value => string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), name,
                StringComparison.Ordinal));

        private static Dictionary<string, object> Object(Dictionary<string, object> data, string key) =>
            data.TryGetValue(key, out object value) && value is Dictionary<string, object> result
                ? result : throw new FormatException("Official inventory data is missing: " + key);

        private static Dictionary<string, object> Object(object value, string context) =>
            value as Dictionary<string, object> ?? throw new FormatException("Official inventory data is missing: " + context);

        private static List<object> Array(Dictionary<string, object> data, string key) =>
            data.TryGetValue(key, out object value) && value is List<object> result ? result : new List<object>();

        private static Vector2 Vector(Dictionary<string, object> data, string key, Vector2 fallback) =>
            data.TryGetValue(key, out object raw) && raw is List<object> values && values.Count >= 2
                ? new Vector2(Float(values[0]), Float(values[1])) : fallback;

        private static Vector3 Vector3Value(Dictionary<string, object> data, string key, Vector3 fallback) =>
            data.TryGetValue(key, out object raw) && raw is List<object> values && values.Count >= 3
                ? new Vector3(Float(values[0]), Float(values[1]), Float(values[2])) : fallback;

        private static Color ColorValue(Dictionary<string, object> data, Color fallback) =>
            data.TryGetValue("color", out object raw) && raw is List<object> values && values.Count >= 4
                ? new Color(Float(values[0]), Float(values[1]), Float(values[2]), Float(values[3])) : fallback;

        private static string Text(Dictionary<string, object> data, string key) =>
            data.TryGetValue(key, out object value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;

        private static bool Boolean(Dictionary<string, object> data, string key, bool fallback) =>
            data.TryGetValue(key, out object value) && value != null ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;

        private static int Integer(Dictionary<string, object> data, string key, int fallback) =>
            data.TryGetValue(key, out object value) && value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;

        private static int Integer(List<object> values, int index) =>
            values != null && index < values.Count ? Convert.ToInt32(values[index], CultureInfo.InvariantCulture) : 0;

        private static float Number(Dictionary<string, object> data, string key, float fallback) =>
            data.TryGetValue(key, out object value) && value != null ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;

        private static float Float(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);

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

        private static string FormatCount(long count)
        {
            double value = count;
            int symbolIndex = 0;
            while (value >= 10000d && symbolIndex < CountSuffixes.Length - 1)
            {
                value /= 10000d;
                symbolIndex++;
            }

            string number = value.ToString(CultureInfo.InvariantCulture);
            int decimalPoint = number.IndexOf('.');
            if (decimalPoint >= 0)
                number = decimalPoint >= 3
                    ? number.Substring(0, decimalPoint)
                    : number.Substring(0, Math.Min(4, number.Length));
            return double.Parse(number, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                + CountSuffixes[symbolIndex];
        }
    }
}
