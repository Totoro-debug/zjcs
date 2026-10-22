using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    public enum ProgressionResultStatus
    {
        Applied,
        Ready,
        Unchanged,
        NotReady,
        Unsupported,
        MissingTask,
        InvalidState
    }

    [Serializable]
    public sealed class ProgressionResult
    {
        public ProgressionResultStatus Status;
        public int TaskClassId;
        public string UnsupportedCondition;
        public string Message;
        public bool IsApplied => Status == ProgressionResultStatus.Applied;
    }

    [Serializable]
    public sealed class OfflineMainlineSave
    {
        public int schema = 1;
        public long CreatedAtUnixSeconds;
        public OfflinePlayerProgress Player = new OfflinePlayerProgress();
        public OfflineWorldProgress World = new OfflineWorldProgress();
        public OfflineTaskProgress Tasks = new OfflineTaskProgress();
    }

    [Serializable]
    public sealed class OfflinePlayerProgress
    {
        public long Experience;
        public long BlessingExperience;
        public int Level;
        public int Rank;
        public int SubRank;
        public int SeasonRank;
        public int SeasonBaseLevel;
        public int MaxProfessionRank;
        public bool HasActiveSeasonRecord;
        public bool RankTutorialSeen;
        public bool RankTutorialRewardClaimed;
        public string CharacterName = string.Empty;
        public bool CharacterCreated;
        public int InitialWeaponType;
        public string GuildName = string.Empty;
        public List<OfflineItemStack> Inventory = new List<OfflineItemStack>();
        public string CommerceStateJson = string.Empty;
        public List<string> AppliedInventoryEventIds = new List<string>();
        public long InventoryRandomState;
        public List<int> UnlockedMapClassIds = new List<int>();
        public OfflineWardrobeProgress Wardrobe = new OfflineWardrobeProgress();
        public OfflineEquipmentProfile Equipment = new OfflineEquipmentProfile();
        public OfflineSkillProfile Skills = new OfflineSkillProfile();
        public List<string> AppliedCharacterEventIds = new List<string>();
    }

    [Serializable]
    public sealed class OfflineWardrobeProgress
    {
        public int Sex;
        public int Profession = 1;
        public List<OfflineAppearanceSkin> BaseSkins = new List<OfflineAppearanceSkin>();
        public List<OfflineAppearanceSkin> WornSkins = new List<OfflineAppearanceSkin>();
        public List<OfflineAppearanceSkin> WornItems = new List<OfflineAppearanceSkin>();
        public List<OfflineAppearanceWearTime> WearTimes = new List<OfflineAppearanceWearTime>();
        public List<OfflineAppearanceDye> Dyes = new List<OfflineAppearanceDye>();
        public List<OfflineTimedAppearance> Expiries = new List<OfflineTimedAppearance>();
        public List<int> LimitedTitleCollection = new List<int>();
        public int WornTitleId;
        public long FashionExperience;
        public int AppearanceLevel;
    }

    [Serializable]
    public sealed class OfflineAppearanceSkin
    {
        public int Type;
        public int Id;
    }

    [Serializable]
    public sealed class OfflineAppearanceWearTime
    {
        public int Type;
        public long UnixSeconds;
    }

    [Serializable]
    public sealed class OfflineAppearanceDye
    {
        public int Type;
        public int ColorId;
        public int Hue;
        public int Saturation;
        public int Value;
    }

    [Serializable]
    public sealed class OfflineTimedAppearance
    {
        public int ItemId;
        public long ExpiresAtUnixSeconds;
    }

    [Serializable]
    public sealed class OfflineItemStack
    {
        public int ItemId;
        public long Count;
        public List<OfflineItemUnitRecord> UnitRecords = new List<OfflineItemUnitRecord>();
    }

    [Serializable]
    public sealed class OfflineItemUnitRecord
    {
        public long AcquiredAtUnixSeconds;
        public long ExpiresAtUnixSeconds;
    }

    [Serializable]
    public sealed class OfflineWorldProgress
    {
        public List<OfflineMapProgress> Maps = new List<OfflineMapProgress>();
        public List<OfflineMapBattleCount> BattleCounts = new List<OfflineMapBattleCount>();
        public List<string> CompletedEntityTags = new List<string>();
        public List<OfflineMapRegionKey> ActivatedGoddessRegions = new List<OfflineMapRegionKey>();
        public List<OfflineChestCount> OpenChestCounts = new List<OfflineChestCount>();
        public List<string> AppliedEventIds = new List<string>();
    }

    [Serializable]
    public sealed class OfflineMapProgress
    {
        public int MapClassId;
        public List<OfflineRegionProgress> Regions = new List<OfflineRegionProgress>();
    }

    [Serializable]
    public sealed class OfflineRegionProgress
    {
        public int Region;
        public int Progress;
    }

    [Serializable]
    public sealed class OfflineMapBattleCount
    {
        public int MapClassId;
        public int Region;
        public int Result;
        public int Count;
    }

    [Serializable]
    public sealed class OfflineMapRegionKey
    {
        public int MapClassId;
        public int Region;
    }

    [Serializable]
    public sealed class OfflineChestCount
    {
        public int ChestId;
        public int Count;
    }

    [Serializable]
    public sealed class OfflineTaskProgress
    {
        public List<int> ClaimedMainlineTaskIds = new List<int>();
    }

    public sealed class OfflineMainlineProgressSession
    {
        private readonly string savePath;
        private readonly OfficialMainlineTaskCatalog catalog;

        private OfflineMainlineProgressSession(string savePath, OfficialMainlineTaskCatalog catalog,
            OfflineMainlineSave save, SaveReadStatus readStatus)
        {
            this.savePath = savePath;
            this.catalog = catalog;
            Save = save;
            ReadStatus = readStatus;
        }

        public OfflineMainlineSave Save { get; private set; }
        public SaveReadStatus ReadStatus { get; }

        public static OfflineMainlineProgressSession Open(ProfileSlotCatalog slots, string slotId,
            OfficialMainlineTaskCatalog catalog)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            string path = slots.GetProgressPath(slotId);
            SaveReadResult result = LocalJsonSaveFile.Read(path, IsValidSaveJson);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("The selected profile progress save is corrupt.");
            OfflineMainlineSave save = result.Json == null ? new OfflineMainlineSave() : ParseSave(result.Json);
            long createdAt = slots.GetCreatedAtUnixSeconds(slotId);
            if (save.CreatedAtUnixSeconds == 0 && createdAt > 0)
            {
                save.CreatedAtUnixSeconds = createdAt;
                LocalJsonSaveFile.Write(path, JsonUtility.ToJson(save, true), IsValidSaveJson);
            }
            return new OfflineMainlineProgressSession(path, catalog, save, result.Status);
        }

        public static OfflineMainlineProgressSession OpenForCommerce(ProfileSlotCatalog slots, string slotId)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            string path = slots.GetProgressPath(slotId);
            SaveReadResult result = LocalJsonSaveFile.Read(path, IsValidSaveJson);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("The selected profile progress save is corrupt.");
            if (result.Json == null)
                throw new FileNotFoundException("The selected profile has no character progress save.", path);
            OfflineMainlineSave save = ParseSave(result.Json);
            return new OfflineMainlineProgressSession(path, null, save, result.Status);
        }

        public static OfflineMainlineProgressSession OpenCharacter(ProfileSlotCatalog slots, string slotId)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            string path = slots.GetProgressPath(slotId);
            SaveReadResult result = LocalJsonSaveFile.Read(path, IsValidSaveJson);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("The selected profile progress save is corrupt.");
            OfflineMainlineSave save = result.Json == null ? new OfflineMainlineSave() : ParseSave(result.Json);
            return new OfflineMainlineProgressSession(path, null, save, result.Status);
        }

        /// <summary>
        /// Re-reads the profile from disk so a feature that owns a long-lived session never
        /// writes over inventory or character changes committed by another feature.
        /// </summary>
        public void Reload()
        {
            SaveReadResult result = LocalJsonSaveFile.Read(savePath, IsValidSaveJson);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("The selected profile progress save is corrupt.");
            if (result.Json != null) Save = ParseSave(result.Json);
        }

        public ProgressionResult InitializeMap(int mapClassId, IReadOnlyList<int> regionIds)
        {
            if (mapClassId <= 0 || regionIds == null || regionIds.Count == 0)
                return Result(ProgressionResultStatus.InvalidState, message: "Official map identity and regions are required.");
            var unique = new SortedSet<int>();
            foreach (int region in regionIds)
                if (region < 0 || !unique.Add(region))
                    return Result(ProgressionResultStatus.InvalidState, message: "Official map regions are invalid.");

            OfflineMapProgress current = FindMap(Save, mapClassId);
            if (current != null)
            {
                if (!current.Regions.Select(value => value.Region).OrderBy(value => value).SequenceEqual(unique))
                    return Result(ProgressionResultStatus.InvalidState, message: "Saved map regions differ from official map data.");
                return Result(ProgressionResultStatus.Unchanged);
            }

            OfflineMainlineSave next = Clone(Save);
            var map = new OfflineMapProgress { MapClassId = mapClassId };
            foreach (int region in unique) map.Regions.Add(new OfflineRegionProgress { Region = region, Progress = 0 });
            next.World.Maps.Add(map);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult RecordMapBattle(int mapClassId, int region, int result, string eventId)
        {
            if (!HasMapRegion(Save, mapClassId, region) || result < 0 || string.IsNullOrWhiteSpace(eventId))
                return Result(ProgressionResultStatus.InvalidState, message: "A real map battle receipt is required.");
            if (Save.World.AppliedEventIds.Contains(eventId)) return Result(ProgressionResultStatus.Unchanged);

            OfflineMainlineSave next = Clone(Save);
            OfflineMapBattleCount entry = next.World.BattleCounts.FirstOrDefault(value =>
                value.MapClassId == mapClassId && value.Region == region && value.Result == result);
            if (entry == null)
            {
                entry = new OfflineMapBattleCount { MapClassId = mapClassId, Region = region, Result = result };
                next.World.BattleCounts.Add(entry);
            }
            if (entry.Count == int.MaxValue)
                return Result(ProgressionResultStatus.InvalidState, message: "Map battle count is exhausted.");
            entry.Count++;
            next.World.AppliedEventIds.Add(eventId);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult SetMapRegionProgress(int mapClassId, int region, int fixedProgress)
        {
            if (fixedProgress < 0 || fixedProgress > 10000 || !HasMapRegion(Save, mapClassId, region))
                return Result(ProgressionResultStatus.InvalidState, message: "Map progress is outside the official region range.");
            OfflineRegionProgress current = FindRegion(Save, mapClassId, region);
            if (fixedProgress <= current.Progress) return Result(ProgressionResultStatus.Unchanged);

            OfflineMainlineSave next = Clone(Save);
            FindRegion(next, mapClassId, region).Progress = fixedProgress;
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult RecordEntityTagCompleted(string tag, string eventId)
        {
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(eventId))
                return Result(ProgressionResultStatus.InvalidState, message: "A completed official entity tag and receipt are required.");
            if (Save.World.AppliedEventIds.Contains(eventId)) return Result(ProgressionResultStatus.Unchanged);
            OfflineMainlineSave next = Clone(Save);
            if (!next.World.CompletedEntityTags.Contains(tag)) next.World.CompletedEntityTags.Add(tag);
            next.World.AppliedEventIds.Add(eventId);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult RecordGoddessActivated(int mapClassId, int region, string eventId)
        {
            if (!HasMapRegion(Save, mapClassId, region) || string.IsNullOrWhiteSpace(eventId))
                return Result(ProgressionResultStatus.InvalidState, message: "A real activated map region and receipt are required.");
            if (Save.World.AppliedEventIds.Contains(eventId)) return Result(ProgressionResultStatus.Unchanged);
            OfflineMainlineSave next = Clone(Save);
            if (!next.World.ActivatedGoddessRegions.Any(value => value.MapClassId == mapClassId && value.Region == region))
                next.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = mapClassId, Region = region });
            next.World.AppliedEventIds.Add(eventId);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult RecordChestOpened(int chestId, string eventId)
        {
            if (chestId < 0 || string.IsNullOrWhiteSpace(eventId))
                return Result(ProgressionResultStatus.InvalidState, message: "A real chest ID and receipt are required.");
            if (Save.World.AppliedEventIds.Contains(eventId)) return Result(ProgressionResultStatus.Unchanged);
            OfflineMainlineSave next = Clone(Save);
            OfflineChestCount entry = next.World.OpenChestCounts.FirstOrDefault(value => value.ChestId == chestId);
            if (entry == null)
            {
                entry = new OfflineChestCount { ChestId = chestId };
                next.World.OpenChestCounts.Add(entry);
            }
            if (entry.Count == int.MaxValue)
                return Result(ProgressionResultStatus.InvalidState, message: "Chest count is exhausted.");
            entry.Count++;
            next.World.AppliedEventIds.Add(eventId);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult SetPlayerProgress(int level, int rank, int subRank, int maxProfessionRank,
            string guildName, IEnumerable<int> unlockedMapClassIds)
        {
            if (level < 0 || rank < 0 || subRank < 0 || maxProfessionRank < 0 || guildName == null)
                return Result(ProgressionResultStatus.InvalidState, message: "Player progression values are invalid.");
            OfflineMainlineSave next = Clone(Save);
            next.Player.Level = level;
            next.Player.Rank = rank;
            next.Player.SubRank = subRank;
            next.Player.MaxProfessionRank = maxProfessionRank;
            next.Player.GuildName = guildName;
            next.Player.UnlockedMapClassIds = new List<int>();
            if (unlockedMapClassIds != null)
                foreach (int id in unlockedMapClassIds)
                {
                    if (id <= 0) return Result(ProgressionResultStatus.InvalidState, message: "Unlocked map IDs must be positive.");
                    if (!next.Player.UnlockedMapClassIds.Contains(id)) next.Player.UnlockedMapClassIds.Add(id);
                }
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult ApplyCharacterMutation(Action<OfflinePlayerProgress> mutation, string eventId = null)
        {
            if (mutation == null)
                return Result(ProgressionResultStatus.InvalidState, message: "A character mutation is required.");
            if (!string.IsNullOrWhiteSpace(eventId) && Save.Player.AppliedCharacterEventIds.Contains(eventId))
                return Result(ProgressionResultStatus.Unchanged);

            OfflineMainlineSave next = Clone(Save);
            try
            {
                mutation(next.Player);
            }
            catch (Exception exception)
            {
                return Result(ProgressionResultStatus.InvalidState, message: exception.Message);
            }
            if (!string.IsNullOrWhiteSpace(eventId)) next.Player.AppliedCharacterEventIds.Add(eventId);
            if (!Validate(next))
                return Result(ProgressionResultStatus.InvalidState, message: "Character data failed profile validation.");
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult ApplyCharacterMutationIfChanged(Func<OfflinePlayerProgress, bool> mutation,
            string eventId = null)
        {
            if (mutation == null)
                return Result(ProgressionResultStatus.InvalidState, message: "A character mutation is required.");
            if (!string.IsNullOrWhiteSpace(eventId) && Save.Player.AppliedCharacterEventIds.Contains(eventId))
                return Result(ProgressionResultStatus.Unchanged);

            OfflineMainlineSave next = Clone(Save);
            bool changed;
            try
            {
                changed = mutation(next.Player);
            }
            catch (Exception exception)
            {
                return Result(ProgressionResultStatus.InvalidState, message: exception.Message);
            }
            if (!changed) return Result(ProgressionResultStatus.Unchanged);
            if (!string.IsNullOrWhiteSpace(eventId)) next.Player.AppliedCharacterEventIds.Add(eventId);
            if (!Validate(next))
                return Result(ProgressionResultStatus.InvalidState, message: "Character data failed profile validation.");
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public ProgressionResult GetCurrentTask(out OfficialMainlineTaskDefinition task, out bool readyToClaim)
        {
            task = null;
            readyToClaim = false;
            if (catalog == null)
                return Result(ProgressionResultStatus.Unsupported, message: "This session only exposes character progress.");
            foreach (OfficialMainlineTaskDefinition candidate in catalog.OrderedTasks)
            {
                if (Save.Tasks.ClaimedMainlineTaskIds.Contains(candidate.classId)) continue;
                ConditionEvaluation accepted = Evaluate(candidate.acquestCondition, Save, 0);
                if (accepted.Status == ProgressionResultStatus.Unsupported)
                    return Result(ProgressionResultStatus.Unsupported, candidate.classId, accepted.UnsupportedCondition);
                if (!accepted.IsMet) continue;
                if (candidate.displayCondition != null)
                {
                    ConditionEvaluation displayed = Evaluate(candidate.displayCondition, Save, 0);
                    if (displayed.Status == ProgressionResultStatus.Unsupported)
                        return Result(ProgressionResultStatus.Unsupported, candidate.classId, displayed.UnsupportedCondition);
                    if (!displayed.IsMet) continue;
                }
                task = candidate;
                ConditionEvaluation finish = Evaluate(candidate.finishCondition, Save, 0);
                if (finish.Status == ProgressionResultStatus.Unsupported)
                    return Result(ProgressionResultStatus.Unsupported, candidate.classId, finish.UnsupportedCondition);
                readyToClaim = finish.IsMet;
                return Result(readyToClaim ? ProgressionResultStatus.Ready : ProgressionResultStatus.NotReady,
                    candidate.classId);
            }
            return Result(ProgressionResultStatus.NotReady, message: "No available official mainline task.");
        }

        public ProgressionResult ClaimTask(int classId)
        {
            if (catalog == null)
                return Result(ProgressionResultStatus.Unsupported, classId, message: "This session only exposes character progress.");
            if (!catalog.TryGetTask(classId, out OfficialMainlineTaskDefinition task))
                return Result(ProgressionResultStatus.MissingTask, classId, message: "Task is not in the pinned mainline catalog.");
            if (Save.Tasks.ClaimedMainlineTaskIds.Contains(classId))
                return Result(ProgressionResultStatus.Unchanged, classId, message: "Task reward was already claimed.");

            ConditionEvaluation accepted = Evaluate(task.acquestCondition, Save, 0);
            if (accepted.Status == ProgressionResultStatus.Unsupported)
                return Result(ProgressionResultStatus.Unsupported, classId, accepted.UnsupportedCondition);
            if (!accepted.IsMet) return Result(ProgressionResultStatus.NotReady, classId);
            if (task.displayCondition != null)
            {
                ConditionEvaluation displayed = Evaluate(task.displayCondition, Save, 0);
                if (displayed.Status == ProgressionResultStatus.Unsupported)
                    return Result(ProgressionResultStatus.Unsupported, classId, displayed.UnsupportedCondition);
                if (!displayed.IsMet) return Result(ProgressionResultStatus.NotReady, classId);
            }

            ConditionEvaluation finish = Evaluate(task.finishCondition, Save, 0);
            if (finish.Status == ProgressionResultStatus.Unsupported)
                return Result(ProgressionResultStatus.Unsupported, classId, finish.UnsupportedCondition);
            if (!finish.IsMet) return Result(ProgressionResultStatus.NotReady, classId);
            if (task.rewardRuleIds.Length != 0 || task.rewardRuleIdsWithProbability.Length != 0)
                return Result(ProgressionResultStatus.Unsupported, classId, "RewardRuleId");

            OfflineMainlineSave next = Clone(Save);
            foreach (OfficialTaskReward reward in task.rewards)
            {
                if (!catalog.ContainsRewardItem(reward.itemId) || reward.count < 0)
                    return Result(ProgressionResultStatus.InvalidState, classId, message: "Task reward does not match the pinned item catalog.");
                if (reward.itemId == catalog.PlayerExpItemId)
                {
                    if (long.MaxValue - next.Player.Experience < reward.count)
                        return Result(ProgressionResultStatus.InvalidState, classId, message: "Player experience is exhausted.");
                    next.Player.Experience += reward.count;
                    continue;
                }

                if (!catalog.TryGetRewardItemType(reward.itemId, out int itemType)
                    || itemType is 100 or 101 or 200 or 201 or 202 or 203 or 204 or 205 or 210 or 211 or 212 or 305 or 400)
                    return Result(ProgressionResultStatus.Unsupported, classId, "RewardItemType:" + reward.itemId);
                OfflineItemStack stack = next.Player.Inventory.FirstOrDefault(value => value.ItemId == reward.itemId);
                if (stack == null)
                {
                    stack = new OfflineItemStack { ItemId = reward.itemId };
                    next.Player.Inventory.Add(stack);
                }
                if (long.MaxValue - stack.Count < reward.count)
                    return Result(ProgressionResultStatus.InvalidState, classId, message: "Reward inventory count is exhausted.");
                stack.Count += reward.count;
            }
            next.Tasks.ClaimedMainlineTaskIds.Add(classId);
            return Commit(next, Result(ProgressionResultStatus.Applied, classId));
        }

        public long InventoryCount(int itemId)
        {
            OfflineItemStack stack = Save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            return stack == null ? 0 : stack.Count;
        }

        public ProgressionResult ApplyInventoryTransaction(string eventId, Action<OfflineMainlineSave> mutation)
        {
            if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 160 || mutation == null)
                return Result(ProgressionResultStatus.InvalidState, message: "Inventory transaction identity and mutation are required.");
            if (Save.Player.AppliedInventoryEventIds.Contains(eventId))
                return Result(ProgressionResultStatus.Unchanged);

            OfflineMainlineSave next = Clone(Save);
            try
            {
                mutation(next);
                next.Player.AppliedInventoryEventIds.Add(eventId);
                if (!Validate(next))
                    return Result(ProgressionResultStatus.InvalidState, message: "Inventory transaction produced an invalid player save.");
            }
            catch (Exception exception)
            {
                return Result(ProgressionResultStatus.InvalidState, message: exception.Message);
            }
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        private ProgressionResult Commit(OfflineMainlineSave next, ProgressionResult result)
        {
            string json = JsonUtility.ToJson(next, true);
            LocalJsonSaveFile.Write(savePath, json, IsValidSaveJson);
            Save = next;
            return result;
        }

        private static ConditionEvaluation Evaluate(OfficialTaskCondition condition, OfflineMainlineSave save, int depth)
        {
            if (condition == null) return ConditionEvaluation.Met(true);
            if (depth > 20) return ConditionEvaluation.Unsupported("ConditionDepth");
            switch (condition.type)
            {
                case "NoneCondition":
                case "AlwaysCondition":
                    return ConditionEvaluation.Met(true);
                case "TaskCompleteCondition":
                    return ConditionEvaluation.Met(condition.taskClassIds != null
                        && condition.taskClassIds.All(save.Tasks.ClaimedMainlineTaskIds.Contains));
                case "MapGameTotalCountCondition":
                    if (!HasMapRegion(save, condition.mapClassId, condition.region))
                        return ConditionEvaluation.Unsupported("MapGameTotalCountCondition:MapNotInitialized");
                    OfflineMapBattleCount battle = save.World.BattleCounts.FirstOrDefault(value =>
                        value.MapClassId == condition.mapClassId && value.Region == condition.region
                        && value.Result == condition.result);
                    return ConditionEvaluation.Met((battle == null ? 0 : battle.Count) >= condition.count);
                case "WorldMapProgressCondition":
                    if (condition.region < 0 || !HasMapRegion(save, condition.mapClassId, condition.region))
                        return ConditionEvaluation.Unsupported("WorldMapProgressCondition:MapOrRegionNotInitialized");
                    return ConditionEvaluation.Met(FindRegion(save, condition.mapClassId, condition.region).Progress
                        >= condition.progress);
                case "MapLogicEntityEndByTagCondition":
                    return string.IsNullOrEmpty(condition.tag)
                        ? ConditionEvaluation.Unsupported("MapLogicEntityEndByTagCondition:TagMissing")
                        : ConditionEvaluation.Met(save.World.CompletedEntityTags.Contains(condition.tag));
                case "MapRegionGoddessCondition":
                    return ConditionEvaluation.Met(save.World.ActivatedGoddessRegions.Any(value =>
                        value.MapClassId == condition.mapClassId && value.Region == condition.region));
                case "MaxProfessionRankCondition":
                    if (save.Player.MaxProfessionRank <= 0)
                        return ConditionEvaluation.Unsupported("MaxProfessionRankCondition:ProfessionProgressNotInitialized");
                    return ConditionEvaluation.Met(save.Player.MaxProfessionRank >= condition.rank);
                case "TotalOpenChestCondition":
                    OfflineChestCount chest = save.World.OpenChestCounts.FirstOrDefault(value => value.ChestId == condition.chestId);
                    return ConditionEvaluation.Met((chest == null ? 0 : chest.Count) >= condition.count);
                default:
                    return ConditionEvaluation.Unsupported(condition.type ?? "<missing-type>");
            }
        }

        private static ProgressionResult Result(ProgressionResultStatus status, int taskClassId = 0,
            string unsupportedCondition = null, string message = null)
        {
            return new ProgressionResult
            {
                Status = status,
                TaskClassId = taskClassId,
                UnsupportedCondition = unsupportedCondition,
                Message = message ?? string.Empty
            };
        }

        private static bool IsValidSaveJson(string json)
        {
            try { return Validate(ParseSave(json)); }
            catch { return false; }
        }

        private static OfflineMainlineSave ParseSave(string json)
        {
            OfflineMainlineSave save = JsonUtility.FromJson<OfflineMainlineSave>(json);
            if (save == null) throw new FormatException("Progress save is not an object.");
            Normalize(save);
            return save;
        }

        private static bool Validate(OfflineMainlineSave save)
        {
            if (save == null || save.schema != 1 || save.Player == null || save.World == null || save.Tasks == null)
                return false;
            Normalize(save);
            if (save.Player.Experience < 0 || save.Player.Level < 0 || save.Player.Rank < 0
                || save.CreatedAtUnixSeconds < 0
                || save.Player.BlessingExperience < 0 || save.Player.SubRank < 0 || save.Player.SeasonRank < 0
                || save.Player.SeasonBaseLevel < 0 || save.Player.MaxProfessionRank < 0 || save.Player.GuildName == null
                || save.Player.CharacterName == null || save.Player.CharacterName.Length > 64
                || (save.Player.CharacterCreated && string.IsNullOrWhiteSpace(save.Player.CharacterName))
                || (save.Player.InitialWeaponType != 0 && save.Player.InitialWeaponType != 200
                    && save.Player.InitialWeaponType != 201)
                || save.Player.Wardrobe == null || save.Player.Equipment == null
                || save.Player.Skills == null || !OfflineSkillProgression.IsValid(save.Player.Skills)
                || save.Player.AppliedCharacterEventIds.Count > 200000
                || save.Player.AppliedInventoryEventIds.Count > 200000
                || save.Player.CommerceStateJson == null || save.Player.CommerceStateJson.Length > 2_000_000
                || save.Player.Inventory.Count > 20000 || save.Player.UnlockedMapClassIds.Count > 1000
                || save.World.Maps.Count > 1000 || save.World.BattleCounts.Count > 100000
                || save.World.CompletedEntityTags.Count > 100000 || save.World.ActivatedGoddessRegions.Count > 100000
                || save.World.OpenChestCounts.Count > 10000 || save.World.AppliedEventIds.Count > 200000
                || save.Tasks.ClaimedMainlineTaskIds.Count > 10000)
                return false;

            var items = new HashSet<int>();
            foreach (OfflineItemStack item in save.Player.Inventory)
                if (item == null || item.ItemId <= 0 || item.Count < 0 || !items.Add(item.ItemId)
                    || item.UnitRecords == null || item.UnitRecords.Count > item.Count
                    || item.UnitRecords.Any(record => record == null || record.AcquiredAtUnixSeconds < 0
                        || record.ExpiresAtUnixSeconds < 0)) return false;
            if (!UniqueStrings(save.Player.AppliedCharacterEventIds) || !UniqueStrings(save.Player.AppliedInventoryEventIds)
                || !ValidateWardrobe(save.Player.Wardrobe, save.Player.Inventory)
                || !ValidateEquipment(save.Player.Equipment)) return false;
            if (!UniquePositiveIds(save.Player.UnlockedMapClassIds)) return false;
            var mapIds = new HashSet<int>();
            foreach (OfflineMapProgress map in save.World.Maps)
            {
                if (map == null || map.MapClassId <= 0 || map.Regions == null || map.Regions.Count == 0
                    || !mapIds.Add(map.MapClassId)) return false;
                var regions = new HashSet<int>();
                foreach (OfflineRegionProgress region in map.Regions)
                    if (region == null || region.Region < 0 || region.Progress < 0 || region.Progress > 10000
                        || !regions.Add(region.Region)) return false;
            }
            foreach (OfflineMapBattleCount battle in save.World.BattleCounts)
                if (battle == null || battle.MapClassId <= 0 || battle.Region < 0 || battle.Result < 0
                    || battle.Count < 0 || !HasMapRegion(save, battle.MapClassId, battle.Region)) return false;
            if (!UniqueStrings(save.World.CompletedEntityTags) || !UniqueStrings(save.World.AppliedEventIds)) return false;
            foreach (OfflineMapRegionKey region in save.World.ActivatedGoddessRegions)
                if (region == null || !HasMapRegion(save, region.MapClassId, region.Region)) return false;
            foreach (OfflineChestCount chest in save.World.OpenChestCounts)
                if (chest == null || chest.ChestId < 0 || chest.Count < 0) return false;
            return UniquePositiveIds(save.Tasks.ClaimedMainlineTaskIds);
        }

        private static void Normalize(OfflineMainlineSave save)
        {
            if (save.Player == null) save.Player = new OfflinePlayerProgress();
            if (save.World == null) save.World = new OfflineWorldProgress();
            if (save.Tasks == null) save.Tasks = new OfflineTaskProgress();
            if (save.Player.Inventory == null) save.Player.Inventory = new List<OfflineItemStack>();
            if (save.Player.AppliedInventoryEventIds == null) save.Player.AppliedInventoryEventIds = new List<string>();
            if (save.Player.CommerceStateJson == null) save.Player.CommerceStateJson = string.Empty;
            foreach (OfflineItemStack stack in save.Player.Inventory)
                if (stack != null && stack.UnitRecords == null) stack.UnitRecords = new List<OfflineItemUnitRecord>();
            if (save.Player.UnlockedMapClassIds == null) save.Player.UnlockedMapClassIds = new List<int>();
            if (save.Player.AppliedCharacterEventIds == null) save.Player.AppliedCharacterEventIds = new List<string>();
            if (save.Player.Wardrobe == null) save.Player.Wardrobe = new OfflineWardrobeProgress();
            if (save.Player.Equipment == null) save.Player.Equipment = new OfflineEquipmentProfile();
            if (save.Player.Skills == null) save.Player.Skills = new OfflineSkillProfile();
            OfflineSkillProgression.Normalize(save.Player.Skills);
            if (save.Player.Equipment.Instances == null) save.Player.Equipment.Instances = new List<OfflineEquipmentInstance>();
            if (save.Player.Equipment.Equipped == null) save.Player.Equipment.Equipped = new List<OfflineEquippedSlot>();
            if (save.Player.Equipment.SlotProgress == null) save.Player.Equipment.SlotProgress = new List<OfflineEquipmentSlotProgress>();
            OfflineEquipmentOperations.Initialize(save.Player.Equipment);
            if (save.Player.Wardrobe.BaseSkins == null) save.Player.Wardrobe.BaseSkins = new List<OfflineAppearanceSkin>();
            if (save.Player.Wardrobe.WornSkins == null) save.Player.Wardrobe.WornSkins = new List<OfflineAppearanceSkin>();
            if (save.Player.Wardrobe.WornItems == null) save.Player.Wardrobe.WornItems = new List<OfflineAppearanceSkin>();
            if (save.Player.Wardrobe.WearTimes == null) save.Player.Wardrobe.WearTimes = new List<OfflineAppearanceWearTime>();
            if (save.Player.Wardrobe.Dyes == null) save.Player.Wardrobe.Dyes = new List<OfflineAppearanceDye>();
            if (save.Player.Wardrobe.Expiries == null) save.Player.Wardrobe.Expiries = new List<OfflineTimedAppearance>();
            if (save.Player.Wardrobe.LimitedTitleCollection == null) save.Player.Wardrobe.LimitedTitleCollection = new List<int>();
            if (save.World.Maps == null) save.World.Maps = new List<OfflineMapProgress>();
            if (save.World.BattleCounts == null) save.World.BattleCounts = new List<OfflineMapBattleCount>();
            if (save.World.CompletedEntityTags == null) save.World.CompletedEntityTags = new List<string>();
            if (save.World.ActivatedGoddessRegions == null) save.World.ActivatedGoddessRegions = new List<OfflineMapRegionKey>();
            if (save.World.OpenChestCounts == null) save.World.OpenChestCounts = new List<OfflineChestCount>();
            if (save.World.AppliedEventIds == null) save.World.AppliedEventIds = new List<string>();
            if (save.Tasks.ClaimedMainlineTaskIds == null) save.Tasks.ClaimedMainlineTaskIds = new List<int>();
            foreach (OfflineMapProgress map in save.World.Maps)
                if (map != null && map.Regions == null) map.Regions = new List<OfflineRegionProgress>();
        }

        private static bool ValidateEquipment(OfflineEquipmentProfile profile)
        {
            if (profile.NextInstanceId <= 0 || profile.Instances.Count > 20000
                || profile.Equipped.Count > 5 || profile.SlotProgress.Count > 5) return false;
            var instanceIds = new HashSet<long>();
            var itemByInstance = new Dictionary<long, OfflineEquipmentInstance>();
            foreach (OfflineEquipmentInstance item in profile.Instances)
            {
                if (item == null || item.InstanceId <= 0 || item.ItemId <= 0 || item.ItemLevel < 0 || item.BlessLevel < 0
                    || item.SecondaryProperty < 0 || item.SuitId < 0 || item.Terms == null || item.Terms.Count > 20
                    || item.Gems == null || item.Gems.Count > 5 || !instanceIds.Add(item.InstanceId)) return false;
                if (item.Terms.Any(term => term == null || term.Type < 1 || term.Type > 5 || term.PropertyId < -1
                    || term.PropertyType < 0 || term.Group < 0 || term.SkillQuality < 0 || term.Indices == null
                    || term.Indices.Count > 16 || float.IsNaN(term.Factor) || float.IsInfinity(term.Factor))) return false;
                var gemSlots = new HashSet<int>();
                foreach (OfflineEquipmentGem gem in item.Gems)
                    if (gem == null || gem.Slot < 0 || gem.Slot > 4 || gem.ItemId < 0 || !gemSlots.Add(gem.Slot)) return false;
                itemByInstance.Add(item.InstanceId, item);
            }
            if (profile.NextInstanceId <= profile.Instances.Select(item => item.InstanceId).DefaultIfEmpty(0).Max()) return false;
            var equippedPositions = new HashSet<int>();
            foreach (OfflineEquippedSlot slot in profile.Equipped)
                if (slot == null || slot.Position < 10 || slot.Position > 14 || slot.InstanceId <= 0
                    || !equippedPositions.Add(slot.Position) || !itemByInstance.ContainsKey(slot.InstanceId)) return false;
            var progressPositions = new HashSet<int>();
            foreach (OfflineEquipmentSlotProgress slot in profile.SlotProgress)
            {
                if (slot == null || slot.Position < 10 || slot.Position > 14 || slot.BaseLevel < 0 || slot.BaseLevel > 550
                    || slot.BlessLevels == null || slot.BlessLevels.Count > 100 || !progressPositions.Add(slot.Position)) return false;
                var seasons = new HashSet<int>();
                foreach (OfflineEquipmentBlessProgress bless in slot.BlessLevels)
                    if (bless == null || bless.SeasonId <= 0 || bless.Level < 0 || bless.Level > 550
                        || !seasons.Add(bless.SeasonId)) return false;
            }
            return true;
        }

        private static bool UniquePositiveIds(List<int> values)
        {
            var seen = new HashSet<int>();
            foreach (int value in values)
                if (value <= 0 || !seen.Add(value)) return false;
            return true;
        }

        private static bool UniqueStrings(List<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) return false;
            return true;
        }

        private static bool ValidateWardrobe(OfflineWardrobeProgress wardrobe, List<OfflineItemStack> inventory)
        {
            if (wardrobe.Sex < 0 || wardrobe.Sex > 1 || wardrobe.Profession <= 0
                || wardrobe.FashionExperience < 0 || wardrobe.WornTitleId < 0
                || wardrobe.AppearanceLevel < 0
                || wardrobe.BaseSkins.Count > 64 || wardrobe.WornSkins.Count > 64
                || wardrobe.WornItems.Count > 64 || wardrobe.WearTimes.Count > 64
                || wardrobe.Dyes.Count > 64 || wardrobe.Expiries.Count > 20000
                || wardrobe.LimitedTitleCollection.Count > 20000)
                return false;
            if (!UniqueAppearanceTypes(wardrobe.BaseSkins) || !UniqueAppearanceTypes(wardrobe.WornSkins)
                || !UniqueAppearanceTypes(wardrobe.WornItems)) return false;
            var wearTypes = new HashSet<int>();
            foreach (OfflineAppearanceWearTime value in wardrobe.WearTimes)
                if (value == null || value.Type <= 0 || value.UnixSeconds < 0 || !wearTypes.Add(value.Type)) return false;
            var dyeTypes = new HashSet<int>();
            foreach (OfflineAppearanceDye value in wardrobe.Dyes)
                if (value == null || value.Type <= 0 || value.ColorId < 0 || value.Hue < 0 || value.Hue > 360
                    || value.Saturation < 0 || value.Saturation > 100 || value.Value < 0 || value.Value > 100
                    || !dyeTypes.Add(value.Type)) return false;
            var expiryItems = new HashSet<int>();
            foreach (OfflineTimedAppearance value in wardrobe.Expiries)
                if (value == null || value.ItemId <= 0 || value.ExpiresAtUnixSeconds < 0
                    || !expiryItems.Add(value.ItemId) || inventory.All(item => item.ItemId != value.ItemId)) return false;
            return UniquePositiveIds(wardrobe.LimitedTitleCollection);
        }

        private static bool UniqueAppearanceTypes(List<OfflineAppearanceSkin> values)
        {
            var types = new HashSet<int>();
            foreach (OfflineAppearanceSkin value in values)
                // Official PlayerSkinType.Eye is zero, including base and creation skins.
                if (value == null || value.Type < 0 || value.Id < 0 || !types.Add(value.Type)) return false;
            return true;
        }

        private static OfflineMainlineSave Clone(OfflineMainlineSave save)
        {
            return ParseSave(JsonUtility.ToJson(save));
        }

        private static OfflineMapProgress FindMap(OfflineMainlineSave save, int mapClassId)
        {
            return save.World.Maps.FirstOrDefault(value => value.MapClassId == mapClassId);
        }

        private static OfflineRegionProgress FindRegion(OfflineMainlineSave save, int mapClassId, int region)
        {
            OfflineMapProgress map = FindMap(save, mapClassId);
            return map?.Regions.FirstOrDefault(value => value.Region == region);
        }

        private static bool HasMapRegion(OfflineMainlineSave save, int mapClassId, int region)
        {
            return mapClassId > 0 && region >= 0 && FindRegion(save, mapClassId, region) != null;
        }

        private sealed class ConditionEvaluation
        {
            public ProgressionResultStatus Status;
            public bool IsMet;
            public string UnsupportedCondition;

            public static ConditionEvaluation Met(bool value)
            {
                return new ConditionEvaluation { Status = ProgressionResultStatus.Applied, IsMet = value };
            }

            public static ConditionEvaluation Unsupported(string type)
            {
                return new ConditionEvaluation
                {
                    Status = ProgressionResultStatus.Unsupported,
                    UnsupportedCondition = type
                };
            }
        }
    }
}
