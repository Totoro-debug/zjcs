// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13GridVisibility
{
public static class VisibilityBodyGeometry
{
public static Pos2d CalcLocalToWorldPos(Pos2d localPos, Pos2d pos, Vector2 realCenter, Direction dir, bool flipX)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return pos + CalcLocalToWorldDir(localPos, realCenter, dir, flipX);
	}
public static Pos2d CalcLocalToWorldDir(Pos2d localPos, Vector2 realCenter, Direction dir, bool flipX)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = new Vector2((float)localPos.X - realCenter.x, (float)localPos.Y - realCenter.y);
		if (flipX)
		{
			val.x = 0f - val.x;
		}
		Vector2 val2 = Rotate(val, dir);
		return new Pos2d((int)Math.Round(val2.x + realCenter.x), (int)Math.Round(val2.y + realCenter.y));
	}
public static Vector2 Rotate(Vector2 pos, Direction dir)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		switch (dir)
		{
		case Direction.Up:
			return pos;
		case Direction.Down:
			return new Vector2(0f - pos.x, 0f - pos.y);
		case Direction.Left:
			return new Vector2(0f - pos.y, pos.x);
		case Direction.Right:
			return new Vector2(pos.y, 0f - pos.x);
		default:
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return pos;
		}
		}
	}
}

}
