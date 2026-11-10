// Migrated selected original source; see skill movement runtime receipt.
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
// Consumed action snapshot; original shared timing methods are copied below.
// The host owns entity creation, authoritative positions and destruction scheduling.
public sealed class FightActionSkillMoveComponent : IEntityComponentInfoSource
{
    public int ClassId;
    public List<Pos2d> Path;
    public Direction Direction;
    public Pos2d? BlockedPos;
    public FightActionSkillMoveComponentInfo Info;
    object IEntityComponentInfoSource.ComponentInfo => Info;
    private Pos2d mSourcePos;
    private float mMoveDistance, mMoveDuration;
    public float TotalMoveDuration => mMoveDuration;

    public void InitializeMovement(Pos2d source)
    {
        mSourcePos = source;
        mMoveDistance = FightMovementViewMath.CalcRealDistance(mSourcePos, Path) * 1f;
        mMoveDuration = GetTotalMoveDuration();
    }
public float GetDuration(float distance)
	{
		if (Info.MoveDuration >= 0f)
		{
			return Info.MoveDuration;
		}
		return distance / Info.MoveSpeed;
	}

public bool IsTargetStart()
	{
		if (Path.Count == 1)
		{
			return mSourcePos == Path[0];
		}
		return false;
	}

public float GetMoveSpeed()
	{
		if (Info.MoveSpeed > 0f)
		{
			return Info.MoveSpeed;
		}
		if (BlockedPos.HasValue && IsTargetStart() && Info.MoveDuration != 0f)
		{
			return 1f / Info.MoveDuration;
		}
		if (mMoveDuration != 0f)
		{
			return mMoveDistance / mMoveDuration;
		}
		return 0f;
	}

private float GetTotalMoveDuration()
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (Info.MoveDuration >= 0f)
		{
			return Info.MoveDuration * (float)Path.Count;
		}
		if (Info.MoveSpeed > 0f)
		{
			return mMoveDistance / Info.MoveSpeed;
		}
		Debug.LogError($"[Battle] ActionSkillMove {ClassId} MoveDuration:{Info.MoveDuration} MoveSpeed:{Info.MoveSpeed}");
		return 0f;
	}
}

}
