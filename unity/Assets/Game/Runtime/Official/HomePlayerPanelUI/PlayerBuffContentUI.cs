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
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerBuffContentUI : HomePlayerPanelUIBase
{
	[SerializeField]
	private MonoDictionary<StatusEffectIconType, AssetGuidProxy<GameObject>> mBuffIconAssetGuidDic;

	[SerializeField]
	private Button mClickBtn;

	private PlayerBuffStatusController mPlayerSelfBuffStatusProvider;

	private IHomePanelResourceLoader ResourceLoader => PanelBindings.Resources;

	private void Awake()
	{


		mPlayerSelfBuffStatusProvider = new PlayerBuffStatusController(PanelBindings, ResourceLoader, mBuffIconAssetGuidDic, ((Component)this).transform);
		mClickBtn.onClick.AddListener((UnityAction)OnClickBtnClick);
	}

	protected override void OnDestroy()
	{


		mClickBtn.onClick.RemoveListener((UnityAction)OnClickBtnClick);
		mPlayerSelfBuffStatusProvider.Dispose();
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mPlayerSelfBuffStatusProvider.Start();
	}

	protected override void OnDisableAndInited()
	{
		mPlayerSelfBuffStatusProvider.End();
		base.OnDisableAndInited();
	}

	public void SetButtonEnable(bool isEnable)
	{
		((Behaviour)mClickBtn).enabled = isEnable;
	}

	private void OnClickBtnClick()
	{
		List<HomeEntryDetailData> entryDatas = mPlayerSelfBuffStatusProvider.EntryDatas;
		if (entryDatas.Count > 0)
		{
			PanelBindings.PopupEntries(this, entryDatas);
		}
	}
}

}
