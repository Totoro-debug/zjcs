using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Editor
{
    public static class ActivityDomainBossPageDataAcceptance
    {
        public static void Run()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(output) || !Directory.Exists(output))
                throw new InvalidOperationException("Use an existing isolated validation workspace.");
            string path = Path.Combine(output, "activity-page-progress-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ProjectTools.Validate();
                var catalog = OfficialDailyChallengeCatalog.LoadDefault();
                CheckRewards(catalog);
                CheckProgress(catalog, path);
                Debug.Log("[Activity page data] Original fixed/probability previews, real 209/212/226 tier rewards, detached progress, settlement/day/period refresh, cold reload and clock rollback passed.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        }

        private static void CheckRewards(OfficialDailyChallengeCatalog catalog)
        {
            Check(catalog.PreviewBossDifficultyRewards(209, 0), (3006, 100), (3007, 1), (23, 1));
            Check(catalog.PreviewBossDifficultyRewards(212, 0), (23, 1), (106, 1), (3025, 60));
            Check(catalog.PreviewBossDifficultyRewards(226, 0), (23, 2), (106, 3));
            var drop = catalog.FindRewardRule(65250);
            Check(OfficialChallengeRewardPreview.Read(new[] { drop }),
                (2, 0), (302, 0), (118, 0), (107, 0), (110, 0), (304, 0));
            // Repeated candidates use max within one rule; independent fixed
            // rules add, while any probabilistic component hides the count.
            var repeated = Rule(1f, new[] { 7, 7 }, new[] { 100, 0 }, new[] { 3.9f, 90f });
            Check(OfficialChallengeRewardPreview.Read(new[] { repeated, Rule(1f, new[] { 7 }, new[] { 1 }, new[] { 4f }) }), (7, 7));
            Check(OfficialChallengeRewardPreview.Read(new[] { repeated, Rule(.5f, new[] { 7 }, new[] { 1 }, new[] { 9f }) }), (7, 0));
            Check(OfficialChallengeRewardPreview.Read(new[] { Rule(0f, new[] { 7 }, new[] { 1 }, new[] { 9f }) }));
            foreach (var tier in catalog.BossDifficulties)
            {
                var preview = catalog.PreviewBossDifficultyRewards(tier.ActivityId, tier.Difficulty);
                var rolled = catalog.ResolveBossDifficultyRewards(tier.ActivityId, tier.Difficulty, 9182)
                    .GroupBy(row => row.ItemId).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
                Require(preview.Count == rolled.Count && preview.All(pair => rolled[pair.Key] == pair.Value),
                    "A configured deterministic boss tier preview disagrees with its reward transaction.");
            }
        }

        private static OfficialChallengeRewardRule Rule(float percent, int[] ids, int[] weights, float[] counts)
            => new OfficialChallengeRewardRule { Percent = percent, IdList = ids, WeightList = weights, CountList = counts };
        private static void Check(IReadOnlyDictionary<int, long> actual, params (int id, long count)[] expected)
            => Require(actual.Count == expected.Length && expected.All(pair => actual.TryGetValue(pair.id, out long count) && count == pair.count),
                "Original reward preview differs from the expected item/count display.");

        private static void CheckProgress(OfficialDailyChallengeCatalog catalog, string path)
        {
            DateTime start = new DateTime(2032, 1, 2, 10, 0, 0, DateTimeKind.Local);
            var saved = new DailyChallengeSave { firstEnteredLocalTicks = start.Ticks, highestObservedLocalTicks = start.Ticks };
            saved.activities.Add(new DailyChallengeActivitySave
            {
                activityId = 209, logicDay = start.Date.AddHours(6).Ticks.ToString(), participated = true,
                highestDifficulty = 1, dailyChallengeAwardCount = 1,
                claimedBossDifficulties = new List<int> { 0 }
            });
            File.WriteAllText(path, JsonUtility.ToJson(saved));
            var service = new OfflineDailyChallengeService(path);
            var first = service.ReadDomainBossPage(209, catalog, start);
            Require(first.ChallengeCount == 1 && first.DailyAwardCount == 1 && !first.CanChallenge
                && first.Difficulty == 1 && first.AwardedDifficulties.SequenceEqual(new[] { 0 }), "Legacy participation or claim data was lost.");
            var settlement = service.ReadDomainBossPage(209, catalog, start.Date.AddDays(1).AddHours(5));
            Require(!settlement.CanChallenge && settlement.ChallengeUnavailableReason == "活动结算中"
                && settlement.ChallengeCount == 1, "05:00 settlement changed daily participation.");
            var reset = service.ReadDomainBossPage(209, catalog, start.Date.AddDays(1).AddHours(6));
            Require(reset.CanChallenge && reset.ChallengeCount == 0 && reset.DailyAwardCount == 0
                && reset.Difficulty == 1 && reset.AwardedDifficulties.Count == 1, "06:00 reset lost period progress.");
            DateTime nextPeriod = start.AddHours(catalog.DurationHours(209));
            var fresh = service.ReadDomainBossPage(209, catalog, nextPeriod);
            Require(fresh.PeriodStart == nextPeriod && fresh.Difficulty == 0 && fresh.AwardedDifficulties.Count == 0,
                "New period retained old tier claims.");
            Require(first.AwardedDifficulties.Count == 1 && first.Difficulty == 1 && first.ChallengeCount == 1,
                "A previously returned page snapshot changed after refresh.");
            var cold = new OfflineDailyChallengeService(path).ReadDomainBossPage(209, catalog, start);
            Require(cold.PeriodStart == fresh.PeriodStart && cold.Difficulty == 0 && cold.AwardedDifficulties.Count == 0,
                "Cold load or clock rollback resurrected a previous period.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
