using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static class DailyDungeonModuleRuleAcceptance
    {
        [MenuItem("Zjcs/Validation/Daily Dungeon Module Rules")]
        public static void Run()
        {
            try
            {
                ProjectTools.Validate();
                RunRules();
                Debug.Log("Daily dungeon module rule acceptance passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void RunRules()
        {
            string validationRoot = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repositoryRoot = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrWhiteSpace(validationRoot) || string.IsNullOrWhiteSpace(repositoryRoot))
                throw new InvalidOperationException("Daily dungeon rules require an isolated validation workspace.");

            string allowedRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "output", "validation"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string actualRoot = Path.GetFullPath(validationRoot)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!actualRoot.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Daily dungeon validation saves must stay under output/validation.");

            var slots = new ProfileSlotCatalog(Path.Combine(actualRoot, "profiles"));
            string slotId = slots.CreateSlot();
            DailyDungeonRuntimeData catalog = OfflineDailyDungeonSession.LoadCatalog();
            DailyDungeonRuntimeCondition dayCondition = (catalog.conditions ?? Array.Empty<DailyDungeonRuntimeCondition>())
                .FirstOrDefault(value => value != null && value.type == "ServerRankUnlockDaysCondition" && value.days > 0);
            if (dayCondition == null)
                throw new InvalidOperationException("No official server-rank unlock day condition is available for rule acceptance.");
            string createdAtPath = Path.Combine(slots.GetSlotDirectory(slotId), "profile-created-at-unix-seconds");
            long nowUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            File.WriteAllText(createdAtPath,
                (nowUnixSeconds - dayCondition.days * 86400L + 60L).ToString(CultureInfo.InvariantCulture));
            OfflineDailyDungeonModule module = OfflineDailyDungeonModule.Open(slots, slotId, catalog);
            if (module.IsConditionMet(dayCondition.id, 0, 0, 0, null, null, null))
                throw new InvalidOperationException("A rank unlock became available before the configured profile age.");
            File.WriteAllText(createdAtPath,
                (nowUnixSeconds - dayCondition.days * 86400L).ToString(CultureInfo.InvariantCulture));
            module = OfflineDailyDungeonModule.Open(slots, slotId, catalog);
            if (!module.IsConditionMet(dayCondition.id, 0, 0, 0, null, null, null))
                throw new InvalidOperationException("A rank unlock stayed locked after the configured profile age.");
            int initialClaims = module.RemainEntries;
            long initialAchievementProgress = module.DailyDungeonGetRewardCount;
            if (initialClaims != module.Catalog.settings.DailyDungeonDailyFreeCount)
                throw new InvalidOperationException("A new profile did not receive the official daily free claim count.");

            string migrationSlotId = slots.CreateSlot();
            var legacyClaimState = new DailyDungeonSave
            {
                claimStateVersion = 2,
                localDate = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                remainingClaims = 3,
                purchasedEntries = 1,
                purchasedAvailableEntries = 1,
                giftedAvailableClaims = 2
            };
            File.WriteAllText(Path.Combine(slots.GetSlotDirectory(migrationSlotId), "daily-dungeon-save.json"),
                JsonUtility.ToJson(legacyClaimState));
            OfflineDailyDungeonModule migratedClaims = OfflineDailyDungeonModule.Open(
                slots, migrationSlotId, catalog);
            if (migratedClaims.Save.claimStateVersion != 3
                || migratedClaims.Save.giftedAvailableClaims != 0
                || migratedClaims.RemainEntries != Math.Min(3, catalog.settings.DailyDungeonMaxLimitCount))
                throw new InvalidOperationException("Legacy gift fields were not cleared while preserving the claim pool.");

            DailyDungeonRuntimeReward probabilityRule = (catalog.rewardRules
                    ?? Array.Empty<DailyDungeonRuntimeReward>())
                .FirstOrDefault(value => value != null && value.Percent > 0f
                    && value.WeightList != null && value.WeightList.Any(weight => weight > 0));
            if (probabilityRule == null)
                throw new InvalidOperationException("No official reward rule is available for probability acceptance.");
            int probabilityCandidate = Array.FindIndex(probabilityRule.WeightList, weight => weight > 0);
            long totalWeight = probabilityRule.WeightList.Where(weight => weight > 0)
                .Sum(weight => (long)weight);
            float expectedProbability = probabilityRule.Percent
                * probabilityRule.WeightList[probabilityCandidate] / totalWeight;
            if (Math.Abs(module.RewardProbability(probabilityRule.Id, probabilityCandidate)
                - expectedProbability) > 0.000001f)
                throw new InvalidOperationException("Reward probability did not use the official Percent and weight formula.");
            if (Math.Abs(DailyDungeonRewardDoubleChance.Probability(null) - 0.05f) > 0.000001f
                || Math.Abs(DailyDungeonRewardDoubleChance.Probability(new DailyDungeonRewardDoubleChanceSources
                    {
                        SeasonAstrologyContractPercent = 20f,
                        AncientRelicLuckyStatuePercent = 30f,
                        EquipmentAffixPercent = 45f
                    }) - 1f) > 0.000001f
                || Math.Abs(DailyDungeonRewardDoubleChance.Probability(new DailyDungeonRewardDoubleChanceSources
                    {
                        SeasonAstrologyContractPercent = 200f
                    }) - 1f) > 0.000001f)
                throw new InvalidOperationException("Daily dungeon grade reward double chance did not use the 5% base, additive sources, and 100% cap.");

            DailyDungeonRuntimeBattle selectedDungeon = null;
            DailyDungeonRuntimeDifficulty selectedDifficulty = null;
            List<int> route = null;
            foreach (DailyDungeonRuntimeBattle dungeon in module.PublicDungeons((_, __) => true))
            {
                foreach (DailyDungeonRuntimeDifficulty difficulty in dungeon.difficulties
                    ?? Array.Empty<DailyDungeonRuntimeDifficulty>())
                {
                    DailyDungeonLevelDefinition level = module.ResolveLevel(dungeon.id, difficulty.difficulty);
                    DailyDungeonRuntimeMap map = module.FindMap(difficulty.mapGroupId);
                    if (difficulty.difficulty != 0 || level == null || !level.SupportsLeaderboard
                        || map == null || !(map.gradeRows ?? Array.Empty<DailyDungeonRuntimeGradeRow>())
                            .Any(value => value != null && value.difficulty == difficulty.difficulty && value.grade > 0))
                        continue;
                    List<int> candidate = FindTerminalRoute(module, map.startRoomId);
                    if (candidate == null) continue;
                    selectedDungeon = dungeon;
                    selectedDifficulty = difficulty;
                    route = candidate;
                    break;
                }
                if (selectedDungeon != null) break;
            }
            if (selectedDungeon == null || selectedDifficulty == null || route == null)
                throw new InvalidOperationException("No public official daily dungeon route with grade rows is available for rule acceptance.");
            DailyDungeonHudModel initialHud = module.BuildHudModel(
                selectedDungeon.id, selectedDifficulty.difficulty);
            if (initialHud.DailyDungeonGetRewardCount != initialAchievementProgress)
                throw new InvalidOperationException("The shared daily dungeon HUD model omitted lifetime claim progress.");

            string errorMessage;
            if (!module.TryBegin(selectedDungeon.id, selectedDifficulty.difficulty,
                _ => true, (_, __) => true, (_, __, ___) => true,
                module.Catalog.settings.DailyDungeonBuyCostItemId,
                DateTimeOffset.Now.ToUnixTimeSeconds(), out errorMessage))
                throw new InvalidOperationException("Could not begin the uncharged daily dungeon route: " + errorMessage);
            if (module.RemainEntries != initialClaims)
                throw new InvalidOperationException("Entering a daily dungeon consumed a claim.");
            if (!module.Leave((_, __) => { }))
                throw new InvalidOperationException("The failed-run route could not be left.");
            if (module.RemainEntries != initialClaims
                || module.DailyDungeonGetRewardCount != initialAchievementProgress)
                throw new InvalidOperationException("Leaving an unclaimed route consumed a claim or advanced the claim achievement.");

            if (!module.TryBegin(selectedDungeon.id, selectedDifficulty.difficulty,
                _ => true, (_, __) => true, (_, __, ___) => true,
                module.Catalog.settings.DailyDungeonBuyCostItemId,
                DateTimeOffset.Now.ToUnixTimeSeconds(), out errorMessage))
                throw new InvalidOperationException("Could not begin the successful route: " + errorMessage);
            for (int index = 0; index < route.Count; index++)
            {
                if (module.ActiveRun.currentRoomId != route[index]
                    || !module.RecordRoomCleared(route[index], string.Empty))
                    throw new InvalidOperationException("The official test route could not record room " + route[index] + ".");
                if (index + 1 < route.Count && !module.ChooseNextRoom(route[index + 1]))
                    throw new InvalidOperationException("The official test route could not choose room " + route[index + 1] + ".");
            }
            int grade = module.CurrentGrade();
            if (grade <= 0 || !module.Finish(grade))
                throw new InvalidOperationException("The official map grade row did not finish the route.");
            if (module.RemainEntries != initialClaims
                || module.DailyDungeonGetRewardCount != initialAchievementProgress)
                throw new InvalidOperationException("Completing a route without claiming consumed a claim or advanced the claim achievement.");

            DailyDungeonPendingClaimSave pending = module.PendingClaims.Single();
            DailyDungeonRuntimeGradeReward gradeReward = module.FindMap(pending.run.mapGroupId)?.gradeRewards
                ?.FirstOrDefault(value => value != null && value.difficulty == pending.run.difficulty
                    && value.grade == pending.run.grade);
            DailyDungeonRuntimeReward gradeRule = (catalog.rewardRules ?? Array.Empty<DailyDungeonRuntimeReward>())
                .FirstOrDefault(value => value != null && gradeReward?.rewardRuleIds?.Contains(value.Id) == true
                    && value.IdList != null && value.WeightList != null && value.CountList != null
                    && value.IdList.Length == value.WeightList.Length && value.IdList.Length == value.CountList.Length);
            if (gradeRule == null)
                throw new InvalidOperationException("The selected official grade has no reward rule for doubling acceptance.");
            int candidateRolls = 0;
            for (int index = 0; index < gradeRule.IdList.Length; index++)
            {
                if (gradeRule.IdList[index] > 0 && gradeRule.WeightList[index] > 0
                    && gradeRule.CountList[index] > 0)
                {
                    float probability = module.RewardProbability(gradeRule.Id, index);
                    if (probability > 0f && probability < 1f) candidateRolls++;
                }
            }
            module.Options.RewardDoubleChanceAdapter = new AcceptanceDoubleChanceAdapter(
                new DailyDungeonRewardDoubleChanceSources());
            float baseDoubleProbability = module.GradeRewardDoubleProbability(pending.run, gradeRule.Id);
            DailyDungeonRewardRoll baseGradeReward = module.RollRewards(pending.run, true, 40, 0,
                _ => true, "daily-dungeon-grade-double-base", 1, new[] { gradeRule.Id },
                new AcceptanceRewardRandom(candidateRolls));
            module.Options.RewardDoubleChanceAdapter = new AcceptanceDoubleChanceAdapter(
                new DailyDungeonRewardDoubleChanceSources { EquipmentAffixPercent = 95f });
            float cappedDoubleProbability = module.GradeRewardDoubleProbability(pending.run, gradeRule.Id);
            DailyDungeonRewardRoll doubledGradeReward = module.RollRewards(pending.run, true, 40, 0,
                _ => true, "daily-dungeon-grade-double-capped", 1, new[] { gradeRule.Id },
                new AcceptanceRewardRandom(candidateRolls));
            long baseGradeRewardCount = baseGradeReward.Items.Sum(value => value.Count);
            long doubledGradeRewardCount = doubledGradeReward.Items.Sum(value => value.Count);
            if (Math.Abs(baseDoubleProbability - 0.05f) > 0.000001f || cappedDoubleProbability != 1f
                || baseGradeRewardCount <= 0 || doubledGradeRewardCount != baseGradeRewardCount * 2)
                throw new InvalidOperationException("The confirmed chance did not double only the selected official grade reward rule.");
            module.Options.RewardDoubleChanceAdapter = null;
            if (module.ClaimPendingReward(pending.id, (_, __, ___) => false, 1))
                throw new InvalidOperationException("A failed reward transaction was reported as claimed.");
            if (module.RemainEntries != initialClaims || module.Save.remainingClaims != initialClaims
                || module.DailyDungeonGetRewardCount != initialAchievementProgress)
                throw new InvalidOperationException("A failed reward transaction consumed/reserved a claim or advanced the claim achievement.");

            IReadOnlyList<DailyDungeonLeaderboardEntry> records = module.Leaderboard(
                selectedDungeon.id, selectedDifficulty.difficulty, grade);
            if (records.Count != 1 || !records[0].IsSelf || records[0].IsNpc)
                throw new InvalidOperationException("The completed route did not produce a local personal leaderboard record.");

            int grantedAwardTimes = 0;
            if (!module.ClaimPendingReward(pending.id, (_, __, awardTimes) =>
                {
                    grantedAwardTimes = awardTimes;
                    return true;
                }, 1)
                || grantedAwardTimes != 1 || module.RemainEntries != initialClaims - 1
                || module.DailyDungeonGetRewardCount != initialAchievementProgress + 1
                || module.BuildHudModel(selectedDungeon.id, selectedDifficulty.difficulty)
                    .DailyDungeonGetRewardCount != initialAchievementProgress + 1)
                throw new InvalidOperationException("A successful reward transaction did not consume one claim and advance the achievement count once.");

            long[] prices = module.Catalog.settings.DailyDungeonBuyCostCountList ?? Array.Empty<long>();
            if (prices.Length == 0 || module.ClaimCapacity < 2)
                throw new InvalidOperationException("The official config does not allow the expected purchase rule check.");
            var economy = new AcceptanceEconomy();
            int beforePurchase = module.RemainEntries;
            if (!module.TryPurchaseEntries("daily-dungeon-rule-test-purchase", 1, economy)
                || economy.Calls != 1 || economy.ItemId != module.Catalog.settings.DailyDungeonBuyCostItemId
                || economy.Count != prices[0] || module.RemainEntries != beforePurchase + 1)
                throw new InvalidOperationException("Official purchase pricing or claim-pool accounting failed.");

            OfflineDailyDungeonModule reloaded = OfflineDailyDungeonModule.Open(slots, slotId);
            if (reloaded.RemainEntries != module.RemainEntries
                || reloaded.DailyDungeonGetRewardCount != module.DailyDungeonGetRewardCount
                || reloaded.Save.purchasedAvailableEntries != module.Save.purchasedAvailableEntries)
                throw new InvalidOperationException("Daily claim or purchase state did not survive reload.");

            DailyDungeonAchievementMilestone firstAchievement = module.AchievementRules.Find(0);
            DailyDungeonAchievementMilestone cycleSeed = module.AchievementRules.Find(3);
            DailyDungeonAchievementMilestone firstCycle = module.AchievementRules.Find(4);
            DailyDungeonAchievementMilestone lastFirstCycle = module.AchievementRules.Find(7);
            if (firstAchievement?.RequiredCount != 5 || cycleSeed?.RequiredCount != 20
                || firstCycle?.RequiredCount != 25 || lastFirstCycle?.RequiredCount != 40)
                throw new InvalidOperationException("Official daily dungeon achievement thresholds or cycle increments changed.");
            int rewardGrantCalls = 0;
            if (module.ClaimDailyDungeonAchievement(0, (_, __) =>
                {
                    rewardGrantCalls++;
                    return true;
                }) || rewardGrantCalls != 0)
                throw new InvalidOperationException("A daily dungeon achievement reward was granted below its claim threshold.");
            module.Save.lifetimeClaimedRewardTimes = cycleSeed.RequiredCount;
            if (!module.ClaimDailyDungeonAchievement(cycleSeed.Index, (receipt, rewards) =>
                {
                    rewardGrantCalls++;
                    return receipt == "daily-dungeon:achievement:3" && rewards.Count == 2
                        && rewards.Any(value => value.ItemId == 1134 && value.Count == 1)
                        && rewards.Any(value => value.ItemId == 103 && value.Count == 3);
                }) || rewardGrantCalls != 1)
                throw new InvalidOperationException("The official daily dungeon achievement reward transaction failed.");
            if (module.ClaimDailyDungeonAchievement(cycleSeed.Index, (_, __) =>
                {
                    rewardGrantCalls++;
                    return true;
                }) || rewardGrantCalls != 1)
                throw new InvalidOperationException("A daily dungeon achievement reward was granted more than once.");
            reloaded = OfflineDailyDungeonModule.Open(slots, slotId);
            if (!reloaded.Save.claimedAchievementIndexes.Contains(cycleSeed.Index)
                || reloaded.DailyDungeonGetRewardCount != cycleSeed.RequiredCount)
                throw new InvalidOperationException("Claimed daily dungeon achievement state did not survive reload.");
        }

        private static List<int> FindTerminalRoute(OfflineDailyDungeonModule module, int startRoomId)
        {
            var parents = new Dictionary<int, int> { [startRoomId] = startRoomId };
            var queue = new Queue<int>();
            queue.Enqueue(startRoomId);
            int terminalRoomId = 0;
            while (queue.Count > 0)
            {
                int roomId = queue.Dequeue();
                DailyDungeonRuntimeRoom room = module.FindRoom(roomId);
                if (room == null) continue;
                if (room.nextRoomIds != null && room.nextRoomIds.Length == 0)
                {
                    terminalRoomId = roomId;
                    break;
                }
                foreach (int nextRoomId in room.nextRoomIds ?? Array.Empty<int>())
                {
                    if (parents.ContainsKey(nextRoomId)) continue;
                    parents.Add(nextRoomId, roomId);
                    queue.Enqueue(nextRoomId);
                }
            }
            if (terminalRoomId <= 0) return null;
            var route = new List<int>();
            for (int roomId = terminalRoomId; ; roomId = parents[roomId])
            {
                route.Add(roomId);
                if (roomId == startRoomId) break;
            }
            route.Reverse();
            return route;
        }

        private sealed class AcceptanceEconomy : IDailyDungeonEconomyAdapter
        {
            public int Calls { get; private set; }
            public int ItemId { get; private set; }
            public long Count { get; private set; }

            public bool Consume(string receipt, int itemId, long count)
            {
                Calls++;
                ItemId = itemId;
                Count = count;
                return !string.IsNullOrWhiteSpace(receipt) && itemId > 0 && count > 0;
            }
        }

        private sealed class AcceptanceDoubleChanceAdapter : IDailyDungeonRewardDoubleChanceAdapter
        {
            private readonly DailyDungeonRewardDoubleChanceSources sources;

            public AcceptanceDoubleChanceAdapter(DailyDungeonRewardDoubleChanceSources sources)
            {
                this.sources = sources;
            }

            public DailyDungeonRewardDoubleChanceSources GetSources(DailyDungeonRunSave run, int rewardRuleId) => sources;
        }

        private sealed class AcceptanceRewardRandom : IDailyDungeonRandom
        {
            private readonly int acceptedRollCount;
            private int rollCount;

            public AcceptanceRewardRandom(int acceptedRollCount)
            {
                this.acceptedRollCount = acceptedRollCount;
            }

            public double NextDouble() => 0.5d;
            public float NextFloat() => 0.5f;
            public bool Roll(double probability) => rollCount++ < acceptedRollCount;
        }
    }
}
