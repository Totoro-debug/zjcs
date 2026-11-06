using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfficialDailyChallengeCatalog
    {
        public IReadOnlyDictionary<int, long> PreviewBossDifficultyRewards(int activityId, int difficulty)
        {
            var configured = Difficulty(activityId, difficulty)
                ?? throw new ArgumentOutOfRangeException(nameof(difficulty));
            return OfficialChallengeRewardPreview.Read(configured.AwardRuleIds.Select(FindRewardRule));
        }
    }

    // Original Common.RewardRuleInfoParser.GetRewardDropInfosByRuleList and
    // GetRewardDictByRuleList, with the selected already-decoded rule rows.
    // Their conditions and level/subrank multipliers are all empty in 871;
    // prepare_unity_daily_challenges.py verifies that supported input boundary.
    public static class OfficialChallengeRewardPreview
    {
        private struct Drop
        {
            public long Fixed, Probability;
            public long Maximum => Fixed + Probability;
            public bool IsProbability => Probability > 0;
        }

        public static IReadOnlyDictionary<int, long> Read(IEnumerable<OfficialChallengeRewardRule> rules)
        {
            var all = new Dictionary<int, Drop>();
            foreach (var rule in rules ?? Array.Empty<OfficialChallengeRewardRule>())
            {
                if (rule == null || rule.Percent == 0f) continue;
                int weightSum = 0;
                for (int i = 0; i < rule.IdList.Length; i++) weightSum += rule.WeightList[i];
                if (weightSum == 0) continue;
                var current = new Dictionary<int, Drop>();
                for (int i = 0; i < rule.IdList.Length; i++)
                {
                    int item = rule.IdList[i], weight = rule.WeightList[i];
                    float count = rule.CountList[i];
                    if (weight <= 0 || count <= 0f) continue;
                    bool probability = rule.Percent * weight / weightSum < 1f;
                    int wholeCount = (int)count;
                    if (current.TryGetValue(item, out var drop))
                    {
                        if (drop.IsProbability) drop.Probability = Math.Max(drop.Maximum, wholeCount);
                        else if (probability)
                        {
                            drop.Probability = Math.Max(drop.Maximum, wholeCount);
                            drop.Fixed = 0;
                        }
                        else drop.Fixed = Math.Max(drop.Fixed, wholeCount);
                    }
                    else if (probability) drop.Probability = wholeCount;
                    else drop.Fixed = wholeCount;
                    current[item] = drop;
                }
                foreach (var pair in current)
                {
                    all.TryGetValue(pair.Key, out var drop);
                    drop.Fixed += pair.Value.Fixed;
                    drop.Probability += pair.Value.Probability;
                    all[pair.Key] = drop;
                }
            }
            return new ReadOnlyDictionary<int, long>(all.ToDictionary(pair => pair.Key,
                pair => pair.Value.IsProbability ? 0L : pair.Value.Fixed));
        }
    }
}
