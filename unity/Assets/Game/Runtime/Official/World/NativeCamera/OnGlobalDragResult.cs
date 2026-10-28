using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public struct OnGlobalDragResult : IInputOperationResult
{
	public Vector2 BeginScreenPos { get; private set; }
	public Vector2 DraggingScreenPos { get; private set; }
	public bool Moved { get; private set; }
	public OnGlobalDragResult(Vector2 beginScreenPos, Vector2 draggingScreenPos, bool moved)
	{
		BeginScreenPos = beginScreenPos;
		DraggingScreenPos = draggingScreenPos;
		Moved = moved;
	}
}
}
