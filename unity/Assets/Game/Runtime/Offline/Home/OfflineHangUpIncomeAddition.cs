using System;
using System.Collections.Generic;
using Zjcs.Offline.State;

namespace Zjcs.Offline.Home
{
    // Supplies only activity output for an interval that the caller has accepted
    // as production time. Capacity, claims and durable fractional carry belong
    // to the income ledger; already stored output is never boosted retroactively.
    public static class OfflineHangUpIncomeAddition
    {
        public static double ValueForInterval(int hangUpId, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds,
            IHomeIncomeAdditionSnapshot additions, long fromUtcTicks, long untilUtcTicks,
            double storedSeconds)
        {
            if (additions == null || !additions.HasRecords || fromUtcTicks >= untilUtcTicks) return 0;
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (double.IsNaN(storedSeconds) || double.IsInfinity(storedSeconds) || storedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(storedSeconds));
            double result = 0;
            foreach (var segment in OfflineIncomeTimeSegments.Split(fromUtcTicks, untilUtcTicks, additions.ChangeTicks))
            {
                int percent = additions.AddPercent(hangUpId, segment.Start);
                if (percent <= 0) continue;
                double before = storedSeconds + (segment.Start - fromUtcTicks) / (double)TimeSpan.TicksPerSecond;
                double after = storedSeconds + (segment.End - fromUtcTicks) / (double)TimeSpan.TicksPerSecond;
                double value = ValueForProductionChange(rule, properties, floatPropertyIds, percent, before, after);
                if (double.IsNaN(value) || value <= 0) continue;
                result += value;
                if (double.IsInfinity(result) || result >= long.MaxValue) return long.MaxValue;
            }
            return result;
        }

        public static double ValueForProductionChange(HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds, int percent,
            double beforeSeconds, double afterSeconds)
        {
            if (percent <= 0 || afterSeconds <= beforeSeconds) return 0;
            double produced = OfflineHangUpIncome.UnsettledRewardValue(rule, properties, floatPropertyIds, afterSeconds)
                - OfflineHangUpIncome.UnsettledRewardValue(rule, properties, floatPropertyIds, beforeSeconds);
            // Original Formula.Int2Float converts through float before the
            // double multiplication; the ledger owns the final integer cast.
            double value = Math.Max(0, produced) * (double)((float)percent / 10000f);
            return double.IsNaN(value) || value <= 0 ? 0 : Math.Min(long.MaxValue, value);
        }
    }
}
