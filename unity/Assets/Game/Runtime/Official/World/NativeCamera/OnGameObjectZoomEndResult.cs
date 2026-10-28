using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public struct OnGameObjectZoomEndResult : IInputOperationResult
{
	public GameObject Target { get; private set; }
	public Vector2 WorldPos { get; private set; }
	public float ZoomFactor { get; private set; }
	public OnGameObjectZoomEndResult(GameObject target, Vector2 worldPos, float zoomFactor)
	{
		Target = target;
		WorldPos = worldPos;
		ZoomFactor = zoomFactor;
	}
}
}
