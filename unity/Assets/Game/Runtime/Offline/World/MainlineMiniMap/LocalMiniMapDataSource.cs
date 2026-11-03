using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Offline.World.MapGridVisibility;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // A coherent read of one visited map, including states refreshed by its
    // owning world module. Exploration history is independent of current state.
    [Serializable]
    public sealed class LocalMiniMapSnapshot
    {
        public int MapClassId, Width, Height;
        public bool Visited;
        public byte[] GridVisibility;
        public List<int> PublicRegions = new List<int>();
        public List<LocalMiniMapEntity> Entities = new List<LocalMiniMapEntity>();
    }

    [Serializable]
    public sealed class LocalMiniMapEntity
    {
        public int PlacementId, EntityClassId, Region, State;
        public bool Explored;
    }

    public sealed class LocalMiniMapDataSource : IMiniMapOfflineDataSource
    {
        private readonly Func<int, LocalMiniMapSnapshot> read;

        public LocalMiniMapDataSource(Func<int, LocalMiniMapSnapshot> read)
        { this.read = read ?? throw new ArgumentNullException(nameof(read)); }

        public MiniMapOfflineDataRequest Request(int mapClassId)
        {
            if (mapClassId <= 0) throw new ArgumentOutOfRangeException(nameof(mapClassId));
            var snapshot = read(mapClassId);
            if (snapshot == null || snapshot.MapClassId != mapClassId || !snapshot.Visited
                || snapshot.PublicRegions == null || snapshot.Entities == null
                || snapshot.GridVisibility == null
                || !OfflineMapGridVisibility.IsValidSnapshot(snapshot.GridVisibility, snapshot.Width, snapshot.Height))
                throw new InvalidDataException("A valid local snapshot of the requested visited map is required.");
            var regions = new HashSet<int>();
            foreach (int region in snapshot.PublicRegions)
                if (region < 0 || !regions.Add(region))
                    throw new InvalidDataException("Local minimap public regions must be unique and non-negative.");
            var response = new PlayerOfflineMiniMapDataResponse
            {
                MapClassId = mapClassId,
                GridVisibilityByte = (byte[])snapshot.GridVisibility.Clone(),
                ActiveRegions = new List<int>(snapshot.PublicRegions),
                ActiveableEntDataDict = new Dictionary<MapWorldActiveableEntityType, List<int>>(),
                LiteEntityDataDict = new Dictionary<int, PlayerOfflineMiniMapLiteEntityData>(),
                UseLatestLocalEntityStates = true
            };
            var placements = new HashSet<int>();
            foreach (var entity in snapshot.Entities)
            {
                if (entity == null || entity.PlacementId <= 0 || entity.EntityClassId <= 0
                    || entity.Region < 0 || !placements.Add(entity.PlacementId))
                    throw new InvalidDataException("Local minimap entity identities must be valid and unique.");
                if (entity.Explored || regions.Contains(entity.Region))
                    response.LiteEntityDataDict.Add(entity.PlacementId, new PlayerOfflineMiniMapLiteEntityData
                    { EntityClassId = entity.EntityClassId, State = entity.State });
            }
            var request = new MiniMapOfflineDataRequest();
            CompleteAfterSubscription(request, response).Forget();
            return request;
        }

        private static async UniTask CompleteAfterSubscription(MiniMapOfflineDataRequest request,
            PlayerOfflineMiniMapDataResponse response)
        {
            // Match the original request handle: the coordinator subscribes
            // immediately after Request returns. Never complete synchronously.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            request.Complete(response, 0);
        }
    }
}
