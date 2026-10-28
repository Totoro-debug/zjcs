// Mechanically adapted original client component; see map12 team targets receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using ITeamTarget = Zjcs.Official.TeamUI.TeamTargetData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.TeamUI
{
public class TeamPanelTargetItem : UIBase, IGameObjectPoolItem
{
	private ITeamTargetServices services;
	public void Configure(ITeamTargetServices value) { services = value; }

	[SerializeField]
	private WorldMultiLanguageText mTargetTitleTxt;

	[SerializeField]
	private WorldMultiLanguageText mTeamMemberCountTxt;

	[SerializeField]
	private WorldStateSwitcher<bool> mSelectSwitcher;

	[SerializeField]
	private WorldStateSwitcher<bool> mHasSubItemSwitcher;

	[SerializeField]
	private WorldStateSwitcher<bool> mFoldSwitcher;

	[SerializeField]
	private Button mBtn;

	[SerializeField]
	private Transform mSubItemContent;

	[SerializeField]
	private TeamPanelTargetSubItem mSubItemTemp;

	private Action<ITeamTarget> mSelectTeamTargetDelegate;

	private Func<ITeamTarget, bool> mIsSelectedDelegate;

	private bool mFold = true;

	private bool mHasSubItem;

	private TeamTargetCatalog mTeamTargetCatalog;

	private List<ITeamTarget> mTeamTargets;

	private GameObjectPool<TeamPanelTargetSubItem> mSubItemPool;

	private List<TeamPanelTargetSubItem> mSubItemList = new List<TeamPanelTargetSubItem>();

	public void Init(TeamTargetCatalog targetCatalog, List<ITeamTarget> teamTargets, Action<ITeamTarget> selectTeamTargetDelegate, Func<ITeamTarget, bool> isSelectedDelegate)
	{
		InitWrapper(() =>
		{
			mTeamTargetCatalog = targetCatalog;
			mTeamTargets = teamTargets;
			mSelectTeamTargetDelegate = selectTeamTargetDelegate;
			mIsSelectedDelegate = isSelectedDelegate;
			mHasSubItem = services.HasMultipleTargets(targetCatalog);
		});
	}

	protected void Awake()
	{
		((UnityEvent)mBtn.onClick).AddListener((UnityAction)OnBtnClick);
		mSubItemPool = new GameObjectPool<TeamPanelTargetSubItem>(((Component)this).transform, mSubItemContent, mSubItemTemp);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mFold = !IsAnySelected();
		Refresh();
	}

	public void Refresh()
	{
		mTargetTitleTxt.SetKey(services.EnumName(mTeamTargetCatalog));
		mTeamMemberCountTxt.Format(mTeamTargets.First().GetTeamMaxPlayer());
		mSelectSwitcher.SetState(IsAnySelected());
		RefreshSubContent();
	}

	private bool IsAnySelected()
	{
		foreach (ITeamTarget mTeamTarget in mTeamTargets)
		{
			if (mIsSelectedDelegate != null && mIsSelectedDelegate(mTeamTarget))
			{
				return true;
			}
		}
		return false;
	}

	private void RefreshSubContent()
	{
		mHasSubItemSwitcher.SetState(mHasSubItem);
		if (mHasSubItem)
		{
			RefreshFoldState();
		}
	}

	private void RefreshFoldState()
	{
		mSubItemPool.RecycleMany(mSubItemList, mTeamTargets.Count);
		for (int i = 0; i < mTeamTargets.Count; i++)
		{
			bool isLastOne = i == mTeamTargets.Count - 1;
			TeamPanelTargetSubItem teamPanelTargetSubItem = mSubItemList[i];
			ITeamTarget teamTarget = mTeamTargets[i];
			teamPanelTargetSubItem.Configure(services);
			teamPanelTargetSubItem.Init(isSelected: mIsSelectedDelegate != null && mIsSelectedDelegate(teamTarget), teamTarget: teamTarget, selectTeamTargetDelegate: mSelectTeamTargetDelegate, isLastOne: isLastOne);
		}
		((Component)mSubItemContent).gameObject.SetActive(!mFold);
		mFoldSwitcher.SetState(mFold);
	}

	private void OnBtnClick()
	{
		if (mHasSubItem)
		{
			mFold = !mFold;
			RefreshFoldState();
		}
		else
		{
			mSelectTeamTargetDelegate?.Invoke(mTeamTargets.First());
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
