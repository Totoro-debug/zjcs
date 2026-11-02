using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.Partners
{
    public interface IPartnerConditionPlayer
    {
        int BaseLevel { get; }
        long GetItemAmount(int classId);
        bool IsMapAccessible(int mapClassId);
        bool RegionIsActived(int mapClassId, int region);
        bool HasNpc(int npcClassId);
    }

    // Configuration/player ports for the selected original condition bodies.
    // This reader covers the NPC meet/show/wait and collection-unlock closure.
    public sealed class PartnerConditionRules
    {
        private readonly NpcInfoParser npcs;
        private readonly ConditionInfoParser conditions;

        public PartnerConditionRules(Func<string, IEnumerable<Dictionary<string, object>>> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            var npcRows = rows("npc").ToList();
            var conditionRows = rows("conditions").ToDictionary(row => Int(row, "ClassId"));
            var needed = new HashSet<int>();
            var visiting = new HashSet<int>();
            Action<Dictionary<string, object>> visit = null;
            visit = node => {
                if (node == null) throw new FormatException("Selected partner condition is absent.");
                string type = Text(node, "_type");
                if (type != "CompositeAndCondition" && type != "CompositeOrCondition") return;
                var ids = Ids(node);
                if (ids == null) return;
                foreach (int id in ids)
                {
                    if (!visiting.Add(id)) throw new FormatException("Cyclic selected partner condition: " + id);
                    if (!conditionRows.TryGetValue(id, out var row))
                        throw new FormatException("Selected partner condition reference is absent: " + id);
                    if (needed.Add(id)) visit(Map(row, "Condition"));
                    visiting.Remove(id);
                }
            };
            foreach (var row in npcRows)
                foreach (string field in new[] { "CanMeetCondition", "CanShowCondition", "WaitMeetCondition" }) visit(Map(row, field));
            foreach (var row in rows("function_limit").Where(row => Int(row, "Type") == 1011 && Int(row, "Key0") == 0 && Int(row, "Key1") == 0))
                visit(Map(row, "Condition"));
            conditions = new ConditionInfoParser(needed.ToDictionary(id => id,
                id => new ConditionInfo { Condition = Create(Map(conditionRows[id], "Condition")) }));
            npcs = new NpcInfoParser(npcRows.ToDictionary(row => Int(row, "Id"), row => new NpcInfo {
                Id = Int(row, "Id"), NpcType = (NpcType)Int(row, "NpcType"), IsAutoMeet = Bool(row, "IsAutoMeet"),
                CanMeetCondition = Create(Map(row, "CanMeetCondition")), CanShowCondition = Create(Map(row, "CanShowCondition")),
                WaitMeetCondition = Create(Map(row, "WaitMeetCondition")) }));
        }

        public bool IsMet(Dictionary<string, object> node, IPartnerConditionPlayer player) => IsMet(node, new DataHub(new Player(player)));
        public bool IsMet(Dictionary<string, object> node, DataHub hub) => Create(node).IsMet(hub);
        public bool IsMet(int id, IPartnerConditionPlayer player) => conditions.GetCondition(id).IsMet(new Player(player));
        public NpcMeetStatus MeetStatus(int npcClassId, IPartnerConditionPlayer player) => GetNpcMeetStatus(new Player(player), npcClassId);
        public bool HasManualMeetNpc(IPartnerConditionPlayer player) => new HasManualMeetNpcCondition(this).IsMet(new Player(player));
        public MeetRequirement? GetMeetRequirement(int npcClassId)
        {
            var requirement = npcs.Get(npcClassId)?.GetHasItemCanMeetCondition();
            return requirement == null ? (MeetRequirement?)null : new MeetRequirement(requirement.ClassId, requirement.Count, requirement.Op);
        }
        public readonly struct MeetRequirement
        {
            public readonly int ClassId, Count;
            public readonly string Op;
            internal MeetRequirement(int classId, int count, string op) { ClassId = classId; Count = count; Op = op; }
        }

        /*GET_MEET_ITEMS*/

        private ICondition Create(Dictionary<string, object> node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            switch (Text(node, "_type"))
            {
                case "NoneCondition": return new NoneCondition(this);
                case "AlwaysCondition": return new AlwaysCondition(this);
                case "LevelCondition": return new LevelCondition(this) { Level = Int(node, "Level"), Op = Text(node, "Op", ">=") };
                case "HasItemCondition": return new HasItemCondition(this) { ClassId = Int(node, "ClassId"), Count = Int(node, "Count", 1), Op = Text(node, "Op", ">=") };
                case "MapUnlockCondition": return new MapUnlockCondition(this) { MapClassId = Int(node, "MapClassId") };
                case "MapRegionActivedCondition": return new MapRegionActivedCondition(this) { MapClassId = Int(node, "MapClassId"), Region = Int(node, "Region") };
                case "HasManualMeetNpcCondition": return new HasManualMeetNpcCondition(this);
                case "CompositeAndCondition": return new CompositeAndCondition(this) { Ids = Ids(node) };
                case "CompositeOrCondition": return new CompositeOrCondition(this) { Ids = Ids(node) };
                default: throw new NotSupportedException("Condition is outside the selected partner closure: " + Text(node, "_type"));
            }
        }
        private static Dictionary<string, object> Map(Dictionary<string, object> row, string key) => row.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;
        private static string Text(Dictionary<string, object> row, string key, string fallback = "") => row.TryGetValue(key, out var value) ? value as string : fallback;
        private static int Int(Dictionary<string, object> row, string key, int fallback = 0) => row.TryGetValue(key, out var value) ? Convert.ToInt32(value) : fallback;
        private static bool Bool(Dictionary<string, object> row, string key) => row.TryGetValue(key, out var value) && value is bool result && result;
        private static List<int> Ids(Dictionary<string, object> row) => row.TryGetValue("Ids", out var value) && value is List<object> ids ? ids.Select(Convert.ToInt32).ToList() : null;

        public sealed class Player
        {
            private readonly IPartnerConditionPlayer source;
            public readonly NpcManager NpcManager;
            public Player(IPartnerConditionPlayer source) { this.source = source ?? throw new ArgumentNullException(nameof(source)); NpcManager = new NpcManager(source); }
            public int BaseLevel => source.BaseLevel;
            public long GetItemAmount(int id) => source.GetItemAmount(id);
            public bool IsMapAccessible(int id) => source.IsMapAccessible(id);
            public bool RegionIsActived(int id, int region) => source.RegionIsActived(id, region);
        }
        public sealed class NpcManager
        {
            private readonly IPartnerConditionPlayer source;
            internal NpcManager(IPartnerConditionPlayer source) { this.source = source; }
            public bool ExistByNpcClassId(int id) => source.HasNpc(id);
        }
        public sealed class PlayerItemDataWrap { public int Id; public long Count; }
        /*DATA_HUB*/
        /*CONDITION_DATA_TYPE*/
        /*CONDITION_TYPE*/
        /*NPC_TYPE*/
        private abstract class ICondition
        {
            protected readonly PartnerConditionRules configuration;
            protected ICondition(PartnerConditionRules configuration) { this.configuration = configuration; }
            public abstract ConditionType Type { get; }
            public virtual bool IsComposite => false;
            public abstract bool IsMet(DataHub hub);
        }
        /*CONDITIONS*/
        private sealed class ConditionInfo { public ICondition Condition; }
        private sealed class ConditionInfoParser
        {
            private readonly Dictionary<int, ConditionInfo> configs;
            public ConditionInfoParser(Dictionary<int, ConditionInfo> configs) { this.configs = configs; }
            /*GET_CONDITION*/
        }
        private sealed class NpcInfoParser
        {
            private readonly Dictionary<int, NpcInfo> configs;
            public NpcInfoParser(Dictionary<int, NpcInfo> configs) { this.configs = configs; }
            public NpcInfo Get(int id) => configs.TryGetValue(id, out var value) ? value : null;
            /*GET_NPCS*/
        }
        private sealed class NpcInfo
        {
            public int Id;
            public NpcType NpcType;
            public bool IsAutoMeet;
            public ICondition CanMeetCondition, CanShowCondition, WaitMeetCondition;
            /*GET_MEET_ITEM*/
        }
        private static class Formula
        {
            /*COMPARE*/
        }
        /*MEET_STATUS*/
    }
}
