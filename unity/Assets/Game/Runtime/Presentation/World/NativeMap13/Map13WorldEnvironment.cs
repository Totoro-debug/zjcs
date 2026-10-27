using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Offline.World.NativePartition;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Presentation.World.NativeMap13
{
    internal sealed class Map13WorldEnvironment : IMapEnvironmentServices<MapGroundRoot, MapEnvData>, IPartitionWorldContext<MapRegionStatus>
    {
        private readonly IMap13NativeWorldState state;
        internal readonly Map13LabelData Data;
        private readonly List<MapRegionStatus> statuses;
        private List<int> progresses;
        public MapGroundRoot MapGroundRoot { get; }
        public MapEnvData EnvironmentData { get; }
        public int MapClassId => 13;
        public int CurrentRegion => Data.QueryRegion(PlayerWorldPosition);
        public Vector3 PlayerWorldPosition => state.PlayerWorldPosition;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        public Map13WorldEnvironment(MapGroundRoot root, MapEnvData config, IMap13NativeWorldState state)
        {
            this.state = state;
            MapGroundRoot = root; EnvironmentData = config;
            Data = JsonUtility.FromJson<Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
            if (Data.manifest != "871_156598" || Data.regions.Length != 106) throw new InvalidOperationException("Unbound Map13 region configuration.");
            statuses = Data.regions.Select(r => Status(r.Region)).ToList();
            progresses = Data.regions.Select(r => state.GetRegionProgress(r.Region)).ToList();
        }
        private MapRegionStatus Status(int region) => state.IsPublicRegion(region) ? MapRegionStatus.Public : MapRegionStatus.Private;
        public List<MapRegionStatus> GetCurrentMapStatus() => statuses;
        public int GetMapRegionGoddessId(int map, int region) => map == 13 ? state.GetRegionGoddessId(region) : 0;
        public int GetWorldMapRegionProgress(int map, int region) => map == 13 && region >= 0 && region < 106 ? state.GetRegionProgress(region) : 0;
        public bool WorldMapRegionCleared(int map, int region, int progress) => map == 13 && progress != 0
            && (Data.maxProgress[region] == 0 || progress >= Data.maxProgress[region]);
        public Pos2d PosToGrid(Vector3 position) => new Pos2d((int)(position.x - Data.origin[0] + .5f), (int)(position.y - Data.origin[1] + .5f));
        public int QueryRegion(Pos2d point) => point.X < 0 || point.X >= Data.width || point.Y < 0 || point.Y >= Data.height
            ? -1 : Data.regionGrid[point.Y * Data.width + point.X];
        public void UnlockAllRegionPositions(int map, int region)
        {
            if (map == 13) state.UnlockAllRegionPositions(region);
        }
        public void Refresh()
        {
            foreach (var region in Data.regions)
            {
                int id = region.Region; var next = Status(id); var old = statuses[id];
                if (next == old) continue;
                statuses[id] = next;
                OnMapStatusUpdate?.Invoke(13, id, old, next);
            }
            var nextProgress = Data.regions.Select(r => state.GetRegionProgress(r.Region)).ToList();
            if (!progresses.SequenceEqual(nextProgress))
            {
                progresses = nextProgress;
                OnWorldMapProgressUpdate?.Invoke(13, progresses);
            }
        }
    }
}
