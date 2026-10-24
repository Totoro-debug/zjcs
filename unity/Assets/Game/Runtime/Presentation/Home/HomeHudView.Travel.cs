using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private static readonly Regex TravelIndexPattern = new Regex(@"\((\d+)\)$", RegexOptions.Compiled);
        private GameObject npcTravelRoot;
        private GameObject npcTravelMapRoot;
        private GameObject npcTravelDialogRoot;
        private GameObject npcTravelSelectRoot;
        private GameObject npcTravelConfirmRoot;
        private HomeUiRenderer npcTravelPageRenderer;
        private HomeUiRenderer npcTravelMapRenderer;
        private HomeUiRenderer npcTravelDialogRenderer;
        private HomeUiRenderer npcTravelSelectRenderer;
        private NpcTravelRow selectedTravel;
        private readonly List<int> selectedTravelNpcIds = new List<int>();
        private int npcTravelMapGroupId;
        private int npcTravelRegionId;
        private int npcTravelLastDayKey;
        private float npcTravelNextTick;
        private int npcTravelEventSequence;
        private readonly List<GameObject> npcTravelConditionItems = new List<GameObject>();
        private readonly List<GameObject> npcTravelExtraRewardItems = new List<GameObject>();
        private Image npcTravelBaseRewardIcon;

        private bool NpcTravelModalOpen => npcTravelRoot != null;

        private bool NpcTravelUnlocked => host.Profile.Save.Player.CharacterProperties.NpcTalentTrees
            .Any(value => value != null && value.ActiveNodeIds != null && value.ActiveNodeIds.Count > 0);

        private void AddNpcAdventureTravelEntry()
        {
            Transform entry = Find(travelNotesRoot == null ? null : travelNotesRoot.transform, "NpcAdventureEntry");
            if (entry == null) return;
            int remaining = Math.Max(0, host.NpcTravelCatalog.Data.daily_start_limit
                - host.NpcTravel.State.DailyStartedCount);
            SetNamedText(entry, "CountText", remaining + "/" + host.NpcTravelCatalog.Data.daily_start_limit);
            Transform buttonNode = Find(entry, "EntryBtn");
            Button button = buttonNode == null ? null : buttonNode.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenNpcAdventureTravelFromNotes);
        }

        private void OpenNpcAdventureTravelFromNotes()
        {
            if (!NpcTravelUnlocked)
            {
                Notify("伙伴羁绊第一节点开放后可使用伙伴旅行");
                return;
            }
            NpcTravelNode node = ResolveNpcTravelNodeFromMainline();
            if (node == null)
            {
                Notify("当前主线进度尚未解锁可用的伙伴旅行位置");
                return;
            }
            if (travelNotesRoot != null) UnityEngine.Object.Destroy(travelNotesRoot);
            travelNotesRoot = null;
            OpenNpcAdventurePage(node.map_group_id, node.region_id);
        }

        private NpcTravelNode ResolveNpcTravelNodeFromMainline()
        {
            var maps = host?.Profile?.Save?.World?.Maps;
            NpcTravelNode[] nodes = host?.NpcTravelCatalog?.Data?.nodes;
            if (maps == null || nodes == null) return null;

            var progressedRegions = new HashSet<long>();
            foreach (OfflineMapProgress map in maps)
            {
                if (map == null || map.MapClassId <= 0 || map.Regions == null) continue;
                foreach (OfflineRegionProgress region in map.Regions)
                {
                    if (region == null || region.Progress <= 0 || region.Region < 0) continue;
                    progressedRegions.Add(((long)map.MapClassId << 32) | (uint)region.Region);
                }
            }

            return nodes.Where(node => node != null && node.stage_id > 0 && node.map_group_id > 0
                    && node.region_id > 0
                    && progressedRegions.Contains(((long)node.map_group_id << 32) | (uint)node.region_id))
                .OrderByDescending(node => node.stage_id)
                .ThenByDescending(node => node.node_id)
                .FirstOrDefault();
        }

        private void OpenNpcAdventurePage(int mapGroupId, int regionId)
        {
            if (!NpcTravelUnlocked)
            {
                Notify("伙伴羁绊第一节点开放后可使用伙伴旅行");
                return;
            }
            if (npcTravelRoot != null) return;
            if (mapGroupId <= 0 || regionId <= 0)
            {
                Notify("当前伙伴旅行位置尚未确定");
                return;
            }

            npcTravelMapGroupId = mapGroupId;
            npcTravelRegionId = regionId;
            DateTime now = DateTime.Now;
            npcTravelLastDayKey = OfflineNpcTravelService.LocalTravelDayKey(now);
            if (!host.NpcTravel.RefreshLocalDay(now, mapGroupId, regionId, TravelEvent("refresh")))
            {
                Notify("旅行委托刷新失败");
                return;
            }

            npcTravelPageRenderer = Renderer();
            npcTravelRoot = npcTravelPageRenderer.Create("npc_adventure", canvas.transform, BindNpcTravelPageButton);
            npcTravelRoot.name = "NpcAdventurePanel";
            npcTravelRoot.transform.SetAsLastSibling();
            npcTravelPageRenderer.SetActive("/NpcAdventurePanel/BgPanel1/Content/NpcAdventurePersonalTopPanel", false);
            npcTravelPageRenderer.SetActive("/NpcAdventurePanel/BgPanel1/Content/AchievementPanelWithTypes", false);
            Transform mapSlot = Find(npcTravelRoot.transform, "MapSlot");
            if (mapSlot == null) throw new InvalidOperationException("Official NPC travel map slot is missing.");
            npcTravelMapRenderer = Renderer();
            npcTravelMapRoot = npcTravelMapRenderer.Create(MapPanelKey(mapGroupId, regionId), mapSlot,
                BindNpcTravelMapButton);
            RefreshNpcTravelMap();
        }

        private void BindNpcTravelPageButton(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.EndsWith("/CloseBtn", StringComparison.Ordinal))
                button.onClick.AddListener(CloseNpcTravelPages);
            else if (path.EndsWith("/RefreshBtn (1)", StringComparison.Ordinal))
                button.onClick.AddListener(RefreshNpcTravelOffers);
            else if (path.EndsWith("/BattleBtn", StringComparison.Ordinal)
                || path.EndsWith("/FastBattleBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => Notify("旅行页的战斗入口按官方客户端范围关闭"));
        }

        private void BindNpcTravelMapButton(string path, GameObject node)
        {
            if (!path.Contains("/TravelItem (", StringComparison.Ordinal)) return;
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            string[] segments = path.Split('/');
            string group = segments.FirstOrDefault(value => value.StartsWith("NpcAdventureMapTravelItem", StringComparison.Ordinal));
            int positionId = TravelIndex(group, 1);
            button.onClick.AddListener(() => OpenNpcTravelDetail(positionId));
        }

        private void RefreshNpcTravelOffers()
        {
            OfflineNpcTravelProgress state = host.NpcTravel.State;
            int[] positions = Enumerable.Range(1, 4).Where(positionId => !state.Targets.Any(target =>
                target.PositionId == positionId && !target.Received)).ToArray();
            if (positions.Length == 0)
            {
                Notify("没有可刷新的旅行位置");
                return;
            }
            long[] costs = host.NpcTravelCatalog.Data.refresh_costs;
            int costIndex = Mathf.Clamp(state.ManualRefreshedCount, 0, costs.Length - 1);
            long cost = costs[costIndex];
            int itemId = host.NpcTravelCatalog.Data.refresh_cost_item_id;
            long balance = host.Profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;
            if (balance < cost)
            {
                Notify("刷新道具不足，需要 " + cost + " 个 " + host.ItemName(itemId));
                return;
            }
            ShowTravelConfirm("刷新旅行委托", "将消耗 " + cost + " 个 " + host.ItemName(itemId)
                + "，并替换 " + positions.Length + " 个未派遣位置。正在旅行的位置会保留。", () =>
                {
                    ProgressionResult result = host.NpcTravel.ManualRefresh(positions,
                        npcTravelMapGroupId, npcTravelRegionId, TravelEvent("manual-refresh"));
                    if (!result.IsApplied)
                    {
                        Notify(result.Message ?? "旅行委托刷新失败");
                        return;
                    }
                    RefreshNpcTravelMap();
                });
        }

        private void RefreshNpcTravelMap()
        {
            if (npcTravelMapRenderer == null || npcTravelRoot == null) return;
            OfflineNpcTravelProgress state = host.NpcTravel.State;
            for (int positionId = 1; positionId <= 4; positionId++)
            {
                OfflineNpcTravelPosition position = state.Positions.FirstOrDefault(value => value.PositionId == positionId);
                if (position == null) continue;
                NpcTravelRow offer = host.NpcTravelCatalog.FindTravel(position.TravelId);
                if (offer == null) continue;
                string groupPath = MapPositionPath(positionId);
                npcTravelMapRenderer.SetActive(groupPath, true);
                for (int nodeId = 1; nodeId <= 3; nodeId++)
                    npcTravelMapRenderer.SetActive(groupPath + "/TravelItem (" + nodeId + ")", nodeId == offer.node_id);

                string card = groupPath + "/TravelItem (" + offer.node_id + ")";
                OfflineNpcTravelTarget target = state.Targets.FirstOrDefault(value =>
                    value.PositionId == positionId && !value.Received);
                NpcTravelQuality quality = host.NpcTravelCatalog.Quality(offer.quality);
                npcTravelMapRenderer.SetText(card + "/BgImgGo/BgImg/TitleText",
                    catalog.LocalizedText("npc_adventure_travel_name_" + offer.travel_id, "旅行委托"));
                npcTravelMapRenderer.SetActive(card + "/BgImgGo/BgImg/BaseRewardTypeText", target == null);
                npcTravelMapRenderer.SetActive(card + "/BgImgGo/BgImg/NormalRewardTypeText",
                    target != null && target.ExtraRewardType == 1);
                npcTravelMapRenderer.SetActive(card + "/BgImgGo/BgImg/SuperlRewardTypeText",
                    target != null && target.ExtraRewardType == 2);
                npcTravelMapRenderer.SetActive(card + "/BgImgGo/BgImg/CountdownGo", target != null);
                npcTravelMapRenderer.SetText(card + "/BgImgGo/BgImg/CountdownGo/Countdown/CountText",
                    target == null ? string.Empty : TravelRemaining(target, DateTime.Now));
                if (quality != null)
                    npcTravelMapRenderer.SetText(card + "/BgImgGo/BgImg/ProbabilityText",
                        target == null ? string.Empty : (target.ExtraProbability / 100.0).ToString("0.##") + "%");
            }
            npcTravelPageRenderer.SetTextByName("BattleLimitTipText",
                "今日剩余派遣次数 " + Math.Max(0, host.NpcTravelCatalog.Data.daily_start_limit
                    - state.DailyStartedCount) + "/" + host.NpcTravelCatalog.Data.daily_start_limit);
        }

        private void OpenNpcTravelDetail(int positionId)
        {
            OfflineNpcTravelPosition position = host.NpcTravel.State.Positions
                .FirstOrDefault(value => value.PositionId == positionId);
            if (position == null) return;
            selectedTravel = host.NpcTravelCatalog.FindTravel(position.TravelId);
            if (selectedTravel == null) return;
            OfflineNpcTravelTarget target = ActiveTarget(positionId);
            selectedTravelNpcIds.Clear();
            if (target != null) selectedTravelNpcIds.AddRange(target.NpcIds);
            if (npcTravelDialogRoot != null) UnityEngine.Object.Destroy(npcTravelDialogRoot);
            npcTravelDialogRenderer = Renderer();
            npcTravelDialogRoot = npcTravelDialogRenderer.Create("npc_travel", canvas.transform,
                BindNpcTravelDetailButton);
            npcTravelDialogRoot.name = "NpcAdventureTravelDialog";
            npcTravelDialogRoot.transform.SetAsLastSibling();
            RefreshNpcTravelDetail(true);
        }

        private void BindNpcTravelDetailButton(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.EndsWith("/StartTravelBtn", StringComparison.Ordinal))
                button.onClick.AddListener(OnNpcTravelPrimaryAction);
            else if (path.EndsWith("/QuickSelectBtn", StringComparison.Ordinal))
                button.onClick.AddListener(QuickSelectTravelPartners);
            else if (path.EndsWith("/ReturnBtn", StringComparison.Ordinal))
                button.onClick.AddListener(ShowNpcTravelRecallConfirm);
            else if (path.EndsWith("/LastBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => OpenAdjacentNpcTravel(-1));
            else if (path.EndsWith("/NextBtn", StringComparison.Ordinal))
                button.onClick.AddListener(() => OpenAdjacentNpcTravel(1));
            else if (path.EndsWith("/PublicPlayerIcon", StringComparison.Ordinal))
                button.onClick.AddListener(OpenNpcTravelSelect);
        }

        private void RefreshNpcTravelDetail(bool rebuildConditions = false)
        {
            if (npcTravelDialogRenderer == null || selectedTravel == null) return;
            NpcTravelQuality quality = host.NpcTravelCatalog.Quality(selectedTravel.quality);
            OfflineNpcTravelTarget target = ActiveTarget(selectedTravel.position_id);
            string root = "/NpcAdventureTravelDialog/BgDialog1/Content";
            npcTravelDialogRenderer.SetText(root + "/TopGo/Top/TitleText",
                catalog.LocalizedText("npc_adventure_travel_name_" + selectedTravel.travel_id, "旅行委托"));
            npcTravelDialogRenderer.SetText(root + "/TopGo/Top/DescText",
                catalog.LocalizedText("npc_adventure_travel_desc_" + selectedTravel.travel_id, string.Empty));
            npcTravelDialogRenderer.SetText(root + "/TopGo/Top/Countdown/DurationText",
                quality == null ? string.Empty : TimeSpan.FromMinutes(quality.duration).ToString("h'h 'm'm'"));
            npcTravelDialogRenderer.SetText(root + "/TopGo/Top/Countdown/CountText",
                target == null ? string.Empty : TravelRemaining(target, DateTime.Now));
            int extraBasisPoints = target == null ? CalculateTravelExtraProbability() : target.ExtraProbability;
            double extraPercent = extraBasisPoints / 100.0;
            int normalThreshold = host.NpcTravelCatalog.Data.extra_normal_threshold;
            int superThreshold = host.NpcTravelCatalog.Data.extra_super_threshold;
            bool super = extraPercent > normalThreshold;
            double normalPercent = super ? 0 : Math.Max(0, extraPercent);
            double superPercent = super ? Math.Clamp(extraPercent - normalThreshold, 0, superThreshold - normalThreshold) : 0;
            npcTravelDialogRenderer.SetText(root + "/TopGo/Reward/ExtraRewardGo/NormalRewardTypeText",
                normalPercent.ToString("0.##") + "%");
            npcTravelDialogRenderer.SetText(root + "/TopGo/Reward/ExtraRewardGo/SuperlRewardTypeText",
                superPercent.ToString("0.##") + "%");
            npcTravelDialogRenderer.SetActive(root + "/TopGo/Reward/ExtraRewardGo/NormalRewardTypeText", !super);
            npcTravelDialogRenderer.SetActive(root + "/TopGo/Reward/ExtraRewardGo/Image", !super);
            npcTravelDialogRenderer.SetActive(root + "/TopGo/Reward/ExtraRewardGo/TopBg", !super);
            npcTravelDialogRenderer.SetActive(root + "/TopGo/Reward/ExtraRewardGo/SuperlRewardTypeText", super);
            npcTravelDialogRenderer.SetActive(root + "/TopGo/Reward/ExtraRewardGo/SuperImage", super);
            npcTravelDialogRenderer.SetActive(root + "/TopGo/Reward/ExtraRewardGo/SuperTopBg", super);
            double baseAdd = (quality?.base_reward_add ?? 0)
                + AggregateNpcTravelProperty(host.NpcTravelCatalog.Data.travel_npc_prop_types[1]);
            npcTravelDialogRenderer.SetText(root + "/TopGo/Reward/BaseRewardGo/RewardTipText",
                (1.0 + baseAdd / 10000.0).ToString("P0"));
            long nodeAdd = host.NpcTravelCatalog.Node(npcTravelMapGroupId, npcTravelRegionId,
                selectedTravel.node_id)?.base_add_count ?? 0;
            long baseReward = target == null
                ? OfflineNpcTravelService.CalcBaseReward(host.NpcTravelCatalog.Data.base_reward_item_count,
                    nodeAdd, CurrentNpcTravelTreasureProperty(host.NpcTravelCatalog.Data.travel_treasure_prop_types[1]),
                    quality?.base_reward_add ?? 0,
                    AggregateNpcTravelProperty(host.NpcTravelCatalog.Data.travel_npc_prop_types[1]))
                : target.BaseReward;
            npcTravelDialogRenderer.SetText(root + "/TopGo/Reward/BaseRewardGo/ItemCountGo/CountText",
                baseReward.ToString("N0"));
            EnsureNpcTravelBaseRewardIcon(root);
            if (rebuildConditions) RebuildNpcTravelExtraRewardIcons(root, target);
            if (rebuildConditions) RebuildNpcTravelConditions(root);
            for (int index = 0; index < 5; index++)
            {
                string slot = root + "/TopGo/PlayerSlot/NpcIconGo" + (index == 0 ? string.Empty : " (" + index + ")");
                int npcId = index < selectedTravelNpcIds.Count ? selectedTravelNpcIds[index] : 0;
                npcTravelDialogRenderer.SetActive(slot, index < (quality?.npc_count ?? 0));
                npcTravelDialogRenderer.SetActive(slot + "/Add", npcId <= 0);
                npcTravelDialogRenderer.SetActive(slot + "/PublicPlayerIcon", true);
                npcTravelDialogRenderer.SetActive(slot + "/PublicPlayerIcon/Name", npcId > 0);
                npcTravelDialogRenderer.SetActive(slot + "/PublicPlayerIcon/PropValue", npcId > 0);
                if (npcId <= 0)
                {
                    npcTravelDialogRenderer.SetActive(slot + "/PublicPlayerIcon/IconGo", false);
                    continue;
                }
                npcTravelDialogRenderer.SetActive(slot + "/PublicPlayerIcon/IconGo", true);
                NpcTravelNpc npc = host.NpcTravelCatalog.Npc(npcId);
                string icon = NpcTravelIconKey(npc);
                Sprite sprite = catalog.FindDynamicSprite(icon);
                if (sprite == null) sprite = catalog.FindDynamicSprite(NpcTravelIconKey(npc, false));
                npcTravelDialogRenderer.SetActive(slot + "/PublicPlayerIcon", true);
                npcTravelDialogRenderer.SetSprite(slot + "/PublicPlayerIcon/IconGo/ImgMask/PlayerHeadGo/HeadImg", sprite);
                npcTravelDialogRenderer.SetText(slot + "/PublicPlayerIcon/Name",
                    catalog.LocalizedText(npc == null ? null : npc.name_key, ""));
                IReadOnlyDictionary<int, long> props = host.NpcTravelNpcProperties(npcId);
                props.TryGetValue(host.NpcTravelCatalog.Data.travel_npc_prop_types[2], out long bonus);
                npcTravelDialogRenderer.SetText(slot + "/PublicPlayerIcon/PropValue", bonus == 0 ? "" : "+" + (bonus / 100.0).ToString("0.##") + "%");
            }
            npcTravelDialogRenderer.SetActive(root + "/DialogButtonGroup/ButtonsGo/ReturnBtn", target != null);
            npcTravelDialogRenderer.SetActive(root + "/DialogButtonGroup/ButtonsGo/QuickSelectBtn", target == null);
            npcTravelDialogRenderer.SetText(root + "/DialogButtonGroup/ButtonsGo/StartTravelBtn/Content/Text",
                target == null ? "开始委托" : OfflineNpcTravelService.IsComplete(target, DateTime.Now) ? "领取奖励" : "旅行中");
            npcTravelDialogRenderer.SetActive(root + "/DialogButtonGroup/ButtonsGo/StartTravelBtn",
                target == null || OfflineNpcTravelService.IsComplete(target, DateTime.Now));
        }

        private void OpenNpcTravelSelect()
        {
            if (selectedTravel == null || ActiveTarget(selectedTravel.position_id) != null) return;
            if (npcTravelSelectRoot != null) UnityEngine.Object.Destroy(npcTravelSelectRoot);
            npcTravelSelectRenderer = Renderer();
            npcTravelSelectRoot = npcTravelSelectRenderer.Create("npc_travel_select", canvas.transform,
                BindNpcTravelSelectButton);
            npcTravelSelectRoot.name = "NpcAdventureSelectNpcDialog";
            npcTravelSelectRoot.transform.SetAsLastSibling();
            string scrollPath = "/NpcAdventureSelectNpcDialog/BgDialog1/Content/ScrollRect";
            Transform viewport = npcTravelSelectRenderer.Find(scrollPath + "/Viewport")?.transform;
            Transform scroll = npcTravelSelectRenderer.Find(scrollPath)?.transform;
            if (viewport == null || scroll == null) throw new InvalidOperationException("Official travel partner list is incomplete.");
            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200f, 160f);
            grid.spacing = new Vector2(10f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport as RectTransform;
            scrollRect.content = content;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            OfflineNpcTravelProgress travelState = host.NpcTravel.State;
            HashSet<int> travelingNpcIds = new HashSet<int>(travelState.Targets.Where(value => !value.Received
                && !OfflineNpcTravelService.IsComplete(value, DateTime.Now)).SelectMany(value => value.NpcIds));
            int[] metNpcIds = host.Profile.Save.Player.CharacterProperties.NpcFriendships
                .Where(value => value != null)
                .Select(value => host.NpcTravelCatalog.Npc(value.NpcClassId))
                .Where(value => value != null && value.can_develop && value.travel_profession > 0)
                .Select(value => value.id).Distinct()
                .OrderBy(id => NpcTravelSelectionOrder(id, travelingNpcIds)).ThenBy(id => id).ToArray();
            foreach (int npcId in metNpcIds)
            {
                NpcTravelNpc npc = host.NpcTravelCatalog.Npc(npcId);
                Sprite sprite = catalog.FindDynamicSprite(NpcTravelIconKey(npc));
                if (sprite == null) sprite = catalog.FindDynamicSprite(NpcTravelIconKey(npc, false));
                HomeUiRenderer itemRenderer = Renderer();
                GameObject item = itemRenderer.Create("travel_npc", content,
                    (path, node) => BindNpcTravelPartnerItem(path, node, npcId));
                item.name = "NpcTravelPartner_" + npcId;
                itemRenderer.SetSprite("/NpcAdventureTravelSelectNpcItem/Mask/IconImg", sprite);
                itemRenderer.SetActive("/NpcAdventureTravelSelectNpcItem/SelectedGo",
                    selectedTravelNpcIds.Contains(npcId));
                bool traveling = travelingNpcIds.Contains(npcId);
                bool resting = !traveling && travelState.NpcDailyCounts.Any(value => value.NpcId == npcId
                    && value.Count >= host.NpcTravelCatalog.Data.npc_daily_limit);
                itemRenderer.SetActive("/NpcAdventureTravelSelectNpcItem/TravelingGo", traveling);
                itemRenderer.SetActive("/NpcAdventureTravelSelectNpcItem/RestGo", resting);
                Button selection = item.GetComponent<Button>();
                if (selection != null) selection.interactable = !traveling && !resting;
                itemRenderer.SetSprite("/NpcAdventureTravelSelectNpcItem/Mask/BottomBg/Bottom/MapGroupIcon",
                    TravelCategoryIcon("map_group", npc?.map_group_id ?? 0));
                itemRenderer.SetSprite("/NpcAdventureTravelSelectNpcItem/Mask/BottomBg/Bottom/ProfessionIcon",
                    TravelCategoryIcon("profession", npc?.travel_profession ?? 0));
                itemRenderer.SetSprite("/NpcAdventureTravelSelectNpcItem/Mask/BottomBg/Bottom/PreferenceGiftIcon",
                    TravelCategoryIcon("preference", npc?.preference_type ?? 0));
            }
            if (metNpcIds.Length == 0) Notify("还没有可派遣的已结识伙伴");
        }

        private void BindNpcTravelPartnerItem(string path, GameObject node, int npcId)
        {
            if (!path.EndsWith("NpcAdventureTravelSelectNpcItem", StringComparison.Ordinal)) return;
            Button button = node.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => ToggleNpcTravelPartner(npcId));
        }

        private static string NpcTravelIconKey(NpcTravelNpc npc, bool preferFigureImage = true)
        {
            string configuredPath = npc == null ? string.Empty
                : preferFigureImage ? npc.figure_image : npc.icon;
            if (string.IsNullOrWhiteSpace(configuredPath)) return string.Empty;
            int separator = configuredPath.LastIndexOf('/');
            return separator >= 0 ? configuredPath.Substring(separator + 1) : configuredPath;
        }

        private void ToggleNpcTravelPartner(int npcId)
        {
            if (selectedTravelNpcIds.Contains(npcId)) selectedTravelNpcIds.Remove(npcId);
            else
            {
                NpcTravelQuality quality = host.NpcTravelCatalog.Quality(selectedTravel.quality);
                if (quality == null || selectedTravelNpcIds.Count >= quality.npc_count)
                {
                    Notify("已达到本委托伙伴人数");
                    return;
                }
                selectedTravelNpcIds.Add(npcId);
            }
            RefreshNpcTravelDetail(true);
            OpenNpcTravelSelect();
        }

        private void BindNpcTravelSelectButton(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.EndsWith("/CancelBtn", StringComparison.Ordinal))
                button.onClick.AddListener(CloseNpcTravelSelect);
            else if (path.EndsWith("/ConfirmBtn", StringComparison.Ordinal))
                button.onClick.AddListener(ConfirmNpcTravelSelect);
        }

        private void ConfirmNpcTravelSelect()
        {
            int required = host.NpcTravelCatalog.Quality(selectedTravel.quality)?.npc_count ?? 0;
            if (selectedTravelNpcIds.Count > required)
            {
                Notify("已超过本委托伙伴人数");
                return;
            }
            CloseNpcTravelSelect();
            RefreshNpcTravelDetail(true);
        }

        private void CloseNpcTravelSelect()
        {
            if (npcTravelSelectRoot != null) UnityEngine.Object.Destroy(npcTravelSelectRoot);
            npcTravelSelectRoot = null;
        }

        private void QuickSelectTravelPartners()
        {
            if (selectedTravel == null) return;
            selectedTravelNpcIds.Clear();
            selectedTravelNpcIds.AddRange(host.NpcTravelQuickSelect(selectedTravel.travel_id,
                host.NpcTravel.EligibleMetNpcIds(DateTimeOffset.Now.ToUnixTimeSeconds())));
            RefreshNpcTravelDetail(true);
        }

        private void OnNpcTravelPrimaryAction()
        {
            if (selectedTravel == null) return;
            OfflineNpcTravelTarget target = ActiveTarget(selectedTravel.position_id);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (target != null)
            {
                if (!OfflineNpcTravelService.IsComplete(target, DateTime.Now)) return;
                ProgressionResult claim = host.NpcTravel.Claim(target.TravelId, now, TravelEvent("claim"));
                if (!claim.IsApplied)
                {
                    Notify(claim.Message ?? "旅行奖励领取失败");
                    return;
                }
                UnityEngine.Object.Destroy(npcTravelDialogRoot);
                npcTravelDialogRoot = null;
                RefreshNpcTravelMap();
                Notify("旅行奖励已领取");
                return;
            }
            int required = host.NpcTravelCatalog.Quality(selectedTravel.quality)?.npc_count ?? 0;
            if (selectedTravelNpcIds.Count == 0)
            {
                OpenNpcTravelSelect();
                return;
            }
            if (selectedTravelNpcIds.Count < required)
            {
                ShowTravelConfirm("伙伴人数不足", "可选伙伴不足委托人数，仍要派遣吗？",
                    () => ConfirmTravelConditions(now));
                return;
            }
            ConfirmTravelConditions(now);
        }

        private void ConfirmTravelConditions(long now)
        {
            int matched = host.NpcTravelCatalog.MeetConditionCount(selectedTravel.travel_id, selectedTravelNpcIds);
            int total = selectedTravel.professions.Length + selectedTravel.map_groups.Length + selectedTravel.preferences.Length;
            if (matched < total)
            {
                ShowTravelConfirm("伙伴条件未满足", "当前伙伴未满足全部委托条件，仍要派遣吗？", () => DispatchNpcTravel(now));
                return;
            }
            DispatchNpcTravel(now);
        }

        private void DispatchNpcTravel(long nowUnixSeconds)
        {
            CharacterPropSnapshot propertySnapshot = host.DailyDungeonProgression.EvaluateProps(nowUnixSeconds);
            var npcProperties = new Dictionary<int, long>();
            foreach (int npcId in selectedTravelNpcIds)
                foreach (KeyValuePair<int, long> property in host.NpcTravelNpcProperties(npcId))
                    npcProperties[property.Key] = npcProperties.TryGetValue(property.Key, out long value)
                        ? checked(value + property.Value) : property.Value;
            ProgressionResult result = host.NpcTravel.Dispatch(selectedTravel.position_id, selectedTravel.travel_id,
                selectedTravel.quality, host.NpcTravelCatalog.Quality(selectedTravel.quality).duration,
                npcTravelRegionId, npcTravelMapGroupId, npcProperties, propertySnapshot.NonBattleProps,
                selectedTravelNpcIds, nowUnixSeconds, TravelEvent("dispatch"));
            if (!result.IsApplied)
            {
                Notify(result.Message ?? "伙伴旅行派遣失败");
                return;
            }
            UnityEngine.Object.Destroy(npcTravelDialogRoot);
            npcTravelDialogRoot = null;
            RefreshNpcTravelMap();
            Notify("伙伴旅行已出发");
        }

        private void ShowNpcTravelRecallConfirm()
        {
            OfflineNpcTravelTarget target = ActiveTarget(selectedTravel?.position_id ?? 0);
            if (target == null) return;
            ShowTravelConfirm("召回旅行伙伴", "召回不会获得奖励，移除旅行并恢复原委托；当日次数按当前规则退还。", () =>
            {
                ProgressionResult result = host.NpcTravel.Recall(target.TravelId,
                    DateTimeOffset.Now.ToUnixTimeSeconds(), TravelEvent("recall"));
                if (!result.IsApplied) Notify(result.Message ?? "旅行召回失败");
                else
                {
                    UnityEngine.Object.Destroy(npcTravelDialogRoot);
                    npcTravelDialogRoot = null;
                    RefreshNpcTravelMap();
                    Notify("旅行伙伴已召回");
                }
            });
        }

        private void ShowTravelConfirm(string title, string message, Action confirm)
        {
            if (npcTravelConfirmRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonConfirmEnterPanel");
            if (prefab == null) throw new InvalidOperationException("Official confirmation panel is missing.");
            npcTravelConfirmRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            npcTravelConfirmRoot.name = "NpcTravelConfirm";
            npcTravelConfirmRoot.transform.SetAsLastSibling();
            SetNamedText(npcTravelConfirmRoot.transform, "TitleText", title);
            SetNamedText(npcTravelConfirmRoot.transform, "Txt", message);
            SetNamedText(npcTravelConfirmRoot.transform, "CommonBtn_11", "取消");
            SetNamedText(npcTravelConfirmRoot.transform, "CommonBtn_12", "确认");
            BindPrefabButton(npcTravelConfirmRoot, "CommonBtn_11", CloseNpcTravelConfirm);
            BindPrefabButton(npcTravelConfirmRoot, "CommonBtn_12", () =>
            {
                CloseNpcTravelConfirm();
                confirm?.Invoke();
            });
        }

        private void CloseNpcTravelConfirm()
        {
            if (npcTravelConfirmRoot != null) UnityEngine.Object.Destroy(npcTravelConfirmRoot);
            npcTravelConfirmRoot = null;
        }

        private void OpenAdjacentNpcTravel(int direction)
        {
            if (selectedTravel == null) return;
            int next = (selectedTravel.position_id - 1 + direction + 4) % 4 + 1;
            OpenNpcTravelDetail(next);
        }

        private OfflineNpcTravelTarget ActiveTarget(int positionId) => host.NpcTravel.State.Targets
            .FirstOrDefault(value => value.PositionId == positionId && !value.Received);

        private static int TravelIndex(string value, int fallback)
        {
            Match match = TravelIndexPattern.Match(value ?? string.Empty);
            return match.Success ? int.Parse(match.Groups[1].Value) : fallback;
        }

        private static string MapPanelKey(int mapGroupId, int regionId) => "npc_map_" + mapGroupId + "_" + regionId;

        private string MapPositionPath(int positionId) =>
            "/NpcAdventureMap_" + npcTravelMapGroupId + "_" + npcTravelRegionId
            + "/BgImg/NpcAdventureMapTravelItem (" + positionId + ")";

        private Sprite CatalogTravelIcon(string key) => catalog.FindDynamicSprite(key)
            ?? catalog.FindDynamicSprite("npc_travel:" + key);

        private Sprite TravelCategoryIcon(string kind, int id) =>
            CatalogTravelIcon("travel-" + kind + "-" + id);

        private bool IsNpcTravelBlocked(int npcId, HashSet<int> travelingNpcIds) =>
            travelingNpcIds.Contains(npcId) || host.NpcTravel.State.NpcDailyCounts.Any(value => value.NpcId == npcId
                && value.Count >= host.NpcTravelCatalog.Data.npc_daily_limit);

        private int NpcTravelSelectionOrder(int npcId, HashSet<int> travelingNpcIds)
        {
            if (selectedTravelNpcIds.Contains(npcId)) return 3;
            if (travelingNpcIds.Contains(npcId)) return 1;
            if (host.NpcTravel.State.NpcDailyCounts.Any(value => value.NpcId == npcId
                    && value.Count >= host.NpcTravelCatalog.Data.npc_daily_limit)) return 2;
            return 0;
        }

        private long AggregateNpcTravelProperty(int propertyType)
        {
            long total = 0;
            foreach (int npcId in selectedTravelNpcIds)
                if (host.NpcTravelNpcProperties(npcId).TryGetValue(propertyType, out long value))
                    total = checked(total + value);
            return total;
        }

        private int CalculateTravelExtraProbability()
        {
            NpcTravelQuality quality = host.NpcTravelCatalog.Quality(selectedTravel.quality);
            NpcTravelNode node = host.NpcTravelCatalog.Node(npcTravelMapGroupId, npcTravelRegionId, selectedTravel.node_id);
            int meetCount = host.NpcTravelCatalog.MeetConditionCount(selectedTravel.travel_id, selectedTravelNpcIds);
            double probability = OfflineNpcTravelService.CalcExtraRewardProbability(
                AggregateNpcTravelProperty(host.NpcTravelCatalog.Data.travel_npc_prop_types[2]),
                CurrentNpcTravelTreasureProperty(host.NpcTravelCatalog.Data.travel_treasure_prop_types[2]),
                node?.extra_probability_add ?? 0, quality?.extra_base_add ?? 0, meetCount,
                quality?.extra_condition_add ?? 0);
            return checked((int)Math.Round(probability * 10000.0, MidpointRounding.ToEven));
        }

        private double CurrentNpcTravelTreasureProperty(int propertyType)
        {
            CharacterPropSnapshot snapshot = host.DailyDungeonProgression.EvaluateProps(
                DateTimeOffset.Now.ToUnixTimeSeconds());
            return snapshot.NonBattleProps.TryGetValue(propertyType, out long value) ? value : 0;
        }

        private void EnsureNpcTravelBaseRewardIcon(string root)
        {
            Transform slot = npcTravelDialogRenderer.Find(root + "/TopGo/Reward/BaseRewardGo/Slot")?.transform;
            if (slot == null) return;
            if (npcTravelBaseRewardIcon == null)
            {
                GameObject iconObject = new GameObject("NpcTravelBaseRewardIcon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(slot, false);
                RectTransform rect = (RectTransform)iconObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(48f, 48f);
                npcTravelBaseRewardIcon = iconObject.GetComponent<Image>();
                npcTravelBaseRewardIcon.preserveAspect = true;
                npcTravelBaseRewardIcon.raycastTarget = false;
            }
            npcTravelBaseRewardIcon.sprite = host.ItemIcon(host.NpcTravelCatalog.Data.base_reward_item_id);
        }

        private void RebuildNpcTravelConditions(string root)
        {
            foreach (GameObject item in npcTravelConditionItems)
                if (item != null) UnityEngine.Object.Destroy(item);
            npcTravelConditionItems.Clear();
            Transform slot = npcTravelDialogRenderer.Find(root + "/TopGo/Tip/ConditionGo")?.transform;
            if (slot == null) return;
            AddNpcTravelConditionRows(slot, selectedTravel.map_groups, npc => npc.map_group_id, "map_group");
            AddNpcTravelConditionRows(slot, selectedTravel.professions, npc => npc.travel_profession, "profession");
            AddNpcTravelConditionRows(slot, selectedTravel.preferences, npc => npc.preference_type, "preference");
        }

        private void RebuildNpcTravelExtraRewardIcons(string root, OfflineNpcTravelTarget target)
        {
            foreach (GameObject item in npcTravelExtraRewardItems)
                if (item != null) UnityEngine.Object.Destroy(item);
            npcTravelExtraRewardItems.Clear();
            Transform slot = npcTravelDialogRenderer.Find(root + "/TopGo/Reward/ExtraRewardGo/Slot")?.transform;
            if (slot == null) return;
            IEnumerable<(int itemId, long count)> rewards = target == null
                ? (host.NpcTravelCatalog.RewardRule(selectedTravel.extra_reward_rule_id)?.item_ids
                    ?? Array.Empty<int>()).Distinct().Select(itemId => (itemId, 0L))
                : target.ExtraRewards.Select(value => (value.ItemId, value.Count));
            foreach ((int itemId, long count) in rewards.Where(value => value.itemId > 0))
            {
                GameObject reward = new GameObject("NpcTravelExtraReward_" + itemId,
                    typeof(RectTransform), typeof(Image));
                reward.transform.SetParent(slot, false);
                RectTransform iconRect = (RectTransform)reward.transform;
                iconRect.sizeDelta = new Vector2(42f, 42f);
                Image icon = reward.GetComponent<Image>();
                icon.sprite = host.ItemIcon(itemId);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                if (count > 0)
                {
                    GameObject labelObject = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                    labelObject.transform.SetParent(reward.transform, false);
                    RectTransform labelRect = (RectTransform)labelObject.transform;
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(1f, 0.35f);
                    labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                    TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
                    label.text = count.ToString("N0");
                    label.fontSize = 14f;
                    label.alignment = TextAlignmentOptions.Center;
                    label.raycastTarget = false;
                    label.color = Color.white;
                }
                npcTravelExtraRewardItems.Add(reward);
            }
        }

        private void AddNpcTravelConditionRows(Transform slot, NpcTravelRequirement[] requirements,
            Func<NpcTravelNpc, int> category, string categoryKind)
        {
            foreach (NpcTravelRequirement requirement in requirements ?? Array.Empty<NpcTravelRequirement>())
            {
                HomeUiRenderer rowRenderer = Renderer();
                GameObject item = rowRenderer.Create("travel_condition", slot, null);
                item.name = "NpcTravelCondition_" + requirement.id + "_" + npcTravelConditionItems.Count;
                rowRenderer.SetSprite("/NpcAdventureTravelConditionItem/ConditionImg",
                    TravelCategoryIcon(categoryKind, requirement.id));
                int matched = selectedTravelNpcIds.Count(npcId =>
                {
                    NpcTravelNpc npc = host.NpcTravelCatalog.Npc(npcId);
                    return npc != null && category(npc) == requirement.id;
                });
                rowRenderer.SetText("/NpcAdventureTravelConditionItem/CountText", matched + "/" + requirement.count);
                npcTravelConditionItems.Add(item);
            }
        }

        private string TravelEvent(string action) => "npc-travel-" + action + "-" +
            DateTimeOffset.Now.ToUnixTimeSeconds() + "-" + (++npcTravelEventSequence);

        private static string TravelRemaining(OfflineNpcTravelTarget target, DateTime now)
        {
            long finish = target.StartedAtUnixSeconds + (long)target.DurationMinutes * 60L;
            long current = DateTimeOffset.Now.ToUnixTimeSeconds();
            TimeSpan remaining = TimeSpan.FromSeconds(Math.Max(0, finish - current));
            return remaining.TotalSeconds <= 0 ? "可领取" :
                remaining.TotalHours >= 1 ? ((int)remaining.TotalHours).ToString("00") + ":" + remaining.Minutes.ToString("00") + ":" + remaining.Seconds.ToString("00")
                    : remaining.ToString(@"mm\:ss");
        }

        private void TickNpcTravel()
        {
            if (npcTravelRoot == null) return;
            if (Time.unscaledTime >= npcTravelNextTick)
            {
                npcTravelNextTick = Time.unscaledTime + 1f;
                DateTime now = DateTime.Now;
                int dayKey = OfflineNpcTravelService.LocalTravelDayKey(now);
                if (dayKey != npcTravelLastDayKey)
                {
                    npcTravelLastDayKey = dayKey;
                    host.NpcTravel.RefreshLocalDay(now, npcTravelMapGroupId, npcTravelRegionId, TravelEvent("refresh"));
                    RefreshNpcTravelMap();
                }
                foreach (OfflineNpcTravelTarget target in host.NpcTravel.State.Targets.Where(value => !value.Received))
                {
                    string positionPath = MapPositionPath(target.PositionId);
                    NpcTravelRow offer = host.NpcTravelCatalog.FindTravel(target.TravelId);
                    if (offer != null)
                        npcTravelMapRenderer?.SetText(positionPath + "/TravelItem (" + offer.node_id
                            + ")/BgImgGo/BgImg/CountdownGo/Countdown/CountText", TravelRemaining(target, now));
                }
                if (npcTravelDialogRoot != null) RefreshNpcTravelDetail();
            }
        }

        private void CloseNpcTravelPages()
        {
            CloseNpcTravelSelect();
            CloseNpcTravelConfirm();
            if (npcTravelDialogRoot != null) UnityEngine.Object.Destroy(npcTravelDialogRoot);
            if (npcTravelRoot != null) UnityEngine.Object.Destroy(npcTravelRoot);
            npcTravelDialogRoot = null;
            npcTravelRoot = null;
            npcTravelMapRoot = null;
            npcTravelPageRenderer = null;
            npcTravelMapRenderer = null;
            npcTravelDialogRenderer = null;
            npcTravelSelectRenderer = null;
            selectedTravel = null;
            selectedTravelNpcIds.Clear();
            selectedTab = 2;
            ApplyTabVisuals();
        }
    }
}
