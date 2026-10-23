using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineWishProgress
    {
        public int Version = 1;
        public long RandomState;
        public long DrawSequence;
        public int GuaranteeSequenceVersion;
        public int NewbieDrawCount;
        public int TutorialState;
        public int SelectedRegularProfession;
        public int SelectedSeniorProfession;
        public List<OfflineWishDailyCounter> DailyCounts = new List<OfflineWishDailyCounter>();
        public List<OfflineWishPoolCounter> PoolCounts = new List<OfflineWishPoolCounter>();
        public List<OfflineWishScoreCounter> ScoreCounts = new List<OfflineWishScoreCounter>();
        public List<OfflineWishPityCounter> PityCounters = new List<OfflineWishPityCounter>();
        public List<OfflineWishProfessionPityCounter> ProfessionPityCounters = new List<OfflineWishProfessionPityCounter>();
        public List<OfflineWishScheduledGuarantee> ScheduledGuarantees = new List<OfflineWishScheduledGuarantee>();
        public List<OfflineWishProfessionSelection> ProfessionSelections = new List<OfflineWishProfessionSelection>();
        public List<OfflineWishAchievementProgress> AchievementProgress = new List<OfflineWishAchievementProgress>();
        public List<OfflineWishReminderState> HundredReminders = new List<OfflineWishReminderState>();
        public List<OfflineWishShopState> Shops = new List<OfflineWishShopState>();
        public List<OfflineWishShopStock> ShopStocks = new List<OfflineWishShopStock>();
        public List<OfflineWishDrawRecord> History = new List<OfflineWishDrawRecord>();

        public static void Normalize(OfflineWishProgress progress)
        {
            if (progress == null) return;
            if (progress.DailyCounts == null) progress.DailyCounts = new List<OfflineWishDailyCounter>();
            if (progress.PoolCounts == null) progress.PoolCounts = new List<OfflineWishPoolCounter>();
            if (progress.ScoreCounts == null) progress.ScoreCounts = new List<OfflineWishScoreCounter>();
            if (progress.PityCounters == null) progress.PityCounters = new List<OfflineWishPityCounter>();
            if (progress.ProfessionPityCounters == null) progress.ProfessionPityCounters = new List<OfflineWishProfessionPityCounter>();
            if (progress.ScheduledGuarantees == null) progress.ScheduledGuarantees = new List<OfflineWishScheduledGuarantee>();
            if (progress.ProfessionSelections == null) progress.ProfessionSelections = new List<OfflineWishProfessionSelection>();
            if (progress.AchievementProgress == null) progress.AchievementProgress = new List<OfflineWishAchievementProgress>();
            if (progress.HundredReminders == null) progress.HundredReminders = new List<OfflineWishReminderState>();
            if (progress.Shops == null) progress.Shops = new List<OfflineWishShopState>();
            if (progress.ShopStocks == null) progress.ShopStocks = new List<OfflineWishShopStock>();
            if (progress.History == null) progress.History = new List<OfflineWishDrawRecord>();
            foreach (OfflineWishProfessionSelection selection in progress.ProfessionSelections)
                if (selection != null && selection.SelectedProfessions == null)
                    selection.SelectedProfessions = new List<int>();
            foreach (OfflineWishDrawRecord record in progress.History)
                if (record != null && record.Rewards == null) record.Rewards = new List<OfflineWishAward>();
            foreach (OfflineWishScheduledGuarantee guarantee in progress.ScheduledGuarantees)
                if (guarantee != null && guarantee.DeadlineSequence <= 0)
                    guarantee.DeadlineSequence = guarantee.TargetSequence;
            MigrateGuaranteeSequences(progress);
        }

        private static void MigrateGuaranteeSequences(OfflineWishProgress progress)
        {
            if (progress.GuaranteeSequenceVersion >= 1) return;
            progress.ScheduledGuarantees.RemoveAll(value => value == null);
            foreach (OfflineWishScheduledGuarantee guarantee in progress.ScheduledGuarantees.ToArray())
            {
                long currentPoolSequence = progress.PoolCounts.FirstOrDefault(value =>
                    value.GachaType == guarantee.GachaType)?.Count ?? 0;
                long targetRemaining = guarantee.TargetSequence - progress.DrawSequence;
                if (targetRemaining <= 0)
                {
                    progress.ScheduledGuarantees.Remove(guarantee);
                    continue;
                }

                long deadlineRemaining = Math.Max(targetRemaining,
                    guarantee.DeadlineSequence - progress.DrawSequence);
                guarantee.TargetSequence = checked(currentPoolSequence + targetRemaining);
                guarantee.DeadlineSequence = checked(currentPoolSequence + deadlineRemaining);
            }
            progress.GuaranteeSequenceVersion = 1;
        }

        public static bool IsValid(OfflineWishProgress progress)
        {
            if (progress == null || progress.Version != 1 || progress.DrawSequence < 0
                || progress.GuaranteeSequenceVersion < 0 || progress.GuaranteeSequenceVersion > 1
                || progress.NewbieDrawCount < 0
                || progress.SelectedRegularProfession < 0 || progress.SelectedSeniorProfession < 0
                || progress.TutorialState < 0 || progress.TutorialState > 3
                || progress.DailyCounts == null || progress.DailyCounts.Count > 100
                || progress.PoolCounts == null || progress.PoolCounts.Count > 8
                || progress.ScoreCounts == null || progress.ScoreCounts.Count > 8
                || progress.PityCounters == null || progress.PityCounters.Count > 100
                || progress.ProfessionPityCounters == null || progress.ProfessionPityCounters.Count > 100
                || progress.ScheduledGuarantees == null || progress.ScheduledGuarantees.Count > 64
                || progress.ProfessionSelections == null || progress.ProfessionSelections.Count > 64
                || progress.AchievementProgress == null || progress.AchievementProgress.Count > 1000
                || progress.HundredReminders == null || progress.HundredReminders.Count > 8
                || progress.Shops == null || progress.Shops.Count > 16
                || progress.ShopStocks == null || progress.ShopStocks.Count > 512
                || progress.History == null || progress.History.Count > 3000) return false;

            var dailyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineWishDailyCounter value in progress.DailyCounts)
                if (value == null || value.DayKey < 0 || value.ClassType < 0 || value.Count < 0
                    || !dailyKeys.Add(value.DayKey + ":" + value.ClassType)) return false;

            var poolKeys = new HashSet<int>();
            foreach (OfflineWishPoolCounter value in progress.PoolCounts)
                if (value == null || value.GachaType < 0 || value.GachaType > 2 || value.Count < 0
                    || !poolKeys.Add(value.GachaType)) return false;

            var scoreKeys = new HashSet<int>();
            foreach (OfflineWishScoreCounter value in progress.ScoreCounts)
                if (value == null || value.GachaType < 0 || value.GachaType > 2 || value.Count < 0
                    || !scoreKeys.Add(value.GachaType)) return false;

            var pityKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineWishPityCounter value in progress.PityCounters)
                if (value == null || value.RuleId <= 0 || value.Quality <= 0 || value.MissCount < 0 || value.HitCount < 0
                    || !pityKeys.Add(value.RuleId + ":" + value.Quality)) return false;

            var professionPityKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineWishProfessionPityCounter value in progress.ProfessionPityCounters)
                if (value == null || value.Profession <= 0 || value.GachaType != 0 && value.GachaType != 2
                    || value.SkillMissCount < 0 || value.PieceMissCount < 0 || value.QualityMissCounts == null
                    || value.QualityMissCounts.Length != 5 || value.QualityMissCounts.Any(count => count < 0)
                    || !professionPityKeys.Add(value.Profession + ":" + value.GachaType)) return false;

            foreach (OfflineWishScheduledGuarantee value in progress.ScheduledGuarantees)
                if (value == null || value.TargetSequence <= 0 || value.DeadlineSequence <= 0
                    || value.GachaType < 0 || value.GachaType > 2
                    || value.Kind < 0 || value.Kind > 2 || value.GuaranteeId <= 0 || value.Profession < 0)
                    return false;

            var selectionKeys = new HashSet<int>();
            foreach (OfflineWishProfessionSelection value in progress.ProfessionSelections)
            {
                if (value == null || value.Profession <= 0 || value.SelectedProfessions == null
                    || value.SelectedProfessions.Count > 4 || !selectionKeys.Add(value.Profession)) return false;
                if (value.Completed != (value.SelectedProfessions.Count == 4)) return false;
                var selected = new HashSet<int>();
                foreach (int profession in value.SelectedProfessions)
                    if (profession <= 0 || !selected.Add(profession)) return false;
            }

            var achievementKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineWishAchievementProgress value in progress.AchievementProgress)
                if (value == null || value.Group <= 0 || value.TemplateId <= 0 || value.CycleIndex < 0
                    || value.Progress < 0 || value.ClaimedCount < 0
                    || !achievementKeys.Add(value.Group + ":" + value.TemplateId + ":" + value.CycleIndex)) return false;

            var reminderKeys = new HashSet<int>();
            foreach (OfflineWishReminderState value in progress.HundredReminders)
                if (value == null || value.GachaType < 0 || value.GachaType > 2 || value.DayKey < 0
                    || !reminderKeys.Add(value.GachaType)) return false;

            var shopKeys = new HashSet<int>();
            foreach (OfflineWishShopState value in progress.Shops)
                if (value == null || value.ShopId <= 0 || value.DayKey < 0 || value.ManualRefreshCount < 0
                    || !shopKeys.Add(value.ShopId)) return false;

            var stockKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineWishShopStock value in progress.ShopStocks)
                if (value == null || value.ShopId <= 0 || value.Slot < 0 || value.GoodsId <= 0
                    || value.RemainingInventory < -1 || value.BoughtCount < 0
                    || !stockKeys.Add(value.ShopId + ":" + value.Slot)) return false;

            var historyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineWishDrawRecord value in progress.History)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.ReceiptId) || value.DrawIndex < 0
                    || value.GachaType < 0 || value.GachaType > 2 || value.Profession < 0
                    || value.UnixSeconds < 0 || value.Rewards == null || value.Rewards.Count == 0
                    || value.Rewards.Count > 64 || !historyKeys.Add(value.ReceiptId + ":" + value.DrawIndex)) return false;
                foreach (OfflineWishAward reward in value.Rewards)
                    if (reward == null || reward.ItemId <= 0 || reward.Count <= 0 || reward.Quality < 0
                        || reward.OriginItemId < 0 || reward.ConvertedItemId < 0 || reward.ConvertedCount < 0
                        || reward.RecycledItemId < 0 || reward.RecycledCount < 0) return false;
            }
            return true;
        }
    }

    [Serializable] public sealed class OfflineWishDailyCounter { public int DayKey; public int ClassType; public int Count; }
    [Serializable] public sealed class OfflineWishPoolCounter { public int GachaType; public long Count; }
    [Serializable] public sealed class OfflineWishScoreCounter { public int GachaType; public long Count; }
    [Serializable] public sealed class OfflineWishPityCounter { public int RuleId; public int Quality; public int MissCount; public int HitCount; }
    [Serializable] public sealed class OfflineWishProfessionPityCounter
    {
        public int Profession;
        public int GachaType;
        public int SkillMissCount;
        public int PieceMissCount;
        public int[] QualityMissCounts = new int[5];
    }
    [Serializable] public sealed class OfflineWishScheduledGuarantee
    {
        public long TargetSequence;
        public long DeadlineSequence;
        public int GachaType;
        public int Kind;
        public int GuaranteeId;
        public int Profession;
    }
    [Serializable] public sealed class OfflineWishProfessionSelection
    {
        public int Profession;
        public bool Completed;
        public List<int> SelectedProfessions = new List<int>();
    }
    [Serializable] public sealed class OfflineWishAchievementProgress
    {
        public int Group;
        public int TemplateId;
        public int CycleIndex;
        public long Progress;
        public int ClaimedCount;
    }
    [Serializable] public sealed class OfflineWishReminderState { public int GachaType; public int DayKey; }
    [Serializable] public sealed class OfflineWishShopState
    {
        public int ShopId;
        public int DayKey;
        public int ManualRefreshCount;
        public long RandomState;
    }
    [Serializable] public sealed class OfflineWishShopStock
    {
        public int ShopId;
        public int Slot;
        public int GoodsId;
        public int RemainingInventory = -1;
        public int BoughtCount;
    }
    [Serializable] public sealed class OfflineWishDrawRecord
    {
        public string ReceiptId;
        public int DrawIndex;
        public int GachaType;
        public int Profession;
        public long UnixSeconds;
        public List<OfflineWishAward> Rewards = new List<OfflineWishAward>();
    }
    [Serializable] public sealed class OfflineWishAward
    {
        public int ItemId;
        public long Count;
        public int Quality;
        public int OriginItemId;
        public int ConvertedItemId;
        public long ConvertedCount;
        public int RecycledItemId;
        public long RecycledCount;
    }
}
