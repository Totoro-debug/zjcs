// Selected original player-info implementation; only service boundaries and C# decompilation repairs differ.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerInfoUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomePlayerLevelText : HomePlayerInfoUIBase
{
	public MultiLanguageText LevelText;

	public MultiLanguageText MaxLevelTxt;

	[SerializeField]
	private int mNumberSize;

	[SerializeField]
	private HomeIntroductionPopBtn mPopBtn;

	[SerializeField]
	private WorldStateSwitcher<bool> mMaxLevelStateSwitcher;

	protected override void OnEnableAndInited()
	{
		InfoBindings.MainPlayer.OnLevelUpdate += MainPlayer_OnLevelUpdate;
		InfoBindings.MainPlayer.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
		InfoBindings.MainPlayer.OnSeasonRecordDataEndUpdate += MainPlayer_OnSeasonRecordDataUpdate;
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (InfoBindings.MainPlayer != null)
		{
			InfoBindings.MainPlayer.OnLevelUpdate -= MainPlayer_OnLevelUpdate;
			InfoBindings.MainPlayer.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
			InfoBindings.MainPlayer.OnSeasonRecordDataEndUpdate -= MainPlayer_OnSeasonRecordDataUpdate;
		}
	}

	private void MainPlayer_OnLevelUpdate(int obj)
	{
		Refresh();
	}

	private void MainPlayer_OnSubRankUpdate(int subRank)
	{
		Refresh();
	}

	private void MainPlayer_OnSeasonRecordDataUpdate(object dataWrap)
	{
		Refresh();
	}

	private void Refresh()
	{
		if (mNumberSize > 0)
		{
			HomeNumberWithSizeArg numberWithSizeArg = new HomeNumberWithSizeArg(InfoBindings.MainPlayer.Level, mNumberSize);
			LevelText.Format(numberWithSizeArg, InfoBindings.MainPlayer.LevelLimit);
			if ((Object)(object)MaxLevelTxt != (Object)null)
			{
				MaxLevelTxt.Format(InfoBindings.MainPlayer.LevelLimit);
			}
			RefreshTMPEffect();
			return;
		}
		CultureNumberArg cultureNumberArg = CultureNumberArg.Compact(InfoBindings.MainPlayer.Level);
		LevelText.Format(cultureNumberArg, InfoBindings.MainPlayer.LevelLimit);
		if ((Object)(object)MaxLevelTxt != (Object)null)
		{
			MaxLevelTxt.Format(InfoBindings.MainPlayer.LevelLimit);
		}
		RefreshTMPEffect();
		bool flag = InfoBindings.MainPlayer.ShowPlayerStoreExpLimit();
		mMaxLevelStateSwitcher.SetState(flag);
		if (!((Object)(object)mPopBtn != (Object)null))
		{
			return;
		}
		((Component)mPopBtn).gameObject.SetActive(flag);
		if (!flag)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (string item in new List<string> { "player_exp_max_desc1", "player_exp_max_desc2" })
		{
			list.Add(InfoBindings.Text(item));
		}
		HomeInfoTextKey titleTextArgs = new HomeInfoTextKey("player_exp_max_title");
		mPopBtn.Init(titleTextArgs, list);
	}

	private void RefreshTMPEffect()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected Obj, but got Unknown
		TMPEffect val = default;
		if (!InfoBindings.TMPEffectFontSizeIssueFixed && ((Component)LevelText).TryGetComponent<TMPEffect>(out val) && ((Behaviour)val).enabled)
		{
			LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)((Component)val).transform);
			val.SetupEffect(true);
		}
	}
}

}
