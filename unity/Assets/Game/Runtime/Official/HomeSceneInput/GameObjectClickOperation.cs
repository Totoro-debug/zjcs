// Original selected native scene input; explicit pointer, camera and UI owners.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeSceneInput
{
using Object = UnityEngine.Object;

public class GameObjectClickOperation : GameObjectInputOperationBase<OnGameObjectClickResult>
{
	private readonly GlobalPointerDownOperation mGlobalPointerDownOperation;

	private readonly GlobalClickOperation mGlobalClickOperation;
    public GameObjectClickOperation(IHomeSceneClickPointer pointer, Action<Exception> reportException,
        IHomeSceneInputWorld world) : base(pointer, reportException, world)
    {
        mGlobalPointerDownOperation = new GlobalPointerDownOperation(pointer, reportException);
        mGlobalClickOperation = new GlobalClickOperation(pointer, reportException);
    }

	public override bool UpdateResult()
	{
		if (!Valid)
		{
			return false;
		}
		if (mGlobalPointerDownOperation.UpdateResult())
		{
			Result = GetHitResult(mGlobalPointerDownOperation.Result.ScreenPos);
		}
		if (!mGlobalClickOperation.UpdateResult())
		{
			return false;
		}
		GameObjectInputResult hitResult = GetHitResult(mGlobalClickOperation.Result.ScreenPos);
		if ((Object)(object)hitResult.Target != (Object)null)
		{
			return (Object)(object)hitResult.Target == (Object)(object)Result.Value.Target;
		}
		return false;
	}
}

}
