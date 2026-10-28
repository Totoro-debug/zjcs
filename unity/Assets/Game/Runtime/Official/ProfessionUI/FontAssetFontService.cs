namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class FontAssetFontService : IFontAssetFontService
{
	public const string kFontAssetFontConfigPath = "Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json";

	protected readonly IProfessionImageResourceLoader mResourceLoader;

	private readonly Func<string, FontAssetFontConfig> mDeserialize;

	private FontAssetFontConfig mConfig;

	private IProfessionAssetOperation<TMP_FontAsset> mDefaultFontAssetHandle;

	private IProfessionAssetOperation<Font> mDefaultFontHandle;

	private Dictionary<string, IProfessionAssetOperation<Font>> mFontHandles = new Dictionary<string, IProfessionAssetOperation<Font>>();

	protected FontAssetFontConfig Config
	{
		get
		{
			if (mConfig == null)
			{
				IProfessionAssetOperation<TextAsset> assetOperationHandle = mResourceLoader.LoadAsset<TextAsset>("Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json");
				string text = assetOperationHandle.Result.text;
				assetOperationHandle.Dispose();
				mConfig = mDeserialize(text);
			}
			return mConfig;
		}
	}

	public FontAssetFontService(IProfessionImageResourceLoader resourceLoader, Func<string, FontAssetFontConfig> deserialize)
	{
		mResourceLoader = resourceLoader;
		mDeserialize = deserialize;
	}

	public void Dispose()
	{
		foreach (IProfessionAssetOperation<Font> value in mFontHandles.Values)
		{
			value.Dispose();
		}
		mFontHandles.Clear();
		if (mDefaultFontHandle != null)
		{
			mDefaultFontHandle.Dispose();
			mDefaultFontHandle = null;
		}
		if (mDefaultFontAssetHandle != null)
		{
			mDefaultFontAssetHandle.Dispose();
			mDefaultFontAssetHandle = null;
		}
	}

	public void Initialize()
	{
		mDefaultFontAssetHandle = mResourceLoader.LoadAsset<TMP_FontAsset>(Config.DefaultFontAsset);
		SetupFont(mDefaultFontAssetHandle.Result, (IProfessionAssetOperation<Font> handle) =>
		{
			mDefaultFontHandle = handle;
		});
	}

	public void SetupFont(TMP_FontAsset fontAsset)
	{
		SetupFont(fontAsset, (IProfessionAssetOperation<Font> handle) =>
		{
			mFontHandles.Add(((Object)fontAsset).name, handle);
		});
	}

	protected virtual void SetupFont(TMP_FontAsset fontAsset, Action<IProfessionAssetOperation<Font>> setFontHandle)
	{




		if ((Object)(object)fontAsset.sourceFontFile != (Object)null)
		{
			return;
		}
		string name = ((Object)fontAsset).name;
		if (!Config.FontAssetInfoDict.TryGetValue(name, out var value))
		{
			Debug.LogError("未找到字体资源(" + name + ")的配置");
			return;
		}
		IProfessionAssetOperation<Font> assetOperationHandle = mResourceLoader.LoadAsset<Font>(value.FontPath);
		setFontHandle(assetOperationHandle);
		typeof(TMP_FontAsset).GetField("m_SourceFontFile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fontAsset, assetOperationHandle.Result);
		Debug.Log(name + "使用加载进内存的字体: " + assetOperationHandle.Result);
	}

	public void ReleaseFont(string name)
	{
		if (mFontHandles.TryGetValue(name, out var value))
		{
			mFontHandles.Remove(name);
			value.Dispose();
		}
	}

	public virtual string GetSourceFontFileName(TMP_FontAsset fontAsset)
	{
		return ((Object)fontAsset.sourceFontFile).name;
	}
}

}
