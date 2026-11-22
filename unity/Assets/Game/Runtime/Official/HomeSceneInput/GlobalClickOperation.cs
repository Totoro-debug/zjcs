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

public class GlobalClickOperation : InputOperationBase<OnGlobalClickResult>
{
	private const float kClickOffsetThread = 20f;

	private readonly GlobalPointerDownOperation mGlobalPointerDownOperation;
    private readonly IHomeSceneClickPointer clickPointer;
    public GlobalClickOperation(IHomeSceneClickPointer pointer, Action<Exception> reportException)
        : base(pointer, reportException)
    {
        clickPointer = pointer;
        mGlobalPointerDownOperation = new GlobalPointerDownOperation(pointer, reportException);
    }

	private bool mPointerDown;

	public const float kClickOffsetThreadSqr = 400f;

	public override InputOperationLayer Layer => InputOperationLayer.GlobalInput;

	public override bool UpdateResult()
	{
		if (!mPointerDown)
		{
			mPointerDown = mGlobalPointerDownOperation.UpdateResult();
			return false;
		}
		Vector3 mousePosition = (Vector3)pointer.Position;
		Vector3 val = (Vector3)mGlobalPointerDownOperation.Result - mousePosition;
		if (val.sqrMagnitude >= 400f)
		{
			mPointerDown = false;
			return false;
		}
		if (!clickPointer.LeftUp)
		{
			return false;
		}
		Result = mGlobalPointerDownOperation.Result.ScreenPos;
		mPointerDown = false;
		return true;
	}
}

}
