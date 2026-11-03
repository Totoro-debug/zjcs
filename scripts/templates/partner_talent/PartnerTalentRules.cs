using System;
using System.Collections.Generic;
using System.Linq;
using ConditionDataType = Zjcs.Official.Partners.PartnerConditionRules.ConditionDataType;

namespace Zjcs.Official.Partners
{
    public interface IPartnerTalentPlayer
    {
        long GetItemAmount(int id);
        bool TryGetNpcFriendshipLevel(int id, out int level);
        IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int id);
        long GetNpcTalentExp(int id);
        IEnumerable<KeyValuePair<int, long>> GetNpcGifts();
    }

    // Read-only projection of the original talent graph and client predicates.
    // State ownership and committing an activation/conversion belong to the save adapter.
    public sealed class PartnerTalentRules
    {
        private readonly Dictionary<int, TalentGraph> graphs = new Dictionary<int, TalentGraph>();
        private readonly Dictionary<int, NpcTalentItemInfo> items;
        private readonly Dictionary<int, ItemInfo> giftItems;
        private readonly Dictionary<int, ICondition> conditions;
        private readonly Func<int, int> maximumFriendshipLevel;
        private readonly NpcTalentTreeSetting settings;

        public PartnerTalentRules(Func<string, IEnumerable<Dictionary<string, object>>> rows,
            Func<int, int> maximumFriendshipLevel)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            this.maximumFriendshipLevel = maximumFriendshipLevel ?? throw new ArgumentNullException(nameof(maximumFriendshipLevel));
            items = rows("npc_talent_item").ToDictionary(row => Int(row, "NpcClassId"), row => new NpcTalentItemInfo(this) {
                NpcClassId = Int(row, "NpcClassId"), TalentItemId = Int(row, "TalentItemId"), TalentExpItemId = Int(row, "TalentExpItemId"),
                TalentTreeUnlockConditionId = Int(row, "TalentTreeUnlockConditionId"), TransformGiftUnlockConditionId = Int(row, "TransformGiftUnlockConditionId") });
            giftItems = rows("item").ToDictionary(row => Int(row, "ClassId"), row => new ItemInfo { Quality = Int(row, "Quality") });
            var needed = new HashSet<int>(items.Values.SelectMany(item => new[] { item.TalentTreeUnlockConditionId, item.TransformGiftUnlockConditionId }));
            var nodes = rows("npc_talent_tree_npc_map").ToList();
            needed.UnionWith(nodes.Select(row => Int(row, "UnlockCondition")));
            conditions = rows("conditions").Where(row => needed.Contains(Int(row, "ClassId"))).ToDictionary(
                row => Int(row, "ClassId"), row => CreateCondition((Dictionary<string, object>)row["Condition"]));
            if (needed.Any(id => !conditions.ContainsKey(id))) throw new FormatException("Missing original talent condition.");
            var settingsRow = (Dictionary<string, object>)rows("game_settings").Single()["NpcTalentTreeSetting"];
            settings = new NpcTalentTreeSetting {
                TalentExp2TalentPointRatio = Int(settingsRow, "TalentExp2TalentPointRatio"),
                GiftQuality2TalentExp = ((Dictionary<string, object>)settingsRow["GiftQuality2TalentExp"]).ToDictionary(
                    pair => int.Parse(pair.Key, System.Globalization.CultureInfo.InvariantCulture), pair => Convert.ToInt64(pair.Value)) };
            // NpcTalentTreeNpcMapInfoParser.PostParse builds all nodes first, then all edges.
            foreach (var row in nodes)
            {
                int id = Int(row, "NpcClassId");
                if (!graphs.TryGetValue(id, out var graph)) graphs.Add(id, graph = new TalentGraph(this, id));
                graph.AddNode(new NpcTalentTreeNpcMapInfo {
                    NpcClassId = id, NodeId = Int(row, "NodeId"), NodeType = (ENodeType)Int(row, "NodeType"),
                    CostTalentPointCount = Convert.ToInt64(row["CostTalentPointCount"]), ShowPropType = Int(row, "ShowPropType"),
                    PolarCoordinate = ((List<object>)row["PolarCoordinate"]).Select(Convert.ToInt32).ToList()
                }, GetCondition(Int(row, "UnlockCondition")), this);
            }
            foreach (var row in nodes)
                foreach (int target in row["ConnectNodes"] == null ? Enumerable.Empty<int>() : ((List<object>)row["ConnectNodes"]).Select(Convert.ToInt32))
                    graphs[Int(row, "NpcClassId")].AddEdge(Int(row, "NodeId"), target);
        }

        public IReadOnlyDictionary<int, TalentGraph> Graphs => graphs;
        public TalentGraph GetTalentGraphByNpcClassId(int id) => graphs.TryGetValue(id, out var graph) ? graph : null;
        public NpcTalentItemInfo Get(int id) => items.TryGetValue(id, out var item) ? item : null;
        public bool IsTalentTreeFunctionLimited(IPartnerTalentPlayer player, int id) => IsTalentTreeFunctionLimit(new Player(this, player), id);
        public bool IsTransformFunctionLimited(IPartnerTalentPlayer player, int id) => IsTransformFunctionLimit(new Player(this, player), id);
        public bool CanTransformGift(IPartnerTalentPlayer player, int id) => IsGiftCanTransformTalentPoint(new Player(this, player), id);
        public EPointState GetPointState(IPartnerTalentPlayer player, TalentNodeInfo node, IReadOnlyCollection<int> activated) => GetPointState(new Player(this, player), node, activated);
        public bool CanActivate(IPartnerTalentPlayer player, int id, int node, out bool itemsMissing) => IsNpcTalentPointCanActive(new Player(this, player), id, node, out itemsMissing);
        public (long TalentPoint, long TalentExp) TransformGift(IPartnerTalentPlayer player, int id, IReadOnlyDictionary<int, long> gifts) => TransformGiftToTalentPoint(new Player(this, player), id, gifts);
        public long GetRemainingPoints(IPartnerTalentPlayer player, int id) => new Player(this, player).GetNpcTalentNeedRemainingTalentPoint(id);
        public bool IsConditionMet(int id, IPartnerTalentPlayer player, int npcId = 0, int nodeId = 0) => GetCondition(id).IsMet(new DataHub(new Player(this, player))
            .Supply(ConditionDataType.NpcTalentTreeNpcClassId, npcId).Supply(ConditionDataType.NpcTalentTreeNodeId, nodeId));

        private ICondition GetCondition(int id) => conditions.TryGetValue(id, out var condition) ? condition : null;
        private ICondition CreateCondition(Dictionary<string, object> row)
        {
            switch ((string)row["_type"])
            {
                case "NoneCondition": return new NoneCondition(this);
                case "NpcFriendshipLevelCondition": return new NpcFriendshipLevelCondition(this) {
                    NpcClassId = Int(row, "NpcClassId"), FriendshipLevel = Int(row, "FriendshipLevel"), IsReverse = (bool)row["IsReverse"] };
                case "NpcFriendshipMaxLevelCondition": return new NpcFriendshipMaxLevelCondition(this) {
                    NpcClassId = Int(row, "NpcClassId"), IsReverse = (bool)row["IsReverse"] };
                case "NpcTalentTreeNodePreNodeActiveCountCondition": return new NpcTalentTreeNodePreNodeActiveCountCondition(this) { Count = Int(row, "Count") };
                default: throw new NotSupportedException("Outside original talent predicate closure: " + row["_type"]);
            }
        }
        private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
        private static TValue ValueOrDefault<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> values, TKey key, TValue fallback) => values.TryGetValue(key, out var value) ? value : fallback;

        public sealed class NpcTalentItemInfo
        {
            private readonly PartnerTalentRules configuration;
            internal NpcTalentItemInfo(PartnerTalentRules configuration) { this.configuration = configuration; }
            public int NpcClassId, TalentItemId, TalentExpItemId, TalentTreeUnlockConditionId, TransformGiftUnlockConditionId;
            public ICondition TalentTreeUnlockCondition => configuration.GetCondition(TalentTreeUnlockConditionId);
        }
        private sealed class NpcTalentTreeSetting { public int TalentExp2TalentPointRatio; public Dictionary<int, long> GiftQuality2TalentExp; }
        private sealed class ItemInfo { public int Quality; }
        public sealed class NpcTalentTreeNpcMapInfo
        {
            public int NpcClassId, NodeId, ShowPropType;
            public long CostTalentPointCount;
            public ENodeType NodeType;
            public List<int> PolarCoordinate;
            /*BRIDGE_POINTS*/
        }
        /*NODE_ENUM*/
        /*POINT_ENUM*/
        /*GRAPH*/

        // A live read-only view, not a second inventory or a separate save.
        public sealed class Player
        {
            private readonly PartnerTalentRules configuration;
            private readonly IPartnerTalentPlayer source;
            internal Player(PartnerTalentRules configuration, IPartnerTalentPlayer source)
            { this.configuration = configuration; this.source = source ?? throw new ArgumentNullException(nameof(source)); NpcManager = new NpcManager(source); }
            public readonly NpcManager NpcManager;
            public long GetItemAmount(int id) => source.GetItemAmount(id);
            public bool TryGetNpcFriendshipLevel(int id, out int level) => source.TryGetNpcFriendshipLevel(id, out level);
            public IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int id) => source.GetNpcTalentTreeUnlockedNodeIds(id);
            public TalentData GetOrCreateSingleNpcTalentTreeData(int id) => new TalentData { TalentExp = source.GetNpcTalentExp(id), UnlockedNodeSet = source.GetNpcTalentTreeUnlockedNodeIds(id) };
            public IEnumerable<Gift> FindItemsByType(int type) => source.GetNpcGifts().Select(pair => new Gift { Id = pair.Key, Count = pair.Value });
            public (long TalentPoint, long TalentExp) TransformGiftToTalentPoint(int id, IReadOnlyDictionary<int, long> gifts) => configuration.TransformGiftToTalentPoint(this, id, gifts);
            public long TransformGiftToTalentExp(int id, IReadOnlyDictionary<int, long> gifts) => configuration.TransformGiftToTalentExp(this, id, gifts);
            /*PLAYER_METHODS*/
        }
        public sealed class TalentData { public long TalentExp; public IReadOnlyCollection<int> UnlockedNodeSet; }
        public sealed class Gift { public int Id; public long Count; }
        public sealed class NpcManager
        {
            private readonly IPartnerTalentPlayer source;
            internal NpcManager(IPartnerTalentPlayer source) { this.source = source; }
            public PlayerNpcDataWrap GetByNpcClassId(int id) => source.TryGetNpcFriendshipLevel(id, out int level) ? new PlayerNpcDataWrap { FriendShipLevel = level } : null;
        }
        public sealed class PlayerNpcDataWrap { public int FriendShipLevel; }
        public abstract class ICondition
        {
            protected readonly PartnerTalentRules configuration;
            protected ICondition(PartnerTalentRules configuration) { this.configuration = configuration; }
            public abstract bool IsMet(DataHub hub);
            public virtual (long current, long target) GetProgress(DataHub hub) => throw new NotSupportedException();
        }
        public sealed class DataHub
        {
            private readonly Player player;
            private readonly Dictionary<ConditionDataType, long> data = new Dictionary<ConditionDataType, long>();
            public DataHub(Player player) { this.player = player; }
            public DataHub Supply(ConditionDataType type, long value) { data[type] = value; return this; }
            public bool TryGetPlayer(out Player value) { value = player; return value != null; }
            public bool TryGetData(ConditionDataType type, out long value) => data.TryGetValue(type, out value);
        }
        /*CONDITIONS*/
        /*METHODS*/
    }
}
