// Selected original screen adaptation; local platform/service boundaries only.
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace Zjcs.Official.HomeScreenUI
{

public static class ScreenWidthAdapterUtils
{
	public const float kMaxResolutionAspectRatio = 121f / 160f;

	public const float kMinResolutionAspectRatio = 0.45f;

	private static MethodInfo sMiCanvasScalerHandle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);

	private static object[] sHandleParams = new object[0];

	public static Rect ConvertUIRectToScreenPixelRect(Rect uiRect, RectTransform rootCanvasRectTr)
	{
		if (rootCanvasRectTr == null)
		{
			UnityEngine.Debug.LogError("ConvertUIRectToScreenPixelRect失败, rootCanvasRectTr为空");
			return uiRect;
		}
		Vector2 size = rootCanvasRectTr.rect.size;
		float num = (float)ScreenMetrics.width / size.x;
		float num2 = (float)ScreenMetrics.height / size.y;
		return new Rect(uiRect.x * num, uiRect.y * num2, uiRect.width * num, uiRect.height * num2);
	}

	public static Rect GetSystemUISafeArea(RectTransform rootCanvasRectTr)
	{
		if (rootCanvasRectTr == null)
		{
			UnityEngine.Debug.LogError("GetApplySafeArea失败, rectTrRootCanvas为空");
			return new Rect(0f, 0f, 0f, 0f);
		}
		Rect systemSafeArea = SafeArea.SystemSafeArea;
		Vector2 size = rootCanvasRectTr.rect.size;
		float num = size.x / (float)ScreenMetrics.width;
		float num2 = size.y / (float)ScreenMetrics.height;
		return new Rect(systemSafeArea.x * num, systemSafeArea.y * num2, systemSafeArea.width * num, systemSafeArea.height * num2);
	}

	public static void ForceUpdateCanvasScaler(RectTransform rectTrCanvas)
	{
		if (rectTrCanvas == null)
		{
			return;
		}
		rectTrCanvas.TryGetComponent<CanvasScaler>(out var component);
		if (component == null)
		{
			return;
		}
		try
		{
			sMiCanvasScalerHandle.Invoke(component, sHandleParams);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("ForceUpdateCanvasScaler异常: " + "\n");
		}
	}

	public static Rect RectIntersection(Rect r1, Rect r2)
	{
		float num = Mathf.Max(r1.xMin, r2.xMin);
		float num2 = Mathf.Max(r1.yMin, r2.yMin);
		float num3 = Mathf.Min(r1.xMax, r2.xMax);
		float num4 = Mathf.Min(r1.yMax, r2.yMax);
		return new Rect(num, num2, Mathf.Max(0f, num3 - num), Mathf.Max(0f, num4 - num2));
	}

	public static float NotZeroFloatAdd2(float f)
	{
		if (f > 0f)
		{
			return f + 2f;
		}
		if (f == 0f)
		{
			return 0f;
		}
		return f;
	}
}

}
