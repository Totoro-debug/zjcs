// Migrated original grid presentation/query source; see Map13 grid-view receipt.
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
// Required original movement/ground/body-data consumption boundaries. They
// implement no navigation, collision, body occupancy or movement-speed rules.

namespace Zjcs.Official.World.Map13EntityGridViews
{
public abstract class Map13MovementAgent : MonoBehaviour
{ public abstract bool IsMoving { get; } public abstract void Stop(); }
public abstract class Map13GridGroundData
{ public abstract Vector3 GridToPos(Pos2d grid); public abstract Pos2d PosToGrid(Vector3 position); }
public abstract class Map13GridTransformInfo
{ public Vector2 RealCenter; public abstract List<Pos2d> GetBodyRange(); }
public sealed class Map13TransformData
{ public Vector3 Position, Scale; public Quaternion Rotation; }
public sealed class Map13GridTransformData
{
    public GridTransformPosUpdatedEventDelegate PosUpdatedEvent = new GridTransformPosUpdatedEventDelegate();
    public Pos2d Pos;
    public Direction Direction;
    public bool FlipX;
    public Map13GridTransformInfo Info;
    public Map13GridGroundData GridGround;
public Vector3 GridToPosition(Pos2d grid)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		return GridToPosition(GridGround, grid, Info.RealCenter);
	}

public Pos2d PositionToGrid(Vector3 pos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		return PositionToGrid(GridGround, pos, Info.RealCenter);
	}

public static Vector3 GridToPosition(Map13GridGroundData gridGround, Pos2d gridPos, Vector2 realCenter)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = realCenter * 1f;
		return gridGround.GridToPos(gridPos) + new Vector3(val.x, val.y, 0f);
	}

public static Pos2d PositionToGrid(Map13GridGroundData gridGround, Vector3 pos, Vector2 realCenter)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		Vector2 val = realCenter * 1f;
		return gridGround.PosToGrid(pos) - new Pos2d((int)val.x, (int)val.y);
	}
}

}
