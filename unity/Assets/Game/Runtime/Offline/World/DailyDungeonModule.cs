using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.State;

namespace Zjcs.Offline.World
{
    /// <summary>
    /// Public seam for every daily dungeon variant.
    ///
    /// The module owns the shared daily dungeon rules through its base
    /// implementation.  A caller only supplies the facts that vary at the
    /// seam: accessible map regions, player condition evaluation and, when
    /// opening the module, the configured adapters.  World Tree, Machine
    /// Mountain, Sea Palace Ruins and private routes therefore use the same
    /// entry, route, settlement and gift models.
    /// </summary>
    public sealed class DailyDungeonModule : OfflineDailyDungeonModule
    {
        private DailyDungeonModule(string path, DailyDungeonRuntimeData catalog,
            DailyDungeonSave save, IDailyDungeonRouteAdapter routeAdapter,
            IDailyDungeonLevelAdapter levelAdapter,
            IDailyDungeonLeaderboardAdapter leaderboardAdapter,
            DailyDungeonModuleOptions options)
            : base(path, catalog, save, routeAdapter, levelAdapter,
                leaderboardAdapter, options)
        {
        }

        public static new DailyDungeonRuntimeData LoadCatalog()
        {
            return OfflineDailyDungeonSession.LoadCatalog();
        }

        public static new DailyDungeonModule Open(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog = null, IDailyDungeonLevelAdapter levelAdapter = null,
            IDailyDungeonLeaderboardAdapter leaderboardAdapter = null,
            DailyDungeonModuleOptions options = null, IDailyDungeonRouteAdapter routeAdapter = null)
        {
            routeAdapter = routeAdapter ?? ConfiguredDailyDungeonRouteAdapter.LoadDefault();
            return (DailyDungeonModule)OpenCore(slots, slotId, catalog, true, routeAdapter,
                (path, data, save) => new DailyDungeonModule(path, data, save, routeAdapter,
                    levelAdapter, leaderboardAdapter, options));
        }

        public static new DailyDungeonModule OpenPrivateMap(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog = null, IDailyDungeonLevelAdapter levelAdapter = null,
            IDailyDungeonLeaderboardAdapter leaderboardAdapter = null,
            DailyDungeonModuleOptions options = null, IDailyDungeonRouteAdapter routeAdapter = null)
        {
            routeAdapter = routeAdapter ?? ConfiguredDailyDungeonRouteAdapter.LoadDefault();
            return (DailyDungeonModule)OpenCore(slots, slotId, catalog, false, routeAdapter,
                (path, data, save) => new DailyDungeonModule(path, data, save, routeAdapter,
                    levelAdapter, leaderboardAdapter, options));
        }

        /// <summary>
        /// Builds the complete entry page state for all accessible public
        /// dungeons.  The UI does not need to know how the catalog is grouped
        /// or how a level is adapted to a route.
        /// </summary>
        public DailyDungeonEntryPageModel BuildEntryPageModel(
            Func<int, int, bool> regionIsPublic,
            Func<int, int, bool> conditionMet = null)
        {
            if (regionIsPublic == null) throw new ArgumentNullException(nameof(regionIsPublic));
            var entries = new List<DailyDungeonEntryModel>();
            foreach (DailyDungeonRuntimeBattle dungeon in PublicDungeons(regionIsPublic))
            {
                Func<int, bool> dungeonCondition = conditionMet == null ? null
                    : id => conditionMet(dungeon.id, id);
                var difficulties = VisibleDifficulties(dungeon.id, dungeonCondition)
                    .Select(value => new DailyDungeonDifficultyModel
                    {
                        Difficulty = value.difficulty,
                        IsPrivateRoute = ResolveLevel(dungeon.id, value.difficulty)?.IsPrivateRoute == true,
                        IsUnlocked = IsDifficultyUnlocked(dungeon.id, value.difficulty, dungeonCondition),
                        BestGrade = BestGrade(dungeon.id, value.difficulty)
                    }).ToArray();
                entries.Add(new DailyDungeonEntryModel
                {
                    DungeonId = dungeon.id,
                    Title = dungeon.title ?? string.Empty,
                    EntryMapClassId = dungeon.entryMapClassId,
                    EntryRegionId = dungeon.entryRegionId,
                    BestGrade = difficulties.Where(value => !value.IsPrivateRoute)
                        .Select(value => value.BestGrade).DefaultIfEmpty(0).Max(),
                    DefaultDifficulty = DefaultDifficulty(dungeon.id),
                    Difficulties = difficulties
                });
            }
            return new DailyDungeonEntryPageModel
            {
                Entries = entries.AsReadOnly(),
                EntryAllowance = EntryAllowance(),
                PendingClaimCount = PendingClaims.Count,
                HasActiveRun = ActiveRun != null,
                RewardProgress = DailyDungeonGetRewardCount,
                MaxAwardTimesPerClaim = MaxAwardTimesPerClaim
            };
        }

