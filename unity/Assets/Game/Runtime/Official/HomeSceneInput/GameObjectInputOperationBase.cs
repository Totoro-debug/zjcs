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

public abstract class GameObjectInputOperationBase<T> : IgnoreUIInputOperationBase<T> where T : IInputOperationResult
{
	protected GameObjectInputOperationBase(IHomeSceneClickPointer pointer, Action<Exception> reportException,
        IHomeSceneInputWorld world) : base(pointer, reportException, world) { }
    public override InputOperationLayer Layer => InputOperationLayer.GameObjectInput;

	protected bool Valid => (Object)(object)world.Camera != (Object)null;

	protected GameObjectInputResult GetHitResult(Vector2 screenPos)
	{
		if (IsCoveredByUI(screenPos))
		{
			return new GameObjectInputResult(null, (Vector2)world.Camera.ScreenToWorldPoint((Vector3)screenPos));
		}
		RaycastHit val = default;
		if (!Physics.Raycast(world.Camera.ScreenPointToRay((Vector3)screenPos), out val, 1000f))
		{
			return new GameObjectInputResult(null, (Vector2)world.Camera.ScreenToWorldPoint((Vector3)screenPos));
		}
		return new GameObjectInputResult(((Component)val.collider).gameObject, (Vector2)val.point);
	}
}

}
