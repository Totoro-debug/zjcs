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

public class BlessingLevelRuleDialog : UIBase
{
    public IHomeBlessingRuleServices Services { protected get; set; }
	[SerializeField]
	private RuleDialog mRuleDialog;

	[SerializeField]
	private Transform mPropSlot;

	[SerializeField]
	private BlessingLevelRuleItem mPropItemPrefab;

	[SerializeField]
	private int mOneSideShowCount = 5;

	private GameObjectPool<BlessingLevelRuleItem> mPool;

	private readonly List<BlessingLevelRuleItem> mPropItems = new List<BlessingLevelRuleItem>();

	private int mBlessingLevel;

	private void Awake()
	{
		mPool = new GameObjectPool<BlessingLevelRuleItem>(transform, mPropSlot, mPropItemPrefab);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		foreach (BlessingLevelRuleItem mPropItem in mPropItems)
		{
			mPool.Return(mPropItem);
		}
		mPropItems.Clear();
		base.OnDisableAndInited();
	}

	public void Init()
	{
		InitWrapper(null);
	}

	private void Refresh()
	{
		mRuleDialog.Rules = Services.RuleTips;
		mRuleDialog.SetRule("BlessingLevelRule");
		mBlessingLevel = Services.Window.Player.GetBlessingLevel();
		mPool.RecycleMany(mPropItems, mOneSideShowCount * 2 + 1);
		int num = ((mBlessingLevel <= mOneSideShowCount) ? 1 : (mBlessingLevel - mOneSideShowCount));
		int num2 = num + mOneSideShowCount * 2;
		for (int i = num; i <= num2; i++)
		{
			mPropItems[i - num].Init(i, mBlessingLevel);
		}
	}
}

}
