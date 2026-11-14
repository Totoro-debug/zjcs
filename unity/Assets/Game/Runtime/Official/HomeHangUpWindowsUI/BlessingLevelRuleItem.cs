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

public class BlessingLevelRuleItem : MonoBehaviour, IGameObjectPoolItem
{
    public IHomeBlessingRuleServices Services { protected get; set; }
	[SerializeField]
	private MultiLanguageText mLevelTxt;

	[SerializeField]
	private MultiLanguageText mOutputTxt;

	[SerializeField]
	private MultiLanguageText mMaxLevelTxt;

	[SerializeField]
	public WorldStateSwitcher<bool> mCurrentStateSwitcher;

	public void Init(int blessingLevel, int currentBlessingLevel)
	{
		Services.Window.TryGetMainHangUpInfo(EFurniture.Bed, out var hangUpInfo);
		double hangUpReward = Services.Window.GetHangUpReward(hangUpInfo.Id, 1.0, blessingLevel, isBase: true);
		int furnitureLevelUpLimit = Services.FurnitureLevelUpLimit(blessingLevel);
		mLevelTxt.Format(blessingLevel);
		mOutputTxt.Format(Services.Window.TextFormatted("ui_rule_hours", (long)(hangUpReward + 0.1)));
		mMaxLevelTxt.Format(furnitureLevelUpLimit);
		mCurrentStateSwitcher.SetState(blessingLevel == currentBlessingLevel);
		Vector3 vector = Vector3.one * ((blessingLevel == currentBlessingLevel) ? 1.2f : 1f);
		Transform transform = mLevelTxt.transform;
		Transform transform2 = mOutputTxt.transform;
		Vector3 vector2 = (mMaxLevelTxt.transform.localScale = vector);
		Vector3 localScale = (transform2.localScale = vector2);
		transform.localScale = localScale;
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
