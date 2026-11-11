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
using UnityEngine;

public class PlayerBuffContentUIController : HomePlayerPanelUIBase
{
	[SerializeField]
	private WorldStateSwitcher<bool> mStateSwitcher;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		RefreshView();
		PanelBindings.MainPlayer.OnStatusEffectDataUpdate += OnMainPlayerStatusEffectDataUpdate;
	}

	private void OnMainPlayerStatusEffectDataUpdate(IHomePanelStatusEffect obj)
	{
		RefreshView();
	}

	protected override void OnDisableAndInited()
	{
		if (PanelBindings.MainPlayer != null)
		{
			PanelBindings.MainPlayer.OnStatusEffectDataUpdate -= OnMainPlayerStatusEffectDataUpdate;
		}
		base.OnDisableAndInited();
	}

	private void RefreshView()
	{
		bool state = false;
		foreach (IHomePanelStatusEffect value in PanelBindings.MainPlayer.StatusEffectManager.Values)
		{
			if (value.IsActive)
			{
				state = true;
				break;
			}
		}
		mStateSwitcher.SetState(state);
	}
}

}
