using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.State;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Inventory;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime : LoopScrollDataSource
    {
        private OfflineNationShopService map13NationShop;
        private OfficialNationShopCatalog map13NationShopCatalog;
        private OfficialMap11WitchWindow map13NationShopWindow;
        private GameObject map13NationShopRoot;
        private Action map13NationShopClosed;
        private int map13NationShopTargetLevel;
        private IReadOnlyList<OfficialNationShopGood> map13NationShopGoods = Array.Empty<OfficialNationShopGood>();
        private bool map13NationShopGoodsLocked;
        private OfficialMap11UiAssets map13NationShopUiAssets;
        private OfficialInventoryAssetCatalog map13NationShopInventoryAssets;
        private OfficialCharacterCatalog map13NationShopItemCatalog;

        private void OpenMap13NationShop(Action closedAction)
        {
            if (map13NationShopRoot != null) throw new InvalidOperationException("The Map13 nation shop is already open.");
            if (progressionSave == null || string.IsNullOrEmpty(slotId) || hud?.ui == null)
                throw new InvalidOperationException("The Map13 nation shop requires an initialized profile and native UI.");

            ProfileSlotCatalog slots = new ProfileSlotCatalog(persistentDataPath);
            OfflineCommerceSession commerce = OfflineCommerceSession.Open(slots, slotId, DateTime.Now, progressionSave);
            map13NationShopCatalog = OfficialNationShopCatalog.LoadDefault();
            map13NationShop = new OfflineNationShopService(map13NationShopCatalog, commerce);
            if (!map13NationShopCatalog.TryGroup(13, out _))
                throw new InvalidDataException("The official Map13 nation-shop group is missing.");

            map13NationShopClosed = closedAction;
            map13NationShopWindow = new OfficialMap11WitchWindow("MapNationShopPanel", hud.ui.OverlayRoot,
                NativeUi.Data, MapProfile.NativeUiRoot);
            map13NationShopRoot = map13NationShopWindow.Root;
            map13NationShopRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            map13NationShopTargetLevel = map13NationShop.Level(13);
            BindMap13NationShopClose();
            BindMap13NationShopTabs();
            RefreshMap13NationShop();
        }

        private void BindMap13NationShopClose()
        {
            Button close = map13NationShopWindow.Find("/MapNationShopPanel/BgPanel1/Content/BottomGo/CloseBtn_dark").GetComponent<Button>();
            if (close == null) throw new InvalidDataException("Official Map13 nation-shop close button is missing.");
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => CloseMap13NationShop(true));
        }

        private void BindMap13NationShopTabs()
        {
            const string root = "/MapNationShopPanel/BgPanel1/Content/BottomGo/Tab/TabGroup";
            foreach (string tab in new[] { "Shop", "Achievement" })
            {
                Button button = map13NationShopWindow.Find(root + "/" + tab).GetComponent<Button>();
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowMap13NationShopPage(tab));
            }
            ShowMap13NationShopPage("Shop");
        }

        private void ShowMap13NationShopPage(string page)
        {
            map13NationShopWindow.Find("/MapNationShopPanel/BgPanel1/Content/MiddleGo/ShopContent")
                .gameObject.SetActive(page == "Shop");
            map13NationShopWindow.Find("/MapNationShopPanel/BgPanel1/Content/MiddleGo/AchievementPanel")
                .gameObject.SetActive(page == "Achievement");
        }

        private void RefreshMap13NationShop()
        {
            if (map13NationShopRoot == null || map13NationShop == null) return;
            OfficialNationShopLevel[] levels = map13NationShopCatalog.Levels(13).ToArray();
            int currentLevel = map13NationShop.Level(13);
            OfficialNationShopLevel target = levels.Single(value => value.level == map13NationShopTargetLevel);
            OfficialNationShopLevel current = levels.Single(value => value.level == currentLevel);
            string root = "/MapNationShopPanel/BgPanel1/Content/TopGo/ShopLevelGo";
            map13NationShopWindow.SetText(root + "/LevelGo/LevelText", string.Format(CultureInfo.CurrentCulture,
                NativeUi.Text("ui_active_pay_panel_shop_level_text"), currentLevel, levels.Length));
            long nextBoundary = levels.Where(value => value.level > currentLevel)
                .Select(value => value.experienceBoundary).DefaultIfEmpty(current.experienceBoundary).First();
            bool maxed = currentLevel == levels.Length;
            map13NationShopWindow.SetText(root + "/ExpText", maxed ? string.Empty : string.Format(CultureInfo.CurrentCulture,
                "{0}/{1}", map13NationShop.Experience(13) - current.experienceBoundary,
                nextBoundary - current.experienceBoundary));
            map13NationShopWindow.Find(root + "/ExpText").gameObject.SetActive(!maxed);
            map13NationShopWindow.Find(root + "/MaxLevelText").gameObject.SetActive(maxed);
            Slider exp = map13NationShopWindow.Find(root + "/Slider").GetComponent<Slider>();
            if (exp != null)
                exp.value = maxed ? 1f : Mathf.Clamp01((float)(map13NationShop.Experience(13) - current.experienceBoundary)
                    / Math.Max(1L, nextBoundary - current.experienceBoundary));
            RefreshMap13NationShopLevels(levels, map13NationShopTargetLevel, currentLevel);
            RefreshMap13NationShopGoods(target, currentLevel);
        }

        private void RefreshMap13NationShopLevels(OfficialNationShopLevel[] levels, int targetLevel, int currentLevel)
        {
            const string path = "/MapNationShopPanel/BgPanel1/Content/MiddleGo/ShopLevelTab/TabGroup";
            Transform tabs = map13NationShopWindow.Find(path);
            WorldTabGroup group = tabs.GetComponent<WorldTabGroup>();
            if (group != null)
                foreach (WorldTabItem oldTab in group.GetAllTabs().ToArray()) group.UnregisterTab(oldTab);
            foreach (Transform child in tabs.Cast<Transform>().ToArray()) Destroy(child.gameObject);
            if (group == null) group = tabs.gameObject.AddComponent<WorldTabGroup>();
            GameObject prefab = Resources.Load<GameObject>(MapProfile.NativeUi("ShopLevelTabItem"));
            if (prefab == null) throw new InvalidDataException("Official Map13 nation-shop level-tab prefab is missing.");
            foreach (OfficialNationShopLevel level in levels)
            {
                GameObject instance = Instantiate(prefab, tabs, false);
                instance.name = "ShopLevelTabItem_" + level.level.ToString(CultureInfo.InvariantCulture);
                TMP_Text label = instance.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = string.Format(CultureInfo.CurrentCulture,
                    NativeUi.Text("ui_MapNationShopPanel_Level_Title"), level.level);
                Transform lockMark = instance.transform.Find("LockGo");
                if (lockMark != null) lockMark.gameObject.SetActive(level.level > currentLevel);
                WorldTabItem tab = instance.GetComponent<WorldTabItem>();
                if (tab == null) tab = instance.AddComponent<WorldTabItem>();
                tab.graphic = instance.transform.Find("Selected")?.GetComponent<Graphic>();
                tab.SetGroup(group);
                int selected = level.level;
                tab.OnValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    map13NationShopTargetLevel = selected;
                    RefreshMap13NationShop();
                });
                tab.SetIsOnWithoutNotify(level.level == targetLevel, true);
                instance.SetActive(true);
            }
        }

        private void RefreshMap13NationShopGoods(OfficialNationShopLevel target, int currentLevel)
        {
            const string panelRoot = "/MapNationShopPanel/BgPanel1/Content/MiddleGo/ShopContent/ShopContent";
            string lockTip = panelRoot + "/LockTipGo";
            bool isLocked = target.level > currentLevel;
            map13NationShopWindow.Find(lockTip).gameObject.SetActive(isLocked);
            map13NationShopWindow.Find(panelRoot + "/UnlockContentGroup").gameObject.SetActive(!isLocked);
            map13NationShopWindow.Find(panelRoot + "/LockContentGroup").gameObject.SetActive(isLocked);
            LoopVerticalScrollRect scroll = map13NationShopWindow.Find(isLocked
                ? "/MapNationShopPanel/BgPanel1/Content/MiddleGo/ShopContent/ShopContent/LockContentGroup/GoodsGrid"
                : "/MapNationShopPanel/BgPanel1/Content/MiddleGo/ShopContent/ShopContent/UnlockContentGroup/GoodsGrid")
                .GetComponent<LoopVerticalScrollRect>();
            if (scroll == null) throw new InvalidDataException("Official Map13 nation-shop goods list is missing its LoopVerticalScrollRect.");
            GameObject prefab = Resources.Load<GameObject>(MapProfile.NativeUi("ShopGoodsItem"));
            if (prefab == null) throw new InvalidDataException("Official Map13 nation-shop goods prefab is missing.");
            GridLayoutGroup layout = scroll.content.GetComponent<GridLayoutGroup>();
            if (layout == null) layout = scroll.content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = ((RectTransform)prefab.transform).sizeDelta;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Mathf.Min(3, Mathf.Max(1, Mathf.FloorToInt((scroll.viewport.rect.width + layout.spacing.x)
                / (layout.cellSize.x + layout.spacing.x))));
            layout.childAlignment = TextAnchor.UpperCenter;
            OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
            if (assets == null) throw new InvalidDataException("Official Map13 dynamic item bindings are missing.");
            OfficialInventoryAssetCatalog inventoryAssets = OfficialInventoryAssetCatalog.LoadDefault();
            OfficialCharacterCatalog itemCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            map13NationShopGoods = map13NationShopCatalog.Goods(target.shopId);
            map13NationShopGoodsLocked = isLocked;
            map13NationShopUiAssets = assets;
            map13NationShopInventoryAssets = inventoryAssets;
            map13NationShopItemCatalog = itemCatalog;
            scroll.ClearCells();
            if (scroll.DefaultPrefabSource == null)
                scroll.DefaultPrefabSource = new LoopScrollSinglePrefabSource { Prefab = prefab };
            scroll.dataSource = this;
            scroll.totalCount = map13NationShopGoods.Count;
            Canvas.ForceUpdateCanvases();
            scroll.RefillCells();
            Transform emptyTip = map13NationShopWindow.Find(panelRoot + (isLocked ? "/LockContentGroup/EmptyTip" : "/UnlockContentGroup/EmptyTip"));
            emptyTip.gameObject.SetActive(scroll.totalCount == 0);
        }

        public void ProvideData(Transform transform, int index)
        {
            if (index < 0 || index >= map13NationShopGoods.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Official Map13 nation-shop goods index is invalid.");
            BindMap13NationShopGood(transform, map13NationShopGoods[index], map13NationShopGoodsLocked,
                map13NationShopUiAssets, map13NationShopInventoryAssets, map13NationShopItemCatalog);
        }

        private void BindMap13NationShopGood(Transform transform, OfficialNationShopGood good, bool isLocked,
            OfficialMap11UiAssets assets, OfficialInventoryAssetCatalog inventoryAssets, OfficialCharacterCatalog itemCatalog)
        {
            transform.name = "ShopGoodsItem_" + good.id.ToString(CultureInfo.InvariantCulture);
            OfficialMap11WitchWindow row = new OfficialMap11WitchWindow("ShopGoodsItem", transform.gameObject,
                NativeUi.Data, MapProfile.NativeUiRoot);
            CanvasGroup itemGroup = transform.GetComponent<CanvasGroup>();
            if (itemGroup != null)
            {
                itemGroup.alpha = 1f;
                itemGroup.interactable = true;
                itemGroup.blocksRaycasts = true;
            }
            Transform iconSlot = row.Find("/ShopGoodsItem/Content/ImgBg/ItemInfoSlot");
            iconSlot.SetAsLastSibling();
            GameObject iconPrefab = Resources.Load<GameObject>(NativeUiResource("ItemIconDefault"));
            if (iconPrefab == null) throw new InvalidDataException("Official Map13 item icon prefab is missing.");
            WorldItemIcon itemIcon = iconSlot.GetComponentInChildren<WorldItemIcon>(true);
            if (itemIcon == null) itemIcon = Instantiate(iconPrefab, iconSlot, false).GetComponent<WorldItemIcon>();
            Sprite itemSprite = inventoryAssets.FindItemSprite(good.itemId) ?? assets.Item(good.itemId);
            var itemDefinition = itemCatalog.FindItem(good.itemId);
            Sprite qualitySprite = itemIcon.QualityImg.sprite;
            if (itemDefinition != null && itemDefinition.TryGetValue("Quality", out object qualityValue))
            {
                int quality = Convert.ToInt32(qualityValue, CultureInfo.InvariantCulture);
                qualitySprite = inventoryAssets.FindQualityBackground(quality) ?? assets.ItemQuality(good.itemId);
            }
            else
            {
                try { qualitySprite = assets.ItemQuality(good.itemId); }
                catch (InvalidOperationException) { }
            }
            itemIcon.Init(itemSprite, qualitySprite, good.itemCount.ToString(CultureInfo.InvariantCulture));
            row.SetText("/ShopGoodsItem/Content/ImgBg/Inventory", progressionSave.InventoryCount(good.itemId)
                .ToString(CultureInfo.InvariantCulture));
            row.SetText("/ShopGoodsItem/Content/ImgBg/BuyBtn/Content/Text/PresentPriceText",
                good.price.ToString(CultureInfo.InvariantCulture));
            Sprite currencySprite = inventoryAssets.FindItemSprite(good.currencyItemId) ?? assets.Item(good.currencyItemId);
            row.Find("/ShopGoodsItem/Content/ImgBg/BuyBtn/Content/Text/MoneyImg").GetComponent<Image>().sprite = currencySprite;
            Button buy = row.Find("/ShopGoodsItem/Content/ImgBg/BuyBtn").GetComponent<Button>();
            buy.interactable = !isLocked && map13NationShop.RemainingStock(good) != 0;
            int goodId = good.id;
            buy.onClick.RemoveAllListeners();
            buy.onClick.AddListener(() => PurchaseMap13NationShopGood(goodId));
        }

        private void PurchaseMap13NationShopGood(int goodId)
        {
            NationShopPurchaseResult result = map13NationShop.Purchase(13, goodId);
            hud.ShowOfficialNotice(result.Message);
            if (result.Code == NationShopPurchaseCode.Applied) RefreshMap13NationShop();
        }

        private void CloseMap13NationShop(bool resumeStory)
        {
            if (map13NationShopRoot == null) return;
            Destroy(map13NationShopRoot);
            map13NationShopRoot = null;
            map13NationShopWindow = null;
            map13NationShop = null;
            map13NationShopCatalog = null;
            Action closedAction = map13NationShopClosed;
            map13NationShopClosed = null;
            if (resumeStory) closedAction?.Invoke();
        }
    }
}
