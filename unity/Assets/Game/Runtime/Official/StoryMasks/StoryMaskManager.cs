// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;
using UnityEngine.UI;

public class StoryMaskManager : IStoryMaskManager
{
	private StoryMask mStoryMask;

	private bool mInPlaying;

	public bool EnableInvisibleMask { get; protected set; }

	public bool EnableColorMask { get; protected set; }

	public bool EnableRectMask { get; protected set; }

	public bool EnableWindowCopyMask { get; protected set; }

	public StoryMaskManager(StoryMask view, IStoryMaskHost host)
    {
        mStoryMask = view;
        view.Bind(host, this);
    }

	public void Dispose()
	{
		Reset();
	}

	public void Enable()
	{
		mInPlaying = true;
	}

	public void Disable()
	{
		mInPlaying = false;
		Reset();
	}

	public void Reset()
	{
		if ((Object)(object)mStoryMask != (Object)null)
		{
			SetInvisibleMask(isOn: false);
			CloseColorMask();
			CloseRectMask();
			CloseClickCallMask();
		}
	}

	public void CloseAllMask()
	{
		Reset();
		CloseNoStoryColorMask();
	}

	public void SetInvisibleMask(bool isOn)
	{
		EnableInvisibleMask = isOn;
		if (isOn)
		{
			if (!mInPlaying)
			{

			}
			else
			{
				mStoryMask.PlayFullMask();
			}
		}
		else
		{
			mStoryMask.CloseFullMask();
		}
	}

	public void SetColorMask(Color color = default(Color))
	{

		EnableColorMask = true;
		mStoryMask.PlayColorMask(color);
	}

	public void CloseColorMask()
	{
		if (EnableColorMask)
		{
			EnableColorMask = false;
			mStoryMask.CloseColorMask();
		}
	}

	private void PrepareForRectMask()
	{
		EnableRectMask = true;
		SetInvisibleMask(isOn: false);
		CloseColorMask();
	}

	public void OpenRectMask(RectTransform transform, MaskSetting setting)
	{
		PrepareForRectMask();
		mStoryMask.Play(transform, setting);
	}

	public void OpenRectMask(GameObject go, MaskSetting setting)
	{

		PrepareForRectMask();
		mStoryMask.Play(go, setting);
	}

	public void OpenRectMask(Button btn, MaskSetting setting)
	{
		RectTransform buttonApplyMaskRect = mStoryMask.GetButtonApplyMaskRect(btn);
		PrepareForRectMask();
		mStoryMask.Play(buttonApplyMaskRect, setting);
	}

	public void OpenRectMask(GameObject gameObject, Vector3 offset, Vector3 scale, Vector3 rectWLeftBottom, Vector3 rectWRightTop)
	{

		PrepareForRectMask();
		mStoryMask.PlayMask(gameObject, offset, scale, rectWLeftBottom, rectWRightTop);
	}

	public void OpenRectMask(GameObject targetGo, MaskSetting setting, Vector2 customSize = default(Vector2))
	{

		PrepareForRectMask();
		mStoryMask.Play(targetGo, setting, customSize);
	}

	public void CloseRectMask()
	{
		if (EnableRectMask)
		{
			EnableRectMask = false;
			mStoryMask.CloseMask();
		}
	}

	public void OpenClickCallMask(Action clickCb)
	{
		mStoryMask.OpenClickCallMask(clickCb);
	}

	public void CloseClickCallMask()
	{
		mStoryMask.CloseClickCallMask();
	}

	public void PlayNoStoryColorMask(Color color, bool overrideSort = true)
	{

		mStoryMask.PlayNoStoryColorMask(color, overrideSort);
	}

	public void CloseNoStoryColorMask()
	{
		mStoryMask.CloseNoStoryColorMask();
	}

	public bool CanShowMask()
	{
		return mStoryMask.CanShowMask();
	}

	public RectTransform GetButtonApplyMaskRect(Button button)
	{
		return mStoryMask.GetButtonApplyMaskRect(button);
	}

	public Vector2 GetBoundSize(GameObject go, AttachToObjUI attachToObjUI)
	{

		return mStoryMask.GetBoundSize(go, attachToObjUI);
	}

	public bool HasAnyMaskPlaying()
	{
		return mStoryMask.HasAnyMaskPlaying();
	}
}

}
