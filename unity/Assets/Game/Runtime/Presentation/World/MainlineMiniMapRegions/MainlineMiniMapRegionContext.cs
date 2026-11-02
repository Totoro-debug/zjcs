using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.World
{
    // Binds original MiniMap/session/controller/resource surfaces before Start.
    public sealed class MainlineMiniMapRegionContext : MiniMapRegionContext, IMiniMapCharacterManager
    {
        private MainlineMiniMapRegionAssets assets;
        private IMainlineMiniMapRegionState state;
        private Dictionary<string, GameObject> prefabs;
        private List<MapRegionStatus> statuses;
        private IMiniMapCharacterController character;

        internal void Bind(MainlineMiniMapRegionAssets assets, IMainlineMiniMapRegionState state)
        {
            this.assets = assets;
            this.state = state;
            prefabs = assets.Regions.ToDictionary(row => row.OriginalGuid, row => row.Prefab);
            statuses = state.RegionStatuses.ToList();
            character = state.Character;
        }

        public override int MapClassId => assets.MapClassId;
        public override int CurrentMapClassId => state.CurrentMapClassId;
        public override bool HasMainPlayer => state.HasMainPlayer;
        public override IMapViewRangeHandler ViewRangeHandler => state.ViewRange;
        public IMiniMapCharacterController MyMeUiCharacter => character;
        public event Action<IMiniMapCharacterController, IMiniMapCharacterController> OnMeMapControllerUpdated;
        public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public override event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        public override List<MapRegionStatus> GetMapStatusListInTeam(int mapClassId) => mapClassId == MapClassId
            ? new List<MapRegionStatus>(statuses) : throw new InvalidDataException("Unexpected minimap region map identity.");
        public override bool IsRangeInViewRange(Pos2d min, Pos2d max) => (ViewRangeHandler
            ?? throw new InvalidOperationException("Original minimap view range is not bound.")).IsRangeInViewRange(min, max);
        public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid) => new Lease(
            prefabs.TryGetValue(guid, out var prefab) ? prefab : throw new InvalidDataException("Original regional prefab is not bound: " + guid));
        public override GameObject InstantiateRegion(GameObject prefab, Transform parent, bool worldPositionStays)
            => Instantiate(prefab, parent, worldPositionStays);

        internal void Refresh()
        {
            if (state.RegionStatuses.Count != statuses.Count)
                throw new InvalidDataException("Minimap region state dimensions changed.");
            var nextCharacter = state.Character;
            if (!ReferenceEquals(character, nextCharacter))
            {
                var previous = character;
                character = nextCharacter;
                OnMeMapControllerUpdated?.Invoke(previous, character);
            }
            for (int region = 0; region < statuses.Count; region++)
            {
                var previous = statuses[region];
                var next = state.RegionStatuses[region];
                if (previous == next) continue;
                statuses[region] = next;
                OnMapStatusUpdate?.Invoke(MapClassId, region, previous, next);
            }
        }

        internal void PublishViewRange() => OnVisibleRangeUpdate?.Invoke(ViewRangeHandler);

        private sealed class Lease : IMiniMapRegionAssetLease
        {
            public GameObject Result { get; private set; }
            public Lease(GameObject prefab) { Result = prefab; }
            // Serialized local assets are shared. Release this retained handle;
            // destroying the asset would invalidate another minimap instance.
            public void Dispose() { Result = null; }
        }
    }
}
