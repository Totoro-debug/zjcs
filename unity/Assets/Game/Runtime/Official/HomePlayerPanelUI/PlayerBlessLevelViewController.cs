// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBlessLevelViewController : UIBase
{
    private IHomePlayerPanelBindings PanelBindings => HomePlayerPanelScope.Required(this);

	[SerializeField]
	private BlessLevelTextViewChanger mBlessTxt;

	[SerializeField]
	private BlessLevelTextViewChanger mCombatRatingTxt;

	[SerializeField]
	private bool mNeedInitByMainPlayer;

	[SerializeField]
	private HomeIntroductionPopBtn mPopBtn;

	[SerializeField]
	private ProfessionBlessPropBtn mBlessPropBtn;

	private IHomePanelPlayerBrief mInfo;

	protected override void Start()
	{
		base.Start();
		if (mNeedInitByMainPlayer && !_Inited)
		{
			Init(null);
		}
	}

	public void Init(IHomePanelPlayerBrief info)
	{
		InitWrapper(() =>
		{
			mInfo = info;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
		PanelBindings.MainPlayer.OnLevelUpdate += Player_OnLevelUpdate;
		PanelBindings.MainPlayer.OnBlessLevelDataReset += Player_OnBlessLevelDataReset;
		PanelBindings.MainPlayer.OnCombatRatingChanged += MainPlayer_OnCombatRatingChanged;
		PanelBindings.MainPlayer.OnNextRankCombatRatingChanged += MainPlayer_OnCombatRatingChanged;
	}

	protected override void OnDisableAndInited()
	{
		IHomePanelPlayer mainPlayer = PanelBindings.MainPlayer;
		if (mainPlayer != null)
		{
			mainPlayer.OnLevelUpdate -= Player_OnLevelUpdate;
			mainPlayer.OnBlessLevelDataReset -= Player_OnBlessLevelDataReset;
			PanelBindings.MainPlayer.OnCombatRatingChanged -= MainPlayer_OnCombatRatingChanged;
			PanelBindings.MainPlayer.OnNextRankCombatRatingChanged -= MainPlayer_OnCombatRatingChanged;
		}
		mInfo = null;
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnCombatRatingChanged(long arg1, long arg2)
	{
		RefreshCombatRating();
	}

	private void Player_OnLevelUpdate(int cur)
	{
		Refresh();
	}

	private void Player_OnBlessLevelDataReset(int pre, int cur)
	{
		Refresh();
	}

	private void Refresh()
	{
		IHomePanelPlayer mainPlayer = PanelBindings.MainPlayer;
		bool flag = mInfo == null || mInfo.Id == mainPlayer.Id;
		bool flag2 = (flag ? mainPlayer.HasBlessLevel() : mInfo.IsBlessLevelOpen());
		if ((Object)(object)mPopBtn != (Object)null)
		{
			((Component)mPopBtn).gameObject.SetActive(flag2 & flag);
			if (flag2)
			{
				string textFormated = PanelBindings.TextFormatted("ui_blessing_player_level_tip_0", mainPlayer.BaseLevel, mainPlayer.GetCurBlessPlayerLevel());
				List<string> list = new List<string> { textFormated };
				foreach (string item in new List<string>
				{
					"ui_blessing_player_level_tip_1",
					PanelBindings.GetSeasonLevelUnEffectDesc(PanelBindings.PlanesRank),
					"ui_blessing_player_level_tip_3"
				})
				{
					list.Add(PanelBindings.Text(item));
				}
				HomeInfoTextKey titleTextArgs = new HomeInfoTextKey("ui_blessing_level");
				mPopBtn.Init(titleTextArgs, list);
			}
		}
		RefreshCombatRating();
		mBlessTxt.Refresh(flag2);
	}

	public void RefreshCombatRating()
	{
		IHomePanelPlayer mainPlayer = PanelBindings.MainPlayer;
		bool flag = mInfo == null || mInfo.Id == mainPlayer.Id;
		if (((Object)(object)mBlessPropBtn != (Object)null) & flag)
		{
			long num = mainPlayer.CombatRating - mainPlayer.NoBlessCombatRating;
			mBlessPropBtn.InitPowerDesc(mainPlayer.MyPlanesRank, PanelBindings.PlanesRank, mainPlayer.NoBlessCombatRating, num);
			mCombatRatingTxt.Refresh(num > 0);
		}
		else
		{
			mCombatRatingTxt.Refresh(isBlessing: false);
		}
	}
}

}
