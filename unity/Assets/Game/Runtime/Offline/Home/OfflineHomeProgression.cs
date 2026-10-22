using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Home
{
    /// <summary>Official ``FurnitureBase.EFurnitureState``.</summary>
    public enum HomeFurnitureState
    {
        Locked = 0,
        RepairNotice = 1,
        CanRepair = 2,
        Functional = 3
    }

    public sealed class HomeLevelUpResult
    {
        public bool Success;
        public int Level;
        public List<HomeCostEntry> Costs = new List<HomeCostEntry>();
        public string Error = string.Empty;
    }

    /// <summary>
    /// Home furniture and hang-up rules ported from ``FurnitureBase``,
    /// ``BaseHangUpFurniture`` and ``HangUpLevelUpPanel``.  Every mutation goes
    /// through <see cref="OfflineHomeIncomeSession.Mutate"/> so a failed save
    /// leaves the previous state untouched.
    /// </summary>
    public sealed class OfflineHomeProgression
    {
        private readonly OfficialHomeRules rules;

        public OfflineHomeProgression(OfficialHomeRules rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public OfficialHomeRules Rules => rules;

        public HomeFurnitureState FurnitureState(OfflineHomeIncomeSession session, int type,
            HomeConditionContext context)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.OwnsFurniture(type)) return HomeFurnitureState.Functional;
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return HomeFurnitureState.Locked;
            if (rules.IsMet(record.unlock, context)) return HomeFurnitureState.CanRepair;
            if (!rules.IsMet(record.repairNotice, context)) return HomeFurnitureState.Locked;
            return HomeFurnitureState.RepairNotice;
        }

        /// <summary>FurnitureBase.DoRepair → FurnitureRepairDialog confirmation.</summary>
        public bool RepairFurniture(OfflineHomeIncomeSession session, int type, HomeConditionContext context,
            Func<int, long> itemCount, Action<int, long> consume, string receipt)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (FurnitureState(session, type, context) != HomeFurnitureState.CanRepair) return false;
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return false;
            var costs = (record.repairCost ?? Array.Empty<HomeCostEntry>())
                .Where(cost => cost != null && cost.itemId > 0 && cost.count > 0).ToArray();
            foreach (HomeCostEntry cost in costs)
                if (itemCount != null && itemCount(cost.itemId) < cost.count) return false;
            if (!session.Mutate(receipt, save =>
                {
                    OfflineHomeFurnitureEntry entry = save.Furniture.FirstOrDefault(value => value.Type == type);
                    if (entry == null)
                    {
                        entry = new OfflineHomeFurnitureEntry { Type = type };
                        save.Furniture.Add(entry);
                    }
                    entry.Owned = true;
                    if (record.additionalRepair != null)
                        foreach (int dependency in record.additionalRepair)
                        {
                            OfflineHomeFurnitureEntry required = save.Furniture.FirstOrDefault(value => value.Type == dependency);
                            if (required == null || !required.Owned)
                                throw new InvalidOperationException("A prerequisite furniture is not repaired.");
                        }
                })) return false;
            foreach (HomeCostEntry cost in costs)
                consume?.Invoke(cost.itemId, cost.count);
            return true;
        }

        public bool UnlockHangUp(OfflineHomeIncomeSession session, int hangUpId, HomeConditionContext context,
            string receipt)
        {
            HomeHangUpRecord record = rules.HangUp(hangUpId);
            if (record == null || session.IsHangUpUnlocked(hangUpId)) return false;
            if (!rules.IsMet(record.unlock, context)) return false;
            return session.Mutate(receipt, save =>
            {
                OfflineHomeHangUpEntry entry = save.HangUps.FirstOrDefault(value => value.HangUpId == hangUpId);
                if (entry == null)
                {
                    entry = new OfflineHomeHangUpEntry { HangUpId = hangUpId };
                    save.HangUps.Add(entry);
                }
                entry.Unlocked = true;
                entry.Level = Math.Max(1, entry.Level);
            });
        }

        public int HangUpLevel(OfflineHomeIncomeSession session, HomeHangUpRecord record, int blessingLevel)
        {
            if (record == null) return 1;
            return record.useBlessingLevel ? Math.Max(1, blessingLevel) : session.HangUpLevel(record.id);
        }

        public HomeLevelUpResult LevelUp(OfflineHomeIncomeSession session, int hangUpId,
            HomeConditionContext context, int blessingLevel, Func<int, long> itemCount,
            Action<int, long> consume, string receipt)
        {
            var result = new HomeLevelUpResult();
            HomeHangUpRecord record = rules.HangUp(hangUpId);
            if (record == null) { result.Error = "unknown_hang_up"; return result; }
            if (!record.canLevelUp) { result.Error = "not_levelable"; return result; }
            if (!session.IsHangUpUnlocked(hangUpId)) { result.Error = "locked"; return result; }
            if (!rules.IsMet(record.unlock, context)) { result.Error = "condition"; return result; }
            int level = session.HangUpLevel(hangUpId);
            int maximum = rules.MaxHangUpLevel(hangUpId);
            if (level >= maximum) { result.Error = "max_level"; result.Level = level; return result; }
            int homeLimit = rules.FurnitureLevelUpLimit(blessingLevel);
            if (level >= homeLimit) { result.Error = "home_level"; result.Level = level; return result; }
            HomeHangUpUpgradeRecord upgrade = rules.Upgrade(hangUpId, level + 1);
            var costs = (upgrade?.cost ?? Array.Empty<HomeCostEntry>())
                .Where(cost => cost != null && cost.itemId > 0 && cost.count > 0).ToList();
            result.Costs = costs;
            foreach (HomeCostEntry cost in costs)
                if (itemCount == null || itemCount(cost.itemId) < cost.count) { result.Error = "items"; return result; }
            if (!session.Mutate(receipt, save =>
                {
                    OfflineHomeHangUpEntry entry = save.HangUps.FirstOrDefault(value => value.HangUpId == hangUpId);
                    if (entry == null) throw new InvalidOperationException("The hang-up is not unlocked.");
                    entry.Level = level + 1;
                })) { result.Error = "save"; return result; }
            foreach (HomeCostEntry cost in costs) consume?.Invoke(cost.itemId, cost.count);
            result.Success = true;
            result.Level = level + 1;
            return result;
        }

        /// <summary>Builds the income catalog from the current furniture and hang-up state.</summary>
        public OfflineHomeIncomeDefinition[] BuildIncomeDefinitions(OfflineHomeIncomeSession session,
            HomeConditionContext context, int blessingLevel)
        {
            var result = new List<OfflineHomeIncomeDefinition>();
            foreach (HomeHangUpRecord record in rules.HangUps)
            {
                if (record.levelRuleId <= 0) continue;
                HomeHangUpRewardRecord reward = rules.HangUpReward(record.rewardId);
                if (reward == null) continue;
                int level = HangUpLevel(session, record, blessingLevel);
                double levelNumber = rules.LevelNumber(record.levelRuleId, level);
                if (levelNumber <= 0) levelNumber = 1;
                HomeRewardRuleRecord rule = rules.SelectRewardRule(reward.rewardRuleId, context);
                if (rule == null || rule.ids == null || rule.ids.Length == 0) continue;
                bool autoUnlocked = record.initAutoUnlock || record.unlock == null
                    || record.unlock._type == "NoneCondition" || record.unlock._type == "AlwaysCondition";
                bool unlocked = (session.IsHangUpUnlocked(record.id) || autoUnlocked) && rules.IsMet(record.unlock, context);
                if (record.bindFurniture >= 0 && !session.OwnsFurniture(record.bindFurniture)) unlocked = false;
                HomeFastHangUpRecord fast = rules.FastHangUp(record.fastHangUpId);
                result.Add(new OfflineHomeIncomeDefinition
                {
                    HangUpId = record.id,
                    OutputItemId = rule.ids[0],
                    OutputItemIds = rule.ids,
                    OutputWeights = rule.weights,
                    FastHangUpId = record.fastHangUpId,
                    FreeFastForwardLimit = fast?.freeTimes ?? 0,
                    FreeFastForwardHours = fast?.freeHours ?? 0,
                    Unlocked = unlocked,
                    Rule = new HangUpIncomeRule
                    {
                        EffectPercentPropertyId = record.effectPercentProp,
                        EffectFixedPropertyId = record.effectFixedProp,
                        FinallyEffectPercentPropertyId = record.finallyEffectPercentProp,
                        IsCart = record.bindFurniture == 10,
                        HourlyOutput = reward.hourlyOutput,
                        LevelNum = levelNumber,
                        MaxStoreHours = Math.Max(1, rules.Data.furnitureLevelRules
                            .FirstOrDefault(value => value.id == record.levelRuleId && value.level == level)?.maxStoreThread
                            ?? rules.Data.furnitureLevelRules
                                .FirstOrDefault(value => value.id == record.levelRuleId)?.maxStoreThread ?? 36)
                    }
                });
            }
            return result.ToArray();
        }

        public OfflineHomeRandomRewardDefinition[] BuildRandomRewardDefinitions(OfflineHomeIncomeSession session,
            HomeConditionContext context)
        {
            HomeRewardTypesData data = rules.Rewards;
            if (data == null) return Array.Empty<OfflineHomeRandomRewardDefinition>();
            var result = new List<OfflineHomeRandomRewardDefinition>();
            foreach (HomeRewardSlotRecord slot in data.slots)
            {
                HomeRewardGroupRecord group = (slot.groupIds ?? Array.Empty<int>())
                    .Select(id => data.groups.FirstOrDefault(value => value.groupId == id))
                    .FirstOrDefault(value => value != null);
                if (group == null) continue;
                HomeRewardTypeRecord type = data.types.FirstOrDefault(value => value.type == group.type);
                if (type == null) continue;
                var rewards = new List<OfflineHomeRewardChoice>();
                foreach (HomeRewardRuleRecord rule in rules.Data.rewardRules.Where(value => value.id == group.ruleId)
                             .OrderBy(value => value.order))
                {
                    if (!rules.IsMet(rule.condition, context)) continue;
                    for (int index = 0; index < rule.ids.Length; index++)
                    {
                        int weight = index < rule.weights.Length ? rule.weights[index] : 1000;
                        double count = index < rule.counts.Length ? rule.counts[index] : 1;
                        if (weight <= 0 || count <= 0) continue;
                        rewards.Add(new OfflineHomeRewardChoice
                        {
                            ItemId = rule.ids[index], Count = Math.Max(1, (long)Math.Round(count)), Weight = weight
                        });
                    }
                    if (rewards.Count > 0) break;
                }
                if (rewards.Count == 0) continue;
                bool unlocked = slot.furnitureLimit <= 0 || slot.furnitureLimit >= 10000
                    || (context?.ItemCount(slot.furnitureLimit) ?? 0) > 0;
                result.Add(new OfflineHomeRandomRewardDefinition
                {
                    Slot = slot.slotId,
                    Type = group.type,
                    MinimumIntervalSeconds = type.minInterval,
                    MaximumIntervalSeconds = type.maxInterval,
                    MaximumCount = type.maxCount,
                    Unlocked = unlocked,
                    Rewards = rewards
                });
            }
            return result.ToArray();
        }

        public bool EquipSkin(OfflineHomeIncomeSession session, int category, int skinItemId, string receipt)
        {
            bool valid = category switch
            {
                0 => rules.Data.furnitureSkins.Any(value => value.skinItemId == skinItemId),
                1 => rules.Data.themeSkins.Any(value => value.skinItemId == skinItemId),
                2 => rules.Data.environmentSkins.Any(value => value.skinItemId == skinItemId),
                _ => false
            };
            if (!valid) return false;
            return session.Mutate(receipt, save =>
            {
                OfflineHomeSkinSelection entry = save.Skins.FirstOrDefault(value => value.Category == category);
                if (entry == null)
                {
                    entry = new OfflineHomeSkinSelection { Category = category };
                    save.Skins.Add(entry);
                }
                entry.SkinItemId = skinItemId;
            });
        }
    }
}
