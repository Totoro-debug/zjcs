using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using UnityEngine;

namespace Zjcs.Offline.World
{
    public enum FantasyTowerResult
    {
        Applied,
        Unchanged,
        InvalidState,
        MissingResult
    }

    [Serializable]
    public sealed class FantasyTowerSave
    {
        public int schema = 1;
        public List<FantasyTowerEntrySave> entries = new List<FantasyTowerEntrySave>();
    }

    [Serializable]
    public sealed class FantasyTowerEntrySave
    {
        public int entryIndex;
        public int clearedLayer;
        public bool autoPushEnabled;
        public string foregroundCheckpoint = string.Empty;
        public string backgroundCheckpoint = string.Empty;
        public int backgroundStartLayer;
        public long backgroundStartedAtUnixSeconds;
        public List<FantasyTowerReward> backgroundRewards = new List<FantasyTowerReward>();
        public List<FantasyTowerPushResult> pendingResults = new List<FantasyTowerPushResult>();
    }

    [Serializable]
    public sealed class FantasyTowerPushResult
    {
        public string id;
        public long createdAtUnixSeconds;
        public int entryIndex;
        public int startLayer;
        public int endLayer;
        public long elapsedSeconds;
        public bool won;
        public bool towerCompleted;
        public List<FantasyTowerReward> rewards = new List<FantasyTowerReward>();
    }

    [Serializable]
    public sealed class FantasyTowerReward
    {
        public int itemId;
        public long count;
    }

    public sealed class OfflineFantasyTowerSession
    {
        private readonly string path;
        private FantasyTowerSave save;

        private OfflineFantasyTowerSession(string path, FantasyTowerSave save)
        {
            this.path = path;
            this.save = save;
        }

        public IReadOnlyList<FantasyTowerEntrySave> Entries => save.entries;

