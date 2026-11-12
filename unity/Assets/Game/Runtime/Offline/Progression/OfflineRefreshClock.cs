using System;

namespace Zjcs.Offline.Progression
{
    // User-selected offline schedule. Official source payloads retain their original times.
    public static class OfflineRefreshClock
    {
        public const int Hour = 6;
        public static readonly TimeSpan TimeOfDay = TimeSpan.FromHours(Hour);
        public static DateTime DayStart(DateTime localNow)
        {
            DateTime start = localNow.Date.Add(TimeOfDay);
            return localNow < start ? start.AddDays(-1) : start;
        }
        // Old persisted boundary identifiers refer to the same calendar day.
        public static long NormalizeBoundary(long ticks)
            => ticks <= 0 ? ticks : new DateTime(ticks).Date.Add(TimeOfDay).Ticks;
    }
}
