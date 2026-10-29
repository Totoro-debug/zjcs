namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public static class MiniMapMarkWayPointRules
{
public static bool IsUsable(IMiniMapMarkDataQueries queries, int mapClassId, int region)
	{
		if (!queries.HasMainPlayer)
		{
			return false;
		}
		return queries.GetMapRegionStatus(mapClassId, region) == MapRegionStatus.Public;
	}
public static bool IsEntityUsable(IMiniMapMarkDataQueries queries, Pos2d position)
	{
		if (!queries.HasMainMapContainer)
		{
			return false;
		}
		int classId = queries.MainMapClassId;
		int region = queries.GridToRegion(classId, position);
		return IsUsable(queries, classId, region);
	}
}

}
