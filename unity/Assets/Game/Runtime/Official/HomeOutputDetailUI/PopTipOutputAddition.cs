// Original selected output-addition UI; local data and input boundaries only.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using OutputGroupType = Zjcs.Official.HomeOutputAddition.OutputGroupType;
namespace Zjcs.Official.HomeOutputDetailUI
{
public class PopTipOutputAddition : UIBase, LoopScrollDataSource
{
    public IOutputAdditionDetailServices Services { get; set; }
	[SerializeField]
	private LoopVerticalScrollWithMaxHeight mScroll;

	[SerializeField]
	private RectTransform mRect;

	[SerializeField]
	private Image mBgImage;

	private Canvas mCanvas;

	private CanvasScaler mCanvasScaler;

	private OutputGroupType mOutputGroupType;

	private HomeWindowResourceBag mDisposableBag;

	private List<(List<OutputAdditionInfo> propList, DateTime endTime)> mInfoLists;

	private void Awake()
	{
		mScroll.dataSource = this;
		mCanvas = ((Component)this).GetComponentInParent<Canvas>();
		mCanvasScaler = ((Component)this).GetComponentInParent<CanvasScaler>();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = new HomeWindowResourceBag();
		Services.HookGlobalPointerDown(OnGameObjectPointerDown);
		InitContent();
	}

	protected override void OnDisableAndInited()
	{
		Services.UnhookGlobalPointerDown(OnGameObjectPointerDown);
		mDisposableBag?.Dispose();
		base.OnDisableAndInited();
	}

	public void Init(OutputGroupType outputGroupType)
	{
		InitWrapper(() =>
		{
			mOutputGroupType = outputGroupType;
		});
	}

	private void InitContent()
	{
		SetDialogPos();
		mInfoLists = Services.Groups(mOutputGroupType);
		mScroll.totalCount = mInfoLists.Count;
		mScroll.RefillCells();
		((Behaviour)mBgImage).enabled = mInfoLists.Count > 1;
	}

	public void ProvideData(Transform transform, int idx)
	{
		if (idx >= 0 && idx < mInfoLists.Count)
		{
			((Component)transform).GetComponent<OutputAdditionItem>().Init(mInfoLists[idx].propList, mInfoLists[idx].endTime);
		}
	}

	private void SetDialogPos()
	{
		Vector2 realCanvasSize = PartnerAdventureUiUtils.GetRealCanvasSize(mCanvas, mCanvasScaler, out var _);
		Transform transform = ((Component)this).transform;
		Vector2 pos = default;
		RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)(object)((transform is RectTransform) ? transform : null), Services.PointerPosition, mCanvas.worldCamera, out pos);
		PartnerAdventureUiUtils.SetRectPopPosInScreen(mRect, pos, realCanvasSize);
	}

	private void OnGameObjectPointerDown(Vector2 screenPosition)
	{
		if (!PartnerAdventureUiUtils.CheckIfClickInsideUI(mRect, screenPosition, mCanvas.worldCamera))
		{
			Services.Close(((Component)this).gameObject);
		}
	}
}
}
