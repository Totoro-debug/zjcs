using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;

namespace Zjcs.Official.HomeHudUI
{
public class PublicBottomBar : MonoBehaviour
{
	public IHomeBottomBarServices Services { get; set; }

	public enum UIState
	{
		Show,
		Hide
	}

	public enum TabType
	{
		Character,
		Home,
		TravelNote,
		Legion,
		Mainland
	}

	[Serializable]
	public class TabData
	{
		public TabType TabType;

		public TabItem Toggle;
	}

	public WorldStateSwitcher<UIState> StateSwitcher;

	public TabGroup ToggleGroup;

	public List<TabData> TabDatas;

	private TabType mCurrentTabType;

	[SerializeField]
	private WorldStateSwitcher<bool> mUpgradeStateSwitcher;

	private Dictionary<TabItem, TabData> mToggle2TabDatas = new Dictionary<TabItem, TabData>();

	private HashSet<object> mReferenceSet = new HashSet<object>();

	public static PublicBottomBar Instance { get; private set; }

	public TabType CurrentTabType
	{
		get
		{
			return mCurrentTabType;
		}
		set
		{
			if (mCurrentTabType != value)
			{
				TabType arg = mCurrentTabType;
				mCurrentTabType = value;
				OnTabTypeChanged?.Invoke(arg, mCurrentTabType);
			}
		}
	}

	public bool IsShow { get; private set; } = true;

	public event Action OnShowBar;

	public event Action OnHideBar;

	public event Action<TabType, TabType> OnTabTypeChanged;

	private void Awake()
	{
		Instance = this;
		((UnityEvent<TabItem, bool>)ToggleGroup.OnTabItemChanged).AddListener((UnityAction<TabItem, bool>)OnToggleGroupToggleChanged);
		Services.OnSubRankUpdate += MainPlayer_OnSubRankUpdate;
		Services.OnPlanesRankUnlock += MainPlayer_OnPlanesRankUnlock;
		TabItem firstActiveTabItem = ToggleGroup.GetFirstActiveTabItem();
		foreach (TabData tabData in TabDatas)
		{
			mToggle2TabDatas.Add(tabData.Toggle, tabData);
			if ((Object)(object)tabData.Toggle == (Object)(object)firstActiveTabItem)
			{
				mCurrentTabType = tabData.TabType;
			}
		}
		RefreshSubRankUpgradeShow();
	}

	private void OnDestroy()
	{
		Instance = null;
		((UnityEvent<TabItem, bool>)ToggleGroup.OnTabItemChanged).RemoveListener((UnityAction<TabItem, bool>)OnToggleGroupToggleChanged);
		if (Services != null && Services.HasPlayer)
		{
			Services.OnSubRankUpdate -= MainPlayer_OnSubRankUpdate;
			Services.OnPlanesRankUnlock -= MainPlayer_OnPlanesRankUnlock;
		}
	}

	private void OnToggleGroupToggleChanged(TabItem arg0, bool arg1)
	{
		if (arg1)
		{
			mToggle2TabDatas.TryGetValue(arg0, out var value);
			CurrentTabType = value.TabType;
		}
	}

	public void TryShowBottomBar(object obj, bool instant = false)
	{
		if (mReferenceSet.Remove(obj))
		{
			UpdateState(instant);
		}
	}

	public void HideBottomBar(object obj, bool instant = false)
	{
		if (mReferenceSet.Add(obj))
		{
			UpdateState(instant);
		}
	}

	private void UpdateState(bool instant = false)
	{
		if (mReferenceSet.Count > 0)
		{
			if (instant)
			{
				((Component)this).gameObject.SetActive(false);
			}
			else
			{
				StateSwitcher.SetState(UIState.Hide);
			}
			IsShow = false;
			OnHideBar?.Invoke();
		}
		else
		{
			if (instant)
			{
				((Component)this).gameObject.SetActive(true);
			}
			else
			{
				StateSwitcher.SetState(UIState.Show);
			}
			IsShow = true;
			OnShowBar?.Invoke();
		}
	}

	public void SelectTab(TabType tabType)
	{
		for (int i = 0; i < TabDatas.Count; i++)
		{
			TabData tabData = TabDatas[i];
			if (tabData.TabType == tabType)
			{
				ToggleGroup.ResetTabItem(needNotify: true, tabData.Toggle);
			}
		}
	}

	private void MainPlayer_OnSubRankUpdate()
	{
		RefreshSubRankUpgradeShow();
	}

	private void MainPlayer_OnPlanesRankUnlock()
	{
		RefreshSubRankUpgradeShow();
	}

	private void RefreshSubRankUpgradeShow()
	{
		if (Services.CheckSubRankUpgradeShow())
		{
			mUpgradeStateSwitcher.SetState(state: true);
		}
		else
		{
			mUpgradeStateSwitcher.SetState(state: false);
		}
	}
}

}
