using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.HomeActivityTipDots;
using Zjcs.Official.ProfessionUI;

public static class HomeActivityGachaLinkValidation
{
    private sealed class Manager : IHomeActivityDynamicTipDotNodeManager
    {
        public Action<TipDotNodeKey> OnAddNode { get; set; }
        public Action<TipDotNodeKey> OnReduceNode { get; set; }
        public readonly List<TipDotNodeKey> Children = new List<TipDotNodeKey>();
        public Action OnAddScopeDataCollector { get; set; }
        public Action OnRemoveScopeDataCollector { get; set; }
        public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => null;
        public TipDotNodeBase FindNode(TipDotNodeKey key) => null;
        public void AddDirtyConditionNode(TipDotConditionNodeBase node) { }
        public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) { }
        public void ConnectNodes(TipDotNodeKey parent, List<TipDotNodeKey> children) => Children.AddRange(children);
        public void DisconnectNode(TipDotNodeKey parent, TipDotNodeKey child) => Children.RemoveAll(value => value.Equals(child));
    }
    private sealed class Services : IHomeActivityGachaLinkServices
    {
        public bool HasPlayer { get; set; } = true;
        public readonly IReadOnlyList<int> Ids = new[] { 7, 8, 12 };
        public int Calls;
        public IReadOnlyList<int> GetSpecialGachaIds(int activityId) { Calls++; if (activityId != 9) throw new InvalidOperationException("Wrong activity id"); return Ids; }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        try
        {
            var manager = new Manager(); var services = new Services();
            var node = new TipDotLinkActivityGachaItemNode { NodeManager = manager, Services = services, mNodeSelector = new TipDotGraphNodeSelector() };
            node.NodeKey = new TipDotNodeKey("FortuneTree", "Activity", new ActivityNodeInstanceInfo(9));
            node.Init();
            Require(services.Calls == 1 && manager.Children.Count == 3, "The original node must query and connect every special-gacha child.");
            Require(manager.Children[0].InstanceInfo is ActivityGachaNodeInstanceInfo first && first.ActivityId == 9 && first.SpecailGachaId == 7,
                "The original child key must preserve the activity and special-gacha ids.");
            Require(manager.Children[1].InstanceInfo is ActivityGachaNodeInstanceInfo second && second.SpecailGachaId == 8
                && manager.Children[2].InstanceInfo is ActivityGachaNodeInstanceInfo third && third.SpecailGachaId == 12,
                "Child order must follow the original parser order.");
            var clone = (TipDotLinkActivityGachaItemNode)node.Clone(); Require(clone.ActivityId == node.ActivityId, "Clone must preserve activity id.");
            services.HasPlayer = false; manager.Children.Clear(); node.InitLinkNode(); Require(manager.Children.Count == 0 && services.Calls == 1, "Missing player must skip the original query.");
            Debug.Log("HOME_ACTIVITY_GACHA_LINK_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
