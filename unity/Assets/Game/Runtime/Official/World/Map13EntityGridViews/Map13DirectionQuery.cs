// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using Zjcs.Official.World.NativeTerrain.Common;
using UnityEngine;

namespace Zjcs.Official.World.Map13EntityGridViews
{
public static class Map13DirectionQuery
{
public static Quaternion DirToRotation(this Direction dir)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		switch (dir)
		{
		case Direction.Up:
			return Quaternion.Euler(0f, 0f, 0f);
		case Direction.Left:
			return Quaternion.Euler(0f, 0f, 90f);
		case Direction.Down:
			return Quaternion.Euler(0f, 0f, 180f);
		case Direction.Right:
			return Quaternion.Euler(0f, 0f, 270f);
		default:
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return Quaternion.Euler(0f, 0f, 0f);
		}
		}
	}
}

}
