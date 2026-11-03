// Original talent predicates; see partner-talent-source-manifest.json.
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
                public static readonly HashSet<ENodeType> sBridgePoints = new HashSet<ENodeType>
    {
        ENodeType.None,
        ENodeType.Point
    };
        }

public enum ENodeType
{
    None,
    Main,
    Minor,
    Point
}

            public enum EPointState
    {
        ConditionLimit,
        TalentPointLimit,
        CanActive,
        Actived
    }
        public class TalentGraph
{
private readonly PartnerTalentRules configuration;
    private int mNpcId;

    private HashSet<int> mNodes = new HashSet<int>();

    private Dictionary<int, TalentNodeInfo> mNodeId2NodeInfo = new Dictionary<int, TalentNodeInfo>();

    private Dictionary<int, HashSet<TalentNodeInfo>> mAdjacencyList = new Dictionary<int, HashSet<TalentNodeInfo>>();

    public int NpcId => mNpcId;

    public IReadOnlyCollection<int> Nodes => mNodes;

    public int TalentPointItemId => configuration.Get(NpcId).TalentItemId;

    public TalentGraph(PartnerTalentRules configuration, int npcId)
    {
        this.configuration = configuration;
 mNpcId = npcId;
    }

    public IReadOnlyCollection<TalentNodeInfo> GetAdjacencyList(int nodeId)
    {
        if (mAdjacencyList.TryGetValue(nodeId, out var value))
        {
            return value;
        }
        return Array.Empty<TalentNodeInfo>();
    }

    public TalentNodeInfo GetNodeInfo(int nodeId)
    {
        return ValueOrDefault<int, TalentNodeInfo>((IReadOnlyDictionary<int, TalentNodeInfo>)mNodeId2NodeInfo, nodeId, (TalentNodeInfo)null);
    }

    public IReadOnlyList<TalentNodeInfo> GetAllNodeInfo()
    {
        return mNodeId2NodeInfo.Values.ToList();
    }

    public Dictionary<int, long> GetNpcTalentTreeAllCost()
    {
        Dictionary<int, long> dictionary = new Dictionary<int, long>();
        foreach (TalentNodeInfo item in GetAllNodeInfo())
        {
            Dictionary<int, long> costs = item.Costs;
            if (costs == null)
            {
                continue;
            }
            foreach (KeyValuePair<int, long> item2 in costs)
            {
                if (dictionary.TryGetValue(item2.Key, out var value))
                {
                    dictionary[item2.Key] = value + item2.Value;
                }
                else
                {
                    dictionary[item2.Key] = item2.Value;
                }
            }
        }
        return dictionary;
    }

    public long GetAllTalentPointCost()
    {
        int talentItemId = configuration.Get(NpcId).TalentItemId;
        return ValueOrDefault<int, long>((IReadOnlyDictionary<int, long>)GetNpcTalentTreeAllCost(), talentItemId, 0L);
    }

    public Dictionary<int, long> GetNpcTalentTreeCost(IReadOnlyCollection<int> nodeSet)
    {
        Dictionary<int, long> dictionary = new Dictionary<int, long>();
        foreach (int item in nodeSet)
        {
            TalentNodeInfo nodeInfo = GetNodeInfo(item);
            if (nodeInfo == null)
            {
                continue;
            }
            Dictionary<int, long> costs = nodeInfo.Costs;
            if (costs == null)
            {
                continue;
            }
            foreach (KeyValuePair<int, long> item2 in costs)
            {
                if (dictionary.TryGetValue(item2.Key, out var value))
                {
                    dictionary[item2.Key] = value + item2.Value;
                }
                else
                {
                    dictionary[item2.Key] = item2.Value;
                }
            }
        }
        return dictionary;
    }

    public long GetTalentPointCost(IReadOnlyCollection<int> nodeSet)
    {
        int talentItemId = configuration.Get(NpcId).TalentItemId;
        return ValueOrDefault<int, long>((IReadOnlyDictionary<int, long>)GetNpcTalentTreeCost(nodeSet), talentItemId, 0L);
    }

