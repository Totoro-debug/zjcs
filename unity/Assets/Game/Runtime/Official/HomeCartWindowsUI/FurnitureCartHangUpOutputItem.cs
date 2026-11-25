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
public class FurnitureCartHangUpOutputItem : MonoBehaviour
{
    public IHomeCartWindowServices Services { protected get; set; }
	public Image Icon;

	public MultiLanguageText OutputTxt;

	private int mHangUpId;

	private long mMaxCount;

	private double mHangUpSeconds;

	private double mHourOutput;

	private long mPoolCount;

	private long mSumCount;

	private HomeCartWindowResourceBag mDisposableBag = new HomeCartWindowResourceBag();

	private void Awake()
	{
		mDisposableBag ??= new HomeCartWindowResourceBag();
	}

	private void OnDestroy()
	{
		mDisposableBag?.Dispose();
	}

	public void Init(int hangUpId)
	{
		mHangUpId = hangUpId;
		HomeWindowItemInfo hangUpRewardItem = Services.GetHangUpRewardItem(hangUpId);
		Icon.sprite = mDisposableBag.Add(Services.Resources.LoadAsset<Sprite>(hangUpRewardItem.Icon)).Result;
		Refresh(hangUpId);
	}

	public void Refresh(int hangUpId)
	{
		if (hangUpId == mHangUpId)
		{
			mMaxCount = Services.GetHangUpMaxRewardStore(hangUpId);
			mPoolCount = Services.GetHangUpAwardCount(hangUpId, out mHangUpSeconds);
			mHourOutput = Services.GetHangUpReward(hangUpId, 1.0);
			mSumCount = mPoolCount;
			OutputTxt.SetKey("hang_up_output_tip", mSumCount, mMaxCount);
		}
	}

	public void UpdateHangUp(float deltaTime)
	{
		if (mSumCount < mMaxCount)
		{
			mHangUpSeconds += deltaTime;
			double num = mHourOutput * (mHangUpSeconds / 3600.0);
			mSumCount = mPoolCount + (long)num;
			OutputTxt.SetKey("hang_up_output_tip", mSumCount, mMaxCount);
		}
	}
}
}
