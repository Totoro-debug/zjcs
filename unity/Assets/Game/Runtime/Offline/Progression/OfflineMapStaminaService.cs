using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class OfflineMapInteractionState
    {
        public int MapClassId, PlacementId, Generation, Attempt;
        public long AvailableAt;
        public string BattleEventId = string.Empty;
    }
    [Serializable] public sealed class OfficialMapStaminaData
    {
        public string manifest;
        public OfficialMapStaminaEntity[] staminaEntities;
        public OfficialMapStaminaRule[] rewards;
        public OfficialMapRandomReward[] randomRewards;
        public MaterialGatheringLevelNumber[] levelNumbers;
    }
    [Serializable] public sealed class OfficialMapStaminaEntity
    {
        public int EntityClassId, AdditionPropType;
        public int[] RewardRuleIds;
    }
    [Serializable] public sealed class OfficialMapStaminaRule
    {
        public int Id, Order, MapGroupId;
        public float Percent;
        public int[] IdList, WeightList, SubRankCountId, LevelCountId;
        public float[] CountList;
    }
    [Serializable] public sealed class OfficialMapRandomReward
    {
        public int Id, MinCount, MaxCount;
        public int[] IdList, WeightOfIdList;
        public bool IsCanAutoExpend;
    }

    /// <summary>
    /// Native RewardRuleInfoParser / LevelNumberParser selection and
    /// RandomRewardInfoParser expansion, with the confirmed offline fractional roll.
    /// Costs, RNG, rewards and EC receipt commit in one profile transaction.
    /// </summary>
    public sealed class OfflineMapStaminaService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialMapStaminaData data;
        public OfflineMapStaminaService(OfflineMainlineProgressSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            TextAsset asset = Resources.Load<TextAsset>("World/Mainline/interactions");
            data = asset == null ? null : JsonUtility.FromJson<OfficialMapStaminaData>(asset.text);
            if (data?.manifest != "854_156383" || data.staminaEntities?.Length != 4)
                throw new InvalidDataException("Official map stamina rules are unbound.");
        }

        public OfflineMapInteractionState State(int map, int placement)
        {
            return session.Save.World.MapInteractions?.SingleOrDefault(value => value.MapClassId == map && value.PlacementId == placement);
        }
        public bool Available(int map, int placement, long now) => (State(map, placement)?.AvailableAt ?? 0) <= now;

        public ProgressionResult StartBattle(int map, int placement, IReadOnlyDictionary<int, long> costs, long now)
        {
            session.Reload();
            var state = State(map, placement);
            if (!Available(map, placement, now)) return new ProgressionResult { Status = ProgressionResultStatus.NotReady };
            if (!string.IsNullOrEmpty(state?.BattleEventId)) return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            string receipt = "map-stamina-start:" + map + ":" + placement + ":" + (state?.Attempt ?? 0);
            return session.ApplyInventoryTransaction(receipt, save =>
            {
                Spend(save.Player, costs, now);
                var next = Get(save, map, placement);
                next.BattleEventId = receipt;
                next.Attempt++;
            });
        }

        public ProgressionResult Collect(int map, int mapGroup, int placement, int entityClass,
            IReadOnlyDictionary<int, long> costs, int level, int subRank, long additionRaw,
            long now, long nextRefresh, out IReadOnlyDictionary<int, long> awards)
        {
            session.Reload();
            var preview = new Dictionary<int, long>();
            awards = preview;
            if (!Available(map, placement, now)) return new ProgressionResult { Status = ProgressionResultStatus.NotReady };
            string receipt = "map-stamina-collect:" + map + ":" + placement + ":" + (State(map, placement)?.Generation ?? 0);
            return session.ApplyInventoryTransaction(receipt, save =>
            {
                Spend(save.Player, costs, now);
                Reward(save.Player, entityClass, mapGroup, level, subRank, additionRaw, now, preview);
                var state = Get(save, map, placement);
                state.Generation++;
                state.AvailableAt = nextRefresh;
            });
        }

        public ProgressionResult FinishBattle(int map, int mapGroup, int placement, int entityClass, bool won,
            int level, int subRank, long additionRaw, long now, long nextRefresh, out IReadOnlyDictionary<int, long> awards)
        {
            session.Reload();
            var preview = new Dictionary<int, long>();
            awards = preview;
            string battleEvent = State(map, placement)?.BattleEventId;
            if (string.IsNullOrEmpty(battleEvent)) return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            return session.ApplyInventoryTransaction(battleEvent + ":finish", save =>
            {
                var state = Get(save, map, placement);
                if (won)
                {
                    Reward(save.Player, entityClass, mapGroup, level, subRank, additionRaw, now, preview);
                    state.Generation++;
                    state.AvailableAt = nextRefresh;
                }
                state.BattleEventId = string.Empty;
            });
        }

        private static OfflineMapInteractionState Get(OfflineMainlineSave save, int map, int placement)
        {
            save.World.MapInteractions ??= new List<OfflineMapInteractionState>();
            var state = save.World.MapInteractions.SingleOrDefault(value => value.MapClassId == map && value.PlacementId == placement);
            if (state == null) save.World.MapInteractions.Add(state = new OfflineMapInteractionState { MapClassId = map, PlacementId = placement });
            return state;
        }
        private static void Spend(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> costs, long now)
        {
            foreach (var cost in costs)
                if (cost.Key <= 0 || cost.Value <= 0 || !OfflineInventoryOperations.Consume(player, cost.Key, cost.Value, now).IsApplied)
                    throw new InvalidOperationException("官方地图交互所需物品不足：" + cost.Key);
        }
        private void Reward(OfflinePlayerProgress player, int entityClass, int mapGroup, int level, int subRank,
            long additionRaw, long now, Dictionary<int, long> preview)
        {
            var entity = data.staminaEntities.Single(value => value.EntityClassId == entityClass);
            if (player.InventoryRandomState == 0) player.InventoryRandomState = 88172645463325252L;
            foreach (int ruleId in entity.RewardRuleIds)
            {
                var rule = data.rewards.Where(value => value.Id == ruleId && (value.MapGroupId == 0 || value.MapGroupId == mapGroup))
                    .OrderBy(value => value.Order == -1 ? int.MaxValue : value.Order).FirstOrDefault();
                if (rule == null) continue;
                int index = Select(rule.WeightList, ref player.InventoryRandomState);
                float count = rule.CountList[index] * Math.Max(0, rule.Percent);
                count *= Multiplier(rule.LevelCountId, index, level);
                count *= Multiplier(rule.SubRankCountId, index, subRank);
                long quantity = Fraction(count, ref player.InventoryRandomState);
                quantity = checked((long)Math.Floor(quantity * (1.0 + additionRaw / 10000.0)));
                if (quantity <= 0) continue;
                int itemId = rule.IdList[index];
                var package = data.randomRewards.SingleOrDefault(value => value.Id == itemId && value.IsCanAutoExpend);
                if (package == null) Grant(player, itemId, quantity, now, preview);
                else for (long unit = 0; unit < quantity; unit++)
                {
                    var weights = package.WeightOfIdList.Length == 0 ? Enumerable.Repeat(100, package.IdList.Length).ToArray() : package.WeightOfIdList;
                    int selected = Select(weights, ref player.InventoryRandomState);
                    long amount = package.MaxCount > 1 ? package.MinCount
                        + (long)(Next(ref player.InventoryRandomState) * (package.MaxCount - package.MinCount + 1)) : 1;
                    Grant(player, package.IdList[selected], amount, now, preview);
                }
            }
        }
        private static void Grant(OfflinePlayerProgress player, int item, long amount, long now, Dictionary<int, long> preview)
        {
            if (!OfflineInventoryOperations.Grant(player, item, amount, now).IsApplied)
                throw new InvalidOperationException("官方地图奖励无法写入背包。");
            preview[item] = checked(preview.GetValueOrDefault(item) + amount);
        }
        private float Multiplier(int[] ids, int index, int level)
        {
            if (ids == null || ids.Length <= index || ids[index] == 0) return 1;
            var values = data.levelNumbers.Single(value => value.Id == ids[index]).Nums;
            return values[Math.Max(0, Math.Min(level, values.Length - 1))];
        }
        private static long Fraction(double value, ref long state)
        {
            long whole = checked((long)Math.Floor(value));
            double fraction = value - whole;
            return fraction > .00001 && Next(ref state) < fraction ? checked(whole + 1) : whole;
        }
        private static int Select(int[] weights, ref long state)
        {
            if (weights == null || weights.Length == 0 || weights.Any(value => value < 0) || weights.Sum() <= 0)
                throw new InvalidDataException("Official map reward weights are invalid.");
            long roll = (long)(Next(ref state) * weights.Sum());
            for (int index = 0; index < weights.Length; index++) if ((roll -= weights[index]) < 0) return index;
            return weights.Length - 1;
        }
        private static double Next(ref long state)
        {
            ulong value = unchecked((ulong)state);
            value ^= value << 13; value ^= value >> 7; value ^= value << 17;
            value &= long.MaxValue;
            if (value == 0) value = 88172645463325252UL;
            state = (long)value;
            return (value >> 10) * (1.0 / 9007199254740992.0);
        }
    }
}
