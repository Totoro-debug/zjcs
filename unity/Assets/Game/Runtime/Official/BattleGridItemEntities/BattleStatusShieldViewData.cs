using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.BattleGridItemEntities
{
    // Captured local settlement values; views never derive gameplay from frames.
    public abstract class BattleStatusShieldViewData
    {
        public abstract GameObject ApplyObject { get; }
        public event Action<EcEntityIdentifier, long, long> OnUpdateRemainData;
        public void NotifyRemainData(EcEntityIdentifier id, long maximum, long remaining)
            => OnUpdateRemainData?.Invoke(id, maximum, remaining);
    }
    public abstract class BattleStatusDamageReduceAbsoluteViewData
    {
        public abstract GameObject ApplyObject { get; }
        public abstract int MaximumCount { get; }
        public event Action<int> OnUpdateTriggerCount;
        public void NotifyTriggerCount(int count) => OnUpdateTriggerCount?.Invoke(count);
    }
}
