// Complete original movement implementation; see mainline character movement source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.ProfessionUI;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Zjcs.Official.World.NativeTerrain.Common;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityClientComponents
{
public sealed class MapEntityMoveHandlerCharacterOther : MapEntityMoveHandlerCharacterBase
{
	public MapEntityMoveHandlerCharacterOther(bool forbidTurn, MyEntity myEnt, UnityEngine.ILogger logger, IMapEntityMovementRuntime runtime)
		: base(forbidTurn, myEnt, logger, runtime)
	{
	}

	public override void UpdatePath(int index, List<Pos2d> newAppendPath)
	{
		if (!IsMoving)
		{
			FixUpdatePath(index, newAppendPath);
			return;
		}
		mCurrentPath = UpdatePathByLogicPath(index, newAppendPath, mCurrentPath);
		mPathEndPos = mCurrentPath[mCurrentPath.Count - 1];
		TryFixToTargetPos(ProcessNextPosition());
	}

	private void FixUpdatePath(int index, List<Pos2d> newAppendPath)
	{
		if (mSpeed <= 0f || index < 0 || newAppendPath == null || newAppendPath.Count <= 0)
		{
			return;
		}
		Vector3 logicPosition = mMyGridTrans.GetLogicPosition();
		if (!Runtime.IsWithinThresholdSqrDistance(pos2: (Vector2)(mMyGridTrans.GridToPosition(newAppendPath[0])), pos1: (Vector2)(logicPosition)))
		{
			Debug.Log("FixUpdatePath failed!");
			return;
		}
		List<Pos2d> list = new List<Pos2d>();
		for (int i = 0; i < index; i++)
		{
			list.Add(mMyGridTrans.Pos);
		}
		list.AddRange(newAppendPath);
		float num = (float)index / mSpeed;
		long ticks = Runtime.GetDateTime().AddSeconds(0f - num).Ticks;
		MoveSub(FillPathByLogicPath(list, mCurrentPath), mSpeed, ticks, mMoveType);
	}
}

}
