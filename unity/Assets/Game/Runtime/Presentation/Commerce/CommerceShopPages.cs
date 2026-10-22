using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Commerce;

namespace Zjcs.Presentation.Commerce
{
    /// <summary>Daily and weekly gift pages, built from the official prefab trees.</summary>
    public sealed partial class CommerceScreenController
    {
        private void OpenDaily()
        {
            CloseDialogKind(DialogKind.Daily);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(DailyPrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/CloseBtn", StringComparison.Ordinal)) button.onClick.AddListener(CloseTopDialog);
                else if (path.EndsWith("/RuleBtn", StringComparison.Ordinal)) button.onClick.AddListener(OpenDailyRule);
                else if (path.EndsWith("/BuyAllBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => OpenPurchase(runtime.Quote("all", 0, runtime.Session.State.DailyGroup)));
            });
            dialog.name = "CommerceDaily";
            RegisterDialog(dialog, DialogKind.Daily);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleText",
                runtime.Catalog.Text("ui_daily_pay_gift_title"));
            Transform keepTime = FindDeep(dialog.transform, "KeepTimeGo");
            if (keepTime != null)
                SetText(keepTime, "RemainTimeTxt",
                    "刷新时间：<color=#E5DD65>" + TimeUntil(runtime.Session.State.Day, false) + "</color>");
            CommerceDailyGroup group = runtime.Catalog.Data.dailyGroups.FirstOrDefault(value =>
                value.groupId == runtime.Session.State.DailyGroup);
            CommerceMoneyCost bundle = group == null ? null : runtime.Catalog.MoneyCost(group.bundleMoneyCostId);
            Transform bottom = FindDeep(dialog.transform, "BottomGo");
            if (bottom != null)
                SetText(bottom, "AccumulatedValueDescGo/ValueTxt",
                    (bundle == null ? 0 : bundle.startValue).ToString("0.##", CultureInfo.InvariantCulture));
            PopulateDailyItems(dialog.transform);
            PopulateDailyFree(dialog.transform);
            PopulateDailyBuyAll(dialog.transform);
            OfficialCommercePrefabRenderer.RebuildLayout(dialog);
        }

