namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using System;
using System.Collections.Generic;

public class CharacterPortraitTexturePool : IDisposable
{
	private const int kLeastKeepCount = 3;

	private readonly Dictionary<int, Stack<CharacterPortraitTexture2D>> mTexturePool = new Dictionary<int, Stack<CharacterPortraitTexture2D>>();

	private readonly ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();

	public CharacterPortraitTexturePool(GAction lowMemory)
	{
		mDisposableBag.Add(lowMemory.Subscribe(ForceReleaseAllResourceCache));
	}

	public void Dispose()
	{
		mDisposableBag.Dispose();
	}

	private int GetHash(int width, int height)
	{
		return (width << 16) | height;
	}

	public CharacterPortraitTexture2D GetTexture(int width, int height)
	{
		int hash = GetHash(width, height);
		if (!mTexturePool.TryGetValue(hash, out var value))
		{
			value = new Stack<CharacterPortraitTexture2D>();
			mTexturePool[hash] = value;
		}
		CharacterPortraitTexture2D result = default;
		if (!value.TryPop(out result))
		{
			return new CharacterPortraitTexture2D(width, height);
		}
		return result;
	}

	public void Recycle(CharacterPortraitTexture2D texPair)
	{
		int hash = GetHash(texPair.Width, texPair.Height);
		mTexturePool[hash].Push(texPair);
	}

	public void ReleaseResourcesCache()
	{
		CharacterPortraitTexture2D characterPortraitTexture2D = default;
		foreach (Stack<CharacterPortraitTexture2D> value in mTexturePool.Values)
		{
			if (value.Count <= 3)
			{
				continue;
			}
			if (!value.TryPop(out characterPortraitTexture2D))
			{
				break;
			}
			if (characterPortraitTexture2D != null)
			{
				characterPortraitTexture2D.DestroyTexture();
				Debug.Log("角色肖像系统清理Texture资源， 释放内存");
			}
		}
	}

	private void ForceReleaseAllResourceCache()
	{
		CharacterPortraitTexture2D characterPortraitTexture2D = default;
		foreach (Stack<CharacterPortraitTexture2D> value in mTexturePool.Values)
		{
			while (value.TryPop(out characterPortraitTexture2D))
			{
				characterPortraitTexture2D.DestroyTexture();
			}
		}
		Debug.Log("低内存强制清理所有Texture资源");
	}
}

}
