using System;

namespace UnityEngine.UI
{

public static class LoopScrollSizeUtils
{
	public static float GetPreferredHeight(RectTransform item)
	{
		ILayoutElement val = default;
		float layoutProperty = LayoutUtility.GetLayoutProperty(item, (Func<ILayoutElement, float>)((ILayoutElement e) => e.minHeight), 0f, out val);
		ILayoutElement val2 = default;
		float layoutProperty2 = LayoutUtility.GetLayoutProperty(item, (Func<ILayoutElement, float>)((ILayoutElement e) => e.preferredHeight), 0f, out val2);
		float result = Mathf.Max(layoutProperty, layoutProperty2);
		if (val2 == null && val == null)
		{
			Rect rect = item.rect;
			result = rect.height;
		}
		return result;
	}

	public static float GetPreferredWidth(RectTransform item)
	{
		ILayoutElement val = default;
		float layoutProperty = LayoutUtility.GetLayoutProperty(item, (Func<ILayoutElement, float>)((ILayoutElement e) => e.minWidth), 0f, out val);
		ILayoutElement val2 = default;
		float layoutProperty2 = LayoutUtility.GetLayoutProperty(item, (Func<ILayoutElement, float>)((ILayoutElement e) => e.preferredWidth), 0f, out val2);
		float result = Mathf.Max(layoutProperty, layoutProperty2);
		if (val2 == null && val == null)
		{
			Rect rect = item.rect;
			result = rect.width;
		}
		return result;
	}
}

}
