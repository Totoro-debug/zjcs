// Original activity-open condition and event subscriptions.
using System;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeActivityTipDots
{
    [Serializable]
    public class TipDotActivityIsOpenNode : TipDotConditionNodeBase
    {
        public ActivityType ActivityType;
        public int ActivityId;
        private int mActivityId;
        public IHomeActivityTipServices Services { get; set; }
        public override void Init()
        {
            base.Init();
            mActivityId = ActivityId <= 0 && NodeKey.InstanceInfo is ActivityNodeInstanceInfo info ? info.ActivityId : ActivityId;
            Services.OnActivityOpen += OnActivityOpen;
            Services.OnActivityClose += OnActivityClose;
            Services.OnActivitiesUpdate += OnActivitiesUpdate;
        }
        public override void Release()
        {
            Services.OnActivityOpen -= OnActivityOpen;
            Services.OnActivityClose -= OnActivityClose;
            Services.OnActivitiesUpdate -= OnActivitiesUpdate;
            base.Release();
        }
        private void OnActivityOpen(HomeTipActivity data) { if (data.Type == ActivityType && data.Id == mActivityId) SetDirty(); }
        private void OnActivityClose(HomeTipActivity data) { if (data.Type == ActivityType && data.Id == mActivityId) SetDirty(); }
        private void OnActivitiesUpdate(ActivityType type) { if (type == ActivityType) SetDirty(); }
        public override bool CheckShowTip() => Services.HasActivity(ActivityType, mActivityId);
        public override TipDotNodeBase Clone() { var node = new TipDotActivityIsOpenNode(); node.Fill(this); return node; }
        public void Fill(TipDotNodeBase source)
        {
            var node = source as TipDotActivityIsOpenNode;
            if (node == null) throw new ArgumentException(GetType().Name, nameof(source));
            ActivityType = node.ActivityType; ActivityId = node.ActivityId;
        }
    }
}
