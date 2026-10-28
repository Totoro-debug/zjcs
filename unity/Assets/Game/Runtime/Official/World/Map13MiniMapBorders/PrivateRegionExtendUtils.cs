using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Zjcs.Official.World.NativeTerrain.Common;
public static class PrivateRegionExtendUtils
{
	public static void DeleteCrossLine(Dictionary<Direction, (Vector2, Vector2)> dirToVec2)
	{
		if (dirToVec2.ContainsKey(Direction.Right) && dirToVec2.ContainsKey(Direction.Up))
		{
			DeleteCrossPoint(dirToVec2, Direction.Right, Direction.Up, xIsFirst: true, yIsFirst: false);
		}
		if (dirToVec2.ContainsKey(Direction.Left) && dirToVec2.ContainsKey(Direction.Down))
		{
			DeleteCrossPoint(dirToVec2, Direction.Left, Direction.Down, xIsFirst: true, yIsFirst: false);
		}
		if (dirToVec2.ContainsKey(Direction.Up) && dirToVec2.ContainsKey(Direction.Left))
		{
			DeleteCrossPoint(dirToVec2, Direction.Left, Direction.Up, xIsFirst: false, yIsFirst: true);
		}
		if (dirToVec2.ContainsKey(Direction.Down) && dirToVec2.ContainsKey(Direction.Right))
		{
			DeleteCrossPoint(dirToVec2, Direction.Right, Direction.Down, xIsFirst: false, yIsFirst: true);
		}
	}

	private static void DeleteCrossPoint(Dictionary<Direction, (Vector2, Vector2)> dirToVec2, Direction xDir, Direction yDir, bool xIsFirst, bool yIsFirst)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		if ((xDir != Direction.Right || yDir != Direction.Left) && (xDir != Direction.Up || yDir != Direction.Down) && dirToVec2.TryGetValue(yDir, out var value) && dirToVec2.TryGetValue(xDir, out var value2))
		{
			Vector2 val = new Vector2
			{
				x = (xIsFirst ? value2.Item1.x : value2.Item2.x),
				y = (yIsFirst ? value.Item1.y : value.Item2.y)
			};
			dirToVec2[xDir] = (xIsFirst ? (val, value2.Item2) : (value2.Item1, val));
			dirToVec2[yDir] = (yIsFirst ? (val, value.Item2) : (value.Item1, val));
		}
	}

	public static void ConcaveAngleHandle(Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> gridBorderLine, Pos2d pos2)
	{
		if (!gridBorderLine.TryGetValue(pos2, out var value))
		{
			return;
		}
		foreach (Direction value2 in Enum.GetValues(typeof(Direction)))
		{
			if (value.ContainsKey(value2))
			{
				ConcaveAnglePointTryExtend(gridBorderLine, pos2, value2);
			}
		}
	}

	private static void ConcaveAnglePointTryExtend(Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> gridBorderLine, Pos2d pos2, Direction dir)
	{
		if (!gridBorderLine.TryGetValue(pos2, out var value) || !value.ContainsKey(dir))
		{
			return;
		}
		switch (dir)
		{
		case Direction.Up:
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.LeftUp))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2 + Pos2d.LeftUp, pos2, Direction.Right, Direction.Up, xIsFirst: false, yIsFirst: true);
			}
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.RightUp))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2 + Pos2d.RightUp, pos2, Direction.Left, Direction.Up, xIsFirst: true, yIsFirst: false);
			}
			break;
		case Direction.Down:
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.LeftDown))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2 + Pos2d.LeftDown, pos2, Direction.Right, Direction.Down, xIsFirst: true, yIsFirst: false);
			}
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.RightDown))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2 + Pos2d.RightDown, pos2, Direction.Left, Direction.Down, xIsFirst: false, yIsFirst: true);
			}
			break;
		case Direction.Left:
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.LeftUp))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2, pos2 + Pos2d.LeftUp, Direction.Left, Direction.Down, xIsFirst: false, yIsFirst: true);
			}
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.LeftDown))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2, pos2 + Pos2d.LeftDown, Direction.Left, Direction.Up, xIsFirst: true, yIsFirst: false);
			}
			break;
		case Direction.Right:
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.RightUp))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2, pos2 + Pos2d.RightUp, Direction.Right, Direction.Down, xIsFirst: true, yIsFirst: false);
			}
			if (gridBorderLine.ContainsKey(pos2 + Pos2d.RightDown))
			{
				ConcaveAnglePointExtend(gridBorderLine, pos2, pos2 + Pos2d.RightDown, Direction.Right, Direction.Up, xIsFirst: false, yIsFirst: true);
			}
			break;
		}
	}

	private static void ConcaveAnglePointExtend(Dictionary<Pos2d, Dictionary<Direction, (Vector2, Vector2)>> gridBorderLine, Pos2d xPos2, Pos2d yPos2, Direction xDir, Direction yDir, bool xIsFirst, bool yIsFirst)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		if (gridBorderLine.TryGetValue(xPos2, out var value) && gridBorderLine.TryGetValue(yPos2, out var value2) && value.TryGetValue(xDir, out var value3) && value2.TryGetValue(yDir, out var value4))
		{
			Vector2 val = new Vector2
			{
				x = (xIsFirst ? value3.Item1.x : value3.Item2.x),
				y = (yIsFirst ? value4.Item1.y : value4.Item2.y)
			};
			value[xDir] = (xIsFirst ? (val, value[xDir].Item2) : (value[xDir].Item1, val));
			value2[yDir] = (yIsFirst ? (val, value2[yDir].Item2) : (value2[yDir].Item1, val));
		}
	}
}

}
