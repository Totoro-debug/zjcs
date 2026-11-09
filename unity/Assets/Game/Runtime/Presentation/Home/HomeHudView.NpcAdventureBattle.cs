using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.TeamUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private GameObject npcAdventureSelectRoot;
        private GameObject npcAdventureResultRoot;
        private NpcTravelNode npcAdventureSelectedNode;
        private readonly List<int> npcAdventureSelectedNpcIds = new List<int>();
        private readonly List<GameObject> npcAdventureSelectItems = new List<GameObject>();

        private bool NpcAdventureBattleUnlocked =>
            host.Profile.Save.Player.UnlockedMapClassIds?.Contains(17) == true;

        private void RefreshNpcAdventureBattleButtons()
        {
            if (npcTravelPageRenderer == null) return;
            const string root = "/NpcAdventurePanel/BgPanel1/Content/MiddleGo/MainContent/BattleGo/";
            NpcTravelNode next = host.NpcAdventure.NextNode;
            bool hasAttempts = host.NpcAdventure.RemainingBattleCount(DateTime.Now) > 0;
            bool resume = host.NpcAdventure.State.ActiveStageId > 0;
            NpcTravelNode first = host.NpcAdventure.FirstNode;
            // Confirmed offline qualification: the first stage is fought manually;
            // subsequent stages expose only the current next-stage sweep.
            bool canSweep = next != null && first != null && next.stage_id != first.stage_id && !resume;
            npcTravelPageRenderer.SetActive(root + "BattleBtn", resume || next != null && hasAttempts && !canSweep);
            npcTravelPageRenderer.SetActive(root + "FastBattleBtn", next != null && hasAttempts && canSweep);
            npcTravelPageRenderer.SetActive(root + "BattleLimitTipText", next != null && !hasAttempts && !resume);
            npcTravelPageRenderer.SetActive(root + "BattleFinishedTipText", next == null);
        }

        internal void OpenNpcAdventureBattleSelection()
        {
            if (npcAdventureResultRoot != null) CloseNpcAdventureBattleResult();
            if (!NpcAdventureBattleUnlocked)
            {
                Notify("解锁羽之国后可进行羁绊冒险");
                return;
            }
            OfflineNpcAdventureProgress progress = host.NpcAdventure.State;
            if (progress.ActiveStageId <= 0
                && host.NpcAdventure.RemainingBattleCount(DateTime.Now) <= 0)
            {
                Notify("今日羁绊冒险挑战次数已用完");
                return;
            }
            npcAdventureSelectedNode = host.NpcAdventure.NextNode;
            if (npcAdventureSelectedNode == null)
            {
                Notify("羁绊冒险节点已全部完成");
                return;
            }
            if (progress.ActiveStageId > 0 && !string.IsNullOrEmpty(progress.ActiveBattleCheckpoint))
            {
                if (progress.ActiveStageId != npcAdventureSelectedNode.stage_id)
                {
                    Notify("羁绊冒险保存的战斗关卡与进度不一致");
                    return;
                }
                StartNpcAdventureBattle(npcAdventureSelectedNode,
                    progress.SelectedNpcClassIds.ToArray());
                return;
            }
            if (npcAdventureSelectRoot != null) return;
            GameObject prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/NpcAdventureSelectNpcDialog");
            if (prefab == null) throw new InvalidOperationException("官方羁绊冒险伙伴选择窗口缺失。");
            npcAdventureSelectRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            DungeonUiBindings.Apply(npcAdventureSelectRoot, "NpcAdventureSelectNpcDialog");
            npcAdventureSelectRoot.name = "NpcAdventureSelectNpcDialog";
            npcAdventureSelectRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(npcAdventureSelectRoot);
            BindNpcAdventureFont(npcAdventureSelectRoot);
            BindPrefabButton(npcAdventureSelectRoot, "CancelBtn", CloseNpcAdventureBattleSelection);
            BindPrefabButton(npcAdventureSelectRoot, "ConfirmBtn", ConfirmNpcAdventureBattleSelection);
            SetNamedText(npcAdventureSelectRoot.transform, "TitleText", "选择助战伙伴");
            npcAdventureSelectedNpcIds.Clear();
            npcAdventureSelectedNpcIds.AddRange(progress.SelectedNpcClassIds
                .Where(id => CandidateNpcIds().Contains(id)).Take(OfflineNpcAdventureSession.MaximumCompanions));
            RefreshNpcAdventureSelectionItems();
        }

        internal void SweepNpcAdventureNextStage()
        {
            if (!NpcAdventureBattleUnlocked)
            {
                Notify("解锁羽之国后可进行羁绊冒险");
                return;
            }
            DateTime now = DateTime.Now;
            OfflineNpcAdventureProgress progress = host.NpcAdventure.State;
            if (progress.ActiveStageId > 0)
            {
                Notify("请先完成当前羁绊冒险战斗");
                return;
            }
            if (host.NpcAdventure.RemainingBattleCount(now) <= 0)
            {
                Notify("今日羁绊冒险挑战次数已用完");
                return;
            }
            NpcTravelNode node = host.NpcAdventure.NextNode;
            if (node == null)
            {
                Notify("羁绊冒险节点已全部完成");
                return;
            }

            if (node.stage_id == host.NpcAdventure.FirstNode?.stage_id)
            {
                Notify("首关需手动挑战");
                return;
            }

            string receipt = host.NpcAdventure.CreateSweepReceipt(node, now);
            if (string.IsNullOrEmpty(receipt))
            {
                Notify("只能扫荡当前下一关");
                return;
            }
            List<NpcAdventureRewardItem> rewards;
            ProgressionResult result;
            try
            {
                ulong randomState = unchecked((ulong)NpcAdventureBattleRunner.StableSeed(
                    receipt, node.stage_id));
                rewards = host.NpcAdventureBattleCatalog.RollRewards(
                    node.stage_id, host.Profile.Save.Player.Level, host.Profile.Save.Player.SubRank,
                    ref randomState);
                List<KeyValuePair<int, long>> pairs = rewards.Select(value =>
                    new KeyValuePair<int, long>(value.ItemId, value.Count)).ToList();
                result = host.NpcAdventure.SweepNextStage(node, pairs, receipt, now);
            }
            catch (Exception error)
            {
                Notify(error.Message);
                return;
            }
            if (!result.IsApplied && result.Status != ProgressionResultStatus.Unchanged)
            {
                Notify(result.Message ?? "羁绊冒险扫荡失败");
                return;
            }

            try
            {
                NpcTravelNode next = host.NpcAdventure.NextNode ?? node;
                CloseNpcTravelPages();
                OpenNpcAdventurePage(next.map_group_id, next.region_id);
                ShowNpcAdventureBattleResult(node, true, rewards,
                    () => OpenNpcAdventureBattleSelection());
            }
            catch (Exception error)
            {
                Notify("羁绊冒险扫荡已结算，但界面刷新失败：" + error.Message);
            }
        }

        private IReadOnlyList<int> CandidateNpcIds()
        {
            var friendships = (host.Profile.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .GroupBy(value => value.NpcClassId)
                .ToDictionary(value => value.Key, value => value.Last().Level);
            const int playType = (int)NpcAssistPlayType.BondAdventure;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            Zjcs.Presentation.Combat.OfficialBattleModelCatalog battleModels =
                Zjcs.Presentation.Combat.OfficialBattleModelCatalog.LoadDefault();
            IReadOnlyList<OfflineNpcAssistCandidate> candidates = OfflineNpcAssistanceCandidates.Build(
                host.NpcAssistanceCatalog,
                OfflineNpcAssistCandidateMode.FightAssist,
                playType,
                host.Level,
                friendships,
                host.StoryCount,
                battleModels.HasNpcBattleAnimations,
                (npcId, cycle, timestamp) => host.NpcAssistanceProgress.AssistCount(
                    playType, npcId, cycle, timestamp),
                npcAdventureSelectedNpcIds,
                OfflineNpcAdventureSession.MaximumCompanions,
                now,
                true);
            return candidates.Where(value => value.Available)
                .Select(value => value.NpcClassId).ToArray();
        }

        private void RefreshNpcAdventureSelectionItems()
        {
            foreach (GameObject item in npcAdventureSelectItems)
                if (item != null) UnityEngine.Object.Destroy(item);
            npcAdventureSelectItems.Clear();
            if (npcAdventureSelectRoot == null) return;
            SetNamedText(npcAdventureSelectRoot.transform, "count",
                npcAdventureSelectedNpcIds.Count + "/" + OfflineNpcAdventureSession.MaximumCompanions);
            Transform slot = Find(npcAdventureSelectRoot.transform, "Slot");
            if (slot == null) throw new InvalidOperationException("官方羁绊冒险伙伴列表槽位缺失。");
            GameObject prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/NpcAdventureSelectNpcItem");
            if (prefab == null) throw new InvalidOperationException("官方羁绊冒险伙伴条目缺失。");
            foreach (int npcId in CandidateNpcIds())
            {
                GameObject item = UnityEngine.Object.Instantiate(prefab, slot, false);
                DungeonUiBindings.Apply(item, "NpcAdventureSelectNpcItem");
                item.name = "NpcAdventureSelectNpcItem_" + npcId;
                BindNpcAdventureFont(item);
                bool selected = npcAdventureSelectedNpcIds.Contains(npcId);
                int order = npcAdventureSelectedNpcIds.IndexOf(npcId) + 1;
                NpcTravelNpc npc = host.NpcTravelCatalog.Npc(npcId);
                SetNamedText(item.transform, "NameText", npc == null ? "伙伴 " + npcId
                    : catalog.LocalizedText(npc.name_key, "伙伴 " + npcId));
                SetNamedText(item.transform, "OrderText", selected ? order.ToString() : string.Empty);
                SetPrefabActive(item, "ImgSelected", selected);
                SetPrefabActive(item, "ImgUnselect", !selected);
                Transform iconNode = Find(item.transform, "IconImg");
                Image icon = iconNode == null ? null : iconNode.GetComponent<Image>();
                if (icon != null)
                {
                    string key = NpcTravelIconKey(npc);
                    icon.sprite = catalog.FindDynamicSprite(key)
                        ?? catalog.FindDynamicSprite(NpcTravelIconKey(npc, false));
                    icon.preserveAspect = true;
                }
                Transform professionNode = Find(item.transform, "ProfessionIcon");
                Image professionIcon = professionNode == null ? null : professionNode.GetComponent<Image>();
                if (professionIcon != null)
                {
                    professionIcon.sprite = TravelCategoryIcon("profession", npc?.travel_profession ?? 0);
                    professionIcon.preserveAspect = true;
                    professionIcon.enabled = professionIcon.sprite != null;
                }
                BindPrefabButton(item, "SelectBtn", () => ToggleNpcAdventureBattlePartner(npcId));
                npcAdventureSelectItems.Add(item);
            }
            if (npcAdventureSelectedNpcIds.Count > OfflineNpcAdventureSession.MaximumCompanions)
                npcAdventureSelectedNpcIds.RemoveRange(OfflineNpcAdventureSession.MaximumCompanions,
                    npcAdventureSelectedNpcIds.Count - OfflineNpcAdventureSession.MaximumCompanions);
        }

        private void ToggleNpcAdventureBattlePartner(int npcId)
        {
            if (npcAdventureSelectedNpcIds.Contains(npcId)) npcAdventureSelectedNpcIds.Remove(npcId);
            else if (npcAdventureSelectedNpcIds.Count < OfflineNpcAdventureSession.MaximumCompanions)
                npcAdventureSelectedNpcIds.Add(npcId);
            else
            {
                Notify("最多选择 3 名助战伙伴");
                return;
            }
            RefreshNpcAdventureSelectionItems();
        }

        private void ConfirmNpcAdventureBattleSelection()
        {
            if (npcAdventureSelectedNpcIds.Count != OfflineNpcAdventureSession.MaximumCompanions)
            {
                Notify("请选择 3 名助战伙伴");
                return;
            }
            ProgressionResult result = host.NpcAdventure.SetSelectedNpcClassIds(npcAdventureSelectedNpcIds);
            if (!result.IsApplied && result.Status != ProgressionResultStatus.Unchanged)
            {
                Notify(result.Message ?? "伙伴选择保存失败");
                return;
            }
            NpcTravelNode node = npcAdventureSelectedNode;
            int[] selectedNpcIds = npcAdventureSelectedNpcIds.ToArray();
            StartNpcAdventureBattle(node, selectedNpcIds);
        }

        private void StartNpcAdventureBattle(NpcTravelNode node, IReadOnlyList<int> selectedNpcIds)
        {
            CloseNpcAdventureBattleSelection();
            CloseNpcTravelPages();
            NpcAdventureBattleRunner runner = host.gameObject.AddComponent<NpcAdventureBattleRunner>();
            try
            {
                runner.Begin(host, node, selectedNpcIds);
            }
            catch (Exception error)
            {
                UnityEngine.Object.Destroy(runner);
                Debug.LogException(error);
                Notify(error.Message);
            }
        }

        private void CloseNpcAdventureBattleSelection()
        {
            foreach (GameObject item in npcAdventureSelectItems)
                if (item != null) UnityEngine.Object.Destroy(item);
            npcAdventureSelectItems.Clear();
            if (npcAdventureSelectRoot != null) UnityEngine.Object.Destroy(npcAdventureSelectRoot);
            npcAdventureSelectRoot = null;
            npcAdventureSelectedNode = null;
        }

        internal void ShowNpcAdventureBattleResult(NpcTravelNode node, bool won,
            IReadOnlyList<NpcAdventureRewardItem> rewards, Action retry)
        {
            CloseNpcAdventureBattleResult();
            GameObject prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/GameEndPanelNpcAdventure");
            if (prefab == null) throw new InvalidOperationException("官方羁绊冒险结算窗口缺失。");
            npcAdventureResultRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            DungeonUiBindings.Apply(npcAdventureResultRoot, "GameEndPanelNpcAdventure");
            npcAdventureResultRoot.name = "NpcAdventureBattleResult";
            npcAdventureResultRoot.transform.SetAsLastSibling();
            PlaceModalCanvasOnTop(npcAdventureResultRoot);
            BindNpcAdventureFont(npcAdventureResultRoot);
            SetPrefabActive(npcAdventureResultRoot, "WinObj", won);
            SetPrefabActive(npcAdventureResultRoot, "LoseObj", !won);
            string stageText = string.Empty;
            if (node != null)
            {
                string key = "ui_npc_travel_map_group_" + node.map_group_id;
                string format = catalog.LocalizedText(key, "{0}-{1}");
                stageText = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    format, node.region_id, node.node_id);
            }
            SetNamedText(npcAdventureResultRoot.transform, "StageText", stageText);
            SetNamedText(npcAdventureResultRoot.transform, "LimitText",
                "今日剩余挑战次数 " + host.NpcAdventure.RemainingBattleCount(DateTime.Now)
                    + "/" + host.NpcAdventure.DailyBattleLimit);
            int remaining = host.NpcAdventure.RemainingBattleCount(DateTime.Now);
            SetPrefabActive(npcAdventureResultRoot, "ContinueBtn", won && remaining > 0);
            SetPrefabActive(npcAdventureResultRoot, "RetryBtn", !won && remaining > 0);
            BindPrefabButton(npcAdventureResultRoot, "ContinueBtn", ContinueNpcAdventureBattle);
            BindPrefabButton(npcAdventureResultRoot, "CloseBtn", CloseNpcAdventureBattleResult);
            BindPrefabButton(npcAdventureResultRoot, "RetryBtn", retry);
            RebuildNpcAdventureRewardItems(rewards);
            RebuildNpcAdventureNodeRewards(node, won);
        }

        private void RebuildNpcAdventureRewardItems(IReadOnlyList<NpcAdventureRewardItem> rewards)
        {
            Transform grid = Find(npcAdventureResultRoot == null ? null : npcAdventureResultRoot.transform,
                "Grid");
            if (grid == null) return;
            foreach (Transform child in grid.Cast<Transform>().ToArray())
                UnityEngine.Object.Destroy(child.gameObject);
            GameObject slotPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Award/AwardPanelItem");
            GameObject awardIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemAwardIcon");
            GameObject itemIconPrefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/Native/Prefabs/UI/Item/ItemIcon");
            if (slotPrefab == null || awardIconPrefab == null || itemIconPrefab == null)
                throw new InvalidOperationException("官方羁绊冒险奖励物品资源缺失。");
            foreach (NpcAdventureRewardItem reward in rewards ?? Array.Empty<NpcAdventureRewardItem>())
            {
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0) continue;
                GameObject slot = UnityEngine.Object.Instantiate(slotPrefab, grid, false);
                slot.name = "AwardPanelItem_" + reward.ItemId;
                DungeonUiBindings.Apply(slot, "AwardPanelItem");
                Transform delay = Find(slot.transform, "DelayShow") ?? slot.transform;
                delay.gameObject.SetActive(true);
                GameObject awardIcon = UnityEngine.Object.Instantiate(awardIconPrefab, delay, false);
                DungeonUiBindings.Apply(awardIcon, "ItemAwardIcon");
                Transform anchor = Find(awardIcon.transform, "Anchor");
                if (anchor == null) throw new InvalidOperationException("官方奖励条目图标挂点缺失。");
                anchor.gameObject.SetActive(true);
                GameObject itemIcon = UnityEngine.Object.Instantiate(itemIconPrefab, anchor, false);
                DungeonUiBindings.Apply(itemIcon, "ItemIcon");
                DungeonUiBindings.ApplyNpcAdventureRewardIcon(itemIcon, reward.ItemId, reward.Count);
            }
        }

        private void RebuildNpcAdventureNodeRewards(NpcTravelNode node, bool won)
        {
            Transform slot = npcAdventureResultRoot == null ? null : npcAdventureResultRoot.transform.Find(
                "Content/WinObj/AdditionContent/Slot");
            if (slot == null) return;
            foreach (Transform child in slot.Cast<Transform>().ToArray())
                UnityEngine.Object.Destroy(child.gameObject);
            if (!won || node == null) return;
            GameObject prefab = Resources.Load<GameObject>(
                "Dungeons/UI/884_156692/NpcAdventureNodeRewardItem");
            if (prefab == null) throw new InvalidOperationException("官方羁绊冒险节点奖励说明资源缺失。");
            AddNpcAdventureNodeReward(slot, prefab, PropType.TravelNodeBaseRewardAddCount,
                node.base_add_count, false);
            AddNpcAdventureNodeReward(slot, prefab, PropType.TravelNodeBaseRewardAddPercent,
                node.base_add_percent, true);
            AddNpcAdventureNodeReward(slot, prefab, PropType.TravelNodeExtraRewardAddProbability,
                node.extra_probability_add, true);
        }

        private void AddNpcAdventureNodeReward(Transform parent, GameObject prefab,
            PropType propertyType, int value, bool percentage)
        {
            if (value <= 0) return;
            string key = "PropType." + propertyType;
            string label = catalog.LocalizedText(key, key);
            string formatted = percentage
                ? (value / 100f).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + "%"
                : value.ToString(System.Globalization.CultureInfo.CurrentCulture);
            GameObject item = UnityEngine.Object.Instantiate(prefab, parent, false);
            DungeonUiBindings.Apply(item, "NpcAdventureNodeRewardItem");
            item.name = "NpcAdventureNodeRewardItem_" + (int)propertyType;
            BindNpcAdventureFont(item);
            SetNamedText(item.transform, "RewardText", label + " +" + formatted);
        }

        private void ContinueNpcAdventureBattle()
        {
            CloseNpcAdventureBattleResult();
            OpenNpcAdventureBattleSelection();
        }

        private void BindNpcAdventureFont(GameObject root)
        {
            if (root == null || catalog.Font == null) return;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = catalog.Font;
                text.fontSharedMaterial = catalog.Font.material;
            }
        }

        private void CloseNpcAdventureBattleResult()
        {
            if (npcAdventureResultRoot != null) UnityEngine.Object.Destroy(npcAdventureResultRoot);
            npcAdventureResultRoot = null;
        }
    }
}
