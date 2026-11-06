// Complete original movement implementation; see mainline character movement source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.ProfessionUI;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Zjcs.Official.World.NativeTerrain.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Official.EntityClientComponents
{
public abstract class MapEntityMoveHandlerCharacterBase : MapEntityMoveHandlerBase
{
	protected List<Vector2> mCurrentPath = new List<Vector2>();

	protected float mSpeed;

	protected long mStartTick;

	protected Vector2? mPathEndPos;

	protected bool mFlipX;

	protected MapMoveType mMoveType;

	private int mCurIndex = -1;

	public MapEntityMoveHandlerCharacterBase(bool forbidTurn, MyEntity myEnt, UnityEngine.ILogger logger, IMapEntityMovementRuntime runtime)
		: base(forbidTurn, myEnt, logger, runtime)
	{
	}

	public override void Move(List<Pos2d> logicPath, float logicSpeed, long moveStartTick, bool forceStartTick, MapMoveType moveType)
	{
		if (logicPath != null && logicPath.Count > 0)
		{
			if (!forceStartTick)
			{
				moveStartTick = FixMoveStartTick(moveStartTick, logicSpeed);
			}
			float speed = logicSpeed * 1f;
			MoveSub(FillPathByLogicPath(logicPath, mCurrentPath), speed, moveStartTick, moveType);
		}
	}

	protected void MoveSub(List<Vector2> path, float speed, long moveStartTick, MapMoveType moveType)
	{
		mCurrentPath = path;
		mSpeed = speed;
		mStartTick = moveStartTick;
		mPathEndPos = mCurrentPath[mCurrentPath.Count - 1];
		mCurIndex = -1;
		mFlipX = mMyGridTrans.GetFlipXByScale();
		mMoveType = moveType;
		StartMove(mMoveType);
		TryFixToTargetPos(ProcessNextPosition());
	}

	public override void Stop(bool forceStop, Vector2? stopPos)
	{
		Vector2? val = stopPos ?? mPathEndPos;
		if (val.HasValue)
		{
			if (forceStop)
			{
				SetTargetPosition(val.Value);
			}
			else
			{
				TryFixToTargetPos(val.Value);
			}
			mPathEndPos = val.Value;
			if (IsMoving)
			{
				TryStop();
			}
		}
	}

	protected bool TryStop()
	{
		if (Vector2.Distance((Vector2)(TargetPosition), mPathEndPos.Value) < 0.01f)
		{
			SetTargetPosition(mPathEndPos.Value);
			StoptMove();
			return true;
		}
		return false;
	}

	public override void Update(float deltaTime)
	{
		if (!IsMoving)
		{
			return;
		}
		if (mCurrentPath.Count <= 0)
		{
			StoptMove();
		}
		else if (!TryStop())
		{
			Vector2 val = ProcessNextPosition();
			if (!TryFixToTargetPos(val))
			{
				Vector2 targetPosition = Vector2.MoveTowards((Vector2)(TargetPosition), val, mSpeed * deltaTime);
				SetTargetPosition(targetPosition);
			}
		}
	}

	protected Vector2 ProcessNextPosition()
	{
		if (mCurrentPath.Count == 0)
		{
			return (Vector2)(TargetPosition);
		}
		float num = (float)Runtime.CalcElapsedSecond(mStartTick) * mSpeed;
		int num2 = (int)num;
		if (num2 > mCurIndex)
		{
			mCurIndex = num2;
			DoOnIndexUpdate();
		}
		if (num2 + 1 >= mCurrentPath.Count)
		{
			return mCurrentPath.Last();
		}
		float num3 = Mathf.Clamp01(num - (float)num2);
		return Vector2.Lerp(mCurrentPath[num2], mCurrentPath[num2 + 1], num3);
	}

	protected bool TryFixToTargetPos(Vector2 targetPos)
	{
		if (!Runtime.IsWithinThresholdSqrDistance((Vector2)(TargetPosition), targetPos))
		{
			SetTargetPosition(targetPos);
			return true;
		}
		return false;
	}

	private void DoOnIndexUpdate()
	{
		if (mCurIndex >= 0 && mCurIndex + 1 < mCurrentPath.Count && !mForbidTurn && (Object)(object)mMyGridTrans != (Object)null)
		{
			bool? flipX = MyGridTransform.GetFlipX((Vector2)(mCurrentPath[mCurIndex]), (Vector2)(mCurrentPath[mCurIndex + 1]));
			if (flipX.HasValue && mFlipX != flipX.Value)
			{
				mFlipX = flipX.Value;
				mMyGridTrans.SetScaleByFlipX(mFlipX);
			}
		}
	}

	private long FixMoveStartTick(long moveStartTick, float logicSpeed)
	{
		if (moveStartTick <= 0)
		{
			return Runtime.GetTick();
		}
		if (Runtime.CalcElapsedSecond(moveStartTick) < (double)(Runtime.SClientDistanceThreshold / logicSpeed))
		{
			return Runtime.GetTick();
		}
		return moveStartTick;
	}
}

}
