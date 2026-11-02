using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.World
{
    public interface IMainlineMiniMapRegionState
    {
        int CurrentMapClassId { get; }
        bool HasMainPlayer { get; }
        IMiniMapCharacterController Character { get; }
        IMapViewRangeHandler ViewRange { get; }
        IReadOnlyList<MapRegionStatus> RegionStatuses { get; }
    }

    /// <summary>Original regional minimap loading, visibility, activation and resource lifetime.</summary>
    public sealed class MainlineMiniMapRegions : IDisposable
    {
        private readonly GameObject root;
        private readonly MainlineMiniMapRegionContext context;
        private bool disposed;

        public MainlineMiniMapRegions(Transform parent, MainlineMiniMapRegionAssets assets,
            IMainlineMiniMapRegionState state)
        {
            if (!assets || !assets.Ground || assets.MapClassId <= 0 || state == null || assets.Regions == null
                || assets.Regions.Length == 0 || state.RegionStatuses == null
                || state.RegionStatuses.Count != assets.Regions.Length
                || assets.Regions.Any(row => row == null || !row.Prefab || string.IsNullOrEmpty(row.OriginalGuid))
                || assets.Regions.Select(row => row.OriginalGuid).Distinct().Count() != assets.Regions.Length)
                throw new InvalidDataException("Original regional minimap assets/state are incomplete.");
            root = new GameObject("Official Map" + assets.MapClassId + " regional minimap");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            try
            {
                context = root.AddComponent<MainlineMiniMapRegionContext>();
                context.Bind(assets, state);
                var ground = UnityEngine.Object.Instantiate(assets.Ground, root.transform, false);
                var managers = ground.GetComponentsInChildren<MiniMapPrivateRegionMaskManager>(true);
                if (managers.Length != 1)
                    throw new InvalidDataException("Original minimap ground requires one regional manager.");
                managers[0].MiniMap = context;
                managers[0].MapMyCharacterManager = context;
                root.SetActive(true);
            }
            catch { Dispose(); throw; }
        }

        public void Refresh()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMapRegions));
            context.Refresh();
        }

        // Call when the original view-range handler reports a change. Repeating
        // this every frame would reset the original ten-second retirement delay.
        public void ViewRangeChanged()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMapRegions));
            context.PublishViewRange();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Original OnDisable removes subscriptions before OnDestroy releases
            // the retained resources; the context remains alive for that sequence.
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        }
    }
}
