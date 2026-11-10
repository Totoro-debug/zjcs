using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;

namespace Zjcs.Offline.World
{
    // The private route is data driven.  The session owns the state machine;
    // this adapter only supplies the route-specific facts extracted from the
    // official stage data.
    public interface IDailyDungeonRouteAdapter
    {
        bool IsPrivateDifficulty(int difficulty);
        bool CanRecordPrivateBattle(DailyDungeonRunSave run, DailyDungeonRuntimeRoom room,
            DailyDungeonRuntimeStage stage);
        bool CanCompletePrivateRoute(DailyDungeonRunSave run, DailyDungeonRuntimeRoom room);
    }

    [Serializable]
    public sealed class DailyDungeonPrivateRouteDefinition
    {
        public int dungeonId;
        public int difficulty;
        public int requiredBattleRoomId;
        public int terminalRoomType;
    }

    [Serializable]
    internal sealed class DailyDungeonPrivateRouteDocument
    {
        public string manifest;
        public int privateDifficulty;
        public DailyDungeonPrivateRouteDefinition[] routes;
    }

    public sealed class ConfiguredDailyDungeonRouteAdapter : IDailyDungeonRouteAdapter
    {
        private readonly DailyDungeonPrivateRouteDefinition[] routes;
        private readonly int privateDifficulty;

        private ConfiguredDailyDungeonRouteAdapter(int privateDifficulty,
            DailyDungeonPrivateRouteDefinition[] values)
        {
            this.privateDifficulty = privateDifficulty;
            routes = values ?? Array.Empty<DailyDungeonPrivateRouteDefinition>();
        }

        public static ConfiguredDailyDungeonRouteAdapter LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dungeons/private-routes");
            if (asset == null)
                throw new FileNotFoundException("Daily dungeon private route configuration is missing.");
            DailyDungeonPrivateRouteDocument document = JsonUtility.FromJson<DailyDungeonPrivateRouteDocument>(asset.text);
            if (document == null || string.IsNullOrWhiteSpace(document.manifest)
                || document.privateDifficulty < 0 || document.routes == null
                || document.routes.Any(value => value == null || value.dungeonId <= 0
                    || value.difficulty < 0 || value.requiredBattleRoomId <= 0 || value.terminalRoomType < 0))
                throw new InvalidDataException("Daily dungeon private route configuration is invalid.");
            if (document.routes.Any(value => value.difficulty != document.privateDifficulty))
                throw new InvalidDataException("Daily dungeon private route difficulty does not match the configured private difficulty.");
            return new ConfiguredDailyDungeonRouteAdapter(document.privateDifficulty, document.routes);
        }

        public bool IsPrivateDifficulty(int difficulty) => difficulty == privateDifficulty;

        public bool CanRecordPrivateBattle(DailyDungeonRunSave run, DailyDungeonRuntimeRoom room,
            DailyDungeonRuntimeStage stage)
        {
            DailyDungeonPrivateRouteDefinition definition = Find(run);
            bool hasEnemyWaves = stage != null && (stage.waveArrayIds?.Length > 0
                || stage.waveMonsterIds?.Any(wave => wave?.Length > 0) == true);
            // The required battle room gates route completion; earlier route fights still persist their result.
            return definition != null && run != null && room != null && stage != null
                && room.id == run.currentRoomId && hasEnemyWaves;
        }

        public bool CanCompletePrivateRoute(DailyDungeonRunSave run, DailyDungeonRuntimeRoom room)
        {
            DailyDungeonPrivateRouteDefinition definition = Find(run);
            return definition != null && run != null && room != null && !run.privateRouteComplete
                && room.id == run.currentRoomId && room.type == definition.terminalRoomType
                && room.nextRoomIds != null && room.nextRoomIds.Length == 0
                && run.lastBattleRoomId == definition.requiredBattleRoomId
                && run.lastBattleOutcome == 1
                && run.clearedRoomIds.Contains(definition.requiredBattleRoomId);
        }