    public TalentNodeInfo GetNodeInfo(int circleIdx, int angleIdx)
    {
        foreach (KeyValuePair<int, TalentNodeInfo> item in mNodeId2NodeInfo)
        {
            TalentNodeInfo value = item.Value;
            if (value.PolarCoordinate[0] == circleIdx && value.PolarCoordinate[1] == angleIdx)
            {
                return value;
            }
        }
        return null;
    }

    public void AddNode(NpcTalentTreeNpcMapInfo nodeMapInfo, ICondition condition, PartnerTalentRules itemInfoParser)
    {
        Dictionary<int, long> dictionary = new Dictionary<int, long>();
        if (nodeMapInfo.CostTalentPointCount > 0)
        {
            dictionary.Add(itemInfoParser.Get(NpcId).TalentItemId, nodeMapInfo.CostTalentPointCount);
        }
        AddNode(new TalentNodeInfo
        {
 configuration = configuration,
            NpcId = nodeMapInfo.NpcClassId,
            NodeId = nodeMapInfo.NodeId,
            NodeType = nodeMapInfo.NodeType,
            Costs = dictionary,
            PolarCoordinate = nodeMapInfo.PolarCoordinate,
            Condition = condition,
            ShowPropType = nodeMapInfo.ShowPropType
        });
    }

    public void AddNode(TalentNodeInfo nodeInfo)
    {
        int nodeId = nodeInfo.NodeId;
        mNodes.Add(nodeId);
        mNodeId2NodeInfo.Add(nodeId, nodeInfo);
    }

    public void AddEdge(int fromNodeId, int toNodeId)
    {
        TalentNodeInfo nodeInfo = GetNodeInfo(fromNodeId);
        TalentNodeInfo nodeInfo2 = GetNodeInfo(toNodeId);
        if (nodeInfo == null || nodeInfo2 == null)
        {
        }
        else if (fromNodeId == toNodeId)
        {
        }
        else
        {
            if (!mAdjacencyList.TryGetValue(fromNodeId, out var value))
            {
                value = new HashSet<TalentNodeInfo>();
                mAdjacencyList[fromNodeId] = value;
            }
            value.Add(nodeInfo2);
            if (!mAdjacencyList.TryGetValue(toNodeId, out var value2))
            {
                value2 = new HashSet<TalentNodeInfo>();
                mAdjacencyList[toNodeId] = value2;
            }
            value2.Add(nodeInfo);
        }
    }

    public void AddEdge(int fromCircleIdx, int fromAngleIdx, int toCircleIdx, int toAngleIdx)
    {
        TalentNodeInfo nodeInfo = GetNodeInfo(fromCircleIdx, fromAngleIdx);
        TalentNodeInfo nodeInfo2 = GetNodeInfo(toCircleIdx, toAngleIdx);
        AddEdge(nodeInfo.NodeId, nodeInfo2.NodeId);
    }

    public int GetDistance(int fromCircleIdx, int fromAngleIdx, int toCircleIdx, int toAngleIdx)
    {
        return Math.Abs(fromCircleIdx - toCircleIdx) + Math.Abs(fromAngleIdx - toAngleIdx);
    }

    public int GetDistance(int fromNodeId, int toNodeId)
    {
        TalentNodeInfo nodeInfo = GetNodeInfo(fromNodeId);
        TalentNodeInfo nodeInfo2 = GetNodeInfo(toNodeId);
        return GetDistance(nodeInfo.PolarCoordinate[0], nodeInfo.PolarCoordinate[1], nodeInfo2.PolarCoordinate[0], nodeInfo2.PolarCoordinate[1]);
    }

