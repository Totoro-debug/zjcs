using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Offline.State;
using UnityEngine;

namespace Zjcs.Offline.World
{
    public enum NpcAssistRefreshCycle
    {
        Daily = 0,
        Weekly = 1,
        None = 10
    }

    [Serializable]
    public sealed class OfflineNpcAssistanceSave
    {
        public int schema = 1;
        public List<OfflineNpcAssistSelection> selections = new List<OfflineNpcAssistSelection>();
        public List<OfflineNpcAssistCount> counts = new List<OfflineNpcAssistCount>();
        public OfflineNpcAssistBattle activeBattle;
    }

    [Serializable]
    public sealed class OfflineNpcAssistSelection
    {
        public int playType;
        public string entryId = string.Empty;
        public List<int> npcClassIds = new List<int>();
    }

    [Serializable]
    public sealed class OfflineNpcAssistCount
    {
        public int playType;
        public int npcClassId;
        public int count;
        public long refreshTimeUnixSeconds;
    }

    [Serializable]
    public sealed class OfflineNpcAssistBattle
    {
        public int playType;
        public string entryId = string.Empty;
        public int stageId;
        public bool committed;
        public long committedAtUnixSeconds;
        public List<int> npcClassIds = new List<int>();
    }

    public sealed class OfflineNpcAssistanceProgress
    {
        private readonly string path;
        private OfflineNpcAssistanceSave save;

        private OfflineNpcAssistanceProgress(string path, OfflineNpcAssistanceSave save)
        {
            this.path = path;
            this.save = save;
        }

        public OfflineNpcAssistBattle ActiveBattle => Clone(save.activeBattle);

