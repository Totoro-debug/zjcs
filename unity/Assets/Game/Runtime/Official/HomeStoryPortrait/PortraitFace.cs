// Adapted from the selected original client; see home-story-portrait source manifest.
namespace Zjcs.Official.HomeStoryPortrait
{
using System;
using Common;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Object = UnityEngine.Object;
using UnityEngine.UI;

public class PortraitFace : MonoBehaviour
{
	public Func<RegionCodeEnum> RegionProvider { get; set; }
	[SerializeField]
	public ResourceAssetProxy<Sprite> mPortraitMainProxy;

	[SerializeField]
	public PortraitFaceMainRegionData[] mRegionPortraitMainProxies;

	[SerializeField]
	private PortraitFaceData[] mFacesProxy;

	[SerializeField]
	private Image mPortraitMainImg;

	[SerializeField]
	private Image mPortraitFaceImg;

	[SerializeField]
	private Vector2 mFaceCommonOffset;

	[SerializeField]
	private PortratFaceOffsetData[] mFaceOffsets;

	[SerializeField]
	private bool mNeedFlipOnRight = true;

	private string mCurrentPortraitType = "";

	private RectTransform mFaceTrans;

	private ETalkPortraitDirectionType mDirectionType;

	public bool NeedFlipOnRight => mNeedFlipOnRight;

	private void Awake()
	{
		mFaceTrans = ((Component)mPortraitFaceImg).GetComponent<RectTransform>();
	}

	public void SetDirection(ETalkPortraitDirectionType directionType)
	{
		mDirectionType = directionType;
	}

	public void LoadMain()
	{
		if ((Object)null != (Object)(object)mPortraitMainImg)
		{
			((Behaviour)mPortraitMainImg).enabled = true;
			mPortraitMainImg.sprite = GetPortraitMainProxy().Load();
			((Graphic)mPortraitMainImg).SetNativeSize();
		}
	}

	private ResourceAssetProxy<Sprite> GetPortraitMainProxy()
	{
		RegionCodeEnum regionCode = RegionProvider();
		if (mRegionPortraitMainProxies != null)
		{
			PortraitFaceMainRegionData[] array = mRegionPortraitMainProxies;
			foreach (PortraitFaceMainRegionData portraitFaceMainRegionData in array)
			{
				if (portraitFaceMainRegionData != null && portraitFaceMainRegionData.Region == regionCode && portraitFaceMainRegionData.PortraitMainProxy != null && portraitFaceMainRegionData.PortraitMainProxy.IsValid)
				{
					return portraitFaceMainRegionData.PortraitMainProxy;
				}
			}
		}
		return mPortraitMainProxy;
	}

	public void ChangeFace(string type)
	{
		if (string.IsNullOrEmpty(type))
		{
			((Behaviour)mPortraitFaceImg).enabled = false;
			mPortraitFaceImg.sprite = null;
		}
		else if (mCurrentPortraitType != type)
		{
			PortraitFaceData portraitFaceData = FindFaceData(type);
			if (portraitFaceData != null)
			{
				((Behaviour)mPortraitFaceImg).enabled = true;
				mPortraitFaceImg.sprite = LoadFaceSprite(portraitFaceData);
				((Graphic)mPortraitFaceImg).SetNativeSize();
				mFaceTrans.anchoredPosition = GetFaceOffset(type);
			}
		}
		mCurrentPortraitType = type;
	}

	public void SetColor(Color color)
	{
		if ((Object)null != (Object)(object)mPortraitMainImg)
		{
			((Graphic)mPortraitMainImg).color = color;
		}
		if ((Object)null != (Object)(object)mPortraitFaceImg)
		{
			((Graphic)mPortraitFaceImg).color = color;
		}
	}

	private Sprite LoadFaceSprite(PortraitFaceData data)
	{
		if (data == null)
		{
			return null;
		}
		ResourceAssetProxy<Sprite> faceSpriteProxy = data.GetFaceSpriteProxy(mDirectionType, RegionProvider());
		if (faceSpriteProxy == null || !faceSpriteProxy.IsValid)
		{
			return null;
		}
		return faceSpriteProxy.Load();
	}

	private PortraitFaceData FindFaceData(string type)
	{
		if (mFacesProxy == null)
		{
			return null;
		}
		PortraitFaceData[] array = mFacesProxy;
		foreach (PortraitFaceData portraitFaceData in array)
		{
			if (portraitFaceData.Type == type)
			{
				return portraitFaceData;
			}
		}
		return null;
	}

	private Vector2 GetFaceOffset(string type)
	{
		if (mFaceOffsets == null)
		{
			return mFaceCommonOffset;
		}
		PortratFaceOffsetData[] array = mFaceOffsets;
		foreach (PortratFaceOffsetData portratFaceOffsetData in array)
		{
			if (portratFaceOffsetData.Type == type)
			{
				return portratFaceOffsetData.Offset;
			}
		}
		return mFaceCommonOffset;
	}
}

}
