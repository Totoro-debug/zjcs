using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineNpcAdventureProgress
    {
        public int LocalDayKey;
        public int DailyBattleCount;
        public int MapGroupId;
        public int RegionId;
        public int NodeId;
        public int ActiveMapGroupId;
        public int ActiveRegionId;
        public int ActiveNodeId;
        public int ActiveStageId;
        public string ActiveBattleReceipt = string.Empty;
        public string ActiveBattleCheckpoint = string.Empty;
        public List<int> SelectedNpcClassIds = new List<int>();

        public static void Normalize(OfflineNpcAdventureProgress value)
        {
            if (value == null) return;
            if (value.ActiveBattleCheckpoint == null) value.ActiveBattleCheckpoint = string.Empty;
            if (value.ActiveBattleReceipt == null) value.ActiveBattleReceipt = string.Empty;
            if (value.SelectedNpcClassIds == null) value.SelectedNpcClassIds = new List<int>();
        }

        public static bool IsValid(OfflineNpcAdventureProgress value)
        {
            if (value == null || value.LocalDayKey < 0 || value.DailyBattleCount < 0
                || value.DailyBattleCount > 10 || value.MapGroupId < 0 || value.RegionId < 0
                || value.NodeId < 0 || value.ActiveMapGroupId < 0 || value.ActiveRegionId < 0
                || value.ActiveNodeId < 0 || value.ActiveStageId < 0
                || value.ActiveBattleReceipt == null || value.ActiveBattleReceipt.Length > 160
                || value.ActiveBattleCheckpoint == null || value.ActiveBattleCheckpoint.Length > 1_000_000
                || value.SelectedNpcClassIds == null || value.SelectedNpcClassIds.Count > 3
                || value.SelectedNpcClassIds.Any(id => id <= 0)
                || value.SelectedNpcClassIds.Distinct().Count() != value.SelectedNpcClassIds.Count)
                return false;

            bool completedNodeEmpty = value.MapGroupId == 0 && value.RegionId == 0 && value.NodeId == 0;
            bool completedNodePresent = value.MapGroupId > 0 && value.RegionId > 0 && value.NodeId > 0;
            bool activeNodeEmpty = value.ActiveMapGroupId == 0 && value.ActiveRegionId == 0
                && value.ActiveNodeId == 0 && value.ActiveStageId == 0;
            bool activeNodePresent = value.ActiveMapGroupId > 0 && value.ActiveRegionId > 0
                && value.ActiveNodeId > 0 && value.ActiveStageId > 0;
            return (completedNodeEmpty || completedNodePresent)
                && (activeNodeEmpty || activeNodePresent)
                && (activeNodePresent || string.IsNullOrEmpty(value.ActiveBattleCheckpoint))
                && (activeNodePresent ? !string.IsNullOrEmpty(value.ActiveBattleReceipt)
                    : string.IsNullOrEmpty(value.ActiveBattleReceipt));
        }
    }

    public sealed class OfflineNpcAdventureSession
    {
        public const int MaximumCompanions = 3;
        public const int MaximumMapGroupId = 17;
        public const int MaximumRegionId = 8;
        public const int MaximumNodeId = 20;

        private readonly OfflineMainlineProgressSession profile;
        private readonly OfficialNpcTravelCatalog catalog;
        public int DailyBattleLimit => catalog.Data.daily_battle_limit;
        private int ResetHour => OfflineRefreshClock.Hour;
        private int ResetMinute => catalog.Data.daily_reset_minute;

        public OfflineNpcAdventureSession(OfflineMainlineProgressSession profile,
            OfficialNpcTravelCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public OfflineNpcAdventureProgress State
        {
            get
            {
                profile.Reload();
                return profile.Save.Player.NpcAdventure;
            }
        }

        public int RemainingBattleCount(DateTime now)
        {
            OfflineNpcAdventureProgress state = State;
            return state.LocalDayKey == DayKey(now)
                ? Math.Max(0, DailyBattleLimit - state.DailyBattleCount)
                : DailyBattleLimit;
        }

        public NpcTravelNode FirstNode => AuthorizedNodes().FirstOrDefault();

        public NpcTravelNode NextNode
        {
            get => NextNodeFor(State);
        }

        public NpcTravelNode CompletedNode => catalog.Node(State.MapGroupId, State.RegionId, State.NodeId);

        public ProgressionResult SetSelectedNpcClassIds(IEnumerable<int> npcClassIds)
        {
            int[] ids = (npcClassIds ?? Array.Empty<int>()).ToArray();
            if (ids.Length > MaximumCompanions || ids.Any(id => id <= 0)
                || ids.Distinct().Count() != ids.Length)
                return Invalid("羁绊冒险伙伴选择无效。");
            return profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineNpcAdventureProgress state = player.NpcAdventure;
                if (state.SelectedNpcClassIds.SequenceEqual(ids)) return false;
                state.SelectedNpcClassIds = ids.ToList();
                return true;
            });
        }

        public ProgressionResult BeginBattle(NpcTravelNode node, DateTime now)
        {
            if (node == null || node.stage_id <= 0 || !IsAuthorized(node))
                return Invalid("羁绊冒险关卡不在当前开放范围内。");
            profile.Reload();
            OfflineNpcAdventureProgress current = profile.Save.Player.NpcAdventure;
            if (current.ActiveStageId > 0) return Invalid("已有未完成的羁绊冒险战斗。");
            if (current.SelectedNpcClassIds.Count != MaximumCompanions)
                return Invalid("请先选择 3 名伙伴助战。");
            if (!IsSameNode(NextNode, node)) return Invalid("羁绊冒险关卡顺序不正确。");
            int day = DayKey(now);
            if (current.LocalDayKey == day && current.DailyBattleCount >= DailyBattleLimit)
                return new ProgressionResult { Status = ProgressionResultStatus.NotReady,
                    Message = "今日羁绊冒险挑战次数已用完。" };

            return profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineNpcAdventureProgress state = player.NpcAdventure;
                if (state.ActiveStageId > 0 || !IsSameNode(NextNode, node)) return false;
                if (state.LocalDayKey != day)
                {
                    state.LocalDayKey = day;
                    state.DailyBattleCount = 0;
                }
                if (state.DailyBattleCount >= DailyBattleLimit) return false;
                state.DailyBattleCount++;
                state.ActiveMapGroupId = node.map_group_id;
                state.ActiveRegionId = node.region_id;
                state.ActiveNodeId = node.node_id;
                state.ActiveStageId = node.stage_id;
                state.ActiveBattleReceipt = "npc-adventure:" + day + ":" + state.DailyBattleCount
                    + ":" + node.stage_id;
                state.ActiveBattleCheckpoint = string.Empty;
                return true;
            });
        }

        public string CreateSweepReceipt(NpcTravelNode node, DateTime now)
        {
            if (node == null || node.stage_id <= 0 || !IsAuthorized(node)
                || IsSameNode(node, FirstNode)) return string.Empty;
            profile.Reload();
            OfflineNpcAdventureProgress state = profile.Save.Player.NpcAdventure;
            if (state.ActiveStageId > 0 || !IsSameNode(NextNodeFor(state), node)) return string.Empty;
            int day = DayKey(now);
            int count = state.LocalDayKey == day ? state.DailyBattleCount : 0;
            if (count >= DailyBattleLimit) return string.Empty;
            return "npc-adventure-sweep:" + day + ":" + (count + 1) + ":" + node.stage_id;
        }

        public ProgressionResult SweepNextStage(NpcTravelNode node,
            IReadOnlyList<KeyValuePair<int, long>> rewards, string receipt, DateTime now)
        {
            if (node == null || node.stage_id <= 0 || !IsAuthorized(node)
                || IsSameNode(node, FirstNode)
                || string.IsNullOrWhiteSpace(receipt) || receipt.Length > 160)
                return Invalid("羁绊冒险扫荡请求无效。");
            if (rewards != null && rewards.Any(value => value.Key <= 0 || value.Value <= 0))
                return Invalid("羁绊冒险奖励无效。");

            profile.Reload();
            OfflineNpcAdventureProgress current = profile.Save.Player.NpcAdventure;
            if (current.ActiveStageId > 0 || !IsSameNode(NextNodeFor(current), node))
                return Invalid("只能扫荡当前下一关。");
            int day = DayKey(now);
            int count = current.LocalDayKey == day ? current.DailyBattleCount : 0;
            if (count >= DailyBattleLimit)
                return new ProgressionResult { Status = ProgressionResultStatus.NotReady,
                    Message = "今日羁绊冒险挑战次数已用完。" };
            string expected = "npc-adventure-sweep:" + day + ":" + (count + 1) + ":" + node.stage_id;
            if (!string.Equals(receipt, expected, StringComparison.Ordinal))
                return Invalid("羁绊冒险扫荡回执不匹配。");

            long nowUnixSeconds = new DateTimeOffset(now).ToUnixTimeSeconds();
            return profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflineNpcAdventureProgress state = save.Player.NpcAdventure;
                if (state.ActiveStageId > 0 || !IsSameNode(NextNodeFor(state), node))
                    throw new InvalidOperationException("只能扫荡当前下一关。");
                if (state.LocalDayKey != day)
                {
                    state.LocalDayKey = day;
                    state.DailyBattleCount = 0;
                }
                if (state.DailyBattleCount >= DailyBattleLimit)
                    throw new InvalidOperationException("今日羁绊冒险挑战次数已用完。");
                state.DailyBattleCount++;
                state.MapGroupId = node.map_group_id;
                state.RegionId = node.region_id;
                state.NodeId = node.node_id;
                foreach (KeyValuePair<int, long> reward in rewards ?? Array.Empty<KeyValuePair<int, long>>())
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(
                        save.Player, reward.Key, reward.Value, nowUnixSeconds);
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("羁绊冒险奖励无法发放：" + reward.Key);
                }
            });
        }

        public ProgressionResult CancelBattleStart(int stageId)
        {
            if (stageId <= 0) return Invalid("羁绊冒险关卡无效。");
            profile.Reload();
            return profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineNpcAdventureProgress state = player.NpcAdventure;
                if (state.ActiveStageId != stageId) return false;
                if (state.DailyBattleCount > 0) state.DailyBattleCount--;
                state.ActiveMapGroupId = 0;
                state.ActiveRegionId = 0;
                state.ActiveNodeId = 0;
                state.ActiveStageId = 0;
                state.ActiveBattleReceipt = string.Empty;
                state.ActiveBattleCheckpoint = string.Empty;
                return true;
            });
        }

        public ProgressionResult SaveBattleCheckpoint(int stageId, string checkpoint)
        {
            if (stageId <= 0 || string.IsNullOrEmpty(checkpoint)) return Invalid("羁绊冒险战斗 checkpoint 无效。");
            return profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineNpcAdventureProgress state = player.NpcAdventure;
                if (state.ActiveStageId != stageId) return false;
                if (state.ActiveBattleCheckpoint == checkpoint) return false;
                state.ActiveBattleCheckpoint = checkpoint;
                return true;
            });
        }

        public ProgressionResult CompleteBattle(int stageId, bool won)
        {
            profile.Reload();
            return profile.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineNpcAdventureProgress state = player.NpcAdventure;
                if (state.ActiveStageId != stageId) return false;
                if (won)
                {
                    state.MapGroupId = state.ActiveMapGroupId;
                    state.RegionId = state.ActiveRegionId;
                    state.NodeId = state.ActiveNodeId;
                }
                state.ActiveMapGroupId = 0;
                state.ActiveRegionId = 0;
                state.ActiveNodeId = 0;
                state.ActiveStageId = 0;
                state.ActiveBattleReceipt = string.Empty;
                state.ActiveBattleCheckpoint = string.Empty;
                return true;
            });
        }

        public ProgressionResult CompleteBattle(int stageId, bool won,
            IReadOnlyList<KeyValuePair<int, long>> rewards, string receipt)
        {
            if (stageId <= 0 || string.IsNullOrWhiteSpace(receipt) || receipt.Length > 160)
                return Invalid("羁绊冒险结算回执无效。");
            profile.Reload();
            OfflineNpcAdventureProgress current = profile.Save.Player.NpcAdventure;
            if (current.ActiveStageId != stageId || !string.Equals(current.ActiveBattleReceipt,
                    receipt, StringComparison.Ordinal))
                return Invalid("羁绊冒险没有匹配的进行中战斗。");
            if (!won && rewards != null && rewards.Any(value => value.Key <= 0 || value.Value <= 0))
                return Invalid("失败结算不能包含无效奖励。");
            if (won && rewards != null && rewards.Any(value => value.Key <= 0 || value.Value <= 0))
                return Invalid("羁绊冒险奖励无效。");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflineNpcAdventureProgress state = save.Player.NpcAdventure;
                if (state.ActiveStageId != stageId || !string.Equals(state.ActiveBattleReceipt,
                        receipt, StringComparison.Ordinal))
                    throw new InvalidOperationException("羁绊冒险结算回执不匹配。");
                if (won)
                {
                    state.MapGroupId = state.ActiveMapGroupId;
                    state.RegionId = state.ActiveRegionId;
                    state.NodeId = state.ActiveNodeId;
                    foreach (KeyValuePair<int, long> reward in rewards ?? Array.Empty<KeyValuePair<int, long>>())
                    {
                        InventoryOperationResult granted = OfflineInventoryOperations.Grant(
                            save.Player, reward.Key, reward.Value, now);
                        if (!granted.IsApplied)
                            throw new InvalidOperationException("羁绊冒险奖励无法发放：" + reward.Key);
                    }
                }
                state.ActiveMapGroupId = 0;
                state.ActiveRegionId = 0;
                state.ActiveNodeId = 0;
                state.ActiveStageId = 0;
                state.ActiveBattleReceipt = string.Empty;
                state.ActiveBattleCheckpoint = string.Empty;
            });
        }

        private IEnumerable<NpcTravelNode> AuthorizedNodes() => catalog.Data.nodes
            .Where(row => row != null && row.stage_id > 0 && IsAuthorized(row))
            .OrderBy(row => row.map_group_id).ThenBy(row => row.region_id).ThenBy(row => row.node_id);

        private NpcTravelNode NextNodeFor(OfflineNpcAdventureProgress state)
        {
            NpcTravelNode[] nodes = AuthorizedNodes().ToArray();
            if (state.MapGroupId == 0) return nodes.FirstOrDefault();
            int current = Array.FindIndex(nodes, row => row.map_group_id == state.MapGroupId
                && row.region_id == state.RegionId && row.node_id == state.NodeId);
            return current < 0 || current + 1 >= nodes.Length ? null : nodes[current + 1];
        }

        private static bool IsAuthorized(NpcTravelNode node) => node.map_group_id < MaximumMapGroupId
            || node.map_group_id == MaximumMapGroupId && (node.region_id < MaximumRegionId
                || node.region_id == MaximumRegionId && node.node_id <= MaximumNodeId);

        private static bool IsSameNode(NpcTravelNode left, NpcTravelNode right) => left != null && right != null
            && left.map_group_id == right.map_group_id && left.region_id == right.region_id
            && left.node_id == right.node_id && left.stage_id == right.stage_id;

        private int DayKey(DateTime now)
        {
            DateTime gameDay = now.Date.AddHours(ResetHour).AddMinutes(ResetMinute);
            if (now < gameDay) gameDay = gameDay.AddDays(-1);
            return gameDay.Year * 10000 + gameDay.Month * 100 + gameDay.Day;
        }

        private static ProgressionResult Invalid(string message) => new ProgressionResult
        { Status = ProgressionResultStatus.InvalidState, Message = message };
    }
}
