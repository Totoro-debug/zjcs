using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Original markers and their service-created data must resolve the same
    // installed map, region, placement and transport inputs as their owning root.
    // Only live player/entity operations cross back to the owning map state.
    internal sealed class MainlineMiniMapWorldQueries : IMiniMapMarkDataQueries, IMiniMapManagedQueries, IServiceProvider
    {
        private readonly MainlineMiniMapWorldData world;
        private readonly MiniMapManagedContext state;
        private readonly IServiceProvider services;

        public MainlineMiniMapWorldQueries(MainlineMiniMapWorldData world, MiniMapManagedContext state, IServiceProvider services)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.state = state ? state : throw new ArgumentNullException(nameof(state));
            this.services = services ?? throw new ArgumentNullException(nameof(services));
        }

        private IMiniMapMarkDataQueries Live => state.MarkDataQueries
            ?? throw new InvalidOperationException("The owning map's live minimap entity queries are unbound.");

        public object GetService(Type type) => type == typeof(IMiniMapMarkDataQueries) ? this : services.GetService(type);
        public int GridToArea(int map, Pos2d position) => world.GridToArea(map, position);
        public string GetAreaTag(int map, int area) => world.GetAreaTag(map, area);
        public MiniMapMarkMapInfo GetMapInfo(int map) => world.GetMapInfo(map);
        public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map, int placement) => world.GetContainerEntityInfo(map, placement);
        public string GetEntityTag(int map, int placement) => world.GetEntityTag(map, placement);
        public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map, int placement) => world.GetMapTransportTargetInfo(map, placement);
        public int GridToRegion(int map, Pos2d position) => world.GridToRegion(map, position);
        public Vector3 MapGridToPos(int map, Pos2d position)
        {
            if (!world.TryGetMapLiteData(map, out var lite))
                throw new InvalidOperationException("The requested original minimap ground is not installed: " + map);
            return lite.GridToPos(position);
        }
        public EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entity, int group, int map, string areaTag, string entityTag)
            => Live.GetEntityMapUIDisplay(entity, group, map, areaTag, entityTag);
        public MiniMapMarkExtendInfo GetEntityExtend(int entity) => Live.GetEntityExtend(entity);
        public IMiniMapMarkEntity GetCommonPrefab(int entity) => Live.GetCommonPrefab(entity);
        public bool RegionIsActived(int map, int region) => Live.RegionIsActived(map, region);
        public int QueryRegion(Pos2d position) => Live.QueryRegion(position);
        public int CurrentMeMapId => Live.CurrentMeMapId;
        public bool HasMainPlayer => Live.HasMainPlayer;
        public bool HasMainMapContainer => Live.HasMainMapContainer;
        public int MainMapClassId => Live.MainMapClassId;
        public MapRegionStatus GetMapRegionStatus(int map, int region) => Live.GetMapRegionStatus(map, region);
        public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity) => Live.DoTransfer(entity);
        public void DoEnterMap(int map, int placement) => Live.DoEnterMap(map, placement);
        public MiniMapManagedMapPartInfo GetMapPartInfo(int map) => state.ManagedQueries.GetMapPartInfo(map);
        public List<MapRegionStatus> GetCurrentMapStatus() => state.ManagedQueries.GetCurrentMapStatus();
        public bool TryGetStatusByContainerId(long container, out MapRegionStatus status)
            => state.ManagedQueries.TryGetStatusByContainerId(container, out status);
        public IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id) => state.ManagedQueries.GetEntity(id);
        public IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map, int region) => world.GetEntityByMapRegion(map, region);
        public long MainPlayerId => state.ManagedQueries.MainPlayerId;
    }
}
