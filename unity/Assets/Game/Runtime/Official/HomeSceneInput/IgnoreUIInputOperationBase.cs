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

public abstract class IgnoreUIInputOperationBase<T> : InputOperationBase<T> where T : IInputOperationResult
{
	protected readonly IHomeSceneInputWorld world;
    protected IgnoreUIInputOperationBase(IHomeSceneClickPointer pointer, Action<Exception> reportException,
        IHomeSceneInputWorld world) : base(pointer, reportException)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

	public override InputOperationLayer Layer => InputOperationLayer.IgnoreUIInput;

	protected bool IsCoveredByUI(Vector2 screenPos)
	{
		if (!world.EventSystemValid)
		{
			return false;
		}
		RaycastResult raycastResult;
		return world.TryGetFirstRaycastResult(screenPos, out raycastResult);
	}
}

}