        public static OfflineNpcAssistanceProgress Open(ProfileSlotCatalog slots, string slotId)
        {
            if (slots == null || !slots.Contains(slotId))
                throw new ArgumentException("A valid profile slot is required.", nameof(slotId));
            string path = Path.Combine(slots.GetSlotDirectory(slotId), "npc-assistance-save.json");
            SaveReadResult read = LocalJsonSaveFile.Read(path, IsValid);
            if (read.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("NPC assistance progress and its backup are invalid.");
            OfflineNpcAssistanceSave data = read.Json == null
                ? new OfflineNpcAssistanceSave()
                : JsonUtility.FromJson<OfflineNpcAssistanceSave>(read.Json);
            Normalize(data);
            var progress = new OfflineNpcAssistanceProgress(path, data);
            if (read.Json == null) progress.Persist(data);
            return progress;
        }

        public IReadOnlyList<int> Selected(int playType, string entryId)
        {
            ValidateKey(playType, entryId);
            OfflineNpcAssistSelection selection = FindSelection(save, playType, entryId);
            return selection == null ? Array.Empty<int>() : selection.npcClassIds.ToArray();
        }

        public bool SetSelection(int playType, string entryId, IReadOnlyList<int> npcClassIds, int maxPlayer)
        {
            ValidateKey(playType, entryId);
            if (npcClassIds == null || maxPlayer < 0 || npcClassIds.Count > maxPlayer
                || npcClassIds.Any(value => value <= 0) || npcClassIds.Distinct().Count() != npcClassIds.Count)
                return false;
            if (save.activeBattle != null && save.activeBattle.committed
                && save.activeBattle.playType == playType && save.activeBattle.entryId == entryId)
                return false;

            OfflineNpcAssistanceSave next = Clone(save);
            OfflineNpcAssistSelection selection = FindSelection(next, playType, entryId);
            if (selection == null)
            {
                selection = new OfflineNpcAssistSelection { playType = playType, entryId = entryId };
                next.selections.Add(selection);
            }
            selection.npcClassIds = npcClassIds.ToList();
            Persist(next);
            return true;
        }

        public OfflineNpcAssistBattle CommitBattle(int playType, string entryId, int stageId,
            int maxPlayer, int oneNpcMaxInviteCount, int maxInviteCount,
            NpcAssistRefreshCycle refreshCycle, long nowUnixSeconds)
        {
            ValidateKey(playType, entryId);
            if (stageId <= 0 || maxPlayer < 0 || oneNpcMaxInviteCount < -1 || maxInviteCount < -1
                || nowUnixSeconds <= 0 || !Enum.IsDefined(typeof(NpcAssistRefreshCycle), refreshCycle))
                throw new ArgumentOutOfRangeException(nameof(stageId), "NPC assistance rule values are invalid.");

            if (save.activeBattle != null && save.activeBattle.committed)
            {
                if (save.activeBattle.playType == playType && save.activeBattle.entryId == entryId
                    && save.activeBattle.stageId == stageId)
                    return Clone(save.activeBattle);
                throw new InvalidOperationException("Another NPC assisted battle is still active.");
            }

            OfflineNpcAssistSelection selection = FindSelection(save, playType, entryId);
            int[] npcIds = selection == null ? Array.Empty<int>() : selection.npcClassIds.ToArray();
            if (npcIds.Length > maxPlayer || (maxInviteCount > 0 && npcIds.Length > 0
                && GetTotalCount(save, playType, refreshCycle, nowUnixSeconds) + npcIds.Length > maxInviteCount))
                throw new InvalidOperationException("Selected NPCs exceed the official assistance limit.");
            foreach (int npcId in npcIds)
            {
                OfflineNpcAssistCount count = GetOrCreateCount(save, playType, npcId, refreshCycle, nowUnixSeconds);
                if (oneNpcMaxInviteCount > 0 && count.count >= oneNpcMaxInviteCount)
                    throw new InvalidOperationException("An NPC has reached its official assistance limit: " + npcId);
            }

            OfflineNpcAssistanceSave next = Clone(save);
            foreach (int npcId in npcIds)
            {
                OfflineNpcAssistCount count = GetOrCreateCount(next, playType, npcId, refreshCycle, nowUnixSeconds);
                count.count = checked(count.count + 1);
                count.refreshTimeUnixSeconds = RefreshTime(nowUnixSeconds);
            }
            next.activeBattle = new OfflineNpcAssistBattle
            {
                playType = playType,
                entryId = entryId,
                stageId = stageId,
                committed = true,
                committedAtUnixSeconds = nowUnixSeconds,
                npcClassIds = npcIds.ToList()
            };
            Persist(next);
            return Clone(save.activeBattle);
        }

        public void FinishBattle(int playType, string entryId, int stageId)
        {
            if (save.activeBattle == null) return;
            if (save.activeBattle.playType != playType || save.activeBattle.entryId != entryId
                || save.activeBattle.stageId != stageId)
                throw new InvalidOperationException("NPC assistance battle identity does not match the active snapshot.");
            OfflineNpcAssistanceSave next = Clone(save);
            next.activeBattle = null;
            Persist(next);
        }

        public void CancelBattle(int playType, string entryId, int stageId)
        {
            if (save.activeBattle == null) return;
            if (save.activeBattle.playType != playType || save.activeBattle.entryId != entryId
                || save.activeBattle.stageId != stageId)
                throw new InvalidOperationException("NPC assistance battle identity does not match the active snapshot.");
            OfflineNpcAssistanceSave next = Clone(save);
            foreach (int npcId in next.activeBattle.npcClassIds ?? new List<int>())
            {
                OfflineNpcAssistCount count = next.counts.FirstOrDefault(value => value.playType == playType
                    && value.npcClassId == npcId);
                if (count != null && count.count > 0) count.count--;
            }
            next.activeBattle = null;
            Persist(next);
        }

        public int AssistCount(int playType, int npcClassId, NpcAssistRefreshCycle refreshCycle,
            long nowUnixSeconds)
        {
            OfflineNpcAssistCount count = save.counts.FirstOrDefault(value => value.playType == playType
                && value.npcClassId == npcClassId);
            if (count == null || IsExpired(count.refreshTimeUnixSeconds, nowUnixSeconds, refreshCycle)) return 0;
            return count.count;
        }

        private void Persist(OfflineNpcAssistanceSave next)
        {
            Normalize(next);
            if (!IsValid(JsonUtility.ToJson(next)))
                throw new InvalidDataException("NPC assistance progress is invalid.");
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(next, true), IsValid);
            save = next;
        }

        private static int GetTotalCount(OfflineNpcAssistanceSave data, int playType,
            NpcAssistRefreshCycle refreshCycle, long nowUnixSeconds)
        {
            int total = 0;
            foreach (OfflineNpcAssistCount count in data.counts)
                if (count.playType == playType && !IsExpired(count.refreshTimeUnixSeconds,
                    nowUnixSeconds, refreshCycle))
                    total = checked(total + count.count);
            return total;
        }

        private static OfflineNpcAssistCount GetOrCreateCount(OfflineNpcAssistanceSave data,
            int playType, int npcClassId, NpcAssistRefreshCycle refreshCycle, long nowUnixSeconds)
        {
            OfflineNpcAssistCount count = data.counts.FirstOrDefault(value => value.playType == playType
                && value.npcClassId == npcClassId);
            if (count == null)
            {
                count = new OfflineNpcAssistCount { playType = playType, npcClassId = npcClassId };
                data.counts.Add(count);
            }
            else if (IsExpired(count.refreshTimeUnixSeconds, nowUnixSeconds, refreshCycle))
            {
                count.count = 0;
            }
            return count;
        }

