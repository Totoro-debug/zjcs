using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonClaimRewardDisplayItem
    {
        public int ItemId { get; }
        public long Count { get; }

        public DailyDungeonClaimRewardDisplayItem(int itemId, long count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    public sealed partial class HomeFeatureRuntime
    {
        public bool GrantDailyDungeonRewardsForSettlement(string receipt, DailyDungeonRunSave run,
            int[] rewardRuleIds, int awardTimes, out IReadOnlyList<DailyDungeonClaimRewardDisplayItem> rewards)
        {
            rewards = Array.Empty<DailyDungeonClaimRewardDisplayItem>();
            if (string.IsNullOrWhiteSpace(receipt) || run == null || rewardRuleIds == null || awardTimes <= 0)
                return false;

            EnsureProgression();
            long firstEquipmentInstanceId = profile.Save.Player.Equipment.NextInstanceId;
            int profession = profile.Save.Player.Wardrobe.Profession;
            long combatRating = DailyDungeonCombatRating();
            DailyDungeonRewardRoll roll = dailyDungeons.RollRewards(run, run.grade > 0,
                profile.Save.Player.Level, profile.Save.Player.SubRank,
                rule => DailyDungeonRewardConditionMet(rule, profession, combatRating), receipt,
                awardTimes, rewardRuleIds, new DailyDungeonRewardRandom(receipt));

            if (!GrantDailyDungeonRewards(receipt, run, rewardRuleIds, awardTimes)) return false;

            var itemCounts = new Dictionary<int, long>();
            OfficialEquipmentCatalog equipment = progression.EquipmentCatalog;
            foreach (DailyDungeonRewardRollItem reward in roll.Items)
            {
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0) continue;
                bool equipmentReward = progression.Catalog.ItemType(reward.ItemId) == 110
                    || equipment?.Find("equip", reward.ItemId) != null;
                if (equipmentReward) continue;
                Add(itemCounts, reward.ItemId, reward.Count);
            }

            foreach (OfflineEquipmentInstance instance in profile.Save.Player.Equipment.Instances
                .Where(value => value != null && value.InstanceId >= firstEquipmentInstanceId))
                Add(itemCounts, instance.ItemId, 1);

            rewards = itemCounts.OrderBy(value => value.Key)
                .Select(value => new DailyDungeonClaimRewardDisplayItem(value.Key, value.Value))
                .ToArray();
            return true;
        }

        private static void Add(IDictionary<int, long> counts, int itemId, long count)
        {
            counts[itemId] = counts.TryGetValue(itemId, out long current)
                ? checked(current + count) : count;
        }
    }
}
