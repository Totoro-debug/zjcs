using System;
using UnityEngine;
using Zjcs.Official.EntityViews;

namespace Zjcs.Official.BattleStatusPlayables
{
    public static class CurrentStatusViewLookup
    {
        public static ECFightStatusPlayableView Get(MyFightStatus status)
        {
            var driver = status ? status.GetComponentInChildren<ECViewDriver>(true) : null;
            return driver ? driver.GetView<ECFightStatusPlayableView>() : null;
        }
    }

    internal sealed class CurrentStatusViewSubscription : IDisposable
    {
        private Action unsubscribe;
        internal CurrentStatusViewSubscription(Action unsubscribe) => this.unsubscribe = unsubscribe;
        public void Dispose() { var action = unsubscribe; unsubscribe = null; action?.Invoke(); }
    }
}
