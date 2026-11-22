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

public struct GameObjectInputResult
{
	public GameObject Target { get; private set; }

	public Vector2 WorldPos { get; private set; }

	public GameObjectInputResult(GameObject target, Vector2 worldPos)
	{
		Target = target;
		WorldPos = worldPos;
	}
}

}
