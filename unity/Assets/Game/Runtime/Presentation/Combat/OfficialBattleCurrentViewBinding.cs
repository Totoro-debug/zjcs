using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Offline.World.NativeMap14EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    internal static class OfficialBattleCurrentViewBinding
    {
        internal static void Prepare(GameObject instance, object services)
        {
            var drivers = instance.GetComponentsInChildren<ECViewDriver>(true);
            if (drivers.Length == 0) return;
            if (!(services is IServiceProvider provider) || !(services is IECViewInjection))
                throw new InvalidOperationException("Current official battle views require entity service injection.");
            if (!instance.GetComponent<ECViewEntityBinding>()) instance.AddComponent<NativeMyEntityBinding>();
            foreach (var driver in drivers)
            {
                driver.ServiceProvider = provider;
                driver.ClientVisibilitySupporterFactory = new ConstantClientVisibilitySupporterFactory();
            }
        }
    }
}
