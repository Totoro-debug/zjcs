using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;

namespace Zjcs.Presentation.Commerce
{
    /// <summary>
    /// Official commerce window. The HUD opens the shop directly (the official
    /// `ShopEntryBtn` behaviour), and every page is rebuilt from the pinned
    /// `commerce-ui.json` prefab trees. No page is drawn by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CommerceScreenController : MonoBehaviour
    {
        internal enum MainTab { Activity = 0, DirectPay = 2 }
        internal enum DialogKind { None, Daily, Weekly, FirstPay, Accumulated, Exchange, Confirm, Rule, PriceTable, ItemDetail }

        internal const string MainWindowPrefab = "commerce_main";
        internal const string DailyPrefab = "commerce_daily";
        internal const string WeeklyPrefab = "commerce_weekly";
        internal const string FirstPrefab = "commerce_first";
        internal const string AccumulatedPrefab = "commerce_accumulated";
        internal const string ExchangePrefab = "commerce_exchange";
        internal const string ConfirmPrefab = "commerce_confirm";
        internal const string RulePrefab = "commerce_rule";
        internal const string PriceTablePrefab = "commerce_price_table";
        internal const string DirectItemPrefab = "commerce_direct_item";
        internal const string VoucherPrefab = "commerce_voucher";
        internal const string GiftPrefab = "commerce_gift";
        internal const string AccumulatedItemPrefab = "commerce_accumulated_item";
        internal const string ActivityEntryPrefab = "commerce_activity_entry";
        internal const string PriceRowPrefab = "commerce_price_row";

        private readonly List<GameObject> dialogStack = new List<GameObject>();
        private readonly List<GameObject> directCells = new List<GameObject>();
        private readonly List<GameObject> voucherCells = new List<GameObject>();
        private CommerceFeatureRuntime runtime;
        private Transform canvasRoot;
        private Transform mainWindow;
        private Transform middle;
        private Transform activityPage;
        private Transform periodicPage;
        private Transform directPage;
        private Transform directContent;
        private Transform voucherContent;
        private Transform activityContent;
        private TMP_Text mainTitle;
        private TMP_Text statusText;
        private GameObject toast;
        private AudioSource audioSource;
        private Action closedCallback;
        private MainTab mainTab = MainTab.DirectPay;
        private bool directPayTab = false;
        private DialogKind currentDialog = DialogKind.None;
        private GameObject currentDialogObject;
        private CommerceOffer pendingOffer;
        private CommercePaymentMethod pendingMethod;
        private bool pendingFirstPay;
        private long exchangeAmount;
        private float toastTimer;
        private float tickTimer;

        public bool IsOpen => runtime != null && runtime.IsOpen && mainWindow != null;
        public CommerceFeatureRuntime Feature => runtime;
        public Transform MainWindow => mainWindow;

        /// <summary>
        /// Creates one official commerce prefab outside the shop window (used by the HUD
        /// first-pay icon). The caller owns the returned object.
        /// </summary>
        public static GameObject CreateOfficialElement(string prefabKey, Transform parent,
            Action<string, GameObject> bind = null)
        {
            var renderer = new OfficialCommercePrefabRenderer(OfficialCommerceAssetCatalog.LoadDefault());
            return renderer.Create(prefabKey, parent, bind);
        }

        public static CommerceScreenController OpenShop(string slotId, Transform parent, Action closed = null,
            bool openFirstPay = false, string persistentRoot = null)
        {
            if (!Zjcs.Offline.State.ProfileSlotCatalog.TryParseSlotId(slotId))
                throw new ArgumentException("A selected existing profile slot is required.", nameof(slotId));
            var host = new GameObject("CommerceUI");
            if (parent != null) host.transform.SetParent(parent, false);
            var controller = host.AddComponent<CommerceScreenController>();
            controller.closedCallback = closed;
            try
            {
                controller.Initialize(slotId, openFirstPay, persistentRoot);
                return controller;
            }
            catch
            {
                Destroy(host);
                throw;
            }
        }

        private void Initialize(string slotId, bool openFirstPay, string persistentRoot)
        {
            CreateCanvas();
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("Commerce Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            runtime = gameObject.AddComponent<CommerceFeatureRuntime>();
            runtime.PersistentRoot = persistentRoot;
            runtime.OpenProfile(slotId);
            BuildMainWindow();
            SetMainTab(MainTab.DirectPay);
            if (openFirstPay) OpenFirstPay();
        }

        private void CreateCanvas()
        {
            var canvasObject = new GameObject("Commerce Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = canvasObject.transform;
        }

        private void OnDestroy()
        {
            try { runtime?.CloseProfile(); } catch { }
            Action callback = closedCallback;
            closedCallback = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (toast != null)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f)
                {
                    Destroy(toast);
                    toast = null;
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (dialogStack.Count > 0) CloseTopDialog();
                else CloseShop();
                return;
            }
            tickTimer += Time.unscaledDeltaTime;
            if (tickTimer < 1f) return;
            tickTimer = 0f;
            if (!IsOpen) return;
            try
            {
                if (runtime.Tick()) RefreshAll();
            }
            catch (Exception exception)
            {
                ShowToast("商城周期刷新失败：" + exception.Message);
            }
        }

        private void BuildMainWindow()
        {
            ClearMainWindow();
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject window = renderer.Create(MainWindowPrefab, canvasRoot, BindMainButton);
            window.name = "CommerceMain";
            mainWindow = window.transform;
            middle = Find(mainWindow, "BgPanel1/Content/MiddleGo");
            if (middle == null) throw new InvalidOperationException("Official commerce middle content is missing.");
            StretchFull(Find(mainWindow, "BgPanel1/Content"));
            activityPage = Find(middle, "Activity");
            periodicPage = Find(middle, "PeriodicCard");
            directPage = Find(middle, "DirectPay");
            if (periodicPage != null) periodicPage.gameObject.SetActive(false);
            // The monthly card business is owned by docs/features/monthly-card.md and is not
            // available yet, so its official tab is hidden like a locked function tab.
            Transform periodicTab = Find(mainWindow,
                "BgPanel1/Content/BottomGo/TabGroup_11/ActivityEntryTypeTabGroup/PeriodicCardTabItem");
            if (periodicTab != null) periodicTab.gameObject.SetActive(false);
            directContent = Find(directPage, "DirectPayScrollRect/Content");
            activityContent = Find(activityPage, "Content/LoopVerticalScrollRect/Viewprot/Content");
            mainTitle = Find(mainWindow, "BgPanel1/Content/TopGo/PanelTitle1/TitleGo/TitleTxt")?.GetComponent<TMP_Text>();
            BuildDirectAndVoucherItems();
            BuildActivityEntries();
            BuildStatusText();
            RefreshMainChrome();
        }

        private void ClearMainWindow()
        {
            foreach (GameObject dialog in dialogStack) if (dialog != null) Destroy(dialog);
            dialogStack.Clear();
            currentDialog = DialogKind.None;
            currentDialogObject = null;
            if (mainWindow != null) Destroy(mainWindow.gameObject);
            mainWindow = null;
        }

        private void BuildStatusText()
        {
            if (statusText != null) return;
            var go = new GameObject("CommerceStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -58);
            rect.sizeDelta = new Vector2(660, 40);
            statusText = go.GetComponent<TextMeshProUGUI>();
            statusText.font = runtime.ChineseTmpFont;
            statusText.fontSize = 22;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.98f, 0.92f, 0.68f, 1f);
            statusText.raycastTarget = false;
            RefreshStatusText();
        }

        private void RefreshStatusText()
        {
            if (statusText == null || !IsOpen) return;
            statusText.text = "耀星 " + Amount(runtime.Catalog.Data.settings.flareStarItemId).ToString("N0")
                + "　代金券 " + Amount(runtime.Catalog.Data.settings.voucherItemId).ToString("N0")
                + "　晨星 " + Amount(runtime.Catalog.Data.settings.diamondItemId).ToString("N0");
        }

        internal long Amount(int itemId)
        {
            if (runtime?.Session == null) return 0;
            foreach (CommerceAmount item in runtime.Session.State.Inventory)
                if (item.ItemId == itemId) return item.Count;
            return 0;
        }

        private void SetMainTab(MainTab tab)
        {
            mainTab = tab;
            if (activityPage != null) activityPage.gameObject.SetActive(tab == MainTab.Activity);
            if (directPage != null) directPage.gameObject.SetActive(tab == MainTab.DirectPay);
            if (periodicPage != null) periodicPage.gameObject.SetActive(false);
            foreach (string name in new[] { "ActivityEntryTabItem", "DirectPayTabItem", "PeriodicCardTabItem" })
            {
                Transform item = Find(mainWindow,
                    "BgPanel1/Content/BottomGo/TabGroup_11/ActivityEntryTypeTabGroup/" + name);
                if (item == null) continue;
                bool selected = name == (tab == MainTab.Activity ? "ActivityEntryTabItem" : "DirectPayTabItem");
                Transform selectedNode = Find(item, "Selected");
                if (selectedNode != null) selectedNode.gameObject.SetActive(selected);
                Transform bg = Find(item, "Bg");
                if (bg != null) bg.gameObject.SetActive(!selected);
            }
            if (mainTitle != null)
                mainTitle.text = tab == MainTab.Activity ? Localized("UI_Pay_Title", "商城")
                    : Localized(directPayTab ? "UI_Pay_DirectPay_Title" : "UI_Pay_Voucher_Title",
                        directPayTab ? "耀星直购" : "代金券");
            if (tab == MainTab.DirectPay) SwitchPayTab(directPayTab);
            RefreshMainChrome();
        }

        private void SwitchPayTab(bool direct)
        {
            directPayTab = direct;
            Transform directTop = Find(directPage, "TopGo/DirectPayTopGo");
            Transform voucherTop = Find(directPage, "TopGo/VoucherTopGo");
            if (directTop != null) directTop.gameObject.SetActive(direct);
            if (voucherTop != null) voucherTop.gameObject.SetActive(!direct);
            foreach (GameObject cell in directCells) if (cell != null) cell.SetActive(direct);
            foreach (GameObject cell in voucherCells) if (cell != null) cell.SetActive(!direct);
            foreach (string name in new[] { "DirectPay", "Voucher" })
            {
                Transform item = Find(directPage, "TabGroup_14/TabGroup/" + name);
                if (item == null) continue;
                bool selected = name == (direct ? "DirectPay" : "Voucher");
                Transform selectedNode = Find(item, "Selected");
                if (selectedNode != null) selectedNode.gameObject.SetActive(selected);
            }
            if (mainTitle != null)
                mainTitle.text = Localized(direct ? "UI_Pay_DirectPay_Title" : "UI_Pay_Voucher_Title",
                    direct ? "耀星直购" : "代金券");
            RefreshMainChrome();
        }

        private void RefreshMainChrome()
        {
            RefreshStatusText();
            RefreshAccumulatedEntry();
            RefreshActivityEntryDots();
        }

        private void BindMainButton(string path, GameObject node)
        {
            Button button = node == null ? null : node.GetComponent<Button>();
            if (button == null) return;
            button.onClick.AddListener(PlayClick);
            if (path.EndsWith("/ActivityEntryTabItem", StringComparison.Ordinal))
                button.onClick.AddListener(() => SetMainTab(MainTab.Activity));
            else if (path.EndsWith("/DirectPayTabItem", StringComparison.Ordinal))
                button.onClick.AddListener(() => SetMainTab(MainTab.DirectPay));
            else if (path.EndsWith("/CloseBtn_dark", StringComparison.Ordinal))
                button.onClick.AddListener(CloseShop);
            else if (path.EndsWith("/TabGroup/DirectPay", StringComparison.Ordinal))
                button.onClick.AddListener(() => SwitchPayTab(true));
            else if (path.EndsWith("/TabGroup/Voucher", StringComparison.Ordinal))
                button.onClick.AddListener(() => SwitchPayTab(false));
            else if (path.EndsWith("/RuleBtn", StringComparison.Ordinal))
                button.onClick.AddListener(OpenPriceTable);
            else if (path.EndsWith("/AccumulatedPayEntryGo/BgImg", StringComparison.Ordinal))
                button.onClick.AddListener(OpenAccumulated);
        }

        private void RefreshAccumulatedEntry()
        {
            if (directPage == null || runtime?.Offers == null) return;
            Transform entry = Find(directPage, "TopGo/DirectPayTopGo/AccumulatedPayEntryGo");
            if (entry == null) return;
            Transform redDot = Find(entry, "RedDotGo");
            if (redDot != null) redDot.gameObject.SetActive(runtime.Offers.HasAvailableAccumulatedAward());
            TMP_Text title = Find(entry, "TitleTxt")?.GetComponent<TMP_Text>();
            if (title != null) title.text = Localized("ui_accumulated_pay_title", "星眷");
        }

        private void RefreshActivityEntryDots()
        {
            if (activityContent == null) return;
            for (int i = 0; i < activityContent.childCount; i++)
            {
                Transform entry = activityContent.GetChild(i);
                if (!entry.name.StartsWith("CommerceActivity_", StringComparison.Ordinal)) continue;
                bool dot = entry.name == "CommerceActivity_daily" ? runtime.Offers.CanClaimDailyFree()
                    : entry.name == "CommerceActivity_weekly" ? WeeklyFreeAvailable()
                    : entry.name == "CommerceActivity_first" && runtime.Offers.FirstPayTodayHasAvailableAward();
                Transform redDot = Find(entry, "Content/Attachment/RedDot");
                if (redDot != null) redDot.gameObject.SetActive(dot);
                Transform newMark = Find(entry, "Content/Attachment/NewMark");
                if (newMark != null) newMark.gameObject.SetActive(false);
            }
        }

        private bool WeeklyFreeAvailable()
        {
            if (runtime?.Offers == null) return false;
            CommerceGift free = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(value =>
                value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId <= 0);
            return free != null && !runtime.Offers.IsWeeklySoldOut(free) && runtime.Offers.Quote("weekly", free.id, free.groupId) != null;
        }

        private void SetDirectPayContent()
        {
            if (directContent == null)
                directContent = Find(directPage, "DirectPayScrollRect/Content");
            if (voucherContent == null)
                voucherContent = Find(directPage, "DirectPayScrollRect/Content");
        }

        private void BuildDirectAndVoucherItems()
        {
            if (directContent == null) return;
            ClearChildren(directContent);
            directCells.Clear();
            voucherCells.Clear();
            OfficialCommercePrefabRenderer directRenderer = runtime.CreatePrefabRenderer();
            foreach (CommerceDirectPay item in runtime.Catalog.Data.directPay)
            {
                CommerceDirectPay row = item;
                GameObject cell = directRenderer.Create(DirectItemPrefab, directContent, (path, node) =>
                {
                    Button button = node.GetComponent<Button>();
                    if (button != null && path == "/PayDirectPayItem")
                    {
                        button.onClick.AddListener(PlayClick);
                        button.onClick.AddListener(() => OpenPurchase(runtime.Quote("direct", row.id, 0)));
                    }
                });
                cell.name = "CommerceDirect_" + row.id;
                PopulateDirectCell(cell.transform, row);
                directCells.Add(cell);
            }
            OfficialCommercePrefabRenderer voucherRenderer = runtime.CreatePrefabRenderer();
            foreach (CommerceVoucher item in runtime.Catalog.Data.vouchers)
            {
                CommerceVoucher row = item;
                GameObject cell = voucherRenderer.Create(VoucherPrefab, directContent, (path, node) =>
                {
                    Button button = node.GetComponent<Button>();
                    if (button != null && path == "/PayVoucherItem")
                    {
                        button.onClick.AddListener(PlayClick);
                        button.onClick.AddListener(() => OpenPurchase(runtime.Quote("voucher", row.id, 0)));
                    }
                });
                cell.name = "CommerceVoucher_" + row.id;
                PopulateVoucherCell(cell.transform, row);
                voucherCells.Add(cell);
            }
            OfficialCommercePrefabRenderer.RebuildLayout(directContent.gameObject);
        }

        private void PopulateDirectCell(Transform cell, CommerceDirectPay row)
        {
            CommerceOffer offer = runtime.Quote("direct", row.id, 0);
            bool first = offer == null || offer.First;
            SetText(cell, "Txt/TitleTxt", runtime.Catalog.Text(row.titleKey));
            Image icon = Find(cell, "IconImg")?.GetComponent<Image>();
            if (icon != null) icon.sprite = runtime.FindSprite(row.icon);
            SetText(cell, "AwardImg/AwardCount/AwardCountText",
                (row.fixedAward.FirstOrDefault()?.count ?? 0).ToString("N0"));
            CommerceReward extra = (first ? row.firstExtraAward : row.repeatExtraAward).FirstOrDefault();
            SetText(cell, "AwardImg/ExtraImg/ExtraAwardCount/ExtraAwardCountText",
                (extra?.count ?? 0).ToString("N0"));
            SetText(cell, "GreatValue/GreatValue/GreatValuePercentTxt",
                first ? string.Empty : row.repeatExtraPercent + "%");
            SetActive(cell, "GreatValue/FirstPay", first);
            SetActive(cell, "GreatValue/GreatValue", !first);
            Image fixedIcon = Find(cell, "AwardImg/AwardCount/Icon")?.GetComponent<Image>();
            if (fixedIcon != null) fixedIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.flareStarItemId);
            Image extraIcon = Find(cell, "AwardImg/ExtraImg/ExtraAwardCount/Icon")?.GetComponent<Image>();
            if (extraIcon != null) extraIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.diamondItemId);
            SetText(cell, "AccumulatedValueDescGo (1)/ValueTxt",
                (runtime.Catalog.MoneyCost(row.moneyCostId)?.startValue ?? 0)
                    .ToString("0.##", CultureInfo.InvariantCulture));
            bool voucher = offer != null && runtime.PaymentMethod(offer) == CommercePaymentMethod.Voucher;
            SetActive(cell, "CostContent/VourcherInfoGo", voucher);
            SetActive(cell, "CostContent/OperationTxt", !voucher);
            if (voucher)
            {
                SetText(cell, "CostContent/VourcherInfoGo/VoucherCountTxt", offer.VoucherCost.ToString("N0"));
                Image voucherIcon = Find(cell, "CostContent/VourcherInfoGo/Icon")?.GetComponent<Image>();
                if (voucherIcon != null) voucherIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.voucherItemId);
            }
            else
            {
                SetText(cell, "CostContent/OperationTxt", MoneyPrice(row.moneyCostId));
            }
        }

        private void PopulateVoucherCell(Transform cell, CommerceVoucher row)
        {
            SetText(cell, "Content/TitleTxt", runtime.Catalog.Text(row.titleKey));
            SetText(cell, "OperationTxt", MoneyPrice(row.moneyCostId));
            CommerceMoneyCost cost = runtime.Catalog.MoneyCost(row.moneyCostId);
            SetText(cell, "Content/AwardInfo/AwardCount/AwardCountText",
                cost == null ? "0" : cost.voucherCost.ToString("N0"));
            Image icon = Find(cell, "Content/IconImg")?.GetComponent<Image>();
            if (icon != null) icon.sprite = runtime.FindSprite(row.icon);
            Image awardIcon = Find(cell, "Content/AwardInfo/AwardCount/Icon")?.GetComponent<Image>();
            if (awardIcon != null) awardIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.voucherItemId);
            SetActive(cell, "Content/AwardInfo/ExtraImg", false);
        }

        private void BuildActivityEntries()
        {
            if (activityContent == null) return;
            ClearChildren(activityContent);
            AddActivityEntry("CommerceActivity_daily", Localized("ui_daily_pay_gift_title", "每日礼包"),
                Sprite("ImgBg_Everyday"), OpenDaily);
            AddActivityEntry("CommerceActivity_weekly", Localized("ui_weekly_pay_gift_title", "每周礼包"),
                Sprite("note_WeeklyActivities"), OpenWeekly);
            AddActivityEntry("CommerceActivity_first", Localized("ui_first_pay_entry_title", "首购"),
                Sprite("ImgItem_FirstPay"), OpenFirstPay);
            AddActivityEntry("CommerceActivity_exchange", "耀星兑换",
                runtime.FindItemSprite(runtime.Catalog.Data.settings.flareStarItemId), OpenExchange);
            RefreshActivityEntryDots();
            OfficialCommercePrefabRenderer.RebuildLayout(activityContent.gameObject);
        }

        private void AddActivityEntry(string name, string title, Sprite icon, Action click)
        {
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject entry = renderer.Create(ActivityEntryPrefab, activityContent, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button != null && path == "/ActivityCommonEntry")
                {
                    button.onClick.AddListener(PlayClick);
                    button.onClick.AddListener(() => click());
                }
            });
            entry.name = name;
            TMP_Text info = Find(entry.transform, "Content/Info/Text")?.GetComponent<TMP_Text>();
            if (info != null)
            {
                info.text = title;
                info.fontSize = 18;
                info.enableWordWrapping = false;
                info.overflowMode = TextOverflowModes.Overflow;
                info.alignment = TextAlignmentOptions.Center;
                info.color = new Color(0.20f, 0.23f, 0.30f, 1f);
            }
            SetActive(entry.transform, "Content/Prompt", false);
            RectTransform entryRect = entry.GetComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(200f, 236f);
            LayoutElement entryLayout = entry.GetComponent<LayoutElement>();
            if (entryLayout == null) entryLayout = entry.AddComponent<LayoutElement>();
            entryLayout.preferredWidth = 200f;
            entryLayout.preferredHeight = 236f;
            RectTransform iconRect = Find(entry.transform, "Content/BgImg") as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.anchoredPosition = new Vector2(0f, -8f);
                iconRect.sizeDelta = new Vector2(176f, 176f);
            }
            RectTransform infoRect = Find(entry.transform, "Content/Info") as RectTransform;
            if (infoRect != null)
            {
                infoRect.anchorMin = infoRect.anchorMax = new Vector2(0.5f, 0f);
                infoRect.pivot = new Vector2(0.5f, 0f);
                infoRect.anchoredPosition = new Vector2(0f, 6f);
                infoRect.sizeDelta = new Vector2(200f, 42f);
            }
            if (info != null)
            {
                RectTransform textRect = info.rectTransform;
                textRect.sizeDelta = new Vector2(196f, 42f);
                info.fontSize = 18f;
                info.enableWordWrapping = false;
                info.overflowMode = TextOverflowModes.Overflow;
                info.alignment = TextAlignmentOptions.Center;
            }
            Image image = Find(entry.transform, "Content/BgImg/IconImg")?.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = icon;
                image.preserveAspect = true;
            }
            SetActive(entry.transform, "Content/Attachment/RedDot", false);
        }

        private Sprite Sprite(string name)
        {
            return runtime.FindSprite(name);
        }

        private void OpenPurchase(CommerceOffer offer)
        {
            if (offer == null)
            {
                ShowToast("商品已售罄或未开放。");
                RefreshAll();
                return;
            }
            pendingOffer = offer;
            pendingFirstPay = false;
            OpenConfirmDialog(offer, null);
        }

        private void OpenConfirmDialog(CommerceOffer offer, Action custom)
        {
            CloseDialogKind(DialogKind.Confirm);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(ConfirmPrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/PayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => ConfirmPending(offer, CommercePaymentMethod.Voucher));
                else if (path.EndsWith("/MoneyPayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => ConfirmPending(offer, CommercePaymentMethod.CashConfirmation));
            });
            dialog.name = "CommerceConfirm";
            RegisterDialog(dialog, DialogKind.Confirm);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleText",
                offer == null ? "确认" : OfferTitle(offer));
            Transform iconSlot = Find(dialog.transform, "BgDialog1/Content/IconGo/IconSlot");
            if (iconSlot != null)
            {
                ClearChildren(iconSlot);
                if (offer != null)
                {
                    string iconPath = offer.Kind == "direct"
                        ? runtime.Catalog.Data.directPay.FirstOrDefault(v => v.id == offer.Id)?.icon
                        : null;
                    Sprite icon = iconPath == null ? null : runtime.FindSprite(iconPath);
                    if (icon != null) AddImage(iconSlot, "GiftIcon", icon, new Vector2(180, 180));
                }
            }
            Transform awards = Find(dialog.transform, "BgDialog1/Content/ItemContent/ItemContent");
            if (awards != null)
            {
                ClearChildren(awards);
                if (offer != null) AddRewardIcons(awards, new CommerceRewardMap(offer.Rewards));
            }
            bool voucher = offer != null && runtime.PaymentMethod(offer) == CommercePaymentMethod.Voucher;
            SetActive(dialog.transform, "BgDialog1/Content/PayBtnGo/PayBtn", voucher);
            SetActive(dialog.transform, "BgDialog1/Content/PayBtnGo/CommonPayBtn", !voucher);
            if (voucher)
            {
                SetText(dialog.transform, "BgDialog1/Content/PayBtnGo/PayBtn/Content/DiamondGo/DiamondPayTxt",
                    offer.VoucherCost.ToString("N0"));
                Image voucherIcon = Find(dialog.transform,
                    "BgDialog1/Content/PayBtnGo/PayBtn/Content/DiamondGo/Icon")?.GetComponent<Image>();
                if (voucherIcon != null) voucherIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.voucherItemId);
            }
            else if (offer != null)
            {
                SetText(dialog.transform, "BgDialog1/Content/PayBtnGo/CommonPayBtn/MoneyPayBtn/Content/Text",
                    MoneyPrice(offer.MoneyCostId));
                SetText(dialog.transform, "BgDialog1/Content/PayBtnGo/CommonPayBtn/AccumulatedValueDescGo/ValueTxt",
                    offer.AccumulatedValue.ToString("0.##", CultureInfo.InvariantCulture));
            }
            Button close = Find(dialog.transform, "NewPublicTopBarParent/Slot")?.GetComponent<Button>();
            if (close != null) close.onClick.AddListener(CloseTopDialog);
            // PopupWindow closes when the dimmed mask is clicked; the extracted tree has no
            // explicit mask button, so Esc and the pay buttons remain the close paths.
            OfficialCommercePrefabRenderer.RebuildLayout(dialog);
        }

        private string OfferTitle(CommerceOffer offer)
        {
            switch (offer.Kind)
            {
                case "direct":
                    CommerceDirectPay direct = runtime.Catalog.Data.directPay.FirstOrDefault(v => v.id == offer.Id);
                    return direct == null ? "确认购买" : runtime.Catalog.Text(direct.titleKey) + " " + MoneyPrice(offer.MoneyCostId);
                case "voucher":
                    CommerceVoucher voucher = runtime.Catalog.Data.vouchers.FirstOrDefault(v => v.id == offer.Id);
                    return voucher == null ? "确认购买" : runtime.Catalog.Text(voucher.titleKey) + " " + MoneyPrice(offer.MoneyCostId);
                case "daily":
                case "all":
                    return runtime.Catalog.Text("ui_daily_pay_gift_title") + " " + MoneyPrice(offer.MoneyCostId);
                case "weekly":
                    CommerceGift gift = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(v => v.id == offer.Id && v.groupId == offer.GroupId);
                    return gift == null ? "确认购买" : runtime.Catalog.Text(gift.titleKey) + " " + MoneyPrice(offer.MoneyCostId);
                default:
                    return "确认购买";
            }
        }

        private void ConfirmPending(CommerceOffer offer, CommercePaymentMethod method)
        {
            pendingMethod = method;
            CommerceTransactionResult result = runtime.Purchase(offer, method);
            CloseDialogKind(DialogKind.Confirm);
            ApplyResult(result, offer.Kind == "voucher" ? "代金券购买成功" : null);
        }

        private void ApplyResult(CommerceTransactionResult result, string successMessage = null)
        {
            if (result == null) return;
            ShowToast(string.IsNullOrEmpty(successMessage) && result.Code == CommerceTransactionCode.Applied
                ? "操作成功" : result.Message);
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (!IsOpen) return;
            DialogKind restore = currentDialog;
            BuildDirectAndVoucherItems();
            BuildActivityEntries();
            SetMainTab(mainTab);
            SwitchPayTab(directPayTab);
            RefreshMainChrome();
            switch (restore)
            {
                case DialogKind.Daily: OpenDaily(); break;
                case DialogKind.Weekly: OpenWeekly(); break;
                case DialogKind.FirstPay: OpenFirstPay(); break;
                case DialogKind.Accumulated: OpenAccumulated(); break;
                case DialogKind.Exchange: OpenExchange(); break;
            }
        }

        internal void PlayClick()
        {
            AudioClip clip = runtime?.FindAudio("Main_Cursor_Normal");
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        internal void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (toast != null) Destroy(toast);
            var go = new GameObject("CommerceToast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 260);
            rect.sizeDelta = new Vector2(560, 110);
            Image image = go.GetComponent<Image>();
            image.sprite = runtime.FindSprite("ImgTxtBg");
            image.type = Image.Type.Sliced;
            image.color = new Color(0f, 0f, 0f, 0.86f);
            image.raycastTarget = false;
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28, 12);
            textRect.offsetMax = new Vector2(-28, -12);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = statusText != null ? statusText.font : null;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.text = message;
            go.transform.SetAsLastSibling();
            toast = go;
            toastTimer = 3.2f;
        }

        internal void RegisterDialog(GameObject dialog, DialogKind kind)
        {
            dialogStack.Add(dialog);
            currentDialogObject = dialog;
            currentDialog = kind;
            dialog.transform.SetAsLastSibling();
        }

        internal void OnDialogClosed(DialogKind kind)
        {
            if (currentDialog == kind)
            {
                currentDialog = DialogKind.None;
                currentDialogObject = null;
                if (dialogStack.Count > 0 &&
                    dialogStack[dialogStack.Count - 1] == null) dialogStack.RemoveAll(value => value == null);
                if (dialogStack.Count > 0)
                {
                    currentDialogObject = dialogStack[dialogStack.Count - 1];
                    currentDialog = currentDialogObject == null ? DialogKind.None : KindOf(currentDialogObject.name);
                }
            }
        }

        public void Close()
        {
            CloseShop();
        }

        public void CloseCurrentDialog()
        {
            CloseTopDialog();
        }

        internal void CloseTopDialog()
        {
            if (dialogStack.Count == 0) return;
            GameObject dialog = dialogStack[dialogStack.Count - 1];
            dialogStack.RemoveAt(dialogStack.Count - 1);
            if (dialog != null) Destroy(dialog);
            currentDialogObject = null;
            for (int i = dialogStack.Count - 1; i >= 0; i--)
            {
                if (dialogStack[i] != null)
                {
                    currentDialogObject = dialogStack[i];
                    break;
                }
            }
            currentDialog = currentDialogObject == null ? DialogKind.None : KindOf(currentDialogObject.name);
        }

        internal void CloseDialog(GameObject dialog, DialogKind kind)
        {
            dialogStack.Remove(dialog);
            if (dialog != null) Destroy(dialog);
            OnDialogClosed(kind);
        }

        private void CloseDialogKind(DialogKind kind)
        {
            string name = DialogName(kind);
            for (int i = dialogStack.Count - 1; i >= 0; i--)
            {
                GameObject dialog = dialogStack[i];
                if (dialog == null) { dialogStack.RemoveAt(i); continue; }
                if (!string.IsNullOrEmpty(name) && dialog.name == name)
                {
                    dialogStack.RemoveAt(i);
                    Destroy(dialog);
                }
            }
            currentDialogObject = dialogStack.Count == 0 ? null : dialogStack[dialogStack.Count - 1];
            currentDialog = currentDialogObject == null ? DialogKind.None : KindOf(currentDialogObject.name);
        }

        internal static string DialogName(DialogKind kind)
        {
            switch (kind)
            {
                case DialogKind.Daily: return "CommerceDaily";
                case DialogKind.Weekly: return "CommerceWeekly";
                case DialogKind.FirstPay: return "CommerceFirst";
                case DialogKind.Accumulated: return "CommerceAccumulated";
                case DialogKind.Exchange: return "CommerceExchange";
                case DialogKind.Confirm: return "CommerceConfirm";
                case DialogKind.Rule: return "CommerceRule";
                case DialogKind.PriceTable: return "CommercePriceTable";
                default: return null;
            }
        }

        private static DialogKind KindOf(string name)
        {
            switch (name)
            {
                case "CommerceDaily": return DialogKind.Daily;
                case "CommerceWeekly": return DialogKind.Weekly;
                case "CommerceFirst": return DialogKind.FirstPay;
                case "CommerceAccumulated": return DialogKind.Accumulated;
                case "CommerceExchange": return DialogKind.Exchange;
                case "CommerceConfirm": return DialogKind.Confirm;
                case "CommerceRule": return DialogKind.Rule;
                case "CommercePriceTable": return DialogKind.PriceTable;
                default: return DialogKind.None;
            }
        }

        private void CloseShop()
        {
            Destroy(gameObject);
        }

        private void OnApplicationQuit()
        {
            try { runtime?.CloseProfile(); } catch { }
        }

        // ---------- shared helpers ----------

        internal static void StretchFull(Transform transform)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static Transform Find(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform direct = root.Find(path);
            if (direct != null) return direct;
            string[] parts = path.Split('/');
            Transform current = root;
            foreach (string raw in parts)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string part = raw.Trim();
                if (part.Length == 0) continue;
                Transform next = current.Find(raw) ?? current.Find(part);
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        internal static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name || child.name.Trim() == name) return child;
                Transform nested = FindDeep(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        internal static void SetText(Transform root, string path, string value)
        {
            Transform node = Find(root, path);
            if (node == null)
            {
                string leaf = path.Substring(path.LastIndexOf('/') + 1).Trim();
                node = FindDeep(root, leaf);
            }
            TMP_Text text = node == null ? null : node.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        internal static void SetActive(Transform root, string path, bool active)
        {
            Transform node = Find(root, path);
            if (node == null)
            {
                string leaf = path.Substring(path.LastIndexOf('/') + 1).Trim();
                node = FindDeep(root, leaf);
            }
            if (node != null) node.gameObject.SetActive(active);
        }

        internal static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                child.name = "Removed_" + child.name;
                child.SetActive(false);
                Destroy(child);
            }
        }

        internal static void AddImage(Transform parent, string name, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        internal string MoneyPrice(int moneyCostId)
        {
            CommerceMoneyCost cost = runtime.Catalog.MoneyCost(moneyCostId);
            if (cost == null) return "-";
            return "¥" + cost.startValue.ToString(cost.startValue % 1 == 0 ? "0" : "0.##", CultureInfo.InvariantCulture);
        }

        internal string Localized(string key, string fallback)
        {
            string value = runtime.Catalog.Text(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        internal string TimeUntil(int period, bool weekly)
        {
            DateTime now = DateTime.Now;
            DateTime zero = new DateTime(2025, 12, 1, 8, 0, 0, DateTimeKind.Local);
            DateTime end = weekly ? zero.AddDays((period + 1) * 7) : zero.AddDays(period + 1);
            TimeSpan left = end - now;
            if (left.TotalSeconds <= 0) return "00:00:00";
            return left.TotalDays >= 1
                ? ((int)left.TotalDays).ToString(CultureInfo.InvariantCulture) + "天"
                    + left.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : left.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        internal sealed class CommerceRewardMap
        {
            private readonly List<KeyValuePair<int, long>> values;
            public CommerceRewardMap(IReadOnlyDictionary<int, long> rewards)
            {
                values = rewards == null
                    ? new List<KeyValuePair<int, long>>()
                    : rewards.OrderByDescending(value => value.Value).ToList();
            }
            public IReadOnlyList<KeyValuePair<int, long>> Values => values;
        }
    }
}


