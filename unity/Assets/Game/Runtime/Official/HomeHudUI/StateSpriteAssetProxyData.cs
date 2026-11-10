namespace Zjcs.Official.HomeHudUI
{
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class StateSpriteAssetProxyData
{
	[Serializable]
	public class SpriteAssetProxyConfig
	{
		public Image Img;

		public WorldStateKVMatch<ResourceAssetProxy<Sprite>> Proxy;
	}

	public List<SpriteAssetProxyConfig> SpriteRendererConfigs;
}

}
