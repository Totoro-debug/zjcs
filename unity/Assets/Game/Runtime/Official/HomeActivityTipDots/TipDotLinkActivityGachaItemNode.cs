// Original dynamic link from an activity entry to each special-gacha node.
using System;
using System.Collections.Generic;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeActivityTipDots
{
    public interface IHomeActivityDynamicTipDotNodeManager : ITipDotNodeManager
    {
        Action<TipDotNodeKey> OnAddNode { get; set; }
        Action<TipDotNodeKey> OnReduceNode { get; set; }
        void ConnectNodes(TipDotNodeKey parent, List<TipDotNodeKey> children);
    }

    public interface IHomeActivityGachaLinkServices
    {
        bool HasPlayer { get; }
        IReadOnlyList<int> GetSpecialGachaIds(int activityId);
    }

    [Serializable]
    public class TipDotLinkActivityGachaItemNode : TipDotNodeBase
    {
        public int ActivityId;
        public TipDotGraphNodeSelector mNodeSelector;
        public IHomeActivityGachaLinkServices Services { get; set; }
        public TipDotNodeKey LinkNodeKey => mNodeSelector == null ? default : mNodeSelector.GetTipDotNodeKey();

        public override void Init()
        {
            base.Init();
            if (NodeKey.InstanceInfo is ActivityNodeInstanceInfo info) ActivityId = info.ActivityId;
            InitLinkNode();
        }

        public void InitLinkNode()
        {
            if (!Services.HasPlayer) return;
            IReadOnlyList<int> ids = Services.GetSpecialGachaIds(ActivityId);
            if (ids == null) return;
            var children = new List<TipDotNodeKey>(ids.Count);
            foreach (int id in ids)
                children.Add(new TipDotNodeKey(LinkNodeKey.GraphName, LinkNodeKey.NodeName, new ActivityGachaNodeInstanceInfo(ActivityId, id)));
            (NodeManager as IHomeActivityDynamicTipDotNodeManager)?.ConnectNodes(NodeKey, children);
        }

        public override TipDotNodeBase Clone()
        {
            var node = new TipDotLinkActivityGachaItemNode(); node.Fill(this); return node;
        }
        public void Fill(TipDotNodeBase source)
        {
            var node = source as TipDotLinkActivityGachaItemNode;
            if (node == null) throw new ArgumentException(GetType().Name, nameof(source));
            ActivityId = node.ActivityId;
            mNodeSelector = node.mNodeSelector?.Clone();
        }
    }
}
