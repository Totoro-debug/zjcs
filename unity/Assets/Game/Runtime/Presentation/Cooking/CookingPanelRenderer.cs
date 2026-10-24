using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Inventory;

namespace Zjcs.Presentation.Cooking
{
    [Serializable]
    internal sealed class CookingUiData
    {
        public string manifest;
        public CookingPrefabData panel;
        public CookingPrefabData itemRow;
    }

    [Serializable]
    internal sealed class CookingPrefabData
    {
        public List<CookingUiNodeData> nodes = new List<CookingUiNodeData>();
    }

    [Serializable]
    internal sealed class CookingUiNodeData
    {
        public int parentIndex;
        public string name;
        public string path;
        public bool active;
        public bool is_rect_transform;
        public float[] anchor_min;
        public float[] anchor_max;
        public float[] anchored_position;
        public float[] size_delta;
        public float[] pivot;
        public float[] local_position;
        public float[] local_rotation;
        public float[] scale;
        public float rotation;
        public string[] scripts;
        public bool has_image;
        public CookingImageData image;
        public bool has_text;
        public CookingTextData text;
        public bool button;
        public bool item_icon;
        public bool mask;
        public bool rectmask2d;
        public bool has_layout;
        public CookingLayoutData layout;
        public bool has_grid;
        public CookingGridData grid;
        public bool has_size_fitter;
        public int[] size_fitter;
        public bool has_layout_element;
        public CookingLayoutElementData layout_element;
        public bool has_scroll;
        public CookingScrollData scroll;
        public float alpha = 1;
    }

    [Serializable]
    internal sealed class CookingImageData
    {
        public string sprite;
        public float[] color;
        public int type;
        public bool preserve_aspect;
        public bool raycast_target = true;
    }

    [Serializable]
    internal sealed class CookingTextData
    {
        public string value;
        public string font_asset_name;
        public float[] color;
        public float font_size;
        public int horizontal;
        public int vertical;
        public int overflow;
        public bool word_wrap = true;
        public float[] margin;
    }

    [Serializable]
    internal sealed class CookingLayoutData
    {
        public int axis;
        public int[] padding;
        public int alignment;
        public float spacing;
        public bool[] expand;
        public bool[] control;
        public bool[] child_scale;
        public bool reverse;
    }

    [Serializable]
    internal sealed class CookingGridData
    {
        public int[] padding;
        public int alignment;
        public int corner;
        public int axis;
        public int constraint;
        public int count;
        public float[] cell;
        public float[] spacing;
    }

    [Serializable]
    internal sealed class CookingLayoutElementData
    {
        public bool ignore;
        public float[] min;
        public float[] preferred;
        public float[] flexible;
    }

    [Serializable]
    internal sealed class CookingScrollData
    {
        public bool horizontal;
        public bool vertical = true;
        public bool inertia = true;
        public int movement = 1;
        public float sensitivity = 1;
        public float elasticity = 0.1f;
        public float deceleration = 0.135f;
    }

    internal sealed class CookingPanelRenderer
    {
        private readonly Dictionary<string, GameObject> nodes = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> prefabSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<int, Transform> recipeRows = new Dictionary<int, Transform>();
        private readonly Sprite[] allSprites;
        private readonly OfficialInventoryAssetCatalog inventoryAssets;
        private readonly TMP_FontAsset font;
        private readonly CookingUiData data;
        private readonly Action<string, GameObject> buttonBinding;
        private readonly string selectedCountFormat;
        private readonly string defaultMakeLabel;
        private GameObject host;
        private Slider batchSlider;

