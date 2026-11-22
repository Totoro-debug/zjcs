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
public struct OnGameObjectClickResult : IInputOperationResult
{
	public GameObjectInputResult Value { get; private set; }

	public static implicit operator OnGameObjectClickResult(GameObjectInputResult result)
	{
		return new OnGameObjectClickResult
		{
			Value = result
		};
	}
}

}
