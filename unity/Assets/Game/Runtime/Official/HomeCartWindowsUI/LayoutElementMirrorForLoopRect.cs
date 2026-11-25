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
using UnityEngine.EventSystems;
namespace Zjcs.Official.HomeCartWindowsUI
{
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class LayoutElementMirrorForLoopRect : UnityEngine.EventSystems.UIBehaviour, ILayoutElement
{
	[SerializeField]
	private LayoutGroup mContentLayoutElement;

	[SerializeField]
	private float mMinHeight;

	[SerializeField]
	private float mMaxHeight = 100f;

	[SerializeField]
	private int mLayoutPriority;

	[Header("可选")]
	[SerializeField]
	private LoopScrollRect mLoopRect;

	private float mPreferrenHeight;

	public float preferredHeight => mPreferrenHeight;

	public float minWidth => -1f;

	public float preferredWidth => -1f;

	public float flexibleWidth => -1f;

	public float minHeight => mMinHeight;

	public float flexibleHeight => -1f;

	public int layoutPriority => mLayoutPriority;

	protected override void OnEnable()
	{
		base.OnEnable();
		Refresh().Forget();
	}

	public void CalculateLayoutInputHorizontal()
	{
	}

	public void CalculateLayoutInputVertical()
	{
	}

	protected void SetDirty()
	{
		if (IsActive())
		{
			LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
		}
	}

	private async UniTask Refresh()
	{
		mPreferrenHeight = mMaxHeight;
		SetDirty();
		await UniTask.NextFrame();
		mLoopRect.RefreshCells();
		float num = mContentLayoutElement.preferredHeight;
		num = ((num > mMaxHeight) ? mMaxHeight : num);
		num = ((num > mMinHeight) ? num : mMinHeight);
		if (mPreferrenHeight != num)
		{
			mPreferrenHeight = num;
			SetDirty();
		}
	}
}
}
