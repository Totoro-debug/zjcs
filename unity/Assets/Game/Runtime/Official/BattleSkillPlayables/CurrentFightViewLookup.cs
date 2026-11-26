using System;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;

namespace Zjcs.Official.BattleSkillPlayables
{
    // Override hosts are separate native views. A selected unsupported managed
    // type must be migrated by its owner before the converter can load it.
    public abstract class BattlePlayableOverrideView : ECViewBase
    {
        public abstract GameObject Resolve(string tag, GameObject original);
    }

    public static class CurrentFightViewLookup
    {
        public static T Get<T>(MyEntity entity) where T : class, IECView
        {
            var driver = entity ? entity.GetComponentInChildren<ECViewDriver>(true) : null;
            return driver ? driver.GetView<T>() : null;
        }

        public static BattlePlayableOverrideView GetOverride(MyEntity entity)
            => Get<BattlePlayableOverrideView>(entity);
    }

    internal sealed class CurrentFightViewSubscription : IDisposable
    {
        private Action unsubscribe;
        internal CurrentFightViewSubscription(Action unsubscribe) => this.unsubscribe = unsubscribe;
        public void Dispose()
        {
            var action = unsubscribe;
            unsubscribe = null;
            action?.Invoke();
        }
    }
}
