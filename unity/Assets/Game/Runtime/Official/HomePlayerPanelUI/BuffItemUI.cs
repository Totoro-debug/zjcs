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
using UnityEngine.Events;
using UnityEngine.UI;

public class BuffItemUI : UIBase, IGameObjectPoolItem
{
    private IHomePlayerPanelBindings PanelBindings => HomePlayerPanelScope.Required(this);

	[SerializeField]
	private Button mBuffBtn;

	[SerializeField]
	private FightStatusUIItem mStatusUI;

	[SerializeField]
	private BuffItemBaseDecorator mBuffItemDecorator;

	private IHomePanelStatusEffect mStatusEffectData;

	public IHomePanelStatusEffect StatusEffectData => mStatusEffectData;

	private void Awake()
	{


		mBuffBtn.onClick.AddListener((UnityAction)OnIconClick);
	}

	public void Init(IHomePanelStatusEffect statusEffectData)
	{
		InitWrapper(() =>
		{
			mStatusEffectData = statusEffectData;
		});
	}

	public void SetButtonEnable(bool isEnable)
	{
		((Behaviour)mBuffBtn).enabled = isEnable;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		RefreshView();
		if ((Object)(object)mBuffItemDecorator != (Object)null)
		{
			mBuffItemDecorator.Init(mStatusEffectData);
		}
	}

	protected override void OnDisableAndInited()
	{
		if ((Object)(object)mBuffItemDecorator != (Object)null)
		{
			mBuffItemDecorator.Release();
		}
		base.OnDisableAndInited();
	}

	private void RefreshView()
	{
		int displayRemainTimes = mStatusEffectData.GetDisplayRemainTimes();
		int entryId = mStatusEffectData.Info.EntryId;
		HomePanelEntryInfo entryInfo = PanelBindings.GetEntry(entryId);
		mStatusUI.Show(entryInfo.Icon, entryInfo.Bg, displayRemainTimes);
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}

	private void OnIconClick()
	{
		PanelBindings.PopupEntry(this, () => BuffItemUIUtils.CreateBuffItemEntryDetailData(PanelBindings, mStatusEffectData));
	}
}

}
