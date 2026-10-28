using Object = UnityEngine.Object;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class ImageLoaderProxy : LoaderProxyBase<Image, Sprite, AssetGuidProxy<Sprite>>
{
	protected override IProfessionImageResourceLoader GetResourceLoader()
	{
		if ((Object)(object)mLoader == (Object)null)
		{
			return null;
		}
		return ProfessionImageResourceBridge.Resolve(mLoader);
	}

	protected override void Load(Sprite data)
	{
		mLoader.sprite = data;
	}

	public new void Load(string spriteName)
	{
		base.Load("Assets/Image/" + spriteName);
	}

	public new void LoadAsync(string spriteName)
	{
		base.LoadAsync("Assets/Image/" + spriteName);
	}
}

}
