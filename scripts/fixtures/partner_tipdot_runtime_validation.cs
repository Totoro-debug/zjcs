using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

public static class PartnerTipDotRuntimeValidation
{
    public static bool Passed { get; private set; }
    sealed class Subscription : IDisposable
    {
        Action dispose;
        public Subscription(Action dispose) { this.dispose = dispose; }
        public void Dispose() { var action = dispose; dispose = null; action?.Invoke(); }
    }
    sealed class Ban : ITipDotBanStrategy
    {
        public bool Value;
        public int Checks, Clears;
        public bool IsTipDotBan(TipDotNodeKey key) { Checks++; return Value; }
        public void ClearTipDotBan() { Clears++; Value = false; }
    }
    public sealed class Condition : TipDotConditionNodeBase
    {
        public bool Value, Requeue;
        public int Releases;
        public readonly List<int> Frames = new List<int>();
        public override bool CheckShowTip()
        {
            Frames.Add(Time.frameCount);
            if (Requeue) { Requeue = false; SetDirty(); }
            return Value;
        }
        public override void Release() { Releases++; base.Release(); }
        public override TipDotNodeBase Clone() => new Condition();
    }
    // Fixture-only graph topology. The production factory still clones,
    // assigns keys/presets and injects every node before manager.InitNode.
    public sealed class GraphNode : TipDotGraphNode
    {
        public readonly List<GraphNode> Children = new List<GraphNode>();
        public override void InitRelationNode(Dictionary<TipDotGraphNode, TipDotNodeBase> nodes)
        {
            var runtime = nodes[this]; runtime.EnsureParents(); runtime.EnsureChilds();
            foreach (var child in Children)
            { runtime.Childs.Add(nodes[child].NodeKey); nodes[child].EnsureParents(); nodes[child].Parents.Add(runtime.NodeKey); }
        }
    }
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Original tip graph: " + message); }

