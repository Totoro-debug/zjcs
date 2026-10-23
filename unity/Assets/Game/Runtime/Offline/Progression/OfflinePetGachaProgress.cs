using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflinePetGachaProgress
    {
        public int Version = 1;
        public long RandomState;
        public long DrawSequence;
        public int LastObservedLocalDayKey;
        public int DailyDrawCount;
        public int HundredReminderSuppressedDayKey;
        public long Score;
        public long TotalDrawCount;
        public int ShopWeekKey;
        public int ShopProfession;
        public int ShopRefreshSequence;
        public List<int> OwnedPetIds = new List<int>();
        public List<OfflinePetGachaPityCounter> PityCounters = new List<OfflinePetGachaPityCounter>();
        public List<OfflinePetGachaAchievementClaim> AchievementClaims = new List<OfflinePetGachaAchievementClaim>();
        public List<OfflinePetGachaGoodsStock> ShopGoods = new List<OfflinePetGachaGoodsStock>();
        public List<OfflinePetGachaDrawRecord> History = new List<OfflinePetGachaDrawRecord>();

        public static void Normalize(OfflinePetGachaProgress progress)
        {
            if (progress == null) return;
            if (progress.OwnedPetIds == null) progress.OwnedPetIds = new List<int>();
            if (progress.PityCounters == null) progress.PityCounters = new List<OfflinePetGachaPityCounter>();
            if (progress.AchievementClaims == null) progress.AchievementClaims = new List<OfflinePetGachaAchievementClaim>();
            if (progress.ShopGoods == null) progress.ShopGoods = new List<OfflinePetGachaGoodsStock>();
            if (progress.History == null) progress.History = new List<OfflinePetGachaDrawRecord>();
            foreach (OfflinePetGachaDrawRecord record in progress.History)
                if (record != null && record.Awards == null) record.Awards = new List<OfflinePetGachaAward>();
        }

        public static bool IsValid(OfflinePetGachaProgress progress)
        {
            if (progress == null || progress.Version != 1 || progress.RandomState < 0 || progress.DrawSequence < 0
                || progress.LastObservedLocalDayKey < 0 || progress.DailyDrawCount < 0
                || progress.HundredReminderSuppressedDayKey < 0 || progress.Score < 0 || progress.TotalDrawCount < 0
                || progress.ShopWeekKey < 0 || progress.ShopProfession < 0 || progress.ShopRefreshSequence < 0
                || progress.OwnedPetIds == null || progress.OwnedPetIds.Count > 1000
                || progress.PityCounters == null || progress.PityCounters.Count > 32
                || progress.AchievementClaims == null || progress.AchievementClaims.Count > 300000
                || progress.ShopGoods == null || progress.ShopGoods.Count > 1000
                || progress.History == null || progress.History.Count > 1000) return false;

            var owned = new HashSet<int>();
            foreach (int itemId in progress.OwnedPetIds)
                if (itemId <= 0 || !owned.Add(itemId)) return false;
            var pity = new HashSet<int>();
            foreach (OfflinePetGachaPityCounter counter in progress.PityCounters)
                if (counter == null || counter.RewardId <= 0 || counter.MissCount < 0 || counter.HitCount < 0
                    || counter.DrawCount < 0 || counter.FireGuaranteedCount < 0
                    || !pity.Add(counter.RewardId)) return false;
            var claims = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflinePetGachaAchievementClaim claim in progress.AchievementClaims)
                if (claim == null || claim.Group != 15 && claim.Group != 16 || claim.Index < 0
                    || !claims.Add(claim.Group + ":" + claim.Index)) return false;
            var goods = new HashSet<int>();
            foreach (OfflinePetGachaGoodsStock stock in progress.ShopGoods)
                if (stock == null || stock.GoodsId <= 0 || stock.Remaining < -1 || !goods.Add(stock.GoodsId)) return false;
            var historyKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflinePetGachaDrawRecord record in progress.History)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.ReceiptId) || record.ReceiptId.Length > 160
                    || record.DrawIndex < 0 || record.UnixSeconds < 0 || record.Awards == null
                    || record.Awards.Count != 1 || !historyKeys.Add(record.ReceiptId + ":" + record.DrawIndex)) return false;
                foreach (OfflinePetGachaAward award in record.Awards)
                    if (award == null || award.ItemId <= 0 || award.Count <= 0 || award.Quality < 0
                        || award.ConvertedItemId < 0 || award.ConvertedCount < 0) return false;
            }
            return true;
        }
    }

    [Serializable] public sealed class OfflinePetGachaPityCounter
    {
        public int RewardId;
        public long DrawCount;
        public int MissCount;
        public int HitCount;
        public int FireGuaranteedCount;
    }
    [Serializable] public sealed class OfflinePetGachaAchievementClaim { public int Group; public int Index; }
    [Serializable] public sealed class OfflinePetGachaGoodsStock { public int GoodsId; public int Remaining; }
    [Serializable] public sealed class OfflinePetGachaDrawRecord
    {
        public string ReceiptId;
        public int DrawIndex;
        public long UnixSeconds;
        public List<OfflinePetGachaAward> Awards = new List<OfflinePetGachaAward>();
    }
    [Serializable] public sealed class OfflinePetGachaAward
    {
        public int ItemId;
        public long Count;
        public int Quality;
        public bool IsPet;
        public bool IsNew;
        public int ConvertedItemId;
        public long ConvertedCount;
    }
}
