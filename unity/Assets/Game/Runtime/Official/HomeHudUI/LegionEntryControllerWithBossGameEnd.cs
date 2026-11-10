namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class LegionEntryControllerWithBossGameEnd : MonoBehaviour
{
	public IHomeBottomBarEntryServices Services { get; set; }

	private bool mIsEnterGame;

	private void OnEnable()
	{
		Services.OnGameUpdate += new Action(MainPlayer_OnGameUpdate);
		if (ShouldExitLegion())
		{
			if (Services.IsLegionBossPanelPopup())
			{
				Services.CloseLegionBossPanel();
			}
			PublicBottomBar.Instance.SelectTab(PublicBottomBar.TabType.Home);
		}
		mIsEnterGame = false;
	}

	private void OnDisable()
	{
		if (Services.ManagerAvailable)
		{
			Services.OnGameUpdate -= new Action(MainPlayer_OnGameUpdate);
		}
	}

	private void MainPlayer_OnGameUpdate()
	{
		if (Services.MainGame == null)
		{
			mIsEnterGame = false;
		}
		else if (Services.MainGame.StageType == StageType.LegionBoss)
		{
			mIsEnterGame = true;
		}
	}

	private bool ShouldExitLegion()
	{
		if (!mIsEnterGame)
		{
			return false;
		}
		if (Services.MainPlayer.LegionId > 0)
		{
			return false;
		}
		if ((Object)(object)PublicBottomBar.Instance == (Object)null)
		{
			return false;
		}
		if (PublicBottomBar.Instance.CurrentTabType != PublicBottomBar.TabType.Legion)
		{
			return false;
		}
		return true;
	}
}

}
