using System;
using System.Collections.Generic;
using Zjcs.Official.HomeIncomeRules;

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
            return RewardCount(rule, properties, floatPropertyIds, 1.0);
        }

        public static double RewardCount(HangUpIncomeRule rule, IReadOnlyDictionary<int, long> properties,
            ISet<int> floatPropertyIds, double hours)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (rule.LevelNum <= 0) return 0;
            var info = new HangUpInfo
            {
                BindFurniture = rule.IsCart ? EFurniture.Cart : EFurniture.None,
                EffectPercentPropType = (PropType)rule.EffectPercentPropertyId,
                EffectFixedPropType = (PropType)rule.EffectFixedPropertyId,
                FinallyEffectPercentPropType = (PropType)rule.FinallyEffectPercentPropertyId
            };
            var formula = new BattleFormulaHandler(type => HomeIncomePropertyCatalog.Get(type) == null ? null
                : floatPropertyIds != null && floatPropertyIds.Contains((int)type) ? FloatingProperty : IntegerProperty);
            return HangUpUtils.CalcRewardCount(info, new HangUpRewardInfo { HourlyOutput = (float)rule.HourlyOutput },
                formula, properties, hours, (float)rule.LevelNum);
        }

        private static readonly PropCfgInfo FloatingProperty = new PropCfgInfo { Float = true };
        private static readonly PropCfgInfo IntegerProperty = new PropCfgInfo { Float = false };

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
            long unsettled = WholeCount(RewardCount(rule, properties, floatPropertyIds, Math.Max(0, record.StoredSeconds) / 3600.0));
            long capacity = WholeCount(RewardCount(rule, properties, floatPropertyIds, Math.Max(0, rule.MaxStoreHours)));
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
