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

public class PlayerHpRedPoint : HomePanelRedDotItemBase
{
	private bool mUpdateFlag = true;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		PanelBindings.MainPlayer.OnHpUpdate += MainPlayer_OnHpUpdate;
		PanelBindings.MainPlayer.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (PanelBindings.MainPlayer != null)
		{
			PanelBindings.MainPlayer.OnHpUpdate -= MainPlayer_OnHpUpdate;
			PanelBindings.MainPlayer.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnItemCountUpdate(HomeExperienceItem itemData, long preCount, object sourceData)
	{
		if (PanelBindings.GetItemInfo(itemData.Id).ItemType == HomePanelItemType.Cooking && (PanelBindings.GetItemInfo(itemData.Id).PartInfo as HomePanelCookingInfo).Type == CookingType.Recovery)
		{
			Refresh();
		}
	}

	private void MainPlayer_OnHpUpdate(float obj)
	{
		Refresh();
	}

	protected override void Update()
	{
		base.Update();
		if (IsEnableAndInited() && mUpdateFlag)
		{
			TryRefresh();
		}
	}

	private void TryRefresh()
	{
		UpdateFlag();
		if (!mUpdateFlag)
		{
			SetDirty();
		}
	}

	private void UpdateFlag()
	{
		mUpdateFlag = PanelBindings.MainPlayer.HpPercent < PanelBindings.PlayerHpRedDotThreshold;
	}

	public override bool ShowRedDot()
	{
		UpdateFlag();
		if (PanelBindings.MainPlayer.HpPercent >= PanelBindings.PlayerHpRedDotThreshold)
		{
			return false;
		}
		if (!PanelBindings.MainPlayer.HasCureItem())
		{
			return false;
		}
		return true;
	}
}

}
