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
    /// <summary>First-pay, accumulated-pay, exchange, rule and price-table pages.</summary>
    public sealed partial class CommerceScreenController
    {
        // ---------- first pay ----------

        private void OpenFirstPay()
        {
            CloseDialogKind(DialogKind.FirstPay);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(FirstPrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/MoneyPayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(OpenFirstPayConfirm);
                else if (path.EndsWith("/TakeAwardBtn", StringComparison.Ordinal))
                {
                    int day = ReadFirstAwardDay(path);
                    button.onClick.AddListener(() => ApplyResult(runtime.ClaimFirstAward(day)));
                }
                else if (path.EndsWith("/Item1", StringComparison.Ordinal) || path.EndsWith("/Item2", StringComparison.Ordinal))
                    button.onClick.AddListener(() => ShowToast("武器比较与详情预览需要装备数据，当前展示首购官方武器图。"));
            });
            dialog.name = "CommerceFirst";
            RegisterDialog(dialog, DialogKind.FirstPay);
            bool active = runtime.Offers.FirstPayIsActive();
            SetText(dialog.transform, "BgImg/Content/TitleGo/InfoBg/InfoCN",
                "购买礼包可获得 <color=#ffb849>传说</color> 武器与三天奖励");
            SetActive(dialog.transform, "BgImg/Content/TitleGo/InfoBg/InfoUS", false);
            SetText(dialog.transform, "BgImg/Content/KeepTimeGo/DescTxt", "活动时间：首购礼包长期开放");
            SetActive(dialog.transform, "BgImg/Content/BottomGo/PaySoldOutGo", active);
            SetActive(dialog.transform, "BgImg/Content/BottomGo/CommonPayBtn", !active);
            if (!active)
            {
                SetText(dialog.transform, "BgImg/Content/BottomGo/CommonPayBtn/MoneyPayBtn/Content/Text", MoneyPrice(runtime.Catalog.Data.settings.firstPurchaseMoneyCostId));
                SetText(dialog.transform, "BgImg/Content/BottomGo/CommonPayBtn/AccumulatedValueDescGo/ValueTxt",
                    (runtime.Catalog.MoneyCost(runtime.Catalog.Data.settings.firstPurchaseMoneyCostId)?.startValue ?? 0)
                        .ToString("0.##", CultureInfo.InvariantCulture));
            }
            PopulateFirstAwards(dialog.transform);
            PopulateFirstPreview(dialog.transform);
            OfficialCommercePrefabRenderer.RebuildLayout(dialog);
        }

        private string FirstPayTitle()
        {
            CommerceMoneyCost cost = runtime.Catalog.MoneyCost(runtime.Catalog.Data.settings.firstPurchaseMoneyCostId);
            string price = cost == null ? "-" : "¥" + cost.startValue.ToString("0.##", CultureInfo.InvariantCulture);
            return "首购礼包 " + price + " 可获传说武器与三天奖励";
        }

        private static int ReadFirstAwardDay(string path)
        {
            if (path.Contains("(2)")) return 3;
            if (path.Contains("(1)")) return 2;
            return 1;
        }

        private void PopulateFirstAwards(Transform dialog)
        {
            string[] items = { "FirstPayAwardItem", "FirstPayAwardItem (1)", "FirstPayAwardItem (2)" };
            CommerceFirstAward[] awards = runtime.Catalog.Data.firstAwards.OrderBy(value => value.day).ToArray();
            for (int i = 0; i < items.Length && i < awards.Length; i++)
            {
                CommerceFirstAward award = awards[i];
                Transform row = Find(dialog, "BgImg/Content/AwardContent/" + items[i]);
                if (row == null) continue;
                SetText(row, "Content/AwardGo/DescTxt", "第" + (i + 1) + "天");
                bool active = runtime.Offers.FirstPayIsActive();
                bool claimed = runtime.Offers.HasClaimedFirst(award.day);
                bool canClaim = runtime.Offers.CanClaimFirst(award.day);
                SetActive(row, "Content/AwardGo/ImgGo/NoAwardImg", !claimed);
                SetActive(row, "Content/AwardGo/ImgGo/HasAwardImg", claimed);
                SetText(row, "Content/AwardGo/AwardStateTxt", FirstAwardState(award.day, active, claimed, canClaim));
                Transform take = Find(row, "Content/TakeAwardBtn");
                Button button = take == null ? null : take.GetComponent<Button>();
                if (button != null) button.interactable = canClaim;
                SetActive(row, "RedDot1", canClaim);

                CommerceProfessionReward weapon = runtime.Offers.FirstAwardWeapon(award,
                    runtime.ProfessionId, award.day);
                var rewards = new List<KeyValuePair<int, long>>();
                if (weapon != null) rewards.Add(new KeyValuePair<int, long>(weapon.itemId, weapon.count));
                foreach (CommerceReward reward in award.rewards ?? Array.Empty<CommerceReward>())
                    rewards.Add(new KeyValuePair<int, long>(reward.itemId, reward.count));
                Transform first = Find(row, "Content/AwardGo/AwardSlotsGo/FirstAwardSlot");
                Transform other = Find(row, "Content/AwardGo/AwardSlotsGo/OtherAwardSlot");
                PopulateAwardSlot(first, rewards.Count > 0 ? rewards[0] : default, 0);
                PopulateAwardSlot(Find(other, "FirstSlot"), rewards.Count > 1 ? rewards[1] : default, 1);
                PopulateAwardSlot(Find(other, "SecondSlot"), rewards.Count > 2 ? rewards[2] : default, 2);
            }
        }

        private string FirstAwardState(int day, bool active, bool claimed, bool canClaim)
        {
            if (!active) return "未激活";
            if (claimed) return "已领取";
            if (canClaim) return "可领取";
            int available = runtime.Offers.FirstPayTodayCanGetAwardIndex();
            if (day > available) return "第" + day + "天可领取";
            return "可领取";
        }

        private void PopulateAwardSlot(Transform slot, KeyValuePair<int, long> reward, int index)
        {
            if (slot == null) return;
            ClearChildren(slot);
            if (reward.Key <= 0) return;
            CreateItemIcon(slot, reward.Key, reward.Value, false, index == 0 ? 104f : 78f);
        }

        private void PopulateFirstPreview(Transform dialog)
        {
            Image main = Find(dialog, "BgImg/Content/CenterGo/Item/Item")?.GetComponent<Image>();
            if (main != null) main.sprite = runtime.FindSprite("ImgItem_FirstPay");
            Image secondary = Find(dialog, "BgImg/Content/CenterGo/Item/Item (1)")?.GetComponent<Image>();
            if (secondary != null) secondary.sprite = runtime.FindSprite("ImgItem_FirstPay1");
            Image sword = Find(dialog, "BgImg/Content/CenterGo/EquitGo/Equit1/Item1")?.GetComponent<Image>();
            if (sword != null) sword.sprite = runtime.FindSprite("ImgItem_FirstPay1");
            Image staff = Find(dialog, "BgImg/Content/CenterGo/EquitGo/Equit2/Item2")?.GetComponent<Image>();
            if (staff != null) staff.sprite = runtime.FindSprite("ImgItem_FirstPay2");
        }

        private void OpenFirstPayConfirm()
        {
            if (runtime.Offers.FirstPayIsActive())
            {
                ShowToast("首购礼包已激活。");
                return;
            }
            OpenSimpleConfirm("确认开启首购礼包\n" + FirstPayTitle(), "确认支付",
                () => ApplyResult(runtime.ActivateFirstPurchase()));
        }

        // ---------- accumulated ----------

        private void OpenAccumulated()
        {
            CloseDialogKind(DialogKind.Accumulated);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(AccumulatedPrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/CloseBtn", StringComparison.Ordinal)) button.onClick.AddListener(CloseTopDialog);
                else if (path.EndsWith("/RuleBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => ShowPriceTable("AccumulatedPayRule"));
            });
            dialog.name = "CommerceAccumulated";
            RegisterDialog(dialog, DialogKind.Accumulated);
            StretchFull(Find(dialog.transform, "BgPanel/Content"));
            SetText(dialog.transform, "BgPanel/Content/TopGo/TitleTxt",
                runtime.Catalog.Text("ui_accumulated_pay_title"));
            PopulateAccumulatedItems(dialog.transform);
            OfficialCommercePrefabRenderer.RebuildLayout(dialog);
        }

        private void PopulateAccumulatedItems(Transform dialog)
        {
            Transform content = Find(dialog, "BgPanel/Content/AwardScroll/Viewport/Content");
            if (content == null) return;
            ClearChildren(content);
            foreach (CommerceAccumulatedAward award in runtime.Offers.CurrentAccumulatedAwards())
            {
                GameObject row = CreateAccumulatedRow(content, award);
                row.name = "Accumulated_" + award.level;
            }
            OfficialCommercePrefabRenderer.RebuildLayout(content.gameObject);
        }

        private GameObject CreateAccumulatedRow(Transform content, CommerceAccumulatedAward award)
        {
            var row = new GameObject("AccumulatedRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.SetParent(content, false);
            rect.sizeDelta = new Vector2(620, 130);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 12;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 6, 6);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(row.transform, false);
            labelRect.sizeDelta = new Vector2(150, 110);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = runtime.ChineseTmpFont;
            label.fontSize = 22;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            label.text = "Lv." + award.level + "\n星值 " + award.threshold.ToString("0");

            var rewards = award.rewards ?? Array.Empty<CommerceReward>();
            for (int i = 0; i < Math.Min(4, rewards.Length); i++)
                CreateItemIcon(row.transform, rewards[i].itemId, rewards[i].count, false, i == 0 ? 100f : 72f);

            bool claimed = runtime.Offers.HasClaimedAccumulated(award.level);
            bool canClaim = runtime.Offers.CanClaimAccumulated(award.level);
            GameObject buttonObject = CreateTextButton(row.transform, claimed ? "已领取" : canClaim ? "领取" : "未达成",
                new Vector2(112, 56));
            Button button = buttonObject.GetComponent<Button>();
            button.interactable = canClaim;
            button.onClick.AddListener(PlayClick);
            int level = award.level;
            button.onClick.AddListener(() => ApplyResult(runtime.ClaimAccumulated(level)));
            return row;
        }

        // ---------- exchange ----------

        private void OpenExchange()
        {
            CloseDialogKind(DialogKind.Exchange);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(ExchangePrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/CancelBtn", StringComparison.Ordinal)) button.onClick.AddListener(CloseTopDialog);
                else if (path.EndsWith("/ConfirmBtn", StringComparison.Ordinal)) button.onClick.AddListener(ConfirmExchange);
                else if (path.EndsWith("/CommonBtn_Reduce", StringComparison.Ordinal))
                    button.onClick.AddListener(() => SetExchangeAmount(exchangeAmount - 1));
                else if (path.EndsWith("/CommonBtn_Add", StringComparison.Ordinal))
                    button.onClick.AddListener(() => SetExchangeAmount(exchangeAmount + 1));
                else if (path.EndsWith("/MaxBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() => SetExchangeAmount(runtime.Offers.ExchangeMax()));
            });
            dialog.name = "CommerceExchange";
            RegisterDialog(dialog, DialogKind.Exchange);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleTxt", "兑换耀星");
            SetText(dialog.transform, "BgDialog1/Content/TipExchangeTxt", "耀星可兑换为等量晨星，兑换后不可撤销。");
            PopulateExchangeItems(dialog.transform);
            long held = Amount(runtime.Catalog.Data.settings.flareStarItemId);
            exchangeAmount = held <= 0 ? 0
                : Math.Max(runtime.Catalog.Data.settings.exchangeMin,
                    Math.Min(exchangeAmount <= 0 ? runtime.Catalog.Data.settings.exchangeMin : exchangeAmount,
                        runtime.Offers.ExchangeMax()));
            ConfigureExchangeSlider(dialog.transform);
            SetExchangeAmount(exchangeAmount);
            OfficialCommercePrefabRenderer.RebuildLayout(dialog.gameObject);
        }

        private void PopulateExchangeItems(Transform dialog)
        {
            PopulateExchangeItem(Find(dialog, "BgDialog1/Content/ItemGo/FrontItem/FrontItem"), runtime.Catalog.Data.settings.flareStarItemId);
            PopulateExchangeItem(Find(dialog, "BgDialog1/Content/ItemGo/BackItem/BackItem"), runtime.Catalog.Data.settings.diamondItemId);
            Image change = Find(dialog, "BgDialog1/Content/ItemGo/changeImg")?.GetComponent<Image>();
            if (change != null) change.sprite = runtime.FindSprite("ImgReturn");
        }

        private void PopulateExchangeItem(Transform item, int itemId)
        {
            if (item == null) return;
            Image quality = Find(item, "Content/QualityImg")?.GetComponent<Image>();
            if (quality != null) quality.sprite = runtime.FindSprite("ImgQuality_Blue");
            Image icon = Find(item, "Content/mask/icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = runtime.FindItemSprite(itemId);
                icon.preserveAspect = true;
            }
            SetText(item, "Content/BgCountText/CountText", "0");
            SetText(item, "Content/BottomText", runtime.Catalog.ItemName(itemId));
        }

        private void ConfigureExchangeSlider(Transform dialog)
        {
            Slider slider = Find(dialog, "BgDialog1/Content/ChangGo/SetCountSlider/SliderGo/Slider")?.GetComponent<Slider>();
            if (slider == null) return;
            long max = Math.Max(1, runtime.Offers.ExchangeMax());
            slider.minValue = runtime.Catalog.Data.settings.exchangeMin;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(value => SetExchangeAmount((long)value, false));
        }

        private void SetExchangeAmount(long amount, bool updateSlider = true)
        {
            long held = Amount(runtime.Catalog.Data.settings.flareStarItemId);
            long max = Math.Max(0, Math.Min(held, runtime.Catalog.Data.settings.exchangeMax));
            exchangeAmount = held <= 0 ? 0 : Math.Max(runtime.Catalog.Data.settings.exchangeMin, Math.Min(amount, max));
            long back = exchangeAmount * runtime.Catalog.Data.settings.exchangeScale;
            Transform dialog = currentDialogObject == null ? null : currentDialogObject.transform;
            if (dialog == null) return;
            SetText(dialog, "BgDialog1/Content/ChangGo/SetCountSlider/SelectedCountTxt",
                "兑换数量：" + exchangeAmount.ToString("N0"));
            SetText(dialog, "BgDialog1/Content/ItemGo/FrontItem/FrontItem/Content/BgCountText/CountText",
                exchangeAmount.ToString("N0"));
            SetText(dialog, "BgDialog1/Content/ItemGo/BackItem/BackItem/Content/BgCountText/CountText",
                back.ToString("N0"));
            SetText(dialog, "BgDialog1/Content/ItemGo/FrontItem/FrontItem/Content/BottomText",
                runtime.Catalog.ItemName(runtime.Catalog.Data.settings.flareStarItemId));
            SetText(dialog, "BgDialog1/Content/ItemGo/BackItem/BackItem/Content/BottomText",
                runtime.Catalog.ItemName(runtime.Catalog.Data.settings.diamondItemId));
            if (updateSlider)
            {
                Slider slider = Find(dialog, "BgDialog1/Content/ChangGo/SetCountSlider/SliderGo/Slider")?.GetComponent<Slider>();
                if (slider != null)
                {
                    slider.minValue = runtime.Catalog.Data.settings.exchangeMin;
                    slider.maxValue = Math.Max(slider.minValue, max);
                    slider.SetValueWithoutNotify(exchangeAmount);
                }
            }
        }

        private void ConfirmExchange()
        {
            long held = Amount(runtime.Catalog.Data.settings.flareStarItemId);
            if (held <= 0 || exchangeAmount <= 0 || exchangeAmount > held)
            {
                OpenSimpleConfirm("耀星不足。是否前往直购获取耀星？", "前往直购", () =>
                {
                    CloseDialogKind(DialogKind.Exchange);
                    SetMainTab(MainTab.DirectPay);
                    SwitchPayTab(true);
                });
                return;
            }
            CommerceTransactionResult result = runtime.Exchange(exchangeAmount);
            if (result.Code == CommerceTransactionCode.Applied)
            {
                ApplyResult(result, "兑换成功，获得晨星 " + (exchangeAmount * runtime.Catalog.Data.settings.exchangeScale).ToString("N0"));
                CloseDialogKind(DialogKind.Exchange);
            }
            else
            {
                ApplyResult(result);
            }
        }

        // ---------- rule and price table ----------

        private void ShowRule(string key)
        {
            CloseDialogKind(DialogKind.Rule);
            CommerceRule rule = runtime.Catalog.Data.rules?.FirstOrDefault(value => value.key == key);
            if (rule == null) return;
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(RulePrefab, canvasRoot);
            dialog.name = "CommerceRule";
            RegisterDialog(dialog, DialogKind.Rule);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleText", rule.title);
            Transform content = Find(dialog.transform, "BgDialog1/Content");
            if (content == null) return;
            ClearChildren(content);
            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 20, 20);
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddWrappedText(content, rule.title, 30, new Color(0.98f, 0.9f, 0.62f, 1f));
            for (int i = 0; i < (rule.rows?.Length ?? 0); i++)
                AddWrappedText(content, (i + 1) + ". " + rule.rows[i], 24, Color.white);
            AddWrappedText(content, "按 Esc 关闭", 20, new Color(0.72f, 0.72f, 0.72f, 1f));
            OfficialCommercePrefabRenderer.RebuildLayout(dialog.gameObject);
        }

        private void AddWrappedText(Transform parent, string value, float size, Color color)
        {
            var go = new GameObject("RuleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(600, size * 2.2f);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = runtime.ChineseTmpFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = value;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 600;
        }

        private void OpenItemDetail(int itemId, long count)
        {
            CloseDialogKind(DialogKind.ItemDetail);
            CommerceItem item = runtime.Catalog.Data.items.FirstOrDefault(value => value.id == itemId);
            string name = item == null ? "物品 " + itemId : item.name;
            int quality = item == null ? 1 : item.quality;
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = null;
            dialog = renderer.Create("commerce_item_detail", canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                if (path.EndsWith("/CommonBtn_11", StringComparison.Ordinal))
                {
                    button.onClick.AddListener(PlayClick);
                    button.onClick.AddListener(() => CloseDialog(dialog, DialogKind.ItemDetail));
                }
            });
            dialog.name = "CommerceItemDetail";
            RegisterDialog(dialog, DialogKind.ItemDetail);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleText", name);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/NameGo", false);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/TypeText", false);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/QualityText", false);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/CountGo", false);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/CountGo/CountContent/MaxCountText", false);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/NameGo/CommonBtn_OutputAdditionDetail", false);
            SetActive(dialog.transform, "BgDialog1/Content/TopGo/ItemRemainTimeContent", false);
            SetActive(dialog.transform, "BgDialog1/Content/MiddleGo/ItemDetailDialogUseResult", false);
            SetActive(dialog.transform, "BgDialog1/Content/MiddleGo/ItemDetailDialogGridLayout", false);
            SetActive(dialog.transform, "BgDialog1/Content/MiddleGo/ButtonsGo", true);
            SetActive(dialog.transform, "BgDialog1/Content/MiddleGo/ButtonsGo/DecomposeBtn", false);
            SetActive(dialog.transform, "BgDialog1/ItemDetailGmTool", false);
            SetText(dialog.transform, "BgDialog1/Content/MiddleGo/ButtonsGo/CommonBtn_11/Content/Text", "关闭");
            SetText(dialog.transform, "BgDialog1/Content/MiddleGo/ScrollRect/Viewport/Content/DetailContent/DetailText",
                "品质：" + QualityName(quality) + "\n数量：" + count.ToString("N0", CultureInfo.InvariantCulture)
                + "\n\n商城礼包奖励，可在背包中查看。");
            SetText(dialog.transform, "BgDialog1/Content/MiddleGo/ScrollRect/Viewport/Content/DetailContent/FuncDetailText", "");
            Image icon = Find(dialog.transform, "BgDialog1/Content/TopGo/IconSlot/Icon")?.GetComponent<Image>();
            if (icon != null) icon.sprite = runtime.FindItemSprite(itemId);
            Image qualityBg = Find(dialog.transform, "BgDialog1/Content/TopGo/QualityBg")?.GetComponent<Image>();
            if (qualityBg != null) qualityBg.sprite = runtime.FindSprite(QualitySprite(quality));
            RectTransform background = Find(dialog.transform, "BgDialog1") as RectTransform;
            if (background != null)
            {
                background.sizeDelta = new Vector2(842f, 900f);
                background.anchoredPosition = Vector2.zero;
                VerticalLayoutGroup backgroundLayout = background.GetComponent<VerticalLayoutGroup>();
                if (backgroundLayout != null) backgroundLayout.enabled = false;
            }
            RectTransform content = Find(dialog.transform, "BgDialog1/Content") as RectTransform;
            if (content != null)
            {
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = new Vector2(16f, 24f);
                content.offsetMax = new Vector2(-16f, -96f);
                VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
                if (contentLayout != null) contentLayout.enabled = false;
            }
            RectTransform top = Find(dialog.transform, "BgDialog1/Content/TopGo") as RectTransform;
            if (top != null)
            {
                top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f);
                top.pivot = new Vector2(0.5f, 1f);
                top.anchoredPosition = new Vector2(0f, -22f);
                top.sizeDelta = new Vector2(810f, 300f);
                VerticalLayoutGroup topLayout = top.GetComponent<VerticalLayoutGroup>();
                if (topLayout != null) topLayout.enabled = false;
            }
            RectTransform middle = Find(dialog.transform, "BgDialog1/Content/MiddleGo") as RectTransform;
            if (middle != null)
            {
                middle.anchorMin = middle.anchorMax = new Vector2(0.5f, 0f);
                middle.pivot = new Vector2(0.5f, 0f);
                middle.anchoredPosition = new Vector2(0f, 24f);
                middle.sizeDelta = new Vector2(810f, 540f);
                VerticalLayoutGroup middleLayout = middle.GetComponent<VerticalLayoutGroup>();
                if (middleLayout != null) middleLayout.enabled = false;
            }
            SetActive(dialog.transform, "BgDialog1/Horn", false);
            foreach (Transform child in dialog.GetComponentsInChildren<Transform>(true))
                if (child.name.IndexOf("Horn", StringComparison.Ordinal) >= 0) child.gameObject.SetActive(false);
            SetActive(dialog.transform, "BgDialog1/BgImg/Image", false);
            OfficialCommercePrefabRenderer.RebuildLayout(dialog.gameObject);
        }

        private static string QualityName(int quality)
        {
            switch (quality)
            {
                case 3: return "稀有";
                case 4: return "史诗";
                case 5: return "传说";
                case 6: return "神话";
                default: return "普通";
            }
        }

        private void OpenPriceTable()
        {
            ShowPriceTable(directPayTab ? "PayVoucherRule" : "AccumulatedPayRule");
        }

        private void ShowPriceTable(string ruleKey)
        {
            CloseDialogKind(DialogKind.PriceTable);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(PriceTablePrefab, canvasRoot);
            dialog.name = "CommercePriceTable";
            RegisterDialog(dialog, DialogKind.PriceTable);
            CommerceRule rule = runtime.Catalog.Data.rules?.FirstOrDefault(value => value.key == ruleKey);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleText", rule?.title ?? "价格表");
            Transform content = Find(dialog.transform, "BgDialog1/Content/RuleScrollRectItem/Viewport/ContentGo");
            if (content == null) return;
            ClearChildren(content);
            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddPriceRow(content, "代金券", ruleKey == "AccumulatedPayRule" ? "累计星值" : "价格", 26,
                new Color(0.98f, 0.9f, 0.62f, 1f));
            foreach (CommerceMoneyCost cost in runtime.Catalog.Data.moneyCosts.OrderBy(value => value.startValue)
                .ThenBy(value => value.id))
            {
                string left = ruleKey == "AccumulatedPayRule"
                    ? cost.startValue.ToString("0.##", CultureInfo.InvariantCulture)
                    : cost.voucherCost.ToString("N0");
                string right = ruleKey == "AccumulatedPayRule"
                    ? cost.startValue.ToString("0.##", CultureInfo.InvariantCulture)
                    : "¥" + cost.startValue.ToString("0.##", CultureInfo.InvariantCulture);
                AddPriceRow(content, left, right, 20, Color.white);
            }
            AddWrappedText(content, "按 Esc 关闭", 20, new Color(0.72f, 0.72f, 0.72f, 1f));
            OfficialCommercePrefabRenderer.RebuildLayout(dialog.gameObject);
        }

        private void AddPriceRow(Transform content, string left, string right, float size, Color color)
        {
            var go = new GameObject("PriceRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(content, false);
            rect.sizeDelta = new Vector2(600, 52);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 8;
            AddRowText(go.transform, left, size, color);
            AddRowText(go.transform, right, size, color);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 52;
        }

        private void AddRowText(Transform parent, string value, float size, Color color)
        {
            var go = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(280, 48);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = runtime.ChineseTmpFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = value;
        }

        // ---------- generic confirmation ----------

        private void OpenSimpleConfirm(string message, string confirmLabel, Action confirm)
        {
            CloseDialogKind(DialogKind.Confirm);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject dialog = renderer.Create(ConfirmPrefab, canvasRoot, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                button.onClick.AddListener(PlayClick);
                if (path.EndsWith("/MoneyPayBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(() =>
                    {
                        CloseTopDialog();
                        confirm?.Invoke();
                    });
            });
            dialog.name = "CommerceConfirm";
            RegisterDialog(dialog, DialogKind.Confirm);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleText", "确认");
            SetText(dialog.transform, "BgDialog1/Content/PayBtnGo/CommonPayBtn/MoneyPayBtn/Content/Text", confirmLabel);
            SetActive(dialog.transform, "BgDialog1/Content/PayBtnGo/PayBtn", false);
            SetActive(dialog.transform, "BgDialog1/Content/PayBtnGo/CommonPayBtn", true);
            Transform awards = Find(dialog.transform, "BgDialog1/Content/ItemContent/ItemContent");
            if (awards != null)
            {
                ClearChildren(awards);
                AddWrappedText(awards, message, 24, Color.white);
            }
            OfficialCommercePrefabRenderer.RebuildLayout(dialog.gameObject);
        }

        private GameObject CreateTextButton(Transform parent, string label, Vector2 size)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;
            Image image = go.GetComponent<Image>();
            image.sprite = runtime.FindSprite("BtnMax");
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 4);
            textRect.offsetMax = new Vector2(-6, -4);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = runtime.ChineseTmpFont;
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = label;
            return go;
        }
    }
}




