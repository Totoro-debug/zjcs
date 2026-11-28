using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.HomePlayerInfoUI;
using PlayerCostMoneyContent=Zjcs.Official.HomeCurrencyWindowsUI.PlayerCostMoneyContent;
using IntroductionPopBtn=Zjcs.Official.HomePlayerInfoUI.HomeIntroductionPopBtn;
using TreasureState=Zjcs.Official.TreasureCultivation.TreasureState;
using PropType=System.Int32;
using SubRank=System.Int32;
using Rank=System.Int32;
using Object=UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

[Serializable]
public class TreasureProps
{
    private ITreasurePropertyViewServices Services;
    public void Configure(ITreasurePropertyViewServices services) => Services=services??throw new ArgumentNullException(nameof(services));

	private enum OperationTreasureState
	{
		None,
		Unlock,
		Lock
	}

	[Header("元素亲和")]
	[SerializeField]
	private ElementPropItem mElementPropItem;

	[SerializeField]
	private ProfessionBlessPropBtn mElementPropBlessBtn;

	[SerializeField]
	private WorldMultiLanguageText mExtraValueText;

	[SerializeField]
	private WorldMultiLanguageText mFixedPropValueText;

	[SerializeField]
	private TreasureNpcEffectContent mNpcEffectContent;

	[SerializeField]
	private WorldMultiLanguageText mTriggerPropValueText;

	[Header("效果")]
	[SerializeField]
	private List<WorldMultiLanguageText> mPropItems;

	[SerializeField]
	private ProfessionBlessPropBtn mScorePropBtn;

	[SerializeField]
	private ProfessionBlessPropBtn mTaskScorePropBtn;

	[SerializeField]
	private WorldStateSwitcher<OperationTreasureState> LockState;

	protected int TreasureClassId;

	protected ITreasurePropProvider mTreasurePropProvider;

	private bool mCanShowNpcTreasure = true;

	private bool mCanShowState;

	private bool mForceInSlot;

	protected TreasureInfo TreasureInfo => mTreasurePropProvider.Info();

	public void SetTreasure(int classId)
	{
		SetTreasure(classId, new TreasureDummyPropProvider(Services.Tables,Services.Player,classId));
	}

	public void SetTreasure(int classId, ITreasurePropProvider provider)
	{
		TreasureClassId = classId;
		mTreasurePropProvider = provider;
		mForceInSlot = false;
		RefreshProps();
	}

	public void SetForceInSlot()
	{
		if (!mForceInSlot && mTreasurePropProvider != null)
		{
			mForceInSlot = true;
			mTreasurePropProvider = new TreasureInSlotPropProvider(mTreasurePropProvider);
			RefreshProps();
		}
	}

	private OperationTreasureState GetOperationTreasureState()
	{
		if (!mCanShowState)
		{
			return OperationTreasureState.None;
		}
		IProfessionItemData itemById = Services.GetItemById(TreasureInfo.ClassId);
		if (itemById != null && Services.GetState(itemById).Active)
		{
			return OperationTreasureState.Unlock;
		}
		return OperationTreasureState.Lock;
	}

