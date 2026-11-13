// Selected original client methods; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using FastHangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowFastHangUpInfo;
using Player = Zjcs.Official.HomeHangUpWindowsUI.IHomeHangUpWindowPlayer;
namespace Zjcs.Official.HomeHangUpWindowsUI
{

public class FurnitureOutputInfo : MonoBehaviour
{
    private IHomeFurnitureOutputServices outputServices;
    public IHomeHangUpWindowServices Services { set => outputServices = new HomeFurnitureOutputServices(value); }
    public IHomeFurnitureOutputServices OutputServices { get => outputServices; set => outputServices = value; }
	[SerializeField]
	private MultiLanguageText mFromLevelTxt;

	[SerializeField]
	private MultiLanguageText mToLevelTxt;

	[SerializeField]
	private WorldStateSwitcher<bool> mMaxLevelStateSwitcher;

	[SerializeField]
	private PlayerPropItem mBaseOutputPropItem;

	[SerializeField]
	private PlayerPropItem mAddSumPropItem;

	[SerializeField]
	private PlayerPropItem mBranchExplorePropItem;

	[SerializeField]
	private PlayerPropItem mTreasurePropItem;

	[SerializeField]
	private PlayerPropItem mMouthCardPropItem;

	[SerializeField]
	private PlayerPropItem mAstrologicalPropItem;

	[SerializeField]
	private PlayerPropItem mSumOutputPropItem;

	[SerializeField]
	private PlayerPropItem mSumStorePropItem;

	[SerializeField]
	private PropUpgradeEffectAgent mEffectAgent;

	[SerializeField]
	private List<EPropHandleType> mBranchExploreEPropHandleTypes = new List<EPropHandleType>
	{
		EPropHandleType.LostGoddess,
		EPropHandleType.Extra
	};

	private Dictionary<PropType, long> mBranchExplorePropValue = new Dictionary<PropType, long>();

	private List<PropType> mTitleTotalProps = new List<PropType>();

	public List<EPropHandleType> BranchExploreEPropHandleTypes => mBranchExploreEPropHandleTypes;
    public IReadOnlyList<PlayerPropItem> OutputItems => new[] { mBaseOutputPropItem, mAddSumPropItem,
        mBranchExplorePropItem, mTreasurePropItem, mMouthCardPropItem, mAstrologicalPropItem,
        mSumOutputPropItem, mSumStorePropItem };

	private void Awake()
	{
		if (mEffectAgent != null)
		{
			mEffectAgent.AddPropItem(mBaseOutputPropItem);
			mEffectAgent.AddPropItem(mSumOutputPropItem);
			mEffectAgent.AddPropItem(mSumStorePropItem);
		}
	}

	public void ShowInfo(int hangUpId, int level)
	{
		OnShowInfo(hangUpId, level);
	}

	public void ShowLevelUpInfo(int hangUpId, bool isLevelMax, int fromLevel, int toLevel, bool instant)
	{
		OnShowLevelUpInfo(hangUpId, isLevelMax, fromLevel, toLevel, instant);
		if (!instant)
		{
			OnPlayLevelUpEffect();
		}
	}

