using System;
using System.Collections.Generic;
using System.Text.Json;
using Zjcs.Offline.Home;

public static class HomeIncomeActivityLedgerChecks
{
    static int checks;
    static readonly Dictionary<int, long> Empty = new Dictionary<int, long>();
    static readonly HashSet<int> FloatProperties = new HashSet<int>();
    static long At(double seconds) => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks + (long)(seconds * TimeSpan.TicksPerSecond);
    sealed class Snapshot : IHomeIncomeAdditionSnapshot
    {
        public bool HasRecords => true;
        public IReadOnlyList<long> ChangeTicks { get; set; } = Array.Empty<long>();
        public Func<int, long, int> Percent = (_, __) => 0;
        public Func<int, long, long, long> Capacity = (_, __, ___) => 0;
        public int AddPercent(int id, long at) => Percent(id, at);
        public long ExtraCapacity(int id, long count, long at) => Capacity(id, count, at);
        public long ExtraCount(int id, double count, long at) => (long)(count * (double)((float)Percent(id, at) / 10000f));
    }
    static HangUpIncomeRule Rule(double hourly = 100, double hours = 24) => new HangUpIncomeRule { HourlyOutput = hourly, LevelNum = 1, MaxStoreHours = hours };
    static void Advance(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule, Snapshot snapshot,
        double from, double until, IReadOnlyDictionary<int, long> properties = null)
    {
        OfflineHomeIncomeActivityLedger.Advance(record, state, 1, rule, properties ?? Empty, FloatProperties, snapshot, At(from), At(until));
        Require(state.IsValid, "each committed state has finite normalized fractions");
    }
    static HangUpIncomePreview Preview(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule,
        Snapshot snapshot, double at, IReadOnlyDictionary<int, long> properties = null)
        => OfflineHomeIncomeActivityLedger.Preview(record, state, 1, rule, properties ?? Empty, FloatProperties, snapshot, At(at));
    static long Claim(HangUpIncomeRecord record, HomeIncomeActivityState state, HangUpIncomeRule rule, Snapshot snapshot,
        double at, IReadOnlyDictionary<int, long> properties = null)
        => OfflineHomeIncomeActivityLedger.Collect(record, state, 1, rule, properties ?? Empty, FloatProperties, snapshot, At(at));
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
    static void Equal(double a, double b, string message) => Require(!double.IsNaN(a) && Math.Abs(a-b) < 1e-7, message + ": " + a + " != " + b);
    public static int Run()
    {
        checks = 0;
        var rule = Rule(); var record = new HangUpIncomeRecord(); var state = new HomeIncomeActivityState();
        var middle = new Snapshot { ChangeTicks = new[] { At(3600), At(7200) },
            Percent = (_, at) => at >= At(3600) && at < At(7200) ? 10000 : 0 };
        Advance(record, state, rule, middle, 0, 10800);
        Equal(Preview(record, state, rule, middle, 10800).Count, 400, "offline interval boosts only its middle hour");
        Equal(Claim(record, state, rule, middle, 10800), 400, "claim consumes the same segmented balance");
        Equal(Preview(record, state, rule, middle, 10800).Count, 0, "claim cannot be repeated");
        var instant = new HangUpIncomeRecord { StoredSeconds = 3600 }; var instantState = new HomeIncomeActivityState();
        var instantRoom = new Snapshot { Capacity = (_, count, __) => count };
        Advance(instant, instantState, rule, instantRoom, 0, 0);
        Equal(Preview(instant, instantState, rule, instantRoom, 0).Capacity, 4800, "capacity starts at its exact boundary without elapsed time");
        Equal(Preview(instant, instantState, rule, instantRoom, 0).Count, 100, "instant activation cannot boost previously stored output");
        var pieces = new HangUpIncomeRecord(); var pieceState = new HomeIncomeActivityState();
        Advance(pieces, pieceState, rule, middle, 0, 1234);
        Advance(pieces, pieceState, rule, middle, 1234, 4567);
        Advance(pieces, pieceState, rule, middle, 4567, 10800);
        Equal(Preview(pieces, pieceState, rule, middle, 10800).Count, 400, "arbitrary checkpoints retain the same whole reward");
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        pieces = JsonSerializer.Deserialize<HangUpIncomeRecord>(JsonSerializer.Serialize(pieces, jsonOptions), jsonOptions);
        pieceState = JsonSerializer.Deserialize<HomeIncomeActivityState>(JsonSerializer.Serialize(pieceState, jsonOptions), jsonOptions);
        Equal(Claim(pieces, pieceState, rule, middle, 10800), 400, "serialized state cold-opens without recalculating past bonuses");
        rule = Rule(3600); record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        var half = new Snapshot { Percent = (_, __) => 5000 };
        Advance(record, state, rule, half, 0, .5);
        Equal(Preview(record, state, rule, half, .5).Count, 0, "base and bonus keep the original separate integer casts");
        Advance(record, state, rule, half, .5, 1);
        Equal(Claim(record, state, rule, half, 1), 1, "sub-unit bonus is not granted early");
        Equal(state.ExtraFraction, .5, "claim preserves earned activity fraction below capacity");
        Advance(record, state, rule, half, 1, 2);
        Equal(Claim(record, state, rule, half, 2), 2, "fractional activity output survives the next claim");
        rule = Rule(1, 1); record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        var speedThenRoom = new Snapshot { ChangeTicks = new[] { At(3600) }, Percent = (_, at) => at < At(3600) ? 10000 : 0,
            Capacity = (_, count, at) => at >= At(3600) ? count : 0 };
        Advance(record, state, rule, speedThenRoom, 0, 5400);
        Equal(Preview(record, state, rule, speedThenRoom, 5400).Count, 1, "time spent full is discarded before later capacity increase");
        Advance(record, state, rule, speedThenRoom, 5400, 7200);
        Equal(Preview(record, state, rule, speedThenRoom, 7200).Count, 2, "later capacity accepts only new production");
        rule = Rule(10, 1); record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        var room = new Snapshot { ChangeTicks = new[] { At(3600) }, Capacity = (_, count, at) => at < At(3600) ? count : 0 };
        Advance(record, state, rule, room, 0, 5400);
        var expired = Preview(record, state, rule, room, 5400);
        Equal(expired.Capacity, 10, "capacity boost ends at its own boundary");
        Equal(expired.Count, 10, "capacity expiry retains earned mature output");
        record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        room.ChangeTicks = new[] { At(5400) }; room.Capacity = (_, count, at) => at < At(5400) ? count : 0;
        Advance(record, state, rule, room, 0, 7200);
        Equal(Preview(record, state, rule, room, 7200).Count, 15, "mature overflow survives capacity expiry without growing while full");
        Equal(Claim(record, state, rule, room, 7200), 15, "overflow remains manually claimable after activity expiry");
        record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        room.ChangeTicks = new[] { At(5580) }; room.Capacity = (_, count, at) => at < At(5580) ? count : 0;
        Advance(record, state, rule, room, 0, 7200);
        Equal(Claim(record, state, rule, room, 7200), 15, "capacity expiry preserves mature output with a partial next item");
        Equal(record.StoredSeconds, 180, "capacity expiry does not discard an earned base fraction on collection");
        record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        var fractionalExpiry = new Snapshot { ChangeTicks = new[] { At(5580) },
            Capacity = (_, count, at) => at < At(5580) ? count * 9 : 0,
            Percent = (_, at) => at < At(5580) ? 5000 : 0 };
        Advance(record, state, rule, fractionalExpiry, 0, 7200);
        Equal(Claim(record, state, rule, fractionalExpiry, 7200), 22, "expiry preserves separate mature base and bonus counts");
        Equal(record.StoredSeconds, 180, "expiry preserves the base fractional-time remainder");
        Equal(state.ExtraFraction, .75, "expiry preserves the earned activity fraction through collection");
        state = JsonSerializer.Deserialize<HomeIncomeActivityState>(JsonSerializer.Serialize(state, jsonOptions), jsonOptions);
        Equal(state.ExtraFraction, .75, "activity fractional carry survives serialized cold reopen");
        record = new HangUpIncomeRecord { SettledCount = 100 }; state = new HomeIncomeActivityState();
        Advance(record, state, rule, half, 0, 7200);
        Equal(Preview(record, state, rule, half, 7200).Count, 100, "existing acceleration overflow is preserved");
        Equal(record.StoredSeconds, 0, "acceleration overflow prevents new ordinary production while full");
        var props = new Dictionary<int, long> { { 1002, 7 } };
        rule = Rule(100); rule.LevelNum = 2; rule.EffectFixedPropertyId = 1002;
        record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        Advance(record, state, rule, half, 0, 1800, props); Advance(record, state, rule, half, 1800, 3600, props);
        Equal(Preview(record, state, rule, half, 3600, props).Count, 321,
            "fixed production is included once, with original ordinary parameter order");
        var legacy = new HangUpIncomeRecord { StoredSeconds = 1810 }; var legacyState = new HomeIncomeActivityState();
        var original = new HangUpIncomeRecord { StoredSeconds = 1810 };
        long expected = OfflineHangUpIncome.Collect(original, rule, props, FloatProperties);
        Equal(Claim(legacy, legacyState, rule, null, 0, props), expected, "empty activity state preserves the existing claim rule");
        Equal(legacy.StoredSeconds, original.StoredSeconds, "empty activity state preserves the exact original time remainder");
        Require(BitConverter.DoubleToInt64Bits(legacy.StoredSeconds) == BitConverter.DoubleToInt64Bits(original.StoredSeconds),
            "legacy remainder is bit-for-bit unchanged");
        rule = Rule(3600); record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        for (int step = 0; step < 100; step++) Advance(record, state, rule, half, step / 10.0, (step + 1) / 10.0);
        Equal(record.StoredSeconds, 10, "frequent subsecond checkpoints retain exact accepted time");
        Equal(Preview(record, state, rule, half, 10).Count, 15, "frequent checkpoints cannot lose a whole base or bonus item");
        var unusual = new Snapshot { Percent = (_, __) => 3333 };
        var once = new HangUpIncomeRecord(); var onceState = new HomeIncomeActivityState();
        record = new HangUpIncomeRecord(); state = new HomeIncomeActivityState();
        Advance(once, onceState, rule, unusual, 0, 10);
        for (int step = 0; step < 100; step++) Advance(record, state, rule, unusual, step / 10.0, (step + 1) / 10.0);
        Require(state.ExtraCount == onceState.ExtraCount
            && BitConverter.DoubleToInt64Bits(state.ExtraFraction) == BitConverter.DoubleToInt64Bits(onceState.ExtraFraction),
            "non-binary float percentage is bit-for-bit independent of checkpoint frequency");
        record = new HangUpIncomeRecord { SettledCount = long.MaxValue }; state = new HomeIncomeActivityState();
        Advance(record, state, rule, half, 0, 1, props);
        Require(Preview(record, state, rule, half, 1, props).Count == long.MaxValue, "maximum settled overflow cannot wrap");
        double unchanged = record.StoredSeconds;
        Advance(record, state, rule, half, 1, 0, props);
        Equal(record.StoredSeconds, unchanged, "clock rollback does not consume production time");
        return checks;
    }
}
