using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Skills;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Skills;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private readonly Dictionary<Transform, Coroutine> cultivationAnimations = new Dictionary<Transform, Coroutine>();
        private int promoteTab;
        private bool promoteGroupMode;
        private Dictionary<string, RectTransform> promoteNodes;
        private Dictionary<string, Button> promoteButtons;

        private void BindOriginalCultivationPanel(int skillId, int rank, int? selectedTab, bool? equippedMode,
            Dictionary<string, RectTransform> windowNodes, Dictionary<string, Button> windowButtons)
        {
            var root = AuxObject(AuxObject(skillUiTemplates["skill_promote"])["root"]);
            var binding = FindOriginalComponent(root, "SkillPromotePanel");
            var definition = progression.SkillDefinition(skillId);
            var planState = progression.Player.Skills.Plans[plan];
            int[] equipped = definition.ItemType == ActiveSkillItemType ? planState.ActiveSkills : planState.PassiveSkills;
            int slot = Array.IndexOf(equipped, skillId);
            promoteGroupMode = equippedMode ?? slot >= 0;
            promoteTab = !promoteGroupMode ? 1 : selectedTab ?? 0;
            LocalizeOriginalCultivation(root, windowNodes);
            RectTransform Bound(string field) => windowNodes[Convert.ToString(binding[field])];
            SetOfficialState("skill_promote", "SkillPromotePanel", "ContentStateSwitcher", promoteTab, windowNodes);
            SetOfficialState("skill_promote", "SkillPromotePanel", "IsNoTabSwitcher", promoteGroupMode ? 1 : 0, windowNodes);
            SetOfficialState("skill_promote", "SkillPromotePanel", "SkillListShowStateSwitcher", promoteGroupMode ? 1 : 0, windowNodes);
            SetOfficialState("skill_promote", "SkillPromotePanel", "mPopTipStates", 0, windowNodes);
            var title = FindOriginalComponent(root, "PanelTitle");
            SetAuxiliaryText(windowNodes[Convert.ToString(title["TitleTxt"])],
                OriginalDetailText("ui_skill_promote_panel_title_" + (promoteTab == 0 ? "LevelUp" : "RankUp")));
            SetAuxiliaryText(Bound("SkillNameText"), SkillName(skillId));
            var iconSlot = Bound("ItemIconSlot");
            RectTransform icon = iconSlot.GetChild(0) as RectTransform;
            string iconPath = windowNodes.First(pair => pair.Value == icon).Key;
            var iconNodes = windowNodes.Where(pair => pair.Key == iconPath || pair.Key.StartsWith(iconPath + "/", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            BindOriginalSkillIcon(skillId, icon, iconNodes, iconLayout: FindLayoutNode(root, iconPath));
            FindDeep(icon, "SkillStarInfo")?.gameObject.SetActive(false);
            DialogButton(windowButtons, "CloseBtnDark")?.onClick.AddListener(ClosePromote);
            foreach (object raw in (List<object>)binding["TabDatas"])
            {
                var tab = AuxObject(raw);
                string path = Convert.ToString(tab["TabItem"]);
                int type = AuxInt(tab, "TabType");
                windowButtons[path].onClick.AddListener(() => OpenPromote(skillId, type, promoteGroupMode));
                PlayOriginalCultivationTab(root, path, type == promoteTab, windowNodes);
            }
            int level = slot < 0 ? OriginalMinimumSkillLevel() : progression.SkillProgression.SlotLevel(progression.Player.Skills, definition.ItemType, slot,
                progression.SkillContext().CurrentSeasonId);
            BindOriginalPromoteProperties(skillId, rank, level, windowNodes);
            BindOriginalCultivationRank(root, skillId, rank, windowNodes, windowButtons);
            BindOriginalCultivationLevel(root, definition.ItemType, slot, level, windowNodes, windowButtons);
            if (promoteGroupMode) BindOriginalCultivationGroup(root, binding, definition.ItemType, equipped, windowNodes, windowButtons);
            else BindOriginalCultivationList(root, binding, definition.ItemType, equipped, windowNodes);
        }

        private void PlayOriginalCultivationTab(Dictionary<string, object> root, string path, bool selected,
            Dictionary<string, RectTransform> windowNodes)
        {
            var source = FindLayoutNode(root, path);
            if (source == null || !source.TryGetValue("animations", out object raw)) return;
            var clip = ((List<object>)raw).Select(AuxObject).FirstOrDefault(value =>
                Convert.ToString(value["name"]).EndsWith(selected ? "_On" : "_Off", StringComparison.Ordinal));
            if (clip != null) PlayOriginalCultivationClip(source, clip, windowNodes, false);
        }

        private void PlayOriginalCultivationClip(Dictionary<string, object> source, Dictionary<string, object> clip,
            Dictionary<string, RectTransform> windowNodes, bool loop)
        {
            var owner = windowNodes[Convert.ToString(source["path"])];
            if (cultivationAnimations.TryGetValue(owner, out Coroutine previous) && previous != null) StopCoroutine(previous);
            if (TryBuildAnimationTracks(clip, windowNodes, out var tracks, out float duration))
                cultivationAnimations[owner] = StartCoroutine(PlaySkillAnimation(tracks, duration, loop));
        }

        private void StopOriginalCultivationAnimations(Transform window)
        {
            foreach (var pair in cultivationAnimations.Where(pair => pair.Key == null || pair.Key.IsChildOf(window)).ToArray())
            {
                if (pair.Value != null) StopCoroutine(pair.Value);
                cultivationAnimations.Remove(pair.Key);
            }
        }

        private int OriginalMinimumSkillLevel()
        {
            return new[] { ActiveSkillItemType, PassiveSkillItemType }.SelectMany(type => Enumerable.Range(0, 4)
                .Select(index => progression.SkillProgression.SlotLevel(progression.Player.Skills, type, index,
                    progression.SkillContext().CurrentSeasonId))).Min();
        }

        private void BindOriginalCultivationList(Dictionary<string, object> root, Dictionary<string, object> binding,
            int itemType, int[] equipped, Dictionary<string, RectTransform> windowNodes)
        {
            var context = progression.SkillContext();
            var ids = progression.Player.Skills.Skills.Where(value => progression.SkillDefinition(value.ClassId).ItemType == itemType
                && (CanPromoteSkill(value.ClassId) || IsAvailableForCurrentProfession(value.ClassId, context) && !equipped.Contains(value.ClassId)))
                .Select(value => value.ClassId).ToList();
            ids.Sort((left, right) => {
                int value = CanPromoteSkill(right).CompareTo(CanPromoteSkill(left));
                if (value != 0) return value;
                value = IsAvailableForCurrentProfession(right, context).CompareTo(IsAvailableForCurrentProfession(left, context));
                if (value != 0) return value;
                value = OfflineSkillProgression.OwnedRank(progression.Player.Skills, right).CompareTo(OfflineSkillProgression.OwnedRank(progression.Player.Skills, left));
                if (value != 0) return value;
                value = OriginalSkillProfessionScore(right).CompareTo(OriginalSkillProfessionScore(left));
                return value != 0 ? value : left.CompareTo(right);
            });
            bool centered = ids.Count < AuxInt(binding, "mShowThreshold");
            var center = windowNodes[Convert.ToString(binding["mCenterGridTransform"])];
            var leftGrid = windowNodes[Convert.ToString(binding["mLeftGridTransform"])];
            center.gameObject.SetActive(centered); leftGrid.gameObject.SetActive(!centered);
            var mount = centered ? center : leftGrid;
            var template = AuxObject(skillUiTemplates["skill_promote_item"]);
            var rowBinding = FindOriginalComponent(AuxObject(template["root"]), "SkillPromotePanelItem");
            var scrollNode = windowNodes[Convert.ToString(binding["SkillScrollList"])];
            var scroll = scrollNode.GetComponent<LoopHorizontalScrollRect>();
            if (scroll == null)
            {
                scroll = scrollNode.gameObject.AddComponent<LoopHorizontalScrollRect>();
                var pool = new GameObject("Pool", typeof(RectTransform));
                pool.transform.SetParent(scrollNode, false); pool.SetActive(false);
                cultivationScrollSource = new CultivationScrollSource(pool.transform);
                ((LoopScrollRectBase)scroll).prefabSource = cultivationScrollSource;
                scroll.dataSource = cultivationScrollSource;
            }
            else scroll.ClearCells();
            var scrollFields = new Dictionary<string, object>(FindOriginalComponent(root, "LoopHorizontalScrollRect"));
            // The original prefab reference is materialized by the explicit local
            // template source, rather than a scene node at /SkillPromotePanelItem.
            scrollFields.Remove("DefaultPrefabSource");
            scrollFields.Remove("m_OnValueChanged");
            OfficialSkillComponentBindings.Apply(scroll, scrollFields, windowNodes);
            scroll.content = mount;
            cultivationScrollSource.Create = () => {
                var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), cultivationScrollSource.Pool as RectTransform, rowNodes, rowButtons);
                cultivationScrollSource.Nodes[row] = rowNodes;
                return row.gameObject;
            };
            cultivationScrollSource.Bind = (row, index) => {
                int id = ids[index];
                var rowNodes = cultivationScrollSource.Nodes[row];
                var slotMount = rowNodes[Convert.ToString(rowBinding["IconSlot"])];
                foreach (Transform child in slotMount) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                var icon = BuildSkillIcon(id, rowNodes[Convert.ToString(rowBinding["IconSlot"])], "SkillIcon");
                if (CanPromoteSkill(id)) AddOriginalCultivationAttachment(icon, 8);
                if (!IsAvailableForCurrentProfession(id, context)) AddOriginalCultivationAttachment(icon, 10);
                Transform selected = FindDeep(icon.transform, "SelAnimGo");
                if (selected != null) selected.gameObject.SetActive(id == promoteSkillId);
                var image = icon.GetComponent<Image>() ?? icon.AddComponent<Image>();
                image.color = Color.clear; image.raycastTarget = true; image.canvasRenderer.cullTransparentMesh = false;
                var button = icon.GetComponent<Button>() ?? icon.AddComponent<Button>();
                button.enabled = true; button.targetGraphic = image;
                button.onClick.AddListener(() => BindOriginalCultivationSelection(id, windowNodes));
                var count = FindDeep(icon.transform, "BgCountText");
                count.gameObject.SetActive(true);
                SetAuxiliaryText(FindDeep(count, "CountText").GetComponent<RectTransform>(), string.Format(CultureInfo.InvariantCulture,
                    OriginalDetailText("ui_common_level_style_3"), OriginalMinimumSkillLevel()));
                cultivationScrollSource.Ids[row] = index;
                cultivationScrollSource.SkillIds[row] = id;
            };
            scroll.totalCount = ids.Count;
            scroll.onValueChanged.RemoveAllListeners();
            int tipIndex = -1;
            void RefreshTip()
            {
                SetOriginalComponentState(binding, "mPopTipStates", 0, windowNodes);
                int firstVisible = -1, left = -1, right = -1;
                var visible = new HashSet<int>();
                foreach (RectTransform row in mount)
                {
                    if (!cultivationScrollSource.Ids.TryGetValue(row, out int index)) continue;
                    var corners = new Vector3[4]; row.GetWorldCorners(corners);
                    if (!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,
                        RectTransformUtility.WorldToScreenPoint(null, corners[2]))) continue;
                    if (CanPromoteSkill(ids[index])) return;
                    visible.Add(index);
                    if (firstVisible < 0) firstVisible = index;
                }
                for (int i = 0; i < ids.Count; i++)
                {
                    if (visible.Contains(i) || !CanPromoteSkill(ids[i])) continue;
                    if (firstVisible >= i) { if (left < 0 || i >= left) left = i; }
                    else if (right < 0 || i <= right) right = i;
                }
                tipIndex = left >= 0 ? left : right;
                SetOriginalComponentState(binding, "mPopTipStates", left >= 0 ? 1 : right >= 0 ? 2 : 0, windowNodes);
            }
            void ScrollToTip()
            {
                if (tipIndex < 0) return;
                int target = tipIndex;
                SetOriginalComponentState(binding, "mPopTipStates", 0, windowNodes);
                scroll.ScrollToCellWithinTime(Math.Max(target - Mathf.CeilToInt(AuxInt(binding, "mShowThreshold") / 2f), 0), .25f);
                // SetItemData refreshes only the detail and selection; it does not refill the list.
                BindOriginalCultivationSelection(ids[target], windowNodes);
            }
            windowNodes[Convert.ToString(binding["mLeftPopTipBtn"])].GetComponent<Button>().onClick.AddListener(ScrollToTip);
            windowNodes[Convert.ToString(binding["mRightPopTipBtn"])].GetComponent<Button>().onClick.AddListener(ScrollToTip);
            scroll.onValueChanged.AddListener(_ => RefreshTip());
            scroll.RefillCells(centered ? 0 : Mathf.Clamp(ids.IndexOf(promoteSkillId), 0, ids.Count));
            Canvas.ForceUpdateCanvases();
            RefreshTip();
        }

        private void BindOriginalCultivationGroup(Dictionary<string, object> root, Dictionary<string, object> binding,
            int itemType, int[] equipped, Dictionary<string, RectTransform> windowNodes, Dictionary<string, Button> windowButtons)
        {
            var group = FindOriginalComponent(root, "SkillContentPanelSkillGroup");
            var paths = (List<object>)group["SkillItems"];
            for (int i = 0; i < paths.Count; i++)
            {
                int index = i;
                var source = FindLayoutNode(root, Convert.ToString(paths[i]));
                var cell = FindOriginalComponent(source, "CharacterPanelSkillItem");
                var mount = windowNodes[Convert.ToString(cell["IconSlot"])];
                foreach (Transform child in mount) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                var icon = BuildSkillIcon(equipped[i], mount, "SkillIcon");
                if (equipped[i] > 0 && !IsAvailableForCurrentProfession(equipped[i], progression.SkillContext()))
                    AddOriginalCultivationAttachment(icon, 10);
                if (promoteTab == 1 && equipped[i] > 0 && (CanPromoteSkill(equipped[i]) || !IsAvailableForCurrentProfession(equipped[i], progression.SkillContext())))
                    AddOriginalCultivationAttachment(icon, 8);
                if (promoteTab == 0 && CanUpgradeOriginalCultivationSlot(itemType, i)) AddOriginalCultivationAttachment(icon, 14);
                int cellLevel = progression.SkillProgression.SlotLevel(progression.Player.Skills, itemType, i, progression.SkillContext().CurrentSeasonId);
                SetAuxiliaryText(windowNodes[Convert.ToString(cell["LevelText"])],
                    string.Format(CultureInfo.InvariantCulture, OriginalDetailText("ui_common_level_style_3"), cellLevel));
                RefreshOriginalBless(cell, "CurLevelTextView", windowNodes, cellLevel > OriginalSlotBaseLimit());
                Transform selection = FindDeep(windowNodes[Convert.ToString(paths[i])], "SelAnimGo");
                if (selection != null) selection.gameObject.SetActive(equipped[i] == promoteSkillId);
                if (windowButtons.TryGetValue(Convert.ToString(cell["IconSlot"]), out Button button))
                    button.onClick.AddListener(() => { if (equipped[index] > 0) OpenPromote(equipped[index], promoteTab); });
            }
            var state = progression.Player.Skills.Plans[plan];
            foreach (var direction in new[] { ("PrevBtn", itemType - 1), ("NextBtn", itemType + 1) })
            {
                int[] slots = direction.Item2 == ActiveSkillItemType ? state.ActiveSkills : direction.Item2 == PassiveSkillItemType ? state.PassiveSkills : Array.Empty<int>();
                int adjacent = slots.FirstOrDefault(value => value > 0);
                string path = Convert.ToString(binding[direction.Item1]);
                windowNodes[path].gameObject.SetActive(adjacent > 0);
                windowButtons[path].onClick.AddListener(() => { if (adjacent > 0) OpenPromote(adjacent, promoteTab); });
            }
        }

        private void BindOriginalCultivationRank(Dictionary<string, object> root, int skillId, int rank,
            Dictionary<string, RectTransform> windowNodes, Dictionary<string, Button> windowButtons)
        {
            var binding = FindOriginalComponent(root, "SkillPromotePanelRankUpContent");
            var definition = progression.SkillDefinition(skillId);
            var current = progression.SkillRules.RankDisplays.First(value => value.Rank == rank);
            bool maximum = rank >= definition.MaximumRank;
            var next = progression.SkillRules.RankDisplays.First(value => value.Rank == (maximum ? rank : rank + 1));
            BindOriginalQualityText(windowNodes[Convert.ToString(binding["myCurRankText"])], current.Quality, 0f);
            BindOriginalQualityText(windowNodes[Convert.ToString(binding["myNextRankText"])], next.Quality, 45f);
            SetOriginalComponentState(binding, "ShowNextStarStateSwitcher", !maximum && current.Quality != next.Quality ? 1 : 0, windowNodes);
            BindOriginalCultivationStars(root, Convert.ToString(binding["SkillStarInfo"]), windowNodes,
                !maximum && current.Quality == next.Quality ? rank + 1 : rank, false, true,
                !maximum && current.Quality == next.Quality ? next.RankAddition : 0);
            var cost = definition.RankCosts.FirstOrDefault(value => value.CurrentRank == rank);
            BindOriginalCultivationCost(root, Convert.ToString(binding["UseItemContent"]), maximum ? 2 : 0,
                cost?.Items ?? Array.Empty<OfflineSkillItemCount>(), windowNodes, windowButtons, DoPromote);
        }

        private void BindOriginalCultivationLevel(Dictionary<string, object> root, int itemType, int slot, int level,
            Dictionary<string, RectTransform> windowNodes, Dictionary<string, Button> windowButtons)
        {
            var binding = FindOriginalComponent(root, "SkillPromotePanelLevelUpContent");
            var context = progression.SkillContext();
            int rank = context.SubRank < 2 ? context.SubRank : 2 + (context.SubRank - 2) / 3;
            int baseLimit = progression.SkillRules.BaseLevelLimits.FirstOrDefault(value => value.SubRank == rank)?.SkillLevel ?? 0;
            var costs = level + 1 <= baseLimit ? progression.SkillRules.SlotLevelCosts.FirstOrDefault(value => value.Level == level + 1)?.Items
                : progression.SkillRules.SlotBlessCosts.FirstOrDefault(value => value.Rank == context.CurrentSeasonRank && value.BlessLevel == level + 1 - baseLimit)?.Items;
            var limit = progression.SkillRules.LevelLimits.FirstOrDefault(value => value.SubRank == context.SubRank
                && value.PlayerLevel == context.BaseLevel && value.PlayerBlessLevel == context.PlayerBlessLevel);
            bool playerLimited = limit == null || level + 1 > limit.SkillLevel + (context.PlayerRank == context.CurrentSeasonRank ? limit.SkillBlessLevel : 0);
            int minimum = 0; bool otherSlots = false;
            bool blessingLimited = level + 1 > baseLimit && !progression.SkillProgression.CheckBlessLevelCanUpgrade(
                progression.Player.Skills, context, level + 1, out minimum, out otherSlots);
            int nextSeason = progression.SkillProgression.NextSeasonLevelLimit(context, level + 1);
            bool limited = slot < 0 || nextSeason >= 0 || playerLimited || blessingLimited;
            var nextLimit = playerLimited ? progression.SkillProgression.NextSkillLevelLimit(context, level) : null;
            bool maximum = nextSeason < 0 && playerLimited && nextLimit == null;
            string limitText = nextSeason >= 0
                ? string.Format(CultureInfo.InvariantCulture, OriginalDetailText("function_limit_season"), OriginalDetailText("astrological_season_" + nextSeason + "_name"))
                : playerLimited && nextLimit != null
                ? string.Format(CultureInfo.InvariantCulture, OriginalDetailText("function_limit_level"), nextLimit.PlayerLevel + nextLimit.PlayerBlessLevel)
                : otherSlots ? OriginalDetailText("ui_blessing_player_skill_level_tip")
                : minimum > 0 ? string.Format(CultureInfo.InvariantCulture, OriginalDetailText("ui_blessing_player_skill_level_minlevel_limit"), minimum) : "";
            SetAuxiliaryText(windowNodes[Convert.ToString(binding["LevelLimitText"])], limitText);
            SetOriginalComponentState(binding, "LevelUpStateSwitcher", maximum ? 2 : limited ? 1 : 0, windowNodes);
            SetOriginalComponentState(binding, "LevelStateSwitcher", maximum ? 1 : 0, windowNodes);
            foreach (var field in new[] { ("CurrentLevelText", level), ("NextLevelText", level + 1), ("MaxLevelText", level) })
                SetAuxiliaryText(windowNodes[Convert.ToString(binding[field.Item1])],
                    string.Format(CultureInfo.InvariantCulture, OriginalDetailText("ui_common_level_style_3"), field.Item2));
            RefreshOriginalBless(binding, "CurLevelTextView", windowNodes, level > baseLimit);
            RefreshOriginalBless(binding, "NextLevelTextView", windowNodes, level + 1 > baseLimit);
            BindOriginalCultivationCost(root, Convert.ToString(binding["UseItemContent"]), maximum ? 2 : limited ? 1 : 0,
                costs ?? Array.Empty<OfflineSkillItemCount>(), windowNodes, windowButtons, () => { if (slot >= 0) UpgradePromoteSlot(itemType, slot); });
        }

        private void BindOriginalCultivationCost(Dictionary<string, object> root, string path, int state,
            OfflineSkillItemCount[] costs, Dictionary<string, RectTransform> windowNodes, Dictionary<string, Button> windowButtons, Action action)
        {
            var source = FindLayoutNode(root, path);
            var binding = FindOriginalComponent(source, "PlayerUseItemContent");
            SetOriginalComponentState(binding, "UseItemStateSwitcher", state, windowNodes);
            var mount = windowNodes[Convert.ToString(binding["MaterialSlot"])];
            foreach (Transform child in mount) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var cost in costs) BuildOriginalCultivationMaterial(mount, cost);
            windowButtons[Convert.ToString(binding["UseItemBtn"])].onClick.AddListener(() => action());
        }

        private bool CanUpgradeOriginalCultivationSlot(int itemType, int slot)
        {
            var copy = progression.Player.Skills.Clone();
            copy.Items.Clear();
            foreach (var item in progression.Player.Inventory)
                copy.Items.Add(new OfflineSkillItemCount { ItemId = item.ItemId, Count = item.Count });
            return progression.SkillProgression.UpgradeSlot(copy, progression.SkillContext(), itemType, slot).IsApplied;
        }

        private void AddOriginalCultivationAttachment(GameObject icon, int kind)
        {
            var template = AuxObject(skillUiTemplates["skill_attachment_" + kind.ToString(CultureInfo.InvariantCulture)]);
            var attachmentNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var attachmentButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            var instance = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), FindDeep(icon.transform, "ItemIconAttachment"), attachmentNodes, attachmentButtons);
            instance.name = "OriginalSkillAttachment_" + kind.ToString(CultureInfo.InvariantCulture);
            instance.gameObject.SetActive(true);
            LocalizeOriginalCultivation(AuxObject(template["root"]), attachmentNodes);
            foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }

        private Dictionary<string, object> OriginalCultivationItem(int itemId)
        {
            var item = progression.Catalog.FindItem(itemId);
            if (item != null) return item;
            var items = AuxObject(skillUiTemplates["cultivation_items"]);
            return items.TryGetValue(itemId.ToString(CultureInfo.InvariantCulture), out object raw) ? AuxObject(raw) : null;
        }

        private string OriginalCultivationItemIcon(int itemId)
        {
            var icons = AuxObject(skillUiTemplates["cultivation_item_icons"]);
            return icons.TryGetValue(itemId.ToString(CultureInfo.InvariantCulture), out object raw)
                ? Convert.ToString(raw) : progression.Catalog.ItemIconUri(itemId);
        }

        private void BuildOriginalCultivationMaterial(RectTransform parent, OfflineSkillItemCount cost)
        {
            bool piece = progression.Catalog.ItemType(cost.ItemId) == PieceItemType;
            var template = AuxObject(skillUiTemplates[piece ? "item_skill_piece" : "item"]);
            var itemNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var itemButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            var root = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), parent, itemNodes, itemButtons);
            var source = AuxObject(template["root"]);
            string uri = OriginalCultivationItemIcon(cost.ItemId);
            Sprite sprite = assets.FindSprite(uri) ?? skillAssets.FindSprite(uri);
            if (sprite == null) throw new InvalidOperationException("Original cultivation material icon is absent: " + cost.ItemId);
            FindDeep(root, "icon").GetComponent<Image>().sprite = sprite;
            FindDeep(root, "FakeIcon")?.gameObject.SetActive(false);
            FindDeep(root, "BottomText")?.gameObject.SetActive(false);
            foreach (var pair in itemNodes)
                if (pair.Value.name.TrimEnd() == "BgCountText") pair.Value.gameObject.SetActive(pair.Key.Contains("/Progress/", StringComparison.Ordinal));
            FindDeep(root, "SkillStarInfo")?.gameObject.SetActive(false);
            int quality = Convert.ToInt32(OriginalCultivationItem(cost.ItemId)["Quality"]);
            FindDeep(root, "QualityImg").GetComponent<Image>().sprite = assets.FindQualityBackgroundSprite(quality);
            var progressBinding = FindOriginalComponent(source, "ItemProgress");
            var progress = FindDeep(root, "Progress");
            progress.gameObject.SetActive(true);
            SetOriginalComponentState(progressBinding, "mStateSwitcher", 0, itemNodes);
            long count = progression.Player.Inventory.Where(value => value.ItemId == cost.ItemId).Sum(value => value.Count);
            RectTransform left = itemNodes[Convert.ToString(progressBinding["mLeftCountText"])];
            RectTransform right = itemNodes[Convert.ToString(progressBinding["mRightCountText"])];
            SetAuxiliaryText(left, OfficialPropFormat.Compact(count));
            SetAuxiliaryText(right, OfficialPropFormat.Compact(cost.Count));
            left.GetComponent<Graphic>().color = OriginalDetailColor(AuxObject(skillUiTemplates["cultivation_colors"])[count >= cost.Count ? "Green" : "LightRed"]);
            right.GetComponent<Graphic>().color = OriginalDetailColor(progressBinding["mDefaultTextColor"]);
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            image.color = Color.clear; image.raycastTarget = true; image.canvasRenderer.cullTransparentMesh = false;
            var button = root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => {
                if (piece) OpenPieceDetail(progression.SkillRules.Skills.First(value => value.PieceItemId == cost.ItemId).ClassId);
                else OpenOriginalGiftItemDetail(cost.ItemId);
            });
        }

        private void BindOriginalRankNotification(int skillId, int fromRank, int toRank,
            Dictionary<string, RectTransform> resultNodes)
        {
            var root = AuxObject(AuxObject(skillUiTemplates["skill_rank_notify"])["root"]);
            var binding = FindOriginalComponent(root, "SkillRankUpNotifyDialog");
            LocalizeOriginalCultivation(root, resultNodes);
            RectTransform Bound(string field) => resultNodes[Convert.ToString(binding[field])];
            int fromQuality = progression.SkillRules.RankDisplays.First(value => value.Rank == fromRank).Quality;
            int toQuality = progression.SkillRules.RankDisplays.First(value => value.Rank == toRank).Quality;
            BindOriginalQualityText(Bound("FromRankText"), fromQuality, 0f);
            BindOriginalQualityText(Bound("ToRankText"), toQuality, 0f);
            SetOfficialState("skill_rank_notify", "SkillRankUpNotifyDialog", "StateSwitcher", toQuality, resultNodes);
            bool qualityUp = fromQuality != toQuality;
            SetOfficialState("skill_rank_notify", "SkillRankUpNotifyDialog", "IsQualityUpStateSwticher", qualityUp ? 1 : 0, resultNodes);
            BindOriginalCultivationStars(root, Convert.ToString(binding["SkillStarInfo"]), resultNodes, fromRank, !qualityUp);
            BindOriginalCultivationStars(root, Convert.ToString(binding["NextSkillStarInfo"]), resultNodes, toRank, !qualityUp);
            SetAuxiliaryText(Bound("SkillNameText"), SkillName(skillId));
            string iconPath = Convert.ToString(binding["mItemIcon"]);
            var iconNodes = resultNodes.Where(pair => pair.Key == iconPath || pair.Key.StartsWith(iconPath + "/", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            BindOriginalSkillIcon(skillId, Bound("mItemIcon"), iconNodes, toRank, iconLayout: FindLayoutNode(root, iconPath));
            var propSlot = Bound("PropSlot");
            foreach (Transform child in propSlot)
                if (child.gameObject.activeSelf) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (detailProperties == null) detailProperties = new OfficialSkillDetailProperties(progression.Catalog, skillAssets);
            var definition = progression.SkillDefinition(skillId);
            var selectedPlan = progression.Player.Skills.Plans[plan];
            int slot = Array.IndexOf(definition.ItemType == ActiveSkillItemType ? selectedPlan.ActiveSkills : selectedPlan.PassiveSkills, skillId);
            int level = slot < 0 ? OriginalMinimumSkillLevel() : progression.SkillProgression.SlotLevel(progression.Player.Skills, definition.ItemType, slot,
                progression.SkillContext().CurrentSeasonId);
            var from = detailProperties.Calculate(skillId, fromRank, Math.Max(1, level), Math.Max(1, progression.Player.SubRank));
            var to = detailProperties.Calculate(skillId, toRank, Math.Max(1, level), Math.Max(1, progression.Player.SubRank));
            var template = AuxObject(skillUiTemplates["skill_rank_notify_prop"]);
            var rowBinding = FindOriginalComponent(AuxObject(template["root"]), "PlayerPropItem");
            foreach (var value in from)
            {
                var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), propSlot, rowNodes, rowButtons);
                row.gameObject.SetActive(true);
                void Text(string field, string content) => SetAuxiliaryText(rowNodes[Convert.ToString(rowBinding[field])], content);
                Text("myPropTypeText", value.Name);
                Text("myPropValueText", value.Value);
                Text("PropValueFromText", value.Value);
                Text("PropValueToText", to.First(item => item.Name == value.Name).Value);
                rowNodes[Convert.ToString(rowBinding["myPropValueText"])].gameObject.SetActive(false);
                rowNodes[Convert.ToString(rowBinding["myPropPreviewObj"])].gameObject.SetActive(true);
            }
        }

        private void LocalizeOriginalCultivation(Dictionary<string, object> root, Dictionary<string, RectTransform> windowNodes)
        {
            foreach (var node in AuxiliaryNodes(root))
            {
                if (!node.TryGetValue("original_bindings", out object raw) || !AuxObject(raw).TryGetValue("MultiLanguageText", out object language)) continue;
                string key = Convert.ToString(AuxObject(language)["myKey"]);
                if (windowNodes.TryGetValue(Convert.ToString(node["path"]), out var target))
                    SetAuxiliaryText(target, OriginalDetailText(key) ?? key);
            }
        }

        private void BindOriginalQualityText(RectTransform node, int quality, float angle)
        {
            string[] names = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            SetAuxiliaryText(node, OriginalDetailText("Quality." + names[quality]));
            if (qualityGradient == null) qualityGradient = skillAssets.LoadQualityGradient();
            node.GetComponent<Graphic>().color = qualityGradient.normalColors[quality];
            TMP_Text tmp = node.GetComponent<TMP_Text>();
            if (tmp == null) { ApplyQualityGradient(node.GetComponent<Text>(), quality, angle); return; }
            var effect = node.GetComponent<OfficialSkillQualityTmpGradient>();
            if (quality != 7) { if (effect != null) Destroy(effect); return; }
            if (effect == null) effect = node.gameObject.AddComponent<OfficialSkillQualityTmpGradient>();
            effect.Configure(tmp, qualityGradient, angle);
        }

        private void BindOriginalCultivationStars(Dictionary<string, object> root, string path,
            Dictionary<string, RectTransform> windowNodes, int rank, bool showNumber = false, bool showBlank = false, int blinkStar = 0)
        {
            var source = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == path);
            var binding = FindOriginalComponent(source, "SkillStarInfo");
            var mini = FindOriginalComponent(source, "StarMiniInfo");
            var display = progression.SkillRules.RankDisplays.First(value => value.Rank == rank);
            bool number = showNumber && display.RankAddition > AuxInt(binding, "ShowStarThresholdValue");
            windowNodes[path].gameObject.SetActive(true);
            SetOriginalComponentState(binding, "StarStateSwitcher", number ? 1 : 0, windowNodes);
            var dictionary = AuxObject(mini["mQualitySpriteDict"]);
            var records = ((List<object>)dictionary["mSerializeBuffer"]).Select(AuxObject).ToArray();
            var record = records.FirstOrDefault(value => AuxInt(value, "Key") == display.Quality) ?? records.First(value => AuxInt(value, "Key") == 0);
            string uri = Convert.ToString(AuxObject(record["Value"])["file"]);
            Sprite sprite = skillAssets.FindSprite(uri) ?? throw new InvalidOperationException("Original cultivation star is absent: " + uri);
            if (number)
            {
                SetAuxiliaryText(windowNodes[Convert.ToString(binding["StarNum"])], display.RankAddition.ToString(CultureInfo.InvariantCulture));
                windowNodes[Convert.ToString(binding["StarNumImage"])].GetComponent<Image>().sprite = sprite;
                return;
            }
            var list = windowNodes[Convert.ToString(mini["StarListSlot"])];
            int maxRank = progression.SkillRules.RankDisplays.Where(value => value.Quality == display.Quality).Max(value => value.RankAddition);
            for (int i = 0; i < list.childCount; i++)
            {
                Transform background = list.GetChild(i);
                background.gameObject.SetActive(i < display.RankAddition || showBlank && i < maxRank);
                if (background.childCount == 0) continue;
                Image image = background.GetChild(0).GetComponent<Image>();
                image.sprite = sprite;
                image.gameObject.SetActive(i < display.RankAddition);
                string imagePath = windowNodes.First(pair => pair.Value == image.transform).Key;
                var imageSource = FindLayoutNode(root, imagePath);
                if (imageSource != null && imageSource.TryGetValue("animations", out object rawAnimations))
                {
                    var clip = ((List<object>)rawAnimations).Select(AuxObject).FirstOrDefault(value =>
                        Convert.ToString(value["name"]) == (i + 1 == blinkStar ? "Star_Twinkle_On" : "Star_Twinkle_Off"));
                    if (clip != null) PlayOriginalCultivationClip(imageSource, clip, windowNodes,
                        i + 1 == blinkStar && CharacterPanelNodeFactory.Boolean(clip, "loop", false));
                }
            }
        }
    }
}