	protected virtual void OnShowInfo(int hangUpId, int level)
	{
		if (mFromLevelTxt != null)
		{
			mFromLevelTxt.Format(level);
		}
		HangUpInfo hangUpInfo = OutputServices.GetHangUpInfo(hangUpId);
		double hangUpReward = OutputServices.GetHangUpReward(hangUpId, 1.0, level, isBase: true);
		if (hangUpInfo.BindFurniture == EFurniture.Cart)
		{
			mBaseOutputPropItem.Init(OutputServices.GetHangUpBaseOutput(hangUpId), GetPropValue(hangUpReward, OutputServices.FormatCount));
		}
		else
		{
			mBranchExplorePropValue.Clear();
			OutputServices.PropertyCenter.GetProps(mBranchExploreEPropHandleTypes, mBranchExplorePropValue);
			_ = mBranchExplorePropValue;
			double prop = OutputServices.GetProp(mBranchExplorePropValue, hangUpInfo.EffectPercentPropType);
			double value = CalcSumSpeed(hangUpReward, 0.0, prop + 1.0);
			mBaseOutputPropItem.Init(OutputServices.GetHangUpBaseOutput(hangUpId), GetPropValue(value, OutputServices.FormatCount));
		}
		if (mSumOutputPropItem != null)
		{
			OutputServices.GetHangUpProp(hangUpInfo, OutputServices.NonBattleProps, out var addPercent, out var addFixed);
			double value2 = CalcSumSpeed(hangUpReward, addFixed, addPercent);
			mSumOutputPropItem.Init(PropConfigUtils.GetPropTypeName(hangUpInfo.EffectFixedPropType), GetPropValue(value2, OutputServices.FormatCount));
		}
		if (mSumStorePropItem != null)
		{
			long hangUpMaxRewardStore = OutputServices.GetHangUpMaxRewardStore(hangUpId);
			mSumStorePropItem.Init(hangUpInfo.MaxStorePropType, hangUpMaxRewardStore, -1L);
		}
		SetAddProp(hangUpId);
	}

	protected virtual void OnShowLevelUpInfo(int hangUpId, bool isLevelMax, int fromLevel, int toLevel, bool instant)
	{
		HangUpInfo hangUpInfo = OutputServices.GetHangUpInfo(hangUpId);
		OutputServices.GetHangUpProp(hangUpInfo, OutputServices.NonBattleProps, out var addPercent, out var addFixed);
		long value = 0L;
		double num = 0.0;
		double value2 = 0.0;
		if (fromLevel > 0)
		{
			num = OutputServices.GetHangUpReward(hangUpId, 1.0, fromLevel, isBase: true);
			value = OutputServices.GetHangUpMaxRewardStore(hangUpId, fromLevel);
			value2 = CalcSumSpeed(num, addFixed, addPercent);
		}
		long nextValue = -1L;
		double num2 = -1.0;
		double value3 = -1.0;
		if (!isLevelMax)
		{
			num2 = (long)(OutputServices.GetHangUpReward(hangUpId, 1.0, toLevel, isBase: true) + 9.999999747378752E-06);
			nextValue = OutputServices.GetHangUpMaxRewardStore(hangUpId, toLevel);
			value3 = CalcSumSpeed(num2, addFixed, addPercent);
		}
		mMaxLevelStateSwitcher.SetState(isLevelMax);
		if (mFromLevelTxt != null)
		{
			mFromLevelTxt.Format(fromLevel);
		}
		if (mToLevelTxt != null && !isLevelMax)
		{
			mToLevelTxt.Format(toLevel);
		}
		mBaseOutputPropItem.Init(OutputServices.GetHangUpBaseOutput(hangUpId), GetPropValue(num, OutputServices.FormatCount), GetPropValue(num2, OutputServices.FormatCount), instant);
		mSumOutputPropItem.Init(PropConfigUtils.GetPropTypeName(hangUpInfo.EffectFixedPropType), GetPropValue(value2, OutputServices.FormatCount), GetPropValue(value3, OutputServices.FormatCount), instant);
		mSumStorePropItem.Init(hangUpInfo.MaxStorePropType, value, nextValue, instant);
		SetAddProp(hangUpId);
	}

	private void OnPlayLevelUpEffect()
	{
		mEffectAgent.StartPlayEffect();
	}

