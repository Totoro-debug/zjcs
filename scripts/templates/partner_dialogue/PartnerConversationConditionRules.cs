using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.Partners
{
    public interface IPartnerConversationConditionPlayer : IPartnerConditionPlayer
    {
        int GetNpcFriendshipLevel(int npcClassId);
        IEnumerable<PartnerConversationRecord> GetEndDialogues();
        int GetStoryCount(string storyKey);
        int CalculateWorldMapProgress(int mapClassId, int region);
        bool IsRegionGoddessActive(int mapClassId, int region);
    }

    public sealed class PartnerConversationConditionRules
    {
        private readonly PartnerConditionRules baseline;
        private readonly Dictionary<int, Dictionary<string, object>> rows;
        private readonly Dictionary<int, ICondition> conditions = new Dictionary<int, ICondition>();
        private readonly HashSet<int> validated = new HashSet<int>();

        public PartnerConversationConditionRules(PartnerConditionRules baseline,
            IEnumerable<Dictionary<string, object>> conditionRows)
        {
            this.baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            rows = conditionRows.ToDictionary(row => Convert.ToInt32(row["ClassId"]),
                row => (Dictionary<string, object>)row["Condition"]);
        }
        public IPartnerConversationCondition Bind(IReadOnlyDictionary<string, object> node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            var values = node as Dictionary<string, object> ?? node.ToDictionary(pair => pair.Key, pair => pair.Value);
            Validate(values, new HashSet<int>());
            return new Bound(Create(values));
        }
        private void Validate(Dictionary<string, object> node, HashSet<int> visiting)
        {
            Create(node); // Reject unsupported leaf types before using the table.
            var ids = Text(node, "_type") == "LogicalNotCondition" ? new List<int> { Int(node, "Id") } : Ids(node);
            foreach (int id in ids ?? Enumerable.Empty<int>())
            {
                if (!visiting.Add(id)) throw new FormatException("Cyclic original conversation condition: " + id);
                if (!rows.TryGetValue(id, out var child)) throw new FormatException("Missing original conversation condition: " + id);
                if (!validated.Contains(id)) { Validate(child, visiting); validated.Add(id); }
                visiting.Remove(id);
            }
        }
        private ICondition GetCondition(int id)
        {
            if (conditions.TryGetValue(id, out var value)) return value;
            return conditions[id] = rows.TryGetValue(id, out var row) ? Create(row) : null;
        }
        private ICondition Create(Dictionary<string, object> node)
        {
            switch (Text(node, "_type"))
            {
                case "NoneCondition": case "LevelCondition": case "MapRegionActivedCondition":
                    return new Baseline(this, node);
                case "CompositeAndCondition": return new CompositeAndCondition(this) { Ids = Ids(node) };
                case "CompositeOrCondition": return new CompositeOrCondition(this) { Ids = Ids(node) };
                case "LogicalNotCondition": return new LogicalNotCondition(this) { Id = Int(node, "Id") };
                case "NpcFriendshipLevelCondition": return new NpcFriendshipLevelCondition(this) {
                    NpcClassId = Int(node, "NpcClassId"), FriendshipLevel = Int(node, "FriendshipLevel"), IsReverse = Bool(node, "IsReverse") };
                case "NpcDialogueEndCondition": return new NpcDialogueEndCondition(this) {
                    NpcDialogues = node["NpcDialogues"] == null ? null : new HashSet<int>(((List<object>)node["NpcDialogues"]).Select(Convert.ToInt32)) };
                case "StoryCountCondition": return new StoryCountCondition(this) { StoryKey = Text(node, "StoryKey"), Count = Int(node, "Count"), Op = Text(node, "Op") };
                case "WorldMapProgressCondition": return new WorldMapProgressCondition(this) {
                    MapClassId = Int(node, "MapClassId"), Region = Int(node, "Region"), Progress = Int(node, "Progress") };
                case "MapRegionGoddessCondition": return new MapRegionGoddessCondition(this) { MapClassId = Int(node, "MapClassId"), Region = Int(node, "Region") };
                default: throw new NotSupportedException("Outside selected conversation condition closure: " + Text(node, "_type"));
            }
        }
        private sealed class Bound : IPartnerConversationCondition
        {
            private readonly ICondition condition;
            internal Bound(ICondition condition) { this.condition = condition; }
            public bool IsMet(IPartnerConversationPlayer player)
            {
                if (!(player is IPartnerConversationConditionPlayer source))
                    throw new InvalidOperationException("Bind actual conversation player condition data.");
                return condition.IsMet(new DataHub(new Player(source)));
            }
        }
        private sealed class Baseline : ICondition
        {
            private readonly Dictionary<string, object> node;
            internal Baseline(PartnerConversationConditionRules configuration, Dictionary<string, object> node) : base(configuration) { this.node = node; }
            public override bool IsMet(DataHub hub)
            {
                hub.TryGetPlayer(out var player);
                return configuration.baseline.IsMet(node, player.Source);
            }
        }
        private abstract class ICondition
        {
            protected readonly PartnerConversationConditionRules configuration;
            protected ICondition(PartnerConversationConditionRules configuration) { this.configuration = configuration; }
            public abstract bool IsMet(DataHub hub);
            public virtual (long current, long target) GetProgress(DataHub hub) => throw new NotSupportedException("This condition is consumed only through IsMet.");
        }
        private sealed class DataHub
        {
            private readonly Player player;
            internal DataHub(Player player) { this.player = player; }
            internal bool TryGetPlayer(out Player value) { value = player; return value != null; }
            internal bool TryGetData(ConditionDataType type, out object value)
            { value = type == ConditionDataType.Player ? player : null; return value != null; }
        }
        private enum ConditionDataType { Player }
        private sealed class Player
        {
            internal readonly IPartnerConversationConditionPlayer Source;
            internal readonly DialogueManager NpcDialogueManager;
            internal Player(IPartnerConversationConditionPlayer source) { Source = source; NpcDialogueManager = new DialogueManager(source); }
            internal bool TryGetNpcFriendshipLevel(int id, out int level) { level = Source.GetNpcFriendshipLevel(id); return level > 0; }
            internal int GetStoryCount(string key) => Source.GetStoryCount(key);
            internal int CalculateWorldMapProgress(int id, int region) => Source.CalculateWorldMapProgress(id, region);
            internal bool IsRegionGoddessActive(int id, int region) => Source.IsRegionGoddessActive(id, region);
        }
        private sealed class DialogueManager
        {
            private readonly IPartnerConversationConditionPlayer source;
            internal DialogueManager(IPartnerConversationConditionPlayer source) { this.source = source; }
            internal IEnumerable<PartnerConversationRecord> GetEndDialogues() => source.GetEndDialogues();
        }
        private static string Text(Dictionary<string, object> row, string key) => (string)row[key];
        private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
        private static bool Bool(Dictionary<string, object> row, string key) => (bool)row[key];
        private static List<int> Ids(Dictionary<string, object> row) => row.TryGetValue("Ids", out var value) && value != null
            ? ((List<object>)value).Select(Convert.ToInt32).ToList() : null;

        /*CONDITIONS*/
        private static class Formula
        {
            /*COMPARE*/
            /*INT2FLOAT*/
        }
    }
}