	public void RefreshProps()
	{
		float treasureSlotPropAddRate = Services.Tables.TreasureSlotPropAddRate;
		int star = mTreasurePropProvider.Star();
		int num = mTreasurePropProvider.Level();
		int num2 = mTreasurePropProvider.BlessLevel();
		int baseLevel = mTreasurePropProvider.BaseLevel();
		bool flag = mTreasurePropProvider.InSlot();
		int hitCount = mTreasurePropProvider.GetHitCount();
		SubRank subRank = mTreasurePropProvider.SubRank();
		Rank rank = Services.GetRank(subRank);
		TreasureCalcPropData calcPropData = TreasureCalcPropData.CreateCalcPropDataByBaseLevel(Services.Tables,baseLevel);
		TreasureCalcPropData treasureCalcPropData = TreasureCalcPropData.CreateCalcPropDataByFinalLevelAndBaseLevel(Services.Tables,num,baseLevel);
		treasureCalcPropData.AssignBlessLevelPropDict(subRank);
		(PropType, long) elementPropInfo = Services.Properties.GetElementPropInfo(TreasureInfo,Services.Player, star, treasureCalcPropData, hitCount, flag);
		long num3 = elementPropInfo.Item2;
		if ((Object)(object)mExtraValueText != (Object)null)
		{
			long num4 = (long)Math.Floor((double)elementPropInfo.Item2 / (1.0 / (double)treasureSlotPropAddRate + 1.0));
			bool flag2 = flag && num4 > 0;
			((Component)mExtraValueText).gameObject.SetActive(flag2);
			if (flag2)
			{
				mExtraValueText.SetKey("+{0}", num4.ToString());
				num3 -= num4;
			}
		}
		mElementPropItem.Init((ProfessionPropertyType)elementPropInfo.Item1, num3, -1L);
		if ((Object)(object)mElementPropBlessBtn != (Object)null)
		{
			((Component)mElementPropBlessBtn).gameObject.SetActive(num2 > 0);
			if (num2 > 0)
			{
				long item = Services.Properties.GetElementPropInfo(TreasureInfo,Services.Player, star, calcPropData, hitCount, flag).PropValue;
				long num5 = elementPropInfo.Item2 - item;
				if (num5 > 0)
				{
					mElementPropBlessBtn.Init(rank,rank,(ProfessionPropertyType)elementPropInfo.Item1, item, num5, hasBless: true);
				}
			}
		}
		foreach (WorldMultiLanguageText mPropItem in mPropItems)
		{
			((Component)((Component)mPropItem).transform.parent).gameObject.SetActive(false);
		}
		if (TreasureInfo.Effect is TreasureEffectTask)
		{
			((Component)((Component)mPropItems[0]).transform.parent).gameObject.SetActive(true);
			(string, object[]) tuple = Services.Descriptions.GenerateDesc(TreasureInfo.Effect,TreasureInfo, mTreasurePropProvider);
			mPropItems[0].SetKey(tuple.Item1, tuple.Item2);
			int levelMaxHitCount = (TreasureInfo.Effect as TreasureEffectTask).GetLevelMaxHitCount(num);
			mPropItems[1].SetKey("{0}/{1}", CultureNumberArg.Compact(hitCount), CultureNumberArg.Compact(levelMaxHitCount));
			((Component)((Component)mPropItems[1]).transform.parent).gameObject.SetActive(true);
			hitCount = Math.Min(hitCount, levelMaxHitCount);
			(PropType, long) effectPropInfo = Services.Properties.GetEffectPropInfo(TreasureInfo,Services.Player, star, treasureCalcPropData, hitCount, flag);
			long num6 = effectPropInfo.Item2;
			if ((Object)(object)mTriggerPropValueText != (Object)null)
			{
				long num7 = (long)Math.Floor((double)num6 / (1.0 / (double)treasureSlotPropAddRate + 1.0));
				bool flag3 = flag && num7 > 0;
				((Component)mTriggerPropValueText).gameObject.SetActive(flag3);
				if (flag3)
				{
					mTriggerPropValueText.SetKey("+{0}", TreasureViewLanguage.FormatPropString(effectPropInfo.Item1, num7, includeUnit: false, includePropDesc: false, -1L));
					num6 -= num7;
				}
			}
			TreasureViewLanguage.PropValue propValue = new TreasureViewLanguage.PropValue(effectPropInfo.Item1, num6, includeUnit: false, includePropDesc: false, -1L);
			mPropItems[2].SetKey("{0}{1}", new CultureTextArg(TreasureViewLanguage.GetPropertyNameKey(effectPropInfo.Item1)), propValue);
			((Component)((Component)mPropItems[2]).transform.parent).gameObject.SetActive(true);
			RefleshCanShowNpcTreasure();
			if ((Object)(object)mTaskScorePropBtn == (Object)null)
			{
				return;
			}
			((Component)mTaskScorePropBtn).gameObject.SetActive(num2 > 0);
			if (num2 > 0)
			{
				long item2 = Services.Properties.GetEffectPropInfo(TreasureInfo,Services.Player, star, calcPropData, hitCount, flag).PropValue;
				long num8 = effectPropInfo.Item2 - item2;
				if (num8 > 0)
				{
					mTaskScorePropBtn.Init(rank,rank,(ProfessionPropertyType)effectPropInfo.Item1, item2, num8, hasBless: true);
				}
			}
			return;
		}
		((Component)((Component)mPropItems[3]).transform.parent).gameObject.SetActive(true);
		(PropType, long) effectPropInfo2 = Services.Properties.GetEffectPropInfo(TreasureInfo,Services.Player, star, treasureCalcPropData, hitCount, flag);
		ITreasurePropProvider propProvider = mTreasurePropProvider;
		if ((Object)(object)mFixedPropValueText != (Object)null)
		{
			propProvider = new TreasureNoSlotPropProvider(mTreasurePropProvider);
			long num9 = (long)Math.Floor((double)effectPropInfo2.Item2 / (1.0 / (double)treasureSlotPropAddRate + 1.0));
			bool flag4 = flag && num9 > 0;
			((Component)mFixedPropValueText).gameObject.SetActive(flag4);
			if (flag4)
			{
				mFixedPropValueText.SetKey("+{0}", new TreasureViewLanguage.PropValue(effectPropInfo2.Item1, num9, includeUnit: false, includePropDesc: false, -1L));
			}
		}
		(string, object[]) tuple2 = Services.Descriptions.GenerateDesc(TreasureInfo.Effect,TreasureInfo, propProvider);
		object[] array = new object[tuple2.Item2.Length];
		for (int i = 0; i < tuple2.Item2.Length; i++)
		{
			array[i] = new TreasureViewLanguage.ReplaceTextArg(tuple2.Item2[i], "+", "");
		}
		mPropItems[3].SetKey(tuple2.Item1, array);
		RefleshCanShowNpcTreasure();
		if ((Object)(object)mScorePropBtn == (Object)null)
		{
			return;
		}
		((Component)mScorePropBtn).gameObject.SetActive(num2 > 0);
		if (num2 > 0)
		{
			long item3 = Services.Properties.GetEffectPropInfo(TreasureInfo,Services.Player, star, calcPropData, hitCount, flag).PropValue;
			long num10 = effectPropInfo2.Item2 - item3;
			if (num10 > 0)
			{
				mScorePropBtn.Init(rank,rank,(ProfessionPropertyType)effectPropInfo2.Item1, item3, num10, hasBless: true);
			}
		}
	}

	public void RefleshCanShowNpcTreasure()
	{
		if (mCanShowNpcTreasure && !((Object)(object)mNpcEffectContent == (Object)null))
		{
			HashSet<int> npcByTreasure = Services.GetNpcByTreasure(TreasureInfo.ClassId);
			if (npcByTreasure != null)
			{
				mNpcEffectContent.Init(npcByTreasure.ToList());
			}
			OperationTreasureState operationTreasureState = GetOperationTreasureState();
			LockState.SetState(operationTreasureState);
		}
	}

	public void SetCanShowNpcTreasureAndState(bool canShow, bool canShowState)
	{
		mCanShowNpcTreasure = canShow;
		mCanShowState = canShowState;
	}
}

}
