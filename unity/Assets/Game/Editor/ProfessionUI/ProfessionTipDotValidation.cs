using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;
using UnityEngine;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionTipDotValidation
    {
        private sealed class Manager : ITipDotNodeManager, ITipDotBanManager
        {
            public Action OnAddScopeDataCollector { get; set; }
            public Action OnRemoveScopeDataCollector { get; set; }
            public readonly Dictionary<TipDotNodeKey, TipDotNodeBase> Nodes = new Dictionary<TipDotNodeKey, TipDotNodeBase>();
            public readonly HashSet<TipDotConditionNodeBase> Dirty = new HashSet<TipDotConditionNodeBase>();
            public bool Ban;
            public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => FindNode(key);
            public TipDotNodeBase FindNode(TipDotNodeKey key) => Nodes.TryGetValue(key, out var node) ? node : null;
            public void AddDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Add(node);
            public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) => Dirty.Remove(node);
            public bool IsTipDotBan(TipDotNodeKey key) => Ban;
            public void ClearTipDotBan() => Ban = false;
            public T Add<T>(string name, T node) where T : TipDotNodeBase
            {
                node.NodeKey = new TipDotNodeKey("ProfessionValidation", name, null);
                node.PresetData = new TipDotNodePresetData { PresetTipLevel = ETipDotLevelType.None };
                node.NodeManager = this;
                node.TipDotBanManager = this;
                Nodes.Add(node.NodeKey, node);
                return node;
            }
        }

        private sealed class Condition : TipDotConditionNodeBase
        {
            public bool Value;
            public string Data = "";
            public int Checks;
            public override bool CheckShowTip() { Checks++; TipDotInfo.Data = Data; return Value; }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        public static void Run()
        {
            VerifySafeAction();
            VerifyAggregation();
            VerifyActivationAndConditions();
            VerifyKeysAndClones();
            Debug.Log("Profession original tip-dot core validation passed: priority/level ordering, ignored levels, all-child gating, ban, unavailable child, UI activation, dirty/force updates, reverse, data refresh, graph cleanup, instance keys, independent clones and reentrant deduplicated callbacks.");
        }

        private static void VerifySafeAction()
        {
            var safe = new SafeAction();
            var count = 0;
            Action once = () => count++;
            safe += once;
            safe += once;
            safe.Invoke();
            Require(count == 1, "Original SafeAction must deduplicate the same delegate.");
            safe -= once;
            safe.Invoke();
            Require(count == 1, "Removing a duplicated delegate must remove its single registration.");
            var first = 0;
            var added = 0;
            var nested = false;
            safe += () =>
            {
                first++;
                if (!nested)
                {
                    nested = true;
                    safe += () => added++;
                    safe.Invoke();
                }
            };
            safe.Invoke();
            Require(first == 2 && added == 1, "Reentrant callback snapshots changed.");
            var typed = new SafeAction<int>();
            var received = 0;
            Action<int> consumer = value => received += value;
            typed += consumer;
            typed += consumer;
            typed.Invoke(7);
            Require(received == 7, "Typed SafeAction lost deduplication or its argument.");
            var continued = 0;
            safe.ClearListener();
            safe += () => throw new InvalidOperationException("Expected original SafeAction callback failure");
            safe += () => continued++;
            var logging = Debug.unityLogger.logEnabled;
            try { Debug.unityLogger.logEnabled = false; safe.Invoke(); }
            finally { Debug.unityLogger.logEnabled = logging; }
            Require(continued == 1, "One failing listener prevented the other callback.");
        }

        private static void SetState(TipDotNodeBase node, ETipDotLevelType level, string data, int priority)
        {
            node.TipDotInfo = new TipDotStateInfo { TipLevel = level, Data = data };
            node.IsShowTip = level != ETipDotLevelType.None;
            node.PresetData.Priority = priority;
        }

        private static void VerifyAggregation()
        {
            var manager = new Manager();
            var parent = manager.Add("Parent", new TipDotCommonNode());
            var a = manager.Add("A", new TipDotCommonNode());
            var b = manager.Add("B", new TipDotCommonNode());
            parent.Childs = new List<TipDotNodeKey> { a.NodeKey, b.NodeKey };
            SetState(a, ETipDotLevelType.VeryImportant, "A", 2);
            SetState(b, ETipDotLevelType.Normal, "B", 3);
            parent.UpdateNode();
            Require(parent.TipDotInfo.TipLevel == ETipDotLevelType.Normal && parent.TipDotInfo.Data == "B", "Original priority must take precedence over severity.");
            b.PresetData.Priority = 2;
            parent.UpdateNode();
            Require(parent.TipDotInfo.TipLevel == ETipDotLevelType.VeryImportant && parent.TipDotInfo.Data == "A", "Equal priority must use the higher level.");
            parent.PresetData.IgnoreLevels = new List<ETipDotLevelType> { ETipDotLevelType.VeryImportant };
            parent.UpdateNode();
            Require(parent.TipDotInfo.Data == "B", "Ignored child levels still affected the parent.");
            parent.PresetData.IgnoreLevels = null;
            parent.PresetData.PresetTipLevel = ETipDotLevelType.Important;
            parent.UpdateNode();
            Require(parent.TipDotInfo.TipLevel == ETipDotLevelType.Important && parent.TipDotInfo.Data == "A", "The original configured parent severity was lost.");
            parent.PresetData.NeedAllShow = true;
            SetState(b, ETipDotLevelType.None, "", 3);
            parent.UpdateNode();
            Require(!parent.IsShowTip && parent.TipDotInfo.Data == "A", "All-child gate changed the original retained data semantics.");
            manager.Ban = true;
            parent.UpdateNode();
            Require(parent.TipDotInfo.TipLevel == ETipDotLevelType.None && parent.TipDotInfo.Data == "", "Banned parent did not clear both level and data.");
            manager.Ban = false;
            parent.PresetData.NeedAllShow = false;
            parent.UpdateNode();
            var previous = parent.TipDotInfo;
            parent.Childs.Insert(0, new TipDotNodeKey("ProfessionValidation", "Absent", null));
            SetState(a, ETipDotLevelType.None, "changed", 2);
            parent.UpdateNode();
            Require(parent.TipDotInfo.TipLevel == previous.TipLevel && parent.TipDotInfo.Data == previous.Data, "Unavailable child must retain the previous aggregate state.");
        }

        private static void VerifyActivationAndConditions()
        {
            var manager = new Manager();
            var parent = manager.Add("Root", new TipDotCommonNode());
            var condition = manager.Add("Condition", new Condition());
            parent.Childs = new List<TipDotNodeKey> { condition.NodeKey };
            parent.InitNode();
            condition.InitNode();
            Require(condition.Parents.Count == 1 && parent.Childs.Count == 1, "Graph initialization duplicated a relationship.");
            Require(!parent.IsActive && !condition.IsActive && condition.Checks == 1, "Initial inactive condition must evaluate once.");
            condition.Value = true;
            condition.SetDirty();
            condition.UpdateNode();
            Require(manager.Dirty.Count == 0 && condition.Checks == 1, "Inactive dirty conditions must wait until activation.");
            Action<TipDotStateInfo> listener = info => { };
            parent.AddUiStateListener(listener);
            Require(parent.IsActive && condition.IsActive && manager.Dirty.Contains(condition), "UI activation failed to reach a dirty descendant.");
            condition.UpdateNode();
            Require(condition.IsShowTip && parent.IsShowTip && condition.Checks == 2 && condition.Priority == -99, "Activated condition failed to propagate its original state.");
            condition.Data = "100";
            condition.SetDirty();
            condition.UpdateNode();
            Require(parent.TipDotInfo.Data == "100", "A data-only change failed to propagate.");
            parent.RemoveUiStateListener(listener);
            Require(!parent.IsActive && !condition.IsActive && parent.UiLinkCount == 0, "Unlinking the UI left a descendant active.");
            condition.Value = false;
            condition.ForceUpdate();
            condition.UpdateNode();
            Require(!condition.IsShowTip && condition.Checks == 4, "Forced inactive update was skipped.");
            condition.PresetData.IsReverse = true;
            condition.ForceUpdate();
            condition.UpdateNode();
            Require(condition.IsShowTip, "Reverse condition did not invert the predicate.");
            manager.Ban = true;
            condition.ForceUpdate();
            condition.UpdateNode();
            Require(!condition.IsShowTip, "Ban must override reversal.");
            condition.DestroyNode();
            Require(condition.Parents == null && condition.Childs == null && parent.Childs.Count == 0 && !manager.Dirty.Contains(condition), "Node release retained relationships or a dirty registration.");
            parent.DestroyNode();
        }

        private sealed class Instance : INodeInstanceInfo
        {
            public string GetInstanceDesc() => "instance";
        }

        private static void VerifyKeysAndClones()
        {
            var instance = new Instance();
            var a = new TipDotNodeKey(new string(new[] { 'G' }), "N", instance);
            var b = new TipDotNodeKey("G", "N", instance);
            var c = new TipDotNodeKey("G", "N", new Instance());
            Require(a == b && a.GetHashCode() == b.GetHashCode() && a != c && a.IsValid() && !default(TipDotNodeKey).IsValid(), "Graph instance key equality changed.");
            var original = new TipDotNodePresetData { Priority = 3, IsReverse = true, IgnoreLevels = new List<ETipDotLevelType> { ETipDotLevelType.Normal } };
            var clone = original.Clone();
            clone.IgnoreLevels.Clear();
            Require(original.IgnoreLevels.Count == 1 && clone.Priority == 3 && clone.IsReverse, "Cloned graph preset shares a mutable list.");
        }
    }
}
