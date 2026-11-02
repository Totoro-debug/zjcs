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

        private abstract class CompositeCondition : ICondition
{
    public List<int> Ids;

public CompositeCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public List<ICondition> GetConditions()
    {
        List<ICondition> list = new List<ICondition>();
        if (Ids == null)
        {
            return list;
        }
        for (int i = 0; i < Ids.Count; i++)
        {
            ICondition condition = configuration.GetCondition(Ids[i]);
            if (condition == null)
            {
            }
            list.Add(condition);
        }
        return list;
    }
}

private sealed class CompositeAndCondition : CompositeCondition
{
public CompositeAndCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        List<ICondition> conditions = GetConditions();
        for (int i = 0; i < conditions.Count; i++)
        {
            if (!conditions[i].IsMet(dataHub))
            {
                return false;
            }
        }
        return true;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        int num = 0;
        List<ICondition> conditions = GetConditions();
        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i].IsMet(dataHub))
            {
                num++;
            }
        }
        return (current: num, target: conditions.Count);
    }
}

private sealed class CompositeOrCondition : CompositeCondition
{
public CompositeOrCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        foreach (ICondition condition in GetConditions())
        {
            if (condition.IsMet(dataHub))
            {
                return true;
            }
        }
        return false;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        bool flag = false;
        foreach (ICondition condition in GetConditions())
        {
            if (condition.IsMet(dataHub))
            {
                flag = true;
                break;
            }
        }
        if (flag)
        {
            return (current: 1L, target: 1L);
        }
        return (current: 0L, target: 1L);
    }
}

private sealed class LogicalNotCondition : ICondition
{
    public int Id;

public LogicalNotCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        if (!GetCondition().IsMet(dataHub))
        {
            return true;
        }
        return false;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        ICondition condition = GetCondition();
        int num = 1;
        if (condition.IsMet(dataHub))
        {
            num = 0;
        }
        return (current: num, target: 1L);
    }

    private ICondition GetCondition()
    {
        ICondition condition = configuration.GetCondition(Id);
        if (condition == null)
        {
        }
        return condition;
    }
}

private sealed class NpcFriendshipLevelCondition : ICondition
{
    public int NpcClassId;

    public int FriendshipLevel;

    public bool IsReverse;

public NpcFriendshipLevelCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        (long, long) progress = GetProgress(dataHub);
        if (!IsReverse)
        {
            return progress.Item1 >= progress.Item2;
        }
        return progress.Item1 < progress.Item2;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: GetValue(dataHub), target: FriendshipLevel);
    }

    private long GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            player.TryGetNpcFriendshipLevel(NpcClassId, out var level);
            return level;
        }
        return 0L;
    }
}

private sealed class NpcDialogueEndCondition : ICondition
{
    public HashSet<int> NpcDialogues = new HashSet<int>();

public NpcDialogueEndCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        var (num, num2) = GetProgress(dataHub);
        return num >= num2;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: GetValue(dataHub), target: NpcDialogues.Count);
    }

    private int GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetData(ConditionDataType.Player, out object value) && value is Player player)
        {
            int num = 0;
            {
                foreach (PartnerConversationRecord endDialogue in player.NpcDialogueManager.GetEndDialogues())
                {
                    if (NpcDialogues.Contains(endDialogue.DialogueClassId))
                    {
                        num++;
                    }
                }
                return num;
            }
        }
        return 0;
    }
}

private sealed class WorldMapProgressCondition : ICondition
{
    public int MapClassId;

    public int Region;

    public int Progress;

public WorldMapProgressCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        var (num, num2) = GetProgress(dataHub);
        return num >= num2;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: GetValue(dataHub), target: CodeProgress);
    }

    public long GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetData(ConditionDataType.Player, out object value) && value is Player player)
        {
            int mapClassId = MapClassId;
            int num = 0;
            num = player.CalculateWorldMapProgress(mapClassId, CodeRegion);
            return (int)(Formula.Int2Float(num) * 100f);
        }
        return 0L;
    }

    public int CodeProgress
    {
        get
        {
            if (_CodeProgress <= 0)
            {
                _CodeProgress = (int)(Formula.Int2Float(Progress) * 100f);
            }
            return _CodeProgress;
        }
    }

public int CodeRegion => Region;

private int _CodeProgress;
}

private sealed class StoryCountCondition : ICondition
{
    public string StoryKey;

    public int Count;

    public string Op = "==";

public StoryCountCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            return Formula.Compare(player.GetStoryCount(StoryKey), Op, Count);
        }
        return false;
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        dataHub.TryGetPlayer(out var player);
        return (current: player.GetStoryCount(StoryKey), target: Count);
    }
}

private sealed class MapRegionGoddessCondition : ICondition
{
    public int MapClassId;

    public int Region;

public MapRegionGoddessCondition(PartnerConversationConditionRules configuration) : base(configuration) { }

    public override bool IsMet(DataHub dataHub)
    {
        return IsGoddessActive(dataHub);
    }

    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: IsGoddessActive(dataHub) ? 1 : 0, target: 1L);
    }

    private bool IsGoddessActive(DataHub dataHub)
    {

        dataHub.TryGetPlayer(out var player);
        return player.IsRegionGoddessActive(MapClassId, CodeRegion);
    }

public int CodeRegion => Region;
}
        private static class Formula
        {
                public static bool Compare<T>(T lhs, string op, T rhs) where T : IComparable<T>, IEquatable<T>
    {
        return op switch
        {
            ">" => lhs.CompareTo(rhs) > 0,
            "<" => lhs.CompareTo(rhs) < 0,
            "=" => lhs.Equals(rhs),
            ">=" => lhs.CompareTo(rhs) >= 0,
            "<=" => lhs.CompareTo(rhs) <= 0,
            "!=" => !lhs.Equals(rhs),
            _ => lhs.CompareTo(rhs) >= 0,
        };
    }
                public static float Int2Float(int val)
    {
        return (float)val / 10000f;
    }
        }
    }
}
