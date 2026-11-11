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
using UnityEngine.UI;

public class MainPlayerHpImage : PlayerIconMeHpSlider
{
	[SerializeField]
	private Slider mSlider;

	public Image HpImage;

	private bool mUpdateFlag = true;

	public override void Init()
	{
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		PanelBindings.MainPlayer.OnHpUpdate += OnHpUpdate;
		UpdateValue();
	}

	protected override void OnDisableAndInited()
	{
		if (PanelBindings.MainPlayer != null)
		{
			PanelBindings.MainPlayer.OnHpUpdate -= OnHpUpdate;
		}
		base.OnDisableAndInited();
	}

	private void Update()
	{
		if (PanelBindings.MainPlayer != null && PanelBindings.MainPlayer.Maps.Count <= 0 && mUpdateFlag)
		{
			UpdateValue();
		}
	}

	private void OnHpUpdate(float hp)
	{
		UpdateValue();
	}

	private void UpdateValue()
	{
		mSlider.normalizedValue = PanelBindings.MainPlayer.HpPercent;
		mUpdateFlag = PanelBindings.MainPlayer.HpPercent < 1f;
	}
}

}
