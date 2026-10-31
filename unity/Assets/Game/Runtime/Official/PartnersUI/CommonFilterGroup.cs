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

public abstract class CommonFilterGroup<T> : BaseFilterGroup where T : struct
{
	protected List<BaseFilterGroupItem<T>> mOptionItems = new List<BaseFilterGroupItem<T>>();

	private bool mIsAwake;

	private HashSet<object> mSelectedOptions = new HashSet<object>();

	protected virtual void Awake()
	{
		InitAllItems();
		mIsAwake = true;
	}

	protected void InitAllItems()
	{
		mOptionItems.Clear();
		mOptionBtns.Clear();
		for (int i = 0; i < ItemContainer.childCount; i++)
		{
			Transform child = ItemContainer.GetChild(i);
			SwitchShowBtn btn = ((Component)child).GetComponent<SwitchShowBtn>();
			mOptionBtns.Add(btn);
			btn.SetBtnTextKey(LanguageUtils.GetEnumKey(((Component)child).GetComponent<BaseFilterGroupItem<T>>().FilterType));
			BaseFilterGroupItem<T> item = ((Component)child).GetComponent<BaseFilterGroupItem<T>>();
			mOptionItems.Add(item);
			btn.SetListener(() =>
			{
				OnOptionChanged?.Invoke(item.FilterType, btn.IsSelected);
			});
			if (mSelectedOptions.Contains(item.FilterType))
			{
				btn.SwitchShowSelected();
			}
		}
		mSelectedOptions.Clear();
	}

	public void InitFilterDefault(List<T> selectedOptions)
	{
		DefaultSelectedOptions.Clear();
		foreach (T selectedOption in selectedOptions)
		{
			DefaultSelectedOptions.Add(selectedOption);
		}
	}

	public override void ResetAllStates()
	{
		ShowSelectedOption((BaseFilterGroupItem<T> item) => DefaultSelectedOptions.Contains(item.FilterType));
		mSelectedOptions.Clear();
	}

	public override void SelectedState(HashSet<object> selectedOptions)
	{
		if (!mIsAwake)
		{
			mSelectedOptions = selectedOptions;
			return;
		}
		ShowSelectedOption((BaseFilterGroupItem<T> item) => selectedOptions.Contains(item.FilterType));
	}

	protected virtual void ShowSelectedOption(Func<BaseFilterGroupItem<T>, bool> filter)
	{
		for (int i = 0; i < mOptionBtns.Count; i++)
		{
			BaseFilterGroupItem<T> arg = mOptionItems[i];
			if (filter(arg))
			{
				mOptionBtns[i].SwitchShowSelected();
			}
			else
			{
				mOptionBtns[i].SwitchShowNormal();
			}
		}
	}
}

}
