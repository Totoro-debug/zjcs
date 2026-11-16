// Adapted from the selected original client; see home-story-portrait source manifest.
namespace Zjcs.Official.HomeStoryPortrait
{
using System;
using Common;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Object = UnityEngine.Object;

[Serializable]
public class PortraitFaceRegionData
{
	public RegionCodeEnum Region;

	public ResourceAssetProxy<Sprite> FaceSpriteProxy;

	public ResourceAssetProxy<Sprite> FaceSpriteRightProxy;
}

}
