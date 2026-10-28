using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
public class DragControlUtils
{
	internal static Vector2 CalculateOffset(ref Bounds borderBounds, ref Bounds targetBounds, bool horizontal, bool vertical, MovementType movementType, ref Vector2 delta)
	{
		Vector2 zero = Vector2.zero;
		if (movementType == MovementType.Unrestricted)
		{
			return zero;
		}
		Vector2 val = CameraVectorCast.Convert(targetBounds.min);
		Vector2 val2 = CameraVectorCast.Convert(targetBounds.max);
		if (horizontal)
		{
			val.x += delta.x;
			val2.x += delta.x;
			float num = borderBounds.max.x - val2.x;
			float num2 = borderBounds.min.x - val.x;
			if (num2 > 0.001f)
			{
				zero.x = num2;
			}
			else if (num < -0.001f)
			{
				zero.x = num;
			}
		}
		if (vertical)
		{
			val.y += delta.y;
			val2.y += delta.y;
			float num3 = borderBounds.max.y - val2.y;
			float num4 = borderBounds.min.y - val.y;
			if (num3 < -0.001f)
			{
				zero.y = num3;
			}
			else if (num4 > 0.001f)
			{
				zero.y = num4;
			}
		}
		return zero;
	}
	internal static float RubberDelta(float overStretching, float viewSize, float overFactor)
	{
		return (1f - 1f / (Mathf.Abs(overStretching) * overFactor / viewSize + 1f)) * viewSize * Mathf.Sign(overStretching);
	}
}
}
