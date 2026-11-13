using System;
using System.IO;
using System.Linq;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // Projects local save fields into the original counter implementations.
    // Mutations belong inside the same profile transaction as cost and awards.
    public sealed class OfflineHomeQuickBuyCounters
    {
        readonly IHomeQuickBuyCalendar calendar;

        public OfflineHomeQuickBuyCounters(IHomeQuickBuyCalendar calendar)
        { this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar)); }

        public long GetBuyCount(OfflinePlayerProgress player, int itemId, EQuickBuyRefreshType type, DateTime now)
        {
            RequireUtc(now);
            return Read(player.QuickBuy).QuickBuyGetItemBuyCount(itemId, type, now);
        }

        public void AddBuyCount(OfflinePlayerProgress player, EQuickBuyRefreshType type,
            int itemId, long count, DateTime time)
        {
            RequireUtc(time);
            if ((int)type < 0 || (int)type > 2 || itemId <= 0 || count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            var data = Read(player.QuickBuy);
            data.QuickBuyAddItemBuyCount(type, itemId, count, time);
            var updated = new OfflineQuickBuyProgress();
            foreach (var entry in data.BuyDatas.OrderBy(row => row.Key))
                updated.Counters.Add(new OfflineQuickBuyCounterProgress {
                    RefreshType = (int)entry.Key,
                    LastUpdateTimeUtcTicks = entry.Value is HomeQuickBuyCounterPeriodic periodic
                        ? periodic.LastUpdateTime.Ticks : 0,
                    BuyCounts = entry.Value.BuyCount.OrderBy(row => row.Key)
                        .Select(row => new OfflineQuickBuyItemCount { ItemId=row.Key, Count=row.Value }).ToList()
                });
            if (!OfflineQuickBuyProgress.IsValid(updated)) throw new InvalidDataException("Invalid quick-buy counter update.");
            player.QuickBuy = updated;
        }

        HomeQuickBuyData Read(OfflineQuickBuyProgress progress)
        {
            if (!OfflineQuickBuyProgress.IsValid(progress)) throw new InvalidDataException("Invalid quick-buy counter save.");
            var result = new HomeQuickBuyData(calendar);
            foreach (var row in progress.Counters)
            {
                HomeQuickBuyCounter counter;
                switch ((EQuickBuyRefreshType)row.RefreshType)
                {
                    case EQuickBuyRefreshType.None: counter = new HomeQuickBuyCounterNonRefresh(); break;
                    case EQuickBuyRefreshType.Daily: counter = new HomeQuickBuyCounterDaily(calendar); break;
                    case EQuickBuyRefreshType.Weekly: counter = new HomeQuickBuyCounterWeekly(calendar); break;
                    default: throw new InvalidDataException("Unknown quick-buy refresh type.");
                }
                if (counter is HomeQuickBuyCounterPeriodic periodic)
                    periodic.LastUpdateTime = new DateTime(row.LastUpdateTimeUtcTicks, DateTimeKind.Utc);
                foreach (var item in row.BuyCounts) counter.BuyCount.Add(item.ItemId, item.Count);
                result.BuyDatas.Add((EQuickBuyRefreshType)row.RefreshType, counter);
            }
            return result;
        }

        static void RequireUtc(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Quick-buy counters require UTC timestamps.");
        }
    }
}
