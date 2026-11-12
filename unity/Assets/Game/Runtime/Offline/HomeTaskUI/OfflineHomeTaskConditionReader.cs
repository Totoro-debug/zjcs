using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.Progression;
using TaskUI = Zjcs.Official.HomeTaskUI;

namespace Zjcs.Offline.HomeTaskUI
{
    // Original condition display/progress methods, with the Player data reads
    // bound to the existing local profile. No UI objects or save writes here.
    public sealed class OfflineHomeTaskConditionReader
    {
        readonly OfflineMainlineProgressSession profile;
        readonly OfflineHomeMainlineTaskData data;
        readonly Func<int> professionRank, gachaId;
        readonly Func<int, int> clearedLayer;

        // Confirmed offline policy: the server task parameter follows the real
        // highest unlocked profession rank, including subsequent unlocks.
        public OfflineHomeTaskConditionReader(OfflineMainlineProgressSession profile,
            OfflineHomeMainlineTaskData data, Func<int, int> clearedLayer, Func<int> gachaId)
            : this(profile, data, () => profile.Save.Player.MaxProfessionRank, clearedLayer, gachaId) { }

        public OfflineHomeTaskConditionReader(OfflineMainlineProgressSession profile,
            OfflineHomeMainlineTaskData data, Func<int> professionRank,
            Func<int, int> clearedLayer, Func<int> gachaId)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.professionRank = professionRank;
            this.clearedLayer = clearedLayer;
            this.gachaId = gachaId;
        }

        public TaskUI.ICondition Finish(int classId)
            => Create((JObject)data.Task(classId).CopyOriginalRow()["FinishCondition"], data.Task(classId).ProgressKey);
        public TaskUI.ICondition Display(int classId)
            => Create(data.Task(classId).CopyOriginalRow()["DisplayCondition"] as JObject, null);

        TaskUI.ICondition Create(JObject row, string key)
        {
            if (row == null) return null;
            var value = new Value(this, row, key);
            return value.Type == "WorldMapProgressCondition" ? (TaskUI.ICondition)new World(value) : new Condition(value);
        }

        sealed class Value
        {
            readonly OfflineHomeTaskConditionReader owner;
            readonly JObject row;
            readonly string key;
            public Value(OfflineHomeTaskConditionReader owner, JObject row, string key)
            { this.owner = owner; this.row = row; this.key = key; }
            public string Type => (string)row["_type"];
            public int Map => (int)row["MapClassId"];
            public int Region => (int)row["Region"];

            public (long current, long target) Progress()
            {
                var save = owner.profile.Save;
                switch (Type) {
                    case "NoneCondition": return (1, 1);
                    case "TaskSubmitCondition": {
                        var ids = row["TaskClassIds"].Values<int>().ToArray();
                        return (ids.Count(save.Tasks.ClaimedMainlineTaskIds.Contains), ids.Length);
                    }
                    case "MapGameTotalCountCondition": {
                        var count = save.World.BattleCounts.FirstOrDefault(value => value.MapClassId == Map
                            && value.Region == Region && value.Result == (int)row["Result"]);
                        return (count?.Count ?? 0, (int)row["Count"]);
                    }
                    case "WorldMapProgressCondition": {
                        if (Region < 0) throw new NotSupportedException("Whole-map task progress requires its original aggregation host.");
                        // The local region field already stores FormulaHandler's
                        // normalized 0..10000 result, rather than raw map points.
                        var region = save.World.Maps.FirstOrDefault(value => value.MapClassId == Map)
                            ?.Regions.FirstOrDefault(value => value.Region == Region);
                        // Selected Formula.Int2Float and WorldMapProgressCondition:
                        // preserve float division, multiplication and truncation.
                        return ((int)((float)(region?.Progress ?? 0) / 10000f * 100f),
                            (int)((float)(int)row["Progress"] / 10000f * 100f));
                    }
                    case "MapLogicEntityEndByTagCondition":
                        return (save.World.CompletedEntityTags.Contains((string)row["Tag"]) ? 1 : 0, 1);
                    case "MapRegionGoddessCondition":
                        return (save.World.ActivatedGoddessRegions.Any(value => value.MapClassId == Map
                            && value.Region == Region) ? 1 : 0, 1);
                    case "MaxProfessionRankCondition": {
                        if (owner.professionRank == null) throw new InvalidOperationException("Task profession-rank parameter is unbound.");
                        if ((string)row["Op"] != ">=") throw new NotSupportedException("Review the newly selected rank comparison.");
                        // Selected MaxProfessionRankCondition.GetProgress is a
                        // boolean 0/1; it does not expose rank/target rank.
                        return (owner.professionRank() >= (int)row["Rank"] ? 1 : 0, 1);
                    }
                    case "TotalOpenChestCondition": {
                        int id = (int)row["ChestId"];
                        if (id == 0) id = owner.gachaId?.Invoke()
                            ?? throw new InvalidOperationException("Original GachaId is unbound.");
                        var count = save.World.OpenChestCounts.FirstOrDefault(value => value.ChestId == id);
                        return (count?.Count ?? 0, (int)row["Count"]);
                    }
                    case "DemonBattleLayerPassCondition": {
                        if (owner.clearedLayer == null) throw new InvalidOperationException("Actual tower progress is unbound.");
                        int layer = owner.clearedLayer((int)row["EntryIndex"]);
                        if (layer < 0) throw new InvalidOperationException("Invalid tower progress.");
                        // Selected client override adds one after the original
                        // raw progress read. Description alone clamps the value.
                        return (layer + 1L, (int)row["TargetLayer"] + 1L);
                    }
                    default: throw new NotSupportedException("Original task UI condition is unbound: " + Type);
                }
            }

