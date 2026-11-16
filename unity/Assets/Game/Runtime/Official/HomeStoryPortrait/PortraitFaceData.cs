// Adapted from the selected original client; see home-story-portrait source manifest.
namespace Zjcs.Official.HomeStoryPortrait
{
using System;
using Common;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Object = UnityEngine.Object;

[Serializable]
public class PortraitFaceData
{
	public string Type;

	public ResourceAssetProxy<Sprite> FaceSpriteProxy;

	public ResourceAssetProxy<Sprite> FaceSpriteRightProxy;

	public PortraitFaceRegionData[] RegionFaceSpriteProxies;

	public ResourceAssetProxy<Sprite> GetFaceSpriteProxy(ETalkPortraitDirectionType directionType, RegionCodeEnum? region = null)
	{
		RegionCodeEnum targetRegion = region ?? throw new InvalidOperationException("Portrait region is required.");
		if (TryGetRegionFaceSpriteProxy(targetRegion, directionType, out var faceSpriteProxy))
		{
			return faceSpriteProxy;
		}
		if (directionType == ETalkPortraitDirectionType.Right && FaceSpriteRightProxy != null && FaceSpriteRightProxy.IsValid)
		{
			return FaceSpriteRightProxy;
		}
		return FaceSpriteProxy;
	}

	private bool TryGetRegionFaceSpriteProxy(RegionCodeEnum targetRegion, ETalkPortraitDirectionType directionType, out ResourceAssetProxy<Sprite> faceSpriteProxy)
	{
		faceSpriteProxy = null;
		if (RegionFaceSpriteProxies == null)
		{
			return false;
		}
		PortraitFaceRegionData[] regionFaceSpriteProxies = RegionFaceSpriteProxies;
		foreach (PortraitFaceRegionData portraitFaceRegionData in regionFaceSpriteProxies)
		{
			if (portraitFaceRegionData != null && portraitFaceRegionData.Region == targetRegion)
			{
				if (directionType == ETalkPortraitDirectionType.Right && portraitFaceRegionData.FaceSpriteRightProxy != null && portraitFaceRegionData.FaceSpriteRightProxy.IsValid)
				{
					faceSpriteProxy = portraitFaceRegionData.FaceSpriteRightProxy;
					return true;
				}
				if (portraitFaceRegionData.FaceSpriteProxy != null && portraitFaceRegionData.FaceSpriteProxy.IsValid)
				{
					faceSpriteProxy = portraitFaceRegionData.FaceSpriteProxy;
					return true;
				}
			}
		}
		return false;
	}
}

}
