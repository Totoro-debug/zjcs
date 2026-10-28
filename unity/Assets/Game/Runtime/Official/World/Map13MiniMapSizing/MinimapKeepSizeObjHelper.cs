using System;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
public class MinimapKeepSizeObjHelper
{
	private GameObject mContent;

	private float mZoomThreshold;

	protected MiniMapSizingContext mMinimap;

	private Vector3 mDefaultScale;

	private MiniMapSizingDisposableBag mMiniMapSizingDisposableBag;

	public void Start(MiniMapSizingContext miniMap, GameObject content, float zoomThreshold)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		mMinimap = miniMap;
		mContent = content;
		mZoomThreshold = zoomThreshold;
		mDefaultScale = mContent.transform.localScale;
		if ((Object)(object)mMinimap == (Object)null || (Object)(object)mMinimap.SizingHandler == (Object)null)
		{
			MiniMapSizingErrorHandler message = new MiniMapSizingErrorHandler(71, 0, out var enabled);
			if (enabled)
			{
				message.AppendLiteral("MinimapKeepSizeObjHelper used but sence not have MiniMapKeepSizeHandler");
			}
			MiniMapSizingDiagnostic.Error(ref message, null, "Start", "/Volumes/Data/Projects/idleXX_cn_release_android/Client/Assets/Scripts/UI/EC/Map/MiniMap/KeppSize/MinimapKeepSizeObjHelper.cs", 21);
		}
		else
		{
			mMiniMapSizingDisposableBag = default;
			MiniMapSizingDisposable.AddTo<IDisposable>(mMinimap.HideFullScreenAction.Subscribe(OnHideFullScreen), ref mMiniMapSizingDisposableBag);
			MiniMapSizingDisposable.AddTo<IDisposable>(mMinimap.SizingHandler.OnCameraOrthographicSizeUpdate.Subscribe(OnCameraOrthographicSizeUpdate), ref mMiniMapSizingDisposableBag);
		}
	}

	public void End()
	{
		mMinimap = null;
		mContent = null;
		mMiniMapSizingDisposableBag.Dispose();
		mZoomThreshold = 0f;
	}

	protected virtual void OnCameraOrthographicSizeUpdate(MiniMapCameraController cameraController)
	{
		Zoom(cameraController);
	}

	private void Zoom(MiniMapCameraController cameraController)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		float zoomRate = cameraController.GetZoomRate();
		if (!(zoomRate < 0f) && !(zoomRate > 1f) && !((Object)(object)mContent == (Object)null) && !(mZoomThreshold <= 0f) && !(zoomRate <= 0f))
		{
			float scale = MiniMapKeepSizeHandler.GetScale(cameraController, mZoomThreshold, zoomRate);
			mContent.transform.localScale = Vector3.one * scale;
		}
	}

	private void OnHideFullScreen()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		mContent.transform.localScale = mDefaultScale;
	}
}

}
