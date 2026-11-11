using UnityEngine;
namespace Zjcs.Official.HomeSceneUiControls
{
public static class DirectionGuiderUtils
{
	public static Vector2 GetGuiderPosition(Vector2 focusObjViewPort, Vector2 itemViewPort, float angleOfLine)
	{
		if (focusObjViewPort == itemViewPort)
		{
			return default;
		}
		float num = (itemViewPort.y - focusObjViewPort.y) / (itemViewPort.x - focusObjViewPort.x);
		if (angleOfLine <= 45f && angleOfLine > -45f)
		{
			return new Vector2(0f, (0f - focusObjViewPort.x) * num + focusObjViewPort.y);
		}
		if (angleOfLine <= 135f && angleOfLine > 45f)
		{
			return new Vector2((0f - focusObjViewPort.y) * (1f / num) + focusObjViewPort.x, 0f);
		}
		if (angleOfLine <= -45f && angleOfLine > -135f)
		{
			return new Vector2((1f - focusObjViewPort.y) * (1f / num) + focusObjViewPort.x, 1f);
		}
		return new Vector2(1f, (1f - focusObjViewPort.x) * num + focusObjViewPort.y);
	}

}
}
