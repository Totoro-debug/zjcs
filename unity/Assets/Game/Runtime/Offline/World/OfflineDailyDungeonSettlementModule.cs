using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.World
{
    public sealed class DailyDungeonSettlementResult
    {
        public bool InventoryCommitted { get; internal set; }
        public bool Committed { get; internal set; }
        public bool HasRewardDetails { get; internal set; }
        public string Receipt { get; internal set; }
        public string Message { get; internal set; }
        public IReadOnlyList<DailyDungeonSettlementItem> Items { get; internal set; } = Array.Empty<DailyDungeonSettlementItem>();
        public IReadOnlyList<long> EquipmentInstanceIds { get; internal set; } = Array.Empty<long>();
    }

    // Owns the reservation -> inventory receipt -> dungeon confirmation sequence.
    // Reward rolls and equipment generation retain the migrated official ordering.
    public sealed class OfflineDailyDungeonSettlementModule
    {
        private readonly DailyDungeonModule dailyDungeons;
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfflineCharacterProgressionService progression;
        private readonly Func<long> now;

        public OfflineDailyDungeonSettlementModule(DailyDungeonModule dailyDungeons,
            OfflineMainlineProgressSession profile, OfflineCharacterProgressionService progression, Func<long> now)
        {
            this.dailyDungeons = dailyDungeons ?? throw new ArgumentNullException(nameof(dailyDungeons));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
        }

        public DailyDungeonSettlementResult Claim(string claimId, int requestedTimes)
        {
            if (string.IsNullOrWhiteSpace(claimId)) return new DailyDungeonSettlementResult();
            profile.Reload();
            string receipt = "daily-dungeon:claim:" + claimId;
            DailyDungeonSettlementResult result = new DailyDungeonSettlementResult { Receipt = receipt };
            bool confirmed;
            try
            {
                confirmed = dailyDungeons.ClaimPendingReward(claimId, (id, run, times) =>
                {
                    result = Grant(id, run, dailyDungeons.ConfiguredRewardRuleIds(run, true), times);
                    return result.InventoryCommitted;
                }, requestedTimes);
            }
            catch (IOException error) { result.Committed = false; result.Message = error.Message; return result; }
            catch (UnauthorizedAccessException error) { result.Committed = false; result.Message = error.Message; return result; }
            if (confirmed) { result.Committed = true; return result; }
            // A repeated request after confirmation can still display the stored result.
            if (!dailyDungeons.PendingClaims.Any(value => value.id == claimId)) return ReadCommitted(receipt);
            result.Committed = false;
            return result;
        }

        public DailyDungeonSettlementResult SettleClearedRooms(string receipt, DailyDungeonRunSave run)
        {
            return Grant(receipt, run, dailyDungeons.ConfiguredRewardRuleIds(run, run?.grade > 0));
        }

        private DailyDungeonSettlementResult ReadCommitted(string receipt)
        {
            bool committed = profile.Save.Player.AppliedInventoryEventIds.Contains(receipt);
            var record = profile.Save.Player.DailyDungeonSettlements?.FirstOrDefault(value => value.Receipt == receipt);
            return new DailyDungeonSettlementResult {
                Receipt = receipt, InventoryCommitted = committed, Committed = committed,
                HasRewardDetails = record != null,
                Items = record == null ? Array.Empty<DailyDungeonSettlementItem>() : record.Items
                    .Select(value => new DailyDungeonSettlementItem { ItemId = value.ItemId, Count = value.Count }).ToArray(),
                EquipmentInstanceIds = record?.EquipmentInstanceIds.ToArray() ?? Array.Empty<long>()
            };
        }

        private static void Add(DailyDungeonSettlementRecord record, int itemId, long count)
        {
            var item = record.Items.FirstOrDefault(value => value.ItemId == itemId);
            if (item == null) record.Items.Add(new DailyDungeonSettlementItem { ItemId = itemId, Count = count });
            else item.Count = checked(item.Count + count);
        }

        private long Now() => now();
        private long DailyDungeonCombatRating() => progression.PropertyCalculator
            .Evaluate(profile.Save.Player, profile.Save.World, Now()).CombatRating;

        private DailyDungeonSettlementResult Grant(string receipt, DailyDungeonRunSave run,
            int[] rewardRuleIds, int awardTimes = 1)
        {
            if (string.IsNullOrWhiteSpace(receipt) || run == null || rewardRuleIds == null || awardTimes <= 0)
                return new DailyDungeonSettlementResult();
            profile.Reload();
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(receipt)) return ReadCommitted(receipt);
            var random = new DailyDungeonRewardRandom(receipt);
            var itemCounts = new Dictionary<int, long>();
            int profession = profile.Save.Player.Wardrobe.Profession;
            long combatRating = DailyDungeonCombatRating();
            Zjcs.Offline.World.DailyDungeonRewardRoll roll = dailyDungeons.RollRewards(run,
                run.grade > 0, profile.Save.Player.Level, profile.Save.Player.SubRank,
                rule => DailyDungeonRewardConditionMet(rule, profession, combatRating), receipt,
                Math.Max(1, awardTimes), rewardRuleIds, random);
            foreach (Zjcs.Offline.World.DailyDungeonRewardRollItem reward in roll.Items)
            {
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0) continue;
                itemCounts[reward.ItemId] = itemCounts.TryGetValue(reward.ItemId, out long current)
                    ? checked(current + reward.Count) : reward.Count;
            }
            OfficialEquipmentCatalog equipment = progression.EquipmentCatalog;
            var dropRange = dailyDungeons.FindMap(run.mapGroupId)?.equipmentDrops?
                .FirstOrDefault(value => value.difficulty == run.difficulty);
            long now = Now();
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                var record = new DailyDungeonSettlementRecord { Receipt = receipt };
                foreach (KeyValuePair<int, long> reward in itemCounts.OrderBy(value => value.Key))
                {
                    int type = progression.Catalog.ItemType(reward.Key);
                    bool randomEquipment = type == 110;
                    bool isEquipment = equipment?.Find("equip", reward.Key) != null;
                    Zjcs.Offline.World.DailyDungeonRuntimeEquipmentPool pool = randomEquipment
                        ? dailyDungeons.Catalog.randomEquipmentPools?.FirstOrDefault(value => value.id == reward.Key)
                        : null;
                    if (randomEquipment && (pool?.equipIds == null || pool.weights == null
                        || pool.equipIds.Length != pool.weights.Length || !pool.weights.Any(value => value > 0)))
                        throw new InvalidOperationException("The official daily dungeon random equipment pool is unavailable: " + reward.Key);
                    if (randomEquipment || isEquipment)
                    {
                        if (equipment == null || dropRange == null)
                            throw new InvalidOperationException("Official daily dungeon equipment drop range is unavailable.");
                        long weightTotal = randomEquipment ? pool.weights.Where(value => value > 0).Sum(value => (long)value) : 0;
                        for (long index = 0; index < reward.Value; index++)
                        {
                            int equipId = reward.Key;
                            if (randomEquipment)
                            {
                                long pick = (long)(random.NextDouble() * weightTotal);
                                int selected = -1;
                                for (int poolIndex = 0; poolIndex < pool.weights.Length; poolIndex++)
                                {
                                    if (pool.weights[poolIndex] <= 0) continue;
                                    pick -= pool.weights[poolIndex];
                                    if (pick < 0) { selected = poolIndex; break; }
                                }
                                if (selected < 0) throw new InvalidOperationException("Daily dungeon equipment selection failed.");
                                equipId = pool.equipIds[selected];
                            }
                            long instanceId = save.Player.Equipment.NextInstanceId;
                            OfflineEquipmentInstance item = OfficialEquipmentGenerator.Generate(equipment,
                                equipId, instanceId, new EquipmentRollContext
                                {
                                    DropLevel = Math.Max(1, save.Player.Level),
                                    PlayerLevel = save.Player.Level,
                                    SubRank = save.Player.SubRank,
                                    SourceOperate = 61,
                                    Daily = true,
                                    MinimumLevel = dropRange.minimumLevel,
                                    MaximumLevel = dropRange.maximumLevel,
                                    MinimumBlessLevel = dropRange.minimumBlessLevel,
                                    MaximumBlessLevel = dropRange.maximumBlessLevel
                                }, random.NextFloat);
                            record.EquipmentInstanceIds.Add(instanceId);
                            Add(record, item.ItemId, 1);
                            save.Player.Equipment.Instances.Add(item);
                            save.Player.Equipment.NextInstanceId++;
                        }
                    }
                    else
                    {
                        InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                            reward.Key, reward.Value, now);
                        Add(record, reward.Key, reward.Value);
                        if (!granted.IsApplied)
                            throw new InvalidOperationException("Unsupported daily dungeon reward item: " + reward.Key);
                    }
                }
                save.Player.DailyDungeonSettlements ??= new List<DailyDungeonSettlementRecord>();
                save.Player.DailyDungeonSettlements.Add(record);
            });
            return result.Status == ProgressionResultStatus.Applied
                || result.Status == ProgressionResultStatus.Unchanged ? ReadCommitted(receipt)
                : new DailyDungeonSettlementResult { Receipt = receipt, Message = result.Message };
        }

        private int DailyDungeonRankForSubRank(int subRank)
        {
            return progression.Catalog.RankOfSubRank(subRank);
        }

        private int DailyDungeonHighSubRank(int rank)
        {
            return progression.Catalog.Rows("player_subrank")
                .Where(row => Convert.ToInt32(row["Rank"], CultureInfo.InvariantCulture) == rank)
                .Select(row => Convert.ToInt32(row["SubRank"], CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0).Max();
        }

        private bool DailyDungeonProfessionMatches(int profession, int ancestor)
        {
            if (profession <= 0 || ancestor <= 0) return false;
            int current = profession;
            var visited = new HashSet<int>();
            while (current > 0 && visited.Add(current))
            {
                if (current == ancestor) return true;
                Dictionary<string, object> row = progression.Catalog.Rows("profession_base")
                    .FirstOrDefault(value => Convert.ToInt32(value["Profession"], CultureInfo.InvariantCulture) == current);
                current = row == null ? 0
                    : Convert.ToInt32(row["PrePro"], CultureInfo.InvariantCulture);
            }
            return false;
        }

        private bool DailyDungeonRewardConditionMet(Zjcs.Offline.World.DailyDungeonRuntimeReward reward,
            int profession, long combatRating)
        {
            if (string.IsNullOrEmpty(reward.ConditionType)) return true;
            if (reward.ConditionType == "PlayerProfessionCondition")
                return profession > 0 && profession == reward.ConditionProfession;
            if (reward.ConditionType == "PlayerProfessionFactionCondition")
                return DailyDungeonProfessionMatches(profession, reward.ConditionProfession);
            if (reward.ConditionType == "CompositeAndCondition" || reward.ConditionType == "CompositeOrCondition")
            {
                int[] ids = reward.ConditionIds ?? Array.Empty<int>();
                if (ids.Length == 0) return false;
                bool Match(int id) => dailyDungeons.IsConditionMet(id, profile.Save.Player.Level,
                    combatRating, profile.Save.Player.SubRank, (_, __) => false,
                    DailyDungeonHighSubRank, DailyDungeonRankForSubRank, profession,
                    DailyDungeonProfessionMatches, profile.InventoryCount);
                return reward.ConditionType == "CompositeAndCondition"
                    ? ids.All(Match) : ids.Any(Match);
            }
            return false;
        }

        private sealed class DailyDungeonRewardRandom : Zjcs.Offline.World.IDailyDungeonRandom
        {
            private uint state;

            public DailyDungeonRewardRandom(string seed)
            {
                state = 2166136261;
                foreach (char value in seed) state = unchecked((state ^ value) * 16777619);
                if (state == 0) state = 0x9e3779b9;
            }

            public double NextDouble()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0x00ffffff) / 16777216d;
            }

            public float NextFloat() => (float)NextDouble();
            public bool Roll(double probability) => NextDouble() < Math.Min(1d, probability);
        }

    }
}
