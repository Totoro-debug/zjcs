using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original condition node. The same local snapshot used by the
    // challenge button supplies CanChallenge; it never manufactures red dots.
    [Serializable]
    public sealed class TipDotActivityDomainBossDailyChallengeConditionNode : TipDotConditionNodeBase
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
        public override bool CheckShowTip() => Bindings.Read(EffectiveActivityId, 0)?.CanChallenge ?? false;
        public override TipDotNodeBase Clone()
        {
            var clone = new TipDotActivityDomainBossDailyChallengeConditionNode();
            clone.Fill(this); return clone;
        }
        public void Fill(TipDotNodeBase source)
        {
            if (!(source is TipDotActivityDomainBossDailyChallengeConditionNode node))
                throw new ArgumentException(GetType().Name, nameof(source));
            Fill(node);
        }
        public void Fill(TipDotActivityDomainBossDailyChallengeConditionNode source) => ActivityId = source.ActivityId;
    }
}
