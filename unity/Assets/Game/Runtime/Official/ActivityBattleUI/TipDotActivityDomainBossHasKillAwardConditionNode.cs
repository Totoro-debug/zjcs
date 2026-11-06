using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original award condition. Both original data update sources
    // arrive through the local activity module's committed Changed event.
    [Serializable]
    public sealed class TipDotActivityDomainBossHasKillAwardConditionNode : TipDotConditionNodeBase
    {
        public int ActivityId;
        public IActivityDomainBossPageBindings Bindings { private get; set; }
        private ActivityNodeInstanceInfo InstanceInfo => NodeKey.InstanceInfo as ActivityNodeInstanceInfo;
        private int EffectiveActivityId => ActivityId == 0 ? InstanceInfo.ActivityId : ActivityId;
        private bool subscribed;
        public override void Init()
        {
            base.Init();
            if (InstanceInfo == null && ActivityId == 0)
                Debug.LogWarning("ActivityNodeInstanceInfo 传递的InstnaceInfo无效");
            else
            {
                Bindings.Changed += OnActivityDataUpdate;
                subscribed = true;
            }
        }
        public override void Release()
        {
            if (subscribed) Bindings.Changed -= OnActivityDataUpdate;
            subscribed = false;
            base.Release();
        }
        private void OnActivityDataUpdate(int activityId)
        { if (activityId == EffectiveActivityId) SetDirty(); }
        public override bool CheckShowTip()
        {
            var state = Bindings.Read(EffectiveActivityId, 0);
            if (state == null) return false;
            for (int i = 0; i < state.Difficulty; i++)
                if (!state.AwardedDifficulties.Contains(i)) return true;
            return false;
        }
        public override TipDotNodeBase Clone()
        {
            var clone = new TipDotActivityDomainBossHasKillAwardConditionNode();
            clone.Fill(this); return clone;
        }
        public void Fill(TipDotNodeBase source)
        {
            if (!(source is TipDotActivityDomainBossHasKillAwardConditionNode node))
                throw new ArgumentException(GetType().Name, nameof(source));
            Fill(node);
        }
        public void Fill(TipDotActivityDomainBossHasKillAwardConditionNode source) => ActivityId = source.ActivityId;
    }
}
