using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public struct OnGlobalDragEndResult : IInputOperationResult
{
	public Vector2 BeginScreenPos { get; private set; }
	public Vector2 EndScreenPos { get; private set; }
	public OnGlobalDragEndResult(Vector2 beginScreenPos, Vector2 endScreenPos)
	{
		BeginScreenPos = beginScreenPos;
		EndScreenPos = endScreenPos;
	}
}
}
