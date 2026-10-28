using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public struct OnIgnoreUIDragBeginResult : IInputOperationResult
{
	public Vector2 ScreenPos { get; private set; }
	public OnIgnoreUIDragBeginResult(Vector2 screenPos)
	{
		ScreenPos = screenPos;
	}
	public static implicit operator OnIgnoreUIDragBeginResult(Vector2 screenPos)
	{
		return new OnIgnoreUIDragBeginResult(screenPos);
	}
	public static implicit operator OnIgnoreUIDragBeginResult(Vector3 screenPos)
	{
		return new OnIgnoreUIDragBeginResult(CameraVectorCast.Convert(screenPos));
	}
	public static implicit operator Vector2(OnIgnoreUIDragBeginResult result)
	{
		return result.ScreenPos;
	}
	public static implicit operator Vector3(OnIgnoreUIDragBeginResult result)
	{
		return CameraVectorCast.Convert(result.ScreenPos);
	}
}
}
