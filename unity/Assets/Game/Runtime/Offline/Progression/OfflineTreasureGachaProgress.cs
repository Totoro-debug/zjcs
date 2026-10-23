using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineTreasureGachaProgress
    {
        public int Version = 1;
        public long RandomState;
        public long DrawSequence;
        public int LastObservedLocalDayKey;
        public int DailyDrawCount;
        public int SelectedSeriesId;
        public int SelectedPreciousGachaType = 4;
        public int SelectedElementGachaType = 8;
        public long PreciousScore;
        public long PreciousDrawCount;
        public long ElementDrawCount;
        public List<OfflineTreasurePoolCount> PoolCounts = new List<OfflineTreasurePoolCount>();
        public List<OfflineTreasurePityCounter> PityCounters = new List<OfflineTreasurePityCounter>();
        public List<OfflineTreasureScheduledGuarantee> ScheduledGuarantees = new List<OfflineTreasureScheduledGuarantee>();
        public List<int> ConfirmedSeriesIds = new List<int>();
        public List<OfflineTreasureHundredReminder> HundredReminders = new List<OfflineTreasureHundredReminder>();
        public List<OfflineTreasureAchievementProgress> AchievementProgress = new List<OfflineTreasureAchievementProgress>();
        public List<OfflineTreasureDrawRecord> History = new List<OfflineTreasureDrawRecord>();

        public static void Normalize(OfflineTreasureGachaProgress progress)
        {
            if (progress == null) return;
            if (progress.PoolCounts == null) progress.PoolCounts = new List<OfflineTreasurePoolCount>();
            if (progress.PityCounters == null) progress.PityCounters = new List<OfflineTreasurePityCounter>();
            if (progress.ScheduledGuarantees == null) progress.ScheduledGuarantees = new List<OfflineTreasureScheduledGuarantee>();
            if (progress.ConfirmedSeriesIds == null) progress.ConfirmedSeriesIds = new List<int>();
            if (progress.HundredReminders == null) progress.HundredReminders = new List<OfflineTreasureHundredReminder>();
            if (progress.AchievementProgress == null) progress.AchievementProgress = new List<OfflineTreasureAchievementProgress>();
            if (progress.History == null) progress.History = new List<OfflineTreasureDrawRecord>();
            foreach (OfflineTreasureDrawRecord record in progress.History)
                if (record != null && record.Awards == null) record.Awards = new List<OfflineTreasureAward>();
        }

        public static bool IsValid(OfflineTreasureGachaProgress progress)
        {
            if (progress == null || progress.Version != 1 || progress.DrawSequence < 0
                || progress.LastObservedLocalDayKey < 0 || progress.DailyDrawCount < 0
                || progress.SelectedSeriesId < 0 || progress.SelectedPreciousGachaType < 4
                || progress.SelectedPreciousGachaType > 7 || progress.SelectedElementGachaType < 8
                || progress.SelectedElementGachaType > 12 || progress.PreciousScore < 0
                || progress.PreciousDrawCount < 0 || progress.ElementDrawCount < 0
                || progress.PoolCounts == null || progress.PoolCounts.Count > 9
                || progress.PityCounters == null || progress.PityCounters.Count > 64
                || progress.ScheduledGuarantees == null || progress.ScheduledGuarantees.Count > 128
                || progress.ConfirmedSeriesIds == null || progress.ConfirmedSeriesIds.Count > 32
                || progress.HundredReminders == null || progress.HundredReminders.Count > 9
                || progress.AchievementProgress == null || progress.AchievementProgress.Count > 1000
                || progress.History == null || progress.History.Count > 5000) return false;

            var poolKeys = new HashSet<int>();
            foreach (OfflineTreasurePoolCount value in progress.PoolCounts)
                if (value == null || value.GachaType < 4 || value.GachaType > 12 || value.Count < 0
                    || !poolKeys.Add(value.GachaType)) return false;

            var pityKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineTreasurePityCounter value in progress.PityCounters)
                if (value == null || value.RecordId <= 0 || value.Quality <= 0 || value.DrawCount < 0
                    || value.MissCount < 0 || value.HitCount < 0
                    || !pityKeys.Add(value.RecordId + ":" + value.Quality)) return false;

            foreach (OfflineTreasureScheduledGuarantee value in progress.ScheduledGuarantees)
                if (value == null || value.RecordId <= 0 || value.TargetPull <= 0 || value.Quality <= 0) return false;

            var confirmed = new HashSet<int>();
            foreach (int id in progress.ConfirmedSeriesIds)
                if (id <= 0 || !confirmed.Add(id)) return false;

            var reminderKeys = new HashSet<int>();
            foreach (OfflineTreasureHundredReminder value in progress.HundredReminders)
                if (value == null || value.GachaType < 4 || value.GachaType > 12 || value.DayKey < 0
                    || !reminderKeys.Add(value.GachaType)) return false;

            var achievementKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineTreasureAchievementProgress value in progress.AchievementProgress)
                if (value == null || value.Group < 11 || value.Group > 13 || value.TemplateId <= 0
                    || value.CycleIndex < 0 || value.ClaimedCount < 0
                    || !achievementKeys.Add(value.Group + ":" + value.TemplateId + ":" + value.CycleIndex)) return false;

            var historyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineTreasureDrawRecord value in progress.History)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.ReceiptId) || value.DrawIndex < 0
                    || value.GachaType < 4 || value.GachaType > 12 || value.SeriesId < 0
                    || value.UnixSeconds < 0 || value.Awards == null || value.Awards.Count != 1
                    || !historyKeys.Add(value.ReceiptId + ":" + value.DrawIndex)) return false;
                foreach (OfflineTreasureAward award in value.Awards)
                    if (award == null || award.ItemId <= 0 || award.Count <= 0 || award.Quality < 0
                        || award.ConvertedItemId < 0 || award.ConvertedCount < 0
                        || award.RecycledItemId < 0 || award.RecycledCount < 0) return false;
            }
            return true;
        }
    }

    [Serializable] public sealed class OfflineTreasurePoolCount { public int GachaType; public long Count; }
    [Serializable] public sealed class OfflineTreasurePityCounter
    {
        public int RecordId;
        public int Quality;
        public long DrawCount;
        public int MissCount;
        public int HitCount;
    }
    [Serializable] public sealed class OfflineTreasureScheduledGuarantee
    {
        public int RecordId;
        public long TargetPull;
        public int Quality;
    }
    [Serializable] public sealed class OfflineTreasureHundredReminder { public int GachaType; public int DayKey; }
    [Serializable] public sealed class OfflineTreasureAchievementProgress
    {
        public int Group;
        public int TemplateId;
        public int CycleIndex;
        public int ClaimedCount;
    }
    [Serializable] public sealed class OfflineTreasureDrawRecord
    {
        public string ReceiptId;
        public int DrawIndex;
        public int GachaType;
        public int SeriesId;
        public long UnixSeconds;
        public List<OfflineTreasureAward> Awards = new List<OfflineTreasureAward>();
    }
    [Serializable] public sealed class OfflineTreasureAward
    {
        public int ItemId;
        public long Count;
        public int Quality;
        public bool IsPiece;
        public int ConvertedItemId;
        public long ConvertedCount;
        public int RecycledItemId;
        public long RecycledCount;
    }

    [Serializable] public sealed class OfflineRankUnlockTime
    {
        public int Rank;
        public long UnixSeconds;
    }
}
