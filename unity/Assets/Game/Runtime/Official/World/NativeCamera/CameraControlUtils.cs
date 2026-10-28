using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public static class CameraControlUtils
{
	private const float kInstantJumpDistanceThreshold = 14f;
	public static bool IsMapMoveImmediately(Vector3 targetPos, Vector3 curPos)
	{
		float num = targetPos.x - curPos.x;
		float num2 = targetPos.y - curPos.y;
		return num * num + num2 * num2 > 196f;
	}
}
}
