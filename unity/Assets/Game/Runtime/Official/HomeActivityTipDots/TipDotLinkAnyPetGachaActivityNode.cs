// Original dynamic link for the pet-tree activity gacha entry.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeActivityTipDots
{
    public interface IHomePetGachaActivityLinkServices : IHomeActivityGachaLinkServices
    {
        event Action<HomeTipActivity> OnActivityOpen;
        event Action<IEnumerable<HomeTipActivity>> OnActivityDatasUpdate;
        event Action<HomeTipActivity> OnActivityClose;
        bool TryGetPetTreeGachaActivity(out int activityId);
    }

    [Serializable]
    public class TipDotLinkAnyPetGachaActivityNode : TipDotNodeBase
    {
        private int mActivityId;
        private ActivityType mActivityTypeGahca = ActivityType.ActivityGacha;
        public TipDotGraphNodeSelector mNodeSelector;
        public IHomePetGachaActivityLinkServices Services { get; set; }
        public override void Init()
        {
            base.Init();
            Services.OnActivityOpen += OnActivitiesOpen;
            Services.OnActivityDatasUpdate += OnActivityDatasUpdate;
            Services.OnActivityClose += OnActivitiesClose;
            InitLinkNode();
        }
        public override void Release()
        {
            Services.OnActivityOpen -= OnActivitiesOpen;
            Services.OnActivityDatasUpdate -= OnActivityDatasUpdate;
            Services.OnActivityClose -= OnActivitiesClose;
            base.Release();
        }
        public void InitLinkNode()
        {
            if (!Services.HasPlayer || !Services.TryGetPetTreeGachaActivity(out int id)) return;
            mActivityId = id;
            (NodeManager as IHomeActivityDynamicTipDotNodeManager)?.ConnectNodes(NodeKey,
                new List<TipDotNodeKey> { ChildKey(id) });
        }
        private TipDotNodeKey ChildKey(int id) => new TipDotNodeKey(mNodeSelector.GetTipDotNodeKey().GraphName,
            mNodeSelector.GetTipDotNodeKey().NodeName, new ActivityNodeInstanceInfo(id));
        private void OnActivityDatasUpdate(IEnumerable<HomeTipActivity> datas) { foreach (var data in datas) OnActivitiesOpen(data); }
        private void OnActivitiesOpen(HomeTipActivity data)
        {
            if (!Services.TryGetPetTreeGachaActivity(out mActivityId)) return;
            if (data.Type == mActivityTypeGahca && data.Id == mActivityId)
                (NodeManager as IHomeActivityDynamicTipDotNodeManager)?.ConnectNodes(NodeKey, new List<TipDotNodeKey> { ChildKey(data.Id) });
        }
        private void OnActivitiesClose(HomeTipActivity data)
        {
            if (!Services.TryGetPetTreeGachaActivity(out mActivityId)) return;
            if (data.Type == mActivityTypeGahca && data.Id == mActivityId)
                (NodeManager as IHomeActivityDynamicTipDotNodeManager)?.DisconnectNode(NodeKey, ChildKey(data.Id));
        }
        public override TipDotNodeBase Clone() { var node = new TipDotLinkAnyPetGachaActivityNode(); node.Fill(this); return node; }
        public void Fill(TipDotNodeBase source)
        {
            var node = source as TipDotLinkAnyPetGachaActivityNode;
            if (node == null) throw new ArgumentException(GetType().Name, nameof(source));
            mNodeSelector = node.mNodeSelector?.Clone();
        }
    }
}
