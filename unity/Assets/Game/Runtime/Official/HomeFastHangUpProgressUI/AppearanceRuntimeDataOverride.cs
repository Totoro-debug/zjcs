// Selected original client; local ownership and native animation endpoints.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;
using AppearanceUI = Zjcs.Official.CombatAppearance.AppearanceUI;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpProgressUI
{


public class AppearanceRuntimeDataOverride : IAppearanceRuntimeDataOverride
{
	private List<int> mMaleOverrideList;
    private readonly IHomeFastHangUpProgressServices services;

	private List<int> mFemaleOverrideList;

	private readonly HashSet<int> mEffectiveSkinIds = new HashSet<int>();

	public AppearanceRuntimeDataOverride(IHomeFastHangUpProgressServices services, List<int> maleOverrideList, List<int> femaleOverrideList)
	{
        this.services = services;
		UpdateOverride(maleOverrideList, femaleOverrideList);
	}

	public AppearanceRuntimeDataOverride(IHomeFastHangUpProgressServices services, IEnumerable<int> overrideList)
	{
		List<int> list = overrideList.ToList();
        this.services = services;
		UpdateOverride(list, list);
	}

	public void ApplyOverride(AppearanceRuntimeData runtimeData, object appearanceData, Dictionary<SkinType, string> modifiedSkinDict)
	{
		if (runtimeData == null)
		{
			return;
		}
		List<int> runtimeData2 = GetRuntimeData(runtimeData.Sex);
		if (runtimeData2 == null)
		{
			return;
		}
		modifiedSkinDict?.Clear();
		services.AppearanceResolver.ResolveAppendAppearanceMutex((AppearanceData)appearanceData, runtimeData.Sex, mEffectiveSkinIds);
		SpineSkinInfoParser spineSkinInfoParser = services.AppearanceTables.SpineSkinInfoParser;
		foreach (int item in runtimeData2)
		{
			SpineSkinInfo spineSkinInfo = spineSkinInfoParser.Get(item);
			SkinType skinType = spineSkinInfo.SkinType;
			if (!services.AppearanceResolver.IsFilterSkinId(mEffectiveSkinIds, item))
			{
				if (modifiedSkinDict != null)
				{
					modifiedSkinDict[skinType] = (runtimeData.SkinMap.TryGetValue(skinType, out var value) ? value : "emptySkin");
				}
				runtimeData.SkinMap[skinType] = spineSkinInfo.SkinName;
			}
		}
	}

	public void UpdateOverride(List<int> maleOverrideList, List<int> femaleOverrideList)
	{
		mMaleOverrideList = maleOverrideList;
		mFemaleOverrideList = femaleOverrideList;
	}

	private List<int> GetRuntimeData(Sex sex)
	{
		if (sex != Sex.Male)
		{
			return mFemaleOverrideList;
		}
		return mMaleOverrideList;
	}

	public static AppearanceRuntimeDataOverride CreateAppearanceOverride(IHomeFastHangUpProgressServices services, List<int> maleOverrideList, List<int> femaleOverrideList)
	{
		return new AppearanceRuntimeDataOverride(services, maleOverrideList, femaleOverrideList);
	}
}

}
