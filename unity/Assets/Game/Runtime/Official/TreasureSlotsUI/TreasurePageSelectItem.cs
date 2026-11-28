// Complete original equipped-treasure page controller; explicit local services.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlots;
namespace Zjcs.Official.TreasureSlotsUI {
using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TreasurePageSelectItem : UIBase
{
    private ITreasurePageServices services;
    private ITreasurePageServices Services => services ?? throw new InvalidOperationException("Bind original treasure page services before activation.");
    public void Configure(ITreasurePageServices value) => services=value??throw new ArgumentNullException(nameof(value));

	[SerializeField]
	private List<ElementType> mPageOrderElementMenus;

	public Action<int> OnTreasureSelected;

	[SerializeField]
	private List<ItemIconTreasureSelectableItem> mItemList;

	[SerializeField]
	private Button mLeftBtn;

	[SerializeField]
	private Button mRightBtn;

	[SerializeField]
	private WorldStateSwitcher<bool> mSinglePageStates;

	private ElementType mType;

	private int mSelectTreasureId;

	protected override void OnEnableAndInited()
	{




		base.OnEnableAndInited();
		((UnityEvent)mLeftBtn.onClick).AddListener((UnityAction)SetPreElement);
		((UnityEvent)mRightBtn.onClick).AddListener((UnityAction)SetNextElement);
		foreach (ItemIconTreasureSelectableItem mItem in mItemList)
		{
			mItem.OnSelectedTreasure = (Action<int>)Delegate.Combine(mItem.OnSelectedTreasure, new Action<int>(OnSelectedTreasure));
		}
		Refresh();
	}

	protected override void OnDisableAndInited()
	{




		((UnityEvent)mLeftBtn.onClick).RemoveListener((UnityAction)SetPreElement);
		((UnityEvent)mRightBtn.onClick).RemoveListener((UnityAction)SetNextElement);
		foreach (ItemIconTreasureSelectableItem mItem in mItemList)
		{
			mItem.OnSelectedTreasure = (Action<int>)Delegate.Remove(mItem.OnSelectedTreasure, new Action<int>(OnSelectedTreasure));
			mItem.Clear();
		}
		mType = ElementType.None;
		base.OnDisableAndInited();
	}

	public void Init(IProfessionItemData itemData)
	{
		InitWrapper(() =>
		{
			mType = (ElementType)Services.Icons.GetElement(itemData.Info.ClassId);
			mSelectTreasureId = itemData.Info.ClassId;
		});
	}

	private void SetElementType(ElementType type)
	{
		if (mType != type)
		{
			mType = type;
			mSelectTreasureId = GetFirstElementTreasureId(mType);
			Refresh();
			OnSelectedTreasure(mSelectTreasureId);
		}
	}

	private void SetPreElement()
	{
		ElementType elementType = mType;
		int num = mPageOrderElementMenus.IndexOf(mType);
		for (int i = 1; i <= mPageOrderElementMenus.Count; i++)
		{
			int num2 = num - i;
			if (num2 < 0)
			{
				num2 += mPageOrderElementMenus.Count;
			}
			ElementType elementType2 = mPageOrderElementMenus[num2];
			if (CheckElementHasTreasure(elementType2))
			{
				elementType = elementType2;
				break;
			}
		}
		SetElementType(elementType);
	}

	private void SetNextElement()
	{
		ElementType elementType = mType;
		int num = mPageOrderElementMenus.IndexOf(mType);
		for (int i = 1; i <= mPageOrderElementMenus.Count; i++)
		{
			int index = (num + i) % mPageOrderElementMenus.Count;
			ElementType elementType2 = mPageOrderElementMenus[index];
			if (CheckElementHasTreasure(elementType2))
			{
				elementType = elementType2;
				break;
			}
		}
		SetElementType(elementType);
	}

	private bool CheckElementHasTreasure(ElementType type)
	{
		PlayerTreasureElementSlotsDataWrap treasureElementSlotData = Services.GetTreasureElementSlotData(type);
		if (treasureElementSlotData == null)
		{
			return false;
		}
		foreach (PlayerTreasureSlotItemDataWrap value in treasureElementSlotData.TreasureSlots.Values)
		{
			if (value.TreasureClassId > 0)
			{
				return true;
			}
		}
		return false;
	}

	private void ShowPageState()
	{
		int num = 0;
		foreach (ElementType mPageOrderElementMenu in mPageOrderElementMenus)
		{
			if (CheckElementHasTreasure(mPageOrderElementMenu) && ++num > 1)
			{
				break;
			}
		}
		mSinglePageStates.SetState(num <= 1);
	}

	private int GetFirstElementTreasureId(ElementType type)
	{
		PlayerTreasureElementSlotsDataWrap treasureElementSlotData = Services.GetTreasureElementSlotData(type);
		if (treasureElementSlotData == null)
		{
			return 0;
		}
		foreach (PlayerTreasureSlotItemDataWrap value in treasureElementSlotData.TreasureSlots.Values)
		{
			if (value.TreasureClassId > 0)
			{
				return value.TreasureClassId;
			}
		}
		return 0;
	}

	private void Refresh()
	{
		PlayerTreasureElementSlotsDataWrap treasureElementSlotData = Services.GetTreasureElementSlotData(mType);
		if (treasureElementSlotData == null)
		{
			return;
		}
		for (int i = 0; i < mItemList.Count; i++)
		{
			ItemIconTreasureSelectableItem itemIconTreasureSelectableItem = mItemList[i];
			int treasureClassId = 0;
			if (treasureElementSlotData.TreasureSlots.TryGetValue(i, out var value))
			{
				treasureClassId = value.TreasureClassId;
			}
			itemIconTreasureSelectableItem.InitItem(treasureClassId);
			itemIconTreasureSelectableItem.OnSelected(mSelectTreasureId);
		}
		ShowPageState();
	}

	private void OnSelectedTreasure(int treasureClassId)
	{
		OnTreasureSelected?.Invoke(treasureClassId);
		foreach (ItemIconTreasureSelectableItem mItem in mItemList)
		{
			mItem.OnSelected(treasureClassId);
		}
	}

	public bool CheckSameElement(ElementType type)
	{
		return mType == type;
	}
}

}
