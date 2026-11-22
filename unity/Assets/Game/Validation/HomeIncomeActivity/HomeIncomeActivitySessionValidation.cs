#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeOutputAddition;

namespace Zjcs.Editor
{
    public static class HomeIncomeActivitySessionValidation
    {
        [Serializable] sealed class Result { public bool passed; public int checks; public string error; }
        sealed class Configuration { public OutputAdditionInfo[] rows; public HomeOutputAdditionSettings settings; }
        static readonly IReadOnlyDictionary<int, long> Properties = new Dictionary<int, long>();
        static readonly ISet<int> FloatProperties = new HashSet<int>();
        static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        static string root;
        static int checks;
        static DateTimeOffset now;
        static OfflineHomeIncomeSession session;
        static OfflineHomeOutputAddition additions;
        static Configuration configuration;
        static DateTimeOffset At(double seconds) => Epoch.AddTicks((long)(seconds * TimeSpan.TicksPerSecond));

        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !Path.IsPathRooted(workspace)
                || !File.Exists(Path.Combine(workspace, ".zjcs-validation.json")))
                throw new InvalidOperationException("An owned validation workspace is required.");
            root = Path.Combine(workspace, "saves-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            checks = 0;
            var result = new Result();
            try
            {
                configuration = JsonConvert.DeserializeObject<Configuration>(Resources.Load<TextAsset>("Home/output-addition-rules").text);
                CheckIntervalsAndAtomicClaims();
                CheckCapacityAndColdRestore();
                CheckRecordReplacement();
                CheckPropertyBoundary();
                CheckFixedPropertyBoundary();
                CheckItemAcceleration();
                CheckSubsecondsAndLegacy();
                result.passed = true;
                Debug.Log("HOME_INCOME_ACTIVITY_SESSION_PASSED checks=" + checks);
            }
            catch (Exception error) { result.error = error.ToString(); throw; }
            finally
            {
                result.checks = checks;
                File.WriteAllText(Path.Combine(workspace, "income-activity-session-result.json"), JsonUtility.ToJson(result, true));
            }
        }

        static OfflineHomeIncomeDefinition[] Definitions(double hourly = 100, double hours = 24)
            => new[] {
                new OfflineHomeIncomeDefinition { HangUpId = 1, OutputItemId = 50, Unlocked = true,
                    Rule = new HangUpIncomeRule { HourlyOutput = hourly, LevelNum = 1, MaxStoreHours = hours } },
                new OfflineHomeIncomeDefinition { HangUpId = 2, OutputItemId = 2, Unlocked = true,
                    Rule = new HangUpIncomeRule { HourlyOutput = 10, LevelNum = 1, MaxStoreHours = hours } } };

