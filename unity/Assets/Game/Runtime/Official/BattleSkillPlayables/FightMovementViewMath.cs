// Migrated selected original source; see skill movement runtime receipt.
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
internal static class FightMovementViewMath
{
public static float CalcRealDistance(Pos2d startPos, List<Pos2d> path)
	{
		float num = 0f;
		if (path == null)
		{
			return num;
		}
		Pos2d lp = startPos;
		foreach (Pos2d item in path)
		{
			num += Pos2d.RealDistance(lp, item);
			lp = item;
		}
		return num;
	}

public static bool CalcFlipX(Pos2d sourcePos, Pos2d targetPos, bool defFlipX = false)
	{
		Pos2d pos2d = targetPos - sourcePos;
		if (pos2d.X > 0)
		{
			return false;
		}
		if (pos2d.X < 0)
		{
			return true;
		}
		return defFlipX;
	}
}

}
