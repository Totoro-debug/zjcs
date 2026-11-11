// Original UIUtils click containment; UI camera supplied by the local scope.
using UnityEngine;
namespace Zjcs.Official.HomeMainUiControls
{
public static class HomeMainUiUtils
{
	public static bool CheckIfClickInsideUI(RectTransform rectTransform, Vector2 clickPosition, Camera uiCamera)
	{
		Vector2 val = default;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, clickPosition, uiCamera, out val);
		Rect rect = rectTransform.rect;
		return rect.Contains(val);
	}
}
}
