using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.CombatAppearance
{
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;

public class AppearanceCharacter : MonoBehaviour
{
	[SerializeField]
	private AppearancePartHandler mPartHandler;

	private IAppearanceComponent mAppearanceComponent;

	private readonly List<string> mSkinsCache = new List<string>();

	private Dictionary<SkinType, string> mSkinNameDict = new Dictionary<SkinType, string>();

	private Dictionary<string, Dictionary<int, HsvChange>> mSkinDyeMapCache;

	private readonly Dictionary<int, HsvChange> mSetDyeCache = new Dictionary<int, HsvChange>();

    private IProfessionAppearanceSceneCharacterBindings Bindings;
    public void Configure(IProfessionAppearanceSceneCharacterBindings bindings, AppearancePartHandler partHandler)
    {
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        mPartHandler = partHandler;
    }

	public IAppearanceComponent AppearanceComponent => mAppearanceComponent ?? (mAppearanceComponent = ((Component)this).GetComponent<IAppearanceComponent>());

	public void LoadAsset(Sex sex)
	{
		if (!Bindings.TryGetPlayerSkeletonAssets(sex, out var skeletonDataAsset, out var splitAnimationSo))
		{
            Debug.LogError("AppearanceCharacter.LoadAsset 主角骨架未预热. sex:" + sex);
		}
		else
		{
			AppearanceComponent.Init(skeletonDataAsset, splitAnimationSo);
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
				mPartHandler.SetSkins(skinMap);
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
			mPartHandler.Release(tryUnloadAllAssets: true);
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