            public bool Met() { var value = Progress(); return value.current >= value.target; }
            public TaskUI.ConditionDescComponent Description(string metColor, string notMetColor)
            {
                // Selected ICondition.GetDescComponent / ConditionTextUtils.
                var result = new TaskUI.ConditionDescComponent {
                    Key = key ?? "condition_" + Type.Substring(0, Type.Length - "Condition".Length) + "_desc",
                    TextProcessor = new TextProcessor(Met() ? metColor : notMetColor)
                };
                switch (Type) {
                    case "WorldMapProgressCondition": {
                        var value = Progress();
                        result.Args = new object[] { new CultureTextArg($"region_{Map}_{Region}"),
                            $"{value.current}%", $"{value.target}%" };
                        break;
                    }
                    case "MapGameTotalCountCondition":
                    case "TotalOpenChestCondition":
                    case "DemonBattleLayerPassCondition": {
                        var value = Progress();
                        result.Args = new object[] { Math.Min(value.current, value.target), value.target };
                        break;
                    }
                    case "MapLogicEntityEndByTagCondition": {
                        var value = Progress();
                        result.Args = new object[] { value.current, value.target };
                        break;
                    }
                }
                return result;
            }
        }

        sealed class TextProcessor : IWorldTextProcessor
        {
            readonly string color;
            public TextProcessor(string color) { this.color = color; }
            public string Process(string text)
            {
                // Selected TextUnchangedProcessor / ConditionTextProcessor and
                // LanguageUtils.TryReplaceConditionStateText, including null color.
                if (string.IsNullOrEmpty(color)) return text;
                if (Regex.IsMatch(text, "{c}")) return text.Replace("{c}", color);
                return text;
            }
        }

        sealed class Condition : TaskUI.ICondition
        {
            readonly Value value;
            public Condition(Value value) { this.value = value; }
            public override TaskUI.ConditionType Type => TaskUI.ConditionType.Other;
            public override bool IsMet(DataHub data) => value.Met();
            public override bool IsMet(TaskUI.IHomeTaskPlayer player) => value.Met();
            public override (long, long) GetProgress(DataHub data) => value.Progress();
            public override TaskUI.ConditionDescComponent GetDescComponent(DataHub data, object context, string metColor, string notMetColor)
                => value.Description(metColor, notMetColor);
        }
        sealed class World : TaskUI.WorldMapProgressCondition
        {
            readonly Value value;
            public World(Value value) { this.value = value; }
            public override int MapClassId => value.Map;
            public override int Region => value.Region;
            public override TaskUI.ConditionType Type => TaskUI.ConditionType.WorldMapProgress;
            public override bool IsMet(DataHub data) => value.Met();
            public override bool IsMet(TaskUI.IHomeTaskPlayer player) => value.Met();
            public override (long, long) GetProgress(DataHub data) => value.Progress();
            public override TaskUI.ConditionDescComponent GetDescComponent(DataHub data, object context, string metColor, string notMetColor)
                => value.Description(metColor, notMetColor);
        }
    }
}
