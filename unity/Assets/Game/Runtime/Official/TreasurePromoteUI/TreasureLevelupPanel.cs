// Complete original promotion controller; explicit saved-player, parser and window ports.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasurePromoteUI {
using Object=UnityEngine.Object;
using System;
using System.Collections.Generic;








using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TreasureLevelupPanel : MonoBehaviour
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	[Serializable]
	private class LevelInfo
	{
		[SerializeField]
		private WorldMultiLanguageText mCurrentLevel;

		[SerializeField]
		private WorldMultiLanguageText mNextLevel;

		[SerializeField]
		private WorldStateSwitcher<bool> mMaxLevelSwitcher;

		public void Show(int lv, bool isMaxLevel)
		{
			mCurrentLevel.SetKey("ui_common_level_style_8", lv);
			mNextLevel.SetKey("ui_common_level_style_8", lv + 1);
			mMaxLevelSwitcher.SetState(isMaxLevel);
		}
	}

	private enum TreasureLevelUpStatus
	{
		Normal,
		Max
	}

	[SerializeField]
	private PlayerUseItemContent mUseItemContent;

	[SerializeField]
	private LevelInfo mLevelInfo;

	[SerializeField]
	private List<ProfessionPlayerPropItem> mProps;

	[SerializeField]
	private List<ProfessionPlayerPropItem> mPropChildren;

	[SerializeField]
	private ProfessionPropEffectPlayAgent mEffectAgent;

	[SerializeField]
	private BlessLevelTextViewChanger mCurLevelTextView;

	[SerializeField]
	private BlessLevelTextViewChanger mNextLevelTextView;

	[SerializeField]
	private WorldStateSwitcher<bool> mMaterialShowStates;

	[SerializeField]
	private Toggle mAutoMergeToggle;

	private IProfessionItemData mItemData;

	private Dictionary<PropType, long> mTmpCurLevelPropsDict = new Dictionary<PropType, long>();

	private Dictionary<PropType, long> mTmpNextLevelPropsDict = new Dictionary<PropType, long>();



	protected void Awake()
	{
		mUseItemContent.SetUseItemBtnHandler(OnLevelBtnClick);
		((UnityEvent<bool>)(object)mAutoMergeToggle.onValueChanged).AddListener((UnityAction<bool>)OnAutoMergeToggleChange);
	}

	private void OnAutoMergeToggleChange(bool isOn) { Services.SetAutoMergeWhenLevelUp(isOn); }

	private void OnEnable()
	{
		Services.Icons.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		mAutoMergeToggle.isOn = Services.AutoMergeWhenLevelUp;
	}

	private void OnDisable()
	{
		if (services != null)
		{
			Services.Icons.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
	}

	private void MainPlayer_OnItemCountUpdate(IProfessionItemData arg1, long arg2, object arg3)
	{
		Services.Icons.GetState(mItemData);
		int treasureFinalLevel = Services.GetTreasureFinalLevel(mItemData.Info.ClassId);
		Dictionary<int, long> levelUpMaterials = Services.GetLevelUpMaterials(treasureFinalLevel+1);
		if ((levelUpMaterials?.Count ?? 0) > 0 && levelUpMaterials.ContainsKey(arg1.Id))
		{
			Refresh();
		}
	}

	public void Show(IProfessionItemData itemData)
	{
		mItemData = itemData;
		foreach (ProfessionPlayerPropItem mProp in mProps)
		{
			((Component)mProp).gameObject.SetActive(false);
		}
		foreach (ProfessionPlayerPropItem mPropChild in mPropChildren)
		{
			((Component)mPropChild).gameObject.SetActive(false);
		}
		((Component)this).gameObject.SetActive(true);
		Refresh();
	}

	private void Refresh()
	{
		ITreasurePromoteServices mainPlayer = Services;
		int treasureFinalLevel = mainPlayer.GetTreasureFinalLevel(mItemData.Info.ClassId);
		int maxTreasureBaseLevel = Services.MaxBaseLevel;
		int num = treasureFinalLevel + 1;
		bool flag = IsReachRankLevelLimit(treasureFinalLevel);
		bool isMaxLevel = Services.IsTreasureMaxLevel(mItemData.Info.ClassId) | flag;
		mLevelInfo.Show(treasureFinalLevel, isMaxLevel);
		bool flag2 = Services.HasBless(num);
		bool isBlessing = Services.HasBless(treasureFinalLevel);
		mNextLevelTextView.Refresh(flag2);
		mCurLevelTextView.Refresh(isBlessing);
		RefreshLevelProps();
		(PlayerUseItemContent.UseItemState, string) useItemState = GetUseItemState();
		if (useItemState.Item1 == PlayerUseItemContent.UseItemState.Limit)
		{
			mUseItemContent.SetLimitKey(useItemState.Item2);
		}
		mUseItemContent.SetUseItemState(useItemState.Item1);
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		if (flag2)
		{
			var treasureBlessInfo = Services.GetBlessInfo(num-maxTreasureBaseLevel);
			if (treasureBlessInfo != null)
			{
				dictionary = treasureBlessInfo.LevelCostItems;
			}
		}
		else
		{
			var treasureLevelInfo = Services.GetLevelInfo(num);
			if (treasureLevelInfo != null)
			{
				dictionary = treasureLevelInfo.Materials;
			}
		}
		if (dictionary != null)
		{
			mUseItemContent.Refresh(dictionary);
			mMaterialShowStates.SetState(dictionary.Count > 0);
		}
		else { Debug.LogError("古遗物找不到升级材料："+mItemData.Info.ClassId+"/"+treasureFinalLevel); }
		bool show = false;

		int num2 = default;
		long num3 = default;
		foreach (KeyValuePair<int, long> item in dictionary)
		{
			item.Deconstruct(out num2, out num3);
			int itemId = num2;
			long num4 = num3;
			long itemAmount = mainPlayer.GetItemAmount(itemId);
			if (itemAmount < num4)
			{
				if (!Services.IsItemSourceFromMerge(itemId))
				{
					show = false;
					break;
				}
				if (itemAmount + Services.CalcCanMergeAmount(itemId, recursion: true) < num4)
				{
					show = false;
					break;
				}
				show = true;
			}
		}
		foreach (ItemIcon itemIcon in mUseItemContent.ItemIconList)
		{
			if (Services.IsItemSourceFromMerge(itemIcon.myInfo.ClassId))
			{
				itemIcon.SetAttachment(ItemIconAttachmentType.MergeTip, show);
			}
		}
	}

	private (PlayerUseItemContent.UseItemState, string) GetUseItemState() => Services.GetLevelUseItemState(mItemData,mAutoMergeToggle.isOn);

	private void RefreshLevelProps()
	{
		TreasureInfo treasureInfo = Services.Tables.GetTreasure(mItemData.Info.ClassId);
		TreasureState itemParamTreasure = Services.Icons.GetState(mItemData);
		int star = itemParamTreasure.Star;
		int treasureFinalLevel = Services.GetTreasureFinalLevel(treasureInfo.ClassId);
		int hitCount = 1;
		bool flag = IsReachRankLevelLimit(treasureFinalLevel);
		bool flag2 = Services.IsTreasureMaxLevel(treasureInfo.ClassId) | flag;
		mTmpCurLevelPropsDict.Clear();
		int treasureBaseLevel = Services.GetTreasureBaseLevel(treasureInfo.ClassId);
		TreasureCalcPropData treasureCalcPropData = TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(Services.Tables,treasureFinalLevel, treasureBaseLevel);
		treasureCalcPropData.AssignBlessLevelPropDict(Services.SubRank);
		bool inSlot = Services.Player.IsTreasureInSlot(treasureInfo.ClassId);
		Services.Calculator.CalcTreasureProps(treasureInfo, star, treasureCalcPropData, hitCount, mTmpCurLevelPropsDict, inSlot, Services.SeasonId);
		int maxTreasureBaseLevel = Services.MaxBaseLevel;
		int baseLevel = ((treasureBaseLevel + 1 > maxTreasureBaseLevel) ? treasureBaseLevel : (treasureBaseLevel + 1));
		TreasureCalcPropData treasureCalcPropData2 = TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(Services.Tables,treasureFinalLevel + 1, baseLevel);
		treasureCalcPropData2.AssignBlessLevelPropDict(Services.SubRank);
		mTmpNextLevelPropsDict.Clear();
		if (!flag2)
		{
			Services.Calculator.CalcTreasureProps(treasureInfo, star, treasureCalcPropData2, hitCount, mTmpNextLevelPropsDict, inSlot, Services.SeasonId);
		}
		bool flag3 = treasureInfo.Effect is TreasureEffectTask;
		int hitCount2 = Services.GetHitCount(treasureInfo,treasureFinalLevel);
		int num = itemParamTreasure?.HitCount ?? 1;
		(PropType, long) effectPropInfo = Services.Properties.GetEffectPropInfo(treasureInfo,Services.Player, star, treasureCalcPropData, hitCount2);
		int levelMaxHitCount = treasureInfo.Effect.GetLevelMaxHitCount(treasureFinalLevel);
		int num2 = (flag2 ? levelMaxHitCount : treasureInfo.Effect.GetLevelMaxHitCount(treasureFinalLevel + 1));
		(PropType, long) tuple = (flag2 ? (0, 0L) : Services.Properties.GetEffectPropInfo(treasureInfo,Services.Player, star, treasureCalcPropData2, Mathf.Min(num, num2)));
		int num3 = (flag3 ? 1 : 0);
		foreach (KeyValuePair<PropType, long> item in mTmpCurLevelPropsDict)
		{
			long value = 0L;
			if (!mTmpNextLevelPropsDict.TryGetValue(item.Key, out value))
			{
				value = -1L;
			}
			if (treasureInfo.EffectPropTypes.Contains(item.Key) & flag3)
			{
				string text = LanguageManager.Instance.GetText(TreasureViewLanguage.GetPropertyNameKey(item.Key));
				mProps[0].Init((ProfessionPropertyType)item.Key, string.Format(LanguageManager.Instance.GetText("treasure_common_total_promote"), text), effectPropInfo.Item2, (tuple.Item1 == 0) ? (-1) : tuple.Item2);
				((Component)mProps[0]).gameObject.SetActive(true);
				mPropChildren[0].Init((ProfessionPropertyType)item.Key, string.Format(LanguageManager.Instance.GetText("treasure_common_single_promote"), text), item.Value, value);
				((Component)mPropChildren[0]).gameObject.SetActive(true);
				mPropChildren[1].Init("treasure_common_hitcount_limit", $"{num}/{levelMaxHitCount}", flag2 ? null : $"{num}/{num2}");
				((Component)mPropChildren[1]).gameObject.SetActive(true);
			}
			else
			{
				mProps[num3].Init((ProfessionPropertyType)item.Key, item.Value, value);
				((Component)mProps[num3]).gameObject.SetActive(true);
				num3++;
			}
		}
	}

	private void OnLevelBtnClick()
    {
        bool isOn=mAutoMergeToggle.isOn;
        int index=Services.GetTreasureInSlotIndex(mItemData.Info.ClassId);
        if(index<0){Debug.LogError("未装备在槽位的古遗物无法强化！treasureClassId = "+mItemData.Info.ClassId);return;}
        int code=Services.CanSlotLevelUp((ElementType)Services.Icons.GetElement(mItemData.Info.ClassId),index,isOn);
        if(code!=0){Services.ShowError(code);return;}
        Services.LevelUp(mItemData,isOn,()=>{mEffectAgent.StartPlayEffect();Refresh();});
    }

	private bool IsReachRankLevelLimit(int finalLevel)
	{
		int maxTreasureBaseLevel = Services.MaxBaseLevel;
		int maxTreasureBlessLevel = Services.MaxBlessLevel;
		return finalLevel >= maxTreasureBaseLevel + maxTreasureBlessLevel;
	}
}

}
