using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Offline.World.NativePartition;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeTerrain.Common;
using TerrainRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
namespace Zjcs.Presentation.World
{
    public interface IMainlineWorldGroundState : IMainlineWorldLabelState
    {
        Vector3 PlayerWorldPosition { get; }
        int GetRegionGoddessId(int region);
        void UnlockAllRegionPositions(int region);
    }
    public sealed class MainlineWorldEnvironment : IMapEnvironmentServices<MapGroundRoot, MapEnvData>,
        IPartitionWorldContext<TerrainRegionStatus>
    {
        private readonly IMainlineWorldGroundState state;
        private readonly List<TerrainRegionStatus> statuses;
        private List<int> progress;

        public MapGroundRoot MapGroundRoot { get; }
        public MapEnvData EnvironmentData { get; }
        public Map14LabelData Data { get; }
        public int MapClassId => Data.mapClassId;
        public int CurrentRegion => Data.QueryRegion(PlayerWorldPosition);
        public Vector3 PlayerWorldPosition => state.PlayerWorldPosition;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, TerrainRegionStatus, TerrainRegionStatus> OnMapStatusUpdate;

        public MainlineWorldEnvironment(MapGroundRoot root, MapEnvData config, Map14LabelData data, IMainlineWorldGroundState state)
        {
            MapGroundRoot = root;
            EnvironmentData = config;
            Data = data;
            this.state = state;
            statuses = Enumerable.Range(0, Data.regions.Length).Select(Status).ToList();
            progress = Enumerable.Range(0, Data.regions.Length).Select(state.GetRegionProgress).ToList();
        }

        private TerrainRegionStatus Status(int region) => state.IsPublicRegion(region)
            ? TerrainRegionStatus.Public : TerrainRegionStatus.Private;
        public List<TerrainRegionStatus> GetCurrentMapStatus() => statuses;
        public int GetMapRegionGoddessId(int mapClassId, int region) => mapClassId == MapClassId ? state.GetRegionGoddessId(region) : 0;
        public int GetWorldMapRegionProgress(int mapClassId, int region) => mapClassId == MapClassId ? state.GetRegionProgress(region) : 0;
        public bool WorldMapRegionCleared(int mapClassId, int region, int value) => mapClassId == MapClassId && value != 0
            && (region < 0 || region >= Data.maxProgress.Length || Data.maxProgress[region] == 0
                || value >= Data.maxProgress[region]);
        public Pos2d PosToGrid(Vector3 position) => new Pos2d(
            (int)(position.x - Data.origin[0] + 0.5f), (int)(position.y - Data.origin[1] + 0.5f));
        public int QueryRegion(Pos2d position) => position.X < 0 || position.X >= Data.width
            || position.Y < 0 || position.Y >= Data.height ? -1 : Data.regionGrid[position.Y * Data.width + position.X];
        public void UnlockAllRegionPositions(int mapClassId, int region)
        {
            if (mapClassId == MapClassId) state.UnlockAllRegionPositions(region);
        }

        public void Refresh()
        {
            List<int> nextProgress = Enumerable.Range(0, Data.regions.Length).Select(state.GetRegionProgress).ToList();
            if (!progress.SequenceEqual(nextProgress))
            {
                progress = nextProgress;
                // Original partitions preload the day layer while the region is
                // still private. Publish completed exploration before activation.
                OnWorldMapProgressUpdate?.Invoke(MapClassId, progress);
            }
            foreach (MapRegionInfo region in Data.regions)
            {
                int id = region.Region;
                TerrainRegionStatus next = Status(id), previous = statuses[id];
                if (next == previous) continue;
                statuses[id] = next;
                OnMapStatusUpdate?.Invoke(MapClassId, id, previous, next);
            }
        }
    }

}
