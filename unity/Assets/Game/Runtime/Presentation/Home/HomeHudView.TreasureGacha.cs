using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private const int TreasureQuickBuyItemId = 301;
        private const int TreasureQuickBuyCurrencyPerItem = 8;
        private const string GachaMain = "/ChestGachaPanel/BgPanel1/Content/MiddleGo/";
        private const string GachaAchievementGrid = "/ChestGachaPanel/BgPanel1/Content/AchievementGo/ChestGachaAchievementPanel/BgPanel1/Content/MiddleGo/ScrollRect/ViewPort/Grid";
        private const string GachaMapsContent = "/ChestMapSwitchPanel/BgDialog1/Content/ScrollRect/Viewport/Content";
        private const string GachaHistoryContent = "/GachaHistoryContent/GoItems/HistoryItemSlot";
        private const string GachaScoreContent = "/ScoreRewardDialog/BgDialog1/Content/RewardItemContent/Scroll View/Viewport/RewardItemSlot";
        private const string GachaDetailPreview = "/GachaDetailDialog/BgDialog1/Content/RewardPreviewScroll";
        private const string GachaDetailHistory = "/GachaDetailDialog/BgDialog1/Content/RewardHistoryScroll";
        private const string GachaDetailRewardRows = "/GachaDetailDialog/BgDialog1/Content/RewardPreviewScroll/RewardPreviewViewport/RewardPreviewGo/AwardRateRuleItem";
        private const string GachaDetailRateTemplate = GachaDetailRewardRows + "/RuleRateInfoItemGacha";

        private HomeUiRenderer treasureGachaMainRenderer;
        private TreasureGachaPerformancePlayer treasureGachaPerformance;
        private HomeUiRenderer treasureGachaOverlayRenderer;
        private GameObject treasureGachaOverlay;
        private Action treasureGachaOverlayClosing;
        private bool treasureGachaShowing;
        private int treasureGachaSourceItemId;
        private Action<int> treasureGachaReturnToSource;
        private int treasureGachaTab;
        private int treasureGachaType = 4;
        private int treasureGachaSeriesId;
        private int treasureGachaPendingElementType = 8;
        private int treasureGachaAchievementGroup = 11;
        private int treasureGachaHistoryPage;
        private List<OfflineTreasureDrawRecord> treasureGachaHistoryRows = new List<OfflineTreasureDrawRecord>();
        private float treasureGachaPreviewRefreshAt;
        private string treasureGachaPreviewTemplate;

        public void ShowTreasureGacha()
        {
            ShowTreasureGachaCore(0, 0);
        }

        public int TreasureGachaSourceItemId => treasureGachaSourceItemId;

        public void ShowTreasureGachaFromItemSource(int sourceItemId, int selectedSeriesId,
            int elementGachaType, Action<int> returnToSource)
        {
            if (sourceItemId <= 0 || selectedSeriesId < 0
                || elementGachaType != 0 && (elementGachaType < 8 || elementGachaType > 12)) return;
            if (activeDialog != null) CloseDialog();
            treasureGachaSourceItemId = sourceItemId;
            treasureGachaReturnToSource = returnToSource;
            ShowTreasureGachaCore(selectedSeriesId, elementGachaType);
        }

        private void ShowTreasureGachaCore(int selectedSeriesId, int elementGachaType)
        {
            treasureGachaTab = elementGachaType >= 8 ? 1 : 0;
            treasureGachaPreviewTemplate = null;
            treasureGachaPreviewRefreshAt = 0f;
            OfflineTreasureGachaProgress state = host.TreasureGacha.State;
            treasureGachaType = elementGachaType >= 8 ? elementGachaType : state.SelectedPreciousGachaType;
            treasureGachaSeriesId = selectedSeriesId > 0 ? selectedSeriesId : state.SelectedSeriesId;
            OpenDialog("treasure_gacha_main", (renderer, panel) =>
            {
                treasureGachaShowing = true;
                treasureGachaMainRenderer = renderer;
                InitializeTreasureGacha(renderer);
            });
        }

        partial void OnDialogClosing()
        {
            astrologicalClosing = true;
            host.EndAstrologicalCharge();
            ClearAstrologicalRows();
            astrologicalClosing = false;
            ClearPetIntroduction();
            ClearFruitTreeTutorial();
            CloseTreasureGachaOverlay();
            treasureGachaShowing = false;
            treasureGachaPerformance = null;
            treasureGachaMainRenderer = null;
            treasureGachaPreviewTemplate = null;
        }

        partial void OnDialogClosed()
        {
            Action<int> returnToSource = treasureGachaReturnToSource;
            int sourceItemId = treasureGachaSourceItemId;
            treasureGachaReturnToSource = null;
            treasureGachaSourceItemId = 0;
            returnToSource?.Invoke(sourceItemId);
        }

        private void InitializeTreasureGacha(HomeUiRenderer renderer)
        {
            long now = TreasureGachaNow();
            host.TreasureGacha.ObserveLocalDay(now,
                "treasure-gacha:observe:" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            TreasureGachaConfig selectedGacha = host.TreasureGachaCatalog.Gacha(treasureGachaType);
            GameObject panelRoot = renderer.Find("/ChestGachaPanel");
            if (selectedGacha == null || panelRoot == null)
                throw new InvalidOperationException("The official treasure gacha machine panel is incomplete.");
            treasureGachaPerformance = panelRoot.AddComponent<TreasureGachaPerformancePlayer>();
            treasureGachaPerformance.Initialize(renderer, host.TreasureGachaCatalog,
                treasureGachaType, selectedGacha.ChestId);

            BindReplace(renderer, "/ChestGachaPanel/BgPanel1/Content/BottomGo/CloseBtn", CloseDialog);
            BindReplace(renderer, GachaMain + "PreciousChestPanel/MapTitleGo/BgImg/SwitchBtnView", ShowTreasureGachaMaps);
            BindReplace(renderer, GachaMain + "PreciousChestPanel/CollectBookBtn", ShowTreasureGachaCollection);
            BindReplace(renderer, GachaMain + "PreciousChestPanel/ScoreRewardUI/ImageBg/ImageBg1/GetRewardBtn", ShowTreasureGachaScore);
            BindReplace(renderer, GachaMain + "PreciousChestPanel/ScoreRewardUI/ScoreInfoBtn", ShowTreasureGachaScore);
            BindReplace(renderer, GachaMain + "PreciousChestPanel/ProbabilityGo/TextTip/DetailBtn", ShowTreasureGachaDetail);
            BindReplace(renderer, GachaMain + "ElementChestPanel/ProbabilityGo/TextTip/DetailBtn", ShowTreasureGachaDetail);
            BindReplace(renderer, GachaMain + "ElementChestPanel/SwitchBtn", ShowElementSelection);

            BindReplace(renderer, "/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/ChestGachaPanelPreciousChestTabBottomBtn",
                () => SelectTreasureGachaTab(0));
            BindReplace(renderer, "/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/ChestGachaPanelElementChestTabBottomBtn",
                () => SelectTreasureGachaTab(1));
            BindReplace(renderer, "/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/AchievementTabBottomBtn",
                () => SelectTreasureGachaTab(2));
            BindReplace(renderer, "/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/ShopTabItem",
                () => SelectTreasureGachaTab(3));

            BindReplace(renderer, GachaMain + "PreciousChestPanel/ChestGo/BlueCard", () => SelectPreciousType(4));
            BindReplace(renderer, GachaMain + "PreciousChestPanel/ChestGo/PurpleCard", () => SelectPreciousType(5));
            BindReplace(renderer, GachaMain + "PreciousChestPanel/ChestGo/OrangeCard", () => SelectPreciousType(6));
            BindReplace(renderer, GachaMain + "PreciousChestPanel/ChestGo/GoldCard", () => SelectPreciousType(7));
            BindReplace(renderer, GachaMain + "PreciousChestPanel/OpenBtnGo/OpenBtn/CommonBtn_CostItem",
                () => DrawTreasureGacha(1));
            BindReplace(renderer, GachaMain + "PreciousChestPanel/OpenBtnGo/RepeatOpenBtn/CommonBtn_CostItem",
                () => DrawTreasureGacha(CurrentRepeatCount()));
            BindReplace(renderer, GachaMain + "PreciousChestPanel/OpenBtnGo/HundredOpenBtn/CommonBtn_CostItem",
                () => DrawTreasureGacha(100));
            BindReplace(renderer, GachaMain + "ElementChestPanel/BtnContent/OneBtn/CommonBtn_CostItem",
                () => DrawTreasureGacha(1));
            BindReplace(renderer, GachaMain + "ElementChestPanel/BtnContent/TenBtn/CommonBtn_CostItem",
                () => DrawTreasureGacha(CurrentRepeatCount()));
            BindReplace(renderer, GachaMain + "ElementChestPanel/BtnContent/HundredOpenBtn/CommonBtn_CostItem",
                () => DrawTreasureGacha(100));
            BindReplace(renderer, GachaMain + "ElementChestPanel/SelectedPanel/BtnSlot/CommonBtn_11",
                () => renderer.SetActive(GachaMain + "ElementChestPanel/SelectedPanel", false));
            BindReplace(renderer, GachaMain + "ElementChestPanel/SelectedPanel/BtnSlot/CommonBtn_12", ConfirmElementSelection);

            BindElementToggle(renderer, "ElementChestFire", 8);
            BindElementToggle(renderer, "ElementChestWater", 9);
            BindElementToggle(renderer, "ElementChestWind", 10);
            BindElementToggle(renderer, "ElementChestLight", 11);
            BindElementToggle(renderer, "ElementChestDark", 12);

            string achievementTabs = "/ChestGachaPanel/BgPanel1/Content/AchievementGo/ChestGachaAchievementPanel/BgPanel1/Content/BottomGo/TabGroup_14/TabGroup/";
            BindReplace(renderer, achievementTabs + "AchievementTabGroupItem_14", () => SetTreasureAchievementGroup(11));
            BindReplace(renderer, achievementTabs + "AchievementTabGroupItem_14 (1)", () => SetTreasureAchievementGroup(13));
            BindReplace(renderer, achievementTabs + "AchievementTabGroupItem_14 (2)", () => SetTreasureAchievementGroup(0));

            BindAllNamed(renderer, "CloseBtn", CloseDialog);
            RefreshTreasureGachaMain();
            renderer.PlayAnimation("ChestGachaPanel_Show", () =>
                renderer.PlayAnimation("ChestGachaPanel_Loop", null, true));
        }

        private void SelectTreasureGachaTab(int tab)
        {
            treasureGachaTab = tab;
            if (tab == 2) RefreshTreasureGachaAchievements();
            RefreshTreasureGachaMain();
        }

        private void SelectPreciousType(int type)
        {
            treasureGachaType = Mathf.Clamp(type, 4, 7);
            TreasureGachaResult result = host.TreasureGacha.SelectPreciousType(treasureGachaType,
                TreasureGachaReceipt("select-precious"));
            if (!TreasureGachaSucceeded(result)) return;
            RefreshTreasureGachaMain();
            PlayTreasureGachaSwitch();
        }

        private void BindElementToggle(HomeUiRenderer renderer, string name, int gachaType)
        {
            GameObject control = renderer.AllNodes.FirstOrDefault(node => node.name == name);
            if (control == null) return;
            Toggle toggle = control.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.onValueChanged.AddListener(value => { if (value) treasureGachaPendingElementType = gachaType; });
                return;
            }
            Button button = control.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => treasureGachaPendingElementType = gachaType);
        }

        private void ConfirmElementSelection()
        {
            treasureGachaType = treasureGachaPendingElementType;
            TreasureGachaResult result = host.TreasureGacha.SelectElementType(treasureGachaType,
                TreasureGachaReceipt("select-element"));
            if (!TreasureGachaSucceeded(result)) return;
            treasureGachaMainRenderer?.SetActive(GachaMain + "ElementChestPanel/SelectedPanel", false);
            RefreshTreasureGachaMain();
            PlayTreasureGachaSwitch();
        }

        private void ShowElementSelection()
        {
            treasureGachaPendingElementType = host.TreasureGacha.State.SelectedElementGachaType;
            treasureGachaMainRenderer.SetActive(GachaMain + "ElementChestPanel/SelectedPanel", true);
            string[] names = { "ElementChestFire", "ElementChestWater", "ElementChestWind", "ElementChestLight", "ElementChestDark" };
            int[] types = { 8, 9, 10, 11, 12 };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject node = treasureGachaMainRenderer.AllNodes.FirstOrDefault(value => value.name == names[i]);
                Toggle toggle = node == null ? null : node.GetComponent<Toggle>();
                if (toggle != null) toggle.SetIsOnWithoutNotify(types[i] == treasureGachaPendingElementType);
            }
        }

        private void RefreshTreasureGachaMain()
        {
            HomeUiRenderer renderer = treasureGachaMainRenderer;
            if (renderer == null) return;
            int selectedType = treasureGachaTab == 1
                ? host.TreasureGacha.State.SelectedElementGachaType
                : host.TreasureGacha.State.SelectedPreciousGachaType;
            if (treasureGachaTab == 1) treasureGachaType = selectedType;
            else if (treasureGachaTab == 0) treasureGachaType = selectedType;

            renderer.SetActive(GachaMain + "PreciousChestPanel", treasureGachaTab == 0);
            renderer.SetActive(GachaMain + "ElementChestPanel", treasureGachaTab == 1);
            renderer.SetActive("/ChestGachaPanel/BgPanel1/Content/AchievementGo", treasureGachaTab == 2);
            renderer.SetActive(GachaMain + "ShopContent", treasureGachaTab == 3);
            renderer.SetActive("/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/ChestGachaPanelPreciousChestTabBottomBtn/Selected",
                treasureGachaTab == 0);
            renderer.SetActive("/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/ChestGachaPanelElementChestTabBottomBtn/Selected",
                treasureGachaTab == 1);
            renderer.SetActive("/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/AchievementTabBottomBtn/Selected",
                treasureGachaTab == 2);
            renderer.SetActive("/ChestGachaPanel/BgPanel1/Content/BottomGo/TabGroup/TabGroup/ShopTabItem/Selected",
                treasureGachaTab == 3);
            renderer.SetActive(GachaMain + "ElementChestPanel/SelectedPanel", false);

            if (treasureGachaTab == 3)
            {
                return;
            }

            TreasureGachaConfig gacha = host.TreasureGachaCatalog.Gacha(treasureGachaType);
            TreasureChestConfig chest = gacha == null ? null : host.TreasureGachaCatalog.Chest(gacha.ChestId);
            if (gacha == null || chest == null) return;
            treasureGachaPerformance?.Configure(chest.ClassId, treasureGachaType);
            long now = TreasureGachaNow();
            IReadOnlyList<TreasureGachaSeriesEntry> series = host.TreasureGacha.SeriesEntries(now);
            RefreshTreasureGachaPreview(renderer, series, now);
            if (treasureGachaType <= 7)
            {
                TreasureGachaSeriesEntry selected = series.LastOrDefault(value => value.IsOpen && value.IsNew)
                    ?? series.FirstOrDefault(value => value.SeriesId == treasureGachaSeriesId && value.IsOpen)
                    ?? series.LastOrDefault(value => value.IsOpen);
                if (selected != null)
                {
                    treasureGachaSeriesId = selected.SeriesId;
                    renderer.SetText(GachaMain + "PreciousChestPanel/MapTitleGo/BgImg/MapNameText", selected.MainTitle);
                    renderer.SetText(GachaMain + "PreciousChestPanel/MapTitleGo/BgImg/CollectionText",
                        "收藏：" + selected.CollectionOwned + "/" + selected.CollectionTotal);
                }
            }

            int costItemId = chest.CostItems == null || chest.CostItems.Length == 0 ? gacha.CostItemId : chest.CostItems[0].ItemId;
            long costPerDraw = chest.CostItems == null || chest.CostItems.Length == 0 ? 10 : chest.CostItems[0].Count;
            long available = costPerDraw <= 0 ? 0 : host.ItemCount(costItemId) / costPerDraw;
            int repeat = Mathf.Clamp((int)Math.Min(available, 10), 2, 10);
            bool canRepeat = available >= 2 && chest.OpenCounts.Contains(repeat);
            bool canHundred = available >= 100 && chest.OpenCounts.Contains(100);
            renderer.SetActive(GachaMain + "PreciousChestPanel/OpenBtnGo/RepeatOpenBtn", canRepeat);
            renderer.SetActive(GachaMain + "PreciousChestPanel/OpenBtnGo/HundredOpenBtn", canHundred);
            renderer.SetActive(GachaMain + "ElementChestPanel/BtnContent/TenBtn", canRepeat);
            renderer.SetActive(GachaMain + "ElementChestPanel/BtnContent/HundredOpenBtn", canHundred);

            SetDrawCost(renderer, GachaMain + "PreciousChestPanel/OpenBtnGo/OpenBtn/CommonBtn_CostItem", costItemId, costPerDraw, 1);
            SetDrawCost(renderer, GachaMain + "PreciousChestPanel/OpenBtnGo/RepeatOpenBtn/CommonBtn_CostItem", costItemId, costPerDraw, repeat);
            SetDrawCost(renderer, GachaMain + "PreciousChestPanel/OpenBtnGo/HundredOpenBtn/CommonBtn_CostItem", costItemId, costPerDraw, 100);
            SetDrawCost(renderer, GachaMain + "ElementChestPanel/BtnContent/OneBtn/CommonBtn_CostItem", costItemId, costPerDraw, 1);
            SetDrawCost(renderer, GachaMain + "ElementChestPanel/BtnContent/TenBtn/CommonBtn_CostItem", costItemId, costPerDraw, repeat);
            SetDrawCost(renderer, GachaMain + "ElementChestPanel/BtnContent/HundredOpenBtn/CommonBtn_CostItem", costItemId, costPerDraw, 100);

            int daily = host.TreasureGacha.DailyCount(DateTime.Now);
            int limit = host.TreasureGachaCatalog.Settings.DailyLimit;
            string dailyText = "今日抽取：" + daily + "/" + limit;
            renderer.SetText(GachaMain + "PreciousChestPanel/DailyCountText/Text", dailyText);
            renderer.SetText(GachaMain + "ElementChestPanel/DailyCountText/Text", dailyText);
            renderer.SetText(GachaMain + "PreciousChestPanel/ScoreRewardUI/ScoreInfoBtn/TextScore",
                host.ItemCount(host.TreasureGachaCatalog.Settings.ScoreItemId).ToString(CultureInfo.InvariantCulture));

            for (int type = 4; type <= 7; type++)
            {
                TreasureGachaConfig cardGacha = host.TreasureGachaCatalog.Gacha(type);
                TreasureChestConfig cardChest = cardGacha == null ? null : host.TreasureGachaCatalog.Chest(cardGacha.ChestId);
                long cardCost = cardChest == null || cardChest.CostItems.Length == 0 ? 0 : cardChest.CostItems[0].Count;
                string cardName = type == 4 ? "BlueCard" : type == 5 ? "PurpleCard" : type == 6 ? "OrangeCard" : "GoldCard";
                renderer.SetText(GachaMain + "PreciousChestPanel/ChestGo/" + cardName + "/Panel (" + (type - 3) + ")/KeyProgress/CountTextGo/CostCountText",
                    host.ItemCount(cardGacha.CostItemId).ToString(CultureInfo.InvariantCulture) + "/" + cardCost);
                renderer.SetActive(GachaMain + "PreciousChestPanel/ChestGo/" + cardName + "/Panel (" + (type - 3) + ")/Selected", type == treasureGachaType);
            }

            int remaining = Enumerable.Range(1, 10)
                .Select(quality => host.TreasureGacha.RemainingGuaranteePulls(treasureGachaType, quality))
                .Where(value => value > 0).DefaultIfEmpty(0).Min();
            string pityText = remaining > 0 ? "距下一档保底还需：" + remaining + " 次" : "保底进度已保存";
            renderer.SetText(GachaMain + "PreciousChestPanel/ProbabilityGo/TextTip", pityText);
            renderer.SetText(GachaMain + "ElementChestPanel/ProbabilityGo/TextTip", pityText);
            if (treasureGachaTab == 0 || treasureGachaTab == 1)
                ApplyTreasureGachaMachineState(renderer);
            if (treasureGachaTab == 2) RefreshTreasureGachaAchievements();
        }

        private void RefreshTreasureGachaPreview(HomeUiRenderer renderer,
            IReadOnlyList<TreasureGachaSeriesEntry> series, long nowUnixSeconds)
        {
            const string path = "/ChestGachaPanel/BgPanel1/Content/TopGo/PreviewBubble";
            TreasureGachaSeriesEntry preview = series.FirstOrDefault(value => value.IsPreview && !value.IsOpen);
            TMP_Text text = renderer.Find<TMP_Text>(path + "/Content/Text");
            if (text != null && treasureGachaPreviewTemplate == null)
                treasureGachaPreviewTemplate = text.text;
            if (preview == null)
            {
                renderer.SetActive(path, false);
                return;
            }

            renderer.SetActive(path, true);
            if (Time.unscaledTime < treasureGachaPreviewRefreshAt) return;
            treasureGachaPreviewRefreshAt = Time.unscaledTime + 0.5f;
            UpdateTreasureGachaPreviewText(renderer, preview, nowUnixSeconds);
        }

        public void TickTreasureGachaPreview()
        {
            if (!treasureGachaShowing || treasureGachaMainRenderer == null
                || Time.unscaledTime < treasureGachaPreviewRefreshAt) return;
            treasureGachaPreviewRefreshAt = Time.unscaledTime + 0.5f;
            long now = TreasureGachaNow();
            IReadOnlyList<TreasureGachaSeriesEntry> entries = host.TreasureGacha.SeriesEntries(now);
            const string path = "/ChestGachaPanel/BgPanel1/Content/TopGo/PreviewBubble";
            TreasureGachaSeriesEntry preview = entries.FirstOrDefault(value => value.IsPreview && !value.IsOpen);
            if (treasureGachaPreviewTemplate == null)
                treasureGachaPreviewTemplate = treasureGachaMainRenderer.Find<TMP_Text>(path + "/Content/Text")?.text;
            treasureGachaMainRenderer.SetActive(path, preview != null);
            if (preview != null) UpdateTreasureGachaPreviewText(treasureGachaMainRenderer, preview, now);
        }

        private void UpdateTreasureGachaPreviewText(HomeUiRenderer renderer,
            TreasureGachaSeriesEntry preview, long nowUnixSeconds)
        {
            if (preview == null || string.IsNullOrEmpty(treasureGachaPreviewTemplate)) return;
            TimeSpan remaining = preview.OpensAt - DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).ToLocalTime().DateTime;
            renderer.SetText("/ChestGachaPanel/BgPanel1/Content/TopGo/PreviewBubble/Content/Text",
                treasureGachaPreviewTemplate.Replace("{0}", FormatTreasureGachaBriefTime(remaining)));
        }

        private string FormatTreasureGachaBriefTime(TimeSpan remaining)
        {
            if (remaining.TotalDays >= 1d)
            {
                int days = (int)remaining.TotalDays;
                return remaining.TotalHours <= 0d
                    ? FormatTreasureGachaTime("DayCountdown", "{0}天", days)
                    : FormatTreasureGachaTime("DayCountdown2", "{0}天{1}小时", days, remaining.Hours);
            }
            if (remaining.TotalHours >= 1d)
                return FormatTreasureGachaTime("time_hour", "{0}小时", (int)remaining.TotalHours);
            if (remaining.TotalMinutes >= 1d)
                return FormatTreasureGachaTime("time_minute", "{0}分钟", (int)remaining.TotalMinutes);
            if (remaining.TotalSeconds > 0d)
                return FormatTreasureGachaTime("time_second", "{0}秒", (int)Math.Ceiling(remaining.TotalSeconds));
            return catalog.LocalizedText("time_end", "已结束");
        }

        private string FormatTreasureGachaTime(string key, string fallback, params object[] args)
        {
            string format = catalog.LocalizedText(key, fallback);
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private void ApplyTreasureGachaMachineState(HomeUiRenderer renderer)
        {
            HomeTreasureGachaMachineConfig machine = host.Catalog.TreasureGachaMachine;
            if (machine == null)
                throw new InvalidOperationException("Official treasure gacha machine switcher data is missing.");

            if (treasureGachaTab == 0)
            {
                TreasureGachaSeries series = host.TreasureGachaCatalog.SeriesInfo(treasureGachaSeriesId);
                int mapClassId = series == null ? 0 : series.MapClassId;
                SetTreasureGachaObjectState(renderer, machine.map_game_object_states, mapClassId);
                SetTreasureGachaSpriteState(renderer, machine.map_sprite_states, mapClassId);
                SetTreasureGachaObjectState(renderer, machine.machine_show_game_object_states, 1);
                return;
            }

            int elementType = host.TreasureGachaCatalog.ElementIdForGachaType(treasureGachaType);
            SetTreasureGachaObjectState(renderer, machine.map_game_object_states, 0);
            SetTreasureGachaSpriteState(renderer, machine.element_sprite_states, elementType);
            SetTreasureGachaObjectState(renderer, machine.machine_show_game_object_states,
                elementType == 0 ? 0 : 1);
        }

        private static void SetTreasureGachaObjectState(HomeUiRenderer renderer,
            HomeStateGameObjectConfig[] states, int state)
        {
            if (states == null) throw new InvalidOperationException("Official treasure gacha object states are missing.");
            var allPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (HomeStateGameObjectConfig entry in states)
                foreach (string path in entry.game_objects ?? Array.Empty<string>()) allPaths.Add(path);
            foreach (string path in allPaths)
                if (!renderer.SetActive(path, false))
                    throw new InvalidOperationException("Official treasure gacha object is missing: " + path);

            HomeStateGameObjectConfig selected = states.FirstOrDefault(entry => entry.state == state);
            if (selected == null) return;
            foreach (string path in selected.game_objects ?? Array.Empty<string>())
                if (!renderer.SetActive(path, true))
                    throw new InvalidOperationException("Official treasure gacha state object is missing: " + path);
        }

        private void SetTreasureGachaSpriteState(HomeUiRenderer renderer,
            HomeStateImageConfig[] images, int state)
        {
            if (images == null) throw new InvalidOperationException("Official treasure gacha background states are missing.");
            foreach (HomeStateImageConfig image in images)
            {
                HomeStateSpriteConfig selected = (image.states ?? Array.Empty<HomeStateSpriteConfig>())
                    .FirstOrDefault(entry => entry.state == state);
                if (selected == null) continue;
                Sprite sprite = host.Catalog.FindSprite(selected.sprite_file);
                if (sprite == null || !renderer.SetSprite(image.path, sprite))
                    throw new InvalidOperationException("Official treasure gacha state sprite is unavailable: " + selected.sprite_file);
            }
        }

        private void SetDrawCost(HomeUiRenderer renderer, string buttonPath, int itemId, long cost, int count)
        {
            renderer.SetText(buttonPath + "/Content/Text", "抽取 x" + count);
            renderer.SetText(buttonPath + "/Content/ItemContent/Text",
                checked(cost * count).ToString(CultureInfo.InvariantCulture));
            Sprite icon = host.ItemIcon(itemId);
            if (icon != null) renderer.SetSprite(buttonPath + "/Content/ItemContent/ItemImg", icon);
        }

        private int CurrentRepeatCount()
        {
            TreasureGachaConfig gacha = host.TreasureGachaCatalog.Gacha(treasureGachaType);
            TreasureChestConfig chest = gacha == null ? null : host.TreasureGachaCatalog.Chest(gacha.ChestId);
            if (chest == null || chest.CostItems == null || chest.CostItems.Length == 0) return 1;
            long available = host.ItemCount(chest.CostItems[0].ItemId) / chest.CostItems[0].Count;
            int count = Mathf.Clamp((int)Math.Min(available, 10), 2, 10);
            return chest.OpenCounts.Contains(count) ? count : chest.OpenCounts.Where(value => value <= count && value > 1).DefaultIfEmpty(1).Max();
        }

        private void DrawTreasureGacha(int count)
        {
            if (count <= 0) return;
            ApplyTreasureGachaDraw(count, false, false, false);
        }

        private void ApplyTreasureGachaDraw(int count, bool confirmHundred, bool confirmQuickBuy, bool suppressReminder)
        {
            TreasureGachaResult result = host.TreasureGacha.Draw(treasureGachaType, count,
                treasureGachaType <= 7 ? treasureGachaSeriesId : 0,
                TreasureGachaReceipt("draw"), TreasureGachaNow(), confirmHundred,
                confirmQuickBuy, suppressReminder);
            if (result.Code == TreasureGachaResultCode.ConfirmationRequired)
            {
                ShowTreasureGachaConfirm("百次抽取确认", "将连续抽取100次并消耗对应材料。",
                    suppressToday =>
                    {
                        if (suppressToday)
                        {
                            TreasureGachaResult reminder = host.TreasureGacha.SetHundredReminder(
                                treasureGachaType, DateTime.Now, true,
                                TreasureGachaReceipt("hundred-reminder"));
                            if (!TreasureGachaSucceeded(reminder))
                            {
                                Notify(string.IsNullOrEmpty(reminder.Message)
                                    ? "免提示设置未保存" : reminder.Message);
                                return;
                            }
                        }
                        ApplyTreasureGachaDraw(count, true, confirmQuickBuy, false);
                    }, true);
                return;
            }
            if (result.Code == TreasureGachaResultCode.InsufficientItems && treasureGachaType == 4
                && !confirmQuickBuy && host.ItemCount(301) < checked(10L * count))
            {
                long missing = checked(10L * count - host.ItemCount(301));
                long currency = checked(missing * 8L);
                ShowTreasureGachaConfirm("快捷购买女神牌",
                    "补足 " + missing + " 张女神牌，消耗晨星 " + currency + "。",
                    _ => ApplyTreasureGachaDraw(count, confirmHundred, true, suppressReminder));
                return;
            }
            if (result.IsApplied || result.Code == TreasureGachaResultCode.Unchanged)
            {
                RefreshCounts();
                RefreshTreasureGachaMain();
                treasureGachaPerformance?.PlayDraw();
                Action showResults = () =>
                {
                    if (!treasureGachaShowing) return;
                    treasureGachaMainRenderer?.PlayAnimation("ChestGachaPanel_Loop", null, true);
                    ShowTreasureGachaResults(result.Awards, count);
                };
                if (treasureGachaMainRenderer == null
                    || !treasureGachaMainRenderer.PlayAnimation("ChestGachaPanel_Open", showResults))
                    showResults();
                return;
            }
            Notify(string.IsNullOrEmpty(result.Message) ? "抽取未完成" : result.Message);
        }

        private void ShowTreasureGachaConfirm(string title, string message, Action<bool> confirm,
            bool showDailyReminder = false)
        {
            OpenTreasureGachaOverlay("treasure_gacha_confirm", (renderer, panel) =>
            {
                renderer.SetText("/ConfirmDialog/BgDialog1/BgImg/BgTitle/TitleText", title);
                renderer.SetText("/ConfirmDialog/BgDialog1/Content/TextContent/ContentTxt", message);
                bool suppressToday = false;
                if (showDailyReminder) AddHundredReminderCheckbox(renderer, value => suppressToday = value);
                BindReplace(renderer, "/ConfirmDialog/BgDialog1/Content/DialogButtonGroup/ButtonsGo/CancelBtn", CloseTreasureGachaOverlay);
                BindReplace(renderer, "/ConfirmDialog/BgDialog1/Content/DialogButtonGroup/ButtonsGo/ConfirmBtn", () =>
                {
                    CloseTreasureGachaOverlay();
                    confirm?.Invoke(suppressToday);
                });
            });
        }

        private void AddHundredReminderCheckbox(HomeUiRenderer renderer, Action<bool> changed)
        {
            Transform slot = renderer.Find("/ConfirmDialog/BgDialog1/Content/AdditonContent")?.transform;
            if (slot == null) return;
            slot.gameObject.SetActive(true);
            var checkObject = new GameObject("TodayNoPromptToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            checkObject.transform.SetParent(slot, false);
            RectTransform checkRect = (RectTransform)checkObject.transform;
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0f, 0.5f);
            checkRect.anchoredPosition = new Vector2(12f, 0f);
            checkRect.sizeDelta = new Vector2(32f, 32f);
            Image checkImage = checkObject.GetComponent<Image>();
            Sprite normal = catalog.FindSprite("BtnCheck_Nor-3b5c5a72eb.png");
            Sprite selected = catalog.FindSprite("BtnCheck_Sel-cbe9e7c698.png");
            checkImage.sprite = normal;
            checkImage.preserveAspect = true;

            GameObject textSource = renderer.Find("/ConfirmDialog/BgDialog1/Content/TextContent/ContentTxt");
            GameObject label = textSource == null
                ? new GameObject("TodayNoPromptText", typeof(RectTransform), typeof(TextMeshProUGUI))
                : UnityEngine.Object.Instantiate(textSource, slot, false);
            label.name = "TodayNoPromptText";
            label.transform.SetParent(slot, false);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.offsetMin = new Vector2(52f, -19f);
            labelRect.offsetMax = new Vector2(-12f, 19f);
            TMP_Text labelText = label.GetComponent<TMP_Text>();
            if (labelText != null) labelText.text = "今日不再提示";
            Button button = checkObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = checkImage;
            bool isOn = false;
            button.onClick.AddListener(() =>
            {
                isOn = !isOn;
                checkImage.sprite = isOn ? selected : normal;
                changed?.Invoke(isOn);
            });
            Button labelButton = label.GetComponent<Button>();
            if (labelButton == null) labelButton = label.AddComponent<Button>();
            labelButton.targetGraphic = labelText;
            labelButton.transition = Selectable.Transition.None;
            labelButton.onClick.AddListener(() => button.onClick.Invoke());
        }

        private void ShowTreasureGachaMaps()
        {
            long now = TreasureGachaNow();
            IReadOnlyList<TreasureGachaSeriesEntry> entries = host.TreasureGacha.SeriesEntries(now);
            TreasureGachaSeriesEntry firstLocked = entries.FirstOrDefault(value => !value.IsOpen);
            var visible = entries.Where(value => value.IsOpen || value == firstLocked).ToArray();
            treasureGachaSeriesId = entries.LastOrDefault(value => value.IsOpen && value.IsNew)?.SeriesId
                ?? entries.FirstOrDefault(value => value.SeriesId == treasureGachaSeriesId && value.IsOpen)?.SeriesId
                ?? entries.LastOrDefault(value => value.IsOpen)?.SeriesId ?? 0;
            OpenTreasureGachaOverlay("treasure_gacha_maps", (renderer, panel) =>
            {
                Transform content = renderer.Find(GachaMapsContent)?.transform;
                HomeUiNode rowTemplate = FindUiNode(host.Catalog.Panel("treasure_gacha_map_row")?.root, "ChestMapSwitchItem");
                if (content == null || rowTemplate == null) return;
                foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
                foreach (TreasureGachaSeriesEntry entry in visible)
                {
                    HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                    GameObject row = rowRenderer.CreateNode(rowTemplate, content, (path, node) =>
                    {
                        if (path.EndsWith("/Unlocked/Click", StringComparison.Ordinal)
                            || path.EndsWith("/Unlocked/IconImage", StringComparison.Ordinal))
                            AddButton(node, () =>
                            {
                                if (!entry.IsOpen) return;
                                treasureGachaSeriesId = entry.SeriesId;
                                RefreshMapRows(renderer, visible, rowTemplate, content);
                            });
                    });
                    SetMapRow(rowRenderer, entry, entry.SeriesId == treasureGachaSeriesId);
                    SetListRowPosition(row.transform as RectTransform, content, visible.Length, Array.IndexOf(visible, entry));
                }
                BindReplace(renderer, "/ChestMapSwitchPanel/BgDialog1/Content/DialogButtonGroup/ButtonsGo/ConfirmBtn", () =>
                {
                    if (treasureGachaSeriesId <= 0) { Notify("当前没有已开放的宝箱系列"); return; }
                    CloseTreasureGachaOverlay();
                });
                BindAllNamed(renderer, "CloseBtn", CloseTreasureGachaOverlay);
            }, ConfirmTreasureGachaMapClose);
        }

        private void ConfirmTreasureGachaMapClose()
        {
            int previousSeriesId = host.TreasureGacha.State.SelectedSeriesId;
            TreasureGachaResult result = host.TreasureGacha.ConfirmOpenSeries(treasureGachaSeriesId,
                TreasureGachaNow(), TreasureGachaReceipt("confirm-series"));
            if (!TreasureGachaSucceeded(result))
                Notify(string.IsNullOrEmpty(result.Message) ? "系列确认未完成" : result.Message);
            RefreshTreasureGachaMain();
            if (previousSeriesId != treasureGachaSeriesId) PlayTreasureGachaSwitch();
        }

        private void PlayTreasureGachaSwitch()
        {
            HomeUiRenderer renderer = treasureGachaMainRenderer;
            renderer?.PlayAnimation("ChestGachaPanel_Switch", () =>
                renderer.PlayAnimation("ChestGachaPanel_Loop", null, true));
        }

        private void RefreshMapRows(HomeUiRenderer renderer, TreasureGachaSeriesEntry[] entries,
            HomeUiNode template, Transform content)
        {
            foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
            for (int i = 0; i < entries.Length; i++)
            {
                TreasureGachaSeriesEntry entry = entries[i];
                HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                GameObject row = rowRenderer.CreateNode(template, content, (path, node) =>
                {
                    if (path.EndsWith("/Unlocked/Click", StringComparison.Ordinal)
                        || path.EndsWith("/Unlocked/IconImage", StringComparison.Ordinal))
                        AddButton(node, () =>
                        {
                            if (!entry.IsOpen) return;
                            treasureGachaSeriesId = entry.SeriesId;
                            RefreshMapRows(renderer, entries, template, content);
                        });
                });
                SetMapRow(rowRenderer, entry, entry.SeriesId == treasureGachaSeriesId);
                SetListRowPosition(row.transform as RectTransform, content, entries.Length, i);
            }
        }

        private void SetMapRow(HomeUiRenderer renderer, TreasureGachaSeriesEntry entry, bool selected)
        {
            renderer.SetActive("/ChestMapSwitchItem/Unlocked", entry.IsOpen);
            renderer.SetActive("/ChestMapSwitchItem/Locked", !entry.IsOpen);
            renderer.SetActive("/ChestMapSwitchItem/Unlocked/Border", selected);
            renderer.SetText("/ChestMapSwitchItem/Unlocked/Title/MainTitle", entry.MainTitle);
            renderer.SetText("/ChestMapSwitchItem/Unlocked/Title/Subtitle", entry.Subtitle);
            renderer.SetText("/ChestMapSwitchItem/Unlocked/CollectionText",
                "收藏：" + entry.CollectionOwned + "/" + entry.CollectionTotal);
            renderer.SetText("/ChestMapSwitchItem/Locked/Infos/LockedText", entry.IsPreview
                ? "将于 " + entry.OpensAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 开放"
                : "尚未解锁");
            SetSprite(renderer, "/ChestMapSwitchItem/Unlocked/BgImg", entry.EntryBgSpriteFile);
            SetSprite(renderer, "/ChestMapSwitchItem/Unlocked/IconImage", entry.EntryIconSpriteFile);
            SetSprite(renderer, "/ChestMapSwitchItem/Locked/IconImage", entry.IconSpriteFile);
            renderer.SetActive("/ChestMapSwitchItem/Unlocked/NewMark", entry.IsNew);
        }

        private void ShowTreasureGachaCollection()
        {
            TreasureGachaSeries series = host.TreasureGachaCatalog.SeriesInfo(treasureGachaSeriesId);
            if (series == null) { Notify("当前系列图鉴不可用"); return; }
            OpenTreasureGachaOverlay("treasure_gacha_collection", (renderer, panel) =>
            {
                renderer.SetActiveByName("SkillContent", false);
                renderer.SetActiveByName("TreasureContent", true);
                renderer.SetActiveByName("TreasureCollectBookContent", true);
                renderer.SetTextByName("CurrentCount", host.TreasureGacha.SeriesEntries(TreasureGachaNow())
                    .FirstOrDefault(value => value.SeriesId == series.ChestGachaId)?.CollectionOwned.ToString() ?? "0");
                renderer.SetTextByName("TotalCount", series.ChestGachaItems.Length.ToString(CultureInfo.InvariantCulture));
                HomeUiNode slotTemplate = FindUiNode(host.Catalog.Panel("treasure_gacha_collection_slot")?.root,
                    "CollectBookItemTemplate");
                HomeUiNode iconTemplate = host.Catalog.Panel("treasure_gacha_collection_icon")?.root;
                Transform collectionContent = FindNamedTransform(panel.transform, "TreasureContent");
                Transform grid = FindNamedTransform(collectionContent, "CollectGrid");
                if (slotTemplate == null || iconTemplate == null || grid == null) return;
                foreach (Transform child in grid) UnityEngine.Object.Destroy(child.gameObject);
                int index = 0;
                foreach (int itemId in series.ChestGachaItems)
                {
                    TreasureItemConfig item = host.TreasureGachaCatalog.Item(itemId);
                    if (item == null) continue;
                    GameObject slot = renderer.CreateNode(slotTemplate, grid);
                    bool ownedItem = host.ItemCount(itemId) > 0;
                    Transform slotContent = FindNamedTransform(slot.transform, "Slot");
                    if (slotContent != null)
                    {
                        HomeUiRenderer iconRenderer = new HomeUiRenderer(host.Catalog);
                        iconRenderer.CreateNode(iconTemplate, slotContent);
                        iconRenderer.SetSprite("/CollectBookItemIcon/Content/mask/icon", TreasureItemIcon(itemId));
                        iconRenderer.SetText("/CollectBookItemIcon/Content/BottomText", item.Name);
                        Transform lockRoot = FindNamedTransform(slotContent, "Lock");
                        if (lockRoot != null) lockRoot.gameObject.SetActive(!ownedItem);
                    }
                    Transform quality = FindNamedTransform(slot.transform, "Bg");
                    if (quality != null) quality.gameObject.SetActive(ownedItem);
                    slot.name = "TreasureCollection_" + itemId;
                    index++;
                }
                BindAllNamed(renderer, "CloseBtn", CloseTreasureGachaOverlay);
            });
        }

        private void ShowTreasureGachaDetail()
        {
            OpenTreasureGachaOverlay("treasure_gacha_detail", (renderer, panel) =>
            {
                renderer.SetTextByName("TitleText", "抽取概率与记录");
                renderer.SetActive(GachaDetailPreview, true);
                renderer.SetActive(GachaDetailHistory, false);
                renderer.SetText("/GachaDetailDialog/BgDialog1/Content/RewardPreviewScroll/RewardPreviewViewport/RewardPreviewGo/GachaRuleItem/RuleTextItem/Desc",
                    BuildTreasureGachaRuleSummary());
                renderer.SetText("/GachaDetailDialog/BgDialog1/Content/RewardPreviewScroll/RewardPreviewViewport/RewardPreviewGo/AwardRateRuleItem/RuleTextItem/Desc",
                    "奖池按当前系列筛选；保底概率为每次抽取的命中概率。");
                PopulateTreasureGachaProbability(renderer);
                BindAllNamed(renderer, "CloseBtn", CloseTreasureGachaOverlay);
                BindAllNamed(renderer, "HistoryTabItem", () => ShowTreasureGachaHistory());
                BindAllNamed(renderer, "HistoryBtn", () => ShowTreasureGachaHistory());
            });
        }

        private void PopulateTreasureGachaProbability(HomeUiRenderer renderer)
        {
            Transform templateTransform = renderer.Find(GachaDetailRateTemplate)?.transform;
            HomeUiNode rateTemplate = FindUiNode(host.Catalog.Panel("treasure_gacha_detail")?.root,
                "RuleRateInfoItemGacha");
            HomeUiNode iconTemplate = host.Catalog.Panel("treasure_gacha_collection_icon")?.root;
            Transform parent = templateTransform == null ? null : templateTransform.parent;
            if (templateTransform == null || rateTemplate == null || iconTemplate == null || parent == null)
                return;

            templateTransform.gameObject.SetActive(false);
            IReadOnlyList<TreasureGachaProbabilityEntry> entries = host.TreasureGacha.ProbabilityItems(
                treasureGachaType, treasureGachaType <= 7 ? treasureGachaSeriesId : 0,
                TreasureGachaNow());
            var groups = entries.GroupBy(value => (value.GroupType, value.Quality))
                .OrderByDescending(value => value.Key.Quality)
                .ThenBy(value => value.Key.GroupType).ToArray();
            foreach (IGrouping<(int GroupType, int Quality), TreasureGachaProbabilityEntry> group in groups)
            {
                HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                rowRenderer.CreateNode(rateTemplate, parent);
                string root = "/RuleRateInfoItemGacha/RateContent";
                string groupName = group.Key.GroupType == 6 ? "古遗物" : group.Key.GroupType == 7
                    ? "古遗物碎片" : "奖励";
                rowRenderer.SetText(root + "/TitleGo/TitleText",
                    QualityName(group.Key.Quality) + groupName);
                rowRenderer.SetText(root + "/TitleGo/BaseProbilityText",
                    "基础概率：" + FormatGachaPercent(group.Sum(value => value.Probability)));
                long pity = host.TreasureGacha.PityProbability(treasureGachaType,
                    group.Key.Quality, group.Key.GroupType);
                rowRenderer.SetText(root + "/TitleGo/PityProbilityText", pity < 0
                    ? string.Empty : "保底概率：" + (pity / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%");
                Transform grid = rowRenderer.Find(root + "/ItemContent/Grid")?.transform;
                if (grid == null) continue;
                foreach (TreasureGachaProbabilityEntry entry in group)
                {
                    TreasureItemConfig item = host.TreasureGachaCatalog.Item(entry.ItemId);
                    if (item == null) continue;
                    HomeUiRenderer iconRenderer = new HomeUiRenderer(host.Catalog);
                    iconRenderer.CreateNode(iconTemplate, grid);
                    iconRenderer.SetSprite("/CollectBookItemIcon/Content/mask/icon",
                        TreasureItemIcon(entry.ItemId));
                    iconRenderer.SetText("/CollectBookItemIcon/Content/BottomText", item.Name);
                    iconRenderer.SetActive("/CollectBookItemIcon/Content/Lock", false);
                    iconRenderer.SetActive("/CollectBookItemIcon/Content/mask/FakeIcon", false);
                }
            }
        }

        private static string FormatGachaPercent(double probability)
        {
            return (probability * 100d).ToString("0.####", CultureInfo.InvariantCulture) + "%";
        }

        private string BuildTreasureGachaRuleSummary()
        {
            TreasureGachaConfig gacha = host.TreasureGachaCatalog.Gacha(treasureGachaType);
            TreasureChestConfig chest = gacha == null ? null : host.TreasureGachaCatalog.Chest(gacha.ChestId);
            int remaining = Enumerable.Range(1, 10)
                .Select(quality => host.TreasureGacha.RemainingGuaranteePulls(treasureGachaType, quality))
                .Where(value => value > 0).DefaultIfEmpty(0).Min();
            string score = gacha == null || gacha.RewardScoreId <= 0 ? "元素宝箱不增加扭蛋积分"
                : "每次抽取获得积分：" + (chest?.Rewards?.FirstOrDefault(value => value.ItemId == gacha.RewardScoreId)?.Count ?? 0);
            return "今日抽取：" + host.TreasureGacha.DailyCount(DateTime.Now) + "/" + host.TreasureGachaCatalog.Settings.DailyLimit
                + "\n" + (remaining > 0 ? "距当前保底还需 " + remaining + " 次" : "保底状态已保存")
                + "\n" + score;
        }

        private void ShowTreasureGachaHistory()
        {
            treasureGachaHistoryPage = 0;
            RefreshTreasureGachaHistory();
        }

        private void RefreshTreasureGachaHistory()
        {
            CloseTreasureGachaOverlay();
            long now = TreasureGachaNow();
            treasureGachaHistoryRows = new List<OfflineTreasureDrawRecord>();
            for (int page = 0; page < 10; page++)
            {
                IReadOnlyList<OfflineTreasureDrawRecord> pageRows = host.TreasureGacha.History(treasureGachaType, now, page, 100);
                treasureGachaHistoryRows.AddRange(pageRows);
                if (pageRows.Count < 100) break;
            }
            OpenTreasureGachaOverlay("treasure_gacha_history", (renderer, panel) =>
            {
                Transform content = renderer.Find(GachaHistoryContent)?.transform;
                HomeUiNode rowTemplate = host.Catalog.Panel("treasure_gacha_history_row")?.root;
                if (content == null || rowTemplate == null) return;
                foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
                int totalPages = Math.Max(1, (HistoryCount(now) + 9) / 10);
                int start = treasureGachaHistoryPage * 10;
                for (int i = 0; i < 10 && start + i < treasureGachaHistoryRows.Count; i++)
                {
                    OfflineTreasureDrawRecord record = treasureGachaHistoryRows[start + i];
                    OfflineTreasureAward award = record.Awards.FirstOrDefault();
                    if (award == null) continue;
                    HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                    GameObject row = rowRenderer.CreateNode(rowTemplate, content);
                    rowRenderer.SetText("/GachaHistoryItem/ItemTypeText", QualityName(award.Quality));
                    rowRenderer.SetText("/GachaHistoryItem/ItemNameText", AwardDescription(award));
                    rowRenderer.SetText("/GachaHistoryItem/GotTimeText",
                        DateTimeOffset.FromUnixTimeSeconds(record.UnixSeconds).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    SetListRowPosition(row.transform as RectTransform, content, Math.Min(10, treasureGachaHistoryRows.Count - start), i);
                }
                renderer.SetText("/GachaHistoryContent/TurnPageUI/CurPageText", (treasureGachaHistoryPage + 1).ToString(CultureInfo.InvariantCulture));
                renderer.SetText("/GachaHistoryContent/TurnPageUI/TotalPageText ", totalPages.ToString(CultureInfo.InvariantCulture));
                BindReplace(renderer, "/GachaHistoryContent/TurnPageUI/PrevBtn", () =>
                {
                    if (treasureGachaHistoryPage <= 0) return;
                    treasureGachaHistoryPage--;
                    RefreshTreasureGachaHistory();
                });
                BindReplace(renderer, "/GachaHistoryContent/TurnPageUI/NextBtn", () =>
                {
                    if (treasureGachaHistoryPage + 1 >= totalPages) return;
                    treasureGachaHistoryPage++;
                    RefreshTreasureGachaHistory();
                });
                BindAllNamed(renderer, "CloseBtn", CloseTreasureGachaOverlay);
            });
        }

        private int HistoryCount(long now)
        {
            long cutoff = Math.Max(0, now - host.TreasureGachaCatalog.Settings.HistoryDays * 86400L);
            return treasureGachaHistoryRows.Count(value => value.UnixSeconds >= cutoff
                && (treasureGachaType <= 7 ? value.GachaType == treasureGachaType : value.GachaType >= 8 && value.GachaType <= 12));
        }

        private void ShowTreasureGachaScore()
        {
            OpenTreasureGachaOverlay("treasure_gacha_score", (renderer, panel) =>
            {
                renderer.SetText("/ScoreRewardDialog/BgDialog1/BgImg/BgTitle/TitleText", "扭蛋积分奖励");
                renderer.SetText("/ScoreRewardDialog/BgDialog1/Content/RuleContent/RuleGo /ViewPort/Content/RuleText",
                    "珍稀宝箱抽取获得积分：蓝、紫、橙、金宝箱每次分别获得1、2、3、5分。当前累计 "
                    + host.TreasureGacha.State.PreciousScore + " 分。");
                Transform content = renderer.Find(GachaScoreContent)?.transform;
                HomeUiNode template = host.Catalog.Panel("treasure_gacha_score_item")?.root;
                if (content == null || template == null) return;
                foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
                IReadOnlyList<TreasureGachaAchievementEntry> entries = host.TreasureGacha.Achievements(12, 0, 100);
                for (int i = 0; i < entries.Count; i++)
                {
                    TreasureGachaAchievementEntry entry = entries[i];
                    HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                    GameObject row = rowRenderer.CreateNode(template, content);
                    FillAchievementRow(rowRenderer, row, entry, () => ClaimTreasureAchievement(12, entry.Index));
                    SetListRowPosition(row.transform as RectTransform, content, entries.Count, i);
                }
                BindAllNamed(renderer, "CloseBtn", CloseTreasureGachaOverlay);
            });
        }

        private void RefreshTreasureGachaAchievements()
        {
            HomeUiRenderer renderer = treasureGachaMainRenderer;
            if (renderer == null) return;
            Transform grid = renderer.Find(GachaAchievementGrid)?.transform;
            HomeUiNode template = FindUiNode(host.Catalog.Panel("treasure_gacha_main")?.root, "AchievementPanelItem");
            if (grid == null || template == null) return;
            foreach (Transform child in grid) UnityEngine.Object.Destroy(child.gameObject);
            IReadOnlyList<TreasureGachaAchievementEntry> entries = treasureGachaAchievementGroup == 0
                ? host.TreasureGacha.Achievements(11, 0, 100)
                    .Concat(host.TreasureGacha.Achievements(13, 0, 100))
                    .OrderBy(value => value.Group).ThenBy(value => value.Index).ToArray()
                : host.TreasureGacha.Achievements(treasureGachaAchievementGroup, 0, 100);
            for (int i = 0; i < entries.Count; i++)
            {
                TreasureGachaAchievementEntry entry = entries[i];
                HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                GameObject row = rowRenderer.CreateNode(template, grid);
                FillAchievementRow(rowRenderer, row, entry,
                    () => ClaimTreasureAchievement(entry.Group, entry.Index));
                SetListRowPosition(row.transform as RectTransform, grid, entries.Count, i);
            }
        }

        private void SetTreasureAchievementGroup(int group)
        {
            treasureGachaAchievementGroup = group;
            RefreshTreasureGachaAchievements();
        }

        private void FillAchievementRow(HomeUiRenderer renderer, GameObject row,
            TreasureGachaAchievementEntry entry, Action claim)
        {
            string name = entry.Group == 12 ? "扭蛋积分" : entry.Group == 13 ? "元素宝箱馈赠" : "珍稀宝箱馈赠";
            renderer.SetTextByName("TitleTxt", name + " · " + entry.Target);
            renderer.SetTextByName("ProgressTxt", entry.CurrentValue + "/" + entry.Target);
            Transform grid = FindNamedTransform(row.transform, "AwardGrid");
            if (grid != null)
            {
                foreach (Transform child in grid) UnityEngine.Object.Destroy(child.gameObject);
                for (int i = 0; i < entry.Rewards.Count; i++)
                {
                    TreasureAmount reward = entry.Rewards[i];
                    TreasureItemConfig item = host.TreasureGachaCatalog.Item(reward.ItemId);
                    var iconGo = new GameObject("Reward_" + reward.ItemId, typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(grid, false);
                    RectTransform rect = iconGo.transform as RectTransform;
                    rect.sizeDelta = new Vector2(58, 58);
                    Image icon = iconGo.GetComponent<Image>();
                    icon.sprite = item == null ? host.ItemIcon(reward.ItemId) : TreasureItemIcon(item.ClassId);
                    icon.preserveAspect = true;
                    var textGo = new GameObject("Count", typeof(RectTransform));
                    textGo.transform.SetParent(iconGo.transform, false);
                    RectTransform textRect = textGo.transform as RectTransform;
                    textRect.anchorMin = new Vector2(1, 0);
                    textRect.anchorMax = new Vector2(1, 0);
                    textRect.pivot = new Vector2(1, 0);
                    textRect.anchoredPosition = Vector2.zero;
                    textRect.sizeDelta = new Vector2(90, 24);
                    TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
                    text.font = catalog.Font;
                    text.fontSize = 18;
                    text.alignment = TextAlignmentOptions.BottomRight;
                    text.color = Color.white;
                    text.text = "×" + reward.Count;
                    text.raycastTarget = false;
                }
            }
            SetActiveInRow(row.transform, "BtnGroup/ReceiveBtn", entry.CanClaim);
            SetActiveInRow(row.transform, "BtnGroup/UncompletedGo", !entry.CanClaim && !entry.IsClaimed);
            SetActiveInRow(row.transform, "BtnGroup/CommonSeal", entry.IsClaimed);
            Button receive = FindNamedTransform(row.transform, "ReceiveBtn")?.GetComponent<Button>();
            if (receive != null) receive.onClick.AddListener(() => claim?.Invoke());
        }

        private void ClaimTreasureAchievement(int group, int index)
        {
            TreasureGachaResult result = host.TreasureGacha.ClaimAchievement(group, index,
                TreasureGachaReceipt("claim-achievement"), TreasureGachaNow());
            if (!TreasureGachaSucceeded(result)) return;
            host.RefreshCounts();
            Notify("奖励已领取");
            if (group == 12) ShowTreasureGachaScore();
            else RefreshTreasureGachaAchievements();
        }

        private void ShowTreasureGachaResults(IReadOnlyList<OfflineTreasureAward> awards, int count)
        {
            OpenTreasureGachaOverlay("treasure_gacha_result", (renderer, panel) =>
            {
                string middle = "/GachaResultPanel/BgPanel1/Content/MiddleGo/";
                bool single = awards.Count == 1;
                renderer.SetActive(middle + "OneGrid", single);
                renderer.SetActive(middle + "HundredGrid", !single);
                renderer.SetActive(middle + "GachaBtnContent", true);
                renderer.SetActive("/GachaResultPanel/BgPanel1/Content/SkipBtn", false);
                int daily = host.TreasureGacha.DailyCount(DateTime.Now);
                int dailyLimit = host.TreasureGachaCatalog.Settings.DailyLimit;
                renderer.SetActive(middle + "TodayCountGo", true);
                renderer.SetText(middle + "TodayCountGo/FreeCount", "今日抽取：" + daily + "/" + dailyLimit);
                if (single)
                {
                    OfflineTreasureAward award = awards[0];
                    renderer.SetText(middle + "OneGrid/NameGo/NameText", host.ItemName(award.ItemId));
                    renderer.SetText(middle + "OneGrid/TypeText", QualityName(award.Quality)
                        + (award.IsPiece ? " · 碎片" : ""));
                    renderer.SetText(middle + "OneGrid/DescGo/DescText", AwardDescription(award)
                        + TreasureAwardConversionText(award));
                    AddTreasureAwardIcon(FindNamedTransform(panel.transform, "Pos1"), award, 144f);
                }
                else
                {
                    Transform content = renderer.Find(middle + "HundredGrid/View/Content")?.transform;
                    HomeUiNode template = host.Catalog.Panel("treasure_gacha_history_row")?.root;
                    if (content != null && template != null)
                    {
                        foreach (Transform child in content) UnityEngine.Object.Destroy(child.gameObject);
                        for (int i = 0; i < awards.Count; i++)
                        {
                            OfflineTreasureAward award = awards[i];
                            HomeUiRenderer rowRenderer = new HomeUiRenderer(host.Catalog);
                            GameObject row = rowRenderer.CreateNode(template, content);
                            ConfigureTreasureResultRow(rowRenderer, row, award);
                            SetListRowPosition(row.transform as RectTransform, content, awards.Count, i);
                        }
                    }
                }
                BindTreasureResultDrawButton(renderer, middle + "GachaBtnContent/GachaOneTimesBtn/CommonBtn_CostItem", 1);
                int repeatCount = CurrentRepeatCount();
                BindTreasureResultDrawButton(renderer, middle + "GachaBtnContent/GachaTenTimesBtn/CommonBtn_CostItem",
                    repeatCount > 1 ? repeatCount : 0);
                BindTreasureResultDrawButton(renderer, middle + "GachaBtnContent/GachaHundredTimesBtn/CommonBtn_CostItem", 100);
                BindAllNamed(renderer, "CloseBtn", CloseTreasureGachaOverlay);
                renderer.PlayAnimation("GachaStarsFly");
            });
        }

        private void ConfigureTreasureResultRow(HomeUiRenderer renderer, GameObject row,
            OfflineTreasureAward award)
        {
            RectTransform rect = row.transform as RectTransform;
            rect.sizeDelta = new Vector2(958f, 68f);
            SetResultColumn(renderer.Find("/GachaHistoryItem/ItemTypeText")?.GetComponent<RectTransform>(),
                new Vector2(140f, 0f), new Vector2(128f, 64f));
            SetResultColumn(renderer.Find("/GachaHistoryItem/ItemNameText")?.GetComponent<RectTransform>(),
                new Vector2(300f, 0f), new Vector2(292f, 64f));
            SetResultColumn(renderer.Find("/GachaHistoryItem/GotTimeText")?.GetComponent<RectTransform>(),
                new Vector2(660f, 0f), new Vector2(318f, 64f));
            renderer.SetText("/GachaHistoryItem/ItemTypeText", QualityName(award.Quality));
            renderer.SetText("/GachaHistoryItem/ItemNameText", AwardDescription(award));
            renderer.SetText("/GachaHistoryItem/GotTimeText", TreasureAwardConversionText(award));
            AddTreasureAwardIcon(row.transform, award, 54f, new Vector2(40f, 0f));
        }

        private static void SetResultColumn(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void AddTreasureAwardIcon(Transform parent, OfflineTreasureAward award, float size,
            Vector2? anchoredPosition = null)
        {
            Sprite sprite = TreasureItemIcon(award.ItemId);
            if (parent == null || sprite == null) return;
            var iconObject = new GameObject("TreasureAwardIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            RectTransform rect = iconObject.transform as RectTransform;
            rect.anchorMin = anchoredPosition.HasValue ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private string TreasureAwardConversionText(OfflineTreasureAward award)
        {
            if (award.ConvertedCount > 0)
                return "\n轉化為 " + host.ItemName(award.ConvertedItemId) + " × " + award.ConvertedCount;
            if (award.RecycledCount > 0)
                return "\n返還 " + host.ItemName(award.RecycledItemId) + " × " + award.RecycledCount;
            return string.Empty;
        }

        private void BindTreasureResultDrawButton(HomeUiRenderer renderer, string path, int count)
        {
            string buttonPath = path.Substring(0, path.LastIndexOf("/", StringComparison.Ordinal));
            if (count <= 0)
            {
                renderer.SetActive(buttonPath, false);
                return;
            }
            TreasureGachaConfig gacha = host.TreasureGachaCatalog.Gacha(treasureGachaType);
            TreasureChestConfig chest = gacha == null ? null : host.TreasureGachaCatalog.Chest(gacha.ChestId);
            if (chest == null || !chest.OpenCounts.Contains(count))
            {
                renderer.SetActive(buttonPath, false);
                return;
            }
            TreasureAmount cost = chest.CostItems.FirstOrDefault();
            if (cost == null) return;
            renderer.SetActive(buttonPath, CanAffordTreasureDraw(chest, count));
            SetDrawCost(renderer, path, cost.ItemId, cost.Count, count);
            BindReplace(renderer, path, () => DrawTreasureGacha(count));
        }

        private bool CanAffordTreasureDraw(TreasureChestConfig chest, int count)
        {
            int dailyLimit = host.TreasureGachaCatalog.Settings.DailyLimit;
            if (dailyLimit > 0 && host.TreasureGacha.DailyCount(DateTime.Now) + count > dailyLimit) return false;
            long quickBuyCurrency = 0;
            foreach (TreasureAmount cost in chest.CostItems)
            {
                long required = checked(cost.Count * count);
                long available = host.ItemCount(cost.ItemId);
                if (available >= required) continue;
                if (treasureGachaType != 4 || cost.ItemId != TreasureQuickBuyItemId) return false;
                quickBuyCurrency = checked(quickBuyCurrency + (required - available) * TreasureQuickBuyCurrencyPerItem);
            }
            return quickBuyCurrency <= host.ItemCount(2);
        }

        private void OpenTreasureGachaOverlay(string panelKey, Action<HomeUiRenderer, GameObject> build,
            Action closing = null)
        {
            if (!treasureGachaShowing || activeDialog == null) return;
            CloseTreasureGachaOverlay();
            var overlay = new GameObject("TreasureGachaOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            overlay.transform.SetParent(activeDialog.transform, false);
            RectTransform rect = overlay.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = overlay.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.54f);
            Button blocker = overlay.GetComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.onClick.AddListener(CloseTreasureGachaOverlay);
            treasureGachaOverlay = overlay;
            HomeUiRenderer renderer = Renderer();
            treasureGachaOverlayRenderer = renderer;
            treasureGachaOverlayClosing = closing;
            GameObject panel = renderer.Create(panelKey, overlay.transform, null);
            RectTransform panelRect = panel.transform as RectTransform;
            HomeUiNode panelRoot = host.Catalog.Panel(panelKey)?.root;
            if (StretchesAcrossParent(panelRoot))
            {
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
            }
            build?.Invoke(renderer, panel);
        }

        private static bool StretchesAcrossParent(HomeUiNode node)
        {
            return node?.anchor_min != null && node.anchor_min.Length >= 2
                && node.anchor_max != null && node.anchor_max.Length >= 2
                && Mathf.Abs(node.anchor_min[0]) < 0.001f && Mathf.Abs(node.anchor_min[1]) < 0.001f
                && Mathf.Abs(node.anchor_max[0] - 1f) < 0.001f && Mathf.Abs(node.anchor_max[1] - 1f) < 0.001f;
        }

        private void CloseTreasureGachaOverlay()
        {
            Action closing = treasureGachaOverlayClosing;
            treasureGachaOverlayClosing = null;
            if (treasureGachaOverlay != null) UnityEngine.Object.Destroy(treasureGachaOverlay);
            if (treasureGachaOverlayRenderer != null) owned.Remove(treasureGachaOverlayRenderer);
            treasureGachaOverlay = null;
            treasureGachaOverlayRenderer = null;
            closing?.Invoke();
        }

        private static bool TreasureGachaSucceeded(TreasureGachaResult result)
        {
            return result != null && (result.IsApplied || result.Code == TreasureGachaResultCode.Unchanged);
        }

        private void BindReplace(HomeUiRenderer renderer, string path, Action action)
        {
            Button button = renderer.Find<Button>(path);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void BindAllNamed(HomeUiRenderer renderer, string name, Action action)
        {
            foreach (GameObject node in renderer.AllNodes)
            {
                if (node == null || node.name != name) continue;
                Button button = node.GetComponent<Button>();
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action?.Invoke());
            }
        }

        private static void AddButton(GameObject node, Action action)
        {
            if (node == null) return;
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private static HomeUiNode FindUiNode(HomeUiNode root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (HomeUiNode child in root.children ?? Array.Empty<HomeUiNode>())
            {
                HomeUiNode found = FindUiNode(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindNamedTransform(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindNamedTransform(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetActiveInRow(Transform root, string path, bool active)
        {
            Transform node = root;
            foreach (string name in path.Split('/'))
            {
                node = node == null ? null : node.Find(name);
                if (node == null) return;
            }
            node.gameObject.SetActive(active);
        }

        private void SetSprite(HomeUiRenderer renderer, string path, string spriteFile)
        {
            if (!string.IsNullOrEmpty(spriteFile)) renderer.SetSprite(path, catalog.FindSprite(spriteFile));
        }

        private Sprite TreasureItemIcon(int itemId)
        {
            TreasureItemConfig item = host.TreasureGachaCatalog.Item(itemId);
            return item == null || string.IsNullOrEmpty(item.IconFile)
                ? host.ItemIcon(itemId) : catalog.FindSprite(item.IconFile) ?? host.ItemIcon(itemId);
        }

        private static void SetListRowPosition(RectTransform row, Transform content, int count, int index)
        {
            if (row == null || content == null) return;
            if (content.GetComponent<LayoutGroup>() == null)
            {
                VerticalLayoutGroup vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vertical.childAlignment = TextAnchor.UpperCenter;
                vertical.childControlWidth = true;
                vertical.childControlHeight = false;
                vertical.childForceExpandWidth = true;
                vertical.childForceExpandHeight = false;
                vertical.spacing = 0f;
            }
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            row.localScale = Vector3.one;
        }

        private string AwardDescription(OfflineTreasureAward award)
        {
            string name = host.ItemName(award.ItemId);
            if (string.IsNullOrEmpty(name)) name = "物品 " + award.ItemId;
            return name + " × " + award.Count + (award.IsPiece ? "（碎片）" : string.Empty);
        }

        private static string QualityName(int quality)
        {
            switch (quality)
            {
                case 1: return "普通";
                case 2: return "稀有";
                case 3: return "史诗";
                case 4: return "传说";
                case 5: return "奇迹";
                default: return "物品";
            }
        }

        private static long TreasureGachaNow() => DateTimeOffset.Now.ToUnixTimeSeconds();

        private static string TreasureGachaReceipt(string action)
            => "treasure-gacha:" + action + ":" + Guid.NewGuid().ToString("N");
    }
}
