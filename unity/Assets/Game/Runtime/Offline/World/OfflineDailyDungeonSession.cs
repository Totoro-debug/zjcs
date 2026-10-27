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
        public int LevelCountId;
        public int[] SubRankCountId;
        public int[] IndexMininum;
        public string ConditionType;
        public int ConditionProfession;
        public int[] ConditionIds;
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
        public string localDate;
        public int usedEntries;
        public int purchasedEntries;
        public List<DailyDungeonDifficultySave> unlocked = new List<DailyDungeonDifficultySave>();
        public List<DailyDungeonBestGradeSave> bestGrades = new List<DailyDungeonBestGradeSave>();
        public DailyDungeonRunSave activeRun;
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
    public sealed class DailyDungeonRunSave
    {
        public string id;
        public int dungeonId;
        public int difficulty;
        public int mapGroupId;
        public int currentRoomId;
        public int grade;
        public List<int> clearedRoomIds = new List<int>();
        public string activeBattleCheckpoint = string.Empty;
        public int lastBattleRoomId;
        public int lastBattleOutcome;
        public bool privateRouteComplete;
        public long startedAtUnixSeconds;
    }

    public sealed class OfflineDailyDungeonSession
    {
        private readonly string path;
        private DailyDungeonSave save;

        public DailyDungeonRuntimeData Catalog { get; }
        public DailyDungeonSave Save => save;
        public DailyDungeonRunSave ActiveRun => save.activeRun;
        public int RemainEntries => Math.Max(0,
            Catalog.settings.DailyDungeonMaxLimitCount - save.usedEntries);

        private OfflineDailyDungeonSession(string path, DailyDungeonRuntimeData catalog, DailyDungeonSave save)
        {
            this.path = path;
            Catalog = catalog;
            this.save = save;
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
            DailyDungeonRuntimeData catalog = null)
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
            Normalize(state);
            var session = new OfflineDailyDungeonSession(path, catalog, state);
            session.ResetDailyEntries(DateTime.Now);
            return session;
        }

        public IEnumerable<DailyDungeonRuntimeBattle> PublicDungeons(Func<int, int, bool> regionIsPublic)
        {
            if (regionIsPublic == null) throw new ArgumentNullException(nameof(regionIsPublic));
            return Catalog.dungeons.Where(info => info != null
                && info.difficulties != null && info.difficulties.Any(value => value.difficulty != 50)
                && info.entryEntityId > 0 && info.entryRegionId >= 0
                && regionIsPublic(info.entryMapClassId, info.entryRegionId));
        }

        public bool IsUnlocked(int dungeonId, int difficulty)
        {
            return save.unlocked.Any(value => value.dungeonId == dungeonId && value.difficulty == difficulty);
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
                    if (difficulty.difficulty != 50 && (difficulty.tabConditionId <= 0
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
            if ((!isPublic && difficulty != 50) || data == null || data.entryConditionId > 0
                && (conditionMet == null || !conditionMet(data.entryConditionId))) return false;
            return difficulty == 0 || difficulty == 50 || IsUnlocked(dungeonId, difficulty);
        }

        public bool TryBegin(int dungeonId, int difficulty, Func<int, bool> isPublic,
            Func<int, int, bool> entryConditionMet, Func<string, int, long, bool> consumeItem,
            int currencyItemId, long nowUnixSeconds, out string error)
        {
            error = null;
            if (save.activeRun != null) { error = "已有未完成的副本路线。"; return false; }
            bool publicRoute = difficulty != 50;
            if (publicRoute && (isPublic == null || !isPublic(dungeonId))
                || !CanEnter(dungeonId, difficulty, publicRoute,
                    id => entryConditionMet != null && entryConditionMet(dungeonId, id)))
            { error = "当前副本难度尚不可进入。"; return false; }
            if (publicRoute && save.usedEntries >= Catalog.settings.DailyDungeonMaxLimitCount)
            { error = "今日副本次数已用完。"; return false; }
            int freeCount = Math.Max(0, Catalog.settings.DailyDungeonDailyFreeCount);
            bool paid = publicRoute && save.usedEntries >= freeCount;
            if (paid)
            {
                int priceIndex = save.purchasedEntries;
                long[] prices = Catalog.settings.DailyDungeonBuyCostCountList ?? Array.Empty<long>();
                long currencyCount = priceIndex < prices.Length ? prices[priceIndex] : 0;
                string receipt = "daily-dungeon:entry:" + save.localDate + ":" + save.usedEntries;
                if (currencyItemId != Catalog.settings.DailyDungeonBuyCostItemId || currencyCount <= 0
                    || consumeItem == null || !consumeItem(receipt, currencyItemId, currencyCount))
                { error = "晨星不足，无法购买挑战次数。"; return false; }
            }
            DailyDungeonRuntimeDifficulty selected = FindDifficulty(dungeonId, difficulty);
            var run = new DailyDungeonRunSave
            {
                id = Guid.NewGuid().ToString("N"), dungeonId = dungeonId,
                difficulty = difficulty, mapGroupId = selected.mapGroupId,
                currentRoomId = FindMap(selected.mapGroupId).startRoomId,
                startedAtUnixSeconds = nowUnixSeconds
            };
            DailyDungeonSave next = Clone(save);
            if (publicRoute) next.usedEntries++;
            if (paid) next.purchasedEntries++;
            next.activeRun = run;
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
            Persist(next);
            return true;
        }

        public bool RecordRoomCleared(int roomId, string battleCheckpoint)
        {
            if (save.activeRun == null || save.activeRun.currentRoomId != roomId
                || FindRoom(roomId) == null || save.activeRun.clearedRoomIds.Contains(roomId)) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.clearedRoomIds.Add(roomId);
            next.activeRun.activeBattleCheckpoint = battleCheckpoint ?? string.Empty;
            Persist(next);
            return true;
        }

        public bool RecordPrivateBattleResult(int roomId, bool won)
        {
            DailyDungeonRunSave run = save.activeRun;
            DailyDungeonRuntimeRoom room = run == null ? null : FindRoom(roomId);
            DailyDungeonRuntimeStage stage = room == null ? null : FindStage(room.stageId);
            bool hasEnemyWaves = stage != null && (stage.waveArrayIds?.Length > 0
                || stage.waveMonsterIds?.Any(wave => wave?.Length > 0) == true);
            if (run == null || run.dungeonId != 121 || run.difficulty != 50
                || run.currentRoomId != roomId || room == null || !hasEnemyWaves
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
            if (run == null || run.dungeonId != 121 || run.difficulty != 50 || run.privateRouteComplete
                || run.currentRoomId != roomId || room == null || room.type != 6
                || room.nextRoomIds == null || room.nextRoomIds.Length != 0 || FindStage(room.stageId) != null
                || run.lastBattleOutcome != 1 || !run.clearedRoomIds.Contains(run.lastBattleRoomId)) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.privateRouteComplete = true;
            Persist(next);
            return true;
        }

        public bool SaveBattleCheckpoint(string battleCheckpoint)
        {
            if (save.activeRun == null) return false;
            DailyDungeonSave next = Clone(save);
            next.activeRun.activeBattleCheckpoint = battleCheckpoint ?? string.Empty;
            Persist(next);
            return true;
        }

        public bool Finish(int grade, Action<string, DailyDungeonRunSave> grantClearedRoomRewards)
        {
            int officialGrade = CurrentGrade();
            DailyDungeonRuntimeRoom current = save.activeRun == null ? null : FindRoom(save.activeRun.currentRoomId);
            if (save.activeRun == null || current == null || current.nextRoomIds == null
                || current.nextRoomIds.Length != 0 || !save.activeRun.clearedRoomIds.Contains(current.id)
                || officialGrade <= 0 || grade != officialGrade || grantClearedRoomRewards == null) return false;
            DailyDungeonRunSave completed = Clone(save.activeRun);
            completed.grade = officialGrade;
            DailyDungeonSave next = Clone(save);
            DailyDungeonBestGradeSave best = next.bestGrades.FirstOrDefault(value =>
                value.dungeonId == completed.dungeonId && value.difficulty == completed.difficulty);
            if (best == null) next.bestGrades.Add(new DailyDungeonBestGradeSave
            { dungeonId = completed.dungeonId, difficulty = completed.difficulty, grade = grade });
            else best.grade = Math.Max(best.grade, grade);
            next.activeRun = null;
            grantClearedRoomRewards("daily-dungeon:rooms:" + completed.id, completed);
            Persist(next);
            return true;
        }

        public bool Leave(Action<string, DailyDungeonRunSave> grantClearedRoomRewards)
        {
            if (save.activeRun == null || grantClearedRoomRewards == null) return false;
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
                    return true;
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

        private void ResetDailyEntries(DateTime now)
        {
            string day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (save.localDate == day) return;
            DailyDungeonSave next = Clone(save);
            next.localDate = day;
            next.usedEntries = 0;
            next.purchasedEntries = 0;
            Persist(next);
        }

        private void Persist(DailyDungeonSave next)
        {
            Normalize(next);
            if (!IsValidSave(JsonUtility.ToJson(next)))
                throw new InvalidDataException("Daily dungeon progress is invalid.");
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(next, true), IsValidSave);
            save = next;
        }

        private static DailyDungeonSave Clone(DailyDungeonSave value) =>
            JsonUtility.FromJson<DailyDungeonSave>(JsonUtility.ToJson(value));

        private static DailyDungeonRunSave Clone(DailyDungeonRunSave value) =>
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

        private static bool IsValidSave(string json)
        {
            try
            {
                DailyDungeonSave data = JsonUtility.FromJson<DailyDungeonSave>(json);
                if (data == null || data.schema != 1 || data.usedEntries < 0 || data.purchasedEntries < 0
                    || data.localDate == null || data.unlocked == null || data.bestGrades == null) return false;
                if (data.activeRun != null && !IsEmptyRun(data.activeRun)
                    && (string.IsNullOrWhiteSpace(data.activeRun.id)
                    || data.activeRun.dungeonId <= 0 || data.activeRun.mapGroupId <= 0
                    || data.activeRun.currentRoomId <= 0 || data.activeRun.clearedRoomIds == null
                    || data.activeRun.activeBattleCheckpoint == null
                    || data.activeRun.lastBattleRoomId < 0 || data.activeRun.lastBattleOutcome < 0
                    || data.activeRun.lastBattleOutcome > 2
                    || (data.activeRun.lastBattleOutcome == 0) != (data.activeRun.lastBattleRoomId == 0))) return false;
                if (data.unlocked.Any(value => value == null || value.dungeonId <= 0 || value.difficulty < 0)
                    || data.bestGrades.Any(value => value == null || value.dungeonId <= 0
                        || value.difficulty < 0 || value.grade < 0)) return false;
                return data.unlocked.Select(value => value.dungeonId + ":" + value.difficulty).Distinct().Count()
                    == data.unlocked.Count && data.bestGrades.Select(value => value.dungeonId + ":" + value.difficulty)
                    .Distinct().Count() == data.bestGrades.Count;
            }
            catch { return false; }
        }

        private static void Normalize(DailyDungeonSave data)
        {
            if (data.localDate == null) data.localDate = string.Empty;
            if (data.unlocked == null) data.unlocked = new List<DailyDungeonDifficultySave>();
            if (data.bestGrades == null) data.bestGrades = new List<DailyDungeonBestGradeSave>();
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
                && run.lastBattleOutcome == 0 && run.startedAtUnixSeconds == 0;
        }
    }
}
