// Adapted from the selected original client; see home-story-option-code source manifest.
namespace Zjcs.Official.HomeStoryUI
{
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;

public class OptionSlot : MonoBehaviour
{
	public List<OptionBaseItem> OptionItems;

	private List<OptionBaseItem> mOptionItems;

	[SerializeField]
	private RectTransform mSlot;

	[SerializeField]
	private WorldMultiLanguageText mOptionSlotTitle;

	public RectTransform Slot => mSlot;

	public void Reset()
	{
		HidenAllOption();
	}

	private void HidenAllOption()
	{
		foreach (OptionBaseItem item in GetOptionItem())
		{
			((Component)item).gameObject.SetActive(false);
		}
	}

	public void SetOptionItems(List<OptionBaseItem> optionItems)
	{
		mOptionItems = optionItems;
	}

	private List<OptionBaseItem> GetOptionItem()
	{
		if (mOptionItems == null)
		{
			return OptionItems;
		}
		return mOptionItems;
	}

	public void ShowOptions(List<DialogOptionDataBase> options, Action<int> onOptionClicked, StoryTextKeyArgWrap? titleKey = null)
	{
		if ((Object)(object)mOptionSlotTitle != (Object)null)
		{
			bool flag = titleKey.HasValue && titleKey.HasValue;
			((Component)mOptionSlotTitle).gameObject.SetActive(flag);
			if (flag)
			{
				mOptionSlotTitle.SetKey(titleKey.Value.Key, titleKey.Value.Args);
			}
		}
		List<OptionBaseItem> optionItem = GetOptionItem();
		int count = options.Count;
		int count2 = optionItem.Count;
		if (count > count2)
		{
			Debug.LogError("OptionSlot: original error branch.");
			return;
		}
		HidenAllOption();
		for (int i = 0; i < count2; i++)
		{
			if (i < count)
			{
				optionItem[i].Init(options[i], i, onOptionClicked);
				((Component)optionItem[i]).gameObject.SetActive(true);
			}
			else
			{
				((Component)optionItem[i]).gameObject.SetActive(false);
			}
		}
	}
}

}
