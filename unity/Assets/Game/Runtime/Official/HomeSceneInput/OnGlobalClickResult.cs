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

public struct OnGlobalClickResult : IInputOperationResult
{
	public Vector2 ScreenPos { get; private set; }

	public OnGlobalClickResult(Vector2 screenPos)
	{
		ScreenPos = screenPos;
	}

	public static implicit operator OnGlobalClickResult(Vector2 screenPos)
	{
		return new OnGlobalClickResult(screenPos);
	}

	public static implicit operator OnGlobalClickResult(Vector3 screenPos)
	{
		return new OnGlobalClickResult((Vector2)screenPos);
	}

	public static implicit operator Vector2(OnGlobalClickResult result)
	{
		return result.ScreenPos;
	}

	public static implicit operator Vector3(OnGlobalClickResult result)
	{
		return (Vector3)result.ScreenPos;
	}
}

}
