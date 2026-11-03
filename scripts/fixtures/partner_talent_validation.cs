using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.Partners;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;

public static class PartnerTalentValidation
{
    sealed class Player : IPartnerTalentPlayer
    {
        public readonly Dictionary<int, long> Items = new Dictionary<int, long>();
        public readonly Dictionary<int, int> Friendship = new Dictionary<int, int>();
        public readonly Dictionary<int, HashSet<int>> Unlocked = new Dictionary<int, HashSet<int>>();
        public readonly Dictionary<int, long> Experience = new Dictionary<int, long>();
        public long GetItemAmount(int id) => Items.TryGetValue(id, out var count) ? count : 0;
        public bool TryGetNpcFriendshipLevel(int id, out int level) => Friendship.TryGetValue(id, out level);
        public IReadOnlyCollection<int> GetNpcTalentTreeUnlockedNodeIds(int id) => Unlocked.TryGetValue(id, out var ids) ? ids : Array.Empty<int>();
        public long GetNpcTalentExp(int id) => Experience.TryGetValue(id, out var count) ? count : 0;
        public IEnumerable<KeyValuePair<int, long>> GetNpcGifts() => Items.Where(pair => pair.Key == 1100 || pair.Key == 1102 || pair.Key == 1104);
    }
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Original partner talent: " + message); }