        private void PopulateDailyItems(Transform dialog)
        {
            CommerceGift[] gifts = runtime.Catalog.Data.dailyGifts
                .Where(value => value.groupId == runtime.Session.State.DailyGroup)
                .OrderBy(value => value.id).ToArray();
            Transform content = Find(dialog, "BgDialog1/Content (1)/ContentGo")
                ?? FindDeep(dialog, "ContentGo");
            if (content == null) return;
            string[] slots = { "DailyPayGiftItemGo", "DailyPayGiftItemGo (1)", "DailyPayGiftItemGo (2)" };
            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot = Find(content, slots[i]) ?? FindDeep(content, slots[i]);
                if (slot == null) continue;
                if (i >= gifts.Length)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }
                slot.gameObject.SetActive(true);
                PopulateDailyItem(slot, gifts[i]);
            }
        }

        private void PopulateDailyItem(Transform slot, CommerceGift gift)
        {
            CommerceOffer offer = runtime.Quote("daily", gift.id, gift.groupId);
            bool canBuy = offer != null;
            bool boughtAll = runtime.Offers.IsDailyGroupBought();
            bool bought = !canBuy && (boughtAll || runtime.Offers.DailyRemaining(gift.id) == 0);
            SetText(slot, "ContentGo/NameTxt", runtime.Catalog.Text(gift.titleKey));
            CommerceMoneyCost cost = runtime.Catalog.MoneyCost(gift.moneyCostId);
            SetText(slot, "ContentGo/AccumulatedValueDescGo/ValueTxt",
                (cost?.startValue ?? 0).ToString("0.##", CultureInfo.InvariantCulture));
            AddRewardIcons(Find(slot, "ContentGo/AwardContent"),
                OfflineCommerceOffers.ToDictionary(gift.rewards));

            Transform buyButton = Find(slot, "BuyBtnGo/BuyGo/BuyBtn");
            Button button = buyButton == null ? null : buyButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(PlayClick);
                button.onClick.AddListener(() => OpenPurchase(runtime.Quote("daily", gift.id, gift.groupId)));
                button.interactable = canBuy;
            }
            Transform buyGo = Find(slot, "BuyBtnGo");
            if (buyGo != null) buyGo.gameObject.SetActive(canBuy);
            SetActive(slot, "Txt", bought);
            SetActive(slot, "Mask", bought);
            bool voucher = offer != null && runtime.PaymentMethod(offer) == CommercePaymentMethod.Voucher;
            SetActive(slot, "BuyBtnGo/BuyGo/VourcherInfoGo", voucher);
            SetActive(slot, "BuyBtnGo/BuyGo/MoneyCostTxt", !voucher);
            if (voucher)
            {
                SetText(slot, "BuyBtnGo/BuyGo/VourcherInfoGo/VoucherCountTxt", offer.VoucherCost.ToString("N0"));
                Image icon = Find(slot, "BuyBtnGo/BuyGo/VourcherInfoGo/Icon")?.GetComponent<Image>();
                if (icon != null) icon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.voucherItemId);
            }
            else
            {
                SetText(slot, "BuyBtnGo/BuyGo/MoneyCostTxt", MoneyPrice(gift.moneyCostId));
            }
        }

        private void PopulateDailyFree(Transform dialog)
        {
            Transform free = Find(dialog, "BgDialog1/Content (1)/TopGo/FreeAwardGo ")
                ?? FindDeep(dialog, "FreeAwardGo");
            if (free == null)
            {
                return;
            }
            bool canClaim = runtime.Offers.CanClaimDailyFree();
            SetActive(free, "DescGo", canClaim);
            SetActive(free, "TakenGo", !canClaim);
            SetText(free, "DescGo/DescTxt", Localized("ui_daily_pay_gift_free", "免费赠礼"));
            SetActive(free, "DescGo/RedDot1 (1)", canClaim);
            Transform claim = Find(free, "DescGo/BgImg (1)") ?? FindDeep(free, "BgImg (1)");
            Button button = claim == null ? null : claim.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(PlayClick);
                button.onClick.AddListener(() => ApplyResult(runtime.ClaimDailyFree()));
            }
            Image icon = Find(free, "DescGo/IconImg")?.GetComponent<Image>();
            if (icon != null) icon.sprite = runtime.FindItemSprite(
                runtime.Catalog.Data.settings.dailyFreeRewards.FirstOrDefault()?.itemId ?? 0);
        }

        private void PopulateDailyBuyAll(Transform dialog)
        {
            Transform panel = Find(dialog, "BgDialog1/Content (1)/BottomGo/BuyAllBtnGo")
                ?? FindDeep(dialog, "BuyAllBtnGo");
            if (panel == null) return;
            int groupId = runtime.Session.State.DailyGroup;
            CommerceDailyGroup group = runtime.Catalog.Data.dailyGroups.FirstOrDefault(value => value.groupId == groupId);
            CommerceGift[] gifts = runtime.Catalog.Data.dailyGifts.Where(value => value.groupId == groupId).ToArray();
            bool already = runtime.Offers.IsDailyGroupBought();
            bool canBuy = runtime.Offers.CanBuyAllDaily();
            SetActive(panel, "CanBuyImg", canBuy);
            SetActive(panel, "CannotBuyGo", !canBuy);
            SetActive(panel, "SoldOutMask", already);
            SetText(panel, "CannotBuyGo/Text", already
                ? runtime.Catalog.Text("ui_daily_pay_gift_already_buy") : runtime.Catalog.Text("ui_daily_pay_gift_buy_all"));
            SetText(panel, "InfoGo/BuyAllTxt", runtime.Catalog.Text("ui_daily_pay_gift_buy_all"));
            CommerceMoneyCost bundle = group == null ? null : runtime.Catalog.MoneyCost(group.bundleMoneyCostId);
            string bundlePrice = bundle == null ? "0" : bundle.voucherCost.ToString("N0");
            SetText(panel, "InfoGo/PriceGo/VourcherInfoGo/VoucherCountTxt", bundlePrice);
            SetText(panel, "InfoGo/PriceGo/PriceTxt", string.Empty);
            Image voucherIcon = Find(panel, "InfoGo/PriceGo/VourcherInfoGo/Icon")?.GetComponent<Image>();
            if (voucherIcon != null) voucherIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.voucherItemId);
            long original = gifts.Sum(value => runtime.Catalog.MoneyCost(value.moneyCostId)?.voucherCost ?? 0);
            SetText(panel, "InfoGo/OriginalPriceTxt", original.ToString("N0"));
            Transform buy = Find(panel, "BuyAllBtn");
            Button button = buy == null ? null : buy.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(PlayClick);
                button.onClick.AddListener(() => OpenPurchase(runtime.Quote("all", 0, groupId)));
                button.interactable = canBuy;
            }
            SetActive(panel, "AdditionInfoTip", false);
        }

        private void OpenDailyRule()
        {
            ShowRule("DailyPayGiftRule");
        }

        private void OpenWeekly()
        {
            CloseDialogKind(DialogKind.Weekly);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(WeeklyPrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/CloseBtn", StringComparison.Ordinal)) button.onClick.AddListener(CloseTopDialog);
                else if (path.EndsWith("/TakeAwardBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(ClaimWeeklyFree);
            });
            dialog.name = "CommerceWeekly";
            RegisterDialog(dialog, DialogKind.Weekly);
            StretchFull(Find(dialog.transform, "BgPanel1/Content"));
            SetText(dialog.transform, "BgPanel1/Content/TopGo/PanelTitle/TitleGo/TitleTxt",
                runtime.Catalog.Text("ui_weekly_pay_gift_title"));
            SetText(dialog.transform, "BgPanel1/Content/MiddleGo/PeriodicGift/TopGo/TimeGo/TimeTxt",
                TimeUntil(OfflineCommerceTransaction.Period("weekly", runtime.Session.State.Day), true));
            PopulateWeeklyFree(dialog.transform);
            PopulateWeeklyItems(dialog.transform);
            OfficialCommercePrefabRenderer.RebuildLayout(dialog);
        }

        private void PopulateWeeklyFree(Transform dialog)
        {
            Transform free = Find(dialog, "BgPanel1/Content/MiddleGo/PeriodicGift/TopGo/FreeAwardItem");
            if (free == null) return;
            CommerceGift gift = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(value =>
                value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId <= 0);
            bool available = gift != null && runtime.Quote("weekly", gift.id, gift.groupId) != null;
            SetActive(free, "BgImg/TakeAwardBtn", available);
            SetActive(free, "TakenGo", !available && gift != null);
            SetActive(free, "RedDot1", available);
            SetText(free, "DescGo/DescTxt", gift == null ? "每周免费礼包"
                : runtime.Catalog.Text(gift.titleKey));
            Image icon = Find(free, "IconImg")?.GetComponent<Image>();
            if (icon != null) icon.sprite = runtime.FindItemSprite(gift?.rewards.FirstOrDefault()?.itemId ?? 0);
        }

        private void ClaimWeeklyFree()
        {
            CommerceGift gift = runtime.Catalog.Data.weeklyGifts.FirstOrDefault(value =>
                value.groupId == runtime.Session.State.WeeklyGroup && value.moneyCostId <= 0);
            if (gift == null) return;
            ApplyResult(runtime.ClaimWeeklyFree(gift.id, gift.groupId));
        }

        private void PopulateWeeklyItems(Transform dialog)
        {
            Transform content = Find(dialog, "BgPanel1/Content/MiddleGo/PeriodicGift/PeriodicGiftScrollRect/ViewPort/Content");
            if (content == null) return;
            ClearChildren(content);
            foreach (CommerceGift gift in runtime.Offers.WeeklyGifts())
            {
                if (gift.moneyCostId <= 0) continue;
                CommerceGift row = gift;
                OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
                GameObject cell = renderer.Create(GiftPrefab, content, (path, node) =>
                {
                    Button button = node.GetComponent<Button>();
                    if (button != null && path == "/PayGiftItem")
                    {
                        button.onClick.AddListener(PlayClick);
                        button.onClick.AddListener(() => OpenPurchase(runtime.Quote("weekly", row.id, row.groupId)));
                    }
                });
                cell.name = "CommerceWeekly_" + gift.id;
                PopulateWeeklyItem(cell.transform, gift);
            }
            OfficialCommercePrefabRenderer.RebuildLayout(content.gameObject);
        }

        private void PopulateWeeklyItem(Transform cell, CommerceGift gift)
        {
            bool soldOut = runtime.Offers.IsWeeklySoldOut(gift);
            int remaining = runtime.Offers.WeeklyRemaining(gift);
            SetText(cell, "Content/ItemTitle", runtime.Catalog.Text(gift.titleKey));
            SetText(cell, "Content/BugCountLimitTxt", soldOut ? string.Empty
                : "可购买 " + remaining + " 次");
            SetActive(cell, "Content/BugCountLimitTxt", !soldOut);
            SetActive(cell, "Content/SoldOutMask", soldOut);
            SetActive(cell, "Content/SoldoutGo", soldOut);
            SetActive(cell, "Content/RedDot1", false);
            SetActive(cell, "Content/NewMark", false);
            SetText(cell, "Content/Pay/MoneyGo/PayTxt", MoneyPrice(gift.moneyCostId));
            SetActive(cell, "Content/Pay/MoneyGo", !soldOut);
            SetActive(cell, "Content/Pay/FreeText", false);
            Image payIcon = Find(cell, "Content/Pay/MoneyGo/Icon")?.GetComponent<Image>();
            if (payIcon != null) payIcon.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.voucherItemId);
            SetActive(cell, "Content/GreatValue/GreateValueGroup/GreatValueGoVertical", true);
            SetActive(cell, "Content/GreatValue/GreateValueGroup/GreatValueGoHorizon", false);
            SetActive(cell, "Content/GreatValue/GreateValueGroup/GreatValueGoVertical2", false);
            SetText(cell, "Content/GreatValue/GreateValueGroup/GreatValueGoVertical/GreatValuePercentTxt",
                gift.greatValue + "%");
            AddRewardIcons(Find(cell, "Content/ItemContent/MoreItemContent"),
                OfflineCommerceOffers.ToDictionary(gift.rewards));
            PopulateGiftIcon(cell, gift);
        }

        private void PopulateGiftIcon(Transform cell, CommerceGift gift)
        {
            Transform slot = Find(cell, "Content/IconSlot");
            if (slot == null) return;
            ClearChildren(slot);
            string key = "commerce_icon_" + gift.moneyCostId;
            CommercePrefab iconPrefab = runtime.Catalog.Prefab(key);
            if (iconPrefab != null)
            {
                OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
                GameObject icon = renderer.Create(key, slot);
                icon.name = "GiftIcon";
                RectTransform rect = icon.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                // The official gift-icon composite is authored at its card size; keep the
                // prefab's aspect while fitting the item's icon slot.
                float scale = Mathf.Min(1f, 260f / Mathf.Max(1f, rect.sizeDelta.x));
                rect.localScale = new Vector3(scale, scale, 1f);
                Image[] images = icon.GetComponentsInChildren<Image>(true);
                CommerceReward[] rewards = gift.rewards ?? Array.Empty<CommerceReward>();
                foreach (Image image in images)
                {
                    if (image.name.StartsWith("Start", StringComparison.Ordinal))
                    {
                        image.sprite = runtime.FindItemSprite(runtime.Catalog.Data.settings.flareStarItemId);
                        image.preserveAspect = true;
                    }
                }
                int index = 0;
                foreach (Image image in images)
                {
                    if (!image.name.StartsWith("ItemImg", StringComparison.Ordinal)
                        && !image.name.StartsWith("ItemIImg", StringComparison.Ordinal)) continue;
                    if (index < rewards.Length)
                    {
                        image.sprite = runtime.FindItemSprite(rewards[index].itemId);
                        image.preserveAspect = true;
                        AddGiftCount(image.transform, rewards[index].count);
                    }
                    index++;
                }
            }
            else
            {
                CreateItemIcon(slot, gift.rewards.FirstOrDefault()?.itemId ?? 0,
                    gift.rewards.FirstOrDefault()?.count ?? 0, false, 160f);
            }
        }

        private void AddGiftCount(Transform parent, long count)
        {
            var go = new GameObject("GiftCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-4f, 2f);
            rect.sizeDelta = new Vector2(56f, 22f);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = runtime.ChineseTmpFont;
            text.fontSize = 15f;
            text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = FormatCount(count);
        }
    }
}





