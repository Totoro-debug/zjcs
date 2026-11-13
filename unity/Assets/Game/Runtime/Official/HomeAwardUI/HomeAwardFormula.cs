// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
public static class HomeAwardFormula
{
	public static void MergeItemAndSort<T>(List<T> itemList, List<T> itemList2) where T : IMergeableItem, IComparable<T>
	{
		MergeItem(itemList, itemList2);
		itemList.Sort();
	}

	public static void MergeItemAndSort<T>(List<T> itemList) where T : IMergeableItem, IComparable<T>
	{
		MergeItem(itemList);
		itemList.Sort();
	}

	public static void MergeItemAndSort<T>(List<T> itemList, T item) where T : IMergeableItem, IComparable<T>
	{
		MergeItem(itemList, item);
		itemList.Sort();
	}
	public static int GetAwardItemTypeScoreForSortAward(ItemInfo info)
	{
		if (info.ItemType == ItemType.Prop)
		{
			return 2;
		}
		if (info.ItemType == ItemType.Money)
		{
			return 1;
		}
		return 0;
	}

	public static void MergeItem<T>(List<T> itemList, T item) where T : IMergeableItem
	{
		if (!item.IsCanMerge())
		{
			itemList.Add(item);
			return;
		}
		foreach (T item2 in itemList)
		{
			if (item2.TryMerge(item))
			{
				return;
			}
		}
		itemList.Add(item);
	}

	public static void MergeItem<T>(List<T> awardList, List<T> awardList2) where T : IMergeableItem
	{
		awardList.AddRange(awardList2);
		MergeItem(awardList);
	}

	public static void MergeItem<T>(List<T> items) where T : IMergeableItem
	{
		List<T> list = new List<T>();
		for (int num = items.Count - 1; num >= 0; num--)
		{
			T item = items[num];
			if (item.IsCanMerge())
			{
				list.Add(item);
				items.RemoveAt(num);
			}
		}
		foreach (T item2 in list)
		{
			MergeItem(items, item2);
		}
	}

}
}
