// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System.Collections;
using System.Collections.Generic;




namespace Zjcs.Official.World.Map13GridVisibility
{
public struct MapActivedRegionGridInfoEnumerator : IEnumerable<Pos2d>, IEnumerable
{
	private MapActivedRegionGridInfo mInfo;
	public MapActivedRegionGridInfoEnumerator(MapActivedRegionGridInfo info) { mInfo = info; }

	public IEnumerator<Pos2d> GetEnumerator()
	{
		if (mInfo == null)
		{
			yield break;
		}
		if (mInfo.RegionActivedGrids != null && mInfo.RegionActivedGrids.Length != 0)
		{
			Pos2d[] regionActivedGrids = mInfo.RegionActivedGrids;
			for (int i = 0; i < regionActivedGrids.Length; i++)
			{
				yield return regionActivedGrids[i];
			}
		}
		if (mInfo.RegionExtendVisiableGrids != null && mInfo.RegionExtendVisiableGrids.Length != 0)
		{
			Pos2d[] regionActivedGrids = mInfo.RegionExtendVisiableGrids;
			for (int i = 0; i < regionActivedGrids.Length; i++)
			{
				yield return regionActivedGrids[i];
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

}
