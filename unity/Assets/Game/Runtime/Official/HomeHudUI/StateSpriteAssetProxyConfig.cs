namespace Zjcs.Official.HomeHudUI
{
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class StateSpriteAssetProxyConfig : IWorldStateConfig
{
	[SerializeField]
	private StateSpriteAssetProxyData mStateProxyData;

	public void SetState(WorldStateConfigValue state)
	{
		foreach (StateSpriteAssetProxyData.SpriteAssetProxyConfig spriteRendererConfig in mStateProxyData.SpriteRendererConfigs)
		{
			if (spriteRendererConfig.Proxy.TryGetValue(state, out var value))
			{
				Sprite sprite = value.Load();
				SetSprite(spriteRendererConfig.Img, sprite);
			}
		}
	}

	public void Clear()
	{
		foreach (StateSpriteAssetProxyData.SpriteAssetProxyConfig spriteRendererConfig in mStateProxyData.SpriteRendererConfigs)
		{
			SetSprite(spriteRendererConfig.Img, null);
			foreach (WorldMonoKeyValuePair<WorldStateConfigValue, ResourceAssetProxy<Sprite>> item in spriteRendererConfig.Proxy)
			{
				item.Value.Unload();
			}
		}
	}

	private void SetSprite(Image image, Sprite sprite)
	{
		image.sprite = sprite;
	}
}

}
