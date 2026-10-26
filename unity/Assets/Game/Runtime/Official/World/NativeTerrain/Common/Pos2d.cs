// Migrated from selected official terrain code; see Map13 source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.NativeTerrain.Common
{

[Serializable]
public struct Pos2d : IEquatable<Pos2d>
{
	public Pos2d(int x, int y) { X = x; Y = y; }

	public static readonly Pos2d Zero = new Pos2d(0, 0);

	public static readonly Pos2d Up = new Pos2d(0, 1);

	public static readonly Pos2d Down = new Pos2d(0, -1);

	public static readonly Pos2d Right = new Pos2d(1, 0);

	public static readonly Pos2d Left = new Pos2d(-1, 0);

	public static readonly Pos2d LeftUp = new Pos2d(-1, 1);

	public static readonly Pos2d LeftDown = new Pos2d(-1, -1);

	public static readonly Pos2d RightUp = new Pos2d(1, 1);

	public static readonly Pos2d RightDown = new Pos2d(1, -1);

	public static readonly List<Pos2d> FourDirs = new List<Pos2d> { Down, Left, Up, Right };

	public static readonly List<Pos2d> EightDirs = new List<Pos2d> { Down, LeftDown, Left, LeftUp, Up, RightUp, Right, RightDown };

	public int X;

	public int Y;

	public static int Distance(Pos2d lp, Pos2d rp)
	{
		return (lp - rp).Magnitude();
	}

	public static float RealDistance(Pos2d lp, Pos2d rp)
	{
		return (lp - rp).RealMagnitude();
	}

	public static int SquareDistance(Pos2d lp, Pos2d rp)
	{
		Pos2d pos2d = lp - rp;
		return pos2d.X * pos2d.X + pos2d.Y * pos2d.Y;
	}

	public static bool operator ==(Pos2d lp, Pos2d rp)
	{
		if (lp.X == rp.X)
		{
			return lp.Y == rp.Y;
		}
		return false;
	}

	public static bool operator !=(Pos2d lp, Pos2d rp)
	{
		if (lp.X == rp.X)
		{
			return lp.Y != rp.Y;
		}
		return true;
	}

	public static Pos2d operator +(Pos2d lp, Pos2d rp)
	{
		return new Pos2d(lp.X + rp.X, lp.Y + rp.Y);
	}

	public static Pos2d operator -(Pos2d lp, Pos2d rp)
	{
		return new Pos2d(lp.X - rp.X, lp.Y - rp.Y);
	}

	public static Pos2d operator *(Pos2d p, int d)
	{
		return new Pos2d(p.X * d, p.Y * d);
	}

	public static Pos2d operator *(int d, Pos2d p)
	{
		return new Pos2d(p.X * d, p.Y * d);
	}

	public static Pos2d operator *(Direction dir, Pos2d pos)
	{
		switch (dir)
		{
		case Direction.Up:
			return pos;
		case Direction.Down:
			return new Pos2d(-pos.X, -pos.Y);
		case Direction.Left:
			return new Pos2d(-pos.Y, pos.X);
		case Direction.Right:
			return new Pos2d(pos.Y, -pos.X);
		default:
		{
			UnityEngine.Debug.LogError($"Direction not supported: {dir}");
			return pos;
		}
		}
	}

	public override bool Equals(object obj)
	{
		if (!(obj is Pos2d pos2d))
		{
			return false;
		}
		return this == pos2d;
	}

	public bool Equals(Pos2d other)
	{
		return this == other;
	}

	public override int GetHashCode()
	{
		return 31 * X.GetHashCode() + Y.GetHashCode();
	}

	public int Magnitude()
	{
		return Math.Abs(X) + Math.Abs(Y);
	}

	public float RealMagnitude()
	{
		return (float)Math.Sqrt(X * X + Y * Y);
	}

	public override string ToString()
	{
		return $"{X},{Y}";
	}

	public static Direction LookDirection(Pos2d sourcePos, Pos2d targetPos, Direction defDir = Direction.Up)
	{
		return LookDirection(targetPos - sourcePos, defDir);
	}

	public static Direction LookDirection(Pos2d dir, Direction defDir = Direction.Up)
	{
		if (dir.X == 0 && dir.Y == 0)
		{
			return defDir;
		}
		if (dir.X > 0)
		{
			if (dir.Y > dir.X)
			{
				return Direction.Up;
			}
			if (dir.Y < -dir.X)
			{
				return Direction.Down;
			}
			return Direction.Right;
		}
		if (dir.Y > -dir.X)
		{
			return Direction.Up;
		}
		if (dir.Y < dir.X)
		{
			return Direction.Down;
		}
		return Direction.Left;
	}

	public static Pos2d LookDirectionVector(Pos2d sourcePos, Pos2d targetPos, Direction defDir = Direction.Up)
	{
		return DirectionGuider(LookDirection(targetPos - sourcePos, defDir));
	}

	public static Pos2d DirectionGuider(Direction dir)
	{
		return dir switch
		{
			Direction.Up => Up, 
			Direction.Down => Down, 
			Direction.Left => Left, 
			Direction.Right => Right, 
			_ => Zero, 
		};
	}

	public static Pos2d Round(Vector2 vec)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return new Pos2d((int)Math.Round(vec.x), (int)Math.Round(vec.y));
	}
}

}