    public static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var gifts = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var rules = new PartnerTalentRules(name => catalog.Rows(name), gifts.GetMaxLevel);
        var player = new Player();
        Require(rules.Graphs.Count == 3 && rules.Graphs.Values.Sum(graph => graph.Nodes.Count) == 252, "all selected talent graphs");
        Require(rules.GetTalentGraphByNpcClassId(8402) == null && rules.Get(8402) == null &&
            rules.IsTalentTreeFunctionLimited(player, 8402) && rules.IsTransformFunctionLimited(player, 8402), "ordinary partners do not manufacture talent graphs");
        Require(!rules.CanActivate(player, 8402, 1, out bool absentCost) && !absentCost &&
            !rules.CanActivate(player, 10608, -1, out absentCost) && !absentCost, "unknown original graph or node cannot activate");
        int tested = 0;
        foreach (int id in new[] { 10608, 10708, 10801 })
        {
            var graph = rules.GetTalentGraphByNpcClassId(id);
            var item = rules.Get(id);
            Require(graph.Nodes.Count == 84 && graph.GetAllTalentPointCost() == 132 && graph.TalentPointItemId == item.TalentItemId,
                "actual selected graph costs and item identity");
            Require(rules.IsTalentTreeFunctionLimited(player, id) && rules.IsTransformFunctionLimited(player, id), "unmet NPC gates");
            foreach (int level in new[] { 49, 50, 99, 100 })
            {
                player.Friendship[id] = level;
                Require(rules.IsTalentTreeFunctionLimited(player, id) == (level < 50), "friendship 49/50 boundary");
                Require(rules.IsTransformFunctionLimited(player, id) == (level < 100), "global maximum boundary independently of rank cap");
            }
            player.Items.Clear(); player.Unlocked[id] = new HashSet<int>();
            Require(rules.GetRemainingPoints(player, id) == 132, "unspent full-tree cost");
            var first = graph.GetNodeInfo(1); var next = graph.GetNodeInfo(2);
            Require(first.Costs[item.TalentItemId] == 3 && next.Costs[item.TalentItemId] == 1 &&
                graph.GetAdjacencyNormalPointNodeIds(1).SequenceEqual(new[] { 2 }), "actual initial path and distinct costs");
            Require(rules.GetPointState(player, first, player.Unlocked[id]) == PartnerTalentRules.EPointState.TalentPointLimit &&
                rules.GetPointState(player, next, player.Unlocked[id]) == PartnerTalentRules.EPointState.ConditionLimit,
                "affordable root and locked successor are distinct conditions");
            player.Items[item.TalentItemId] = 3;
            Require(rules.CanActivate(player, id, 1, out bool missing) && !missing &&
                rules.GetPointState(player, first, player.Unlocked[id]) == PartnerTalentRules.EPointState.CanActive,
                "root exact cost is activatable");
            player.Unlocked[id].Add(1); player.Items[item.TalentItemId] = 0;
            Require(rules.GetPointState(player, first, player.Unlocked[id]) == PartnerTalentRules.EPointState.Actived &&
                !rules.CanActivate(player, id, 1, out missing) && !missing &&
                rules.GetPointState(player, next, player.Unlocked[id]) == PartnerTalentRules.EPointState.TalentPointLimit,
                "activation unlocks adjacency without letting root spend twice");
            player.Items[item.TalentItemId] = 1;
            Require(rules.CanActivate(player, id, 2, out missing) && !missing && rules.GetRemainingPoints(player, id) == 128,
                "remaining cost subtracts unlocked nodes and current inventory");
            player.Unlocked[id].Clear(); player.Items[item.TalentItemId] = 0;
            foreach (int node in graph.Nodes)
            {
                var current = graph.GetNodeInfo(node);
                foreach (var adjacent in graph.GetAdjacencyList(node))
                    Require(adjacent.Graph == graph && graph.GetAdjacencyList(adjacent.NodeId).Contains(current), "original undirected graph is reciprocal");
                player.Items[item.TalentItemId] = current.Costs[item.TalentItemId];
                player.Unlocked[id].Clear(); player.Unlocked[id].UnionWith(graph.GetAdjacencyNormalPointNodeIds(node));
                Require(rules.CanActivate(player, id, node, out missing) && !missing, "every configured node accepts its original adjacency and exact cost");
                player.Items[item.TalentItemId]--;
                Require(!rules.CanActivate(player, id, node, out missing) && missing, "every configured node rejects one point below cost");
                tested++;
            }
            player.Unlocked[id].Clear(); player.Items.Clear();
            foreach (var sample in new[] { (1104, 60L), (1102, 12L), (1100, 4L) })
            {
                player.Items.Clear(); player.Items[sample.Item1] = sample.Item2 - 1;
                Require(!rules.CanTransformGift(player, id), "gift quality below one point");
                player.Items[sample.Item1]++;
                Require(rules.CanTransformGift(player, id) && rules.TransformGift(player, id, player.Items) == (1L, 0L), "each original gift quality exact threshold");
            }
            player.Items.Clear(); player.Items[1100] = 3; player.Experience[id] = 2400;
            Require(rules.CanTransformGift(player, id) && rules.TransformGift(player, id, player.Items) == (1L, 0L), "NPC-specific retained experience participates");
            player.Experience[id] = 0; player.Items[1100] = 5;
            Require(rules.TransformGift(player, id, player.Items) == (1L, 2400L), "original quotient and remainder");
            player.Items[1100] = 4294967296L;
            Require(rules.TransformGift(player, id, player.Items) == (0L, 0L), "original long quantity cast to int is preserved");
            player.Items[1100] = 2147483648L;
            Require(rules.TransformGift(player, id, player.Items) == (0L, 0L), "negative cast experience follows original clamp");
            player.Items[1100] = 4; player.Unlocked[id].UnionWith(graph.Nodes);
            Require(!rules.CanTransformGift(player, id) && rules.GetRemainingPoints(player, id) == 0, "fully activated tree suppresses conversion hint");
        }
        // The selected 252 rows contain no bridge points. Exercise that original
        // branch separately, without changing any production configuration.
        var bridge = rules.GetTalentGraphByNpcClassId(10608);
        var actual = bridge.GetNodeInfo(2).NodeType;
        try
        {
            bridge.GetNodeInfo(2).NodeType = PartnerTalentRules.ENodeType.Point;
            Require(bridge.GetAdjacencyNormalPointNodeIds(1).SequenceEqual(new[] { 3 }), "bridge traversal reaches the next normal point");
            Require(rules.GetPointState(player, bridge.GetNodeInfo(2), new[] { 1 }) == PartnerTalentRules.EPointState.Actived &&
                rules.GetPointState(player, bridge.GetNodeInfo(2), Array.Empty<int>()) == PartnerTalentRules.EPointState.ConditionLimit,
                "bridge display derives from adjacent activated normal points");
        }
        finally { bridge.GetNodeInfo(2).NodeType = actual; }
        Debug.Log("Original partner talent checks passed: " + tested + " nodes, 3 friendship gates, conversion thresholds, bridge and overflow branches");
    }
}
