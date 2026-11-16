// Adapted from the selected original client; see home-story-portrait source manifest.
namespace Zjcs.Official.HomeStoryPortrait
{
using System;
using Common;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Object = UnityEngine.Object;

[Serializable]
public class PortraitFaceMainRegionData
{
	public RegionCodeEnum Region;

	public ResourceAssetProxy<Sprite> PortraitMainProxy;
}

}
