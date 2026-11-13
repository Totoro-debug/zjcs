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

public class OutputAdditionDetailButton : UIBase
{
    public IHomeHangUpWindowServices Services { protected get; set; }
	[SerializeField]
	private Button mBtn;

	[SerializeField]
	private OutputGroupType mOutputGroupType;

	[SerializeField]
	private GameObject mContext;

	[SerializeField]
	private MultiLanguageText mTxt;

	private HomeWindowResourceBag mDisposableBag;
    private IDisposable functionStateSubscription;

	private void Awake()
	{
		functionStateSubscription = Services.SubscribeOutputAdditionFunctionState(OnFunctionDisabledUpdate);
		mBtn.onClick.AddListener(OnBtnClick);
		Init();
	}

	protected override void OnDestroy()
	{
		functionStateSubscription?.Dispose();
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag?.Dispose();
		mDisposableBag = new HomeWindowResourceBag();
		mDisposableBag.Add(Services.SubscribeNewDay(OnNewDay));
		InitContent();
	}

	private void OnFunctionDisabledUpdate()
    {
        // The port subscribes only to the original OutputAddition function.
        if (isActiveAndEnabled) Refresh();
    }

	protected override void OnDisableAndInited()
	{
		mDisposableBag?.Dispose();
		base.OnDisableAndInited();
	}

	public void Init()
	{
		InitWrapper(() =>
		{
		});
	}

	public void SetGroupType(OutputGroupType groupType)
	{
		mOutputGroupType = groupType;
	}

	private void InitContent()
	{
		Refresh();
	}

	private void OnNewDay(object _)
	{
		Refresh();
	}

	private void Refresh()
	{
		mContext.SetActive(value: false);
		if (!Services.IsOutputAdditionDisabled)
		{
			List<(List<HomeWindowOutputAdditionInfo>, DateTime)> currOutputGroupPropList = Services.GetCurrOutputGroupPropList(mOutputGroupType);
			if (currOutputGroupPropList.Count > 0 && currOutputGroupPropList.First().Item1.Count > 0)
			{
				mContext.SetActive(value: true);
				int outputAdditionPropType = currOutputGroupPropList.First().Item1.First().OutputAdditionPropType;
				mTxt.SetKey("+{0}%", ((float)Services.Player.GetOutputAdditionPropValue(outputAdditionPropType) / 10000f * 100f));
			}
		}
	}

	private void OnBtnClick()
	{
		Services.OpenOutputAddition(mOutputGroupType);
	}
}

}
