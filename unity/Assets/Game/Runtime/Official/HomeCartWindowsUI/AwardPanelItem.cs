// Selected 903_156948 client controllers; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.Map13MiniMap;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using HangUpInfo = Zjcs.Official.HomeCartWindowsUI.HomeCartHangUpInfo;
using Player = Zjcs.Official.HomeCartWindowsUI.IHomeCartWindowPlayer;
using ItemAwardIcon = Zjcs.Official.HomeAwardUI.HomeAwardIcon;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using ItemType = Zjcs.Official.HomeAwardUI.ItemType;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
namespace Zjcs.Official.HomeCartWindowsUI
{
public class AwardPanelItem : AwardPanelItemBase, IGameObjectPoolItem
{
	[SerializeField]
	private Transform mAnchor;

	[SerializeField]
	private Animation mShowAnimation;

	private ItemAwardIcon mAwardIcon;

	private bool mIsActive;

	public override void Init(AwardData awardData, Action<ItemAwardIcon, AwardData> initItemIcon, bool isSelf = true)
	{
		if ((Object)mAwardIcon == (Object)null)
		{
			mAwardIcon = Services.AwardItemServices.InstantiateAward(mAnchor, awardData, initItemIcon, autoTransition: true, isSelf, showOutputAddition: true);
		}
		else
		{
			mAwardIcon.Init(awardData, initItemIcon, autoTransition: true, isSelf);
		}
	}

	private void OnDisable()
	{
		if ((Object)mAwardIcon != (Object)null)
		{
			mAwardIcon.Clear();
		}
	}

	public override void Show()
	{
		if (!mIsActive)
		{
			mIsActive = true;
			mAnchor.gameObject.SetActive(true);
			mShowAnimation.Play();
		}
	}

	public override void Hide()
	{
		mIsActive = false;
		mAnchor.gameObject.SetActive(false);
	}

	public void OnGameObjectPoolReset()
	{
		mIsActive = false;
	}

	public void OnGameObjectPoolClear()
	{
	}
}
}
