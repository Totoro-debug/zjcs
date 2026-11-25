using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.HomeShopNewMark
{
    public static class NewFunctionTipValidation
    {
        sealed class Manager : ITipDotNodeManager
        {
            public Action OnAddScopeDataCollector { get; set; }
            public Action OnRemoveScopeDataCollector { get; set; }
            public readonly HashSet<TipDotConditionNodeBase> Dirty = new HashSet<TipDotConditionNodeBase>();
            public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => null;
            public TipDotNodeBase FindNode(TipDotNodeKey key) => null;
            public void AddDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Add(node);
            public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Remove(node);
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var state = new HomeNewFunctionState();
            var manager = new Manager();
            var node = new TipDotFunctionTypeHasNewConditionNode { Services = state, NodeManager = manager,
                NodeKey = new TipDotNodeKey("FortuneTree", "PetShop", null),
                Types = new List<int> { 18 }, Arg1List = new List<int> { 7 } };
            node.Init(); node.IsActive = true;
            state.AddNewFunction(18, 6);
            Require(!node.CheckShowTip() && manager.Dirty.Count == 0, "An unrelated shop must not refresh this tip.");
            state.AddNewFunction(18, 7);
            Require(node.CheckShowTip() && manager.Dirty.Contains(node), "The real state must notify the matching shop tip.");
            manager.Dirty.Clear(); state.RemoveNewFunction(18, 7);
            Require(!node.CheckShowTip() && manager.Dirty.Contains(node), "Removal must clear and dirty the matching tip.");
            var clone = (TipDotFunctionTypeHasNewConditionNode)node.Clone();
            clone.Types.Clear(); clone.Arg1List.Clear();
            Require(node.Types.Count == 1 && node.Arg1List.Count == 1 && clone.Services == null, "Clone must isolate its lists and leave services unbound.");
            node.Release(); manager.Dirty.Clear(); state.AddNewFunction(18, 7);
            Require(manager.Dirty.Count == 0, "Release must detach the real state event listeners.");

            var wildcard = new TipDotFunctionTypeHasNewConditionNode { Services = state, NodeManager = manager,
                NodeKey = new TipDotNodeKey("FortuneTree", "AnyShop", null), Types = new List<int> { 18 }, Arg1List = new List<int>() };
            wildcard.Init(); wildcard.IsActive = true;
            Require(wildcard.CheckShowTip(), "A missing argument selects any new function of that type.");
            manager.Dirty.Clear(); state.AddNewFunction(18, 8);
            Require(manager.Dirty.Contains(wildcard), "A zero argument listens to every argument of its type.");
            wildcard.Release();

            var map = new MapNodeInstanceInfo(7);
            var mapped = new TipDotFunctionTypeHasNewConditionNode { Services = state, NodeManager = manager,
                NodeKey = new TipDotNodeKey("Map", "New", map), Types = new List<int> { 18 }, Arg1List = new List<int> { 9, 10 } };
            mapped.Init();
            Require(mapped.Arg1List.Count == 1 && mapped.Arg1List[0] == 7 && mapped.CheckShowTip()
                && map.Equals(new MapNodeInstanceInfo(7)) && map.GetInstanceDesc() == "Mainland_7",
                "Map instances must replace arguments and preserve original identity and language key.");
            mapped.Release();
            Debug.Log("HOME_NEW_FUNCTION_TIP_VALIDATION_PASSED");
        }
    }
}
