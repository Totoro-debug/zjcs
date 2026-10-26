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
            AnimationClip show = runtime.AccumulatedPayPanelShow;
            if (show != null)
            {
                Animation animation = dialog.GetComponent<Animation>() ?? dialog.AddComponent<Animation>();
                animation.AddClip(show, show.name);
                animation.Play(show.name, PlayMode.StopAll);
            }
        }

        private void PopulateAccumulatedItems(Transform dialog)
        {
            Transform content = Find(dialog, "BgPanel/Content/AwardScroll/Viewport/Content");
            if (content == null)
            {
                Transform viewport = Find(dialog, "BgPanel/Content/AwardScroll/Viewport");
                if (viewport == null) return;
                GameObject contentObject = new GameObject("Content", typeof(RectTransform),
                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                RectTransform contentRect = contentObject.GetComponent<RectTransform>();
                contentRect.SetParent(viewport, false);
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = Vector2.one;
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.sizeDelta = new Vector2(0f, 0f);
                VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.spacing = 0f;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                content = contentObject.transform;
                ScrollRect scrollRect = Find(dialog, "BgPanel/Content/AwardScroll")?.GetComponent<ScrollRect>();
                if (scrollRect != null) scrollRect.content = contentRect;
            }
            ClearChildren(content);
            int group = runtime.Offers.CurrentAccumulatedGroup();
            if (group <= 0) return;
            CommerceAccumulatedAward[] awards = runtime.Catalog.Data.accumulatedAwards
                .Where(value => value.group == group).OrderBy(value => value.level).ToArray();
            foreach (CommerceAccumulatedAward award in awards)
            {
                GameObject row = CreateAccumulatedRow(content, award);
                row.name = "Accumulated_" + award.level;
            }
            CommerceAccumulatedAward last = runtime.Catalog.Data.accumulatedAwards
                .OrderBy(value => value.level).LastOrDefault();
            if (last != null && group == last.group)
                CreateAccumulatedLockedEntry(content, last.level + 1, last.threshold);
            OfficialCommercePrefabRenderer.RebuildLayout(content.gameObject);
            ScrollRect scroll = Find(dialog, "BgPanel/Content/AwardScroll")?.GetComponent<ScrollRect>();
            if (scroll != null && awards.Length > 1)
            {
                int target = Mathf.Clamp(runtime.Offers.FirstUnclaimedAccumulatedLevel() - awards[0].level,
                    0, awards.Length - 1);
                scroll.verticalNormalizedPosition = 1f - target / (float)(awards.Length - 1);
            }
        }

        private void CreateAccumulatedGroupHeader(Transform content, int group)
        {
            var headerObject = new GameObject("AccumulatedGroup_" + group,
                typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform rect = headerObject.GetComponent<RectTransform>();
            rect.SetParent(content, false);
            rect.sizeDelta = new Vector2(940f, 44f);
            LayoutElement layout = headerObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 940f;
            layout.preferredHeight = 44f;
            TextMeshProUGUI text = headerObject.GetComponent<TextMeshProUGUI>();
            text.font = runtime.ChineseTmpFont;
            text.fontSize = 24f;
            text.color = new Color(0.95f, 0.85f, 0.45f, 1f);
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            text.text = "星眷第 " + group + " 组";
        }

        private GameObject CreateAccumulatedRow(Transform content, CommerceAccumulatedAward award)
        {
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            GameObject row = renderer.Create(AccumulatedItemPrefab, content);
            bool claimed = runtime.Offers.HasClaimedAccumulated(award.level);
            bool canClaim = runtime.Offers.CanClaimAccumulated(award.level);
            bool active = runtime.Session.State.AccumulatedValue >= award.threshold;
            long current = (long)Math.Min(runtime.Session.State.AccumulatedValue, award.threshold);
            string progressFormat = active && !claimed ? award.progressActiveFormat : award.progressFormat;
            SetText(row.transform, "AwardContent/Content/StateGo/ProgressGo/ProgressTxt",
                FormatAccumulatedText(progressFormat, current, (long)award.threshold));
            SetText(row.transform, "AwardContent/Content/StateGo/StateGo/ReceiveTxt",
                claimed ? "已领取" : canClaim ? "可领取" : "未达成");
            long remaining = (long)Math.Max(0d,
                award.threshold - runtime.Session.State.AccumulatedValue);
            SetText(row.transform, "AwardContent/Content/StateGo/StateGo/LevelRequireTxt",
                active ? string.Empty : FormatAccumulatedText(award.requireFormat, remaining));
            SetText(row.transform, "AwardContent/Content/Txt", award.description);
            SetActive(row.transform, "AwardContent/Content/StateGo/StateGo/AwardAvailableImg", canClaim);
            SetActive(row.transform, "AwardContent/Content/StateGo/StateGo", !canClaim);
            SetActive(row.transform, "AwardContent/TakenAwardMask", claimed);
            SetActive(row.transform, "AwardContent/Content/TakeAwardBtn", canClaim);
            CanvasGroup canvasGroup = row.GetComponent<CanvasGroup>() ?? row.AddComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = claimed ? 0.6f : 1f;
                canvasGroup.interactable = !claimed;
                canvasGroup.blocksRaycasts = !claimed;
            }
            ConfigureAccumulatedLines(row.transform, award);
            ConfigureAccumulatedCoreAward(row.transform, award, canClaim);
            Transform subAwards = Find(row.transform, "AwardContent/Content/OtherAwards");
            if (subAwards != null)
                foreach (CommerceReward reward in award.subRewards ?? Array.Empty<CommerceReward>())
                    CreateItemIcon(subAwards, reward.itemId, reward.count, false, 96f);
            Button button = Find(row.transform, "AwardContent/Content/TakeAwardBtn/Image")?.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(PlayClick);
                int level = award.level;
                button.onClick.AddListener(() => ApplyResult(runtime.ClaimAccumulated(level)));
            }
            return row;
        }

        private void ConfigureAccumulatedLines(Transform row, CommerceAccumulatedAward award)
        {
            if (award.level <= 1) return;
            bool lineRight = (award.level & 1) == 0;
            CommerceAccumulatedAward previous = runtime.Catalog.Data.accumulatedAwards
                .FirstOrDefault(value => value.level == award.level - 1);
            float previousThreshold = previous == null ? 0f : previous.threshold;
            float progress = award.threshold <= previousThreshold ? 1f : Mathf.Clamp01((float)
                ((runtime.Session.State.AccumulatedValue - previousThreshold) / (award.threshold - previousThreshold)));
            string activePath = lineRight ? "AwardContent/DownRightLineGo/ActiveDownRighttLineImg"
                : "AwardContent/DownLeftLineGo/ActiveDownLeftLineImg";
            string inactivePath = lineRight ? "AwardContent/DownRightLineGo/InactiveDownRighttLineImg"
                : "AwardContent/DownLeftLineGo/InactiveDownLeftLineImg";
            SetActive(row, activePath, progress > 0f);
            SetActive(row, inactivePath, progress < 1f);
            Transform lineRoot = Find(row, lineRight
                ? "AwardContent/DownRightLineGo" : "AwardContent/DownLeftLineGo");
            RectTransform active = Find(row, activePath) as RectTransform;
            RectTransform inactive = Find(row, inactivePath) as RectTransform;
            if (lineRoot is RectTransform parent && active != null && inactive != null)
            {
                float width = parent.rect.width;
                active.sizeDelta = new Vector2(progress * width, active.sizeDelta.y);
                inactive.sizeDelta = new Vector2((1f - progress) * width, inactive.sizeDelta.y);
            }
        }

        private static string FormatAccumulatedText(string format, params long[] values)
        {
            return string.IsNullOrEmpty(format) ? string.Join("/", values)
                : string.Format(CultureInfo.InvariantCulture, format, values.Cast<object>().ToArray());
        }

        private void ConfigureAccumulatedCoreAward(Transform row, CommerceAccumulatedAward award, bool canClaim)
        {
            Transform slot = Find(row, "AwardContent/Content/CoreAwardGo/CoreAwardSlot");
            CommerceReward[] rewards = award.coreRewards ?? award.rewards ?? Array.Empty<CommerceReward>();
            if (slot == null || rewards.Length == 0) return;
            string prefab = rewards.Length > 1 ? "commerce_core_multi" : "commerce_core";
            GameObject core = runtime.CreatePrefabRenderer().Create(prefab, slot);
            int qualityLevel = rewards.Select(reward => runtime.Catalog.Data.items
                    .FirstOrDefault(value => value.id == reward.itemId)?.quality ?? 1)
                .DefaultIfEmpty(1).Max();
            CommerceQualityFrame qualityFrame = runtime.Catalog.Data.accumulatedQualityFrames?
                .FirstOrDefault(value => value.quality == qualityLevel);
            Image quality = Find(core.transform, "QualityImg")?.GetComponent<Image>();
            string qualitySprite = qualityFrame == null ? QualitySprite(qualityLevel)
                : award.isBig ? qualityFrame.big : qualityFrame.small;
            if (quality != null) quality.sprite = runtime.FindSprite(qualitySprite);
            SetActive(core.transform, "GlowBig", award.isBig);
            SetActive(core.transform, "GlowSmall", !award.isBig);
            Transform imageSlot = Find(core.transform, "ItemImg");
            Action<int> showReward = index =>
            {
                if (imageSlot == null) return;
                ClearChildren(imageSlot);
                CommerceReward reward = rewards[index % rewards.Length];
                CreateItemIcon(imageSlot, reward.itemId, reward.count, false, rewards.Length > 1 ? 92f : 148f);
            };
            showReward(0);
            if (rewards.Length > 1)
            {
                CommerceUiAnimationEventReceiver receiver = core.GetComponent<CommerceUiAnimationEventReceiver>();
                if (receiver != null)
                {
                    int current = 0;
                    receiver.OnEvent = eventName =>
                    {
                        if (eventName != "SwitchItem") return;
                        current = (current + 1) % rewards.Length;
                        showReward(current);
                    };
                }
                Animation animation = core.GetComponent<Animation>();
                if (animation != null && animation.GetClip("AccumulatedPayCoreMulltiAwardGo_Loop") != null)
                    animation.Play("AccumulatedPayCoreMulltiAwardGo_Loop", PlayMode.StopAll);
            }
            SetActive(core.transform, "QualityImg/Receive", canClaim);
            SetActive(row, "AwardContent/Content/CoreAwardGo/RedDot1", canClaim);
            SetActive(row, "AwardContent/Content/CoreAwardGo/LockedGo", false);
        }

        private void CreateAccumulatedLockedEntry(Transform content, int level, float threshold)
        {
            CommerceAccumulatedAward last = runtime.Catalog.Data.accumulatedAwards
                .FirstOrDefault(value => value.level == level - 1);
            if (last == null) return;
            GameObject go = runtime.CreatePrefabRenderer().Create(AccumulatedItemPrefab, content);
            go.name = "AccumulatedLocked_" + level;
            SetActive(go.transform, "AwardContent/Content/StateGo", true);
            SetActive(go.transform, "AwardContent/Content/StateGo/ProgressGo", true);
            SetActive(go.transform, "AwardContent/Content/StateGo/StateGo", false);
            SetActive(go.transform, "AwardContent/Content/TakeAwardBtn", false);
            SetActive(go.transform, "AwardContent/Content/CoreAwardGo/LockedGo", true);
            Transform locked = Find(go.transform, "AwardContent/Content/CoreAwardGo/LockedGo");
            Image lockedImage = locked == null ? null
                : locked.GetComponent<Image>() ?? locked.GetComponentInChildren<Image>(true);
            if (lockedImage != null)
                lockedImage.sprite = runtime.FindSprite(runtime.Catalog.Data.accumulatedLockedSprite);
            SetActive(go.transform, "AwardContent/Content/CoreAwardGo/RedDot1", false);
            SetActive(go.transform, "AwardContent/Content/CoreAwardGo/CoreAwardSlot", false);
            SetActive(go.transform, "AwardContent/TakenAwardMask", false);
            SetActive(go.transform, "AwardContent/Content/Txt", false);
            SetText(go.transform, "AwardContent/Content/StateGo/ProgressGo/ProgressTxt",
                FormatAccumulatedText(last.progressLockedFormat, (long)runtime.Session.State.AccumulatedValue));
        }

        // ---------- exchange ----------

        private void OpenExchange()
        {
            CloseDialogKind(DialogKind.Exchange);
            OfficialCommercePrefabRenderer renderer = runtime.CreatePrefabRenderer();
            void BindExchangeButton(string path, GameObject node)
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
            }
            GameObject dialog;
            if (worldExchangeOnly && worldExchangeFactory != null)
            {
                dialog = worldExchangeFactory(canvasRoot);
                foreach (Button button in dialog.GetComponentsInChildren<Button>(true))
                    BindExchangeButton("/" + button.name, button.gameObject);
            }
            else dialog = renderer.Create(ExchangePrefab, canvasRoot, BindExchangeButton);
            dialog.name = "CommerceExchange";
            RegisterDialog(dialog, DialogKind.Exchange);
            SetText(dialog.transform, "BgDialog1/BgImg/BgTitle/TitleTxt", "兑换耀星");
            SetText(dialog.transform, "BgDialog1/Content/TipExchangeTxt", "耀星可兑换为等量晨星，兑换后不可撤销。");
            if (!worldExchangeOnly || worldExchangeFactory == null) PopulateExchangeItems(dialog.transform);
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
            exchangeAmount = fixedExchangeCount > 0 ? fixedExchangeCount
                : held <= 0 ? 0 : Math.Max(runtime.Catalog.Data.settings.exchangeMin, Math.Min(amount, max));
            long back = exchangeAmount * runtime.Catalog.Data.settings.exchangeScale;
            Transform dialog = currentDialogObject == null ? null : currentDialogObject.transform;
            if (dialog == null) return;
            RefreshFixedExchange(dialog);
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
                    OpenDirectPayFromExchange();
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
            bool accumulatedTable = ruleKey == "AccumulatedPayRule";
            string firstColumn = Localized(accumulatedTable
                ? "ui_accumulated_pay_table_column1" : "ui_voucher_table_column1", "充值金额");
            string secondColumn = Localized(accumulatedTable
                ? "ui_accumulated_pay_table_column2" : "ui_voucher_table_column2", "星眷值");
            AddPriceRow(content, firstColumn, secondColumn);
            CommerceMoneyCost[] priceOrder = runtime.Catalog.Data.moneyCosts
                .Where(value => value.isShow)
                .GroupBy(value => value.currencyCost)
                .Select(group => group.OrderBy(value => value.id).First())
                .OrderBy(value => value.id)
                .ToArray();
            foreach (CommerceMoneyCost cost in priceOrder)
            {
                string left = accumulatedTable
                    ? "¥" + cost.currencyCost.ToString("0.##", CultureInfo.InvariantCulture)
                    : cost.voucherCost.ToString("N0", CultureInfo.InvariantCulture);
                string right = accumulatedTable
                    ? cost.startValue.ToString("0.##", CultureInfo.InvariantCulture)
                    : "¥" + cost.currencyCost.ToString("0.##", CultureInfo.InvariantCulture);
                AddPriceRow(content, left, right);
            }
            AddWrappedText(content, "按 Esc 关闭", 20, new Color(0.72f, 0.72f, 0.72f, 1f));
            OfficialCommercePrefabRenderer.RebuildLayout(dialog.gameObject);
        }

        private void AddPriceRow(Transform content, string left, string right)
        {
            GameObject row = runtime.CreatePrefabRenderer().Create(PriceRowPrefab, content);
            SetText(row.transform, "VoucherText", left);
            SetText(row.transform, "PriceText", right);
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




