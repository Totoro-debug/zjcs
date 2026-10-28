// Mechanically adapted original client component; see map12 team targets receipt.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using ITeamTarget = Zjcs.Official.TeamUI.TeamTargetData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.TeamUI
{
public class TeamPanelTargetSubItem : UIBase, IGameObjectPoolItem
{
	private ITeamTargetServices services;
	public void Configure(ITeamTargetServices value) { services = value; }

	[SerializeField]
	private WorldMultiLanguageText mTargetTitleTxt;

	[SerializeField]
	private WorldStateSwitcher<bool> mSelectSwitcher;

	[SerializeField]
	private Button mBtn;

	[SerializeField]
	private WorldStateSwitcher<bool> mLastOneSwitcher;

	private Action<ITeamTarget> mSelectTeamTargetDelegate;

	private ITeamTarget mTeamTarget;

	private bool mSelected;

	private bool mIsLastOne;

	public void Init(ITeamTarget teamTarget, Action<ITeamTarget> selectTeamTargetDelegate, bool isSelected, bool isLastOne)
	{
		InitWrapper(() =>
		{
			mTeamTarget = teamTarget;
			mSelectTeamTargetDelegate = selectTeamTargetDelegate;
			mSelected = isSelected;
			mIsLastOne = isLastOne;
		});
	}

	protected void Awake()
	{
		((UnityEvent)mBtn.onClick).AddListener((UnityAction)OnBtnClick);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
	}

	private void Refresh()
	{
		mTargetTitleTxt.SetKey(mTeamTarget.GetItemName());
		mSelectSwitcher.SetState(mSelected);
		mLastOneSwitcher.SetState(mIsLastOne);
	}

	private void OnBtnClick()
	{
		if (!mSelected)
		{
			mSelectTeamTargetDelegate?.Invoke(mTeamTarget);
		}
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}
}
