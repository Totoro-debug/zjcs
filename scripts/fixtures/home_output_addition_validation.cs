using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeOutputAddition;

public sealed partial class HomeOutputAdditionHostRunner
{
    private IEnumerator CheckOutputAddition()
    {
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        DateTime current = now;
        bool globallyDisabled = false;
        var disabled = new HashSet<int>();
        Func<int?, bool> gate = id => globallyDisabled || (id.HasValue && disabled.Contains(id.Value));
        var source = new OfflineHomeOutputAddition(home.Session, () => current, gate);
        var original = File.ReadAllBytes(Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "home-save.json"));
        Require(source.ReadState().Records.Count == 0 && source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 0
            && source.Groups(OutputGroupType.Bed).Count == 0 && source.ActiveActions().Count == 0,
            "new game has no synthesized activity record or property");
        Require(original.SequenceEqual(File.ReadAllBytes(Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "home-save.json"))),
            "queries do not write the home save");
        var payload = Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("Home/output-addition-rules").text);
        var exp = payload["rows"].First(row => (int)row["OutputAdditionPropType"] == (int)OutputAdditionPropType.HangUpExpAddPercent
            && (int)row["OutputAdditionPropVal"] == 10000);
        var capacity = payload["rows"].First(row => (int)row["OutputAdditionPropType"] == (int)OutputAdditionPropType.HangUpExpMaxStore
            && (int)row["OutputAdditionPropVal"] == 10000);
        var action = (OutputActionType)(int)exp["OutputActionType"];
        var group = (OutputGroupType)(int)exp["OutputGroupType"];
        int expId = (int)exp["ClassId"], capacityId = (int)capacity["ClassId"];
        var record = new PlayerOutputAdditionDataWrap {
            Id = 1, OutputActionType = action, StartTime = now.AddHours(-1), EndTime = now.AddHours(1),
            OutputAdditionPropDict = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> {
                { OutputAdditionPropType.HangUpExpAddPercent, new PlayerOutputAdditionPropTypeDataWrap {
                    ClassId = expId, StartTime = now.AddMinutes(1), EndTime = now.AddMinutes(30) } },
                { OutputAdditionPropType.HangUpExpMaxStore, new PlayerOutputAdditionPropTypeDataWrap {
                    ClassId = capacityId, StartTime = now.AddMinutes(1), EndTime = now.AddMinutes(30) } }
            }
        };
        int notifications = 0; source.Changed += () => notifications++;
        Require(source.ApplyRecord(record, "output-addition:create") && notifications == 1,
            "activity owner commits its real record and notifies once");
        Require(!source.ApplyRecord(record, "output-addition:create") && notifications == 1,
            "replayed activity receipt does not write or notify again");
        Require(source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 10000 && source.Groups(group).Count == 0,
            "original property query ignores child interval while display excludes future child");
        Require(source.ExtraCount(1, 123.5) == 123 && source.ExtraCapacity(1, 16777217) == 16777216
            && source.ExtraCount(1, 0) == 0 && source.ExtraCapacity(1, -1) == 0
            && source.ExtraCount(999, 123) == 0 && source.ExtraCapacity(999, 123) == 0,
            "original selected hang-up mappings, float capacity cast and truncation are retained");
        current = now.AddMinutes(1);
        Require(source.Groups(group).Count == 1 && source.Groups(group)[0].propList.Count == 2
            && source.ActionClassIds(action).Count == 2 && source.ActiveActions().SequenceEqual(new[] { action }),
            "child start is inclusive and original group/action order is retained");
        disabled.Add(expId); disabled.Add(capacityId);
        Require(source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 0 && source.Groups(group).Count == 0
            && source.ActionClassIds(action).Count == 0 && source.ActiveActions().Count == 0,
            "individual original function gates filter all queries");
        disabled.Clear(); globallyDisabled = true;
        Require(source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 0 && source.Groups(group).Count == 0,
            "global original function gate is respected");
        globallyDisabled = false; current = now.AddMinutes(30);
        Require(source.Groups(group).Count == 0 && source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 10000,
            "child display end is exclusive without changing original property-query behavior");
        current = now.AddHours(1);
        Require(source.Groups(group).Count == 0 && source.ActiveActions().Count == 0
            && source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 10000,
            "original owner-end boundary differs between display and property query");
        current = current.AddTicks(1);
        Require(source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 0, "owner expires after its exact end");
        current = now.AddHours(-1).AddTicks(-1);
        Require(source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 0, "future owner is excluded");
        current = now.AddMinutes(5);
        var second = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerOutputAdditionDataWrap>(
            Newtonsoft.Json.JsonConvert.SerializeObject(record));
        second.Id = 2;
        Require(source.ApplyRecord(second, "output-addition:second")
            && source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 20000
            && source.Groups(group).Count == 2 && source.ActionClassIds(action).Count == 4
            && source.ActiveActions().SequenceEqual(new[] { action, action }),
            "multiple owners add properties and retain duplicate original action entries");
        second.EndTime = now;
        Require(source.ApplyRecord(second, "output-addition:expire-second")
            && source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 10000,
            "owner update replaces the original identity without inventing another activity");
        string beforeInvalid = home.Session.Save.OutputAdditionStateJson;
        second.OutputAdditionPropDict[OutputAdditionPropType.HangUpExpAddPercent].ClassId = int.MaxValue;
        bool rejected = false;
        try { source.ApplyRecord(second, "output-addition:invalid"); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected && home.Session.Save.OutputAdditionStateJson == beforeInvalid,
            "unbound activity configuration leaves the durable records unchanged");
        home.Close(); CloseCapture(); yield return null; Open(); yield return new WaitForSeconds(1);
        source = new OfflineHomeOutputAddition(home.Session, () => current, gate);
        var saved = source.ReadState().Records.Single(value => value.Id == 1);
        Require(saved.Id == record.Id && saved.StartTime == record.StartTime && saved.StartTime.Kind == DateTimeKind.Utc
            && saved.OutputAdditionPropDict[OutputAdditionPropType.HangUpExpAddPercent].ClassId == expId
            && source.PropertyValue(OutputAdditionPropType.HangUpExpAddPercent) == 10000
            && !source.ApplyRecord(record, "output-addition:create"), "real home reopen preserves activity records and receipt");
        Capture("home-output-addition-reopened.png");
        Debug.Log("HOME_OUTPUT_ADDITION_PASSED: actual empty new home save, selected 108 rows, original gates/time discrepancies, ordered groups, float capacity, persisted record and idempotent disk reopen. Native bed composition and activity scheduling remain pending.");
    }
}
