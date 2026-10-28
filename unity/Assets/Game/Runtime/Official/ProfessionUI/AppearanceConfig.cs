namespace Zjcs.Official.ProfessionUI
{
using System.Collections.Generic;
using UnityEngine;

public class AppearanceConfig : ScriptableObject
{
	[SerializeField]
	private List<PlayerSkinType> mPlayerSkinTypes;

	[SerializeField]
	private List<AppendSkinConfig> mAppendSkinConfigs;

	[SerializeField]
	private List<AppearancePartConfig> mPartConfigs;

	[SerializeField]
	private List<AppearanceOverrideConfig> mAppearanceOverrideConfigs;

	[SerializeField]
	private List<AppearanceHashValue> mAppearanceHashConfigs;

	[SerializeField]
	private List<AppearanceSkeletonPath> mAppearanceSkeletonPaths;

	public Color MimicsMonsterColor;

	private Dictionary<string, AppearanceSkeletonPath> mAppearanceSkeletonPathDict;

	public IReadOnlyList<PlayerSkinType> PlayerSkinTypes => mPlayerSkinTypes;

	public IReadOnlyList<AppearanceHashValue> AppearanceToHash => mAppearanceHashConfigs;

	public string GetSkeletonPath(string skeletonName)
	{
		if (mAppearanceSkeletonPathDict == null)
		{
			mAppearanceSkeletonPathDict = new Dictionary<string, AppearanceSkeletonPath>();
			foreach (AppearanceSkeletonPath mAppearanceSkeletonPath in mAppearanceSkeletonPaths)
			{
				mAppearanceSkeletonPathDict.Add(mAppearanceSkeletonPath.SkeletonName, mAppearanceSkeletonPath);
			}
		}
		if (!mAppearanceSkeletonPathDict.TryGetValue(skeletonName, out var value))
		{
			return string.Empty;
		}
		return value.Path;
	}

	public PlayerSkinType GetPlayerSkinType(SkinType skinType)
	{
		return mPlayerSkinTypes.Find((PlayerSkinType s) => s.SkinType == skinType);
	}

	public List<string> GetAppendSkins(AppendSkinType appendSkinType)
	{
		foreach (AppendSkinConfig mAppendSkinConfig in mAppendSkinConfigs)
		{
			if (mAppendSkinConfig.AppendSkinType == appendSkinType)
			{
				return mAppendSkinConfig.AppendSkins;
			}
		}
		return null;
	}

	public AppearancePartConfig GetPartConfig(SkinType skinType)
	{
		foreach (AppearancePartConfig mPartConfig in mPartConfigs)
		{
			if (mPartConfig.SkinType == skinType)
			{
				return mPartConfig;
			}
		}
		return null;
	}

	public AppearanceOverrideConfig GetOverrideConfig(AppearanceOverrideType type)
	{
		foreach (AppearanceOverrideConfig mAppearanceOverrideConfig in mAppearanceOverrideConfigs)
		{
			if (mAppearanceOverrideConfig.Type == type)
			{
				return mAppearanceOverrideConfig;
			}
		}
		return null;
	}

	public void ClearAppearanceHash(in List<string> skinName)
	{
		for (int num = mAppearanceHashConfigs.Count - 1; num >= 0; num--)
		{
			if (skinName.Contains(mAppearanceHashConfigs[num].SkinName))
			{
				mAppearanceHashConfigs.RemoveAt(num);
			}
		}
	}

	public void AddAppearanceHash(Dictionary<string, long> appearanceToHash)
	{
		foreach (KeyValuePair<string, long> item in appearanceToHash)
		{
			mAppearanceHashConfigs.Add(new AppearanceHashValue
			{
				SkinName = item.Key,
				HashValue = item.Value
			});
		}
		mAppearanceHashConfigs.Sort((AppearanceHashValue a1, AppearanceHashValue a2) => a1.SkinName.CompareTo(a2.SkinName));
	}
}

}
