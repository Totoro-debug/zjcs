using System;
using System.Collections.Generic;
using Zjcs.Offline.Home;

public static class HomeIncomeAdditionIntervalChecks
{
    static int checks;
    static readonly Dictionary<int, long> Empty = new Dictionary<int, long>();
    static readonly HashSet<int> FloatProperties = new HashSet<int>();
    sealed class Snapshot : IHomeIncomeAdditionSnapshot
    {
        public bool HasRecords { get; set; } = true;
        public IReadOnlyList<long> ChangeTicks { get; set; } = Array.Empty<long>();
        public Func<int, long, int> Percent = (_, __) => 0;
        public int AddPercent(int id, long at) => Percent(id, at);
        public long ExtraCapacity(int id, long capacity, long at) => throw new InvalidOperationException("Only income is projected here.");
        public long ExtraCount(int id, double count, long at) => throw new InvalidOperationException("Fractions must reach the ledger before truncation.");
    }
    static long At(double seconds) => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks + (long)(seconds * TimeSpan.TicksPerSecond);
    static double Bonus(HangUpIncomeRule rule, Snapshot snapshot, double start, double end, double stored = 0,
        IReadOnlyDictionary<int, long> props = null) => OfflineHangUpIncomeAddition.ValueForInterval(1, rule, props ?? Empty,
            FloatProperties, snapshot, At(start), At(end), stored);
    static void Equal(double actual, double expected, string message)
    {
        if (double.IsNaN(actual) || Math.Abs(actual - expected) > 1e-9 * Math.Max(1, Math.Abs(expected)))
            throw new InvalidOperationException(message + ": " + actual + " != " + expected);
        checks++;
    }
    public static int Run()
    {
        checks = 0;
        var rule = new HangUpIncomeRule { HourlyOutput = 100, LevelNum = 1, MaxStoreHours = 24 };
        var middle = new Snapshot { ChangeTicks = new[] { At(3600), At(7200) },
            Percent = (id, at) => id == 1 && at >= At(3600) && at < At(7200) ? 10000 : 0 };
        Equal(Bonus(rule, middle, 0, 10800), 100, "only one of three offline hours is boosted");
        Equal(Bonus(rule, middle, 0, 3600), 0, "future boost does not affect an interval ending at its start");
        Equal(Bonus(rule, middle, 7200, 10800), 0, "expired boost does not affect later production");
        Equal(Bonus(rule, middle, 0, 10800, 3600), 100, "stored output is not retroactively boosted");
        var overlap = new Snapshot { ChangeTicks = new[] { At(1800), At(3600), At(5400) },
            Percent = (_, at) => at < At(1800) ? 10000 : at < At(3600) ? 15000 : at < At(5400) ? 5000 : 0 };
        Equal(Bonus(rule, overlap, 0, 7200), 150, "overlapping activity percentages follow their own boundaries");
        double pieces = Bonus(rule, overlap, 0, 1234) + Bonus(rule, overlap, 1234, 4321, 1234)
            + Bonus(rule, overlap, 4321, 7200, 4321);
        Equal(pieces, 150, "checkpoint partitions do not discard bonus fractions");
        var fractional = new Snapshot { ChangeTicks = new[] { At(.5), At(1.5) },
            Percent = (_, at) => at >= At(.5) && at < At(1.5) ? 3333 : 0 };
        rule.HourlyOutput = 3600;
        Equal(Bonus(rule, fractional, 0, 2), (double)(3333f / 10000f), "subsecond boundaries retain the original float percentage");
        Equal(Bonus(rule, fractional, 0, .75) + Bonus(rule, fractional, .75, 2, .75),
            (double)(3333f / 10000f), "subsecond checkpoints retain fractional bonus");
        Equal(Bonus(rule, fractional, 2, 0), 0, "clock rollback produces no bonus");
        Equal(Bonus(rule, fractional, 1, 1), 0, "equal checkpoints produce no bonus");
        fractional.HasRecords = false;
        Equal(Bonus(rule, fractional, 0, 2), 0, "empty ledgers cannot create an activity");
        var fixedProps = new Dictionary<int, long> { { 1002, 7 } };
        rule.EffectFixedPropertyId = 1002; rule.HourlyOutput = 100; rule.LevelNum = 2;
        Equal(OfflineHangUpIncome.UnsettledRewardValue(rule, fixedProps, FloatProperties, 7200), 414,
            "ordinary production keeps the original level/elapsed argument order");
        Equal(OfflineHangUpIncome.RewardCount(rule, fixedProps, FloatProperties, 2), 414,
            "two-hour fast output happens to match at level two");
        Equal(OfflineHangUpIncome.UnsettledRewardValue(rule, fixedProps, FloatProperties, 3600), 214,
            "ordinary fixed effect is scaled by level");
        Equal(OfflineHangUpIncome.RewardCount(rule, fixedProps, FloatProperties, 1), 207,
            "fast fixed effect is scaled by requested hours");
        var full = new Snapshot { Percent = (_, __) => 10000 };
        Equal(Bonus(rule, full, 0, 3600, 3600, fixedProps), 200,
            "crossing checkpoints does not repeatedly add the original fixed effect");
        Equal(Bonus(rule, full, 0, 3600, 0, fixedProps), 214,
            "a first production interval includes the original fixed effect once");
        Equal(OfflineHangUpIncome.UnsettledRewardValue(rule, fixedProps, FloatProperties, 0), 0,
            "zero duration cannot introduce original zero-denominator NaN into a balance");
        long ordinary = OfflineHangUpIncome.UnsettledRewardCount(rule, fixedProps, FloatProperties, 1234.5, out double remainder);
        Equal(ordinary, (long)OfflineHangUpIncome.UnsettledRewardValue(rule, fixedProps, FloatProperties, 1234.5),
            "unrounded projection agrees with the existing integer preview");
        Equal(remainder, (OfflineHangUpIncome.UnsettledRewardValue(rule, fixedProps, FloatProperties, 1234.5) - ordinary)
            * 1234.5 / OfflineHangUpIncome.UnsettledRewardValue(rule, fixedProps, FloatProperties, 1234.5),
            "ordinary fractional-time preview remains unchanged");
        return checks;
    }
}
