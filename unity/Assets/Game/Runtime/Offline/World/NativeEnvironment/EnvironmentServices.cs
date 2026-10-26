using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.NativeEnvironment
{
    public interface IMapEnvironmentServices<TRoot, TData>
    {
        TRoot MapGroundRoot { get; }
        TData EnvironmentData { get; }
        int MapClassId { get; }
        List<MapRegionStatus> GetCurrentMapStatus();
        Pos2d PosToGrid(Vector3 position);
        int QueryRegion(Pos2d position);
        void UnlockAllRegionPositions(int mapClassId, int region);
    }

    public interface IMapRegionEntityLight<TLight>
    {
        int Region { get; }
        TLight Light { get; }
    }
}
