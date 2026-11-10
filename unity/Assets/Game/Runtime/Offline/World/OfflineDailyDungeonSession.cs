using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;

namespace Zjcs.Offline.World
{
    [Serializable]
    public sealed class DailyDungeonRuntimeData
    {
        public string manifest;
        public DailyDungeonRuntimeSettings settings;
        public DailyDungeonRuntimeBattle[] dungeons;
        public DailyDungeonRuntimeMap[] maps;
        public DailyDungeonRuntimeRoom[] rooms;
        public DailyDungeonRuntimeStage[] stages;
        public DailyDungeonRuntimeBattleScene[] battleScenes;
        public DailyDungeonRuntimeFormation[] formations;
        public DailyDungeonRuntimeCondition[] conditions;
        public DailyDungeonRuntimeReward[] rewardRules;
        public DailyDungeonRuntimeEquipmentPool[] randomEquipmentPools;
        public DailyDungeonRuntimeLevelNumber[] levelNumbers;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeSettings
    {
        public int DailyDungeonDailyFreeCount;
        public int DailyDungeonBuyCostItemId;
        public long[] DailyDungeonBuyCostCountList;
        public int DailyDungeonDefaultBuyCount;
        public int DailyDungeonMaxLimitCount;
        public int SuppressUnlockRank;
        public float DailyDungeonSelectNextStageOverTime;
        public int[] DailyDungeonRoomPriorityList;
        public int DailyDungeonBonfireEntityClassId;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeBattle
    {
        public int id;
        public string title;
        public int entryMapClassId;
        public int entryEntityId;
        public int entryRegionId;
        public int rank;
        public bool isShowStory;
        public DailyDungeonRuntimeDifficulty[] difficulties;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeDifficulty
    {
        public int difficulty;
        public int unlockConditionId;
        public int entryConditionId;
        public int tabConditionId;
        public int mapGroupId;
        public int retrieveGrade;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeMap
    {
        public int id;
        public int rowCount;
        public int columnCount;
        public int startRoomId;
        public int[] roomArray;
        public DailyDungeonRuntimeGradeRow[] gradeRows;
        public DailyDungeonRuntimeEquipmentDrop[] equipmentDrops;
        public DailyDungeonRuntimeGradeReward[] gradeRewards;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeGradeRow
    {
        public int difficulty;
        public int grade;
        public int row;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeEquipmentDrop
    {
        public int difficulty;
        public int minimumLevel;
        public int maximumLevel;
        public int minimumBlessLevel;
        public int maximumBlessLevel;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeGradeReward
    {
        public int difficulty;
        public int grade;
        public int[] rewardRuleIds;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeRoom
    {
        public int id;
        public int type;
        public int stageId;
        public int[] nextRoomIds;
        public DailyDungeonRuntimeSurpriseReward[] surpriseRewards;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeSurpriseReward
    {
        public int difficulty;
        public int[] rewardRuleIds;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeStage
    {
        public int id;
        public int gameId;
        public float level;
        public int subRank;
        public int maxRound;
        public int[] waveArrayIds;
        public int[] waveArrayCatalogs;
        public int[][] waveMonsterIds;
        public int[] waveMonsterIdsFlat;
        public int[] waveMonsterOffsets;
        public int monsterGroupNo;
        public int monsterLevelAdd;
        public int playerRoundLimit;
        public bool isPlayerRoundLimit;
        public bool usePlayerHp;
        public bool background;
        public int[] awards;
        public int monsterAwardGroupId;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeBattleScene
    {
        public int gameId;
        public string sceneName;
        public int gridWidth;
        public int gridHeight;
        public float[] gridOrigin;
        public DailyDungeonRuntimeBattleCell[] cells;
        public DailyDungeonRuntimeBattleFaction[] factions;
        public float[][] waveOrigins;
        public float[] waveOriginsFlat;
        public int[] waveOriginOffsets;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeBattleCell
    {
        public int x;
        public int y;
        public int groundType;
        public int height;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeBattleFaction
    {
        public int id;
        public int[] entryPosition;
        public int[] entryPositionsFlat;
        public int[][] entryPositions;
        public int[] entryDirections;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeFormation
    {
        public int id;
        public int catalog;
        public DailyDungeonRuntimeFormationMonster[] monsters;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeFormationMonster
    {
        public int groupId;
        public int x;
        public int y;
        public int rotation;
        public float levelAdd;
        public int faction;
        public int viewGroup;
        public int monsterType;
        public bool flipX;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeCondition
    {
        public int id;
        public string type;
        public int[] andIds;
        public int[] orIds;
        public long combatRating;
        public int rank;
        public int level;
        public int dungeonId;
        public int difficulty;
        public int gradeType;
        public int days;
        public bool onlyRank;
        public string op;
        public int profession;
        public int itemId;
        public long count;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeReward
    {
        public int Id;
        public int Order;
        public float Percent;
        public int[] IdList;
        public int[] WeightList;
        public float[] CountList;
        public int[] LevelCountIds;
        public int LevelCountId;
        public int[] SubRankCountId;
        public int[] IndexMininum;
        public string ConditionType;
        public int ConditionProfession;
        public int[] ConditionIds;

        public int LevelCountIdFor(int index)
        {
            return LevelCountIds != null && index >= 0 && index < LevelCountIds.Length
                ? LevelCountIds[index] : LevelCountId;
        }
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeLevelNumber
    {
        public int id;
        public float[] values;
    }

    [Serializable]
    public sealed class DailyDungeonRuntimeEquipmentPool
    {
        public int id;
        public int[] equipIds;
        public int[] weights;
    }

    [Serializable]
    public sealed class DailyDungeonSave
    {
        public int schema = 1;
        public int claimStateVersion;
        public string localDate;
        public int usedEntries;
        public int purchasedEntries;
        public int remainingClaims;
        public long lifetimeClaimedRewardTimes;
        public List<int> claimedAchievementIndexes = new List<int>();
        public List<int> claimedLeaderboardGiftIds = new List<int>();
        public List<DailyDungeonCompletionCountSave> completionCounts = new List<DailyDungeonCompletionCountSave>();
        public int purchasedAvailableEntries; // Legacy source split; cleared by claim-state migration.
        public int giftedAvailableClaims; // Legacy save field; cleared by claim-state migration.
        public List<string> entryPurchaseReceipts = new List<string>();
        public List<DailyDungeonDifficultySave> unlocked = new List<DailyDungeonDifficultySave>();
        public List<DailyDungeonBestGradeSave> bestGrades = new List<DailyDungeonBestGradeSave>();
        public List<DailyDungeonLeaderboardRecordSave> leaderboard = new List<DailyDungeonLeaderboardRecordSave>();
        public List<DailyDungeonGiftSave> gifts = new List<DailyDungeonGiftSave>();
        public List<DailyDungeonPendingClaimSave> pendingClaims = new List<DailyDungeonPendingClaimSave>();
        public DailyDungeonRunSave activeRun;
    }

    [Serializable]
    public sealed class DailyDungeonCompletionCountSave
    {
        public int dungeonId;
        public int difficulty;
        public int grade;
        public long count;
    }

    [Serializable]
    public sealed class DailyDungeonPendingClaimSave
    {
        public string id;
        public int awardTimes;
        public DailyDungeonRunSave run;
    }

    [Serializable]
    public sealed class DailyDungeonDifficultySave
    {
        public int dungeonId;
        public int difficulty;
    }

    [Serializable]
    public sealed class DailyDungeonBestGradeSave
    {
        public int dungeonId;
        public int difficulty;
        public int grade;
    }

    [Serializable]
    public sealed class DailyDungeonTeamMemberSave
    {
        public int npcClassId;
        public int level;
        public int subRank;
    }

    [Serializable]
    public sealed class DailyDungeonRunSave
    {
        public string id;
        public int dungeonId;
        public int difficulty;
        public int mapGroupId;
        public int currentRoomId;
        public int grade;
        public List<int> clearedRoomIds = new List<int>();
        public List<DailyDungeonTeamMemberSave> teamMembers = new List<DailyDungeonTeamMemberSave>();
        public string activeBattleCheckpoint = string.Empty;
        public int lastBattleRoomId;
        public int lastBattleOutcome;
        public bool privateRouteComplete;
        public long startedAtUnixSeconds;
        public long finishedAtUnixSeconds;
        public long startedAtUtcTicks;
        public long finishedAtUtcTicks;
        public long nextRoomSelectionStartedAtUtcTicks;
    }

    public class OfflineDailyDungeonSession
    {
        private readonly string path;
        private DailyDungeonSave save;
        private long profileCreatedAtUnixSeconds;
        protected readonly IDailyDungeonRouteAdapter RouteAdapter;

        public DailyDungeonRuntimeData Catalog { get; }
        public DailyDungeonSave Save => save;
        public DailyDungeonRunSave ActiveRun => save.activeRun;
        public IReadOnlyList<DailyDungeonPendingClaimSave> PendingClaims => save.pendingClaims;
        public long DailyDungeonGetRewardCount => Math.Max(0L, save.lifetimeClaimedRewardTimes);
        protected virtual int DailyFreeClaimLimit => Math.Max(0, Catalog.settings.DailyDungeonDailyFreeCount);
        public int ClaimCapacity => Math.Max(0, Catalog.settings.DailyDungeonMaxLimitCount
            - Math.Max(0, save.remainingClaims));
        public virtual int RemainEntries
        {
            get
            {
                long available = Math.Max(0L, Math.Min(Catalog.settings.DailyDungeonMaxLimitCount,
                    Math.Max(0, save.remainingClaims)));
                return (int)Math.Min(int.MaxValue, available);
            }
        }

        protected OfflineDailyDungeonSession(string path, DailyDungeonRuntimeData catalog, DailyDungeonSave save,
            IDailyDungeonRouteAdapter routeAdapter = null)
        {
            this.path = path;
            Catalog = catalog;
            this.save = save;
            RouteAdapter = routeAdapter ?? ConfiguredDailyDungeonRouteAdapter.LoadDefault();
        }

        public static DailyDungeonRuntimeData LoadCatalog()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dungeons/dungeons-runtime");
            if (asset == null) throw new InvalidDataException("Official daily dungeon runtime data is missing.");
            DailyDungeonRuntimeData data = JsonUtility.FromJson<DailyDungeonRuntimeData>(asset.text);
            ValidateCatalog(data);
            return data;
        }

        public static OfflineDailyDungeonSession Open(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog = null) => OpenCore(slots, slotId, catalog, true);

        // A private story route does not run the public dungeon's daily counter reset.
        public static OfflineDailyDungeonSession OpenPrivateMap(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog = null) => OpenCore(slots, slotId, catalog, false);

        protected static OfflineDailyDungeonSession OpenCore(ProfileSlotCatalog slots, string slotId,
            DailyDungeonRuntimeData catalog, bool resetDailyEntries,
            IDailyDungeonRouteAdapter routeAdapter = null,
            Func<string, DailyDungeonRuntimeData, DailyDungeonSave, OfflineDailyDungeonSession> factory = null)
        {
            if (slots == null || !slots.Contains(slotId))
                throw new ArgumentException("A valid profile slot is required.", nameof(slotId));
            catalog = catalog ?? LoadCatalog();
            string path = Path.Combine(slots.GetSlotDirectory(slotId), "daily-dungeon-save.json");
            SaveReadResult read = LocalJsonSaveFile.Read(path, IsValidSave);
            if (read.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("Daily dungeon progress and its backup are invalid.");
            DailyDungeonSave state = read.Json == null
                ? new DailyDungeonSave()
                : JsonUtility.FromJson<DailyDungeonSave>(read.Json);
            Normalize(state, catalog.settings.DailyDungeonDailyFreeCount,
                catalog.settings.DailyDungeonMaxLimitCount);
            var session = factory == null
                ? new OfflineDailyDungeonSession(path, catalog, state, routeAdapter)
                : factory(path, catalog, state);
            session.profileCreatedAtUnixSeconds = slots.GetCreatedAtUnixSeconds(slotId);
            if (resetDailyEntries) session.ResetDailyEntries(DateTime.Now);
            return session;
        }

        public IEnumerable<DailyDungeonRuntimeBattle> PublicDungeons(Func<int, int, bool> regionIsPublic)
        {
            if (regionIsPublic == null) throw new ArgumentNullException(nameof(regionIsPublic));
            return Catalog.dungeons.Where(info => info != null
                && info.difficulties != null && info.difficulties.Any(value => !RouteAdapter.IsPrivateDifficulty(value.difficulty))
                && info.entryEntityId > 0 && info.entryRegionId >= 0
                && regionIsPublic(info.entryMapClassId, info.entryRegionId));
        }

        public bool IsUnlocked(int dungeonId, int difficulty)
        {
            return save.unlocked.Any(value => value.dungeonId == dungeonId && value.difficulty == difficulty);
        }

        // The first public difficulty can be gated by its own unlock condition.
        // Later difficulties require the persisted unlock operation, matching the
        // official client flow where the player confirms the unlock before entry.
        public bool IsDifficultyUnlocked(int dungeonId, int difficulty,
            Func<int, bool> conditionMet = null)
        {
            DailyDungeonRuntimeDifficulty data = FindDifficulty(dungeonId, difficulty);
            if (data == null) return false;
            if (RouteAdapter.IsPrivateDifficulty(difficulty) || IsUnlocked(dungeonId, difficulty))
                return true;
            DailyDungeonRuntimeBattle dungeon = FindDungeon(dungeonId);
            int firstPublicDifficulty = (dungeon?.difficulties
                ?? Array.Empty<DailyDungeonRuntimeDifficulty>())
                .Where(value => value != null && !RouteAdapter.IsPrivateDifficulty(value.difficulty))
                .Select(value => value.difficulty).DefaultIfEmpty(-1).Min();
            if (difficulty != firstPublicDifficulty) return false;
            return data.unlockConditionId <= 0
                || conditionMet != null && conditionMet(data.unlockConditionId);
        }

        public int BestGrade(int dungeonId, int difficulty)
        {
            DailyDungeonBestGradeSave row = save.bestGrades.FirstOrDefault(value =>
                value.dungeonId == dungeonId && value.difficulty == difficulty);
            return row == null ? 0 : row.grade;
        }

        public IEnumerable<DailyDungeonRuntimeDifficulty> VisibleDifficulties(int dungeonId,
            Func<int, bool> conditionMet)
        {
            DailyDungeonRuntimeBattle dungeon = FindDungeon(dungeonId);
            if (dungeon?.difficulties == null) return Array.Empty<DailyDungeonRuntimeDifficulty>();
            return dungeon.difficulties.Where(value => value != null
                && (value.tabConditionId <= 0 || conditionMet != null && conditionMet(value.tabConditionId)))
                .OrderBy(value => value.difficulty).ToArray();
        }

        public int DefaultDifficulty(int dungeonId)
        {
            DailyDungeonRuntimeBattle dungeon = FindDungeon(dungeonId);
            if (dungeon?.difficulties == null || dungeon.difficulties.Length == 0) return -1;
            return dungeon.difficulties.Where(value => value != null
                    && (value.difficulty == 0 || IsUnlocked(dungeonId, value.difficulty)))
                .Select(value => value.difficulty).DefaultIfEmpty(-1).Max();
        }

        public IReadOnlyList<(int dungeonId, int difficulty)> NavigationOrder(
            Func<int, bool> dungeonIsPublic, Func<int, int, bool> tabConditionMet)
        {
            var result = new List<(int dungeonId, int difficulty)>();
            foreach (DailyDungeonRuntimeBattle dungeon in Catalog.dungeons.OrderBy(value => value.id))
            {
                if (dungeon == null || dungeonIsPublic == null || !dungeonIsPublic(dungeon.id)) continue;
                foreach (DailyDungeonRuntimeDifficulty difficulty in dungeon.difficulties ?? Array.Empty<DailyDungeonRuntimeDifficulty>())
                    if (!RouteAdapter.IsPrivateDifficulty(difficulty.difficulty) && (difficulty.tabConditionId <= 0
                        || tabConditionMet != null && tabConditionMet(dungeon.id, difficulty.tabConditionId)))
                        result.Add((dungeon.id, difficulty.difficulty));
            }
            return result;
        }

        public bool IsConditionMet(int conditionId, int level, long combatRating, int subRank,
            Func<int, int, bool> regionIsPublic, Func<int, int> highSubRank,
            Func<int, int> rankForSubRank, int profession = 0,
            Func<int, int, bool> professionMatches = null,
            Func<int, long> itemCount = null)
        {
            return Evaluate(conditionId, level, combatRating, subRank, regionIsPublic,
                highSubRank, rankForSubRank, profession, professionMatches, itemCount, 0);
        }

        public bool UnlockDifficulty(int dungeonId, int difficulty,
            Func<int, bool> conditionMet)
        {
            DailyDungeonRuntimeDifficulty data = FindDifficulty(dungeonId, difficulty);
            if (data == null || data.unlockConditionId <= 0 || conditionMet == null
                || !conditionMet(data.unlockConditionId)) return false;
            if (IsUnlocked(dungeonId, difficulty)) return true;
            DailyDungeonSave next = Clone(save);
            next.unlocked.Add(new DailyDungeonDifficultySave { dungeonId = dungeonId, difficulty = difficulty });
            Persist(next);
            return true;
        }

        public bool CanEnter(int dungeonId, int difficulty, bool isPublic, Func<int, bool> conditionMet)
        {
            DailyDungeonRuntimeDifficulty data = FindDifficulty(dungeonId, difficulty);
            if ((!isPublic && !RouteAdapter.IsPrivateDifficulty(difficulty)) || data == null || data.entryConditionId > 0
                && (conditionMet == null || !conditionMet(data.entryConditionId))) return false;
            return IsDifficultyUnlocked(dungeonId, difficulty, conditionMet);
        }

        public bool TryBegin(int dungeonId, int difficulty, Func<int, bool> isPublic,
            Func<int, int, bool> entryConditionMet, Func<string, int, long, bool> consumeItem,
            int currencyItemId, long nowUnixSeconds, out string error)
        {
            error = null;
            if (save.activeRun != null) { error = "已有未完成的副本路线。"; return false; }
            bool publicRoute = !RouteAdapter.IsPrivateDifficulty(difficulty);
            if (publicRoute && (isPublic == null || !isPublic(dungeonId))
                || !CanEnter(dungeonId, difficulty, publicRoute,
                    id => entryConditionMet != null && entryConditionMet(dungeonId, id)))
            { error = "当前副本难度尚不可进入。"; return false; }
            DailyDungeonRuntimeDifficulty selected = FindDifficulty(dungeonId, difficulty);
            DailyDungeonRuntimeMap selectedMap = selected == null ? null : FindMap(selected.mapGroupId);
            if (selectedMap == null || selectedMap.startRoomId <= 0)
            { error = "当前副本地图配置不可用。"; return false; }
            var run = new DailyDungeonRunSave
            {
                id = Guid.NewGuid().ToString("N"), dungeonId = dungeonId,
                difficulty = difficulty, mapGroupId = selected.mapGroupId,
                currentRoomId = selectedMap.startRoomId,
                startedAtUnixSeconds = nowUnixSeconds,
                startedAtUtcTicks = DateTime.UtcNow.Ticks
            };
            DailyDungeonSave next = Clone(save);
            next.activeRun = run;
            Persist(next);
            return true;
        }

        public bool TryPurchaseEntries(string receipt, int count, IDailyDungeonEconomyAdapter economy)
        {
            if (economy == null || string.IsNullOrWhiteSpace(receipt) || count <= 0
                || save.entryPurchaseReceipts.Contains(receipt)
                || count > ClaimCapacity)
                return false;
            long[] prices = Catalog.settings.DailyDungeonBuyCostCountList ?? Array.Empty<long>();
            if ((long)save.purchasedEntries + count > prices.Length) return false;
            long cost = 0;
            try
            {
                for (int index = 0; index < count; index++)
                {
                    long price = prices[save.purchasedEntries + index];
                    if (price <= 0) return false;
                    cost = checked(cost + price);
                }
            }
            catch (OverflowException) { return false; }
            if (cost <= 0 || !economy.Consume(receipt, Catalog.settings.DailyDungeonBuyCostItemId, cost))
                return false;
            DailyDungeonSave next = Clone(save);
            next.purchasedEntries += count;
            next.remainingClaims += count;
            next.entryPurchaseReceipts.Add(receipt);
            Persist(next);
            return true;
        }

        public bool ChooseNextRoom(int roomId)
        {
            DailyDungeonRuntimeRoom current = save.activeRun == null ? null : FindRoom(save.activeRun.currentRoomId);
            if (current == null || !save.activeRun.clearedRoomIds.Contains(current.id)
                || current.nextRoomIds == null || !current.nextRoomIds.Contains(roomId)) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.currentRoomId = roomId;
            next.activeRun.activeBattleCheckpoint = string.Empty;
            next.activeRun.nextRoomSelectionStartedAtUtcTicks = 0;
            Persist(next);
            return true;
        }

        public bool RecordRoomCleared(int roomId, string battleCheckpoint)
        {
            return RecordRoomCleared(roomId, battleCheckpoint, DateTime.UtcNow.Ticks);
        }

        public bool RecordRoomCleared(int roomId, string battleCheckpoint, long selectionStartedAtUtcTicks)
        {
            if (selectionStartedAtUtcTicks <= 0 || selectionStartedAtUtcTicks > DateTime.MaxValue.Ticks) return false;
            if (save.activeRun == null || save.activeRun.currentRoomId != roomId
                || FindRoom(roomId) == null || save.activeRun.clearedRoomIds.Contains(roomId)) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.clearedRoomIds.Add(roomId);
            next.activeRun.activeBattleCheckpoint = battleCheckpoint ?? string.Empty;
            next.activeRun.nextRoomSelectionStartedAtUtcTicks = selectionStartedAtUtcTicks;
            Persist(next);
            return true;
        }

        public bool RecordPrivateBattleResult(int roomId, bool won)
        {
            DailyDungeonRunSave run = save.activeRun;
            DailyDungeonRuntimeRoom room = run == null ? null : FindRoom(roomId);
            DailyDungeonRuntimeStage stage = room == null ? null : FindStage(room.stageId);
            if (run == null || !RouteAdapter.CanRecordPrivateBattle(run, room, stage)
                || run.currentRoomId != roomId
                || run.clearedRoomIds.Contains(roomId)) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.lastBattleRoomId = roomId;
            next.activeRun.lastBattleOutcome = won ? 1 : 2;
            next.activeRun.activeBattleCheckpoint = string.Empty;
            if (won) next.activeRun.clearedRoomIds.Add(roomId);
            Persist(next);
            return true;
        }

        public bool CompletePrivateRoute(int roomId)
        {
            DailyDungeonRunSave run = save.activeRun;
            DailyDungeonRuntimeRoom room = run == null ? null : FindRoom(roomId);
            if (run == null || !RouteAdapter.CanCompletePrivateRoute(run, room)
                || FindStage(room.stageId) != null) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.privateRouteComplete = true;
            Persist(next);
            return true;
        }

        public bool SaveBattleCheckpoint(string battleCheckpoint)
        {
            return SaveBattleCheckpoint(battleCheckpoint, null);
        }

        public bool SaveBattleCheckpoint(string battleCheckpoint,
            IReadOnlyList<DailyDungeonTeamMemberSave> teamMembers)
        {
            if (save.activeRun == null || !IsValidTeamMembers(teamMembers)) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.activeBattleCheckpoint = battleCheckpoint ?? string.Empty;
            if (teamMembers != null) next.activeRun.teamMembers = CopyTeamMembers(teamMembers);
            Persist(next);
            return true;
        }

        public virtual bool Finish(int grade)
        {
            return FinishAndPersist(grade, null);
        }

        protected bool FinishAndPersist(int grade,
            Action<DailyDungeonSave, DailyDungeonRunSave> addCompletionData, bool partial = false)
        {
            int officialGrade = CurrentGrade();
            DailyDungeonRuntimeRoom current = save.activeRun == null ? null : FindRoom(save.activeRun.currentRoomId);
            if (save.activeRun == null || current == null || current.nextRoomIds == null
                || !partial && (current.nextRoomIds.Length != 0 || !save.activeRun.clearedRoomIds.Contains(current.id))
                || RouteAdapter.IsPrivateDifficulty(save.activeRun.difficulty)
                || officialGrade <= 0 || grade != officialGrade) return false;
            DailyDungeonRunSave completed = Clone(save.activeRun);
            completed.grade = officialGrade;
            completed.activeBattleCheckpoint = string.Empty;
            completed.finishedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            completed.finishedAtUtcTicks = DateTime.UtcNow.Ticks;
            DailyDungeonSave next = Clone(save);
            DailyDungeonBestGradeSave best = next.bestGrades.FirstOrDefault(value =>
                value.dungeonId == completed.dungeonId && value.difficulty == completed.difficulty);
            if (best == null) next.bestGrades.Add(new DailyDungeonBestGradeSave
            { dungeonId = completed.dungeonId, difficulty = completed.difficulty, grade = grade });
            else best.grade = Math.Max(best.grade, grade);
            // A partial grade earns its configured rewards, but is not a cleared dungeon
            // for completion achievements or the full-route leaderboard.
            if (!partial)
            {
                DailyDungeonCompletionCountSave count = next.completionCounts.FirstOrDefault(value =>
                    value.dungeonId == completed.dungeonId && value.difficulty == completed.difficulty
                    && value.grade == completed.grade);
                if (count == null) next.completionCounts.Add(new DailyDungeonCompletionCountSave
                { dungeonId = completed.dungeonId, difficulty = completed.difficulty, grade = completed.grade, count = 1 });
                else count.count = checked(count.count + 1);
            }
            next.pendingClaims.Add(new DailyDungeonPendingClaimSave { id = completed.id, run = completed });
            next.activeRun = null;
            addCompletionData?.Invoke(next, completed);
            Persist(next);
            return true;
        }

        public bool DiscardPendingClaim(string claimId)
        {
            if (string.IsNullOrWhiteSpace(claimId)) return false;
            DailyDungeonSave next = Clone(save);
            int removed = next.pendingClaims.RemoveAll(value => value != null && value.id == claimId);
            if (removed == 0) return false;
            Persist(next);
            return true;
        }

        public virtual bool Leave(Action<string, DailyDungeonRunSave> grantClearedRoomRewards)
        {
            if (save.activeRun == null || grantClearedRoomRewards == null) return false;
            // Confirmed offline rule: leaving uses the grade reached by cleared rows.
            // Reuse the normal pending-claim transaction and its reward multipliers.
            if (!RouteAdapter.IsPrivateDifficulty(save.activeRun.difficulty) && CurrentGrade() > 0)
            {
                int grade = CurrentGrade();
                return Finish(grade) || FinishAndPersist(grade, null, true);
            }
            DailyDungeonRunSave left = Clone(save.activeRun);
            DailyDungeonSave next = Clone(save);
            next.activeRun = null;
            grantClearedRoomRewards("daily-dungeon:rooms:" + left.id, left);
            Persist(next);
            return true;
        }

        public DailyDungeonRuntimeBattle FindDungeon(int id) => Catalog.dungeons.FirstOrDefault(value => value.id == id);
        public DailyDungeonRuntimeMap FindMap(int id) => Catalog.maps.FirstOrDefault(value => value.id == id);
        public DailyDungeonRuntimeRoom FindRoom(int id) => Catalog.rooms.FirstOrDefault(value => value.id == id);
        public DailyDungeonRuntimeStage FindStage(int id) => Catalog.stages.FirstOrDefault(value => value.id == id);

        public float LevelNumber(int id, int level)
        {
            DailyDungeonRuntimeLevelNumber row = Catalog.levelNumbers?.FirstOrDefault(value => value.id == id);
            if (row?.values == null || row.values.Length == 0) return 0f;
            return row.values[Mathf.Clamp(level, 0, row.values.Length - 1)];
        }

        public int CurrentGrade()
        {
            if (save.activeRun == null) return 0;
            DailyDungeonRuntimeMap map = FindMap(save.activeRun.mapGroupId);
            if (map == null || map.roomArray == null || map.columnCount <= 0) return 0;
            int deepestRow = 0;
            foreach (int roomId in save.activeRun.clearedRoomIds)
            {
                int position = Array.IndexOf(map.roomArray, roomId);
                if (position >= 0) deepestRow = Math.Max(deepestRow, position / map.columnCount + 1);
            }
            return map.gradeRows.Where(value => value.difficulty == save.activeRun.difficulty
                    && deepestRow >= value.row)
                .Select(value => value.grade).DefaultIfEmpty(0).Max();
        }

        public int[] ClearedRoomRewardRuleIds(DailyDungeonRunSave run)
        {
            if (run == null) return Array.Empty<int>();
            return run.clearedRoomIds.Select(id => FindRoom(id))
                .Where(room => room != null)
                .SelectMany(room => room.surpriseRewards ?? Array.Empty<DailyDungeonRuntimeSurpriseReward>())
                .Where(reward => reward.difficulty == run.difficulty)
                .SelectMany(reward => reward.rewardRuleIds ?? Array.Empty<int>())
                .ToArray();
        }

        public int[] ConfiguredRewardRuleIds(DailyDungeonRunSave run, bool completed)
        {
            if (run == null) return Array.Empty<int>();
            var ids = new List<int>(ClearedRoomRewardRuleIds(run));
            if (completed && run.grade > 0)
            {
                DailyDungeonRuntimeMap map = FindMap(run.mapGroupId);
                DailyDungeonRuntimeGradeReward grade = map?.gradeRewards?.FirstOrDefault(value =>
                    value.difficulty == run.difficulty && value.grade == run.grade);
                ids.AddRange(grade?.rewardRuleIds ?? Array.Empty<int>());
            }
            return ids.ToArray();
        }

        private DailyDungeonRuntimeDifficulty FindDifficulty(int dungeonId, int difficulty)
        {
            DailyDungeonRuntimeBattle dungeon = FindDungeon(dungeonId);
            return dungeon?.difficulties?.FirstOrDefault(value => value.difficulty == difficulty);
        }

        private bool Evaluate(int conditionId, int level, long combatRating, int subRank,
            Func<int, int, bool> regionIsPublic, Func<int, int> highSubRank,
            Func<int, int> rankForSubRank, int profession,
            Func<int, int, bool> professionMatches, Func<int, long> itemCount, int depth)
        {
            if (conditionId <= 0) return true;
            if (depth > 16) return false;
            DailyDungeonRuntimeCondition condition = Catalog.conditions.FirstOrDefault(value => value.id == conditionId);
            if (condition == null) return false;
            switch (condition.type)
            {
                case "CompositeAndCondition":
                    return condition.andIds != null && condition.andIds.Length > 0
                        && condition.andIds.All(id => Evaluate(id, level, combatRating, subRank,
                            regionIsPublic, highSubRank, rankForSubRank, profession,
                            professionMatches, itemCount, depth + 1));
                case "CompositeOrCondition":
                    return condition.orIds != null && condition.orIds.Length > 0
                        && condition.orIds.Any(id => Evaluate(id, level, combatRating, subRank,
                            regionIsPublic, highSubRank, rankForSubRank, profession,
                            professionMatches, itemCount, depth + 1));
                case "HasItemCondition":
                    return itemCount != null && Compare(itemCount(condition.itemId), condition.op, condition.count);
                case "PlayerProfessionCondition":
                    return profession > 0 && profession == condition.profession;
                case "PlayerProfessionFactionCondition":
                    return profession > 0 && (profession == condition.profession
                        || professionMatches != null && professionMatches(profession, condition.profession));
                case "CombatRatingOrRankSuppressCondition":
                    if (combatRating >= condition.combatRating) return true;
                    if (subRank < Catalog.settings.SuppressUnlockRank
                        || highSubRank == null || rankForSubRank == null) return false;
                    int requiredSubRank = highSubRank(condition.rank);
                    return requiredSubRank > 0 && rankForSubRank(subRank) > rankForSubRank(requiredSubRank);
                case "DailyDungeonUnlockCondition":
                    DailyDungeonRuntimeBattle prior = FindDungeon(condition.dungeonId);
                    return prior != null && prior.entryEntityId > 0 && regionIsPublic != null
                        && regionIsPublic(prior.entryMapClassId, prior.entryRegionId);
                case "DailyDungeonPersonalGradeCondition":
                    return BestGrade(condition.dungeonId, condition.difficulty) >= condition.gradeType;
                case "LevelCondition":
                    return condition.op == ">=" ? level >= condition.level
                        : condition.op == ">" ? level > condition.level
                        : condition.op == "<=" ? level <= condition.level
                        : condition.op == "<" ? level < condition.level
                        : condition.op == "==" ? level == condition.level : false;
                case "DailyDungeonUnlockDifficultyCondition":
                    return IsUnlocked(condition.dungeonId, condition.difficulty);
                case "ServerRankUnlockDaysCondition":
                    if (condition.days <= 0) return true;
                    if (profileCreatedAtUnixSeconds <= 0) return false;
                    long elapsedProfileSeconds = Math.Max(0L,
                        DateTimeOffset.Now.ToUnixTimeSeconds() - profileCreatedAtUnixSeconds);
                    return elapsedProfileSeconds / 86400L >= condition.days;
                default:
                    return false;
            }
        }

        private static bool Compare(long value, string operation, long target)
        {
            switch (operation)
            {
                case ">=": return value >= target;
                case ">": return value > target;
                case "<=": return value <= target;
                case "<": return value < target;
                case "==": return value == target;
                case "!=": return value != target;
                default: return false;
            }
        }

        protected void ResetDailyEntries(DateTime now)
        {
            string day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (save.localDate == day) return;
            DailyDungeonSave next = Clone(save);
            next.localDate = day;
            next.usedEntries = 0;
            next.purchasedEntries = 0;
            next.purchasedAvailableEntries = 0;
            // Official GetDailyRemainCount carries the previous total balance
            // across the reset, with the free daily allowance as a floor.
            next.remainingClaims = Math.Max(save.remainingClaims, DailyFreeClaimLimit);
            next.entryPurchaseReceipts.Clear();
            Persist(next);
        }

        protected void Persist(DailyDungeonSave next)
        {
            Normalize(next, Catalog.settings.DailyDungeonDailyFreeCount,
                Catalog.settings.DailyDungeonMaxLimitCount);
            if (!IsValidSave(JsonUtility.ToJson(next)))
                throw new InvalidDataException("Daily dungeon progress is invalid.");
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(next, true), IsValidSave);
            save = next;
        }

        protected static DailyDungeonSave Clone(DailyDungeonSave value) =>
            JsonUtility.FromJson<DailyDungeonSave>(JsonUtility.ToJson(value));

        protected static DailyDungeonRunSave Clone(DailyDungeonRunSave value) =>
            JsonUtility.FromJson<DailyDungeonRunSave>(JsonUtility.ToJson(value));

        private static void ValidateCatalog(DailyDungeonRuntimeData data)
        {
            if (data == null || string.IsNullOrEmpty(data.manifest) || data.settings == null
                || data.dungeons == null || data.maps == null || data.rooms == null || data.stages == null
                || data.conditions == null || data.rewardRules == null || data.randomEquipmentPools == null
                || data.levelNumbers == null
                || data.battleScenes == null || data.formations == null
                || data.settings.DailyDungeonDailyFreeCount < 0
                || data.settings.DailyDungeonMaxLimitCount < data.settings.DailyDungeonDailyFreeCount
                || data.dungeons.Length != 76 || data.maps.Length != 136 || data.rooms.Length != 1804
                || data.randomEquipmentPools.Length != 220
                || data.randomEquipmentPools.Any(value => value == null || value.id <= 0
                    || value.equipIds == null || value.weights == null || value.equipIds.Length == 0
                    || value.equipIds.Length != value.weights.Length || !value.weights.Any(weight => weight > 0)))
                throw new InvalidDataException("Official daily dungeon catalog failed validation.");
        }

        private static bool IsValidTeamMembers(IReadOnlyList<DailyDungeonTeamMemberSave> teamMembers)
        {
            return teamMembers == null || (teamMembers.All(value => value != null
                && value.npcClassId > 0 && value.level > 0 && value.subRank > 0)
                && teamMembers.Select(value => value.npcClassId).Distinct().Count() == teamMembers.Count);
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

        private static bool IsValidSave(string json)
        {
            try
            {
                DailyDungeonSave data = JsonUtility.FromJson<DailyDungeonSave>(json);
                if (data == null || data.schema != 1 || data.claimStateVersion < 0
                    || data.usedEntries < 0 || data.purchasedEntries < 0 || data.remainingClaims < 0
                    || data.lifetimeClaimedRewardTimes < 0
                    || data.purchasedAvailableEntries < 0 || data.giftedAvailableClaims < 0
                    || data.localDate == null
                    || data.unlocked == null || data.bestGrades == null) return false;
                if (data.activeRun != null && !IsEmptyRun(data.activeRun)
                    && (string.IsNullOrWhiteSpace(data.activeRun.id)
                    || data.activeRun.dungeonId <= 0 || data.activeRun.mapGroupId <= 0
                    || data.activeRun.currentRoomId <= 0 || data.activeRun.clearedRoomIds == null
                    || data.activeRun.activeBattleCheckpoint == null
                    || !IsValidTeamMembers(data.activeRun.teamMembers)
                    || data.activeRun.lastBattleRoomId < 0 || data.activeRun.lastBattleOutcome < 0
                    || data.activeRun.lastBattleOutcome > 2
                    || (data.activeRun.lastBattleOutcome == 0) != (data.activeRun.lastBattleRoomId == 0))) return false;
                if (data.unlocked.Any(value => value == null || value.dungeonId <= 0 || value.difficulty < 0)
                    || data.bestGrades.Any(value => value == null || value.dungeonId <= 0
                        || value.difficulty < 0 || value.grade < 0)) return false;
                if (data.leaderboard != null && data.leaderboard.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.id) || value.dungeonId <= 0
                    || value.difficulty < 0 || value.grade < 0 || value.finishTicks < 0
                    || !IsValidTeamMembers(value.teamMembers))) return false;
                if (data.gifts != null && data.gifts.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.receipt) || value.count <= 0)) return false;
                if (data.pendingClaims != null && data.pendingClaims.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.id) || value.awardTimes < 0 || value.run == null
                    || value.id != value.run.id || value.run.dungeonId <= 0
                    || value.run.difficulty < 0 || value.run.mapGroupId <= 0
                    || value.run.grade <= 0 || value.run.finishedAtUnixSeconds <= 0
                    || !IsValidTeamMembers(value.run.teamMembers))) return false;
                if (data.entryPurchaseReceipts != null
                    && (data.entryPurchaseReceipts.Any(string.IsNullOrWhiteSpace)
                        || data.entryPurchaseReceipts.Distinct().Count() != data.entryPurchaseReceipts.Count)) return false;
                if (data.claimedAchievementIndexes != null
                    && (data.claimedAchievementIndexes.Any(value => value < 0)
                        || data.claimedAchievementIndexes.Distinct().Count() != data.claimedAchievementIndexes.Count)) return false;
                if (data.pendingClaims != null
                    && data.pendingClaims.Select(value => value.id).Distinct().Count() != data.pendingClaims.Count) return false;
                if (data.claimedLeaderboardGiftIds != null && (data.claimedLeaderboardGiftIds.Any(value => value <= 0)
                    || data.claimedLeaderboardGiftIds.Distinct().Count() != data.claimedLeaderboardGiftIds.Count)) return false;
                if (data.completionCounts != null && (data.completionCounts.Any(value => value == null
                    || value.dungeonId <= 0 || value.difficulty < 0 || value.grade <= 0 || value.count <= 0)
                    || data.completionCounts.Select(value => (value.dungeonId, value.difficulty, value.grade))
                        .Distinct().Count() != data.completionCounts.Count)) return false;
                return data.unlocked.Select(value => value.dungeonId + ":" + value.difficulty).Distinct().Count()
                    == data.unlocked.Count && data.bestGrades.Select(value => value.dungeonId + ":" + value.difficulty)
                    .Distinct().Count() == data.bestGrades.Count;
            }
            catch { return false; }
        }

        private static void Normalize(DailyDungeonSave data, int dailyFreeClaimLimit, int dailyMaxLimit)
        {
            if (data.localDate == null) data.localDate = string.Empty;
            if (data.unlocked == null) data.unlocked = new List<DailyDungeonDifficultySave>();
            if (data.bestGrades == null) data.bestGrades = new List<DailyDungeonBestGradeSave>();
            if (data.leaderboard == null) data.leaderboard = new List<DailyDungeonLeaderboardRecordSave>();
            if (data.gifts == null) data.gifts = new List<DailyDungeonGiftSave>();
            if (data.pendingClaims == null) data.pendingClaims = new List<DailyDungeonPendingClaimSave>();
            if (data.entryPurchaseReceipts == null) data.entryPurchaseReceipts = new List<string>();
            if (data.claimedAchievementIndexes == null) data.claimedAchievementIndexes = new List<int>();
            if (data.claimedLeaderboardGiftIds == null) data.claimedLeaderboardGiftIds = new List<int>();
            if (data.completionCounts == null) data.completionCounts = new List<DailyDungeonCompletionCountSave>();
            if (data.activeRun != null && data.activeRun.teamMembers == null)
                data.activeRun.teamMembers = new List<DailyDungeonTeamMemberSave>();
            foreach (DailyDungeonLeaderboardRecordSave record in data.leaderboard)
                if (record != null && record.teamMembers == null)
                    record.teamMembers = new List<DailyDungeonTeamMemberSave>();
            if (data.purchasedAvailableEntries < 0) data.purchasedAvailableEntries = 0;
            if (data.giftedAvailableClaims < 0) data.giftedAvailableClaims = 0;
            if (data.lifetimeClaimedRewardTimes < 0) data.lifetimeClaimedRewardTimes = 0;
            int previousClaimStateVersion = data.claimStateVersion;
            if (data.claimStateVersion < 1)
            {
                // Legacy usedEntries counted route starts, including failed runs.
                // The confirmed offline policy charges only after a successful claim.
                data.usedEntries = 0;
                data.claimStateVersion = 1;
            }
            if (data.claimStateVersion < 2)
            {
                data.remainingClaims = previousClaimStateVersion >= 1
                    ? Math.Max(0, dailyFreeClaimLimit - data.usedEntries)
                        + data.purchasedAvailableEntries + data.giftedAvailableClaims
                    : dailyFreeClaimLimit;
                data.remainingClaims = Math.Min(Math.Max(0, dailyMaxLimit), data.remainingClaims);
                data.claimStateVersion = 2;
            }
            else
            {
                data.remainingClaims = Math.Min(Math.Max(0, dailyMaxLimit), data.remainingClaims);
            }
            if (data.claimStateVersion < 3)
            {
                // Keep the persisted total pool, but stop treating server-only gifts as a local source.
                data.giftedAvailableClaims = 0;
                data.claimStateVersion = 3;
            }
            if (data.claimStateVersion < 4)
            {
                // The official client persists one claim balance, not a free/paid source split.
                data.purchasedAvailableEntries = 0;
                data.claimStateVersion = 4;
            }
            foreach (DailyDungeonGiftSave gift in data.gifts)
            {
                if (gift == null) continue;
                if (gift.receipt == null) gift.receipt = string.Empty;
                if (gift.localDate == null) gift.localDate = string.Empty;
                gift.count = Math.Max(0, gift.count);
            }
            foreach (DailyDungeonPendingClaimSave pending in data.pendingClaims)
            {
                if (pending == null) continue;
                if (pending.id == null) pending.id = string.Empty;
                pending.awardTimes = Math.Max(0, pending.awardTimes);
                if (pending.run == null) continue;
                if (pending.run.id == null) pending.run.id = string.Empty;
                if (pending.run.clearedRoomIds == null) pending.run.clearedRoomIds = new List<int>();
                if (pending.run.teamMembers == null) pending.run.teamMembers = new List<DailyDungeonTeamMemberSave>();
                if (pending.run.activeBattleCheckpoint == null) pending.run.activeBattleCheckpoint = string.Empty;
            }
            if (data.activeRun != null && IsEmptyRun(data.activeRun)) data.activeRun = null;
            if (data.activeRun != null)
            {
                if (data.activeRun.id == null) data.activeRun.id = string.Empty;
                if (data.activeRun.clearedRoomIds == null) data.activeRun.clearedRoomIds = new List<int>();
                if (data.activeRun.activeBattleCheckpoint == null) data.activeRun.activeBattleCheckpoint = string.Empty;
            }
        }

        private static bool IsEmptyRun(DailyDungeonRunSave run)
        {
            return run != null && string.IsNullOrEmpty(run.id) && run.dungeonId == 0
                && run.difficulty == 0 && run.mapGroupId == 0 && run.currentRoomId == 0
                && run.grade == 0 && (run.clearedRoomIds == null || run.clearedRoomIds.Count == 0)
                && !run.privateRouteComplete
                && string.IsNullOrEmpty(run.activeBattleCheckpoint) && run.lastBattleRoomId == 0
                && run.lastBattleOutcome == 0 && run.startedAtUnixSeconds == 0
                && run.finishedAtUnixSeconds == 0 && run.startedAtUtcTicks == 0
                && run.finishedAtUtcTicks == 0 && run.nextRoomSelectionStartedAtUtcTicks == 0;
        }
    }
}
