using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
namespace Zjcs.Official.World.Map13MiniMap
{
    public abstract class MiniMapVisibilityAgent:MonoBehaviour
    {
        public abstract GridVisibilityAgentComponent Component { get; }
        public abstract event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
    }
    public abstract class MiniMapMaskContext:MiniMapDescriptionContext
    {
        public abstract MiniMapVisibilityAgent VisibilityAgent { get; }
        public abstract int CharacterViewRange { get; }
        public abstract Pos2d CharacterGridPos { get; }
        public abstract IEnumerable<Pos2d> ForEachMapRegionActivedAllGrid(int map,int region);
        public Vector3 GridToWorld(int map,Pos2d grid)
        {
            if(!TryGetMapLiteData(map,out var info))throw new InvalidOperationException("Original minimap map data is unavailable.");
            return info.GridToPos(grid);
        }
	public Vector3Int GetTilePosByGrid(int mapClassId, Pos2d grid)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = GridToWorld(mapClassId, grid);
		return new Vector3Int((int)((val.x - 0.5f) / 1f), (int)((val.y - 0.5f) / 1f), (int)(val.z / 1f));
	}

    }
}
