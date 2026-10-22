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
    [DisallowMultipleComponent]
    public sealed class CommerceScreenController : MonoBehaviour
    {
        private static readonly string[] PageKeys =
        {
            "commerce_main", "commerce_daily", "commerce_weekly", "commerce_first",
            "commerce_accumulated", "commerce_exchange"
        };
        private static readonly string[] PageTitles = { "商城", "每日礼包", "每周礼包", "首购", "累计星值", "兑换" };
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<GameObject> dialogs = new List<GameObject>();
        private readonly List<GameObject> directCells = new List<GameObject>();
        private readonly List<GameObject> voucherCells = new List<GameObject>();
        private CommerceFeatureRuntime runtime;
        private Transform screen;
        private TextMeshProUGUI status;
        private AudioSource audioSource;
        private int currentPage;
        private long exchangeAmount = 1;

        public static void Install()
        {
            if (FindObjectOfType<CommerceScreenController>() == null)
                new GameObject("Commerce Screen Bootstrap").AddComponent<CommerceScreenController>();
        }

        private void Start()
        {
            GameObject canvasObject = new GameObject("Commerce Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            screen = canvasObject.transform;
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("Commerce Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            audioSource = gameObject.AddComponent<AudioSource>();
            runtime = gameObject.AddComponent<CommerceFeatureRuntime>();
            ShowProfileSlots();
        }

        private void Update()
        {
            if (dialogs.Count > 0 && Input.GetKeyDown(KeyCode.Escape))
            {
                GameObject dialog = dialogs[dialogs.Count - 1];
                dialogs.RemoveAt(dialogs.Count - 1);
                Destroy(dialog);
            }
        }

        private void ShowProfileSlots()
        {
            ClearScreen();
            IReadOnlyList<string> slots;
            try { slots = runtime.ExistingCharacterSlots(); }
            catch (Exception exception) { ShowMessage("商城资源未导入：" + exception.Message); return; }
            Transform panel = CreatePanel("选择已有角色存档", new Vector2(820, 760));
            if (slots.Count == 0)
            {
                AddText(panel, "当前没有可用的角色存档", 30, new Vector2(0, 40));
                AddText(panel, "商城不会创建角色或存档", 24, new Vector2(0, -30));
                return;
            }
            for (int i = 0; i < slots.Count; i++)
            {
                string slot = slots[i];
                CreateButton(panel, "角色 " + slot, new Vector2(0, 180 - i * 96), () => OpenProfile(slot));
            }
        }

        private void OpenProfile(string slot)
        {
            try
            {
                runtime.OpenProfile(slot);
                BuildPages();
                ShowPage(0);
            }
            catch (Exception exception) { ShowMessage("无法读取角色存档：" + exception.Message); }
        }

        private void BuildPages()
        {
            ClearScreen();
            for (int i = 0; i < PageKeys.Length; i++)
            {
                int pageIndex = i;
                OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
                GameObject page = renderer.Create(PageKeys[i], screen,
                    (path, target) => BindPageButton(pageIndex, path, target));
                page.name = PageKeys[i];
                pages.Add(page);
            }
            PopulateDirectItems();
            PopulateVoucherItems();
            PopulateDailyItems();
            PopulateWeeklyItems();
            PopulateFirstAwards();
            PopulateAccumulatedItems();
            CreateNavigation();
            status = AddText(screen, string.Empty, 22, new Vector2(0, 70));
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 1);
            statusRect.anchorMax = new Vector2(0.5f, 1);
            statusRect.pivot = new Vector2(0.5f, 1);
            statusRect.anchoredPosition = new Vector2(0, -110);
            RefreshStatus();
        }

        private void CreateNavigation()
        {
            GameObject bar = new GameObject("Commerce Navigation", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup));
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.SetParent(screen, false);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, 92);
            bar.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 0.96f);
            HorizontalLayoutGroup layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int i = 0; i < PageTitles.Length; i++)
            {
                int page = i;
                CreateButton(bar.transform, PageTitles[i], Vector2.zero, () => ShowPage(page), true);
            }
            bar.transform.SetAsLastSibling();
            if (status != null) status.transform.SetAsLastSibling();
        }

        private void BindPageButton(int page, string path, GameObject target)
        {
            Button button = target.GetComponent<Button>();
            if (button == null) return;
            button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
            if (page == 0 && path.EndsWith("Voucher", StringComparison.Ordinal))
                button.onClick.AddListener(() => SwitchDirectPayTab(false));
            else if (page == 0 && path.EndsWith("DirectPay", StringComparison.Ordinal))
                button.onClick.AddListener(() => SwitchDirectPayTab(true));
            else if (path.EndsWith("CloseBtn", StringComparison.Ordinal)
                || path.EndsWith("CloseBtn_dark", StringComparison.Ordinal))
                button.onClick.AddListener(() => ShowPage(0));
            else if (page == 1 && path.Contains("FreeAwardGo") && path.Contains("BgImg (1)"))
            {
                button.interactable = runtime.Offers.CanClaimDailyFree();
                button.onClick.AddListener(ClaimDailyFree);
            }
            else if (page == 1 && path.EndsWith("BuyAllBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => OpenPurchase("all", 0, runtime.Session.State.DailyGroup));
            else if (page == 1 && path.EndsWith("BuyBtn", StringComparison.Ordinal))
            {
                int id = path.Contains("(2)") ? 3 : path.Contains("(1)") ? 2 : 1;
                button.onClick.AddListener(() => OpenPurchase("daily", id, runtime.Session.State.DailyGroup));
            }
            else if (page == 2 && path.Contains("/FreeAwardItem/BgImg/TakeAwardBtn", StringComparison.Ordinal))
            {
                CommerceGift freeGift = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(value =>
                    value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId <= 0);
                if (freeGift != null)
                {
                    button.interactable = runtime.Quote("weekly", freeGift.id, freeGift.groupId) != null;
                    button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                    button.onClick.AddListener(() => OpenPurchase("weekly", freeGift.id, freeGift.groupId));
                }
                else button.interactable = false;
            }
            else if (page == 3 && path.EndsWith("MoneyPayBtn", StringComparison.Ordinal))
                button.onClick.AddListener(OpenFirstPayConfirmation);
            else if (page == 3 && path.EndsWith("TakeAwardBtn", StringComparison.Ordinal))
            {
                int day = path.Contains("(2)") ? 3 : path.Contains("(1)") ? 2 : 1;
                button.interactable = runtime.Offers.CanClaimFirst(day);
                button.onClick.AddListener(() => Apply(runtime.ClaimFirstAward(day)));
            }
            else if (page == 4 && path.Contains("/TakeAwardBtn/"))
                button.onClick.AddListener(() => Apply(runtime.ClaimAccumulated(ReadLevel(target.transform))));
            else if (page == 5 && path.EndsWith("ConfirmBtn", StringComparison.Ordinal))
                button.onClick.AddListener(Exchange);
            else if (page == 5 && path.EndsWith("CancelBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => ShowPage(0));
            else if (page == 5 && path.EndsWith("CommonBtn_Reduce", StringComparison.Ordinal))
                button.onClick.AddListener(() => SetExchangeAmount(exchangeAmount - 1));
            else if (page == 5 && path.EndsWith("CommonBtn_Add", StringComparison.Ordinal))
                button.onClick.AddListener(() => SetExchangeAmount(exchangeAmount + 1));
            else if (page == 5 && path.EndsWith("MaxBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => SetExchangeAmount(Amount(runtime.Catalog.Data.settings.flareStarItemId)));
        }

        private void PopulateDirectItems()
        {
            Transform content = pages[0].transform.Find("BgPanel1/Content/MiddleGo/DirectPay/DirectPayScrollRect/Content");
            CommercePrefab prefab = runtime.Catalog.Prefab("commerce_direct_item");
            if (content == null || prefab?.root == null) return;
            foreach (CommerceDirectPay item in runtime.Catalog.Data.directPay)
            {
                GameObject cell = runtime.CreatePrefabRenderer().CreateSubtree(prefab.root, content,
                    (path, target) =>
                    {
                        Button button = target.GetComponent<Button>();
                        if (button != null && path == "/PayDirectPayItem")
                        {
                            button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                            button.onClick.AddListener(() => OpenPurchase("direct", item.id, 0));
                        }
                    });
                cell.name = "PayDirectPayItem_" + item.id;
                directCells.Add(cell);
                foreach (Image image in cell.GetComponentsInChildren<Image>(true))
                    if (image.name == "IconImg") image.sprite = runtime.FindSprite(item.icon);
                SetTexts(cell.transform, text =>
                {
                    if (text.name == "TitleTxt")
                        text.text = runtime.Catalog.Text(item.titleKey);
                    else if (text.name == "OperationTxt") text.text = Price(item.moneyCostId);
                    else if (text.name == "VoucherCountTxt") text.text = runtime.Catalog.MoneyCost(item.moneyCostId)?.voucherCost.ToString("N0") ?? "0";
                    else if (text.name == "AwardCountText") text.text = item.fixedAward.FirstOrDefault()?.count.ToString("N0") ?? "0";
                    else if (text.name == "ExtraAwardCountText") text.text = "+" + (item.firstExtraAward.FirstOrDefault()?.count ?? 0).ToString("N0");
                    else if (text.name == "ValueTxt") text.text = runtime.Catalog.MoneyCost(item.moneyCostId)?.startValue.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
                });
            }
        }

        private void PopulateVoucherItems()
        {
            Transform content = pages[0].transform.Find("BgPanel1/Content/MiddleGo/DirectPay/DirectPayScrollRect/Content");
            CommercePrefab prefab = runtime.Catalog.Prefab("commerce_voucher");
            if (content == null || prefab?.root == null) return;
            foreach (CommerceVoucher item in runtime.Catalog.Data.vouchers)
            {
                GameObject cell = runtime.CreatePrefabRenderer().CreateSubtree(prefab.root, content,
                    (path, target) =>
                    {
                        Button button = target.GetComponent<Button>();
                        if (button != null && path == "/PayVoucherItem")
                        {
                            button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                            button.onClick.AddListener(() => OpenPurchase("voucher", item.id, 0));
                        }
                    });
                cell.name = "PayVoucherItem_" + item.id;
                voucherCells.Add(cell);
                SetDescendantText(cell.transform, "TitleTxt", runtime.Catalog.Text(item.titleKey));
                SetDescendantText(cell.transform, "OperationTxt", Price(item.moneyCostId));
                CommerceMoneyCost cost = runtime.Catalog.MoneyCost(item.moneyCostId);
                SetDescendantText(cell.transform, "AwardCountText", cost == null ? "0" : cost.voucherCost.ToString("N0"));
                foreach (Image image in cell.GetComponentsInChildren<Image>(true))
                    if (image.name == "IconImg") image.sprite = runtime.FindSprite(item.icon);
            }
            SwitchDirectPayTab(true);
        }

        private void SwitchDirectPayTab(bool direct)
        {
            foreach (GameObject cell in directCells) cell.SetActive(direct);
            foreach (GameObject cell in voucherCells) cell.SetActive(!direct);
            Transform directTop = pages[0].transform.Find("BgPanel1/Content/MiddleGo/DirectPay/TopGo/DirectPayTopGo");
            Transform voucherTop = pages[0].transform.Find("BgPanel1/Content/MiddleGo/DirectPay/TopGo/VoucherTopGo");
            if (directTop != null) directTop.gameObject.SetActive(direct);
            if (voucherTop != null) voucherTop.gameObject.SetActive(!direct);
        }

        private void PopulateWeeklyItems()
        {
            Transform content = pages[2].transform.Find("BgPanel1/Content/MiddleGo/PeriodicGift/PeriodicGiftScrollRect/ViewPort/Content");
            CommercePrefab prefab = runtime.Catalog.Prefab("commerce_gift");
            if (content == null || prefab?.root == null) return;
            CommerceGift[] gifts = runtime.Catalog.Data.weeklyGifts
                .Where(value => value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId > 0).ToArray();
            CommerceGift freeGift = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(value =>
                value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId <= 0);
            Transform freeAward = pages[2].transform.Find(
                "BgPanel1/Content/MiddleGo/PeriodicGift/TopGo/FreeAwardItem");
            if (freeAward != null)
            {
                Transform claim = freeAward.Find("BgImg/TakeAwardBtn");
                CommerceOffer freeOffer = freeGift == null ? null
                    : runtime.Quote("weekly", freeGift.id, freeGift.groupId);
                if (claim != null) claim.gameObject.SetActive(freeOffer != null);
                Transform taken = freeAward.Find("TakenGo");
                if (taken != null) taken.gameObject.SetActive(freeOffer == null && freeGift != null);
            }
            foreach (CommerceGift gift in gifts)
            {
                GameObject cell = runtime.CreatePrefabRenderer().CreateSubtree(prefab.root, content,
                    (path, target) =>
                    {
                        Button button = target.GetComponent<Button>();
                        if (button != null && path == "/PayGiftItem")
                        {
                            button.interactable = runtime.Quote("weekly", gift.id, gift.groupId) != null;
                            button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                            button.onClick.AddListener(() => OpenPurchase("weekly", gift.id, gift.groupId));
                        }
                    });
                cell.name = "WeeklyGift_" + gift.id;
                SetDescendantText(cell.transform, "ItemTitle", runtime.Catalog.Text(gift.titleKey));
                SetDescendantText(cell.transform, "PayTxt", gift.moneyCostId <= 0 ? "免费领取" : Price(gift.moneyCostId));
                SetDescendantText(cell.transform, "DiamondPayTxt", Price(gift.moneyCostId));
                SetDescendantText(cell.transform, "BugCountLimitTxt", "限购 " + gift.limit);
                bool soldOut = runtime.Quote("weekly", gift.id, gift.groupId) == null;
                SetDescendantActive(cell.transform, "SoldOutMask", soldOut);
                SetDescendantActive(cell.transform, "SoldoutGo", soldOut);
                Transform awardContent = FindDescendant(cell.transform, "AwardContent", "MoreItemContent");
                if (awardContent != null) PopulateRewardIcons(awardContent, ToRewardMap(gift.rewards));
            }
        }

        private void PopulateDailyItems()
        {
            Transform content = pages[1].transform.Find("BgDialog1/Content (1)/ContentGo");
            if (content == null) return;
            CommerceGift[] gifts = runtime.Catalog.Data.dailyGifts
                .Where(value => value.groupId == runtime.Session.State.DailyGroup).OrderBy(value => value.id).ToArray();
            Transform[] slots = content.Cast<Transform>()
                .Where(value => value.name.StartsWith("DailyPayGiftItemGo", StringComparison.Ordinal)).ToArray();
            for (int i = 0; i < Math.Min(slots.Length, gifts.Length); i++)
            {
                CommerceGift gift = gifts[i];
                Transform slot = slots[i];
                SetDescendantText(slot, "NameTxt", runtime.Catalog.Text(gift.titleKey));
                SetDescendantText(slot, "MoneyCostTxt", Price(gift.moneyCostId));
                CommerceMoneyCost cost = runtime.Catalog.MoneyCost(gift.moneyCostId);
                SetDescendantText(slot, "ValueTxt", cost == null ? "0" : cost.startValue.ToString("0.##", CultureInfo.InvariantCulture));
                CommerceOffer offer = runtime.Quote("daily", gift.id, gift.groupId);
                Transform buyButton = FindDescendant(slot, "BuyBtn");
                Button button = buyButton == null ? null : buyButton.GetComponent<Button>();
                if (button != null) button.interactable = offer != null;
                SetDescendantActive(slot, "Txt", offer == null);
                SetDescendantActive(slot, "Mask", offer == null);
                Transform awardContent = FindDescendant(slot, "AwardContent");
                if (awardContent != null) PopulateRewardIcons(awardContent, ToRewardMap(gift.rewards));
            }
            Transform freeRoot = pages[1].transform.Find("BgDialog1/Content (1)/TopGo/FreeAwardGo ");
            if (freeRoot != null)
            {
                bool canClaim = runtime.Offers.CanClaimDailyFree();
                Transform claim = freeRoot.Find("DescGo/BgImg (1)");
                if (claim != null) claim.gameObject.SetActive(canClaim);
                Transform taken = freeRoot.Find("TakenGo");
                if (taken != null) taken.gameObject.SetActive(!canClaim);
            }
        }

        private void PopulateFirstAwards()
        {
            Transform content = pages[3].transform.Find("BgImg/Content/AwardContent");
            if (content == null) return;
            CommerceFirstAward[] awards = runtime.Catalog.Data.firstAwards.OrderBy(value => value.day).ToArray();
            Transform[] slots = content.Cast<Transform>()
                .Where(value => value.name.StartsWith("FirstPayAwardItem", StringComparison.Ordinal)).ToArray();
            for (int i = 0; i < Math.Min(awards.Length, slots.Length); i++)
            {
                CommerceFirstAward award = awards[i];
                Dictionary<int, long> rewardMap = ToRewardMap(award.rewards);
                CommerceProfessionReward weapon = (award.professionItems ?? Array.Empty<CommerceProfessionReward>())
                    .FirstOrDefault(value => value.professions != null && value.professions.Contains(runtime.ProfessionId));
                if (weapon != null) AddReward(rewardMap, weapon.itemId, weapon.count);
                Transform awardContent = FindDescendant(slots[i], "AwardGo");
                if (awardContent != null) PopulateRewardIcons(awardContent, rewardMap);
                Transform claim = FindDescendant(slots[i], "TakeAwardBtn");
                Button button = claim == null ? null : claim.GetComponent<Button>();
                bool claimed = runtime.Offers.HasClaimedFirst(award.day);
                if (button != null) button.interactable = runtime.Offers.CanClaimFirst(award.day);
                SetDescendantActive(slots[i], "NoAwardImg", !claimed);
                SetDescendantActive(slots[i], "HasAwardImg", claimed);
            }
            bool firstPayAvailable = runtime.Session.State.FirstPurchaseDay < 0;
            Transform firstPage = pages[3].transform;
            Transform payButton = FindDescendant(firstPage, "MoneyPayBtn");
            if (payButton != null) payButton.gameObject.SetActive(firstPayAvailable);
            SetDescendantActive(firstPage, "PaySoldOutGo", !firstPayAvailable);
        }

        private void PopulateAccumulatedItems()
        {
            Transform content = pages[4].transform.Find("BgPanel/Content/AwardScroll/Viewport/Content");
            CommercePrefab prefab = runtime.Catalog.Prefab("commerce_accumulated_item");
            if (content == null || prefab?.root == null) return;
            foreach (CommerceAccumulatedAward award in runtime.Offers.CurrentAccumulatedAwards())
            {
                GameObject cell = runtime.CreatePrefabRenderer().CreateSubtree(prefab.root, content,
                    (path, target) =>
                    {
                        Button button = target.GetComponent<Button>();
                        if (button != null && path.Contains("/TakeAwardBtn/"))
                        {
                            button.interactable = runtime.Offers.CanClaimAccumulated(award.level);
                            button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                            button.onClick.AddListener(() => Apply(runtime.ClaimAccumulated(award.level)));
                        }
                    });
                cell.name = "AccumulatedAward_" + award.level;
                SetDescendantText(cell.transform, "LevelTxt", award.level.ToString(CultureInfo.InvariantCulture));
                SetDescendantText(cell.transform, "ScoreTxt", award.threshold.ToString("0", CultureInfo.InvariantCulture));
                bool claimed = runtime.Offers.HasClaimedAccumulated(award.level);
                bool canClaim = runtime.Offers.CanClaimAccumulated(award.level);
                SetDescendantActive(cell.transform, "LockedGo", !canClaim && !claimed);
                SetDescendantActive(cell.transform, "AwardAvailableImg", canClaim);
                SetDescendantActive(cell.transform, "LevelRequireBg", !canClaim && !claimed);
                SetDescendantActive(cell.transform, "TakenAwardMask", claimed);
                SetDescendantActive(cell.transform, "TakeAwardBtn", !claimed);
                Transform awardContent = FindDescendant(cell.transform, "CoreAwardGo", "SubAwardGo");
                if (awardContent != null) PopulateRewardIcons(awardContent, ToRewardMap(award.rewards));
            }
        }

        private void OpenPurchase(string kind, int id, int groupId)
        {
            CommerceOffer offer = runtime.Quote(kind, id, groupId);
            if (offer == null) { SetStatus("商品不可购买或已售罄。"); return; }
            if (kind == "weekly" && offer.MoneyCostId <= 0)
            {
                Apply(runtime.Offers.Purchase(kind, id, groupId, CommercePaymentMethod.CashConfirmation));
                return;
            }
            GameObject dialog = null;
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            dialog = renderer.Create("commerce_confirm", screen, (path, target) =>
            {
                Button button = target.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                if (path.EndsWith("MoneyPayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => ConfirmPurchase(offer, CommercePaymentMethod.CashConfirmation, dialog));
                else if (path.EndsWith("PayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => ConfirmPurchase(offer, CommercePaymentMethod.Voucher, dialog));
            });
            dialog.name = "CommercePurchaseConfirm";
            dialogs.Add(dialog);
            bool voucherAvailable = runtime.PaymentMethod(offer) == CommercePaymentMethod.Voucher;
            renderer.SetActive("/PayGiftConfirmDialog/BgDialog1/Content/PayBtnGo/PayBtn", voucherAvailable);
            renderer.SetActive("/PayGiftConfirmDialog/BgDialog1/Content/PayBtnGo/CommonPayBtn/MoneyPayBtn", !voucherAvailable);
            foreach (TMP_Text text in dialog.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text.Contains("common_format_args1")) text.text = RewardNames(offer.Rewards);
                else if (text.name.Contains("Title")) text.text = "确认购买";
            }
            Transform awardContent = dialog.transform.Find("BgDialog1/Content/AwardContent");
            if (awardContent != null) PopulateRewardIcons(awardContent, offer.Rewards);
            dialog.transform.SetAsLastSibling();
        }

        private void OpenFirstPayConfirmation()
        {
            if (runtime.Session.State.FirstPurchaseDay >= 0)
            {
                SetStatus("首购礼包已激活。");
                return;
            }
            GameObject dialog = null;
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            dialog = renderer.Create("commerce_confirm", screen, (path, target) =>
            {
                Button button = target.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(() => PlaySound("Main_Cursor_Normal"));
                if (path.EndsWith("MoneyPayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() =>
                    {
                        dialogs.Remove(dialog);
                        Destroy(dialog);
                        Apply(runtime.ActivateFirstPurchase());
                    });
            });
            dialog.name = "FirstPayLocalConfirmation";
            dialogs.Add(dialog);
            renderer.SetActive("/PayGiftConfirmDialog/BgDialog1/Content/PayBtnGo/PayBtn", false);
            foreach (TMP_Text text in dialog.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name.Contains("Title")) text.text = "确认开启首购礼包";
                else if (text.name.Contains("MoneyCost") || text.name.Contains("PayTxt")) text.text = Price(runtime.Catalog.Data.settings.firstPurchaseMoneyCostId);
            }
            dialog.transform.SetAsLastSibling();
        }

        private void ConfirmPurchase(CommerceOffer offer, CommercePaymentMethod method, GameObject dialog)
        {
            CommerceTransactionResult result = runtime.Offers.Purchase(offer.Kind, offer.Id, offer.GroupId, method);
            dialogs.Remove(dialog);
            Destroy(dialog);
            Apply(result);
        }

        private void ClaimDailyFree() => Apply(runtime.ClaimDailyFree());

        private void PopulateRewardIcons(Transform parent, IReadOnlyDictionary<int, long> rewards)
        {
            foreach (KeyValuePair<int, long> reward in rewards)
            {
                GameObject entry = new GameObject("Reward_" + reward.Key, typeof(RectTransform), typeof(Image));
                RectTransform rect = entry.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.sizeDelta = new Vector2(92, 92);
                Image image = entry.GetComponent<Image>();
                image.sprite = runtime.FindItemSprite(reward.Key);
                image.preserveAspect = true;
                TextMeshProUGUI count = AddText(entry.transform, reward.Value.ToString("N0"), 18, new Vector2(0, -54));
                count.rectTransform.sizeDelta = new Vector2(112, 30);
                count.color = Color.white;
            }
        }

        private static Dictionary<int, long> ToRewardMap(CommerceReward[] rewards)
        {
            var result = new Dictionary<int, long>();
            foreach (CommerceReward reward in rewards ?? Array.Empty<CommerceReward>()) AddReward(result, reward.itemId, reward.count);
            return result;
        }

        private static void AddReward(Dictionary<int, long> rewards, int itemId, long count)
        {
            rewards[itemId] = rewards.TryGetValue(itemId, out long current) ? current + count : count;
        }

        private static Transform FindDescendant(Transform root, params string[] names)
        {
            foreach (Transform child in root)
            {
                if (Array.IndexOf(names, child.name) >= 0) return child;
                Transform nested = FindDescendant(child, names);
                if (nested != null) return nested;
            }
            return null;
        }

        private void PlaySound(string eventName)
        {
            AudioClip clip = runtime == null ? null : runtime.FindAudio(eventName);
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        private void Exchange()
        {
            Apply(runtime.Exchange(exchangeAmount));
            ShowPage(5);
        }

        private void SetExchangeAmount(long amount)
        {
            CommerceSettings settings = runtime.Catalog.Data.settings;
            exchangeAmount = Math.Max(settings.exchangeMin, Math.Min(amount, Math.Min(settings.exchangeMax,
                Amount(settings.flareStarItemId))));
            SetDescendantText(pages[5].transform, "CountTxt", exchangeAmount.ToString(CultureInfo.InvariantCulture));
            Slider slider = pages[5].GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                slider.minValue = settings.exchangeMin;
                slider.maxValue = Math.Max(settings.exchangeMin, Math.Min(settings.exchangeMax, Amount(settings.flareStarItemId)));
                slider.wholeNumbers = true;
                slider.value = exchangeAmount;
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(value => { exchangeAmount = (long)value; SetDescendantText(pages[5].transform, "CountTxt", exchangeAmount.ToString(CultureInfo.InvariantCulture)); });
            }
        }

        private void ShowPage(int index)
        {
            if (pages.Count == 0) return;
            currentPage = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == currentPage);
            if (currentPage == 5) SetExchangeAmount(exchangeAmount);
            RefreshStatus();
        }

        private void Apply(CommerceTransactionResult result)
        {
            if (result == null) return;
            SetStatus(result.Message);
            if (result.Code != CommerceTransactionCode.Applied) return;
            int page = currentPage;
            foreach (GameObject oldPage in pages) Destroy(oldPage);
            pages.Clear();
            directCells.Clear();
            voucherCells.Clear();
            BuildPages();
            ShowPage(page);
        }

        private void RefreshStatus()
        {
            if (status == null || runtime?.Session == null) return;
            status.text = "耀星 " + Amount(runtime.Catalog.Data.settings.flareStarItemId).ToString("N0")
                + "　代金券 " + Amount(runtime.Catalog.Data.settings.voucherItemId).ToString("N0");
        }

        private long Amount(int id) => runtime.Session.State.Inventory.Where(value => value.ItemId == id)
            .Select(value => value.Count).DefaultIfEmpty(0).First();

        private string Price(int id)
        {
            CommerceMoneyCost cost = runtime.Catalog.MoneyCost(id);
            return cost == null ? "-" : "¥" + cost.startValue.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private string RewardNames(params CommerceReward[][] groups) => string.Join(" / ", groups
            .SelectMany(value => value ?? Array.Empty<CommerceReward>())
            .Select(value => runtime.Catalog.ItemName(value.itemId) + " ×" + value.count));

        private string RewardNames(IReadOnlyDictionary<int, long> rewards) => string.Join(" / ", rewards
            .Select(value => runtime.Catalog.ItemName(value.Key) + " ×" + value.Value));

        private static void SetTexts(Transform root, Action<TMP_Text> update)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) update(text);
        }

        private static void SetDescendantText(Transform root, string name, string value)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == name) text.text = value;
        }

        private static void SetDescendantActive(Transform root, string name, bool active)
        {
            Transform target = FindDescendant(root, name);
            if (target != null) target.gameObject.SetActive(active);
        }

        private static int ReadLevel(Transform target)
        {
            Transform current = target;
            while (current.parent != null && !current.name.StartsWith("AccumulatedAward_", StringComparison.Ordinal))
                current = current.parent;
            return int.TryParse(current.name.Replace("AccumulatedAward_", string.Empty), out int level) ? level : 0;
        }

        private Transform CreatePanel(string title, Vector2 size)
        {
            GameObject panel = new GameObject("ProfileSelection", typeof(RectTransform), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(screen, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);
            AddText(panel.transform, title, 38, new Vector2(0, 270));
            return panel.transform;
        }

        private void ShowMessage(string message)
        {
            ClearScreen();
            Transform panel = CreatePanel("商城", new Vector2(860, 500));
            AddText(panel, message, 24, Vector2.zero);
        }

        private void SetStatus(string value)
        {
            if (status != null) status.text = value;
            Debug.Log("Commerce: " + value);
        }

        private void ClearScreen()
        {
            pages.Clear();
            dialogs.Clear();
            status = null;
            if (screen != null)
                for (int i = screen.childCount - 1; i >= 0; i--) Destroy(screen.GetChild(i).gameObject);
        }

        private static TextMeshProUGUI AddText(Transform parent, string value, float size, Vector2 position)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(720, 76);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void CreateButton(Transform parent, string label, Vector2 position, Action action, bool stretch = false)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (stretch) go.AddComponent<LayoutElement>().minWidth = 100;
            else
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = new Vector2(580, 76);
            }
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.28f, 0.34f, 0.4f, 1);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { PlaySound("Main_Cursor_Normal"); action(); });
            AddText(go.transform, label, stretch ? 20 : 28, Vector2.zero);
        }
    }
}
