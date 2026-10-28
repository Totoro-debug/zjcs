using System;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Zjcs.Official.World.NativeCamera
{
[Serializable]
public class CameraBaseSetting
{
	[Tooltip("默认相机阻尼")]
	public Vector2 DefaultFollowDamping = new Vector2(0.2f, 0.2f);
	[Tooltip("双指移动造成摄像机拉近拉远的系数")]
	public float ZoomFactor = 1f;
	[Tooltip("滚轮造成摄像机拉近拉远的系数")]
	public float EditorZoomFactor = 0.5f;
	[Tooltip("视野靠近的最大系数")]
	[Range(1f, 10f)]
	public float ViewCloseLimitFactor = 2f;
	[Tooltip("视野远离的最小系数")]
	[Range(0.1f, 1f)]
	public float ViewLeaveLimitFactor = 0.75f;
	[Tooltip("视野拖动靠近的最大系数")]
	public float ViewZoomCloseLimitFactor = 3f;
	[Tooltip("视野拖动远离的最大系数")]
	public float ViewZoomLeaveLimitFactor = 0.5f;
	public float GmViewCloseLimitFactor;
	public float GmViewLeaveLimitFactor;
	[Tooltip("视野超出最近限制后的复位速度")]
	public float ViewOverCloseLimitResetSpeed = 1f;
	[Tooltip("视野超出最远限制后的复位速度")]
	public float ViewOverLeaveLimitResetSpeed = 1.5f;
	public void GetViewSize(float baseViewSize, bool gmControl, out float softViewSizeMin, out float softViewSizeMax, out float zoomViewSizeMin, out float zoomViewSizeMax)
	{
		if (gmControl)
		{
			softViewSizeMin = (zoomViewSizeMin = baseViewSize / GmViewCloseLimitFactor);
			softViewSizeMax = (zoomViewSizeMax = baseViewSize / GmViewLeaveLimitFactor);
			return;
		}
		softViewSizeMin = baseViewSize / ViewCloseLimitFactor;
		softViewSizeMax = baseViewSize / ViewLeaveLimitFactor;
		zoomViewSizeMin = baseViewSize / ViewZoomCloseLimitFactor;
		zoomViewSizeMax = baseViewSize / ViewZoomLeaveLimitFactor;
	}
	public CameraBaseSetting()
	{
	}
}
}