        private static bool IsExpired(long refreshTimeUnixSeconds, long nowUnixSeconds,
            NpcAssistRefreshCycle refreshCycle)
        {
            if (refreshTimeUnixSeconds <= 0 || refreshCycle == NpcAssistRefreshCycle.None) return false;
            DateTime previous = DateTimeOffset.FromUnixTimeSeconds(refreshTimeUnixSeconds).LocalDateTime;
            DateTime current = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime;
            if (refreshCycle == NpcAssistRefreshCycle.Daily) return previous.Date != current.Date;
            System.Globalization.Calendar calendar = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            return calendar.GetWeekOfYear(previous, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday)
                    != calendar.GetWeekOfYear(current, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday)
                || previous.Year != current.Year;
        }

        private static long RefreshTime(long nowUnixSeconds)
        {
            return new DateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).LocalDateTime.Date)
                .ToUnixTimeSeconds();
        }

        private static OfflineNpcAssistSelection FindSelection(OfflineNpcAssistanceSave data,
            int playType, string entryId) => data.selections.FirstOrDefault(value =>
                value.playType == playType && value.entryId == entryId);

        private static void ValidateKey(int playType, string entryId)
        {
            if (playType <= 0 || string.IsNullOrWhiteSpace(entryId) || entryId.Length > 256)
                throw new ArgumentException("NPC assistance requires a play type and entry identity.");
        }

        private static bool IsValid(string json)
        {
            try
            {
                OfflineNpcAssistanceSave data = JsonUtility.FromJson<OfflineNpcAssistanceSave>(json);
                if (data == null || data.schema != 1 || data.selections == null || data.counts == null)
                    return false;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (OfflineNpcAssistSelection selection in data.selections)
                    if (selection == null || selection.playType <= 0 || string.IsNullOrWhiteSpace(selection.entryId)
                        || selection.entryId.Length > 256 || selection.npcClassIds == null
                        || selection.npcClassIds.Any(value => value <= 0)
                        || selection.npcClassIds.Distinct().Count() != selection.npcClassIds.Count
                        || !keys.Add(selection.playType + ":" + selection.entryId)) return false;
                keys.Clear();
                foreach (OfflineNpcAssistCount count in data.counts)
                    if (count == null || count.playType <= 0 || count.npcClassId <= 0 || count.count < 0
                        || count.refreshTimeUnixSeconds < 0
                        || !keys.Add(count.playType + ":" + count.npcClassId)) return false;
                if (data.activeBattle != null && !IsEmptyBattle(data.activeBattle)
                    && (data.activeBattle.playType <= 0
                    || string.IsNullOrWhiteSpace(data.activeBattle.entryId) || data.activeBattle.entryId.Length > 256
                    || data.activeBattle.stageId <= 0 || data.activeBattle.committedAtUnixSeconds <= 0
                    || !data.activeBattle.committed || data.activeBattle.npcClassIds == null
                    || data.activeBattle.npcClassIds.Any(value => value <= 0)
                    || data.activeBattle.npcClassIds.Distinct().Count() != data.activeBattle.npcClassIds.Count))
                    return false;
                return true;
            }
            catch { return false; }
        }

        private static void Normalize(OfflineNpcAssistanceSave data)
        {
            if (data.selections == null) data.selections = new List<OfflineNpcAssistSelection>();
            if (data.counts == null) data.counts = new List<OfflineNpcAssistCount>();
            foreach (OfflineNpcAssistSelection selection in data.selections)
            {
                if (selection.npcClassIds == null) selection.npcClassIds = new List<int>();
                if (selection.entryId == null) selection.entryId = string.Empty;
            }
            foreach (OfflineNpcAssistCount count in data.counts)
            {
                if (count.refreshTimeUnixSeconds < 0) count.refreshTimeUnixSeconds = 0;
            }
            if (data.activeBattle != null && IsEmptyBattle(data.activeBattle)) data.activeBattle = null;
            if (data.activeBattle != null)
            {
                if (data.activeBattle.entryId == null) data.activeBattle.entryId = string.Empty;
                if (data.activeBattle.npcClassIds == null) data.activeBattle.npcClassIds = new List<int>();
            }
        }

        private static bool IsEmptyBattle(OfflineNpcAssistBattle battle)
        {
            return battle != null && battle.playType == 0 && string.IsNullOrEmpty(battle.entryId)
                && battle.stageId == 0 && !battle.committed && battle.committedAtUnixSeconds == 0
                && (battle.npcClassIds == null || battle.npcClassIds.Count == 0);
        }

        private static OfflineNpcAssistanceSave Clone(OfflineNpcAssistanceSave value) =>
            JsonUtility.FromJson<OfflineNpcAssistanceSave>(JsonUtility.ToJson(value));

        private static OfflineNpcAssistBattle Clone(OfflineNpcAssistBattle value) => value == null ? null :
            JsonUtility.FromJson<OfflineNpcAssistBattle>(JsonUtility.ToJson(value));
    }
}
