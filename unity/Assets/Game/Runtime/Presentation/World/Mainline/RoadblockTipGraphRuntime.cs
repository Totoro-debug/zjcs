using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.World
{
    // Platform inputs for the original ElementDoor graph. Aggregation, priority,
    // reverse/ban handling and condition state transitions use the migrated core.
    internal sealed class RoadblockTipGraphRuntime : ITipDotNodeManager, IDisposable
    {
        [Serializable] internal sealed class Data
        {
            public string manifest;
            public TipDotNodePresetData root, property, stage;
            public int targetState;
        }
        private sealed class Condition : TipDotConditionNodeBase
        {
            public Func<bool> Check;
            public override bool CheckShowTip() => Check();
        }
        private readonly Dictionary<TipDotNodeKey, TipDotNodeBase> nodes = new Dictionary<TipDotNodeKey, TipDotNodeBase>();
        private readonly TipDotCommonNode root;
        private readonly Condition property, stage;
        public Action OnAddScopeDataCollector { get; set; }
        public Action OnRemoveScopeDataCollector { get; set; }
        public RoadblockTipGraphRuntime(TipDotNodeKey key, Func<bool> propertyCheck, Func<int> stageState)
        {
            var data = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("Map14/UI/roadblock-tip-graph").text);
            if (data.manifest != "884_156692") throw new InvalidOperationException("Unverified ElementDoor graph.");
            var propKey = new TipDotNodeKey(key.GraphName, "1", key.InstanceInfo);
            var stageKey = new TipDotNodeKey(key.GraphName, "2", key.InstanceInfo);
            root = new TipDotCommonNode { NodeKey = key, PresetData = data.root,
                Childs = new List<TipDotNodeKey> { propKey, stageKey }, UiLinkCount = 1 };
            // Original TipDotMapStageConPropStandardCondtionNode.CheckShowTip and
            // TipDotMapWorldStageStateCondtionNode.CheckShowTip consume these inputs.
            property = new Condition { NodeKey = propKey, PresetData = data.property, Check = propertyCheck,
                Parents = new List<TipDotNodeKey> { key } };
            stage = new Condition { NodeKey = stageKey, PresetData = data.stage,
                Check = () => stageState() == data.targetState, Parents = new List<TipDotNodeKey> { key } };
            foreach (var node in new TipDotNodeBase[] { root, property, stage }) { node.NodeManager = this; nodes.Add(node.NodeKey, node); }
            foreach (var node in nodes.Values) node.InitNode();
            Refresh();
        }
        public TipDotStateInfo Refresh()
        {
            property.ForceUpdate(); stage.ForceUpdate(); root.UpdateNode();
            return root.TipDotInfo;
        }
        public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key) => FindNode(key);
        public TipDotNodeBase FindNode(TipDotNodeKey key) => nodes.TryGetValue(key, out var value) ? value : null;
        public void AddDirtyConditionNode(TipDotConditionNodeBase node) => node.UpdateNode();
        public void RemoveDirtyConditionNode(TipDotConditionNodeBase node) { }
        public void Dispose() { foreach (var node in nodes.Values.ToArray()) node.DestroyNode(); nodes.Clear(); }
    }
}
