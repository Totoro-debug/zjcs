using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeOutputAddition;

public static class HomeIncomeAdditionSnapshotChecks
{
    static int checks;
    static DateTime At(int second) => new DateTime(638000000000000000L + second * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
    public static int Run(OutputAdditionInfo[] rows, HomeOutputAdditionSettings settings)
    {
        checks = 0;
        var income = rows.First(row => row.OutputAdditionPropType == settings.HomeHangUpAddPercentTypeDict[1] && row.OutputAdditionPropVal > 0);
        var capacity = rows.First(row => row.OutputAdditionPropType == settings.HomeHangUpMaxStoreTypeDict[1] && row.OutputAdditionPropVal > 0);
        var record = Record(1, income, 20, 30);
        record.StartTime = At(10); record.EndTime = At(40);
        record.OutputAdditionPropDict.Add(capacity.OutputAdditionPropType,
            new PlayerOutputAdditionPropTypeDataWrap { ClassId = capacity.ClassId, StartTime = At(15), EndTime = At(35) });
        bool globallyDisabled = false, incomeDisabled = false;
        Func<int?, bool> disabled = id => id.HasValue ? incomeDisabled && id.Value == income.ClassId : globallyDisabled;
        var snapshot = new OfflineHomeIncomeAdditionSnapshot(new[] { record }, rows, settings, disabled);
        Require(snapshot.HasRecords && snapshot.ChangeTicks.SequenceEqual(new[] { 10, 15, 20, 30, 35, 40 }.Select(second => At(second).Ticks)),
            "parent and individual property boundaries are complete, ordered and unique");
        Require(snapshot.AddPercent(1, At(9).Ticks) == 0 && snapshot.AddPercent(1, At(10).Ticks) == 0,
            "future activity properties do not apply before their own start");
        Require(snapshot.AddPercent(1, At(20).Ticks) == income.OutputAdditionPropVal,
            "income begins exactly at its recorded boundary");
        Require(snapshot.AddPercent(1, At(30).Ticks - 1) == income.OutputAdditionPropVal
            && snapshot.AddPercent(1, At(30).Ticks) == 0, "property expiry is exact to one UTC tick");
        Require(snapshot.ExtraCapacity(1, 1000, At(14).Ticks) == 0
            && snapshot.ExtraCapacity(1, 1000, At(35).Ticks) == 0, "capacity uses its own active period");
        Require(snapshot.AddPercent(1, At(40).Ticks) == 0 && snapshot.ExtraCapacity(1, 1000, At(40).Ticks) == 0,
            "parent expiry cannot leave an effective property");
        var oracle = new HomeOutputAdditionPlayer(new Dictionary<long, PlayerOutputAdditionDataWrap> { { record.Id, record } },
            new OutputAdditionInfoParser(rows.ToDictionary(row => row.ClassId)), settings, () => At(20), _ => false);
        Require(snapshot.ExtraCapacity(1, 16777217, At(20).Ticks) == oracle.CalcOutputAdditionExtStore(1, 16777217),
            "capacity retains the original float conversion and truncation");
        Require(snapshot.ExtraCount(1, 19.9, At(20).Ticks) == oracle.CalcOutputAdditionExtCount(1, 19.9),
            "income retains the original numeric formula and truncation");
        incomeDisabled = true;
        Require(snapshot.AddPercent(1, At(20).Ticks) == 0 && snapshot.ExtraCapacity(1, 1000, At(20).Ticks) > 0,
            "class-specific disable gates only its original property");
        incomeDisabled = false; globallyDisabled = true;
        Require(snapshot.AddPercent(1, At(20).Ticks) == 0 && snapshot.ExtraCapacity(1, 1000, At(20).Ticks) == 0
            && snapshot.ExtraCount(1, 100, At(20).Ticks) == 0, "global disable retains the original gate");
        globallyDisabled = false;
        var overlap = new OfflineHomeIncomeAdditionSnapshot(new[] { record, Record(2, income, 25, 45) }, rows, settings, disabled);
        Require(overlap.AddPercent(1, At(25).Ticks) == income.OutputAdditionPropVal * 2,
            "simultaneous activities retain the original additive property rule");
        Require(overlap.AddPercent(1, At(35).Ticks) == income.OutputAdditionPropVal
            && overlap.AddPercent(1, At(45).Ticks) == 0, "overlap ends independently for each property");
        var empty = new OfflineHomeIncomeAdditionSnapshot(Array.Empty<PlayerOutputAdditionDataWrap>(), rows, settings, disabled);
        Require(!empty.HasRecords && empty.ChangeTicks.Count == 0 && empty.AddPercent(1, At(20).Ticks) == 0,
            "an empty saved ledger creates no activity or bonus");
        var cart = rows.First(row => row.OutputAdditionPropType == settings.HomeHangUpAddPercentTypeDict[2] && row.OutputAdditionPropVal > 0);
        var cartSnapshot = new OfflineHomeIncomeAdditionSnapshot(new[] { Record(3, cart, 0, 50) }, rows, settings, disabled);
        Require(cartSnapshot.AddPercent(1, At(20).Ticks) == 0 && cartSnapshot.AddPercent(2, At(20).Ticks) == cart.OutputAdditionPropVal,
            "bed and cart use their actual separate configuration mappings");
        Require(snapshot.AddPercent(999999, At(20).Ticks) == 0,
            "an unmapped hang-up does not invent an activity property");
        int originalValue = income.OutputAdditionPropVal;
        record.EndTime = At(0); record.OutputAdditionPropDict.Clear(); income.OutputAdditionPropVal = 1;
        settings.HomeHangUpAddPercentTypeDict[1] = OutputAdditionPropType.None;
        Require(snapshot.AddPercent(1, At(20).Ticks) == originalValue,
            "the settlement snapshot is isolated from later record, table and mapping mutations");
        bool duplicateRejected = false;
        try { new OfflineHomeIncomeAdditionSnapshot(new[] { record, record }, rows, settings, disabled); }
        catch (ArgumentException) { duplicateRejected = true; }
        Require(duplicateRejected, "duplicate activity identities cannot enter a settlement snapshot");
        return checks;
    }
    static PlayerOutputAdditionDataWrap Record(long id, OutputAdditionInfo row, int from, int until)
        => new PlayerOutputAdditionDataWrap { Id = id, OutputActionType = row.OutputActionType,
            StartTime = At(0), EndTime = At(50), OutputAdditionPropDict = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> {
                { row.OutputAdditionPropType, new PlayerOutputAdditionPropTypeDataWrap { ClassId = row.ClassId,
                    StartTime = At(from), EndTime = At(until) } } } };
}
