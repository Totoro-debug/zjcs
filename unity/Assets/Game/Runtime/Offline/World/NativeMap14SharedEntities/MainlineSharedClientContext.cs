using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Owns the original local player lifecycle and merged-map subscriptions.
    // The caller supplies its projected original Player; no second player model
    // or replacement condition evaluator is installed here.
    internal sealed class MainlineSharedClientContext : IDisposable
    {
        private MainlineSharedClientServices services;
        private object player, map, previousMap;
        internal object Player => player;
        private PropertyInfo playerProperty, mapProperty;

        public MainlineSharedClientContext(MainlineSharedWorld world, MainlineSharedClientServices services, object player,
            IReadOnlyDictionary<int, int> regions, Func<int, long> containerId)
        {
            try
            {
                this.services = services;
                var manager = services.Manager;
                playerProperty = manager.GetType().GetProperty("Player");
                mapProperty = manager.GetType().GetProperty("MapData");
                if (playerProperty.GetValue(manager) != null)
                    throw new InvalidOperationException("Close the active original client player before opening another map.");
                previousMap = mapProperty.GetValue(manager);
                var shared = world.Assemblies;
                var statusType = shared.CommonType("MapRegionStatus");
                var ids = regions.Keys.OrderBy(id => id).ToArray();
                if (!ids.SequenceEqual(Enumerable.Range(0, ids.Length)))
                    throw new InvalidOperationException("Original region snapshot indices are not contiguous.");
                var statuses = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(statusType));
                foreach (int region in ids) statuses.Add(Enum.ToObject(statusType, regions[region]));
                ((IDictionary)NativeMap14SharedPrefabs.Get(player, "MapRegionStatusDict"))[world.Data.MapClassId] = statuses;
                map = Activator.CreateInstance(shared.CommonType("PlayerMap"));
                mapProperty.SetValue(manager, map);
                this.player = player;
                playerProperty.SetValue(manager, player);
                var metadataType = shared.CommonType("MapInfoManager");
                var info = metadataType.GetMethod("GetMapPartInfo").Invoke(
                    metadataType.GetProperty("Instance").GetValue(null), new object[] { world.Data.MapClassId });
                foreach (int status in new[] { 1, 2 })
                    if ((bool)info.GetType().GetMethod("RegionStatusSupported").Invoke(info, new[] { Enum.ToObject(statusType, status) }))
                        map.GetType().GetMethod("SetConatainerStatus").Invoke(map,
                            new[] { Enum.ToObject(statusType, status), (object)containerId(status) });
                map.GetType().GetProperty("mMapLoEntityIdManager", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(map, world.LoEntityIds);
                map.GetType().GetProperty("MapContainer").SetValue(map, world.Root.Container);
                var component = world.Root.Root.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                    .Invoke(world.Root.Root, new object[] { shared.CommonType("MapComponent") });
                map.GetType().GetProperty("Map").SetValue(map, component);
                if ((bool)NativeMap14SharedPrefabs.Get(info, "SupportPublicRegion"))
                {
                    var line = map.GetType().GetProperty("PublicMapLineStatus");
                    line.SetValue(map, Enum.Parse(line.PropertyType, "Free"));
                    map.GetType().GetMethod("SetGroupMapIdOrderList").Invoke(map,
                        new object[] { new HashSet<long> { containerId(2) } });
                }
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (services == null) return;
            var manager = services.Manager;
            try
            {
                try { map?.GetType().GetProperty("MapContainer").SetValue(map, null); }
                finally
                {
                    try
                    {
                        if (player != null && ReferenceEquals(playerProperty.GetValue(manager), player))
                            playerProperty.SetValue(manager, null);
                    }
                    finally
                    {
                        if (map != null && ReferenceEquals(mapProperty.GetValue(manager), map))
                            mapProperty.SetValue(manager, previousMap);
                    }
                }
            }
            finally
            {
                services = null;
                player = map = previousMap = null;
            }
        }
    }
}