	private void SetAddProp(int hangUpId)
	{
		HangUpInfo hangUpInfo = OutputServices.GetHangUpInfo(hangUpId);
		PropType effectFixedPropType = hangUpInfo.EffectFixedPropType;
		PropType effectPercentPropType = hangUpInfo.EffectPercentPropType;
		mTitleTotalProps.Clear();
		if (mBranchExplorePropItem != null)
		{
			SetProp(mBranchExplorePropItem, "hangup_output_add_feeder_explore", mBranchExploreEPropHandleTypes, effectFixedPropType, effectPercentPropType, ref mBranchExplorePropValue);
		}
		if (mAstrologicalPropItem != null)
		{
			SetProp(mAstrologicalPropItem, "hangup_output_add_astrological", EPropHandleType.Astrological, effectFixedPropType, PropType.AddFinallyAwardExpPercent);
		}
		if (hangUpInfo.EffectPercentPropType == PropType.HangUpExpAddPercent)
		{
			mTitleTotalProps.Add(PropType.AddFinallyAwardExpPercent);
		}
		else
		{
			mTitleTotalProps.Add(effectPercentPropType);
		}
		if (hangUpInfo.BindFurniture == EFurniture.Cart)
		{
			SetProp(mMouthCardPropItem, "UI_Pay_PeriodicCard", EPropHandleType.PeriodIcCar, PropType.None, PropType.HangUpCartAllPercent);
			SetProp(mTreasurePropItem, "hangup_output_add_treasure", EPropHandleType.Treasure, effectFixedPropType, effectPercentPropType);
			mTitleTotalProps.Add(PropType.HangUpCartAllPercent);
		}
		else
		{
			SetProp(mMouthCardPropItem, "UI_Pay_PeriodicCard", EPropHandleType.PeriodIcCar, effectFixedPropType, PropType.AddFinallyAwardExpPercent);
			SetProp(mTreasurePropItem, "hangup_output_add_treasure", EPropHandleType.Treasure, effectFixedPropType, PropType.AddFinallyAwardExpPercent);
		}
		SetProp(mAddSumPropItem, "hangup_output_add_title", OutputServices.NonBattleProps, effectFixedPropType, mTitleTotalProps.ToArray());
	}

	private void SetProp(PlayerPropItem propItem, string propName, EPropHandleType handleType, PropType speedProp, PropType percentProp)
	{
		Dictionary<PropType, long> chunckProps = OutputServices.PropertyCenter.GetChunckProps(handleType);
		SetProp(propItem, propName, chunckProps, speedProp, percentProp);
	}

	private void SetProp(PlayerPropItem propItem, string propName, List<EPropHandleType> handleTypes, PropType speedProp, PropType percentProp, ref Dictionary<PropType, long> props)
	{
		props.Clear();
		OutputServices.PropertyCenter.GetProps(handleTypes, props);
		SetProp(propItem, propName, props, speedProp, percentProp);
	}

	private void SetProp(PlayerPropItem propItem, string propName, IReadOnlyDictionary<PropType, long> props, PropType speedProp, params PropType[] percentProps)
	{
		long num = 0L;
		long value = 0L;
		if (props != null)
		{
			props.TryGetValue(speedProp, out value);
			foreach (PropType key in percentProps)
			{
				props.TryGetValue(key, out var value2);
				num += value2;
			}
		}
		propItem.Init(propName, GetComplexValueStr(value, num));
	}

	public static double CalcSumSpeed(double baseSpeed, double addSpeed, double addPercent)
	{
		return (baseSpeed + addSpeed) * addPercent;
	}

	public static string GetPropValue(double value, Func<long, string> formatter)
	{
		if (value < 1.0)
		{
			return Math.Round(value, 1).ToString(CultureInfo.InvariantCulture);
		}
		return formatter((long)Math.Round(value, 1));
	}

	private string GetComplexValueStr(long speed, long percent)
	{
		if (speed != 0L)
		{
			if (percent != 0L)
			{
				return OutputServices.TextFormatted("hang_up_prop_add_sum_speed_and_percent", GetPercentStr(percent), speed);
			}
			return speed.ToString();
		}
		if (percent == 0L)
		{
			return "0";
		}
		return OutputServices.TextFormatted("ui_common_percent", GetPercentStr(percent));
	}

	private string GetPercentStr(long percent)
	{
		return ((float)percent / 100f).ToString("0.#");
	}
}

}
