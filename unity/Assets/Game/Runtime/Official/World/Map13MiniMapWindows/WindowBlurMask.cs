using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;
public class WindowBlurMask : WindowMaskBase
{
	[SerializeField]
	private RawImage mImage;

	private Dictionary<PopupWindow, RenderTexture> mWindow2RTDLookup = new Dictionary<PopupWindow, RenderTexture>();

	private List<PopupWindow> mExpiredWindowList = new List<PopupWindow>();

	public IBlurManager BlurManager { private get; set; }

	protected override void OnAttachToWindow(PopupWindow target)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		RemoveExpiredRenderTexture();
		if (!mWindow2RTDLookup.TryGetValue(target, out var value))
		{
			return;
		}
		mImage.texture = (Texture)(object)value;
		ExpandImageToFullCanvas();
		Transform transform = ((Component)this).transform;
		RectTransform val = (RectTransform)(object)((transform is RectTransform) ? transform : null);
		if (val != null)
		{
			Rect rect = val.rect;
			float num = rect.width * (float)((Texture)value).height;
			float num2 = (float)((Texture)value).width * rect.height;
			if (!Mathf.Approximately(num2, num))
			{
				Rect uvRect = mImage.uvRect;
				uvRect.width = num / num2;
				uvRect.x = 0.5f - 0.5f * uvRect.width;
				mImage.uvRect = uvRect;
			}
		}
	}

	private void ExpandImageToFullCanvas()
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)Canvas == (Object)null || (Object)(object)Canvas.rootCanvas == (Object)null)
		{
			Debug.LogError("WindowBlurMask ExpandToFullScreen failed! Canvas or rootCanvas is null! Canvas == null:" + (Canvas == null));
			return;
		}
		Transform transform = ((Component)Canvas.rootCanvas).transform;
		RectTransform val = (RectTransform)(object)((transform is RectTransform) ? transform : null);
		if ((Object)(object)val != (Object)null && (Object)(object)mImage != (Object)null)
		{
			RectTransform rectTransform = ((Graphic)mImage).rectTransform;
			rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
			rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
			Rect rect = val.rect;
			rectTransform.sizeDelta = rect.size;
			rectTransform.anchoredPosition = Vector2.zero;
		}
	}

	protected override void OnUnattachToWindow(PopupWindow target)
	{
		RemoveExpiredRenderTexture();
	}

	public override async UniTask Prepare(PopupWindow window, bool reopenWindowInTop, CancellationToken token)
    {
        if (!mWindow2RTDLookup.TryGetValue(window, out var value))
        {
            value = BlurManager.GetRenderTexture();
            mWindow2RTDLookup.Add(window, value);
        }
        if (!reopenWindowInTop)
        {
            BlurManager.TakeAShot(value);
            await UniTask.NextFrame(token, false);
        }
        else Graphics.Blit(mImage.texture, value);
    }

	private void RemoveExpiredRenderTexture()
	{
		foreach (PopupWindow key in mWindow2RTDLookup.Keys)
		{
			if (key.State != PopupWindow.EState.Show && key.State != PopupWindow.EState.Showing)
			{
				mExpiredWindowList.Add(key);
			}
		}
		RenderTexture rt = default;
		foreach (PopupWindow mExpiredWindow in mExpiredWindowList)
		{
			if (mWindow2RTDLookup.Remove(mExpiredWindow, out rt))
			{
				BlurManager.ReleaseRenderTexture(rt);
			}
		}
		mExpiredWindowList.Clear();
	}
}

}