        public CookingPanelRenderer(Action<string, GameObject> buttonBinding)
        {
            this.buttonBinding = buttonBinding;
            string path = Path.Combine(Application.streamingAssetsPath, "Cooking/ui.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official Cooking UI payload is missing.", path);
            data = JsonUtility.FromJson<CookingUiData>(File.ReadAllText(path));
            if (data == null || data.manifest != "854_156383" || data.panel?.nodes == null || data.itemRow?.nodes == null)
                throw new InvalidDataException("Official Cooking UI payload is invalid or unsupported.");
            selectedCountFormat = data.panel.nodes.FirstOrDefault(node => node.path.EndsWith(
                "/CountSelector/SelectedCountTxt", StringComparison.Ordinal))?.text?.value;
            defaultMakeLabel = data.panel.nodes.FirstOrDefault(node => node.path.EndsWith(
                "/CountGo/MakeBtn/Content/Text", StringComparison.Ordinal))?.text?.value;
            allSprites = Resources.LoadAll<Sprite>("Cooking/Sprites");
            foreach (Sprite sprite in allSprites)
                if (sprite != null)
                {
                    prefabSprites[sprite.name] = sprite;
                    prefabSprites[Path.GetFileNameWithoutExtension(sprite.name)] = sprite;
                }
            try { inventoryAssets = OfficialInventoryAssetCatalog.LoadDefault(); }
            catch (Exception) { inventoryAssets = null; }
            font = Resources.Load<TMP_FontAsset>("Commerce/FZY4K-Dynamic");
        }

