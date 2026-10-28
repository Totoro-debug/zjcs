namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public class AppearanceCharacter : MonoBehaviour
{
	[SerializeField]
	private Component mPartHandler;

	private IAppearanceComponent mAppearanceComponent;

	private readonly List<string> mSkinsCache = new List<string>();

	private Dictionary<SkinType, string> mSkinNameDict = new Dictionary<SkinType, string>();

	private Dictionary<string, Dictionary<int, HsvChange>> mSkinDyeMapCache;

	private readonly Dictionary<int, HsvChange> mSetDyeCache = new Dictionary<int, HsvChange>();

	private IDisposable mDisposable;

	private IDisposable mDisposableSplitAnimationSo;

    private IProfessionAppearanceCharacterBindings mBindings;
    private IProfessionAppearanceCharacterBindings Bindings => mBindings ?? ProfessionAppearanceCharacterBindings.Required;
    public void Configure(IProfessionAppearanceCharacterBindings bindings) { mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings)); }

	public IAppearanceComponent AppearanceComponent => mAppearanceComponent ?? (mAppearanceComponent = ((Component)this).GetComponent<IAppearanceComponent>());

	private void OnDestroy()
	{
		mDisposable?.Dispose();
		mDisposableSplitAnimationSo?.Dispose();
	}

	public void LoadAsset(Sex sex)
	{
		mDisposable?.Dispose();
		string playerSkeletonAssetPath = ProfessionAppearanceCharacterPaths.GetPlayerSkeletonAssetPath(sex);
        IProfessionAppearanceCharacterAsset assetOperationHandle = (IProfessionAppearanceCharacterAsset)(mDisposable = Bindings.LoadAsset(playerSkeletonAssetPath));
        IProfessionAppearanceCharacterAsset assetOperationHandle2 = (IProfessionAppearanceCharacterAsset)(mDisposableSplitAnimationSo = Bindings.LoadAsset(ProfessionAppearanceCharacterPaths.GetPlayerSplitAnimationSoPath(sex)));
        ScriptableObject result = assetOperationHandle.Result;
        ScriptableObject result2 = assetOperationHandle2.Result;
		if (!result)
		{
            Debug.LogError("not exist character data");
		}
		else
		{
			AppearanceComponent.Init(result, result2);
		}
	}

	public void SetRuntimeData(AppearanceRuntimeData data)
	{
		LoadAsset(data.Sex);
		SetSkins(data.SkinMap, data.DyeMap, data.AppendSkins);
	}

	public bool TryGetSkinName(SkinType skinType, out string skinName)
	{
		return mSkinNameDict.TryGetValue(skinType, out skinName);
	}

	private bool CheckAndSetupCache(Dictionary<SkinType, string> skinMap, Dictionary<SkinType, Dictionary<int, HsvChange>> dyeMap = null, List<string> appendSkins = null)
	{
		if (!CheckValid())
		{
			return false;
		}
		if (dyeMap != null)
		{
			if (mSkinDyeMapCache == null)
			{
				mSkinDyeMapCache = new Dictionary<string, Dictionary<int, HsvChange>>();
			}
			mSkinDyeMapCache.Clear();
		}
		mSkinsCache.Clear();
		mSkinNameDict.Clear();
		AppearanceConfig value = Bindings.Config;
		SkinType skinType = default;
		string text = default;
		foreach (KeyValuePair<SkinType, string> item in skinMap)
		{
			skinType = item.Key; text = item.Value;
			SkinType skinType2 = skinType;
			string text2 = text;
			if (text2 == "emptySkin" || value.GetPartConfig(skinType2) != null)
			{
				continue;
			}
			mSkinsCache.Add(text2);
			mSkinNameDict.Add(skinType2, text2);
			if (dyeMap == null || !dyeMap.TryGetValue(skinType2, out var value2))
			{
				continue;
			}
			Dictionary<int, HsvChange> dictionary = new Dictionary<int, HsvChange>(value2.Count);
			foreach (KeyValuePair<int, HsvChange> item2 in value2)
			{
				dictionary.Add(item2.Key, item2.Value);
			}
			mSkinDyeMapCache[text2] = dictionary;
		}
		if (appendSkins != null && appendSkins.Count > 0)
		{
			mSkinsCache.AddRange(appendSkins);
		}
		return true;
	}

	private void SetDyeByCache()
	{
		if (mSkinDyeMapCache == null)
		{
			return;
		}
		string text = default;
		Dictionary<int, HsvChange> dictionary = default;
		foreach (KeyValuePair<string, Dictionary<int, HsvChange>> item in mSkinDyeMapCache)
		{
			text = item.Key; dictionary = item.Value;
			string skinName = text;
			Dictionary<int, HsvChange> hsvChangeDic = dictionary;
			AppearanceComponent.SetDye(skinName, hsvChangeDic);
		}
	}

	public void SetSkins(Dictionary<SkinType, string> skinMap, Dictionary<SkinType, Dictionary<int, HsvChange>> dyeMap = null, List<string> appendSkins = null)
	{
		Release();
		if (CheckAndSetupCache(skinMap, dyeMap, appendSkins))
		{
			AppearanceComponent.SetSkins(mSkinsCache);
			SetDyeByCache();
			if ((Object)(object)mPartHandler != (Object)null)
			{
				Bindings.SetPartSkins(mPartHandler, skinMap);
			}
		}
	}

	public void SetDye(string skinName, int channel, HsvChange hsvChange)
	{
		if (CheckValid())
		{
			mSetDyeCache.Clear();
			mSetDyeCache[channel] = hsvChange;
			AppearanceComponent.SetDye(skinName, mSetDyeCache);
		}
	}

	public void Release(bool tryUnloadAllAssets = false)
	{
		mSkinsCache.Clear();
		mSkinNameDict.Clear();
		mSkinDyeMapCache?.Clear();
		AppearanceComponent?.Release(tryUnloadAllAssets);
		if (tryUnloadAllAssets && (Object)(object)mPartHandler != (Object)null)
		{
			Bindings.ReleaseParts(mPartHandler, tryUnloadAllAssets: true);
		}
	}

	private bool CheckValid()
	{
		if (!AppearanceComponent.Valid)
		{
            Debug.LogError("AppearanceComponent invalid");
			return false;
		}
		return true;
	}
}

}
