using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class MainlineMonsterRewardStage
    {
        public int Id, MonsterAwardGroupId;
        public int[] Awards;
        public bool AwardUsePlayerLevelAndSubRank;
    }

    [Serializable] public sealed class MainlineMonsterRewardBinding
    {
        public int GroupId, MonsterId;
        public int[] RuleIds;
    }

    [Serializable] public sealed class MainlineMonsterRewardRule
    {
        public int Id, Order;
        public bool HasCondition;
        public OfficialExploreCondition Condition;
        public float Percent;
        public int[] IdList, WeightList, LevelCountId, SubRankCountId, IndexMininum;
        public string RecordSharedId;
        public float[] CountList;
    }

    [Serializable] public sealed class MainlineMonsterRewardData
    {
        public string manifest;
        public MainlineMonsterRewardStage[] stages;
        public MainlineMonsterRewardBinding[] monsterRewards;
        public MainlineMonsterRewardRule[] rewardRules;
        public OfficialExploreRandomReward[] randomRewards;
        public OfficialExploreConditionRow[] conditions;
        public MaterialGatheringLevelNumber[] levelNumbers;
        public int[] missingRewardRuleIds;
    }

    // Uses the original ordered rule selection and multiplier tables. The
    // probability roll and weighted choice are the user-confirmed offline policy.
    public sealed class OfficialMainlineMonsterRewardCatalog
    {
        public MainlineMonsterRewardData Data { get; }
        public OfficialMainlineMonsterRewardCatalog(string json)
        {
            Data = JsonUtility.FromJson<MainlineMonsterRewardData>(json);
            if (Data?.manifest != "884_156692" || Data.stages == null || Data.monsterRewards == null
                || Data.rewardRules == null || Data.randomRewards == null || Data.conditions == null || Data.levelNumbers == null
                || Data.missingRewardRuleIds == null)
                throw new InvalidDataException("Original monster reward inputs are incomplete.");
            if (Data.stages.Select(v => v.Id).Distinct().Count() != Data.stages.Length
                || Data.monsterRewards.Select(v => (v.GroupId, v.MonsterId)).Distinct().Count() != Data.monsterRewards.Length
                || Data.rewardRules.Select(v => (v.Id, v.Order)).Distinct().Count() != Data.rewardRules.Length)
                throw new InvalidDataException("Original monster reward identities are duplicated.");
            // JsonUtility materializes JSON null as an empty reference object.
            foreach (var package in Data.randomRewards)
                if (string.IsNullOrEmpty(package.RandomItemRangeSelector?._type)) package.RandomItemRangeSelector = null;
        }

        public static OfficialMainlineMonsterRewardCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("World/Mainline/monster-reward-inputs");
            return new OfficialMainlineMonsterRewardCatalog(asset?.text
                ?? throw new InvalidDataException("Original monster reward inputs are missing."));
        }

        public IReadOnlyDictionary<int, long> Roll(int stageId, IEnumerable<int> monsters, int level, int subRank,
            Func<OfficialExploreCondition, bool> condition, Func<double> random)
        {
            if (monsters == null || condition == null || random == null) throw new ArgumentNullException();
            var stage = Data.stages.SingleOrDefault(v => v.Id == stageId)
                ?? throw new InvalidDataException("Original reward stage is missing: " + stageId);
            var ids = new List<int>(stage.Awards ?? Array.Empty<int>());
            foreach (int monster in monsters)
            {
                var binding = Data.monsterRewards.SingleOrDefault(v => v.GroupId == stage.MonsterAwardGroupId
                    && v.MonsterId == monster);
                if (binding != null) ids.AddRange(binding.RuleIds ?? Array.Empty<int>());
            }
            var result = new Dictionary<int, long>();
            foreach (int id in ids)
            {
                var rules = Data.rewardRules.Where(v => v.Id == id).OrderBy(v => v.Order == -1 ? int.MaxValue : v.Order);
                var rule = rules.FirstOrDefault(v => !v.HasCondition || condition(v.Condition));
                if (rule == null) continue; // Original parser returns null for absent or unmatched rules.
                if (rule.IndexMininum?.Length > 0 || !string.IsNullOrEmpty(rule.RecordSharedId))
                    throw new InvalidDataException("Unconverted monster reward guarantee: " + id);
                if (rule.Percent < 0 || rule.Percent > 1 || rule.IdList == null || rule.WeightList == null
                    || rule.CountList == null || rule.IdList.Length != rule.WeightList.Length
                    || rule.IdList.Length != rule.CountList.Length || rule.WeightList.Any(v => v < 0))
                    throw new InvalidDataException("Invalid original monster reward rule: " + id);
                if (rule.Percent == 0 || rule.WeightList.Sum(v => (long)v) == 0) continue;
                if (rule.Percent < 1 && Next(random) >= rule.Percent) continue;
                long total = rule.WeightList.Sum(v => (long)v);
                double choice = Next(random) * total;
                int index = 0;
                for (; index < rule.WeightList.Length - 1 && (choice -= rule.WeightList[index]) >= 0; index++) { }
                double amount = rule.CountList[index] * Scale(rule.LevelCountId, index, level)
                    * Scale(rule.SubRankCountId, index, subRank);
                if (amount < 0 || double.IsNaN(amount) || double.IsInfinity(amount))
                    throw new InvalidDataException("Invalid original monster reward amount: " + id);
                long count = checked((long)Math.Floor(amount));
                if (amount > count && Next(random) < amount - count) count = checked(count + 1);
                if (count > 0)
                    result[rule.IdList[index]] = checked(result.GetValueOrDefault(rule.IdList[index]) + count);
            }
            return result;
        }

        private double Scale(int[] ids, int index, int level)
        {
            if (ids == null || ids.Length <= index || ids[index] == 0) return 1;
            var row = Data.levelNumbers.SingleOrDefault(v => v.Id == ids[index]);
            if (row?.Nums == null || row.Nums.Length == 0)
                throw new InvalidDataException("Original reward multiplier is missing: " + ids[index]);
            return row.Nums[Math.Max(0, Math.Min(level, row.Nums.Length - 1))];
        }

        private static double Next(Func<double> random)
        {
            double value = random();
            if (value < 0 || value >= 1 || double.IsNaN(value))
                throw new InvalidDataException("Reward random value is outside [0, 1).");
            return value;
        }
    }
}