    public List<int> GetAdjacencyNormalPointNodeIds(int nodeId)
    {
        List<int> list = new List<int>();
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(nodeId);
        HashSet<int> hashSet = new HashSet<int> { nodeId };
        while (queue.Count != 0)
        {
            int num = queue.Dequeue();
            GetNodeInfo(num);
            if (!mAdjacencyList.TryGetValue(num, out var value))
            {
                continue;
            }
            foreach (TalentNodeInfo item in value)
            {
                int nodeId2 = item.NodeId;
                if (!hashSet.Contains(nodeId2))
                {
                    hashSet.Add(nodeId2);
                    if (item.IsBridgePoint)
                    {
                        queue.Enqueue(nodeId2);
                    }
                    else
                    {
                        list.Add(nodeId2);
                    }
                }
            }
        }
        return list;
    }
}

public class TalentNodeInfo
{
internal PartnerTalentRules configuration;
    public int NpcId;

    public int NodeId;

    public ENodeType NodeType;

    public Dictionary<int, long> Costs;

    public List<int> PolarCoordinate;

    public ICondition Condition;

    public int ShowPropType;

    public TalentGraph Graph => configuration.GetTalentGraphByNpcClassId(NpcId);

    public bool IsBridgePoint => NpcTalentTreeNpcMapInfo.sBridgePoints.Contains(NodeType);

    public bool IsCanActive(Player player, int npcClassId, out bool isItemNotEnough)
    {
        isItemNotEnough = false;
        if (IsBridgePoint)
        {
            return false;
        }
        if (player.GetOrCreateSingleNpcTalentTreeData(npcClassId).UnlockedNodeSet.Contains(NodeId))
        {
            return false;
        }
        DataHub dataHub = new DataHub(player);
        dataHub.Supply(ConditionDataType.NpcTalentTreeNodeId, NodeId);
        dataHub.Supply(ConditionDataType.NpcTalentTreeNpcClassId, NpcId);
        if (!Condition.IsMet(dataHub))
        {
            return false;
        }
        if (Costs.Count != 0 && !player.HasItems(Costs))
        {
            isItemNotEnough = true;
            return false;
        }
        return true;
    }

