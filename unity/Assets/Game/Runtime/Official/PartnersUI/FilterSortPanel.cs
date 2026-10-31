// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FilterSortPanel : MonoBehaviour
{
	[Header("动画控制")]
	public Zjcs.Official.WorldUI.WorldStateSwitcher<int> StateSwitcher;

	public int AnimatorStatusId_Closed;

	public int AnimatorStatusId_Open = 1;

	[Header("面板和按钮")]
	public GameObject FilterSortDialog;

	public Button SwitchBtn;

	public Button CloseBtn;

	public MultiLanguageText SwitchBtnText;

	public MultiLanguageText FilterTitleText;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldStateSwitcher<bool> mIsFiltingSwitcher;

	public List<BaseSortGroup> SortGroups;

	public Button ResetFilterBtn;

	public List<BaseFilterGroup> FilterGroups;

	public List<BaseSwitchGroup> SwitchGroups;

	public bool IsReverse;

	private bool mIsEnable;

	public Dictionary<CommonFilterType, HashSet<object>> FilterResult { get; protected set; } = new Dictionary<CommonFilterType, HashSet<object>>();

	public Dictionary<CommonFilterType, HashSet<object>> CacheSelecteParam { get; set; } = new Dictionary<CommonFilterType, HashSet<object>>();

	public event Action<object, bool, bool> OnSortOptionChanged;

	public event Action<CommonSwitchType, object> OnSwitchOptionChanged;

	public event Action<Dictionary<CommonFilterType, HashSet<object>>> OnFilterOptionChanged;

	public event Action OnFilterSortPanelShow;

	protected virtual void Awake()
	{
		((UnityEvent)SwitchBtn.onClick).AddListener((UnityAction)OnSwitchBtnClick);
		((UnityEvent)CloseBtn.onClick).AddListener((UnityAction)OnClickCloseBtn);
		if ((Object)(object)ResetFilterBtn != (Object)null)
		{
			((UnityEvent)ResetFilterBtn.onClick).AddListener((UnityAction)OnFilterResetBtnClick);
		}
		for (int i = 0; i < SortGroups.Count; i++)
		{
			BaseSortGroup baseSortGroup = SortGroups[i];
			baseSortGroup.OnOptionChanged = (Action<object, bool, bool>)Delegate.Combine(baseSortGroup.OnOptionChanged, (Action<object, bool, bool>)((object type, bool isOn, bool IsReverse) =>
			{
				OnSortOptionChanged(type, isOn, IsReverse);
			}));
		}
		for (int num = 0; num < SwitchGroups.Count; num++)
		{
			BaseSwitchGroup group = SwitchGroups[num];
			BaseSwitchGroup baseSwitchGroup = group;
			baseSwitchGroup.OnOptionChanged = (Action<object>)Delegate.Combine(baseSwitchGroup.OnOptionChanged, (Action<object>)((object type) =>
			{
				OnSwitchOptionChanged(group.SwitchGroupType, type);
			}));
		}
		for (int num2 = 0; num2 < FilterGroups.Count; num2++)
		{
			BaseFilterGroup group2 = FilterGroups[num2];
			BaseFilterGroup baseFilterGroup = group2;
			baseFilterGroup.OnOptionChanged = (Action<object, bool>)Delegate.Combine(baseFilterGroup.OnOptionChanged, (Action<object, bool>)((object type, bool isOn) =>
			{
				SetFilterResult(group2.FilterGroupType, type, isOn);
			}));
		}
	}

	protected virtual void OnEnable()
	{
		mIsEnable = true;
		if (CacheSelecteParam.Count <= 0)
		{
			Reset();
			return;
		}
		foreach (KeyValuePair<CommonFilterType, HashSet<object>> item in CacheSelecteParam)
		{
			CommonFilterType key = item.Key;
			HashSet<object> value = item.Value;
			TrySelectedFliterGroupAtEnable(key, value, needNotify: false);
		}
		CacheSelecteParam.Clear();
	}

	protected virtual void OnDisable()
	{
		CloseFuncBtnPanel();
		mIsEnable = false;
		CacheSelecteParam.Clear();
	}

	public void InitFilterDefault(CommonFilterType type, HashSet<object> selectedOptions)
	{
		for (int i = 0; i < FilterGroups.Count; i++)
		{
			BaseFilterGroup baseFilterGroup = FilterGroups[i];
			if (baseFilterGroup.FilterGroupType == type)
			{
				baseFilterGroup.DefaultSelectedOptions.Clear();
				baseFilterGroup.DefaultSelectedOptions.UnionWith(selectedOptions);
			}
		}
	}

	public void SelecteFilterGroup(CommonFilterType type, HashSet<object> selectedOptions, bool needNotify = false)
	{
		if (!TrySelectedFliterGroupAtEnable(type, selectedOptions, needNotify))
		{
			CacheSelecteParam.Add(type, selectedOptions);
		}
	}

	private bool TrySelectedFliterGroupAtEnable(CommonFilterType type, HashSet<object> selectedOptions, bool needNotify)
	{
		if (!mIsEnable)
		{
			return false;
		}
		for (int i = 0; i < FilterGroups.Count; i++)
		{
			BaseFilterGroup baseFilterGroup = FilterGroups[i];
			if (baseFilterGroup.FilterGroupType != type)
			{
				continue;
			}
			baseFilterGroup.SelectedState(selectedOptions);
			foreach (object selectedOption in selectedOptions)
			{
				SetFilterResult(type, selectedOption, isOn: true, notifyChanged: false);
			}
		}
		mIsFiltingSwitcher.SetState(FilterResult.Count > 0);
		if (needNotify)
		{
			OnFilterOptionChanged?.Invoke(FilterResult);
		}
		return true;
	}

	protected void SetFilterResult(CommonFilterType type, object target, bool isOn, bool notifyChanged = true)
	{
		if (FilterResult.TryGetValue(type, out var value))
		{
			if (!value.Add(target))
			{
				value.Remove(target);
				if (value.Count == 0)
				{
					FilterResult.Remove(type);
				}
			}
		}
		else
		{
			value = new HashSet<object> { target };
			FilterResult[type] = value;
		}
		mIsFiltingSwitcher.SetState(FilterResult.Count > 0);
		if (notifyChanged)
		{
			OnFilterOptionChanged?.Invoke(FilterResult);
		}
	}

	public void ClearFilterResult(CommonFilterType type, bool notifyChanged = true)
	{
		if (!FilterResult.TryGetValue(type, out var _))
		{
			return;
		}
		FilterResult.Remove(type);
		for (int i = 0; i < FilterGroups.Count; i++)
		{
			BaseFilterGroup baseFilterGroup = FilterGroups[i];
			if (!((Component)baseFilterGroup).gameObject.activeSelf || baseFilterGroup.FilterGroupType != type)
			{
				continue;
			}
			baseFilterGroup.ResetAllStates();
			if (baseFilterGroup.DefaultSelectedOptions.Count <= 0)
			{
				break;
			}
			foreach (object defaultSelectedOption in baseFilterGroup.DefaultSelectedOptions)
			{
				SetFilterResult(baseFilterGroup.FilterGroupType, defaultSelectedOption, isOn: true, notifyChanged: false);
			}
			break;
		}
		mIsFiltingSwitcher.SetState(FilterResult.Count > 0);
		if (notifyChanged)
		{
			OnFilterOptionChanged?.Invoke(FilterResult);
		}
	}

	public void CloseFuncBtnPanel()
	{
		StateSwitcher.SetState(AnimatorStatusId_Closed);
	}

	private void OnSwitchBtnClick()
	{
		if (!FilterSortDialog.activeSelf)
		{
			StateSwitcher.SetState(AnimatorStatusId_Open);
			OnFilterSortPanelShow?.Invoke();
		}
		else
		{
			CloseFuncBtnPanel();
		}
	}

	private void OnClickCloseBtn()
	{
		CloseFuncBtnPanel();
	}

	private void OnFilterResetBtnClick()
	{
		ResetFilter();
		OnFilterOptionChanged?.Invoke(FilterResult);
	}

	public virtual void ResetFilter()
	{
		FilterResult.Clear();
		for (int i = 0; i < FilterGroups.Count; i++)
		{
			BaseFilterGroup baseFilterGroup = FilterGroups[i];
			if (!((Component)baseFilterGroup).gameObject.activeSelf)
			{
				continue;
			}
			baseFilterGroup.ResetAllStates();
			if (baseFilterGroup.DefaultSelectedOptions.Count <= 0)
			{
				continue;
			}
			foreach (object defaultSelectedOption in baseFilterGroup.DefaultSelectedOptions)
			{
				SetFilterResult(baseFilterGroup.FilterGroupType, defaultSelectedOption, isOn: true, notifyChanged: false);
			}
		}
		mIsFiltingSwitcher.SetState(FilterResult.Count > 0);
	}

	public virtual void Reset()
	{
		for (int i = 0; i < SortGroups.Count; i++)
		{
			SortGroups[i].ResetTabItem();
		}
		ResetFilter();
		CacheSelecteParam.Clear();
	}

	public virtual T GetSortGroup<T>() where T : BaseSortGroup
	{
		for (int i = 0; i < SortGroups.Count; i++)
		{
			if (SortGroups[i] is T result)
			{
				return result;
			}
		}
		return null;
	}

	public virtual T GetSwitchGroup<T>() where T : BaseSwitchGroup
	{
		for (int i = 0; i < SwitchGroups.Count; i++)
		{
			if (SwitchGroups[i] is T result)
			{
				return result;
			}
		}
		return null;
	}

	public virtual T GetFilterGroup<T>() where T : BaseFilterGroup
	{
		for (int i = 0; i < FilterGroups.Count; i++)
		{
			if (FilterGroups[i] is T result)
			{
				return result;
			}
		}
		return null;
	}
}

}