        static string SavePath(string scenario) => Path.Combine(root, scenario, "home.json");
        static void Open(string scenario, double second, OfflineHomeIncomeDefinition[] definitions,
            IReadOnlyDictionary<int, long> properties = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath(scenario)));
            now = At(second);
            session = OfflineHomeIncomeSession.Open(SavePath(scenario), definitions, properties ?? Properties, FloatProperties,
                Array.Empty<OfflineHomeRandomRewardDefinition>(), now.ToUnixTimeSeconds(), restored => {
                    additions = new OfflineHomeOutputAddition(restored, () => now.UtcDateTime, _ => false);
                    restored.SetIncomeAdditionSource(additions.CaptureIncomeSnapshot);
                    restored.Reconfigure(definitions, properties ?? Properties, FloatProperties,
                        Array.Empty<OfflineHomeRandomRewardDefinition>());
                });
        }
        static OutputAdditionInfo Row(int id, bool capacity = false)
        {
            var type = capacity ? configuration.settings.HomeHangUpMaxStoreTypeDict[id]
                : configuration.settings.HomeHangUpAddPercentTypeDict[id];
            return configuration.rows.First(row => row.OutputAdditionPropType == type && row.OutputAdditionPropVal == 10000);
        }
        static PlayerOutputAdditionDataWrap Record(long id, OutputAdditionInfo row, double childStart, double childEnd)
            => new PlayerOutputAdditionDataWrap { Id = id, OutputActionType = row.OutputActionType,
                StartTime = Epoch.UtcDateTime, EndTime = At(36000).UtcDateTime,
                OutputAdditionPropDict = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> {
                    { row.OutputAdditionPropType, new PlayerOutputAdditionPropTypeDataWrap { ClassId = row.ClassId,
                        StartTime = At(childStart).UtcDateTime, EndTime = At(childEnd).UtcDateTime } } } };
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        static long InventoryCount(int id) => session.Save.Inventory.Where(value => value.ItemId == id).Sum(value => value.Count);
        static OfflineHomeIncomeEntry Entry(OfflineHomeSave save, int id)
        {
            var entry = save.Income.FirstOrDefault(value => value.HangUpId == id);
            if (entry == null) save.Income.Add(entry = new OfflineHomeIncomeEntry { HangUpId = id });
            return entry;
        }

        static void CheckIntervalsAndAtomicClaims()
        {
            var catalog = Definitions();
            Open("interval", 0, catalog);
            Require(additions.ApplyRecord(Record(1, Row(1), 3600, 7200), "activity:bed"), "saved bed activity fixture");
            Require(additions.ApplyRecord(Record(2, Row(2), 3600, 7200), "activity:cart"), "saved cart activity fixture");
            string before = JsonUtility.ToJson(session.Save);
            Require(session.PreviewAt(1, At(10800)).Count == 400 && session.PreviewAt(2, At(10800)).Count == 40,
                "actual saved parent/child intervals settle bed and cart independently");
            Require(JsonUtility.ToJson(session.Save) == before, "live preview does not mutate save, inventory or time");
            session.AdvanceTo(At(10800));
            Open("interval", 10800, catalog);
            Require(session.Preview(1).Count == 400 && session.Preview(2).Count == 40, "Unity JSON cold restore retains earned activity amounts");
            var reward = session.CollectReward(1, "claim:bed", At(10800));
            Require(reward?.Count == 400 && reward.ItemId == 50, "ordinary claim returns the committed XP receipt");
            Require(session.Save.Inventory.Single(value => value.ItemId == 50).Count == 400, "ordinary activity output grants inventory once");
            before = JsonUtility.ToJson(session.Save);
            Require(session.CollectReward(1, "claim:bed", At(20000)) == null
                && JsonUtility.ToJson(session.Save) == before, "duplicate claim consumes neither future time nor a second reward");
            Open("failure", 0, catalog);
            Require(additions.ApplyRecord(Record(1, Row(1), 3600, 7200), "activity:bed"), "failed-grant activity fixture");
            Require(session.Mutate("inventory:full", save => {
                save.GrantSerial = 1; save.Inventory.Add(new OfflineHomeItemStack { ItemId = 50, Count = long.MaxValue, Serial = 1 });
            }), "full inventory fixture");
            before = JsonUtility.ToJson(session.Save);
            byte[] disk = File.ReadAllBytes(SavePath("failure"));
            Require(session.CollectReward(1, "claim:rejected", At(10800)) == null,
                "inventory overflow rejects the grant");
            Require(JsonUtility.ToJson(session.Save) == before && File.ReadAllBytes(SavePath("failure")).SequenceEqual(disk),
                "failed grant rolls back time, activity fractions, inventory and receipt in memory and on disk");
            Open("failure", 0, catalog);
            Require(JsonUtility.ToJson(session.Save) == before, "cold reopen retains the complete pre-failure transaction");
            Require(!session.Mutate("corrupt:fraction", save => {
                Entry(save, 1).Activity = new HomeIncomeActivityState { Enabled = true, ExtraFraction = 1 };
            }), "invalid activity fraction is rejected before persistence");
        }

        static void CheckCapacityAndColdRestore()
        {
            var catalog = Definitions(100, 1);
            Open("capacity", 0, catalog);
            Require(additions.ApplyRecord(Record(1, Row(1, true), 0, 5418), "activity:capacity"), "capacity activity fixture");
            session.AdvanceTo(At(10800));
            Open("capacity", 10800, catalog);
            var preview = session.Preview(1);
            Require(preview.Capacity == 100 && preview.Count == 150, "expired capacity retains mature overflow and stops later production");
            Require(session.CollectReward(1, "claim:overflow", At(10800))?.Count == 150,
                "expired-capacity overflow remains manually claimable");
            Require(Math.Abs(session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds - 18) < 1e-6,
                "earned base fraction survives expiry, cold restore and collection");
        }

        static void CheckRecordReplacement()
        {
            var catalog = Definitions();
            Open("replacement", 0, catalog);
            Require(additions.ApplyRecord(Record(1, Row(1), 0, 10800), "activity:first"), "replaceable activity fixture");
            now = At(3600);
            var replacement = Record(1, Row(1), 0, 10800); replacement.OutputAdditionPropDict.Clear();
            Require(additions.ApplyRecord(replacement, "activity:replace"), "actual activity update transaction");
            session.AdvanceTo(At(10800));
            Require(session.Preview(1).Count == 400, "replacing activity data first settles its prior effective interval");
            string before = JsonUtility.ToJson(session.Save);
            now = At(20000);
            Require(!additions.ApplyRecord(replacement, "activity:replace") && JsonUtility.ToJson(session.Save) == before,
                "replayed activity update cannot consume time or a second receipt");
        }

        static void CheckPropertyBoundary()
        {
            Open("properties", 0, Definitions());
            Require(additions.ApplyRecord(Record(1, Row(1), 0, 36000), "activity:properties"), "property-change activity fixture");
            session.Reconfigure(Definitions(200), Properties, FloatProperties, Array.Empty<OfflineHomeRandomRewardDefinition>(), At(3600));
            session.AdvanceTo(At(5400));
            Open("properties", 5400, Definitions(200));
            session.AdvanceTo(At(7200));
            Require(session.Preview(1).Count == 700, "property change retains prior bonus and applies the new original rate only afterward");
        }

        static void CheckSubsecondsAndLegacy()
        {
            var catalog = Definitions(3600);
            Open("precise", 0, catalog);
            Require(additions.ApplyRecord(Record(1, Row(1), 0, 3600), "activity:precise"), "subsecond activity fixture");
            session.AdvanceTo(At(.25));
            Open("precise", .25, catalog);
            Require(session.Save.LastIncomeSubsecond == .25
                && session.Save.Income.Single(value => value.HangUpId == 1).Activity.ExtraFraction == .25,
                "Unity JSON preserves checkpoint and activity fractions exactly");
            Require(session.CollectReward(1, "claim:precise", At(1))?.Count == 2, "precise claim includes base and activity once");
            string before = JsonUtility.ToJson(session.Save);
            session.AdvanceTo(At(.5));
            Require(JsonUtility.ToJson(session.Save) == before, "clock rollback cannot alter a committed activity balance");
            Open("legacy", 0, catalog);
            Require(session.Mutate("legacy:fixture", save => Entry(save, 1).StoredSeconds = .5),
                "legacy fractional income fixture");
            Require(session.CollectReward(1, "claim:legacy", At(.75))?.Count == 1
                && session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds == .25,
                "no-activity save keeps the original fractional-time claim rule");
            // Unity serializes an inline null class as an empty object. Check
            // the activity/earned-value semantics across that native behavior.
            Require(additions.ReadState().Records.Count == 0 && session.Save.Income.All(value => value.Activity == null
                || !value.Activity.Enabled && value.Activity.ExtraCount == 0 && value.Activity.ExtraFraction == 0),
                "empty activity saves retain no active accounting or invented activity records");
        }

        static void CheckFixedPropertyBoundary()
        {
            var definitions = Definitions(); definitions[0].Rule.LevelNum = 2;
            definitions[0].Rule.EffectFixedPropertyId = 1002;
            Open("fixed-properties", 0, definitions, new Dictionary<int, long> { { 1002, 7 } });
            Require(additions.ApplyRecord(Record(1, Row(1), 0, 36000), "activity:fixed-properties"), "fixed-property activity fixture");
            session.Reconfigure(definitions, new Dictionary<int, long> { { 1002, 14 } }, FloatProperties,
                Array.Empty<OfflineHomeRandomRewardDefinition>(), At(3600));
            session.AdvanceTo(At(7200));
            Require(session.Preview(1).Count == 842,
                "fixed property changes retain original ordinary ordering without retroactive activity output");
        }

        static void CheckItemAcceleration()
        {
            var definitions = Definitions();
            Open("item-acceleration", 0, definitions);
            Require(additions.ApplyRecord(Record(1, Row(1), 0, 36000), "activity:items"), "item-acceleration activity fixture");
            session.AdvanceTo(At(3600));
            Require(session.Preview(1).Count == 200, "ordinary production contains its earned activity output before acceleration");
            Require(session.Mutate("crystal:fixture", save => {
                save.GrantSerial++;
                save.Inventory.Add(new OfflineHomeItemStack { ItemId = 9001, Count = 2, Serial = save.GrantSerial });
            }), "isolated crystal inventory fixture");
            Require(session.ApplyFastForwardItems(1, 9001, 2, 2, 1, "accelerate:items") == 200,
                "item acceleration retains its original non-activity reward formula");
            Require(InventoryCount(9001) == 0 && InventoryCount(50) == 0 && session.Preview(1).Count == 400,
                "item acceleration preserves prior activity output in furniture and consumes only its crystal cost");
            Open("item-acceleration", 3600, definitions);
            Require(session.CollectReward(1, "claim:items", At(3600))?.Count == 400,
                "ordinary activity and accelerated output cold-open and collect exactly once");
        }
    }
}
#endif
