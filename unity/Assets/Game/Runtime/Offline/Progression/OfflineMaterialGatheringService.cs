using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public enum MaterialGatheringResultCode
    {
        Applied,
        Unchanged,
        Locked,
        MissingTool,
        QuickGatherLocked,
        MissingTargetRule,
        InvalidState
    }

    public sealed class MaterialGatheringAward
    {
        public int ItemId { get; internal set; }
        public long Count { get; internal set; }
    }

    public sealed class MaterialGatheringActionResult
    {
        internal MaterialGatheringActionResult(MaterialGatheringResultCode code, string message = null,
            int actionCount = 0, int targetMaterialId = 0, int remainingDurability = 0,
            IReadOnlyList<MaterialGatheringAward> rewards = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            ActionCount = actionCount;
            TargetMaterialId = targetMaterialId;
            RemainingDurability = remainingDurability;
            Rewards = rewards ?? Array.Empty<MaterialGatheringAward>();
        }

        public MaterialGatheringResultCode Code { get; }
        public string Message { get; }
        public int ActionCount { get; }
        public int TargetMaterialId { get; }
        public int RemainingDurability { get; }
        public IReadOnlyList<MaterialGatheringAward> Rewards { get; }
        public bool IsApplied => Code == MaterialGatheringResultCode.Applied;
    }

    public sealed class OfflineMaterialGatheringService
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfficialMaterialGatheringCatalog catalog;
        private readonly OfficialPropCatalog props;

        public OfflineMaterialGatheringService(OfflineMainlineProgressSession profile,
            OfficialMaterialGatheringCatalog catalog, OfficialPropCatalog props)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.props = props ?? throw new ArgumentNullException(nameof(props));
        }

        public OfflineMaterialGatheringProgress State => profile.Save.Player.MaterialGathering;

        public bool IsUnlocked(int dungeonType, Func<int, bool> stageCleared)
        {
            if (stageCleared == null || !catalog.TryGetDungeon(dungeonType, out MaterialGatheringDungeon dungeon))
                return false;
            return dungeon.UnlockStages.Any(stageCleared);
        }

        public MaterialGatheringActionResult Gather(int dungeonType, int requestedCount, bool quick,
            bool quickUnlocked, int subRank, IReadOnlyDictionary<int, long> nonBattleProps,
            string receiptId)
        {
            if (requestedCount <= 0 || requestedCount > catalog.MaximumActionCount || subRank < 0
                || nonBattleProps == null || string.IsNullOrWhiteSpace(receiptId) || receiptId.Length > 160
                || !catalog.TryGetDungeon(dungeonType, out MaterialGatheringDungeon dungeon))
                return Result(MaterialGatheringResultCode.InvalidState);
            if (quick && !quickUnlocked) return Result(MaterialGatheringResultCode.QuickGatherLocked);
            profile.Reload();
            int limit = Math.Min(requestedCount, quick ? catalog.MaximumActionCount : 1);
            var previewAwards = new Dictionary<int, long>();
            int completed = 0;
            int resultTarget = 0;
            int resultDurability = 0;
            bool missingNextTarget = false;
            ProgressionResult transaction = profile.ApplyInventoryTransaction(receiptId, save =>
            {
                OfflineMaterialGatheringProgress progress = save.Player.MaterialGathering;
                OfflineMaterialGatheringDungeonState state = FindDungeon(progress, dungeonType);
                EnsureRandomState(progress, receiptId, save.CreatedAtUnixSeconds);
                for (int index = 0; index < limit; index++)
                {
                    if (state.TargetMaterialId <= 0 && !SelectNextTarget(state, dungeon, ref progress.RandomState))
                    {
                        if (completed == 0) throw new MissingMaterialRuleException();
                        missingNextTarget = true;
                        break;
                    }
                    MaterialGatheringMaterial material = GetMaterial(state.TargetMaterialId);
                    if (!material.NeedCost)
                    {
                        AddRewards(material.BreakRewardIds, save, progress, dungeon, subRank, nonBattleProps, previewAwards);
                        state.CollectedAmount = checked(state.CollectedAmount + 1);
                        state.TargetMaterialId = 0;
                        state.RemainingDurability = 0;
                        completed++;
                        if (!SelectNextTarget(state, dungeon, ref progress.RandomState)) missingNextTarget = true;
                        if (missingNextTarget) break;
                        break;
                    }

                    if (InventoryCount(save.Player, dungeon.CostItemId) < dungeon.CostCount)
                    {
                        if (completed == 0) throw new MissingToolException();
                        break;
                    }
                    InventoryOperationResult consume = OfflineInventoryOperations.Consume(save.Player,
                        dungeon.CostItemId, dungeon.CostCount, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    if (!consume.IsApplied) throw new InvalidOperationException("采集工具不足。");
                    (long damage, bool critical) = Damage(dungeon, nonBattleProps, ref progress.RandomState);
                    AddRewards(material.AttackRewardIds, save, progress, dungeon, subRank, nonBattleProps, previewAwards);
                    state.AttackCount = checked(state.AttackCount + 1);
                    progress.TotalAttackCount = checked(progress.TotalAttackCount + 1);
                    completed++;
                    state.RemainingDurability = Math.Max(0, state.RemainingDurability - (int)Math.Min(int.MaxValue, damage));
                    if (state.RemainingDurability == 0)
                    {
                        AddRewards(material.BreakRewardIds, save, progress, dungeon, subRank, nonBattleProps, previewAwards);
                        state.CollectedAmount = checked(state.CollectedAmount + 1);
                        if (material.CanCount)
                        {
                            state.BreakCount = checked(state.BreakCount + 1);
                            progress.TotalBreakCount = checked(progress.TotalBreakCount + 1);
                            RecordUnlockStageWin(save, dungeon);
                        }
                        state.TargetMaterialId = 0;
                        if (!SelectNextTarget(state, dungeon, ref progress.RandomState)) missingNextTarget = true;
                        break;
                    }
                }
                resultTarget = state.TargetMaterialId;
                resultDurability = state.RemainingDurability;
            });
            if (transaction.Status == ProgressionResultStatus.InvalidState
                && transaction.Message == MissingMaterialRuleException.Error)
                return Result(MaterialGatheringResultCode.MissingTargetRule, transaction.Message);
            if (transaction.Status == ProgressionResultStatus.InvalidState
                && transaction.Message == MissingToolException.Error)
                return Result(MaterialGatheringResultCode.MissingTool, transaction.Message);
            if (!transaction.IsApplied)
                return Result(transaction.Status == ProgressionResultStatus.Unchanged
                    ? MaterialGatheringResultCode.Unchanged : MaterialGatheringResultCode.InvalidState,
                    transaction.Message);
            return new MaterialGatheringActionResult(MaterialGatheringResultCode.Applied,
                missingNextTarget ? "官方目标生成配置未能生成下一目标。" : transaction.Message,
                completed, resultTarget, resultDurability,
                previewAwards.Select(value => new MaterialGatheringAward { ItemId = value.Key, Count = value.Value }).ToArray());
        }

        private void AddRewards(int[] rewardIds, OfflineMainlineSave save, OfflineMaterialGatheringProgress progress,
            MaterialGatheringDungeon dungeon, int subRank, IReadOnlyDictionary<int, long> nonBattleProps,
            Dictionary<int, long> preview)
        {
            foreach (int rewardId in rewardIds ?? Array.Empty<int>())
            {
                if (!catalog.TryGetReward(rewardId, out MaterialGatheringRewardRule rule)
                    || rule.IdList == null || rule.WeightList == null || rule.CountList == null
                    || rule.IdList.Length != rule.WeightList.Length || rule.IdList.Length != rule.CountList.Length)
                    throw new InvalidOperationException("官方采集奖励配置无效: " + rewardId);
                int selected = SelectWeighted(rule.WeightList, ref progress.RandomState);
                float count = rule.CountList[selected] * Math.Max(0f, rule.Percent);
                int levelId = rule.LevelCountId != null && rule.LevelCountId.Length > selected
                    ? rule.LevelCountId[selected] : 0;
                int subRankId = rule.SubRankCountId != null && rule.SubRankCountId.Length > selected
                    ? rule.SubRankCountId[selected] : 0;
                count *= catalog.LevelMultiplier(levelId, save.Player.Level);
                count *= catalog.LevelMultiplier(subRankId, subRank);
                long baseCount = RollFraction(count, ref progress.RandomState);
                double addition = PropValue(nonBattleProps, dungeon.AddtionAwardProp);
                long finalCount = (long)Math.Floor(baseCount * (1.0 + addition));
                if (finalCount <= 0) continue;
                int itemId = rule.IdList[selected];
                InventoryOperationResult grant = OfflineInventoryOperations.Grant(save.Player, itemId, finalCount,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (!grant.IsApplied) throw new InvalidOperationException("采集奖励无法加入背包。");
                preview[itemId] = checked(preview.GetValueOrDefault(itemId) + finalCount);
            }
        }

        private (long Damage, bool Critical) Damage(MaterialGatheringDungeon dungeon,
            IReadOnlyDictionary<int, long> values, ref long randomState)
        {
            double critRate = PropValue(values, dungeon.CritRateProp);
            bool critical = NextUnit(ref randomState) <= critRate;
            double attack = PropValue(values, dungeon.AttackProp);
            float variation = 1f - catalog.DamageFloat + (float)NextUnit(ref randomState) * catalog.DamageFloat * 2f;
            attack *= variation;
            if (critical) attack *= 1.0 + PropValue(values, dungeon.CritPowerProp);
            return ((long)Math.Max(0.0, attack + 0.5), critical);
        }

        private static void RecordUnlockStageWin(OfflineMainlineSave save, MaterialGatheringDungeon dungeon)
        {
            List<OfflineStageWinCount> counts = save.World.StageWinCounts;
            int highestIndex = -1;
            for (int index = 0; index < dungeon.UnlockStages.Length; index++)
            {
                int stageId = dungeon.UnlockStages[index];
                if (counts.Any(value => value.StageId == stageId && value.Count > 0)) highestIndex = index;
            }

            int nextIndex = Math.Min(highestIndex + 1, dungeon.UnlockStages.Length - 1);
            int nextStageId = dungeon.UnlockStages[nextIndex];
            OfflineStageWinCount entry = counts.FirstOrDefault(value => value.StageId == nextStageId);
            if (entry == null)
            {
                entry = new OfflineStageWinCount { StageId = nextStageId };
                counts.Add(entry);
            }
            if (entry.Count == int.MaxValue) throw new InvalidOperationException("秘境关卡计数已达到上限。");
            entry.Count++;
        }

        private double PropValue(IReadOnlyDictionary<int, long> values, int propId)
        {
            long value = values.TryGetValue(propId, out long current) ? current : 0;
            OfficialPropDefinition definition = props.Get(propId);
            if (definition == null) return 0.0;
            return definition.Float ? value / 10000.0 : value;
        }

        private MaterialGatheringMaterial GetMaterial(int id)
        {
            return catalog.TryGetMaterial(id, out MaterialGatheringMaterial material) ? material
                : throw new InvalidOperationException("官方采集目标配置缺失: " + id);
        }

        private static OfflineMaterialGatheringDungeonState FindDungeon(OfflineMaterialGatheringProgress progress, int type)
        {
            OfflineMaterialGatheringDungeonState state = progress.Dungeons.FirstOrDefault(value => value.DungeonType == type);
            if (state != null) return state;
            progress.Dungeons.Add(state = new OfflineMaterialGatheringDungeonState { DungeonType = type });
            return state;
        }

        private bool SelectNextTarget(OfflineMaterialGatheringDungeonState state, MaterialGatheringDungeon dungeon,
            ref long randomState)
        {
            if (state.NewbieSequenceIndex < dungeon.NewbieRewardMaterials.Length)
                state.TargetMaterialId = dungeon.NewbieRewardMaterials[state.NewbieSequenceIndex++];
            else
            {
                if (!catalog.TryGetReward(dungeon.GenerateMaterialRuleId, out MaterialGatheringRewardRule rule)
                    || rule.IdList == null || rule.WeightList == null
                    || rule.IdList.Length == 0 || rule.IdList.Length != rule.WeightList.Length)
                    return false;
                state.TargetMaterialId = rule.IdList[SelectWeighted(rule.WeightList, ref randomState)];
            }
            state.RemainingDurability = GetMaterial(state.TargetMaterialId).Durability;
            return true;
        }

        private static int SelectWeighted(int[] weights, ref long randomState)
        {
            long total = 0;
            foreach (int weight in weights)
            {
                if (weight < 0) throw new InvalidOperationException("官方采集奖励权重无效。");
                total = checked(total + weight);
            }
            if (total <= 0) throw new InvalidOperationException("官方采集奖励权重为空。");
            long roll = (long)(NextUnit(ref randomState) * total);
            for (int index = 0; index < weights.Length; index++)
            {
                if (roll < weights[index]) return index;
                roll -= weights[index];
            }
            return weights.Length - 1;
        }

        private static long RollFraction(double count, ref long randomState)
        {
            long whole = (long)Math.Floor(count);
            double fraction = count - whole;
            return fraction > 0.00001 && NextUnit(ref randomState) < fraction ? checked(whole + 1) : whole;
        }

        private static long InventoryCount(OfflinePlayerProgress player, int itemId)
        {
            return player.Inventory.FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;
        }

        private static void EnsureRandomState(OfflineMaterialGatheringProgress progress, string receiptId, long createdAt)
        {
            if (progress.RandomState != 0) return;
            ulong hash = 14695981039346656037UL;
            foreach (char character in receiptId + ":" + createdAt.ToString(CultureInfo.InvariantCulture))
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
            progress.RandomState = (long)(hash % (ulong)long.MaxValue);
            if (progress.RandomState == 0) progress.RandomState = 88172645463325252L;
        }

        private static double NextUnit(ref long state)
        {
            ulong value = unchecked((ulong)state);
            value ^= value << 13;
            value ^= value >> 7;
            value ^= value << 17;
            value &= long.MaxValue;
            if (value == 0) value = 88172645463325252UL;
            state = (long)value;
            return (value >> 10) * (1.0 / 9007199254740992.0);
        }

        private static MaterialGatheringActionResult Result(MaterialGatheringResultCode code, string message = null)
            => new MaterialGatheringActionResult(code, message);

        private sealed class MissingMaterialRuleException : Exception
        {
            public const string Error = "官方目标生成配置无效，无法选择下一目标。";
            public MissingMaterialRuleException() : base(Error) { }
        }

        private sealed class MissingToolException : Exception
        {
            public const string Error = "采集工具不足。";
            public MissingToolException() : base(Error) { }
        }
    }
}
