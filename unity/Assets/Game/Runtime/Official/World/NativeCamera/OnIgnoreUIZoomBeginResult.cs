using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public struct OnIgnoreUIZoomBeginResult : IInputOperationResult
{
	public Vector2 ScreenPos { get; private set; }
	public float ZoomFactor { get; private set; }
	public OnIgnoreUIZoomBeginResult(Vector2 screenPos, float zoomFactor)
	{
		ScreenPos = screenPos;
		ZoomFactor = zoomFactor;
	}
}
}
