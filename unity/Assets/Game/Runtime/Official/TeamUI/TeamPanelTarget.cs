// Mechanically adapted original client component; see map12 team targets receipt.
using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using ITeamTarget = Zjcs.Official.TeamUI.TeamTargetData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.TeamUI
{
public class TeamPanelTarget : UIBase
{
	private ITeamTargetServices services;
	public void Configure(ITeamTargetServices value) { services = value; }

	[SerializeField]
	private Button mHideBtn;

	[SerializeField]
	private Button mConfirmBtn;

	[SerializeField]
	private Transform mItemContent;

	[SerializeField]
	private TeamPanelTargetItem mItemTemp;

	public ITeamTarget CurrentTarget;

	private List<ITeamTarget> mAllTargets;

	[HideInInspector]
	public List<TeamTargetCatalog> Catalogs = new List<TeamTargetCatalog>();

	public Dictionary<TeamTargetCatalog, List<ITeamTarget>> CatalogTargets = new Dictionary<TeamTargetCatalog, List<ITeamTarget>>();

	public Action<ITeamTarget> mFireSelectTeamTarget;

	private GameObjectPool<TeamPanelTargetItem> mItemPool;

	private List<TeamPanelTargetItem> mItemList = new List<TeamPanelTargetItem>();

	private Action<ITeamTarget> mConfirmCallback;

	private ITeamTarget mInitTarget;

	public TeamTargetCatalog CurrentCatalog => CurrentTarget?.TargetCatalog ?? TeamTargetCatalog.None;

	public TeamTargetCatalog SelectedCatalog { get; private set; }

	public Action<ITeamTarget> FireSelectTeamTarget
	{
		get
		{
			if (mFireSelectTeamTarget == null)
			{
				mFireSelectTeamTarget = SetCurrentTarget;
			}
			return mFireSelectTeamTarget;
		}
	}

	protected void Awake()
	{
		((UnityEvent)mConfirmBtn.onClick).AddListener((UnityAction)OnConfirmBtnClicked);
		((UnityEvent)mHideBtn.onClick).AddListener((UnityAction)OnHideBtnClicked);
		mItemPool = new GameObjectPool<TeamPanelTargetItem>(((Component)this).transform, mItemContent, mItemTemp);
	}

	protected override void OnDisable()
	{
		mConfirmCallback = null;
		base.OnDisable();
	}

	private void OnHideBtnClicked()
	{
		((Component)this).gameObject.SetActive(false);
	}

	private void OnConfirmBtnClicked()
	{
		if (CurrentTarget == null)
		{
			services.ShowError(1252, false);
			return;
		}
		mConfirmCallback?.Invoke(CurrentTarget);
		((Component)this).gameObject.SetActive(false);
	}

	public void Init(ITeamTarget currentTarget, List<ITeamTarget> allTeamTargets, Action<ITeamTarget> confirmCallback)
	{
		InitWrapper(() =>
		{
			Catalogs.Clear();
			CatalogTargets.Clear();
			CurrentTarget = currentTarget;
			SelectedCatalog = CurrentCatalog;
			mInitTarget = currentTarget;
			mConfirmCallback = confirmCallback;
			mAllTargets = allTeamTargets;
			foreach (ITeamTarget mAllTarget in mAllTargets)
			{
				if (!Catalogs.Contains(mAllTarget.TargetCatalog))
				{
					Catalogs.Add(mAllTarget.TargetCatalog);
				}
				if (!CatalogTargets.TryGetValue(mAllTarget.TargetCatalog, out var value))
				{
					value = new List<ITeamTarget>();
					CatalogTargets.Add(mAllTarget.TargetCatalog, value);
				}
				value.Add(mAllTarget);
			}
			Catalogs.Sort((TeamTargetCatalog a, TeamTargetCatalog b) => services.CategorySort(a) - services.CategorySort(b));
			SortCatalogTargets(CatalogTargets);
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
	}

	private void Refresh()
	{
		mItemPool.ReturnMany(mItemList);
		foreach (KeyValuePair<TeamTargetCatalog, List<ITeamTarget>> catalogTarget in CatalogTargets)
		{
			TeamTargetCatalog key = catalogTarget.Key;
			List<ITeamTarget> value = catalogTarget.Value;
			TeamPanelTargetItem teamPanelTargetItem = mItemPool.Borrow();
			mItemList.Add(teamPanelTargetItem);
			teamPanelTargetItem.Configure(services);
			teamPanelTargetItem.Init(key, value, FireSelectTeamTarget, IsTargetSelected);
		}
	}

	private bool IsTargetSelected(ITeamTarget teamTarget)
	{
		return string.Equals(teamTarget.ToTeamTargetKey(), CurrentTarget?.ToTeamTargetKey());
	}

	private void SetCurrentTarget(ITeamTarget teamTarget)
	{
		CurrentTarget = teamTarget;
		foreach (TeamPanelTargetItem mItem in mItemList)
		{
			mItem.Refresh();
		}
	}

	private void SortCatalogTargets(Dictionary<TeamTargetCatalog, List<ITeamTarget>> catalogTargets)
	{
		foreach (KeyValuePair<TeamTargetCatalog, List<ITeamTarget>> catalogTarget in catalogTargets)
		{
			if (catalogTarget.Key == TeamTargetCatalog.DelegateTask)
			{
				catalogTarget.Value.Sort(services.CompareDelegateTargets);
			}
		}
	}
}
}