        private DailyDungeonPrivateRouteDefinition Find(DailyDungeonRunSave run)
        {
            return run == null ? null : routes.FirstOrDefault(value => value.dungeonId == run.dungeonId
                && value.difficulty == run.difficulty);
        }
    }

    public interface IDailyDungeonLevelAdapter
    {
        DailyDungeonLevelDefinition Resolve(DailyDungeonRuntimeBattle dungeon,
            DailyDungeonRuntimeDifficulty difficulty);
    }

    public sealed class DailyDungeonLevelDefinition
    {
        public int DungeonId { get; internal set; }
        public string Title { get; internal set; }
        public int Difficulty { get; internal set; }
        public int MapGroupId { get; internal set; }
        public int EntryMapClassId { get; internal set; }
        public int EntryRegionId { get; internal set; }
        public bool IsPrivateRoute { get; internal set; }
        public bool SupportsLeaderboard { get; internal set; }
        public IReadOnlyList<int> LeaderboardGrades { get; internal set; } = Array.Empty<int>();
        public int RetrieveGrade { get; internal set; }
    }

    public sealed class CatalogDailyDungeonLevelAdapter : IDailyDungeonLevelAdapter
    {
        private readonly IDailyDungeonRouteAdapter routeAdapter;
        private readonly Dictionary<(int dungeonId, int difficulty), int[]> leaderboardGrades;

        [Serializable] private sealed class LeaderboardConfiguration
        { public string manifest; public LeaderboardLevel[] levels; }
        [Serializable] private sealed class LeaderboardLevel
        { public int dungeonId, difficulty; public int[] grades; }

        public CatalogDailyDungeonLevelAdapter(IDailyDungeonRouteAdapter routeAdapter = null)
        {
            this.routeAdapter = routeAdapter ?? ConfiguredDailyDungeonRouteAdapter.LoadDefault();
            TextAsset source = Resources.Load<TextAsset>("Dungeons/daily-dungeon-leaderboards")
                ?? throw new InvalidDataException("Official daily dungeon leaderboard configuration is missing.");
            LeaderboardConfiguration configuration = JsonUtility.FromJson<LeaderboardConfiguration>(source.text);
            if (configuration == null || configuration.manifest != "884_156692" || configuration.levels == null)
                throw new InvalidDataException("Official daily dungeon leaderboard configuration is invalid.");
            leaderboardGrades = configuration.levels.ToDictionary(value => (value.dungeonId, value.difficulty),
                value => value.grades ?? Array.Empty<int>());
        }

        public DailyDungeonLevelDefinition Resolve(DailyDungeonRuntimeBattle dungeon,
            DailyDungeonRuntimeDifficulty difficulty)
        {
            if (dungeon == null || difficulty == null) return null;
            bool privateRoute = routeAdapter.IsPrivateDifficulty(difficulty.difficulty);
            int[] grades = leaderboardGrades.TryGetValue((dungeon.id, difficulty.difficulty), out int[] configured)
                ? configured : Array.Empty<int>();
            return new DailyDungeonLevelDefinition
            {
                DungeonId = dungeon.id,
                Title = dungeon.title ?? string.Empty,
                Difficulty = difficulty.difficulty,
                MapGroupId = difficulty.mapGroupId,
                EntryMapClassId = dungeon.entryMapClassId,
                EntryRegionId = dungeon.entryRegionId,
                IsPrivateRoute = privateRoute,
                SupportsLeaderboard = !privateRoute && grades.Length > 0,
                LeaderboardGrades = privateRoute ? Array.Empty<int>() : grades,
                RetrieveGrade = difficulty.retrieveGrade
            };
        }
    }

    [Serializable]
    public sealed class DailyDungeonLeaderboardRecordSave
    {
        public string id;
        public int dungeonId;
        public int difficulty;
        public int grade;
        public long finishTicks;
        public long playerId;
        public string playerName;
        public bool npc;
        public List<DailyDungeonTeamMemberSave> firstClearTeamMembers = new List<DailyDungeonTeamMemberSave>();
        // Legacy best-time roster retained for old saves and NPC team templates.
        public List<DailyDungeonTeamMemberSave> teamMembers = new List<DailyDungeonTeamMemberSave>();
    }

    [Serializable]
    public sealed class DailyDungeonGiftSave
    {
        public string receipt;
        public string localDate;
        public int count;
        public long playerId;
    }

    public sealed class DailyDungeonEntryAllowanceState
    {
        public int TotalRemaining { get; internal set; }
        public int DailyFreeLimit { get; internal set; }
        public int PurchaseRemaining { get; internal set; }
        public int UsedEntries { get; internal set; }
        public int PurchasedEntries { get; internal set; }
    }

    public sealed class DailyDungeonLeaderboardEntry
    {
        public int Rank { get; internal set; }
        public int DungeonId { get; internal set; }
        public int Difficulty { get; internal set; }
        public int Grade { get; internal set; }
        public long FinishTicks { get; internal set; }
        public long PlayerId { get; internal set; }
        public string PlayerName { get; internal set; }
        public bool IsSelf { get; internal set; }
        public bool IsNpc { get; internal set; }
        public IReadOnlyList<DailyDungeonTeamMemberSave> TeamMembers { get; internal set; }
            = Array.Empty<DailyDungeonTeamMemberSave>();
    }

    public sealed class DailyDungeonScore
    {
        public int Grade { get; internal set; }
        public int DeepestRow { get; internal set; }
        public int ClearedRoomCount { get; internal set; }
        public long ElapsedSeconds { get; internal set; }
    }

    public sealed class DailyDungeonRewardRollItem
    {
        public bool IsGradeReward { get; internal set; }
        public int ItemId { get; internal set; }
        public long Count { get; internal set; }
        public float Probability { get; internal set; }
    }

    public sealed class DailyDungeonRewardRoll
    {
        public IReadOnlyList<DailyDungeonRewardRollItem> Items { get; internal set; }
    }

    public sealed class DailyDungeonRewardDoubleChanceSources
    {
        public float SeasonAstrologyContractPercent;
        public float AncientRelicLuckyStatuePercent;
        public float EquipmentAffixPercent;
    }

    public static class DailyDungeonRewardDoubleChance
    {
        public const float BasePercent = 5f;

        public static float Probability(DailyDungeonRewardDoubleChanceSources sources)
        {
            float total = BasePercent;
            if (sources != null)
            {
                total += Finite(sources.SeasonAstrologyContractPercent);
                total += Finite(sources.AncientRelicLuckyStatuePercent);
                total += Finite(sources.EquipmentAffixPercent);
            }
            return Math.Max(0f, Math.Min(100f, total)) / 100f;
        }

        private static float Finite(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    public interface IDailyDungeonRandom
    {
        double NextDouble();
        float NextFloat();
        bool Roll(double probability);
    }

    public interface IDailyDungeonRewardDoubleChanceAdapter
    {
        DailyDungeonRewardDoubleChanceSources GetSources(DailyDungeonRunSave run, int rewardRuleId);
    }

    /// <summary>
    /// Supplies the maximum number of reward claims that one completed public
    /// dungeon result may consume.  The official client starts at one and
    /// adds the ExtraClaimCount from active DailyDungeonExtraClaim benefits;
    /// the benefit store stays outside this module and is supplied here.
    /// </summary>
    public interface IDailyDungeonAwardTimesAdapter
    {
        int GetMaxAwardTimesPerClaim();
    }

    public sealed class DailyDungeonHudModel
    {
        public DailyDungeonLevelDefinition Level { get; internal set; }
        public DailyDungeonRuntimeMap Map { get; internal set; }
        public int BestGrade { get; internal set; }
        public int CurrentGrade { get; internal set; }
        public int DeepestRow { get; internal set; }
        public DailyDungeonEntryAllowanceState EntryAllowance { get; internal set; }
        public int RemainingEntries { get; internal set; }
        public long DailyDungeonGetRewardCount { get; internal set; }
        public bool IsUnlocked { get; internal set; }
    }

    public interface IDailyDungeonLeaderboardAdapter
    {
        IReadOnlyList<DailyDungeonLeaderboardEntry> Rank(IEnumerable<DailyDungeonLeaderboardRecordSave> records,
            int dungeonId, int difficulty, int grade, int limit, long selfId);
    }

    public sealed class OfflineDailyDungeonLeaderboardAdapter : IDailyDungeonLeaderboardAdapter
    {
        public IReadOnlyList<DailyDungeonLeaderboardEntry> Rank(
            IEnumerable<DailyDungeonLeaderboardRecordSave> records, int dungeonId, int difficulty,
            int grade, int limit, long selfId)
        {
            return (records ?? Array.Empty<DailyDungeonLeaderboardRecordSave>())
                .Where(value => value != null && value.dungeonId == dungeonId
                    && value.difficulty == difficulty && (grade <= 0 || value.grade == grade))
                .OrderByDescending(value => value.grade)
                .ThenBy(value => value.finishTicks <= 0 ? long.MaxValue : value.finishTicks)
                .Take(Math.Max(0, limit))
                .Select((value, index) => new DailyDungeonLeaderboardEntry
                {
                    Rank = index + 1,
                    DungeonId = value.dungeonId,
                    Difficulty = value.difficulty,
                    Grade = value.grade,
                    FinishTicks = value.finishTicks,
                    PlayerId = value.playerId,
                    PlayerName = value.playerName ?? string.Empty,
                    IsSelf = selfId != 0 && value.playerId == selfId,
                    IsNpc = value.npc,
                    TeamMembers = ((value.npc ? value.teamMembers : value.firstClearTeamMembers)
                        ?? new List<DailyDungeonTeamMemberSave>())
                        .Select(member => new DailyDungeonTeamMemberSave
                        {
                            npcClassId = member.npcClassId,
                            level = member.level,
                            subRank = member.subRank
                        }).ToArray()
                }).ToArray();
        }
    }

    public interface IDailyDungeonEconomyAdapter
    {
        bool Consume(string receipt, int itemId, long count);
    }

    // Compatibility implementation for the public DailyDungeonModule seam.
    // Keep this type while older callers and saved validation entry points move
    // to the common module name.
    public class OfflineDailyDungeonModule : OfflineDailyDungeonSession
    {
        private readonly IDailyDungeonLevelAdapter levelAdapter;
        private readonly IDailyDungeonLeaderboardAdapter leaderboardAdapter;
        private readonly DailyDungeonModuleOptions options;
        private readonly DailyDungeonAchievementRules achievementRules;

        protected OfflineDailyDungeonModule(string path, DailyDungeonRuntimeData catalog, DailyDungeonSave save,
            IDailyDungeonRouteAdapter routeAdapter, IDailyDungeonLevelAdapter levelAdapter,
            IDailyDungeonLeaderboardAdapter leaderboardAdapter, DailyDungeonModuleOptions options)
            : base(path, catalog, save, routeAdapter)
        {
            this.levelAdapter = levelAdapter ?? new CatalogDailyDungeonLevelAdapter(routeAdapter);
            this.leaderboardAdapter = leaderboardAdapter ?? new OfflineDailyDungeonLeaderboardAdapter();
            this.options = options ?? DailyDungeonModuleOptions.Default;
            achievementRules = DailyDungeonAchievementRules.LoadDefault();
            if (this.options.PlayerId <= 0) this.options.PlayerId = 1;
            if (this.options.LeaderboardLimit <= 0) this.options.LeaderboardLimit = 50;
            if (this.options.MaxAwardTimesPerClaim <= 0) this.options.MaxAwardTimesPerClaim = 1;
        }

        public IDailyDungeonLevelAdapter LevelAdapter => levelAdapter;
        public IDailyDungeonLeaderboardAdapter LeaderboardAdapter => leaderboardAdapter;
        public IDailyDungeonRewardDoubleChanceAdapter RewardDoubleChanceAdapter => options.RewardDoubleChanceAdapter;
        public IDailyDungeonAwardTimesAdapter AwardTimesAdapter => options.AwardTimesAdapter;
        public DailyDungeonAchievementRules AchievementRules => achievementRules;
        public DailyDungeonModuleOptions Options => options;
        public int MaxAwardTimesPerClaim
        {
            get
            {
                int configured = Math.Max(1, options.MaxAwardTimesPerClaim);
                int adapted = options.AwardTimesAdapter?.GetMaxAwardTimesPerClaim() ?? configured;
                return Math.Max(1, adapted);
            }
        }

        public IReadOnlyList<DailyDungeonAchievementMilestone> DailyDungeonAchievementRows(
            int showCount = 5, bool showReceived = false)
            => achievementRules.Visible(DailyDungeonGetRewardCount,
                Save.claimedAchievementIndexes, showCount, showReceived);

        public bool ClaimDailyDungeonAchievement(int index,
            Func<string, IReadOnlyList<DailyDungeonAchievementReward>, bool> grantRewards)
        {
            DailyDungeonAchievementMilestone milestone = achievementRules.Find(index);
            if (milestone == null || grantRewards == null
                || DailyDungeonGetRewardCount < milestone.RequiredCount
                || Save.claimedAchievementIndexes.Contains(index)) return false;
            string receipt = "daily-dungeon:achievement:" + index;
            if (!grantRewards(receipt, milestone.Rewards)) return false;
            DailyDungeonSave next = Clone(Save);
            if (next.claimedAchievementIndexes.Contains(index)) return false;
            next.claimedAchievementIndexes.Add(index);
            next.claimedAchievementIndexes.Sort();
            Persist(next);
            return true;
        }

        public static OfflineDailyDungeonModule Open(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog = null, IDailyDungeonLevelAdapter levelAdapter = null,
            IDailyDungeonLeaderboardAdapter leaderboardAdapter = null,
            DailyDungeonModuleOptions options = null, IDailyDungeonRouteAdapter routeAdapter = null)
        {
            routeAdapter = routeAdapter ?? ConfiguredDailyDungeonRouteAdapter.LoadDefault();
            return (OfflineDailyDungeonModule)OpenCore(slots, slotId, catalog, true, routeAdapter,
                (path, data, save) => new OfflineDailyDungeonModule(path, data, save, routeAdapter,
                    levelAdapter, leaderboardAdapter, options));
        }

        public static OfflineDailyDungeonModule OpenPrivateMap(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog = null, IDailyDungeonLevelAdapter levelAdapter = null,
            IDailyDungeonLeaderboardAdapter leaderboardAdapter = null,
            DailyDungeonModuleOptions options = null, IDailyDungeonRouteAdapter routeAdapter = null)
        {
            routeAdapter = routeAdapter ?? ConfiguredDailyDungeonRouteAdapter.LoadDefault();
            return (OfflineDailyDungeonModule)OpenCore(slots, slotId, catalog, false, routeAdapter,
                (path, data, save) => new OfflineDailyDungeonModule(path, data, save, routeAdapter,
                    levelAdapter, leaderboardAdapter, options));
        }

        public DailyDungeonLevelDefinition ResolveLevel(int dungeonId, int difficulty)
        {
            return levelAdapter.Resolve(FindDungeon(dungeonId),
                FindDungeon(dungeonId)?.difficulties?.FirstOrDefault(value => value.difficulty == difficulty));
        }

        public DailyDungeonHudModel BuildHudModel(int dungeonId, int difficulty,
            Func<int, bool> conditionMet = null)
        {
            DailyDungeonLevelDefinition level = ResolveLevel(dungeonId, difficulty);
            DailyDungeonRunSave run = ActiveRun != null && ActiveRun.dungeonId == dungeonId
                && ActiveRun.difficulty == difficulty ? ActiveRun : null;
            return new DailyDungeonHudModel
            {
                Level = level,
                Map = level == null ? null : FindMap(level.MapGroupId),
                BestGrade = BestGrade(dungeonId, difficulty),
                CurrentGrade = run == null ? 0 : CurrentGrade(),
                DeepestRow = DeepestRow(run),
                EntryAllowance = EntryAllowance(),
                RemainingEntries = RemainEntries,
                DailyDungeonGetRewardCount = this.DailyDungeonGetRewardCount,
                IsUnlocked = IsDifficultyUnlocked(dungeonId, difficulty, conditionMet)
            };
        }

        public DailyDungeonScore CalculateScore(DailyDungeonRunSave run, long finishedAtUnixSeconds = 0)
        {
            if (run == null) return new DailyDungeonScore();
            int deepestRow = DeepestRow(run);
            int grade = run.grade > 0 ? run.grade : GradeFor(run, deepestRow);
            if (finishedAtUnixSeconds <= 0) finishedAtUnixSeconds = run.finishedAtUnixSeconds;
            long elapsed = finishedAtUnixSeconds <= 0 || run.startedAtUnixSeconds <= 0
                ? 0 : Math.Max(0, finishedAtUnixSeconds - run.startedAtUnixSeconds);
            return new DailyDungeonScore
            {
                Grade = grade,
                DeepestRow = deepestRow,
                ClearedRoomCount = run.clearedRoomIds?.Count ?? 0,
                ElapsedSeconds = elapsed
            };
        }

        public float RewardProbability(int ruleId, int candidateIndex)
        {
            DailyDungeonRuntimeReward rule = Catalog.rewardRules?.FirstOrDefault(value => value != null && value.Id == ruleId);
            if (rule == null || rule.WeightList == null || candidateIndex < 0 || candidateIndex >= rule.WeightList.Length)
                return 0f;
            long total = rule.WeightList.Where(value => value > 0).Sum(value => (long)value);
            if (total <= 0 || rule.WeightList[candidateIndex] <= 0 || rule.Percent <= 0f) return 0f;
            return Math.Min(1f, rule.Percent * rule.WeightList[candidateIndex] / total);
        }

        public float GradeRewardDoubleProbability(DailyDungeonRunSave run, int rewardRuleId)
        {
            DailyDungeonRuntimeGradeReward grade = run == null ? null
                : FindMap(run.mapGroupId)?.gradeRewards?.FirstOrDefault(value => value != null
                    && value.difficulty == run.difficulty && value.grade == run.grade);
            if (grade?.rewardRuleIds == null || !grade.rewardRuleIds.Contains(rewardRuleId)) return 0f;
            DailyDungeonRewardDoubleChanceSources sources = options.RewardDoubleChanceAdapter?.GetSources(run, rewardRuleId);
            return DailyDungeonRewardDoubleChance.Probability(sources);
        }

        public DailyDungeonRewardRoll RollRewards(DailyDungeonRunSave run, bool completed, int level, int subRank,
            Func<DailyDungeonRuntimeReward, bool> conditionMet, string receipt, int awardTimes = 1,
            IEnumerable<int> rewardRuleIds = null, IDailyDungeonRandom randomSource = null)
        {
            if (run == null) return new DailyDungeonRewardRoll { Items = Array.Empty<DailyDungeonRewardRollItem>() };
            awardTimes = Math.Max(1, awardTimes);
            IDailyDungeonRandom random = randomSource ?? new StableDungeonRandom(receipt ?? run.id ?? string.Empty);
            var results = new List<DailyDungeonRewardRollItem>();
            int[] rules = (rewardRuleIds ?? ConfiguredRewardRuleIds(run, completed)).ToArray();
            int roomRuleCount = ClearedRoomRewardRuleIds(run).Length;
            for (int awardTime = 0; awardTime < awardTimes; awardTime++)
            {
                for (int ruleIndex = 0; ruleIndex < rules.Length; ruleIndex++)
                {
                    int ruleId = rules[ruleIndex];
                    DailyDungeonRuntimeReward rule = Catalog.rewardRules?.Where(value => value != null && value.Id == ruleId)
                        .OrderBy(value => value.Order < 0 ? int.MaxValue : value.Order)
                        .FirstOrDefault(value => conditionMet == null || conditionMet(value));
                    if (rule == null || rule.IdList == null || rule.WeightList == null || rule.CountList == null
                        || rule.IdList.Length != rule.WeightList.Length || rule.IdList.Length != rule.CountList.Length)
                        continue;
                    long total = rule.WeightList.Where(value => value > 0).Sum(value => (long)value);
                    if (total <= 0 || rule.Percent <= 0f) continue;
                    var ruleResults = new List<DailyDungeonRewardRollItem>();
                    for (int index = 0; index < rule.IdList.Length; index++)
                    {
                        if (rule.IdList[index] <= 0 || rule.WeightList[index] <= 0 || rule.CountList[index] <= 0)
                            continue;
                        float probability = RewardProbability(ruleId, index);
                        if (probability < 1f && !random.Roll(probability)) continue;
                        float count = rule.CountList[index];
                        int levelCountId = rule.LevelCountIdFor(index);
                        if (levelCountId > 0) count *= LevelNumber(levelCountId, level);
                        if (rule.SubRankCountId != null && index < rule.SubRankCountId.Length
                            && rule.SubRankCountId[index] > 0)
                            count *= LevelNumber(rule.SubRankCountId[index], subRank);
                        long amount = Math.Max(0L, (long)count);
                        if (amount <= 0) continue;
                        ruleResults.Add(new DailyDungeonRewardRollItem
                        {
                            ItemId = rule.IdList[index], Count = amount, Probability = probability,
                            IsGradeReward = ruleIndex >= roomRuleCount
                        });
                    }
                    float doubleProbability = GradeRewardDoubleProbability(run, ruleId);
                    if (ruleResults.Count > 0 && (doubleProbability >= 1f
                        || (doubleProbability > 0f && random.Roll(doubleProbability))))
                    {
                        foreach (DailyDungeonRewardRollItem reward in ruleResults)
                            reward.Count = checked(reward.Count * 2);
                    }
                    results.AddRange(ruleResults);
                }
            }
            results = results.GroupBy(value => new { value.ItemId, value.Probability, value.IsGradeReward })
                .Select(group => new DailyDungeonRewardRollItem
                {
                    ItemId = group.Key.ItemId,
                    Count = group.Sum(value => value.Count),
                    Probability = group.Key.Probability
                    , IsGradeReward = group.Key.IsGradeReward
                }).ToList();
            return new DailyDungeonRewardRoll
            {
                Items = results
            };
        }

        public DailyDungeonEntryAllowanceState EntryAllowance()
        {
            return new DailyDungeonEntryAllowanceState
            {
                TotalRemaining = RemainEntries,
                DailyFreeLimit = DailyFreeClaimLimit,
                PurchaseRemaining = Math.Max(0,
                    (Catalog.settings.DailyDungeonBuyCostCountList?.Length ?? 0) - Save.purchasedEntries),
                UsedEntries = Math.Max(0, Save.usedEntries),
                PurchasedEntries = Save.purchasedEntries
            };
        }

        public IReadOnlyList<DailyDungeonLeaderboardEntry> Leaderboard(int dungeonId, int difficulty,
            int grade = 0, int limit = 50, long selfId = 0)
        {
            if (ResolveLevel(dungeonId, difficulty)?.SupportsLeaderboard != true)
                return Array.Empty<DailyDungeonLeaderboardEntry>();
            if (selfId <= 0) selfId = options.PlayerId;
            return leaderboardAdapter.Rank(Save.leaderboard, dungeonId, difficulty, grade,
                limit <= 0 ? options.LeaderboardLimit : limit, selfId);
        }

        public bool ClaimPendingReward(string claimId,
            Func<string, DailyDungeonRunSave, int, bool> grantRewards, int awardTimes = 1)
        {
            DailyDungeonPendingClaimSave pending = Save.pendingClaims.FirstOrDefault(value =>
                value != null && value.id == claimId);
            DailyDungeonLevelDefinition level = pending?.run == null ? null
                : ResolveLevel(pending.run.dungeonId, pending.run.difficulty);
            if (pending?.run == null || grantRewards == null || level == null
                || level.IsPrivateRoute)
                return false;
            if (Save.pendingClaims.Any(value => value != null && value.id != claimId
                && value.awardTimes > 0)) return false;

            if (pending.awardTimes <= 0)
            {
                if (awardTimes <= 0 || awardTimes > MaxAwardTimesPerClaim || awardTimes > RemainEntries)
                    return false;
                DailyDungeonSave reserved = Clone(Save);
                DailyDungeonPendingClaimSave reservation = reserved.pendingClaims.FirstOrDefault(value =>
                    value != null && value.id == claimId);
                if (reservation == null) return false;
                reservation.awardTimes = awardTimes;
                Persist(reserved);
                pending = Save.pendingClaims.FirstOrDefault(value => value != null && value.id == claimId);
                if (pending == null) return false;
            }
            else
            {
                awardTimes = pending.awardTimes;
            }
            if (Save.remainingClaims < awardTimes
                || Save.lifetimeClaimedRewardTimes > long.MaxValue - awardTimes) return false;

            string receipt = "daily-dungeon:claim:" + pending.id;
            bool granted = grantRewards(receipt, Clone(pending.run), awardTimes);
            if (!granted) return false;

            DailyDungeonSave next = Clone(Save);
            DailyDungeonPendingClaimSave claimed = next.pendingClaims.FirstOrDefault(value =>
                value != null && value.id == claimId && value.awardTimes == awardTimes);
            if (claimed == null) return false;
            for (int index = 0; index < awardTimes; index++)
            {
                next.remainingClaims--;
                next.usedEntries++;
            }
            next.lifetimeClaimedRewardTimes = checked(next.lifetimeClaimedRewardTimes + awardTimes);
            next.pendingClaims.Remove(claimed);
            Persist(next);
            return true;
        }

        public override bool Finish(int grade)
        {
            return FinishAndPersist(grade, (next, run) =>
            {
                long finishTicks = run.startedAtUtcTicks > 0 && run.finishedAtUtcTicks >= run.startedAtUtcTicks
                    ? run.finishedAtUtcTicks - run.startedAtUtcTicks
                    : Math.Max(0, run.finishedAtUnixSeconds - run.startedAtUnixSeconds) * TimeSpan.TicksPerSecond;
                long playerId = options.PlayerId > 0 ? options.PlayerId : 1;
                string recordId = run.dungeonId + ":" + run.difficulty + ":" + run.grade + ":" + playerId;
                DailyDungeonLeaderboardRecordSave record = next.leaderboard.FirstOrDefault(value =>
                    value != null && value.id == recordId);
                long completionCount = next.completionCounts.FirstOrDefault(value =>
                    value != null && value.dungeonId == run.dungeonId && value.difficulty == run.difficulty
                    && value.grade == run.grade)?.count ?? 0;
                if (record == null)
                {
                    next.leaderboard.Add(new DailyDungeonLeaderboardRecordSave
                    {
                        id = recordId, dungeonId = run.dungeonId, difficulty = run.difficulty,
                        grade = run.grade, finishTicks = finishTicks, playerId = playerId,
                        playerName = string.IsNullOrWhiteSpace(options.PlayerName) ? "本地玩家" : options.PlayerName,
                        npc = false,
                        firstClearTeamMembers = completionCount == 1
                            ? CopyTeamMembers(run.teamMembers) : new List<DailyDungeonTeamMemberSave>(),
                        teamMembers = CopyTeamMembers(run.teamMembers)
                    });
                }
                else if (record.finishTicks <= 0 || finishTicks < record.finishTicks)
                {
                    record.finishTicks = finishTicks;
                    record.playerName = string.IsNullOrWhiteSpace(options.PlayerName) ? "本地玩家" : options.PlayerName;
                    record.npc = false;
                    record.teamMembers = CopyTeamMembers(run.teamMembers);
                }
            });
        }

        private static List<DailyDungeonTeamMemberSave> CopyTeamMembers(
            IEnumerable<DailyDungeonTeamMemberSave> teamMembers)
        {
            return (teamMembers ?? Array.Empty<DailyDungeonTeamMemberSave>())
                .Select(value => new DailyDungeonTeamMemberSave
                {
                    npcClassId = value.npcClassId,
                    level = value.level,
                    subRank = value.subRank
                }).ToList();
        }

        private DailyDungeonRuntimeDifficulty FindDifficultyForModule(int dungeonId, int difficulty)
        {
            return FindDungeon(dungeonId)?.difficulties?.FirstOrDefault(value => value != null
                && value.difficulty == difficulty);
        }

        private int DeepestRow(DailyDungeonRunSave run)
        {
            if (run == null) return 0;
            DailyDungeonRuntimeMap map = FindMap(run.mapGroupId);
            if (map == null || map.roomArray == null || map.columnCount <= 0) return 0;
            return (run.clearedRoomIds ?? new List<int>()).Select(id => Array.IndexOf(map.roomArray, id))
                .Where(index => index >= 0).Select(index => index / map.columnCount + 1).DefaultIfEmpty(0).Max();
        }

        private int GradeFor(DailyDungeonRunSave run, int deepestRow)
        {
            DailyDungeonRuntimeMap map = FindMap(run?.mapGroupId ?? 0);
            return map?.gradeRows?.Where(value => value != null && value.difficulty == run.difficulty
                    && deepestRow >= value.row).Select(value => value.grade).DefaultIfEmpty(0).Max() ?? 0;
        }

        private sealed class StableDungeonRandom : IDailyDungeonRandom
        {
            private uint state;
            public StableDungeonRandom(string seed)
            {
                state = 2166136261u;
                foreach (char value in seed) state = unchecked((state ^ value) * 16777619u);
                if (state == 0) state = 0x9e3779b9u;
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

    [Serializable]
    public sealed class DailyDungeonModuleOptions
    {
        public int LeaderboardLimit = 50;
        public int MaxAwardTimesPerClaim = 1;
        public IDailyDungeonRewardDoubleChanceAdapter RewardDoubleChanceAdapter;
        public IDailyDungeonAwardTimesAdapter AwardTimesAdapter;
        public long PlayerId = 1;
        public string PlayerName = "本地玩家";

        public static DailyDungeonModuleOptions Default => new DailyDungeonModuleOptions();
    }
}