    public static IEnumerator Check()
    {
        Passed = false;
        bool player = true;
        int loaded = 0, initialized = 0, cleared = 0, showChanged = 0, scopesDisposed = 0, playerDisposed = 0;
        Action playerUpdate = null, scopeRemove = null;
        Action<object> scopeAdd = null;
        var graph = ScriptableObject.CreateInstance<TipDotNodeGraph>(); graph.name = "ValidationNpcGraph";
        var root = ScriptableObject.CreateInstance<GraphNode>(); root.graph = graph;
        root.PresetData.IsRoot = true; root.TipDotNode = new TipDotCommonNode();
        var leaf = ScriptableObject.CreateInstance<GraphNode>(); leaf.graph = graph; leaf.TipDotNodeId = 7;
        leaf.TipDotNode = new Condition(); root.Children.Add(leaf);
        graph.nodes.Add(root); graph.nodes.Add(leaf);
        var definitions = new[] { new PartnerTipDotGraphInfo { GraphName = graph.name,
            GraphType = TipDotGraphType.Dynamic, GraphScope = TipDotGraphScope.Home } };
        var bindings = new PartnerTipDotRuntimeBindings(() => player, () => definitions,
            name => name == graph.name ? TipDotGraphType.Dynamic : (TipDotGraphType?)null,
            (add, remove) => { scopeAdd += add; scopeRemove += remove;
                return new Subscription(() => { scopeAdd -= add; scopeRemove -= remove; scopesDisposed++; }); },
            callback => { playerUpdate += callback;
                return new Subscription(() => { playerUpdate -= callback; playerDisposed++; }); });
        var bansFirst = new Ban(); var bansSecond = new Ban();
        var bans = new TipDotBanManager(new ITipDotBanStrategy[] { null, bansFirst, bansSecond });
        var graphs = new TipDotGraphHandler(name => { loaded++; return name == graph.name ? graph : null; });
        TipDotNodeManager manager = null;
        var factory = new TipDotNodeFactory(graphs.GetNodeGraph, node => { node.NodeManager = manager; node.TipDotBanManager = bans; });
        var repository = new TipDotNodeDataRepository();
        manager = new TipDotNodeManager(repository, factory, graphs, bindings);
        var system = new TipDotSystem(manager, bans, bindings);
        system.OnTipDotSystemInited += () => { Require(manager.Inited && !system.Inited, "original init event order"); initialized++; };
        system.OnTipDotSystemPreClear += () => { Require(manager.Inited && system.Inited, "original preclear event order"); cleared++; };
        system.OnTipDotShowChanged += () => showChanged++;
        system.Initialize(); Require(!manager.Inited, "construction/listener setup does not invent a player event");
        playerUpdate(); playerUpdate();
        Require(initialized == 1 && system.Inited && manager.Inited, "one initialization per player lifetime");
        var info = new NpcNodeInstanceInfo(8402);
        var key = new TipDotNodeKey(graph.name, "0", info);
        var conditionKey = new TipDotNodeKey(graph.name, "7", new NpcNodeInstanceInfo(8402));
        var parent = manager.FindNodeOrCreate(key);
        var condition = (Condition)manager.FindNode(conditionKey);
        Require(parent != null && condition != null && manager.TotalNodeCount == 2 && loaded == 1,
            "factory creates whole graph; equivalent NPC instance keys resolve the same records");
        Require(!parent.IsActive && !condition.IsActive && !parent.IsShowTip, "unlinked graph remains inactive");
        condition.Value = true; condition.SetDirty();
        yield return null; yield return null;
        Require(!parent.IsShowTip, "inactive dirty conditions wait for UI activation");
        int callbacks = 0;
        Action<TipDotStateInfo> listener = state => callbacks++;
        var controller = new TipDotSystemController(manager);
        Require(controller.LinkNodeStateChange(key, listener) && callbacks == 1 && condition.IsActive,
            "link publishes current state immediately and activates descendants");
        yield return null; yield return null;
        Require(parent.IsShowTip && callbacks == 2, "scheduled condition propagates actual aggregate state");
        int checks = condition.Frames.Count;
        condition.Value = false; condition.Requeue = true; condition.SetDirty(); condition.SetDirty();
        yield return null; yield return null; yield return null;
        Require(!parent.IsShowTip && condition.Frames.Count == checks + 2 &&
            condition.Frames[checks] != condition.Frames[checks + 1],
            "dirty set deduplicates; updates dirtied during processing survive to the following frame");
        int warnings = 0;
        Application.LogCallback warning = (message, stack, type) => { if (type == LogType.Warning && message.Contains("Official tip graph")) warnings++; };
        Application.logMessageReceived += warning;
        try { manager.ConnectNode(conditionKey, key); }
        finally { Application.logMessageReceived -= warning; }
        Require(warnings == 1 && !condition.IncludeChildNode(key) && parent.IncludeChildNode(conditionKey),
            "parent traversal rejects and rolls back a dynamic cycle");
        manager.DisconnectNode(key, conditionKey);
        Require(!parent.IncludeChildNode(conditionKey) && !condition.Parents.Contains(key), "disconnect updates both directions");
        manager.ReconnectNodes(key, new List<TipDotNodeKey> { conditionKey });
        Require(repository.GetNodeAllParents(conditionKey).SequenceEqual(new[] { conditionKey, key }), "original parent topology order");
        object collector = new object(); scopeAdd(collector);
        Require(ReferenceEquals(manager.ScopeDataCollector, collector), "scope collector installed before listeners");
        scopeRemove(); Require(manager.ScopeDataCollector == null, "scope collector cleared after listeners");
        bansFirst.Value = true;
        int secondChecks = bansSecond.Checks;
        Require(bans.IsTipDotBan(key) && bansSecond.Checks == secondChecks, "ban strategy order and short circuit");
        system.SetShowTipDot(false); system.SetShowTipDot(false);
        Require(!system.IsShowTipDot && showChanged == 2, "original repeated show-setting notifications");
        controller.UnlinkNodeStateChange(key, listener);
        Require(parent.UiLinkCount == 0 && !parent.IsActive, "last UI unlink deactivates graph");
        var otherKey = new TipDotNodeKey(graph.name, "0", new NpcNodeInstanceInfo(8401));
        manager.FindNodeOrCreate(otherKey);
        Require(manager.TotalNodeCount == 4, "two NPC instances own separate runtime nodes");
        Require(manager.DestoryGraphNode(graph.name, new NpcNodeInstanceInfo(8402)) == 2 && manager.TotalNodeCount == 2 &&
            manager.FindNode(otherKey) != null,
            "destroy removes only the selected graph instance");
        Require(manager.DestoryGraphNode(graph.name, new NpcNodeInstanceInfo(8401)) == 2 && manager.TotalNodeCount == 0,
            "remaining NPC instance owns a separate destruction lifetime");
        manager.FindNodeOrCreate(key);
        Require(loaded == 1, "graph asset lookup cached across instance recreation");
        player = false; playerUpdate(); playerUpdate();
        Require(cleared == 1 && !manager.Inited && manager.TotalNodeCount == 0 && scopesDisposed == 1 &&
            bansFirst.Clears == 2 && bansSecond.Clears == 2, "release order and unconditional ban clearing on absent player");
        player = true; playerUpdate();
        Require(initialized == 2 && manager.Inited, "player replacement supports a fresh graph lifetime");
        player = false; playerUpdate(); system.Dispose(); manager.Dispose();
        Require(playerDisposed == 1 && scopesDisposed == 2, "subscriptions released by owning lifetime");
        Object.Destroy(root); Object.Destroy(leaf); Object.Destroy(graph);
        yield return null;
        Passed = true;
        Debug.Log("Original partner tip graph runtime checks passed");
    }
}
