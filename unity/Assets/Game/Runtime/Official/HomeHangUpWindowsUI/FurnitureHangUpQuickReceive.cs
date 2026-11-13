// Selected original client methods; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using FastHangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowFastHangUpInfo;
using Player = Zjcs.Official.HomeHangUpWindowsUI.IHomeHangUpWindowPlayer;
namespace Zjcs.Official.HomeHangUpWindowsUI
{

public class FurnitureHangUpQuickReceive : UIBase
{
    public IHomeHangUpWindowServices Services { protected get; set; }
	[SerializeField]
	private Toggle mQuickReceiveToggle;

	[SerializeField]
	private WorldStateSwitcher<bool> mFunctionLimitSwitcher;

	private EFurniture mFurniture;

	private object mCondition;

	public void Init(EFurniture furniture)
	{
		InitWrapper(() =>
		{
			mFurniture = furniture;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Player mainPlayer = Services.Player;
		bool flag = mainPlayer.IsFunctionLimit(164);
		mFunctionLimitSwitcher.SetState(flag);
		mQuickReceiveToggle.isOn = mainPlayer.GetHangUpQuickReceiveState(mFurniture);
		mQuickReceiveToggle.onValueChanged.AddListener(OnQuickReceiveToggleChanged);
		if (flag)
		{
			HomeWindowFunctionLimitInfo functionLimitInfo = Services.GetFunctionLimitInfo(164);
			if (functionLimitInfo != null)
			{
				mCondition = functionLimitInfo.Condition;
				Services.RegisterCondition(mCondition, OnCondition);
			}
		}
	}

	protected override void OnDisableAndInited()
	{
		mQuickReceiveToggle.onValueChanged.RemoveListener(OnQuickReceiveToggleChanged);
		if (mCondition != null)
		{
			Services.DeregisterCondition(mCondition, OnCondition);
			mCondition = null;
		}
		base.OnDisableAndInited();
	}

	private void OnCondition()
	{
		bool state = Services.Player.IsFunctionLimit(164);
		mFunctionLimitSwitcher.SetState(state);
	}

	private void OnQuickReceiveToggleChanged(bool isOn)
    {
        if (isOn != Services.Player.GetHangUpQuickReceiveState(mFurniture))
            Services.SwitchQuickReceive(mFurniture, isOn, (resp, err) => mQuickReceiveToggle.SetIsOnWithoutNotify(!isOn));
    }
}

}
