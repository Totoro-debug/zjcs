using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Home
{
    public sealed class HangUpIncomeRule
    {
        public int EffectPercentPropertyId;
        public int EffectFixedPropertyId;
        public int FinallyEffectPercentPropertyId;
        public bool IsCart;
        public double HourlyOutput;
        public double LevelNum;
        public double MaxStoreHours;
    }

    public sealed class HangUpIncomeRecord
    {
        public double StoredSeconds;
        public long SettledCount;
    }

    public sealed class HangUpIncomePreview
    {
        public double HourlyRate;
        public long Count;
        public long Capacity;
        public long UnsettledCount;
    }

    public static class OfflineHangUpIncome
    {
        public static double HourlyRate(HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties,
            ISet<int> floatPropertyIds)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (rule.LevelNum <= 0) return 0;

            double percent = 1.0 + Property(rule.EffectPercentPropertyId, properties, floatPropertyIds);
            if (rule.IsCart)
                percent += Property(1034, properties, floatPropertyIds);
            if (rule.FinallyEffectPercentPropertyId > 0)
                percent *= 1.0 + Property(rule.FinallyEffectPercentPropertyId, properties, floatPropertyIds);

            double fixedOutput = Property(rule.EffectFixedPropertyId, properties, floatPropertyIds);
            return rule.HourlyOutput * rule.LevelNum * percent + fixedOutput;
        }

        public static long AdvanceCheckpoint(ref long lastSeenUnixSeconds, long nowUnixSeconds)
        {
            long elapsed = nowUnixSeconds <= lastSeenUnixSeconds
                ? 0L
                : lastSeenUnixSeconds < 0 && nowUnixSeconds > long.MaxValue + lastSeenUnixSeconds
                    ? long.MaxValue
                    : nowUnixSeconds - lastSeenUnixSeconds;
            lastSeenUnixSeconds = Math.Max(lastSeenUnixSeconds, nowUnixSeconds);
            return elapsed;
        }

        public static void Accrue(HangUpIncomeRecord record, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds, double elapsedSeconds)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (!IsFinite(elapsedSeconds) || elapsedSeconds <= 0) return;

            double storeLimit = Math.Max(0, rule.MaxStoreHours) * 3600.0;
            record.StoredSeconds = Math.Min(storeLimit, Math.Max(0, record.StoredSeconds) + elapsedSeconds);
        }

        public static HangUpIncomePreview Preview(HangUpIncomeRecord record, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            double rate = HourlyRate(rule, properties, floatPropertyIds);
            long unsettled = WholeCount(Math.Max(0, record.StoredSeconds) / 3600.0 * rate);
            long capacity = WholeCount(Math.Max(0, rule.MaxStoreHours) * rate);
            long total = AddSaturated(Math.Max(0, record.SettledCount), unsettled);
            return new HangUpIncomePreview
            {
                HourlyRate = rate,
                Count = Math.Max(Math.Max(0, record.SettledCount), Math.Min(total, capacity)),
                Capacity = capacity,
                UnsettledCount = unsettled
            };
        }

        public static long Collect(HangUpIncomeRecord record, HangUpIncomeRule rule,
            IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds)
        {
            HangUpIncomePreview preview = Preview(record, rule, properties, floatPropertyIds);
            if (preview.Count <= 0) return 0;

            if (preview.HourlyRate > 0)
                record.StoredSeconds = Math.Max(0, record.StoredSeconds - preview.UnsettledCount * 3600.0 / preview.HourlyRate);
            record.SettledCount = 0;
            return preview.Count;
        }

        private static double Property(int id, IReadOnlyDictionary<int, long> properties, ISet<int> floatPropertyIds)
        {
            if (id <= 0 || properties == null || !properties.TryGetValue(id, out long value)) return 0;
            return floatPropertyIds != null && floatPropertyIds.Contains(id) ? value / 10000.0 : value;
        }

        private static long WholeCount(double value)
        {
            if (!IsFinite(value) || value <= 0) return 0;
            return value >= long.MaxValue ? long.MaxValue : (long)value;
        }

        private static long AddSaturated(long left, long right)
        {
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