        public static OfflineFantasyTowerSession Open(ProfileSlotCatalog slots, string slotId)
        {
            if (slots == null || !slots.Contains(slotId))
                throw new ArgumentException("A valid profile slot is required.", nameof(slotId));
            string path = Path.Combine(slots.GetSlotDirectory(slotId), "fantasy-tower-save.json");
            SaveReadResult read = LocalJsonSaveFile.Read(path, IsValid);
            if (read.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("Fantasy tower progress and its backup are invalid.");
            FantasyTowerSave data = read.Json == null
                ? new FantasyTowerSave()
                : JsonUtility.FromJson<FantasyTowerSave>(read.Json);
            Normalize(data);
            var session = new OfflineFantasyTowerSession(path, data);
            bool resetAutoPush = data.entries.Any(entry => entry.autoPushEnabled);
            if (resetAutoPush)
            {
                foreach (FantasyTowerEntrySave entry in data.entries)
                {
                    if (string.IsNullOrEmpty(entry.foregroundCheckpoint)
                        && !string.IsNullOrEmpty(entry.backgroundCheckpoint))
                        entry.foregroundCheckpoint = entry.backgroundCheckpoint;
                    entry.backgroundCheckpoint = string.Empty;
                    entry.autoPushEnabled = false;
                    if (entry.backgroundStartLayer > 0
                        && entry.clearedLayer >= entry.backgroundStartLayer)
                        AddBackgroundResult(entry, entry.entryIndex, true,
                            IsEntryCompleted(entry.entryIndex, entry.clearedLayer),
                            DateTimeOffset.Now.ToUnixTimeSeconds());
                    else
                    {
                        entry.backgroundStartLayer = 0;
                        entry.backgroundStartedAtUnixSeconds = 0;
                        entry.backgroundRewards.Clear();
                    }
                }
            }
            if (read.Json == null || resetAutoPush) session.Persist(data);
            return session;
        }

        public FantasyTowerEntrySave GetEntry(int entryIndex)
        {
            if (!IsTowerEntry(entryIndex)) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            FantasyTowerEntrySave entry = save.entries.FirstOrDefault(value => value.entryIndex == entryIndex);
            if (entry != null) return entry;
            FantasyTowerSave next = Clone(save);
            entry = new FantasyTowerEntrySave { entryIndex = entryIndex };
            next.entries.Add(entry);
            Persist(next);
            return save.entries.First(value => value.entryIndex == entryIndex);
        }

        public FantasyTowerResult SetAutoPush(int entryIndex, bool enabled)
        {
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (enabled && (current.clearedLayer < 3 || IsEntryCompleted(entryIndex, current.clearedLayer)))
                return FantasyTowerResult.InvalidState;
            if (current.autoPushEnabled == enabled) return FantasyTowerResult.Unchanged;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave updated = Find(next, entryIndex);
            if (!enabled) FinishBackgroundBatch(updated, entryIndex,
                DateTimeOffset.Now.ToUnixTimeSeconds());
            updated.autoPushEnabled = enabled;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult BeginBackgroundPush(int entryIndex, long nowUnixSeconds)
        {
            if (!IsTowerEntry(entryIndex) || nowUnixSeconds <= 0)
                return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (!current.autoPushEnabled) return FantasyTowerResult.InvalidState;
            if (current.backgroundStartLayer > 0) return FantasyTowerResult.Unchanged;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = Find(next, entryIndex);
            entry.backgroundStartLayer = entry.clearedLayer + 1;
            entry.backgroundStartedAtUnixSeconds = nowUnixSeconds;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult FinishBackgroundPushBatch(int entryIndex, long nowUnixSeconds)
        {
            if (!IsTowerEntry(entryIndex) || nowUnixSeconds <= 0)
                return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (current.backgroundStartLayer <= 0) return FantasyTowerResult.Unchanged;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = Find(next, entryIndex);
            FinishBackgroundBatch(entry, entryIndex, nowUnixSeconds);
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult SaveForegroundCheckpoint(int entryIndex, FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.StageId <= 0 || checkpoint.Finished)
                return FantasyTowerResult.InvalidState;
            FantasyTowerSave next = Clone(save);
            FindOrAdd(next, entryIndex).foregroundCheckpoint = FightBattleCheckpointSerializer.Save(checkpoint);
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FightBattleCheckpoint LoadForegroundCheckpoint(int entryIndex, int stageId)
        {
            FantasyTowerEntrySave entry = GetEntry(entryIndex);
            if (string.IsNullOrEmpty(entry.foregroundCheckpoint)) return null;
            FightBattleCheckpoint checkpoint = FightBattleCheckpointSerializer.Load(entry.foregroundCheckpoint);
            if (checkpoint.StageId != stageId || checkpoint.Finished) return null;
            return checkpoint;
        }

        public FantasyTowerResult ClearForegroundCheckpoint(int entryIndex)
        {
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (string.IsNullOrEmpty(current.foregroundCheckpoint)) return FantasyTowerResult.Unchanged;
            FantasyTowerSave next = Clone(save);
            Find(next, entryIndex).foregroundCheckpoint = string.Empty;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult ClearBackgroundCheckpoint(int entryIndex)
        {
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (string.IsNullOrEmpty(current.backgroundCheckpoint)) return FantasyTowerResult.Unchanged;
            FantasyTowerSave next = Clone(save);
            Find(next, entryIndex).backgroundCheckpoint = string.Empty;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult SaveBackgroundCheckpoint(int entryIndex, FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.StageId <= 0 || checkpoint.Finished)
                return FantasyTowerResult.InvalidState;
            FantasyTowerSave next = Clone(save);
            FindOrAdd(next, entryIndex).backgroundCheckpoint = FightBattleCheckpointSerializer.Save(checkpoint);
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FightBattleCheckpoint LoadBackgroundCheckpoint(int entryIndex, int stageId)
        {
            FantasyTowerEntrySave entry = GetEntry(entryIndex);
            if (!entry.autoPushEnabled || string.IsNullOrEmpty(entry.backgroundCheckpoint)) return null;
            FightBattleCheckpoint checkpoint = FightBattleCheckpointSerializer.Load(entry.backgroundCheckpoint);
            if (checkpoint.StageId != stageId || checkpoint.Finished) return null;
            return checkpoint;
        }

        public FantasyTowerResult RecordBackgroundResult(FantasyTowerPushResult result)
        {
            if (!IsValidResult(result)) return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave current = GetEntry(result.entryIndex);
            if (result.startLayer != current.clearedLayer + 1
                || result.endLayer > MaxLayer(result.entryIndex)) return FantasyTowerResult.InvalidState;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = FindOrAdd(next, result.entryIndex);
            if (entry.pendingResults.Any(value => value.id == result.id)) return FantasyTowerResult.Unchanged;
            entry.pendingResults.Add(Clone(result));
            entry.backgroundCheckpoint = string.Empty;
            if (result.won) entry.clearedLayer = Math.Max(entry.clearedLayer, result.endLayer);
            if (!result.won || result.towerCompleted) entry.autoPushEnabled = false;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult CompleteBackgroundLayer(int entryIndex, int layer, int stageId,
            bool won, IReadOnlyList<FantasyTowerReward> rewards, long nowUnixSeconds)
        {
            if (!IsTowerEntry(entryIndex) || layer <= 0 || stageId <= 0 || rewards == null
                || nowUnixSeconds <= 0) return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (!current.autoPushEnabled || layer != current.clearedLayer + 1)
                return FantasyTowerResult.InvalidState;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = Find(next, entryIndex);
            if (entry.backgroundStartLayer <= 0)
            {
                entry.backgroundStartLayer = layer;
                entry.backgroundStartedAtUnixSeconds = nowUnixSeconds;
            }
            foreach (FantasyTowerReward reward in rewards)
            {
                if (reward == null || reward.itemId <= 0 || reward.count < 0)
                    return FantasyTowerResult.InvalidState;
                if (reward.count == 0) continue;
                FantasyTowerReward existing = entry.backgroundRewards.FirstOrDefault(value => value.itemId == reward.itemId);
                if (existing == null)
                    entry.backgroundRewards.Add(new FantasyTowerReward { itemId = reward.itemId, count = reward.count });
                else existing.count = checked(existing.count + reward.count);
            }
            entry.backgroundCheckpoint = string.Empty;
            if (won) entry.clearedLayer = layer;
            bool completed = won && IsEntryCompleted(entryIndex, layer);
            if (!won || completed)
            {
                AddBackgroundResult(entry, entryIndex, won, completed, nowUnixSeconds);
                entry.autoPushEnabled = false;
            }
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult StopBackgroundPush(int entryIndex, long nowUnixSeconds)
        {
            if (!IsTowerEntry(entryIndex) || nowUnixSeconds <= 0) return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (!current.autoPushEnabled) return FantasyTowerResult.Unchanged;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = Find(next, entryIndex);
            FinishBackgroundBatch(entry, entryIndex, nowUnixSeconds);
            entry.autoPushEnabled = false;
            entry.backgroundCheckpoint = string.Empty;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerPushResult EarliestPendingResult()
        {
            return save.entries.SelectMany(value => value.pendingResults)
                .OrderBy(value => value.createdAtUnixSeconds).FirstOrDefault();
        }

        public FantasyTowerResult ClaimResult(string resultId,
            Action<string, FantasyTowerPushResult> grantTransaction)
        {
            if (string.IsNullOrEmpty(resultId) || grantTransaction == null) return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave source = save.entries.FirstOrDefault(entry =>
                entry.pendingResults.Any(result => result.id == resultId));
            if (source == null) return FantasyTowerResult.MissingResult;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = Find(next, source.entryIndex);
            FantasyTowerPushResult result = entry.pendingResults.First(value => value.id == resultId);
            try
            {
                grantTransaction("fantasy-tower:claim:" + resultId, Clone(result));
                entry.pendingResults.Remove(result);
                Persist(next);
                return FantasyTowerResult.Applied;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return FantasyTowerResult.InvalidState;
            }
        }

        public FantasyTowerResult ClaimResult(string resultId, OfflineMainlineProgressSession profile)
        {
            if (profile == null) return FantasyTowerResult.InvalidState;
            return ClaimResult(resultId, (transactionId, result) =>
            {
                ProgressionResult applied = profile.ApplyInventoryTransaction(transactionId, next =>
                {
                    foreach (FantasyTowerReward reward in result.rewards)
                    {
                        if (reward.count == 0) continue;
                        InventoryOperationResult granted = OfflineInventoryOperations.Grant(
                            next.Player, reward.itemId, reward.count, DateTimeOffset.Now.ToUnixTimeSeconds());
                        if (!granted.IsApplied)
                            throw new InvalidOperationException("Fantasy Tower push reward could not be granted: "
                                + reward.itemId + " " + granted.Status);
                    }
                });
                if (applied.Status != ProgressionResultStatus.Applied
                    && applied.Status != ProgressionResultStatus.Unchanged)
                    throw new InvalidOperationException("Fantasy Tower reward transaction failed: " + applied.Message);
            });
        }

        public FantasyTowerResult CompleteForegroundBattle(int entryIndex, int layer, int stageId,
            bool won, IReadOnlyList<FantasyTowerReward> rewards, OfflineMainlineProgressSession profile)
        {
            if (!IsTowerEntry(entryIndex) || layer <= 0 || stageId <= 0 || rewards == null || profile == null)
                return FantasyTowerResult.InvalidState;
            FantasyTowerEntrySave current = GetEntry(entryIndex);
            if (layer != current.clearedLayer + 1 || IsEntryCompleted(entryIndex, current.clearedLayer))
                return FantasyTowerResult.InvalidState;
            if (won)
            {
                string transactionId = "fantasy-tower:stage:" + entryIndex + ":" + stageId;
                ProgressionResult applied = profile.ApplyInventoryTransaction(transactionId, next =>
                {
                    foreach (FantasyTowerReward reward in rewards)
                    {
                        if (reward == null || reward.itemId <= 0 || reward.count < 0)
                            throw new InvalidDataException("Fantasy Tower stage reward is invalid.");
                        if (reward.count == 0) continue;
                        InventoryOperationResult granted = OfflineInventoryOperations.Grant(
                            next.Player, reward.itemId, reward.count, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        if (!granted.IsApplied)
                            throw new InvalidOperationException("Fantasy Tower reward could not be granted: "
                                + reward.itemId + " " + granted.Status);
                    }
                });
                if (applied.Status != ProgressionResultStatus.Applied
                    && applied.Status != ProgressionResultStatus.Unchanged)
                    return FantasyTowerResult.InvalidState;
            }
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = Find(next, entryIndex);
            entry.foregroundCheckpoint = string.Empty;
            if (won) entry.clearedLayer = layer;
            if (!won || IsEntryCompleted(entryIndex, layer)) entry.autoPushEnabled = false;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        public FantasyTowerResult SetClearedLayer(int entryIndex, int layer)
        {
            if (!IsTowerEntry(entryIndex) || layer < 0 || layer > MaxLayer(entryIndex))
                return FantasyTowerResult.InvalidState;
            FantasyTowerSave next = Clone(save);
            FantasyTowerEntrySave entry = FindOrAdd(next, entryIndex);
            if (entry.clearedLayer == layer) return FantasyTowerResult.Unchanged;
            entry.clearedLayer = layer;
            Persist(next);
            return FantasyTowerResult.Applied;
        }

        private void Persist(FantasyTowerSave next)
        {
            Normalize(next);
            if (!IsValid(JsonUtility.ToJson(next)))
                throw new InvalidDataException("Fantasy tower progress is invalid.");
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(next, true), IsValid);
            save = next;
        }

        private static bool IsValid(string json)
        {
            try
            {
                FantasyTowerSave data = JsonUtility.FromJson<FantasyTowerSave>(json);
                if (data == null || data.schema != 1 || data.entries == null) return false;
                var ids = new HashSet<int>();
                foreach (FantasyTowerEntrySave entry in data.entries)
                {
                    if (entry == null || !IsTowerEntry(entry.entryIndex) || entry.clearedLayer < 0
                        || !ids.Add(entry.entryIndex) || entry.pendingResults == null) return false;
                    if (entry.backgroundStartLayer < 0 || entry.backgroundStartedAtUnixSeconds < 0
                        || entry.backgroundRewards == null || entry.backgroundRewards.Any(reward =>
                            reward == null || reward.itemId <= 0 || reward.count < 0)) return false;
                    foreach (FantasyTowerPushResult result in entry.pendingResults)
                        if (!IsValidResult(result) || result.entryIndex != entry.entryIndex) return false;
                    ValidateCheckpoint(entry.foregroundCheckpoint);
                    ValidateCheckpoint(entry.backgroundCheckpoint);
                }
                return true;
            }
            catch { return false; }
        }

        private static void ValidateCheckpoint(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            FightBattleCheckpointSerializer.Load(json);
        }

        private static bool IsValidResult(FantasyTowerPushResult result)
        {
            return result != null && !string.IsNullOrWhiteSpace(result.id) && result.id.Length <= 128
                && IsTowerEntry(result.entryIndex) && result.startLayer > 0
                && result.endLayer >= result.startLayer - 1 && result.createdAtUnixSeconds > 0
                && result.elapsedSeconds >= 0 && result.rewards != null
                && result.rewards.All(reward => reward != null && reward.itemId > 0 && reward.count >= 0);
        }

        private static void Normalize(FantasyTowerSave data)
        {
            if (data.entries == null) data.entries = new List<FantasyTowerEntrySave>();
            foreach (FantasyTowerEntrySave entry in data.entries)
            {
                if (entry.pendingResults == null) entry.pendingResults = new List<FantasyTowerPushResult>();
                if (entry.foregroundCheckpoint == null) entry.foregroundCheckpoint = string.Empty;
                if (entry.backgroundCheckpoint == null) entry.backgroundCheckpoint = string.Empty;
                if (entry.backgroundRewards == null) entry.backgroundRewards = new List<FantasyTowerReward>();
            }
        }

        private static void AddBackgroundResult(FantasyTowerEntrySave entry, int entryIndex,
            bool won, bool completed, long nowUnixSeconds)
        {
            int startLayer = entry.backgroundStartLayer;
            if (startLayer <= 0) return;
            var rewards = entry.backgroundRewards.Select(value => new FantasyTowerReward
                { itemId = value.itemId, count = value.count }).ToList();
            entry.pendingResults.Add(new FantasyTowerPushResult
            {
                id = "fantasy-tower:background:" + entryIndex + ":" + Guid.NewGuid().ToString("N"),
                createdAtUnixSeconds = nowUnixSeconds,
                entryIndex = entryIndex,
                startLayer = startLayer,
                endLayer = Math.Max(startLayer - 1, entry.clearedLayer),
                elapsedSeconds = Math.Max(0, nowUnixSeconds - entry.backgroundStartedAtUnixSeconds),
                won = won,
                towerCompleted = completed,
                rewards = rewards
            });
            entry.backgroundStartLayer = 0;
            entry.backgroundStartedAtUnixSeconds = 0;
            entry.backgroundRewards.Clear();
        }

        private static void FinishBackgroundBatch(FantasyTowerEntrySave entry, int entryIndex,
            long nowUnixSeconds)
        {
            if (entry.backgroundStartLayer <= 0) return;
            if (entry.clearedLayer >= entry.backgroundStartLayer)
                AddBackgroundResult(entry, entryIndex, true,
                    IsEntryCompleted(entryIndex, entry.clearedLayer), nowUnixSeconds);
            else
            {
                entry.backgroundStartLayer = 0;
                entry.backgroundStartedAtUnixSeconds = 0;
                entry.backgroundRewards.Clear();
            }
        }

        private static FantasyTowerEntrySave Find(FantasyTowerSave data, int entryIndex)
        {
            return data.entries.FirstOrDefault(value => value.entryIndex == entryIndex)
                ?? throw new InvalidDataException("Fantasy tower entry is missing.");
        }

        private static FantasyTowerEntrySave FindOrAdd(FantasyTowerSave data, int entryIndex)
        {
            if (!IsTowerEntry(entryIndex)) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            FantasyTowerEntrySave entry = data.entries.FirstOrDefault(value => value.entryIndex == entryIndex);
            if (entry != null) return entry;
            entry = new FantasyTowerEntrySave { entryIndex = entryIndex };
            data.entries.Add(entry);
            return entry;
        }

        private static bool IsTowerEntry(int entryIndex) => entryIndex >= 1 && entryIndex <= 4;

        private static bool IsEntryCompleted(int entryIndex, int layer)
        {
            int maxLayer = MaxLayer(entryIndex);
            return maxLayer > 0 && layer >= maxLayer;
        }

        private static int MaxLayer(int entryIndex) => entryIndex == 1 ? 110 : entryIndex == 2 ? 115
            : entryIndex == 3 ? 160 : entryIndex == 4 ? 200 : 0;

        private static FantasyTowerSave Clone(FantasyTowerSave value)
        {
            return JsonUtility.FromJson<FantasyTowerSave>(JsonUtility.ToJson(value));
        }

        private static FantasyTowerPushResult Clone(FantasyTowerPushResult value)
        {
            return JsonUtility.FromJson<FantasyTowerPushResult>(JsonUtility.ToJson(value));
        }
    }
}