    public bool IsActived(IReadOnlyCollection<int> activatedNodeSet)
    {
        if (!IsBridgePoint)
        {
            return activatedNodeSet.Contains(NodeId);
        }
        return Graph.GetAdjacencyNormalPointNodeIds(NodeId).Any((int x) => activatedNodeSet.Contains(x));
    }
}


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
                public long GetNpcTalentAllPointCount(int npcClassId)
    {
        int talentItemId = configuration.Get(npcClassId).TalentItemId;
        return this.GetItemAmount(talentItemId);
    }
    public long GetNpcTalentNeedRemainingTalentPoint(int npcClassId)
    {
        long npcTalentAllPointCount = GetNpcTalentAllPointCount(npcClassId);
        TalentGraph talentGraphByNpcClassId = configuration.GetTalentGraphByNpcClassId(npcClassId);
        if (talentGraphByNpcClassId == null)
        {
            return 0L;
        }
        IReadOnlyCollection<int> npcTalentTreeLockedNodeIds = GetNpcTalentTreeLockedNodeIds(npcClassId);
        long talentPointCost = talentGraphByNpcClassId.GetTalentPointCost(npcTalentTreeLockedNodeIds);
        return Math.Max(0L, talentPointCost - npcTalentAllPointCount);
    }
    public IReadOnlyCollection<int> GetNpcTalentTreeLockedNodeIds(int npcClassId)
    {
        TalentGraph talentGraphByNpcClassId = configuration.GetTalentGraphByNpcClassId(npcClassId);
        if (talentGraphByNpcClassId == null)
        {
            return new List<int>();
        }
        IReadOnlyCollection<int> unlockedNodeIds = GetNpcTalentTreeUnlockedNodeIds(npcClassId);
        return talentGraphByNpcClassId.Nodes.Where((int nodeId) => !unlockedNodeIds.Contains(nodeId)).ToList();
    }
    public bool HasItems( Dictionary<int, long> items)
    {
        foreach (KeyValuePair<int, long> item in items)
        {
            if (GetItemAmount(item.Key) < item.Value)
            {
                return false;
            }
        }
        return true;
    }
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
        private class NoneCondition : ICondition {

public NoneCondition(PartnerTalentRules configuration) : base(configuration) {}
    public override bool IsMet(DataHub dataHub)
    {
        return true;
    }
}
private class NpcFriendshipLevelCondition : ICondition {
public int NpcClassId, FriendshipLevel; public bool IsReverse;
public NpcFriendshipLevelCondition(PartnerTalentRules configuration) : base(configuration) {}
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
private class NpcFriendshipMaxLevelCondition : ICondition {
public int NpcClassId; public bool IsReverse;
public NpcFriendshipMaxLevelCondition(PartnerTalentRules configuration) : base(configuration) {}
    public override bool IsMet(DataHub dataHub)
    {
        return GetValue(dataHub);
    }
    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: GetValue(dataHub) ? 1 : 0, target: 1L);
    }
    private bool IsMaxLevel(Player player)
    {
        PlayerNpcDataWrap byNpcClassId = player.NpcManager.GetByNpcClassId(NpcClassId);
        if (byNpcClassId == null)
        {
            return false;
        }
        int maxLevel = configuration.maximumFriendshipLevel(NpcClassId);
        return byNpcClassId.FriendShipLevel >= maxLevel;
    }
    private bool GetValue(DataHub dataHub)
    {
        if (dataHub.TryGetPlayer(out var player))
        {
            if (!IsMaxLevel(player))
            {
                return IsReverse;
            }
            return !IsReverse;
        }
        return false;
    }
}
private class NpcTalentTreeNodePreNodeActiveCountCondition : ICondition {
public int Count;
public NpcTalentTreeNodePreNodeActiveCountCondition(PartnerTalentRules configuration) : base(configuration) {}
    public override bool IsMet(DataHub dataHub)
    {
        (long, long) progress = GetProgress(dataHub);
        return progress.Item1 >= progress.Item2;
    }
    public override (long current, long target) GetProgress(DataHub dataHub)
    {
        return (current: GetValue(dataHub), target: Count);
    }
    protected long GetValue(DataHub dataHub)
    {
        long result = 0L;
        if (!dataHub.TryGetPlayer(out var player))
        {
            return 0L;
        }
        bool flag = dataHub.TryGetData(ConditionDataType.NpcTalentTreeNpcClassId, out long value);
        if (!flag)
        {
            return 0L;
        }
        bool flag2 = dataHub.TryGetData(ConditionDataType.NpcTalentTreeNodeId, out long value2);
        if (!flag2)
        {
            return 0L;
        }
        if (flag2 & flag)
        {
            int npcClassId = (int)value;
            int nodeId = (int)value2;
            List<int> adjacencyNormalPointNodeIds = configuration.GetTalentGraphByNpcClassId(npcClassId).GetAdjacencyNormalPointNodeIds(nodeId);
            IReadOnlyCollection<int> unlockedNodeSet = player.GetNpcTalentTreeUnlockedNodeIds(npcClassId);
            result = adjacencyNormalPointNodeIds.Count((int adjacencyId) => unlockedNodeSet.Contains(adjacencyId));
        }
        return result;
    }
}
            public bool IsTransformFunctionLimit(Player player, int npcId)
    {
        NpcTalentItemInfo npcTalentItemInfo = Get(npcId);
        if (npcTalentItemInfo == null)
        {
            return true;
        }
        ICondition condition = GetCondition(npcTalentItemInfo.TransformGiftUnlockConditionId);
        DataHub dataHub = new DataHub(player);
        return !condition.IsMet(dataHub);
    }
    public bool IsTalentTreeFunctionLimit(Player player, int npcId)
    {
        NpcTalentItemInfo npcTalentItemInfo = Get(npcId);
        if (npcTalentItemInfo == null)
        {
            return true;
        }
        ICondition talentTreeUnlockCondition = npcTalentItemInfo.TalentTreeUnlockCondition;
        if (talentTreeUnlockCondition == null)
        {
            return false;
        }
        DataHub dataHub = new DataHub(player);
        return !talentTreeUnlockCondition.IsMet(dataHub);
    }
    private (long TalentPoint, long TalentExp) TransExpToTalentPoint(long exp)
    {
        if (exp < 0)
        {
            return (TalentPoint: 0L, TalentExp: 0L);
        }
        int talentExp2TalentPointRatio = settings.TalentExp2TalentPointRatio;
        long item = exp / talentExp2TalentPointRatio;
        long item2 = exp % talentExp2TalentPointRatio;
        return (TalentPoint: item, TalentExp: item2);
    }
    private long TransformGiftToTalentExp(Player player, int npcClassId, IReadOnlyDictionary<int, long> gifts)
    {
        long num = 0L;
        NpcTalentTreeSetting npcTalentTreeSetting = settings;
        foreach (KeyValuePair<int, long> gift in gifts)
        {
            int key = gift.Key;
            int num2 = (int)gift.Value;
            int quality = giftItems[key].Quality;
            long valueOrDefault = ValueOrDefault<int, long>((IReadOnlyDictionary<int, long>)npcTalentTreeSetting.GiftQuality2TalentExp, quality, 0L);
            num += valueOrDefault * num2;
        }
        return num;
    }
    private (long TalentPoint, long TalentExp) TransformGiftToTalentPoint(Player player, int npcClassId, IReadOnlyDictionary<int, long> gifts)
    {
        return TransExpToTalentPoint(player.GetOrCreateSingleNpcTalentTreeData(npcClassId).TalentExp + player.TransformGiftToTalentExp(npcClassId, gifts));
    }
    private bool IsNpcTalentPointCanActive(Player player, int npcClassId, int nodeId, out bool isItemNotEnough)
    {
        isItemNotEnough = false;
        TalentGraph talentGraphByNpcClassId = GetTalentGraphByNpcClassId(npcClassId);
        if (talentGraphByNpcClassId == null)
        {
            return false;
        }
        return talentGraphByNpcClassId.GetNodeInfo(nodeId)?.IsCanActive(player, npcClassId, out isItemNotEnough) ?? false;
    }
    private bool IsGiftCanTransformTalentPoint(Player player, int npcId)
    {
        if (player.GetNpcTalentNeedRemainingTalentPoint(npcId) <= 0)
        {
            return false;
        }
        _ = settings;
        Dictionary<int, long> gifts = player.FindItemsByType(328).Select((Gift value, int index) => new { value.Id, value.Count }).ToDictionary(x => x.Id, x => x.Count);
        return player.TransformGiftToTalentPoint(npcId, gifts).TalentPoint > 0;
    }
    private EPointState GetPointState(Player player, TalentNodeInfo nodeInfo, IReadOnlyCollection<int> activatedSet)
    {
        int nodeId = nodeInfo.NodeId;
        List<int> adjacencyNormalPointNodeIds = nodeInfo.Graph.GetAdjacencyNormalPointNodeIds(nodeId);
        if (nodeInfo.IsBridgePoint)
        {
            if (adjacencyNormalPointNodeIds.Any((int x) => activatedSet.Contains(x)))
            {
                return EPointState.Actived;
            }
            return EPointState.ConditionLimit;
        }
        if (activatedSet.Contains(nodeId))
        {
            return EPointState.Actived;
        }
        if (nodeInfo.IsCanActive(player, nodeInfo.NpcId, out var isItemNotEnough))
        {
            return EPointState.CanActive;
        }
        if (isItemNotEnough)
        {
            return EPointState.TalentPointLimit;
        }
        return EPointState.ConditionLimit;
    }
    }
}