        public GameObject Create(Transform parent)
        {
            host = new GameObject("CookingPanelHost", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            host.transform.SetParent(parent, false);
            RectTransform hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 800;
            CanvasScaler scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            BuildTree(data.panel.nodes, host.transform, "panel");
            GameObject grid = FindNode("FoodGrid", "/FoodScrollList/Viewport/FoodGrid");
            if (grid != null && grid.GetComponent<GridLayoutGroup>() == null)
            {
                GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
                layout.cellSize = new Vector2(160, 160);
                layout.spacing = new Vector2(12, 8);
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 2;
                layout.childAlignment = TextAnchor.UpperCenter;
            }
            SetUpScroll();
            Canvas.ForceUpdateCanvases();
            ConfigureGrid();
            return host;
        }

        private void ConfigureGrid()
        {
            GameObject grid = FindNode("FoodGrid", "/FoodScrollList/Viewport/FoodGrid");
            if (grid == null) return;
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            if (layout == null) layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(100, 100);
            layout.spacing = Vector2.zero;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            RectTransform viewport = FindNode("Viewport", "/FoodScrollList/Viewport")?.transform as RectTransform;
            float width = viewport == null ? 100 : viewport.rect.width;
            layout.constraintCount = Mathf.Max(1, Mathf.FloorToInt(width / layout.cellSize.x));
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        public GameObject FindNode(string name, string pathEnding = null)
        {
            if (!string.IsNullOrEmpty(pathEnding))
            {
                foreach (KeyValuePair<string, GameObject> entry in nodes)
                    if (entry.Key.EndsWith(pathEnding, StringComparison.Ordinal)) return entry.Value;
            }
            foreach (KeyValuePair<string, GameObject> entry in nodes)
                if (entry.Value != null && entry.Value.name == name) return entry.Value;
            return null;
        }

        public void SetText(string name, string pathEnding, string value)
        {
            GameObject node = FindNode(name, pathEnding);
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        public Transform RenderRows(IReadOnlyList<Offline.Cooking.CookingEntry> entries,
            Func<Offline.Cooking.CookingEntry, string> label, Func<Offline.Cooking.CookingEntry, Sprite> icon,
            int selectedListItemId)
        {
            Transform content = FindNode("FoodGrid", "/FoodScrollList/Viewport/FoodGrid")?.transform;
            if (content == null) return null;
            for (int index = content.childCount - 1; index >= 0; index--) UnityEngine.Object.Destroy(content.GetChild(index).gameObject);
            recipeRows.Clear();
            Transform defaultRecovery = null;
            foreach (Offline.Cooking.CookingEntry entry in entries)
            {
                GameObject row = CreateRow(content, entry, label(entry), icon(entry), selectedListItemId == entry.ItemId);
                if (entry.Kind == Offline.Cooking.CookingEntryKind.Recipe)
                {
                    recipeRows[entry.RecipeId] = row.transform;
                    if (entry.IsBestRecovery) defaultRecovery = row.transform;
                }
            }
            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            return defaultRecovery;
        }

        public Transform RecipeRow(int recipeId) => recipeRows.TryGetValue(recipeId, out Transform row) ? row : null;

        public void RenderIcon(Transform slot, int itemId, int quality)
        {
            if (slot == null) return;
            Transform root = slot.Find("CookingIconOverlay");
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            var overlay = new GameObject("CookingIconOverlay", typeof(RectTransform));
            RectTransform rect = (RectTransform)overlay.transform;
            rect.SetParent(slot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
            rect.SetAsLastSibling();
            if (inventoryAssets != null)
            {
                Sprite qualitySprite = inventoryAssets.FindQualityBackground(quality);
                if (qualitySprite != null)
                {
                    Image qualityImage = overlay.AddComponent<Image>();
                    qualityImage.sprite = qualitySprite;
                    qualityImage.raycastTarget = false;
                }
            }
            Sprite itemSprite = inventoryAssets == null ? null : inventoryAssets.FindItemSprite(itemId);
            if (itemSprite == null)
                itemSprite = FindSprite(itemId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (itemSprite != null)
            {
                var imageObject = new GameObject("Item", typeof(RectTransform), typeof(Image));
                RectTransform imageRect = (RectTransform)imageObject.transform;
                imageRect.SetParent(overlay.transform, false);
                imageRect.anchorMin = new Vector2(0.12f, 0.12f);
                imageRect.anchorMax = new Vector2(0.88f, 0.88f);
                imageRect.offsetMin = Vector2.zero;
                imageRect.offsetMax = Vector2.zero;
                Image image = imageObject.GetComponent<Image>();
                image.sprite = itemSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        public void SetButton(string name, bool interactable)
        {
            GameObject node = FindNode(name);
            Button button = node == null ? null : node.GetComponent<Button>();
            if (button != null) button.interactable = interactable;
        }

        public void SetNodeActive(string name, string pathEnding, bool active)
        {
            GameObject node = FindNode(name, pathEnding);
            if (node != null) node.SetActive(active);
        }

        public void SetMakeLabel(string text)
        {
            GameObject button = FindNode("MakeBtn", "/CountGo/MakeBtn");
            TMP_Text label = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = string.IsNullOrEmpty(text) ? defaultMakeLabel : text;
        }

        public void BindButton(string exactName, Action action)
        {
            foreach (GameObject node in nodes.Values)
            {
                if (node == null || node.name != exactName) continue;
                Button button = node.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => action?.Invoke());
                }
                return;
            }
        }

        public IReadOnlyList<Transform> MaterialSlots()
        {
            var result = new List<Transform>();
            foreach (KeyValuePair<string, GameObject> entry in nodes)
                if (entry.Value != null && entry.Key.Contains("/FoodMaterial/Materials/ItemIconDefault")
                    && !entry.Key.Substring(entry.Key.IndexOf("/FoodMaterial/Materials/ItemIconDefault", StringComparison.Ordinal)
                        + "/FoodMaterial/Materials/".Length).Contains("/"))
                    result.Add(entry.Value.transform);
            return result.OrderBy(value => value.name == "ItemIconDefault" ? 0
                : ParseTrailingIndex(value.name), Comparer<int>.Default).Take(5).ToArray();
        }

        public void SetMaterialCount(Transform slot, string value)
        {
            if (slot == null) return;
            TMP_Text label = slot.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.gameObject.name == "CountText");
            if (label != null) label.text = value;
        }

        public void SetSelectedCount(long value)
        {
            string format = string.IsNullOrEmpty(selectedCountFormat) ? "{0}" : selectedCountFormat;
            SetText("SelectedCountTxt", "/CountSelector/SelectedCountTxt",
                string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value));
        }

        public void SetRecipeCount(long value)
        {
            Transform output = FindNode("ItemIconDefault", "/FoodMaterial/FoodGo/ItemIconDefault")?.transform;
            if (output != null)
            {
                TMP_Text label = output.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(text => text.gameObject.name == "CountText");
                if (label != null) label.text = value.ToString();
            }
        }

        public void EnsureBatchSlider(Action<float> changed)
        {
            GameObject sliderRoot = FindNode("SliderGo", "/CountSelector/SliderGo");
            if (sliderRoot == null) return;
            Slider slider = sliderRoot.GetComponent<Slider>();
            if (slider == null)
            {
                var overlay = new GameObject("CookingBatchSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
                RectTransform rect = (RectTransform)overlay.transform;
                rect.SetParent(sliderRoot.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(40, 0);
                rect.offsetMax = new Vector2(-40, 0);
                Image background = overlay.GetComponent<Image>();
                background.color = new Color(1, 1, 1, 0.03f);
                background.raycastTarget = true;
                slider = overlay.GetComponent<Slider>();
                var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                RectTransform fillRect = (RectTransform)fillObject.transform;
                fillRect.SetParent(overlay.transform, false);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(1, 1);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                Image fillImage = fillObject.GetComponent<Image>();
                fillImage.color = new Color(0.8f, 0.68f, 0.36f, 0.8f);
                fillImage.raycastTarget = false;
                var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
                RectTransform handleRect = (RectTransform)handleObject.transform;
                handleRect.SetParent(overlay.transform, false);
                handleRect.sizeDelta = new Vector2(32, 32);
                Image handleImage = handleObject.GetComponent<Image>();
                handleImage.sprite = FindSprite("ImgSlider_Handler-db18aab099");
                handleImage.raycastTarget = false;
                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = background;
                slider.direction = Slider.Direction.LeftToRight;
                slider.transition = Selectable.Transition.None;
            }
            batchSlider = slider;
            slider.wholeNumbers = true;
            slider.minValue = 1;
            slider.maxValue = 1;
            slider.value = 1;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(value => changed?.Invoke(value));
        }

        public void SetBatchSlider(long max, long value)
        {
            Slider slider = batchSlider;
            if (slider == null) return;
            slider.minValue = 1;
            slider.maxValue = Math.Max(1, max);
            slider.interactable = max > 1;
            slider.SetValueWithoutNotify(Mathf.Clamp((float)value, 1, Mathf.Max(1, max)));
        }

        public void Close()
        {
            if (host != null) UnityEngine.Object.Destroy(host);
        }

        private void BuildTree(List<CookingUiNodeData> source, Transform parent, string key)
        {
            nodes.Clear();
            GameObject[] objects = new GameObject[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                CookingUiNodeData node = source[index];
                GameObject gameObject = node.is_rect_transform
                    ? new GameObject(node.name, typeof(RectTransform)) : new GameObject(node.name);
                Transform actualParent = node.parentIndex >= 0 && node.parentIndex < index
                    ? objects[node.parentIndex].transform : parent;
                gameObject.transform.SetParent(actualParent, false);
                RectTransform rect = gameObject.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = Vector(node.anchor_min, new Vector2(0.5f, 0.5f));
                    rect.anchorMax = Vector(node.anchor_max, new Vector2(0.5f, 0.5f));
                    rect.anchoredPosition = Vector(node.anchored_position, Vector2.zero);
                    rect.sizeDelta = Vector(node.size_delta, Vector2.zero);
                    rect.pivot = Vector(node.pivot, new Vector2(0.5f, 0.5f));
                    rect.localScale = Vector(node.scale, Vector3.one);
                    rect.localEulerAngles = new Vector3(0, 0, node.rotation);
                }
                else
                {
                    gameObject.transform.localPosition = Vector(node.local_position, Vector3.zero);
                    gameObject.transform.localRotation = QuaternionValue(node.local_rotation);
                    gameObject.transform.localScale = Vector(node.scale, Vector3.one);
                }
                gameObject.SetActive(node.active);
                objects[index] = gameObject;
                nodes[node.path] = gameObject;

                Image image = node.has_image ? CreateImage(node.image, gameObject) : null;
                if (node.has_text && node.text != null) CreateText(node.text, gameObject);
                if (node.mask && image != null) gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (node.rectmask2d) gameObject.AddComponent<RectMask2D>();
                if (node.has_layout && node.layout != null) CreateLayout(node.layout, gameObject);
                if (node.has_grid && node.grid != null) CreateGrid(node.grid, gameObject);
                if (node.has_size_fitter && node.size_fitter != null && node.size_fitter.Length >= 2)
                {
                    ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(node.size_fitter[0], 0, 2);
                    fitter.verticalFit = (ContentSizeFitter.FitMode)Mathf.Clamp(node.size_fitter[1], 0, 2);
                }
                if (node.has_layout_element && node.layout_element != null) CreateLayoutElement(node.layout_element, gameObject);
                if (node.alpha < 0.999f) gameObject.AddComponent<CanvasGroup>().alpha = Mathf.Clamp01(node.alpha);
                if (node.button || HasScript(node, "Button"))
                {
                    if (image == null && gameObject.GetComponentInChildren<Graphic>() == null)
                    {
                        image = gameObject.AddComponent<Image>();
                        image.color = new Color(1, 1, 1, 0.001f);
                    }
                    Button button = gameObject.AddComponent<Button>();
                    button.targetGraphic = image != null ? image : gameObject.GetComponentInChildren<Graphic>();
                    buttonBinding?.Invoke(node.path, gameObject);
                }
            }
        }

        private GameObject CreateRow(Transform parent, Offline.Cooking.CookingEntry entry,
            string label, Sprite icon, bool selected)
        {
            CookingPrefabData source = data.itemRow;
            var rowRoot = new GameObject("CookingRow_" + entry.ItemId, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rootRect = (RectTransform)rowRoot.transform;
            rootRect.SetParent(parent, false);
            rootRect.sizeDelta = new Vector2(100, 100);
            rootRect.localScale = Vector3.one;
            Image background = rowRoot.GetComponent<Image>();
            background.sprite = FindSprite("ImgBg_List-d73b106df0");
            background.color = new Color(1, 1, 1, 0.95f);
            Button button = rowRoot.GetComponent<Button>();
            button.targetGraphic = background;
            buttonBinding?.Invoke("CookingEntry:" + entry.ItemId, rowRoot);
            GameObject original = CreateTemplate(source.nodes, rowRoot.transform);
            Transform itemSlot = FindInTree(original.transform, "itemSlot");
            if (itemSlot == null) itemSlot = original.transform;
            Image selection = new GameObject("Selected", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            selection.transform.SetParent(original.transform, false);
            RectTransform selectionRect = (RectTransform)selection.transform;
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = Vector2.zero;
            selectionRect.offsetMax = Vector2.zero;
            selection.sprite = FindSprite("ImgItemSelect-de48b6abe9");
            selection.color = Color.white;
            selection.raycastTarget = false;
            selection.gameObject.SetActive(selected);
            RenderStandaloneIcon(itemSlot, entry.ItemId, icon);
            var textObject = new GameObject("CookingLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = (RectTransform)textObject.transform;
            labelRect.SetParent(original.transform, false);
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 0.3f);
            labelRect.offsetMin = new Vector2(0, 3);
            labelRect.offsetMax = new Vector2(0, 0);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = label;
            text.fontSize = 15;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            if (font != null) text.font = font;
            if (entry.Kind == Offline.Cooking.CookingEntryKind.Formula)
            {
                var formula = new GameObject("FormulaBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform formulaRect = (RectTransform)formula.transform;
                formulaRect.SetParent(original.transform, false);
                formulaRect.anchorMin = new Vector2(0, 0.7f);
                formulaRect.anchorMax = new Vector2(1, 1);
                formulaRect.offsetMin = Vector2.zero;
                formulaRect.offsetMax = Vector2.zero;
                TMP_Text formulaText = formula.GetComponent<TMP_Text>();
                formulaText.text = "配方";
                formulaText.fontSize = 14;
                formulaText.color = new Color(1, 0.8f, 0.35f);
                formulaText.alignment = TextAlignmentOptions.Center;
                formulaText.raycastTarget = false;
                if (font != null) formulaText.font = font;
            }
            return rowRoot;
        }

        private GameObject CreateTemplate(List<CookingUiNodeData> source, Transform parent)
        {
            var objects = new GameObject[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                CookingUiNodeData node = source[index];
                GameObject gameObject = node.is_rect_transform
                    ? new GameObject(node.name, typeof(RectTransform)) : new GameObject(node.name);
                Transform actualParent = node.parentIndex >= 0 && node.parentIndex < index
                    ? objects[node.parentIndex].transform : parent;
                gameObject.transform.SetParent(actualParent, false);
                RectTransform rect = gameObject.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = Vector(node.anchor_min, new Vector2(0.5f, 0.5f));
                    rect.anchorMax = Vector(node.anchor_max, new Vector2(0.5f, 0.5f));
                    rect.anchoredPosition = Vector(node.anchored_position, Vector2.zero);
                    rect.sizeDelta = Vector(node.size_delta, Vector2.zero);
                    rect.pivot = Vector(node.pivot, new Vector2(0.5f, 0.5f));
                    rect.localScale = Vector(node.scale, Vector3.one);
                    rect.localEulerAngles = new Vector3(0, 0, node.rotation);
                }
                gameObject.SetActive(node.active);
                objects[index] = gameObject;
                if (node.has_image && node.image != null) CreateImage(node.image, gameObject);
                if (node.has_text && node.text != null) CreateText(node.text, gameObject);
            }
            return objects[0];
        }

        private void RenderStandaloneIcon(Transform parent, int itemId, Sprite icon)
        {
            if (icon == null && inventoryAssets != null) icon = inventoryAssets.FindItemSprite(itemId);
            if (icon == null) return;
            var imageObject = new GameObject("CookingItemIcon", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.1f, 0.14f);
            rect.anchorMax = new Vector2(0.9f, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void SetUpScroll()
        {
            GameObject scrollObject = FindNode("FoodScrollList", "/FoodScrollList");
            if (scrollObject == null) return;
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            if (scroll == null) scroll = scrollObject.AddComponent<ScrollRect>();
            Transform viewport = FindInTree(scrollObject.transform, "Viewport");
            Transform content = FindInTree(scrollObject.transform, "FoodGrid");
            if (viewport == null || content == null) return;
            scroll.viewport = viewport as RectTransform;
            scroll.content = content as RectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 36;
        }

        private Image CreateImage(CookingImageData source, GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.AddComponent<Image>();
            image.sprite = ResolveSprite(source?.sprite);
            image.type = (Image.Type)Mathf.Clamp(source?.type ?? 0, 0, 3);
            image.preserveAspect = source != null && source.preserve_aspect;
            image.raycastTarget = source == null || source.raycast_target;
            image.color = ColorValue(source?.color, Color.white);
            return image;
        }

        private void CreateText(CookingTextData source, GameObject target)
        {
            TMP_Text text = target.GetComponent<TMP_Text>();
            if (text == null) text = target.AddComponent<TextMeshProUGUI>();
            text.text = source.value ?? string.Empty;
            if (source.font_size > 0) text.fontSize = source.font_size;
            text.color = ColorValue(source.color, Color.white);
            text.alignment = (TextAlignmentOptions)(source.horizontal | source.vertical);
            text.enableWordWrapping = source.word_wrap;
            text.overflowMode = (TextOverflowModes)Mathf.Clamp(source.overflow, 0, 4);
            if (source.margin != null && source.margin.Length >= 4)
                text.margin = new Vector4(source.margin[0], source.margin[1], source.margin[2], source.margin[3]);
            if (font != null) text.font = font;
            text.raycastTarget = false;
        }

        private static void CreateLayout(CookingLayoutData source, GameObject target)
        {
            HorizontalOrVerticalLayoutGroup group = source.axis == 0
                ? (HorizontalOrVerticalLayoutGroup)target.AddComponent<HorizontalLayoutGroup>()
                : target.AddComponent<VerticalLayoutGroup>();
            if (source.padding != null && source.padding.Length >= 4)
                group.padding = new RectOffset(source.padding[0], source.padding[1], source.padding[2], source.padding[3]);
            group.childAlignment = (TextAnchor)Mathf.Clamp(source.alignment, 0, 8);
            group.spacing = source.spacing;
            group.childForceExpandWidth = source.expand != null && source.expand.Length > 0 && source.expand[0];
            group.childForceExpandHeight = source.expand != null && source.expand.Length > 1 && source.expand[1];
            group.childControlWidth = source.control == null || source.control.Length == 0 || source.control[0];
            group.childControlHeight = source.control == null || source.control.Length < 2 || source.control[1];
            group.childScaleWidth = source.child_scale != null && source.child_scale.Length > 0 && source.child_scale[0];
            group.childScaleHeight = source.child_scale != null && source.child_scale.Length > 1 && source.child_scale[1];
            group.reverseArrangement = source.reverse;
        }

        private static void CreateGrid(CookingGridData source, GameObject target)
        {
            GridLayoutGroup group = target.AddComponent<GridLayoutGroup>();
            if (source.padding != null && source.padding.Length >= 4)
                group.padding = new RectOffset(source.padding[0], source.padding[1], source.padding[2], source.padding[3]);
            group.childAlignment = (TextAnchor)Mathf.Clamp(source.alignment, 0, 8);
            group.startCorner = (GridLayoutGroup.Corner)Mathf.Clamp(source.corner, 0, 3);
            group.startAxis = (GridLayoutGroup.Axis)Mathf.Clamp(source.axis, 0, 1);
            group.constraint = (GridLayoutGroup.Constraint)Mathf.Clamp(source.constraint, 0, 2);
            group.constraintCount = Mathf.Max(1, source.count);
            group.cellSize = Vector(source.cell, new Vector2(100, 100));
            group.spacing = Vector(source.spacing, Vector2.zero);
        }

        private static void CreateLayoutElement(CookingLayoutElementData source, GameObject target)
        {
            LayoutElement element = target.AddComponent<LayoutElement>();
            element.ignoreLayout = source.ignore;
            if (source.min != null && source.min.Length >= 2)
            {
                element.minWidth = source.min[0];
                element.minHeight = source.min[1];
            }
            if (source.preferred != null && source.preferred.Length >= 2)
            {
                element.preferredWidth = source.preferred[0];
                element.preferredHeight = source.preferred[1];
            }
            if (source.flexible != null && source.flexible.Length >= 2)
            {
                element.flexibleWidth = source.flexible[0];
                element.flexibleHeight = source.flexible[1];
            }
        }

        private Sprite ResolveSprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string name = Path.GetFileNameWithoutExtension(key);
            return prefabSprites.TryGetValue(name, out Sprite sprite) ? sprite : null;
        }

        private Sprite FindSprite(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return null;
            foreach (KeyValuePair<string, Sprite> entry in prefabSprites)
                if (entry.Key.StartsWith(prefix, StringComparison.Ordinal)) return entry.Value;
            return null;
        }

        private static Transform FindInTree(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindInTree(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool HasScript(CookingUiNodeData node, string script)
        {
            if (node.scripts == null) return false;
            foreach (string value in node.scripts) if (value == script) return true;
            return false;
        }

        private static Vector2 Vector(float[] value, Vector2 fallback)
            => value == null || value.Length < 2 ? fallback : new Vector2(value[0], value[1]);
        private static Vector3 Vector(float[] value, Vector3 fallback)
            => value == null || value.Length < 3 ? fallback : new Vector3(value[0], value[1], value[2]);
        private static Quaternion QuaternionValue(float[] value)
            => value == null || value.Length < 4 ? Quaternion.identity : new Quaternion(value[0], value[1], value[2], value[3]);
        private static Color ColorValue(float[] value, Color fallback)
            => value == null || value.Length < 4 ? fallback : new Color(value[0], value[1], value[2], value[3]);

        private static int ParseTrailingIndex(string value)
        {
            int open = value.LastIndexOf('(');
            int close = value.LastIndexOf(')');
            return open >= 0 && close > open && int.TryParse(value.Substring(open + 1, close - open - 1), out int index)
                ? index : int.MaxValue;
        }
    }
}
