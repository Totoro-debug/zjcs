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
public class RewardPreviewDialog : UIBase, LoopScrollDataSource
{
	[SerializeField]
	private LoopVerticalScrollRect mScrollRect;

	[SerializeField]
	private GridLayoutGroup mLayoutGroup;

	[SerializeField]
	private int mFixedRow;

	private IRewardPreviewDataProvider mDataProvider;

	private int mCount;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Init();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		mDataProvider = null;
		mScrollRect.ClearCells();
	}

	private void Init()
	{
		mCount = mDataProvider.Count;
		int constraintCount = mLayoutGroup.constraintCount;
		bool flag = Mathf.CeilToInt(1f * (float)mCount / (float)constraintCount) > mFixedRow;
		mScrollRect.movementType = (flag ? LoopScrollRectBase.MovementType.Elastic : LoopScrollRectBase.MovementType.Clamped);
		mScrollRect.dataSource = this;
		mScrollRect.totalCount = mCount;
		mScrollRect.RefillCells();
	}

	public void Init(IRewardPreviewDataProvider dataProvider)
	{
		InitWrapper(() =>
		{
			mDataProvider = dataProvider;
		});
	}

	public void ProvideData(Transform transform, int idx)
	{
		if (idx < mCount)
		{
			RewardPreviewDialogItem component = transform.GetComponent<RewardPreviewDialogItem>();
			mDataProvider.RefreshItem(idx, component);
		}
	}
}
}
