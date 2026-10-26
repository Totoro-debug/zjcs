using System;
using UnityEngine;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Offline.World.NativeEnvironment;

namespace Zjcs.Offline.World.NativeMap13
{
    // Supplies the original components' explicit local services before Awake/Start.
    public static class NativeMap13EnvironmentBinding
    {
        public static void Bind(GameObject instance, IMapEnvironmentServices<MapGroundRoot, MapEnvData> services)
        {
            if (!instance) throw new ArgumentNullException(nameof(instance));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (instance.activeInHierarchy)
                throw new InvalidOperationException("Bind original Map13 environment dependencies while the instance is inactive.");
            foreach (var component in instance.GetComponentsInChildren<MapEnvEffect>(true)) component.Bind(services);
            foreach (var component in instance.GetComponentsInChildren<MapEffect>(true)) component.Bind(services);
            foreach (var component in instance.GetComponentsInChildren<MapLight>(true)) component.Bind(services);
            foreach (var component in instance.GetComponentsInChildren<MapRegionLight>(true)) component.Bind(services);
        }
    }
}
