// Original selected client implementation; only player/parser/reward ports differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HomePlayerExpSlider : HomeExperiencePlayerUIBase
{
	[SerializeField]
	private Slider mSlider;

	[SerializeField]
	private float mSliderMinValue;

	[SerializeField]
	private float mSliderMaxValue = 1f;

	[SerializeField]
	private float mExpSliderPlayTime = 1f;

	[SerializeField]
	private GameObject mUpdateObj;

	[SerializeField]
	private MultiLanguageText mExpChangeText;

	[SerializeField]
	private MultiLanguageText mExpText;

	[SerializeField]
	private List<MultiLanguageText> mOtherExpTexts;

	[SerializeField]
	private bool mShowExpColor;

	private int mLevel;

	private IHomeExperienceRewards PlayerExpRewardHandler => Bindings.Rewards;

	public int Level
	{
		get
		{
			return mLevel;
		}
		protected set
		{
			if (value != mLevel)
			{
				mLevel = value;
				OnLevelUpdate?.Invoke(mLevel);
			}
		}
	}

	public event Action<int> OnLevelUpdate;

	private void Awake()
	{
		if ((Object)(object)mUpdateObj != (Object)null)
		{
			mUpdateObj.SetActive(false);
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Bindings.MainPlayer.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		Bindings.MainPlayer.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
		IHomeExperiencePlayer mainPlayer = Bindings.MainPlayer;
		mainPlayer.OnBlessLevelDataUpdate += MainPlayer_OnBlessDataUpdate;
		Bindings.MainPlayer.OnSeasonRecordDataAdd += MainPlayer_OnSeasonRecordDataUpdate;
		Bindings.MainPlayer.OnSeasonRecordDataEndUpdate += MainPlayer_OnSeasonRecordDataUpdate;
		long handledExp = PlayerExpRewardHandler.HandledExp;
		long playerRealExp = PlayerExpRewardHandler.PlayerRealExp;
		PlayerExpRewardHandler.RegisterRewardExpDisplay(DoPlayerExp);
		if (handledExp == playerRealExp)
		{
			SetExp(playerRealExp);
		}
		RefreshExpText();
	}

	protected override void OnDisableAndInited()
	{
		if (Bindings.MainPlayer != null)
		{
			Bindings.MainPlayer.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
			Bindings.MainPlayer.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
			IHomeExperiencePlayer mainPlayer = Bindings.MainPlayer;
			mainPlayer.OnBlessLevelDataUpdate -= MainPlayer_OnBlessDataUpdate;
			Bindings.MainPlayer.OnSeasonRecordDataAdd -= MainPlayer_OnSeasonRecordDataUpdate;
			Bindings.MainPlayer.OnSeasonRecordDataEndUpdate -= MainPlayer_OnSeasonRecordDataUpdate;
		}
		PlayerExpRewardHandler.UnregisterRewardExpDisplay(DoPlayerExp);
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnSubRankUpdate(int subRank)
	{
		RefreshExpText();
	}

	private void MainPlayer_OnItemCountUpdate(HomeExperienceItem itemData, long preCount, object sourceData)
	{
		if (itemData.Id == Bindings.PlayerExpItemId)
		{
			RefreshExpText();
		}
	}

	private void MainPlayer_OnBlessDataUpdate()
	{
		RefreshExpText();
	}

	private void MainPlayer_OnSeasonRecordDataUpdate(object dataWrap)
	{
		RefreshExpText();
	}

	private IEnumerator DoPlayerExp(long preExp, long postExp)
	{
		long diff = postExp - preExp;
		if (diff > 0)
		{
			if ((Object)(object)mUpdateObj != (Object)null)
			{
				mUpdateObj.SetActive(true);
			}
			if ((Object)(object)mExpChangeText != (Object)null)
			{
				mExpChangeText.Format(Bindings.Compact(postExp - preExp));
			}
		}
		for (float totalTime = 0f; totalTime < mExpSliderPlayTime; totalTime += Time.deltaTime)
		{
			long exp = OriginalLerp(preExp, postExp, totalTime / mExpSliderPlayTime);
			SetExp(exp);
			yield return null;
		}
		SetExp(postExp);
		if (diff > 0 && (Object)(object)mUpdateObj != (Object)null)
		{
			mUpdateObj.SetActive(false);
		}
	}

	private bool SetExp(long exp)
	{
		IHomeExperiencePlayer mainPlayer = Bindings.MainPlayer;
		IHomeLevelExpParser levelExpParser = Bindings.LevelExpParser;
		int levelLimit = Bindings.MainPlayer.LevelLimit;
		long exp2 = levelExpParser.GetExp(1, levelLimit);
		if (mainPlayer.IsBlessLevelOpen())
		{
			long num = exp - exp2;
			IHomeLevelBlessParser levelBlessInfoParser = Bindings.LevelBlessInfoParser;
			int rank = mainPlayer.Rank;
			int blessLevel = levelBlessInfoParser.GetBlessLevel(rank, num);
			int level = mainPlayer.BaseLevel + blessLevel;
			Level = level;
			mSlider.minValue = Bindings.LevelBlessInfoParser.GetExpRequestByBlessLevel(blessLevel);
			mSlider.maxValue = Bindings.LevelBlessInfoParser.GetExpRequestByBlessLevel(blessLevel + 1);
			SetSliderValue(num);
			return false;
		}
		int num2 = levelExpParser.CalcLevelByExp(1, exp);
		num2 = (Level = Mathf.Min(num2, levelLimit));
		if (Bindings.MainPlayer.ShowPlayerStoreExpLimit())
		{
			mSlider.minValue = levelExpParser.GetExp(1, mLevel);
			mSlider.maxValue = levelExpParser.GetExp(1, Bindings.MainPlayer.GetMaxStoreExpLevel());
		}
		else
		{
			mSlider.minValue = levelExpParser.GetExp(1, mLevel);
			mSlider.maxValue = levelExpParser.GetExp(1, mLevel + 1);
		}
		SetSliderValue(exp);
		return num2 == levelLimit;
	}

	private void SetSliderValue(float value)
	{
		float num = (value - mSlider.minValue) / (mSlider.maxValue - mSlider.minValue);
		float num2 = Mathf.Lerp(mSliderMinValue, mSliderMaxValue, num);
		mSlider.value = Mathf.Lerp(mSlider.minValue, mSlider.maxValue, num2);
	}

	private void RefreshExpText()
	{
		if ((Object)(object)mExpText == (Object)null)
		{
			return;
		}
		IHomeExperiencePlayer mainPlayer = Bindings.MainPlayer;
		Bindings.ResolveSubRank(mainPlayer.SubRank);
		IHomeLevelExpParser levelExpParser = Bindings.LevelExpParser;
		long value = levelExpParser.GetLevelUpExp(1, mainPlayer.Level);
		long itemAmount = mainPlayer.GetItemAmount(Bindings.PlayerExpItemId);
		itemAmount -= levelExpParser.GetExp(1, mainPlayer.Level);
		bool flag = Bindings.MainPlayer.ShowPlayerStoreExpLimit();
		if (flag)
		{
			value = levelExpParser.GetLevelUpExp(1, mainPlayer.Level, Bindings.MainPlayer.GetMaxStoreExpLevel());
		}
		else if (mainPlayer.IsBlessLevelOpen())
		{
			if (mainPlayer.IsMaxBlessLevel())
			{
				SetExpText("function_limit_season", Bindings.GetSeasonNameByRank(Bindings.PlanesRank + 1));
				return;
			}
			itemAmount = mainPlayer.GetCurBlessPlayerExp();
			int curBlessPlayerLevel = mainPlayer.GetCurBlessPlayerLevel();
			IHomeLevelBlessParser levelBlessInfoParser = Bindings.LevelBlessInfoParser;
			HomeLevelBlessInfo levelBlessInfo = levelBlessInfoParser.Get(mainPlayer.Rank, curBlessPlayerLevel);
			HomeLevelBlessInfo levelBlessInfo2 = levelBlessInfoParser.Get(mainPlayer.Rank, curBlessPlayerLevel + 1);
			long num = levelBlessInfo?.Exp ?? 0;
			value = levelBlessInfo2.Exp - num;
			itemAmount -= num;
		}
		if (mShowExpColor & flag)
		{
			SetExpText("<color=#e17126>{0}</color>/{1}", Bindings.Compact(itemAmount), Bindings.Compact(value));
		}
		else
		{
			SetExpText("{0}/{1}", Bindings.Compact(itemAmount), Bindings.Compact(value));
		}
	}

	private void SetExpText(string key, params object[] args)
	{
		mExpText.SetKey(key, args);
		foreach (MultiLanguageText mOtherExpText in mOtherExpTexts)
		{
			mOtherExpText.SetKey(key, args);
		}
	}

    private static long OriginalLerp(long start, long end, float t)
    {
        return (long)((decimal)(end - start) * (decimal)Mathf.Clamp(t, 0f, 1f)) + start;
    }
}

}
