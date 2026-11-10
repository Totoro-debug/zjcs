using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static partial class DailyDungeonModuleRuleAcceptance
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
            var perCandidateCounts = new DailyDungeonRuntimeReward
            {
                LevelCountId = 13,
                LevelCountIds = new[] { 7, 9 }
            };
            var legacyCount = new DailyDungeonRuntimeReward { LevelCountId = 13 };
            if (perCandidateCounts.LevelCountIdFor(0) != 7
                || perCandidateCounts.LevelCountIdFor(1) != 9
                || legacyCount.LevelCountIdFor(0) != 13
                || legacyCount.LevelCountIdFor(1) != 13)
                throw new InvalidOperationException("Daily dungeon level-count multipliers must preserve per-candidate official data and legacy scalar catalogs.");

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
            DailyDungeonRuntimeData catalog = DailyDungeonModule.LoadCatalog();
            DailyDungeonRuntimeCondition dayCondition = (catalog.conditions ?? Array.Empty<DailyDungeonRuntimeCondition>())
                .FirstOrDefault(value => value != null && value.type == "ServerRankUnlockDaysCondition" && value.days > 0);
            if (dayCondition == null)
                throw new InvalidOperationException("No official server-rank unlock day condition is available for rule acceptance.");
            string createdAtPath = Path.Combine(slots.GetSlotDirectory(slotId), "profile-created-at-unix-seconds");
            long nowUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            File.WriteAllText(createdAtPath,
                (nowUnixSeconds - dayCondition.days * 86400L + 60L).ToString(CultureInfo.InvariantCulture));
            DailyDungeonModule module = DailyDungeonModule.Open(slots, slotId, catalog);
            DailyDungeonLevelDefinition worldTree = module.ResolveLevel(11, 0);
            if (worldTree == null || !worldTree.SupportsLeaderboard
                || !worldTree.LeaderboardGrades.SequenceEqual(new[] { 7 }))
                throw new InvalidOperationException("World Tree leaderboard tabs must use the official S-only TopRecordGradeTypeDict, not all completion grades.");
            DailyDungeonLevelDefinition privateMountain = module.ResolveLevel(121, 50);
            if (privateMountain == null || privateMountain.SupportsLeaderboard
                || privateMountain.LeaderboardGrades.Count != 0)
                throw new InvalidOperationException("Private dungeon routes must not acquire public leaderboard tabs.");
            if (module.IsConditionMet(dayCondition.id, 0, 0, 0, null, null, null))
                throw new InvalidOperationException("A rank unlock became available before the configured profile age.");
            File.WriteAllText(createdAtPath,
                (nowUnixSeconds - dayCondition.days * 86400L).ToString(CultureInfo.InvariantCulture));
            module = DailyDungeonModule.Open(slots, slotId, catalog);
            if (!module.IsConditionMet(dayCondition.id, 0, 0, 0, null, null, null))
                throw new InvalidOperationException("A rank unlock stayed locked after the configured profile age.");
            int initialClaims = module.RemainEntries;
            long initialAchievementProgress = module.DailyDungeonGetRewardCount;
            ValidateLeaderboardGifts(slots, catalog);
            if (initialClaims != module.Catalog.settings.DailyDungeonDailyFreeCount)
                throw new InvalidOperationException("A new profile did not receive the official daily free claim count.");
            if (module.MaxAwardTimesPerClaim != 1)
                throw new InvalidOperationException("A module without daily dungeon claim benefits did not use the official base award count.");

            DailyDungeonRuntimeBattle conditionLockedDungeon = (catalog.dungeons
                    ?? Array.Empty<DailyDungeonRuntimeBattle>())
                .FirstOrDefault(dungeon => dungeon?.difficulties?.Any(value => value != null
                    && value.difficulty == 0 && value.unlockConditionId > 0) == true);
            DailyDungeonRuntimeDifficulty conditionLockedDifficulty = conditionLockedDungeon?.difficulties?
                .FirstOrDefault(value => value != null && value.difficulty == 0 && value.unlockConditionId > 0);
            if (conditionLockedDungeon == null || conditionLockedDifficulty == null)
                throw new InvalidOperationException("No official first-difficulty unlock condition is available for rule acceptance.");
            DailyDungeonEntryPageModel lockedEntryPage = module.BuildEntryPageModel((_, __) => true,
                (_, conditionId) => conditionId != conditionLockedDifficulty.unlockConditionId);
            DailyDungeonEntryModel lockedDungeonEntry = lockedEntryPage.Entries.FirstOrDefault(value =>
                value != null && value.DungeonId == conditionLockedDungeon.id);
            DailyDungeonDifficultyModel lockedDifficultyEntry = lockedDungeonEntry?.Difficulties?
                .FirstOrDefault(value => value.Difficulty == conditionLockedDifficulty.difficulty);
            if (lockedDifficultyEntry == null || lockedDifficultyEntry.IsUnlocked
                || module.CanEnter(conditionLockedDungeon.id, conditionLockedDifficulty.difficulty, true,
                    _ => false))
                throw new InvalidOperationException("A public dungeon hid or entered its condition-locked first difficulty.");
            DailyDungeonEntryPageModel unlockedEntryPage = module.BuildEntryPageModel((_, __) => true,
                (_, conditionId) => conditionId == conditionLockedDifficulty.unlockConditionId);
            DailyDungeonDifficultyModel unlockedDifficultyEntry = unlockedEntryPage.Entries
                .First(value => value.DungeonId == conditionLockedDungeon.id).Difficulties
                .First(value => value.Difficulty == conditionLockedDifficulty.difficulty);
            if (!unlockedDifficultyEntry.IsUnlocked
                || !module.CanEnter(conditionLockedDungeon.id, conditionLockedDifficulty.difficulty, true,
                    conditionId => conditionId == conditionLockedDifficulty.unlockConditionId
                        || conditionId == conditionLockedDifficulty.entryConditionId))
                throw new InvalidOperationException("A public dungeon did not unlock its first difficulty after the official condition passed.");

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
            DailyDungeonModule migratedClaims = DailyDungeonModule.Open(
                slots, migrationSlotId, catalog);
            if (migratedClaims.Save.claimStateVersion != 4
                || migratedClaims.Save.giftedAvailableClaims != 0
                || migratedClaims.Save.purchasedAvailableEntries != 0
                || migratedClaims.RemainEntries != Math.Min(3, catalog.settings.DailyDungeonMaxLimitCount))
                throw new InvalidOperationException("Legacy gift fields were not cleared while preserving the claim pool.");

            string rolloverSlotId = slots.CreateSlot();
            var rolloverState = new DailyDungeonSave
            {
                claimStateVersion = 4,
                localDate = DateTime.Now.AddDays(-1).ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                usedEntries = 2,
                purchasedEntries = 1,
                purchasedAvailableEntries = 1,
                remainingClaims = 3,
                entryPurchaseReceipts = new List<string> { "previous-day-purchase" }
            };
            File.WriteAllText(Path.Combine(slots.GetSlotDirectory(rolloverSlotId), "daily-dungeon-save.json"),
                JsonUtility.ToJson(rolloverState));
            DailyDungeonModule rolloverModule = DailyDungeonModule.Open(slots, rolloverSlotId, catalog);
            DailyDungeonEntryAllowanceState rolloverAllowance = rolloverModule.EntryAllowance();
            if (rolloverModule.RemainEntries != Math.Max(3, catalog.settings.DailyDungeonDailyFreeCount)
                || rolloverModule.Save.usedEntries != 0 || rolloverModule.Save.purchasedEntries != 0
                || rolloverModule.Save.purchasedAvailableEntries != 0
                || rolloverModule.Save.entryPurchaseReceipts.Count != 0
                || rolloverAllowance.PurchaseRemaining != catalog.settings.DailyDungeonBuyCostCountList.Length)
                throw new InvalidOperationException("Daily reset did not carry the total claim pool and reset purchase state.");

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
            IReadOnlyList<DailyDungeonRewardPreviewItem> rewardPreview = module.BuildRewardPreview(
                selectedDungeon.id, selectedDifficulty.difficulty, 100, 0, _ => true,
                new AcceptanceRewardItemMetadataAdapter());
            if (rewardPreview.Count == 0 || rewardPreview.Any(value => value == null || value.ItemId <= 0
                || value.Count < 0) || rewardPreview.Select(value => value.ItemId).Distinct().Count() != rewardPreview.Count)
                throw new InvalidOperationException("The shared difficulty reward preview did not aggregate official items.");
            for (int index = 1; index < rewardPreview.Count; index++)
            {
                DailyDungeonRewardItemMetadata previous = new AcceptanceRewardItemMetadataAdapter()
                    .Get(rewardPreview[index - 1].ItemId);
                DailyDungeonRewardItemMetadata current = new AcceptanceRewardItemMetadataAdapter()
                    .Get(rewardPreview[index].ItemId);
                if (CompareRewardPreviewItems(previous, current) > 0)
                    throw new InvalidOperationException("The shared difficulty reward preview lost official item ordering.");
            }
            ValidatePartialLeave(slots, catalog, selectedDungeon, selectedDifficulty, route);
            DailyDungeonEntryPageModel entryPage = module.BuildEntryPageModel((_, __) => true);
            DailyDungeonEntryModel entry = entryPage.Entries.FirstOrDefault(value =>
                value != null && value.DungeonId == selectedDungeon.id);
            if (entry == null || entry.Difficulties == null
                || entry.Difficulties.All(value => value.Difficulty != selectedDifficulty.difficulty)
                || entryPage.EntryAllowance.TotalRemaining != initialClaims
                || entryPage.MaxAwardTimesPerClaim != 1)
                throw new InvalidOperationException("The shared entry page model did not expose the official dungeon difficulty and allowance state.");
            module.Options.AwardTimesAdapter = new AcceptanceAwardTimesAdapter(3);
            if (module.MaxAwardTimesPerClaim != 3
                || module.BuildEntryPageModel((_, __) => true).MaxAwardTimesPerClaim != 3)
                throw new InvalidOperationException("The daily dungeon award-times adapter did not expose the configured extra-claim benefit.");
            module.Options.AwardTimesAdapter = null;
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
            DailyDungeonRouteHudModel routeHud = module.BuildRouteHudModel();
            if (routeHud == null || routeHud.Run != module.ActiveRun
                || routeHud.Rooms == null || routeHud.Rooms.All(value => !value.IsCurrent)
                || routeHud.IsSelectingNextRoom)
                throw new InvalidOperationException("The shared route HUD model did not expose the active official route.");
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
                if (index == 0)
                {
                    routeHud = module.BuildRouteHudModel();
                    if (routeHud == null || !routeHud.IsSelectingNextRoom
                        || routeHud.Rooms.All(value => !value.IsCleared || !value.IsCurrent))
                        throw new InvalidOperationException("The shared route HUD model did not expose the cleared-room selection state.");
                }
                if (index + 1 < route.Count && !module.ChooseNextRoom(route[index + 1]))
                    throw new InvalidOperationException("The official test route could not choose room " + route[index + 1] + ".");
            }
            int grade = module.CurrentGrade();
            DailyDungeonTeamMemberSave[] teamSnapshot =
            {
                new DailyDungeonTeamMemberSave { npcClassId = 700001, level = 40, subRank = 1 }
            };
            if (!module.SaveBattleCheckpoint(string.Empty, teamSnapshot))
                throw new InvalidOperationException("The selected daily dungeon partner team was not saved with the run.");
            module.ActiveRun.startedAtUtcTicks = 0;
            module.ActiveRun.startedAtUnixSeconds -= 3600;
            if (grade <= 0 || !module.Finish(grade))
                throw new InvalidOperationException("The official map grade row did not finish the route.");
            module = DailyDungeonModule.Open(slots, slotId, catalog);
            DailyDungeonLeaderboardEntry bestRecord = module.Leaderboard(selectedDungeon.id,
                selectedDifficulty.difficulty, grade, int.MaxValue).FirstOrDefault(value =>
                    value.PlayerId == (module.Options.PlayerId > 0 ? module.Options.PlayerId : 1));
            if (bestRecord == null || bestRecord.TeamMembers.Count != teamSnapshot.Length
                || bestRecord.TeamMembers[0].npcClassId != teamSnapshot[0].npcClassId
                || bestRecord.TeamMembers[0].level != teamSnapshot[0].level
                || bestRecord.TeamMembers[0].subRank != teamSnapshot[0].subRank)
                throw new InvalidOperationException("The first-clear leaderboard snapshot did not retain its invited partner team.");

            long firstClearFinishTicks = bestRecord.FinishTicks;
            DailyDungeonPendingClaimSave firstClearPending = module.PendingClaims.Single();
            DailyDungeonRunSave fasterRun = JsonUtility.FromJson<DailyDungeonRunSave>(
                JsonUtility.ToJson(firstClearPending.run));
            fasterRun.id += ":faster-repeat";
            fasterRun.startedAtUtcTicks = DateTime.UtcNow.Ticks - TimeSpan.TicksPerSecond;
            fasterRun.finishedAtUtcTicks = 0;
            fasterRun.startedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            fasterRun.finishedAtUnixSeconds = 0;
            fasterRun.teamMembers = new List<DailyDungeonTeamMemberSave>
            {
                new DailyDungeonTeamMemberSave { npcClassId = 700002, level = 45, subRank = 2 }
            };
            module.Save.activeRun = fasterRun;
            if (!module.Finish(grade))
                throw new InvalidOperationException("The faster repeat clear did not update the personal best.");
            if (!module.DiscardPendingClaim(fasterRun.id))
                throw new InvalidOperationException("The faster repeat-clear test could not remove its isolated pending claim.");
            module = DailyDungeonModule.Open(slots, slotId, catalog);
            DailyDungeonLeaderboardEntry updatedBest = module.Leaderboard(selectedDungeon.id,
                selectedDifficulty.difficulty, grade, int.MaxValue).FirstOrDefault(value =>
                    value.PlayerId == (module.Options.PlayerId > 0 ? module.Options.PlayerId : 1));
            if (updatedBest == null)
                throw new InvalidOperationException("The faster repeat clear lost the personal leaderboard row.");
            DailyDungeonLeaderboardRecordSave persistedBest = module.Save.leaderboard.Single(value =>
                value != null && value.dungeonId == updatedBest.DungeonId
                    && value.difficulty == updatedBest.Difficulty && value.grade == updatedBest.Grade
                    && value.playerId == updatedBest.PlayerId);
            if (updatedBest.FinishTicks >= firstClearFinishTicks
                || updatedBest.TeamMembers.Count != teamSnapshot.Length
                || updatedBest.TeamMembers[0].npcClassId != teamSnapshot[0].npcClassId
                || persistedBest.teamMembers[0].npcClassId != fasterRun.teamMembers[0].npcClassId)
                throw new InvalidOperationException("A faster repeat clear replaced the displayed first-clear team instead of only updating the best-time roster.");
            if (module.RemainEntries != initialClaims
                || module.DailyDungeonGetRewardCount != initialAchievementProgress)
                throw new InvalidOperationException("Completing a route without claiming consumed a claim or advanced the claim achievement.");

            DailyDungeonPendingClaimSave pending = module.PendingClaims.Single();
            module.Options.AwardTimesAdapter = new AcceptanceAwardTimesAdapter(3);
            DailyDungeonSettlementHudModel settlementHud = module.BuildSettlementHudModel(
                pending, 3, 40, 0);
            if (settlementHud == null || settlementHud.MaxAwardTimesPerClaim != 3
                || settlementHud.AwardTimes != 3
                || settlementHud.CanClaim != (settlementHud.RemainingClaims >= 3))
                throw new InvalidOperationException("The shared settlement HUD model did not apply the extra-claim benefit.");
            module.Options.AwardTimesAdapter = null;
            string awardTimesSlotId = slots.CreateSlot();
            var extraClaimPending = JsonUtility.FromJson<DailyDungeonPendingClaimSave>(
                JsonUtility.ToJson(pending));
            extraClaimPending.awardTimes = 0;
            var extraClaimState = new DailyDungeonSave
            {
                claimStateVersion = 3,
                localDate = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                remainingClaims = 3,
                pendingClaims = new List<DailyDungeonPendingClaimSave> { extraClaimPending }
            };
            File.WriteAllText(Path.Combine(slots.GetSlotDirectory(awardTimesSlotId),
                "daily-dungeon-save.json"), JsonUtility.ToJson(extraClaimState));
            var extraClaimOptions = DailyDungeonModuleOptions.Default;
            extraClaimOptions.AwardTimesAdapter = new AcceptanceAwardTimesAdapter(3);
            DailyDungeonModule extraClaimModule = DailyDungeonModule.Open(slots, awardTimesSlotId,
                catalog, null, null, extraClaimOptions);
            int receivedAwardTimes = 0;
            if (!extraClaimModule.ClaimPendingReward(extraClaimPending.id, (_, __, times) =>
                {
                    receivedAwardTimes = times;
                    return true;
                }, 2)
                || receivedAwardTimes != 2
                || extraClaimModule.RemainEntries != 1
                || extraClaimModule.DailyDungeonGetRewardCount != 2)
                throw new InvalidOperationException("The extra-claim benefit did not change the shared claim transaction limit.");
            ValidateSettlement(slots, catalog, pending);
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
            if (records.Count != 1 || !records[0].IsSelf || records[0].IsNpc
                || records[0].TeamMembers.Count != 1
                || records[0].TeamMembers[0].npcClassId != teamSnapshot[0].npcClassId
                || records[0].TeamMembers[0].level != teamSnapshot[0].level
                || records[0].TeamMembers[0].subRank != teamSnapshot[0].subRank)
                throw new InvalidOperationException("The completed route did not produce a local personal leaderboard record.");
            IReadOnlyList<DailyDungeonLeaderboardEntry> orderedRecords =
                new OfflineDailyDungeonLeaderboardAdapter().Rank(new[]
                {
                    new DailyDungeonLeaderboardRecordSave
                    {
                        id = "slow-high-grade", dungeonId = selectedDungeon.id,
                        difficulty = selectedDifficulty.difficulty, grade = grade + 1,
                        finishTicks = 200, playerId = 2, playerName = "高评级"
                    },
                    new DailyDungeonLeaderboardRecordSave
                    {
                        id = "fast-low-grade", dungeonId = selectedDungeon.id,
                        difficulty = selectedDifficulty.difficulty, grade = grade,
                        finishTicks = 100, playerId = 3, playerName = "低评级"
                    },
                    new DailyDungeonLeaderboardRecordSave
                    {
                        id = "z-fast-same-grade", dungeonId = selectedDungeon.id,
                        difficulty = selectedDifficulty.difficulty, grade = grade + 1,
                        finishTicks = 50, playerId = 4, playerName = "同评级快通"
                    },
                    new DailyDungeonLeaderboardRecordSave
                    {
                        id = "a-tie-earlier-id", dungeonId = selectedDungeon.id,
                        difficulty = selectedDifficulty.difficulty, grade = grade + 1,
                        finishTicks = 50, playerId = 5, playerName = "同评级同时间"
                    }
                }, selectedDungeon.id, selectedDifficulty.difficulty, 0, 10, 0);
            if (orderedRecords.Count != 4 || orderedRecords[0].PlayerId != 4
                || orderedRecords[1].PlayerId != 5 || orderedRecords[2].PlayerId != 2
                || orderedRecords[3].PlayerId != 3)
                throw new InvalidOperationException("The local leaderboard did not sort only by official grade and completion time while retaining tied input order.");
            DailyDungeonLeaderboardRecordSave legacyRoster = new DailyDungeonLeaderboardRecordSave
            {
                id = "legacy-best-time-roster", dungeonId = selectedDungeon.id,
                difficulty = selectedDifficulty.difficulty, grade = grade, finishTicks = 25,
                playerId = 6, playerName = "旧档案",
                teamMembers = new List<DailyDungeonTeamMemberSave>
                {
                    new DailyDungeonTeamMemberSave { npcClassId = 700003, level = 42, subRank = 1 }
                }
            };
            DailyDungeonLeaderboardEntry legacyEntry = new OfflineDailyDungeonLeaderboardAdapter().Rank(
                new[] { legacyRoster }, selectedDungeon.id, selectedDifficulty.difficulty,
                grade, 1, 0).Single();
            if (legacyEntry.TeamMembers.Count != 0)
                throw new InvalidOperationException("A legacy best-time roster was incorrectly presented as a first-clear team.");

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

            DailyDungeonModule reloaded = DailyDungeonModule.Open(slots, slotId);
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
            reloaded = DailyDungeonModule.Open(slots, slotId);
            if (!reloaded.Save.claimedAchievementIndexes.Contains(cycleSeed.Index)
                || reloaded.DailyDungeonGetRewardCount != cycleSeed.RequiredCount)
                throw new InvalidOperationException("Claimed daily dungeon achievement state did not survive reload.");
            DailyDungeonGiftModel gift = reloaded.BuildGiftModel(5, true);
            if (gift.ClaimProgress != cycleSeed.RequiredCount
                || gift.Achievements == null
                || !gift.ClaimedIndexes.Contains(cycleSeed.Index))
                throw new InvalidOperationException("The shared Type 22 gift model did not expose cumulative progress and claimed state.");

            ValidateBattleEntryTransaction(slots, slotId);
        }

        private static void ValidateBattleEntryTransaction(ProfileSlotCatalog slots, string slotId)
        {
            const int playType = (int)NpcAssistPlayType.All;
            const string entryId = "daily-dungeon-entry-module-test";
            const int npcId = 11001;
            OfflineNpcAssistanceProgress progress = OfflineNpcAssistanceProgress.Open(slots, slotId);
            if (!progress.SetSelection(playType, entryId, new[] { npcId }, 3))
                throw new InvalidOperationException("The battle entry transaction fixture could not save its assist selection.");
            var rule = new FightOfficialNpcInviteRule
            {
                OneNpcMaxInviteCount = -1,
                MaxInviteCount = -1,
                RefreshCycle = (int)NpcAssistRefreshCycle.Daily,
                MaxPlayer = 3
            };
            var checkpoint = new FightBattleCheckpoint
            {
                StageId = 91001,
                StageType = 11,
                Units = new List<FightBattleUnitState>
                {
                    new FightBattleUnitState { Id = 1, NpcClassId = 0 },
                    new FightBattleUnitState { Id = 2, NpcClassId = npcId }
                }
            };
            DailyDungeonBattleEntryModule module = new DailyDungeonBattleEntryModule();
            DailyDungeonBattleEntryModule.PreparedEntry prepared = module.Prepare(
                checkpoint.StageId, checkpoint.StageType, playType, entryId, false, checkpoint,
                new[] { npcId }, null, false);
            List<string> successfulOrder = new List<string>();
            module.Enter(prepared, rule, progress, 3, false,
                DateTimeOffset.Now.ToUnixTimeSeconds(), mounted =>
                {
                    if (progress.ActiveBattle == null) throw new InvalidOperationException(
                        "The assist transaction was not committed before battle mount.");
                    successfulOrder.Add("mount");
                    return mounted;
                }, serialized =>
                {
                    if (progress.ActiveBattle == null) throw new InvalidOperationException(
                        "The assist transaction was lost before checkpoint save.");
                    successfulOrder.Add("save");
                    return !string.IsNullOrWhiteSpace(serialized);
                }, () => throw new InvalidOperationException("A successful battle entry unexpectedly disposed its battle."));
            if (!successfulOrder.SequenceEqual(new[] { "mount", "save" }))
                throw new InvalidOperationException("Battle entry did not mount and save in the official order.");
            progress.FinishBattle(playType, entryId, checkpoint.StageId);

            checkpoint = new FightBattleCheckpoint
            {
                StageId = 91002,
                StageType = 11,
                Units = new List<FightBattleUnitState>
                {
                    new FightBattleUnitState { Id = 1 },
                    new FightBattleUnitState { Id = 2, NpcClassId = npcId }
                }
            };
            prepared = module.Prepare(checkpoint.StageId, checkpoint.StageType, playType, entryId,
                false, checkpoint, new[] { npcId }, null, false);
            bool disposed = false;
            bool rejected = false;
            try
            {
                module.Enter(prepared, rule, progress, 3, false,
                    DateTimeOffset.Now.ToUnixTimeSeconds(), mounted => mounted,
                    _ => false, () => disposed = true);
            }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected || !disposed || progress.ActiveBattle != null
                || progress.AssistCount(playType, npcId, NpcAssistRefreshCycle.Daily,
                    DateTimeOffset.Now.ToUnixTimeSeconds()) != 1)
                throw new InvalidOperationException("A failed checkpoint save did not cancel the new assist transaction.");
        }

        private static List<int> FindTerminalRoute(DailyDungeonModule module, int startRoomId)
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

        private static int CompareRewardPreviewItems(DailyDungeonRewardItemMetadata left,
            DailyDungeonRewardItemMetadata right)
        {
            return (Flag(left.IsFiltrationItem) - Flag(right.IsFiltrationItem)) * 10000
                + (right.Quality - left.Quality) * 1000
                + (Flag(right.IsEquipment) - Flag(left.IsEquipment)) * 100
                + (Flag(right.IsCommonEquipmentPiece) - Flag(left.IsCommonEquipmentPiece)) * 10
                + (Flag(right.IsRandomReward) - Flag(left.IsRandomReward));
        }

        private static int Flag(bool value) => value ? 1 : 0;

        private sealed class AcceptanceRewardItemMetadataAdapter : IDailyDungeonRewardItemMetadataAdapter
        {
            public DailyDungeonRewardItemMetadata Get(int itemId)
            {
                return new DailyDungeonRewardItemMetadata(itemId, itemId % 8,
                    itemId % 2 == 0, itemId % 3 == 0, itemId % 5 == 0, itemId % 7 == 0);
            }
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

        private sealed class AcceptanceAwardTimesAdapter : IDailyDungeonAwardTimesAdapter
        {
            private readonly int maxAwardTimes;

            public AcceptanceAwardTimesAdapter(int maxAwardTimes)
            {
                this.maxAwardTimes = maxAwardTimes;
            }

            public int GetMaxAwardTimesPerClaim() => maxAwardTimes;
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
