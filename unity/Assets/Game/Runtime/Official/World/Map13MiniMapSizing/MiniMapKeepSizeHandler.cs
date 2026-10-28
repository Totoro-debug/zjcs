using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
[RequireComponent(typeof(MiniMapSizingContext))]
public class MiniMapKeepSizeHandler : MonoBehaviour, IFullScrMiniMapStateListener
{
	public GAction<MiniMapCameraController> OnCameraOrthographicSizeUpdate = new GAction<MiniMapCameraController>();

	public void OnShowFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Combine(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(FireOnCameraOrthographicSizeUpdate));
		OnCameraOrthographicSizeUpdate?.Invoke(cameraController);
	}

	public void OnHideFullScreenMiniMap(MiniMapCameraController cameraController)
	{
		cameraController.OnCameraOrthographicSizeUpdate = (Action<MiniMapCameraController>)Delegate.Remove(cameraController.OnCameraOrthographicSizeUpdate, new Action<MiniMapCameraController>(FireOnCameraOrthographicSizeUpdate));
	}

	private void FireOnCameraOrthographicSizeUpdate(MiniMapCameraController cameraController)
	{
		OnCameraOrthographicSizeUpdate?.Invoke(cameraController);
	}

	public static float GetScale(MiniMapCameraController cameraController, float zoomThreshold, float zoomRate)
	{
		if (zoomRate <= zoomThreshold)
		{
			return 1f;
		}
		float num = cameraController.mySizeMin + (cameraController.mySizeMax - cameraController.mySizeMin) * zoomThreshold;
		return 1f + (cameraController.MiniMapCamera.orthographicSize - num) / num;
	}
}

}
