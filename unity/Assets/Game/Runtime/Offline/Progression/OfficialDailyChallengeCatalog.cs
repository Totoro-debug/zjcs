using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfficialDailyChallengeData
    {
        public string manifest;
        public List<OfficialDomainBossActivity> activity_domain_boss = new List<OfficialDomainBossActivity>();
        public List<OfficialDomainBossDifficulty> activity_domain_boss_difficulty = new List<OfficialDomainBossDifficulty>();
        public List<OfficialDomainBossPropCorrection> activity_domain_boss_prop_correction = new List<OfficialDomainBossPropCorrection>();
        public List<OfficialFunnyBossActivity> activity_domain_funny_boss = new List<OfficialFunnyBossActivity>();
        public List<OfficialActivityPeriod> activity = new List<OfficialActivityPeriod>();
        public List<OfficialChallengeChest> chest = new List<OfficialChallengeChest>();
        public List<OfficialChallengeStepAward> step_award = new List<OfficialChallengeStepAward>();
        public List<OfficialChallengeRewardRule> reward_rule = new List<OfficialChallengeRewardRule>();
    }

    [Serializable] public sealed class OfficialDomainBossActivity
    {
        public int ActivityId, StageId, MapClassId, MaxDailyChallengeCount, MaxDailyChallengeAwardCount, MaxDailyKillCount;
        public OfficialChallengeProperty[] DailyChallengeAwards;
    }

    [Serializable] public sealed class OfficialDomainBossDifficulty
    {
        public int ActivityId, Difficulty;
        public int[] AwardRuleIds;
        public OfficialChallengeProperty[] DifficultyPropEntries;
    }

    [Serializable] public sealed class OfficialDomainBossPropCorrection
    {
        public int Rank, Level;
        public OfficialChallengeProperty[] CorrectionPropEntries;
    }

    [Serializable] public sealed class OfficialChallengeProperty
    {
        public int Id;
        public long Value;
    }

    [Serializable] public sealed class OfficialFunnyBossActivity
    {
        public int ActivityId, MapClassId, DailyStepRewardId, ChestId, StageId, DailyMaxKillCount, OuterBossHpScale;
    }

    [Serializable] public sealed class OfficialActivityPeriod
    {
        public int Id, ActivityType, Duration, PreviewDuration;
    }

    [Serializable] public sealed class OfficialChallengeChest
    {
        public int ClassId;
        public int[] RewardRules;
    }

    [Serializable] public sealed class OfficialChallengeStepAward
    {
        public int Index, Score, RewardId;
    }

    [Serializable] public sealed class OfficialChallengeRewardRule
    {
        public int Id;
        public float Percent;
        public int[] IdList;
        public int[] WeightList;
        public float[] CountList;
    }

    [Serializable] public sealed class OfficialChallengeReward
    {
        public int ItemId;
        public long Count;
    }

    public sealed class OfficialDailyChallengeCatalog
    {
        public const string ResourcePath = "DailyChallenges/daily-challenges";
        public const string ExpectedManifest = "871_156598";
        public const int FunnyBossActivityId = 217;
        private readonly OfficialDailyChallengeData data;

        private OfficialDailyChallengeCatalog(OfficialDailyChallengeData value) => data = value;
        public IReadOnlyList<OfficialDomainBossDifficulty> BossDifficulties => data.activity_domain_boss_difficulty;
        public IReadOnlyList<OfficialDomainBossPropCorrection> BossPropCorrections => data.activity_domain_boss_prop_correction;
        public IReadOnlyList<OfficialChallengeStepAward> StepAwards => data.step_award;
        public IReadOnlyList<OfficialChallengeRewardRule> RewardRules => data.reward_rule;
        public OfficialFunnyBossActivity FunnyBoss => data.activity_domain_funny_boss.Single();

        public static OfficialDailyChallengeCatalog LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new FileNotFoundException("Official daily challenge configuration is missing.");
            OfficialDailyChallengeData value = JsonUtility.FromJson<OfficialDailyChallengeData>(asset.text);
            if (value == null || value.manifest != ExpectedManifest
                || value.activity_domain_boss == null || value.activity_domain_boss.Count != 3
                || value.activity_domain_boss_difficulty == null || value.activity_domain_boss_prop_correction == null
                || value.activity_domain_funny_boss == null
                || value.activity == null || value.chest == null || value.step_award == null || value.reward_rule == null)
                throw new InvalidDataException("Official daily challenge configuration is invalid.");
            if (value.activity_domain_funny_boss.Count != 1
                || value.activity_domain_funny_boss[0].ActivityId != FunnyBossActivityId
                || value.activity_domain_boss.Any(row => row.StageId <= 0 || row.MaxDailyChallengeCount <= 0)
                || value.activity_domain_boss_difficulty.GroupBy(row => row.ActivityId + ":" + row.Difficulty)
                    .Any(group => group.Count() != 1))
                throw new InvalidDataException("Official daily challenge stage or difficulty data is inconsistent.");
            return new OfficialDailyChallengeCatalog(value);
        }

        public int DurationHours(int activityId)
        {
            OfficialActivityPeriod period = data.activity.SingleOrDefault(row => row.Id == activityId);
            if (period == null || period.Duration <= 1) throw new InvalidDataException("Activity period is missing: " + activityId);
            return period.Duration;
        }

        public int DailyLimit(int activityId)
        {
            if (activityId == FunnyBossActivityId) return int.MaxValue;
            OfficialDomainBossActivity boss = data.activity_domain_boss.SingleOrDefault(row => row.ActivityId == activityId);
            if (boss == null) throw new InvalidDataException("Domain boss activity is missing: " + activityId);
            return boss.MaxDailyChallengeCount;
        }

        public int DailyKillLimit(int activityId) => activityId == FunnyBossActivityId
            ? FunnyBoss.DailyMaxKillCount : 0;

        public int DailyAwardLimit(int activityId)
        {
            OfficialDomainBossActivity boss = data.activity_domain_boss.SingleOrDefault(row => row.ActivityId == activityId);
            return boss == null ? 0 : boss.MaxDailyChallengeAwardCount;
        }

        public int StageId(int activityId)
        {
            if (activityId == FunnyBossActivityId) return FunnyBoss.StageId;
            OfficialDomainBossActivity boss = data.activity_domain_boss.SingleOrDefault(row => row.ActivityId == activityId);
            if (boss == null) throw new InvalidDataException("Domain boss activity is missing: " + activityId);
            return boss.StageId;
        }

        public int StageType(int activityId) => activityId == FunnyBossActivityId ? 103 : 102;

        public IReadOnlyList<OfficialDomainBossDifficulty> Difficulties(int activityId) =>
            data.activity_domain_boss_difficulty.Where(row => row.ActivityId == activityId)
                .OrderBy(row => row.Difficulty).ToArray();

        public OfficialDomainBossDifficulty Difficulty(int activityId, int difficulty) =>
            data.activity_domain_boss_difficulty.SingleOrDefault(row =>
                row.ActivityId == activityId && row.Difficulty == difficulty);

        public IReadOnlyList<OfficialChallengeProperty> DailyChallengeAwards(int activityId)
        {
            OfficialDomainBossActivity boss = data.activity_domain_boss.SingleOrDefault(row => row.ActivityId == activityId);
            return boss == null ? Array.Empty<OfficialChallengeProperty>()
                : boss.DailyChallengeAwards ?? Array.Empty<OfficialChallengeProperty>();
        }

        public IReadOnlyList<OfficialChallengeProperty> BossCorrection(int rank, int level)
        {
            OfficialDomainBossPropCorrection correction = data.activity_domain_boss_prop_correction
                .SingleOrDefault(row => row.Rank == rank && row.Level == level);
            return correction == null ? Array.Empty<OfficialChallengeProperty>()
                : correction.CorrectionPropEntries ?? Array.Empty<OfficialChallengeProperty>();
        }

        public IReadOnlyList<OfficialChallengeStepAward> DoubleShadowStepAwards =>
            data.step_award.Where(row => row.Index == FunnyBoss.DailyStepRewardId).OrderBy(row => row.Score).ToArray();

        public OfficialChallengeRewardRule FindRewardRule(int id) =>
            data.reward_rule.SingleOrDefault(row => row.Id == id);

        public IReadOnlyList<OfficialChallengeReward> ResolveRewardRules(IEnumerable<int> ruleIds, int seed)
        {
            var random = new System.Random(seed);
            var rewards = new List<OfficialChallengeReward>();
            foreach (int ruleId in ruleIds ?? Array.Empty<int>())
            {
                OfficialChallengeRewardRule rule = FindRewardRule(ruleId);
                if (rule == null || rule.IdList == null || rule.WeightList == null || rule.CountList == null
                    || rule.IdList.Length == 0 || rule.IdList.Length != rule.WeightList.Length
                    || rule.IdList.Length != rule.CountList.Length || random.NextDouble() >= rule.Percent)
                    continue;
                int totalWeight = rule.WeightList.Where(value => value > 0).Sum();
                if (totalWeight <= 0) throw new InvalidDataException("Official challenge reward weights are invalid: " + ruleId);
                int roll = random.Next(totalWeight);
                int selected = -1;
                for (int i = 0; i < rule.WeightList.Length; i++)
                {
                    if (rule.WeightList[i] <= 0) continue;
                    roll -= rule.WeightList[i];
                    if (roll < 0) { selected = i; break; }
                }
                if (selected < 0 || rule.IdList[selected] <= 0 || rule.CountList[selected] <= 0)
                    throw new InvalidDataException("Official challenge reward row is invalid: " + ruleId);
                rewards.Add(new OfficialChallengeReward
                { ItemId = rule.IdList[selected], Count = (long)rule.CountList[selected] });
            }
            return rewards;
        }

        public IReadOnlyList<OfficialChallengeReward> ResolveBossDifficultyRewards(int activityId, int difficulty,
            int seed) => ResolveRewardRules(Difficulty(activityId, difficulty)?.AwardRuleIds, seed);

        public IReadOnlyList<OfficialChallengeReward> ResolveDoubleShadowStepRewards(IEnumerable<int> scores,
            int seed)
        {
            HashSet<int> selected = new HashSet<int>(scores ?? Array.Empty<int>());
            return ResolveRewardRules(DoubleShadowStepAwards.Where(row => selected.Contains(row.Score))
                .Select(row => row.RewardId), seed);
        }

        public IReadOnlyList<OfficialChallengeReward> RollDoubleShadowChest(int seed)
        {
            OfficialChallengeChest chest = data.chest.SingleOrDefault(row => row.ClassId == FunnyBoss.ChestId);
            if (chest == null) throw new InvalidDataException("Official double shadow chest is missing.");
            return ResolveRewardRules(chest.RewardRules, seed);
        }
    }
}
