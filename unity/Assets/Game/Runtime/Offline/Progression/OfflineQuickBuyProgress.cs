using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineQuickBuyProgress
    {
        public List<OfflineQuickBuyCounterProgress> Counters = new List<OfflineQuickBuyCounterProgress>();

        public static bool IsValid(OfflineQuickBuyProgress progress)
        {
            if (progress == null || progress.Counters == null || progress.Counters.Count > 3) return false;
            var types = new HashSet<int>();
            foreach (var counter in progress.Counters)
            {
                if (counter == null || counter.RefreshType < 0 || counter.RefreshType > 2
                    || !types.Add(counter.RefreshType) || counter.LastUpdateTimeUtcTicks < 0
                    || counter.LastUpdateTimeUtcTicks > DateTime.MaxValue.Ticks
                    || counter.BuyCounts == null || counter.BuyCounts.Count > 20000) return false;
                var ids = new HashSet<int>();
                foreach (var item in counter.BuyCounts)
                    if (item == null || item.ItemId <= 0 || item.Count < 0 || !ids.Add(item.ItemId)) return false;
            }
            return true;
        }
    }

    [Serializable]
    public sealed class OfflineQuickBuyCounterProgress
    {
        public int RefreshType;
        public long LastUpdateTimeUtcTicks;
        public List<OfflineQuickBuyItemCount> BuyCounts = new List<OfflineQuickBuyItemCount>();
    }

    [Serializable]
    public sealed class OfflineQuickBuyItemCount
    {
        public int ItemId;
        public long Count;
    }
}
