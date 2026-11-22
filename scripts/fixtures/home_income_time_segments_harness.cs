using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

// Only the pure production accumulator is exercised here. Save/profile methods
// below are compile boundaries and do not stand in for transaction validation.
public static class IncomeTimeSegmentsChecks
{
    static int checks;
    public static int Run()
    {
        checks = 0;
        var parts = OfflineIncomeTimeSegments.Split(0, 10, new long[] { 10, 7, 2, 2, 0, -1, 20 }).ToArray();
        Require(parts.Length == 3 && parts.Sum(part => (part.End - part.Start)
            * (part.Start >= 2 && part.Start < 7 ? 2 : 1)) == 15, "overlapping activity boundaries integrate only their active intervals");
        long nearMax = long.MaxValue - 10;
        var exact = OfflineIncomeTimeSegments.Split(nearMax, long.MaxValue, new[] { nearMax + 2, nearMax + 7 }).ToArray();
        Require(exact.Select(part => part.End - part.Start).SequenceEqual(new long[] { 2, 5, 3 }), "integer time units retain precision near the range limit");
        Require(!OfflineIncomeTimeSegments.Split(10, 0, Array.Empty<long>()).Any()
            && !OfflineIncomeTimeSegments.Split(10, 10, Array.Empty<long>()).Any(), "rollback and repeated checkpoints produce no interval");

        var state = Fruit(new OfflineFruitTreeOutputAddition { Id = "one", BeginUnixSeconds = 3600, EndUnixSeconds = 7200, SpeedPercent = 10000 });
        Advance(state, 0, 10800);
        Require(state.StoredCount == 1 && Math.Abs(state.FractionalCount) < 1e-12, "one boosted hour within three hours produces exactly one fruit");
        state = Overlap(); Advance(state, 0, 10800);
        Require(state.StoredCount == 2 && Math.Abs(state.FractionalCount - .25) < 1e-12, "overlapping production rates retain their fractional fruit");
        var resumed = Overlap(); Advance(resumed, 0, 4500); Advance(resumed, 4500, 10800);
        Require(resumed.StoredCount == state.StoredCount && Math.Abs(resumed.FractionalCount - state.FractionalCount) < 1e-12,
            "a save checkpoint within an activity does not change the production result");
        state = Fruit(new OfflineFruitTreeOutputAddition { Id = "capacity", BeginUnixSeconds = 0, EndUnixSeconds = 3600, CapacityPercent = 10000 });
        state.StoredCount = 20; state.FractionalCount = .5; Advance(state, 0, 7200);
        Require(state.StoredCount == 20 && Math.Abs(state.FractionalCount - .75) < 1e-12,
            "capacity expiry preserves already mature overflow and stops additional production");
        state = Fruit(); state.StoredCount = 14; state.FractionalCount = .9; Advance(state, 0, 7200);
        Require(state.StoredCount == 15 && state.FractionalCount == 0,
            "reaching ordinary capacity clears its production remainder");
        return checks;
    }
    static OfflineFruitTreeProgress Fruit(params OfflineFruitTreeOutputAddition[] additions)
        => new OfflineFruitTreeProgress { ProductionRepaired = true, OutputAdditions = additions.ToList() };
    static OfflineFruitTreeProgress Overlap() => Fruit(
        new OfflineFruitTreeOutputAddition { Id = "a", BeginUnixSeconds = 0, EndUnixSeconds = 7200, SpeedPercent = 10000 },
        new OfflineFruitTreeOutputAddition { Id = "b", BeginUnixSeconds = 3600, EndUnixSeconds = 10800, SpeedPercent = 20000 });
    static void Advance(OfflineFruitTreeProgress state, long from, long until)
        => typeof(OfflineFruitTreeService).GetMethod("AdvanceSegments", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { state, from, until });
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
}

namespace Zjcs.Offline.Progression
{
    public enum ProgressionResultStatus { Applied, Unchanged, Ready, InvalidState }
    public sealed class ProgressionResult { public ProgressionResultStatus Status; public string Message; public bool IsApplied => Status == ProgressionResultStatus.Applied; }
    public sealed class OfflineItemStack { public int ItemId; public long Count; }
    public sealed class SegmentFixturePlayer { public OfflineFruitTreeProgress FruitTree = new OfflineFruitTreeProgress(); public List<OfflineItemStack> Inventory = new List<OfflineItemStack>(); }
    public sealed class SegmentFixtureSave { public SegmentFixturePlayer Player = new SegmentFixturePlayer(); }
    public sealed class OfflineMainlineProgressSession
    {
        public SegmentFixtureSave Save = new SegmentFixtureSave();
        public void Reload() => throw new NotSupportedException("No save operations in pure timeline checks.");
        public ProgressionResult ApplyInventoryTransaction(string receipt, Action<SegmentFixtureSave> action) => throw new NotSupportedException();
        public ProgressionResult ApplyCharacterMutationIfChanged(Func<SegmentFixturePlayer, bool> action) => throw new NotSupportedException();
    }
}
