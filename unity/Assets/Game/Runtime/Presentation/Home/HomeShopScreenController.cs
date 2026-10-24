using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    /// <summary>Opens the official home-shop prefab and serves the confirmed offline fixed goods.</summary>
    [DisallowMultipleComponent]
    public sealed class HomeShopScreenController : MonoBehaviour
    {
        private enum Page
        {
            Grocery,
            Treasury,
            Gifts
        }

        private const string RootPath = "/HomeShopPanel";
        private const string ShopContentPath = RootPath + "/BgPanel1/Content/MiddleGo/ShopContent";
        private const string ShopGridPath = ShopContentPath + "/ShopContent/ShopContentGroup/GoodsGrid";
        private const string ShopGridContentPath = ShopGridPath + "/ViewPort/Content";
        private const string ShopEmptyPath = ShopContentPath + "/ShopContent/ShopContentGroup/EmptyTip";
        private const string RefreshGoPath = ShopContentPath + "/TopGo/RefreshGo";
        private const string RefreshButtonPath = RefreshGoPath + "/RefreshInfoGo/RefreshBtn/CommonBtn_CostItem";
        private const string AchievementRootPath = RootPath + "/BgPanel1/Content/AchievementPanel";
        private const string AchievementScrollPath = AchievementRootPath + "/BgPanel1/Content/MiddleGo/ScrollRect";
        private const string AchievementGridPath = AchievementScrollPath + "/ViewPort/Grid";
        private const string AchievementEmptyPath = AchievementRootPath + "/BgPanel1/Content/MiddleGo/EmptyGo";
        private const int GiftVisibleRows = 5;
        private const string VisitRootPath = RootPath + "/BgPanel1/Content/HomeVisitShopContent";
        private const string VisitTabPath = RootPath + "/BgPanel1/Content/BottomGo/TabBottom/TabGroup/HomeVisitShopTabButtomBtn";
        private const string FirstTabPath = RootPath + "/BgPanel1/Content/BottomGo/TabBottom/TabGroup/HomeShopPanelTabBottomBtn";
        private const string SecondTabPath = RootPath + "/BgPanel1/Content/BottomGo/TabBottom/TabGroup/HomeShopPanelTabBottomBtn (1)";
        private const string GiftTabPath = RootPath + "/BgPanel1/Content/BottomGo/TabBottom/TabGroup/AchievementTabBottomBtn";

        private readonly List<GoodsRow> rows = new List<GoodsRow>();
        private HomeFeatureRuntime host;
        private HomeUiRenderer panelRenderer;
        private GameObject panelRoot;
        private HomeUiRenderer detailRenderer;
        private GameObject detailRoot;
        private OfflineHomeShopService service;
        private Action closed;
        private Page page;
        private int selectedRow;
        private bool firstPageSwitch = true;
        private bool closing;

        public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;
        public int CurrentPageIndex => (int)page;

        public void CloseScreen() => Close();

        public bool SelectPage(int pageIndex)
        {
            if (pageIndex < (int)Page.Grocery || pageIndex > (int)Page.Gifts) return false;
            ShowPage((Page)pageIndex);
            return true;
        }

        public static HomeShopScreenController Open(HomeFeatureRuntime host, Transform parent, Action closed = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            GameObject owner = new GameObject("HomeShopScreenController");
            HomeShopScreenController controller = owner.AddComponent<HomeShopScreenController>();
            try
            {
                controller.Initialize(host, parent, closed);
                owner.transform.SetParent(controller.panelRoot.transform, false);
                return controller;
            }
            catch
            {
                if (controller.panelRoot != null) Destroy(controller.panelRoot);
                if (owner != null) Destroy(owner);
                throw;
            }
        }

        private void Initialize(HomeFeatureRuntime home, Transform parent, Action onClosed)
        {
            host = home;
            closed = onClosed;
            string folder = Path.Combine(Application.streamingAssetsPath, "HomeShop");
            string extensionPath = Path.Combine(folder, "ui.json");
            if (!host.Catalog.HasPanel("home_shop")) host.Catalog.LoadPanelExtension(extensionPath);
            OfficialHomeShopCatalog catalog = OfficialHomeShopCatalog.Load(Path.Combine(folder, "rules.json"));
            service = new OfflineHomeShopService(host.Profile, catalog);

            panelRenderer = new HomeUiRenderer(host.Catalog);
            panelRoot = panelRenderer.Create("home_shop", parent, BindPanelControl);
            panelRoot.name = "HomeShopPanel";
            panelRoot.SetActive(true);
            ConfigureGoodsGrid();
            panelRenderer.SetActive(VisitRootPath, false);
            panelRenderer.SetActive(VisitTabPath, false);
            panelRenderer.SetActive(ShopContentPath + "/ShopContent/ShopContentGroup/ShopBuyBatchContent", false);
            panelRenderer.SetActive(ShopEmptyPath, false);

            detailRenderer = new HomeUiRenderer(host.Catalog);
            detailRoot = detailRenderer.Create("home_shop_item", panelRoot.transform);
            detailRoot.name = "HomeShopItemDetail";
            detailRoot.SetActive(false);
            ShowPage(Page.Grocery);
        }

        private void ConfigureGoodsGrid()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = panelRenderer.Find(ShopGridPath + "/ViewPort")?.GetComponent<RectTransform>();
            GameObject content = panelRenderer.Find(ShopGridContentPath);
            GridLayoutGroup grid = content == null ? null : content.GetComponent<GridLayoutGroup>();
            if (viewport == null || grid == null)
                throw new InvalidDataException("The official HomeShop grid layout is missing.");

            if (grid.spacing.x >= 0f) return;
            int columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? grid.constraintCount : 1;
            if (columns < 2) return;
            float freeWidth = viewport.rect.width - grid.padding.horizontal - grid.cellSize.x * columns;
            float fittedSpacing = Mathf.Max(0f, freeWidth / (columns - 1));
            grid.spacing = new Vector2(fittedSpacing, grid.spacing.y);
            Canvas.ForceUpdateCanvases();
        }

        private void BindPanelControl(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.EndsWith("/CloseBtn_dark", StringComparison.Ordinal))
            {
                button.onClick.AddListener(Close);
            }
            else if (path == FirstTabPath)
            {
                button.onClick.AddListener(() => ShowPage(Page.Grocery));
            }
            else if (path == SecondTabPath)
            {
                button.onClick.AddListener(() => ShowPage(Page.Treasury));
            }
            else if (path == GiftTabPath)
            {
                button.onClick.AddListener(() => ShowPage(Page.Gifts));
            }
            else if (path == RefreshButtonPath)
            {
                button.onClick.AddListener(RefreshGrocery);
            }
        }

        private void BuildRows(IReadOnlyList<OfflineHomeShopShelfItem> shelf)
        {
            foreach (GoodsRow row in rows)
            {
                row.Root.SetActive(false);
                Destroy(row.Root);
            }
            rows.Clear();
            GameObject gridContent = panelRenderer.Find(ShopGridContentPath);
            Transform parent = gridContent == null ? null : gridContent.transform;
            if (parent == null) throw new InvalidDataException("The official HomeShop goods grid is missing.");
            foreach (OfflineHomeShopShelfItem stock in shelf)
            {
                var rowRenderer = new HomeUiRenderer(host.Catalog);
                GameObject rowRoot = rowRenderer.Create("home_shop_goods", parent);
                rowRoot.name = "ShopGoodsItem_" + stock.Goods.goods_id;
                rowRoot.SetActive(true);
                if (!rowRenderer.PlayAnimation("ShopGoodsItem_In"))
                    throw new InvalidDataException("The official HomeShop item entrance animation could not start.");
                var row = new GoodsRow(rowRoot, stock);
                rows.Add(row);
                BindGoodsRow(row);
            }
            if (rows.Count > 0)
                EventSystem.current?.SetSelectedGameObject(rows[0].BuyButton.gameObject);
        }

        private void BindGoodsRow(GoodsRow row)
        {
            Image itemImage = FindComponent<Image>(row.Root, "ItemImg");
            if (itemImage == null)
            {
                Transform slot = FindTransform(row.Root, "ItemInfoSlot");
                if (slot == null) throw new InvalidDataException("The official HomeShop goods icon slot is missing.");
                var iconObject = new GameObject("ItemImg", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(slot, false);
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(142f, 142f);
                itemImage = iconObject.GetComponent<Image>();
                itemImage.preserveAspect = true;
            }
            itemImage.sprite = host.Catalog.FindSprite(row.Offer.icon_file);
            itemImage.enabled = itemImage.sprite != null;
            SetItemCount(itemImage.transform, row.Offer.item_count, host.Catalog.Font);
            Image moneyImage = FindComponent<Image>(row.Root, "MoneyImg");
            if (moneyImage != null)
            {
                moneyImage.sprite = host.Catalog.FindSprite(row.Offer.currency_icon_file);
                moneyImage.enabled = moneyImage.sprite != null;
            }

            row.QuantityLabel = FindComponent<TMP_Text>(row.Root, "Inventory");
            row.PriceLabel = FindComponent<TMP_Text>(row.Root, "PresentPriceText");
            row.StatusLabel = FindComponent<TMP_Text>(row.Root, "BottomTxt");
            Transform buyNode = FindTransform(row.Root, "BuyBtn");
            Transform infoNode = FindTransform(row.Root, "ItemInfoBtn");
            row.BuyButton = buyNode == null ? null : buyNode.GetComponent<Button>();
            Button infoButton = infoNode == null ? null : infoNode.GetComponent<Button>();
            if (row.BuyButton == null) throw new InvalidDataException("The official HomeShop buy button is missing.");
            if (infoButton != null) infoButton.onClick.AddListener(() => ShowItemDetail(row.Offer));
            row.BuyButton.onClick.AddListener(() => Purchase(row));
            if (row.StatusLabel != null)
            {
                row.StatusLabel.text = string.Empty;
                Transform statusRoot = FindTransform(row.Root, "BottomDescGo");
                if (statusRoot != null) statusRoot.gameObject.SetActive(false);
            }

            if (row.Offer.support_buy_multi_count)
            {
                AddQuantityButton(row, "DecreaseQuantity", "−", -104f, () => ChangeQuantity(row, -1));
                AddQuantityButton(row, "IncreaseQuantity", "+", 104f, () => ChangeQuantity(row, 1));
            }
            RefreshRow(row);
        }

        private void AddQuantityButton(GoodsRow row, string name, string label, float x, Action action)
        {
            if (row.QuantityLabel == null) return;
            RectTransform quantityRect = row.QuantityLabel.rectTransform;
            quantityRect.sizeDelta = new Vector2(130f, quantityRect.sizeDelta.y);
            Transform parent = quantityRect.parent;
            var control = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Button));
            control.transform.SetParent(parent, false);
            var rect = (RectTransform)control.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = quantityRect.anchoredPosition + new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(36f, 42f);
            TMP_Text text = control.GetComponent<TMP_Text>();
            text.font = host.Catalog.Font;
            text.fontSize = 27f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.13f, 0.13f, 0.13f, 1f);
            text.text = label;
            Button button = control.GetComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() => action());
        }

        private void ChangeQuantity(GoodsRow row, long delta)
        {
            if (!row.Offer.support_buy_multi_count) return;
            if (delta > 0 && row.Quantity == long.MaxValue) return;
            if (delta < 0 && row.Quantity <= 1) return;
            row.Quantity += delta;
            RefreshRow(row);
        }

        private void RefreshRow(GoodsRow row)
        {
            if (row.QuantityLabel != null)
            {
                row.QuantityLabel.text = row.Offer.support_buy_multi_count
                    ? "数量 × " + row.Quantity.ToString("N0")
                    : row.Shelf.RemainingInventory == 0 ? "已售罄" : "限购 × 1";
            }
            long unitPrice = 0;
            long totalPrice = 0;
            try
            {
                unitPrice = checked((long)(row.Offer.original_price * row.Shelf.PriceOff));
                totalPrice = checked(unitPrice * row.Quantity);
            }
            catch (OverflowException) { totalPrice = long.MaxValue; }
            if (row.PriceLabel != null)
                row.PriceLabel.text = totalPrice == 0 ? string.Empty
                    : totalPrice == long.MaxValue ? "MAX" : totalPrice.ToString("N0");
            Transform freeLabel = FindTransform(row.Root, "FreeText");
            if (freeLabel != null) freeLabel.gameObject.SetActive(unitPrice == 0);
            Image moneyImage = FindComponent<Image>(row.Root, "MoneyImg");
            if (moneyImage != null) moneyImage.enabled = unitPrice != 0 && moneyImage.sprite != null;
            Transform discountRoot = FindTransform(row.Root, "PriceOffBg");
            bool discounted = unitPrice > 0 && row.Shelf.PriceOff < 1f;
            if (discountRoot != null) discountRoot.gameObject.SetActive(discounted);
            TMP_Text discountText = FindComponent<TMP_Text>(row.Root, "PriceOff");
            if (discountText != null) discountText.text = Mathf.RoundToInt(row.Shelf.PriceOff * 10f) + "折";
            TMP_Text discountRate = FindComponent<TMP_Text>(row.Root, "PriceRate");
            if (discountRate != null) discountRate.text = Mathf.RoundToInt(row.Shelf.PriceOff * 10f).ToString();
            bool hasInventory = row.Shelf.RemainingInventory < 0
                || row.Quantity <= row.Shelf.RemainingInventory;
            bool withinBuyLimit = row.Offer.total_buy_count_limit < 0
                || row.Quantity <= row.Offer.total_buy_count_limit - row.Shelf.BoughtCount;
            bool canAfford = unitPrice == 0 || host.Profile.InventoryCount(row.Offer.currency_id) >= totalPrice;
            if (row.BuyButton != null) row.BuyButton.interactable = hasInventory && withinBuyLimit && canAfford;
        }

        private void Purchase(GoodsRow row)
        {
            string requestId = Guid.NewGuid().ToString("N");
            HomeShopPurchaseResult result = page == Page.Treasury
                ? service.PurchaseTreasury(row.Shelf.SlotIndex, row.Quantity, requestId)
                : row.Shelf.IsRandom
                    ? service.PurchaseGroceryRandom(row.Shelf.SlotIndex, row.Quantity, requestId)
                    : service.Purchase(row.Offer.goods_id, row.Quantity, requestId);
            if (result.IsApplied)
            {
                host.RefreshCounts();
                if (row.Shelf.RemainingInventory >= 0)
                    row.Shelf.RemainingInventory -= checked((int)row.Quantity);
                row.Shelf.BoughtCount += row.Quantity;
                RefreshRow(row);
                SetRowMessage(row, "购买成功");
            }
            else
            {
                SetRowMessage(row, PurchaseMessage(result));
            }
        }

        private void RefreshGrocery()
        {
            HomeShopPurchaseResult result = service.RefreshGrocery(Guid.NewGuid().ToString("N"));
            if (!result.IsApplied)
            {
                panelRenderer.SetText(RefreshButtonPath + "/Content/Text",
                    PurchaseMessage(result));
                UpdateRefreshState(false);
                return;
            }
            host.RefreshCounts();
            BuildRows(service.GetGroceryShelf());
            UpdateRefreshState();
        }

        private void UpdateRefreshState(bool resetMessage = true)
        {
            bool canRefresh = service.TryGetGroceryRefreshCost(out int currencyId, out int cost, out int remaining);
            string infoPath = RefreshGoPath + "/RefreshInfoGo";
            string buttonPath = RefreshGoPath + "/RefreshInfoGo/RefreshBtn/CommonBtn_CostItem";
            panelRenderer.SetActive(infoPath, canRefresh);
            panelRenderer.SetActive(RefreshGoPath + "/RefreshLimitedGo", !canRefresh);
            panelRenderer.SetActive(RefreshGoPath + "/CountDownText", false);
            if (!canRefresh)
            {
                panelRenderer.SetText(RefreshGoPath + "/RefreshLimitedGo/RefreshLimitTxt", "刷新次数已达上限");
                return;
            }

            if (resetMessage) panelRenderer.SetText(buttonPath + "/Content/Text", "刷新");
            panelRenderer.SetText(buttonPath + "/Content/ItemContent/Text", cost.ToString("N0"));
            panelRenderer.SetText(buttonPath + "/Content/RefreshCount/RefreshCountTxt", remaining.ToString());
            OfficialHomeShopGoods currency = catalogGoodsForCurrency(currencyId);
            panelRenderer.SetSprite(buttonPath + "/Content/ItemContent/ItemImg", currency == null
                ? null : host.Catalog.FindSprite(currency.currency_icon_file));
            Button refreshButton = panelRenderer.Find(buttonPath)?.GetComponent<Button>();
            if (refreshButton != null) refreshButton.interactable = host.Profile.InventoryCount(currencyId) >= cost;
        }

        private OfficialHomeShopGoods catalogGoodsForCurrency(int currencyId)
        {
            return service.GetVisibleFixedGoods().FirstOrDefault(goods => goods.currency_id == currencyId)
                ?? service.GetGroceryShelf().Select(item => item.Goods).FirstOrDefault(goods => goods.currency_id == currencyId);
        }

        private static string PurchaseMessage(HomeShopPurchaseResult result)
        {
            switch (result.Status)
            {
                case HomeShopPurchaseStatus.Duplicate: return "本次购买已保存";
                case HomeShopPurchaseStatus.Locked: return "地图尚未解锁";
                case HomeShopPurchaseStatus.InsufficientCurrency: return "货币不足";
                case HomeShopPurchaseStatus.OutOfStock: return "商品已售罄";
                case HomeShopPurchaseStatus.RefreshLimitReached: return "刷新次数已达上限";
                case HomeShopPurchaseStatus.Unsupported: return "该货架尚未开放";
                case HomeShopPurchaseStatus.InvalidRequest: return "购买数量无效";
                case HomeShopPurchaseStatus.SaveRejected: return "存档未能写入";
                default: return "购买未保存";
            }
        }

        private static void SetRowMessage(GoodsRow row, string message)
        {
            if (row.StatusLabel == null) return;
            row.StatusLabel.text = message ?? string.Empty;
            Transform statusRoot = FindTransform(row.Root, "BottomDescGo");
            if (statusRoot != null) statusRoot.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void ShowItemDetail(OfficialHomeShopGoods offer)
        {
            detailRenderer.SetTextByName("GoodsNameText", offer.item_name);
            detailRenderer.SetTextByName("CountText", "×" + offer.item_count);
            detailRenderer.SetSpriteByName("ItemImg", host.Catalog.FindSprite(offer.icon_file));
            detailRoot.SetActive(true);
            detailRoot.transform.SetAsLastSibling();
        }

        private static void SetItemCount(Transform icon, int count, TMP_FontAsset font)
        {
            Transform existing = icon.Find("ItemCount");
            if (count <= 1)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            GameObject labelObject = existing == null
                ? new GameObject("ItemCount", typeof(RectTransform), typeof(TextMeshProUGUI))
                : existing.gameObject;
            labelObject.transform.SetParent(icon, false);
            RectTransform rect = labelObject.transform as RectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-4f, 4f);
            rect.sizeDelta = new Vector2(82f, 32f);
            TMP_Text text = labelObject.GetComponent<TMP_Text>();
            text.font = font;
            text.text = count.ToString("N0");
            text.fontSize = 23f;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = Color.white;
            text.raycastTarget = false;
            Shadow shadow = labelObject.GetComponent<Shadow>();
            if (shadow == null) shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private string GiftRowPath(int index)
        {
            return AchievementGridPath + "/AchievementPanelItem"
                + (index == 0 ? string.Empty : " (" + index + ")");
        }

        private void PopulateGiftRows()
        {
            IReadOnlyList<OfflineHomeShopGiftEntry> entries = service.GetGiftEntries(GiftVisibleRows);
            panelRenderer.SetActive(AchievementScrollPath, true);
            panelRenderer.SetActive(AchievementEmptyPath, entries.Count == 0);
            panelRenderer.SetText(AchievementEmptyPath + "/Text", "暂无馈赠进度");
            for (int index = 0; index < GiftVisibleRows; index++)
            {
                string rowPath = GiftRowPath(index);
                bool visible = index < entries.Count;
                panelRenderer.SetActive(rowPath, visible);
                if (!visible) continue;

                OfflineHomeShopGiftEntry entry = entries[index];
                panelRenderer.SetText(rowPath + "/TitleTxt",
                    "商店消费积分达到 " + FormatCompactScore(entry.TargetScore));
                panelRenderer.SetText(rowPath + "/ProgressGo/ProgressTxt",
                    FormatCompactScore(Math.Min(entry.CurrentScore, entry.TargetScore)) + "/"
                    + FormatCompactScore(entry.TargetScore));
                panelRenderer.SetActive(rowPath + "/ChestGo/FinishedChestImg", entry.CanClaim);
                panelRenderer.SetActive(rowPath + "/ChestGo/UnfinishedChestImg", !entry.CanClaim);
                panelRenderer.SetActive(rowPath + "/BtnGroup/ReceiveBtn", entry.CanClaim);
                panelRenderer.SetActive(rowPath + "/BtnGroup/UncompletedGo", !entry.CanClaim);
                panelRenderer.SetActive(rowPath + "/BtnGroup/CommonSeal", false);
                panelRenderer.SetText(rowPath + "/BtnGroup/ReceiveBtn/Content/Text", "领取");
                Button claimButton = panelRenderer.Find(rowPath + "/BtnGroup/ReceiveBtn")?.GetComponent<Button>();
                if (claimButton != null)
                {
                    claimButton.interactable = entry.CanClaim;
                    claimButton.onClick.RemoveAllListeners();
                    int achievementId = entry.AchievementId;
                    claimButton.onClick.AddListener(() => ClaimGift(achievementId));
                }
                PopulateGiftRewards(rowPath + "/AwardGrid", entry.Milestone.rewards);
            }
        }

        private void PopulateGiftRewards(string gridPath, IReadOnlyList<OfficialHomeShopReward> rewards)
        {
            GameObject gridObject = panelRenderer.Find(gridPath);
            if (gridObject == null) throw new InvalidDataException("The official HomeShop gift reward grid is missing.");
            Transform grid = gridObject.transform;
            for (int index = grid.childCount - 1; index >= 0; index--)
            {
                Transform child = grid.GetChild(index);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            foreach (OfficialHomeShopReward reward in rewards)
            {
                GameObject rewardObject = new GameObject("Reward_" + reward.item_id,
                    typeof(RectTransform), typeof(LayoutElement));
                rewardObject.transform.SetParent(grid, false);
                RectTransform rewardRect = rewardObject.transform as RectTransform;
                rewardRect.sizeDelta = new Vector2(132f, 160f);
                LayoutElement layout = rewardObject.GetComponent<LayoutElement>();
                layout.minWidth = 132f;
                layout.preferredWidth = 132f;
                layout.minHeight = 160f;
                layout.preferredHeight = 160f;
                layout.flexibleWidth = 0f;

                GameObject iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(rewardObject.transform, false);
                RectTransform iconRect = iconObject.transform as RectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, 14f);
                iconRect.sizeDelta = new Vector2(112f, 112f);
                Image icon = iconObject.GetComponent<Image>();
                icon.sprite = host.Catalog.FindSprite(reward.icon_file);
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                GameObject countObject = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                countObject.transform.SetParent(rewardObject.transform, false);
                RectTransform countRect = countObject.transform as RectTransform;
                countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0f);
                countRect.pivot = new Vector2(0.5f, 0f);
                countRect.anchoredPosition = Vector2.zero;
                countRect.sizeDelta = new Vector2(128f, 30f);
                TMP_Text count = countObject.GetComponent<TMP_Text>();
                count.font = host.Catalog.Font;
                count.fontSize = 24f;
                count.alignment = TextAlignmentOptions.Center;
                count.color = Color.white;
                count.text = "×" + reward.count.ToString("N0");
                count.raycastTarget = false;
            }
        }

        private void ClaimGift(int achievementId)
        {
            HomeShopPurchaseResult result = service.ClaimGift(achievementId, Guid.NewGuid().ToString("N"));
            PopulateGiftRows();
            if (result.IsApplied)
            {
                host.RefreshCounts();
                return;
            }

            IReadOnlyList<OfflineHomeShopGiftEntry> entries = service.GetGiftEntries(GiftVisibleRows);
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].AchievementId != achievementId) continue;
                panelRenderer.SetText(GiftRowPath(index) + "/BtnGroup/ReceiveBtn/Content/Text",
                    PurchaseMessage(result));
                break;
            }
        }

        private static string FormatCompactScore(long score)
        {
            if (score >= 10000)
                return (score / 10000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "万";
            if (score >= 1000)
                return (score / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "千";
            return Math.Max(0, score).ToString("N0");
        }

        private void ShowPage(Page target)
        {
            if (!firstPageSwitch && target != page && target != Page.Gifts && page != Page.Gifts)
                panelRenderer.PlayAnimation("HomeShopPanel_Switch");
            page = target;
            firstPageSwitch = false;
            detailRoot?.SetActive(false);
            bool shopPage = target != Page.Gifts;
            panelRenderer.SetActive(ShopContentPath, shopPage);
            panelRenderer.SetActive(RefreshGoPath, target == Page.Grocery);
            panelRenderer.SetActive(AchievementRootPath, target == Page.Gifts);
            panelRenderer.SetActive(VisitRootPath, false);
            panelRenderer.SetActive(VisitTabPath, false);
            panelRenderer.SetActive(FirstTabPath + "/Selected", target == Page.Grocery);
            panelRenderer.SetActive(SecondTabPath + "/Selected", target == Page.Treasury);
            panelRenderer.SetActive(GiftTabPath + "/Selected", target == Page.Gifts);

            if (target == Page.Grocery)
            {
                BuildRows(service.GetGroceryShelf());
                panelRenderer.SetActive(ShopGridPath, true);
                panelRenderer.SetActive(ShopEmptyPath, rows.Count == 0);
                panelRenderer.SetText(ShopEmptyPath + "/Text", "当前没有可购买的商品");
                panelRenderer.SetText(RootPath + "/BgPanel1/Content/TopGo/PanelTitle/TitleGo/TitleTxt", "杂货");
                UpdateRefreshState();
                foreach (GoodsRow row in rows) row.Root.SetActive(true);
                EventSystem.current?.SetSelectedGameObject(rows.Count > 0 ? rows[0].BuyButton.gameObject : null);
            }
            else if (target == Page.Treasury)
            {
                BuildRows(service.GetTreasuryShelf());
                panelRenderer.SetActive(ShopGridPath, true);
                panelRenderer.SetActive(ShopEmptyPath, rows.Count == 0);
                panelRenderer.SetText(ShopEmptyPath + "/Text", "当前没有可购买的商品");
                panelRenderer.SetText(RootPath + "/BgPanel1/Content/TopGo/PanelTitle/TitleGo/TitleTxt", "宝库");
                EventSystem.current?.SetSelectedGameObject(rows.Count > 0 ? rows[0].BuyButton.gameObject : null);
            }
            else
            {
                panelRenderer.PlayAnimation("AchievementPanelBase_In");
                panelRenderer.SetText(AchievementRootPath + "/BgPanel1/Content/TopGo/PanelTitle/TitleGo/TitleTxt", "馈赠");
                PopulateGiftRows();
                EventSystem.current?.SetSelectedGameObject(panelRenderer.Find(GiftTabPath));
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (detailRoot != null && detailRoot.activeSelf) detailRoot.SetActive(false);
                else Close();
                return;
            }
            if (Input.GetKeyDown(KeyCode.PageUp)) ShowPage((Page)Mathf.Max(0, (int)page - 1));
            if (Input.GetKeyDown(KeyCode.PageDown)) ShowPage((Page)Mathf.Min((int)Page.Gifts, (int)page + 1));
            if (page == Page.Gifts || rows.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow)) SelectRow(Mathf.Max(0, selectedRow - 1));
            if (Input.GetKeyDown(KeyCode.DownArrow)) SelectRow(Mathf.Min(rows.Count - 1, selectedRow + 1));
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ChangeQuantity(rows[selectedRow], -1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeQuantity(rows[selectedRow], 1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Purchase(rows[selectedRow]);
        }

        private void SelectRow(int index)
        {
            selectedRow = index;
            EventSystem.current?.SetSelectedGameObject(rows[selectedRow].BuyButton.gameObject);
        }

        private void Close()
        {
            if (closing) return;
            closing = true;
            closed?.Invoke();
            if (panelRoot != null) Destroy(panelRoot);
            else Destroy(gameObject);
        }

        private static Transform FindTransform(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == name);
        }

        private static T FindComponent<T>(GameObject root, string name) where T : Component
        {
            Transform transform = FindTransform(root, name);
            return transform == null ? null : transform.GetComponent<T>();
        }

        private sealed class GoodsRow
        {
            public readonly GameObject Root;
            public readonly OfflineHomeShopShelfItem Shelf;
            public readonly OfficialHomeShopGoods Offer;
            public Button BuyButton;
            public TMP_Text QuantityLabel;
            public TMP_Text PriceLabel;
            public TMP_Text StatusLabel;
            public long Quantity = 1;

            public GoodsRow(GameObject root, OfflineHomeShopShelfItem shelf)
            {
                Root = root;
                Shelf = shelf;
                Offer = shelf.Goods;
            }
        }
    }
}
