// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.World.Map13GridVisibility
{
public static class VisibilityRangeQuery
{
public static List<Pos2d> CalcAllVisiblePos(GridGroundComponent gridGround, Pos2d pos, int viewRange)
	{
		return GetAllVisiblePosEnumerator(gridGround, pos, viewRange).ToList();
	}
public static IEnumerable<Pos2d> GetAllVisiblePosEnumerator(GridGroundComponent gridGround, Pos2d pos, int viewRange)
	{
		List<Pos2d> list = VisibilityRangeMath.GetLoopOutList(pos, viewRange);
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			if (gridGround.InGround(list[i]))
			{
				yield return list[i];
			}
		}
	}
}

}
