using System;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeCurrencyUI
{
    // Confirmed offline choice: machine-local civil dates, with the shared 06:00
    // reset. Original day/week algorithms run over those local calendar fields;
    // persisted purchase instants remain UTC, including across daylight saving.
    public sealed class OfflineHomeLocalQuickBuyCalendar : IHomeQuickBuyCalendar
    {
        readonly Func<TimeZoneInfo> zone;
        readonly HomeQuickBuyCalendar wallCalendar;

        public OfflineHomeLocalQuickBuyCalendar(DayOfWeek weeklyStartDay, Func<TimeZoneInfo> zone = null)
        {
            this.zone = zone ?? (() => TimeZoneInfo.Local);
            wallCalendar = new HomeQuickBuyCalendar(DateTime.MinValue.Add(OfflineRefreshClock.TimeOfDay), 0, weeklyStartDay);
        }

        public int CalcDays(DateTime from, DateTime to)
        {
            var timeZone = Zone;
            return wallCalendar.CalcDays(LocalFields(from, timeZone), LocalFields(to, timeZone));
        }
        public bool IsSameDay(DateTime from, DateTime to) => Math.Abs(CalcDays(from, to)) < 1;
        public bool IsSameWeek(DateTime from, DateTime to)
        {
            var timeZone = Zone;
            return wallCalendar.IsSameWeek(LocalFields(from, timeZone), LocalFields(to, timeZone));
        }
        public DateTime GetDailyResetTimeFromDate(DateTime time)
        {
            var timeZone = Zone;
            DateTime reset = wallCalendar.GetDailyResetTimeFromDate(LocalFields(time, timeZone));
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(reset, DateTimeKind.Unspecified), timeZone);
        }
        TimeZoneInfo Zone => zone() ?? throw new InvalidOperationException("The local quick-buy time zone is unavailable.");
        static DateTime LocalFields(DateTime utc, TimeZoneInfo timeZone)
        {
            // Original newly constructed counters use default(DateTime) before
            // their first purchase. It is a sentinel, not a saved local instant.
            if (utc == DateTime.MinValue) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Local quick-buy calendars require UTC instants.");
            // Kind here belongs only to the original field-based calculation.
            // It must never be persisted as an actual UTC purchase timestamp.
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone), DateTimeKind.Utc);
        }
    }
}