        /// <summary>
        /// Projects the active route into the data required by the official
        /// big-map HUD.  Room choice, battle and persistence remain commands on
        /// this same module; presentation only renders this projection.
        /// </summary>
        public DailyDungeonRouteHudModel BuildRouteHudModel()
        {
            DailyDungeonRunSave run = ActiveRun;
            if (run == null) return null;
            DailyDungeonRuntimeMap map = FindMap(run.mapGroupId);
            if (map == null) return null;
            bool choosing = run.clearedRoomIds.Contains(run.currentRoomId)
                && !run.privateRouteComplete;
            var rooms = new List<DailyDungeonRouteRoomModel>();
            foreach (int roomId in map.roomArray ?? Array.Empty<int>())
            {
                DailyDungeonRuntimeRoom room = FindRoom(roomId);
                if (room == null) continue;
                rooms.Add(new DailyDungeonRouteRoomModel
                {
                    RoomId = room.id,
                    Type = room.type,
                    StageId = room.stageId,
                    NextRoomIds = (room.nextRoomIds ?? Array.Empty<int>()).ToArray(),
                    IsCurrent = room.id == run.currentRoomId,
                    IsCleared = run.clearedRoomIds.Contains(room.id),
                    IsSelectable = !run.privateRouteComplete && !run.clearedRoomIds.Contains(room.id)
                        && (room.id == run.currentRoomId || choosing
                            && run.clearedRoomIds.Contains(run.currentRoomId)
                            && (FindRoom(run.currentRoomId)?.nextRoomIds ?? Array.Empty<int>()).Contains(room.id))
                });
            }
            return new DailyDungeonRouteHudModel
            {
                Run = run,
                Map = map,
                IsSelectingNextRoom = choosing,
                CurrentGrade = CurrentGrade(),
                Score = CalculateScore(run),
                Rooms = rooms.AsReadOnly(),
                GradeRows = (map.gradeRows ?? Array.Empty<DailyDungeonRuntimeGradeRow>())
                    .Where(value => value != null && value.difficulty == run.difficulty)
                    .Select(value => new KeyValuePair<int, int>(value.grade, value.row))
                    .ToArray()
            };
        }

        /// <summary>
        /// Keeps the official result preview behind the same module seam as
        /// claiming.  The caller cannot accidentally preview another module's
        /// catalog or reward rules.
        /// </summary>
        public DailyDungeonSettlementHudModel BuildSettlementHudModel(
            DailyDungeonPendingClaimSave pending, int requestedAwardTimes,
            int playerLevel, int subRank)
        {
            return DailyDungeonSettlementHudModelBuilder.Build(this, pending,
                requestedAwardTimes, playerLevel, subRank);
        }

        /// <summary>
        /// Returns the official Type 22 cumulative claim rewards, exposed as
        /// the entry page's cumulative gift state.  These rewards are independent from
        /// the daily claim pool and are claimed through ClaimDailyDungeonAchievement.
        /// </summary>
        public DailyDungeonGiftModel BuildGiftModel(int showCount = 5,
            bool showReceived = false)
        {
            return new DailyDungeonGiftModel
            {
                ClaimProgress = DailyDungeonGetRewardCount,
                Achievements = DailyDungeonAchievementRows(showCount, showReceived),
                ClaimedIndexes = (Save.claimedAchievementIndexes ?? new List<int>()).ToArray()
            };
        }
    }

    public sealed class DailyDungeonEntryPageModel
    {
        public IReadOnlyList<DailyDungeonEntryModel> Entries { get; internal set; }
        public DailyDungeonEntryAllowanceState EntryAllowance { get; internal set; }
        public int PendingClaimCount { get; internal set; }
        public bool HasActiveRun { get; internal set; }
        public long RewardProgress { get; internal set; }
        public int MaxAwardTimesPerClaim { get; internal set; }
    }

    public sealed class DailyDungeonEntryModel
    {
        public int DungeonId { get; internal set; }
        public string Title { get; internal set; }
        public int EntryMapClassId { get; internal set; }
        public int EntryRegionId { get; internal set; }
        public int BestGrade { get; internal set; }
        public int DefaultDifficulty { get; internal set; }
        public IReadOnlyList<DailyDungeonDifficultyModel> Difficulties { get; internal set; }
    }

    public sealed class DailyDungeonDifficultyModel
    {
        public int Difficulty { get; internal set; }
        public bool IsPrivateRoute { get; internal set; }
        public bool IsUnlocked { get; internal set; }
        public int BestGrade { get; internal set; }
    }

    public sealed class DailyDungeonRouteHudModel
    {
        public DailyDungeonRunSave Run { get; internal set; }
        public DailyDungeonRuntimeMap Map { get; internal set; }
        public bool IsSelectingNextRoom { get; internal set; }
        public int CurrentGrade { get; internal set; }
        public DailyDungeonScore Score { get; internal set; }
        public IReadOnlyList<DailyDungeonRouteRoomModel> Rooms { get; internal set; }
        public IReadOnlyList<KeyValuePair<int, int>> GradeRows { get; internal set; }
    }

    public sealed class DailyDungeonRouteRoomModel
    {
        public int RoomId { get; internal set; }
        public int Type { get; internal set; }
        public int StageId { get; internal set; }
        public IReadOnlyList<int> NextRoomIds { get; internal set; }
        public bool IsCurrent { get; internal set; }
        public bool IsCleared { get; internal set; }
        public bool IsSelectable { get; internal set; }
    }

    public sealed class DailyDungeonGiftModel
    {
        public long ClaimProgress { get; internal set; }
        public IReadOnlyList<DailyDungeonAchievementMilestone> Achievements { get; internal set; }
        public IReadOnlyList<int> ClaimedIndexes { get; internal set; }
    }
}
