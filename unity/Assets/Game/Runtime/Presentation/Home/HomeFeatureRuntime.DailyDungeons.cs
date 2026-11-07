using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonClaimRewardDisplayItem
    {
        public int ItemId { get; }
        public long Count { get; }
        public DailyDungeonClaimRewardDisplayItem(int itemId, long count) { ItemId = itemId; Count = count; }
    }

    public sealed partial class HomeFeatureRuntime
    {
        public bool ClaimDailyDungeonLeaderboardGift(int id)
        {
            if (dailyDungeons == null || profile == null) return false;
            bool claimed = dailyDungeons.ClaimLeaderboardGift(id, (receipt, rewards) =>
            {
                long now = Now();
                ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
                {
                    foreach (DailyDungeonAchievementReward reward in rewards)
                        if (!OfflineInventoryOperations.Grant(save.Player, reward.ItemId, reward.Count, now).IsApplied)
                            throw new InvalidOperationException("Daily dungeon leaderboard gift could not be saved.");
                });
                return result.Status == ProgressionResultStatus.Applied
                    || result.Status == ProgressionResultStatus.Unchanged || result.Status == ProgressionResultStatus.Ready;
            });
            if (claimed) { ReloadContext(); hud?.RefreshAll(); }
            return claimed;
        }

        private OfflineDailyDungeonSettlementModule DungeonSettlement()
        {
            EnsureProgression();
            return new OfflineDailyDungeonSettlementModule(dailyDungeons, profile, progression, Now);
        }

        public bool ClaimDailyDungeonRewards(string claimId, int times,
            out IReadOnlyList<DailyDungeonClaimRewardDisplayItem> rewards, out bool hasRewardDetails)
        {
            DailyDungeonSettlementResult result = DungeonSettlement().Claim(claimId, times);
            hasRewardDetails = result.HasRewardDetails;
            rewards = result.Items.Select(value => new DailyDungeonClaimRewardDisplayItem(value.ItemId, value.Count)).ToArray();
            if (result.InventoryCommitted) { ReloadContext(); hud?.RefreshAll(); }
            return result.Committed;
        }

        public bool SettleDailyDungeonRooms(string receipt, DailyDungeonRunSave run)
        {
            DailyDungeonSettlementResult result = DungeonSettlement().SettleClearedRooms(receipt, run);
            if (result.Committed) { ReloadContext(); hud?.RefreshAll(); }
            return result.Committed;
        }
    }
}
