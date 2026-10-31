// Migrated selected official client; see partner-gift-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using LanguageUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyLanguage;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipNpcInfo;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System.Collections.Generic;
using UnityEngine;

public class NpcFriendshipPreviewPropItem : UIBase, IGameObjectPoolItem
{
    private IPartnerFriendshipUiServices services;
    public void Configure(IPartnerFriendshipUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        mPropItem.Configure(value.Properties);
    }

	[SerializeField]
	private PlayerPropItem mPropItem;

	[SerializeField]
	private MultiLanguageText mTravelPropText;

	[SerializeField]
	private MultiLanguageText mLevelText;

	[SerializeField]
	private WorldStateSwitcher<bool> mTravelPropStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<bool> mTravelItemStateSwitcher;

	private NpcInfo mNpcInfo;

	private int mLevel;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		IReadOnlyDictionary<PropType, long> readOnlyDictionary;
		if (mLevel > 1)
		{
			readOnlyDictionary = services.GetFriendshipLevelPropDiff(mNpcInfo, mLevel - 1, mLevel);
			if (readOnlyDictionary.Count > 2)
			{
				Debug.LogError("level_prop_npc_friendship.csv: at most two property differences, CurrentLevel:" + (mLevel - 1));
				return;
			}
		}
		else
		{
			readOnlyDictionary = services.GetLevelProps(mNpcInfo.LevelPropId, mLevel);
		}
		bool state = false;
		List<PropType> travelNpcPropTypeList = services.TravelNpcPropTypeList;
		mLevelText.Format(mLevel);
		foreach (KeyValuePair<PropType, long> item in readOnlyDictionary)
		{
			if (item.Value > 0)
			{
				if (travelNpcPropTypeList.Contains(item.Key))
				{
					state = true;
					CultureTextArg cultureTextArg = new CultureTextArg(PropConfigUtils.GetPropTypeName(item.Key));
					CultureTextArg cultureTextArg2 = new CultureTextArg("ui_common_prop_plus", new LanguageUtils.PropValue(item.Key, item.Value, includeUnit: false, includePropDesc: false, -1L));
					mTravelPropText.SetKey("{0}{1}", cultureTextArg, cultureTextArg2);
				}
				else
				{
					mPropItem.Init(item.Key, item.Value, -1L);
					mPropItem.SetNeedSpaceBeforeValue(needSpaceBeforeValue: true);
				}
			}
		}
		mTravelPropStateSwitcher.SetState(state);
		bool state2 = !services.IsTravelFunctionLimited;
		mTravelItemStateSwitcher.SetState(state2);
	}

	public void Init(NpcInfo npcInfo, int level)
	{
		InitWrapper(() =>
		{
			mNpcInfo = npcInfo;
			mLevel = level;
		});
	}

	private (PropType propType, long value) GetFirstProp(IReadOnlyDictionary<PropType, long> propDict)
	{
		foreach (KeyValuePair<PropType, long> item in propDict)
		{
			if (item.Value > 0)
			{
				return (propType: item.Key, value: item.Value);
			}
		}
		Debug.LogError("level_prop_npc_friendship.csv: no positive property change, NpcId:" + mNpcInfo.Id + ", Level:" + mLevel);
		return (propType: PropType.None, value: 0L);
	}

	public void SetBgVisible(bool value)
	{
		mPropItem.SetBgVisible(value);
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
