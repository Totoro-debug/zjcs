namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUseItemContent : UIBase
{
    private IProfessionUpgradeBindings mBindings;
    protected IProfessionUpgradeBindings Bindings => mBindings ?? ProfessionUpgradeBindings.Required;
    public void Configure(IProfessionUpgradeBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public enum UseItemState
	{
		Normal,
		Limit,
		Max
	}

	public WorldStateSwitcher<UseItemState> UseItemStateSwitcher;

	public Button UseItemBtn;

	public Transform MaterialSlot;

	public WorldMultiLanguageText CannotMakeText;

	public WorldMultiLanguageText UseBtnTxt;

	public WorldMultiLanguageText UseTitleTxt;

	public WorldMultiLanguageText UseBtnTipTxt;

	private Action mUseItemBtnHandler;

	private Action<List<ItemIcon>> mItemIconInitHandler;

	private bool mIsShowMergeTip;

	private Dictionary<int, long> mItems;


	public List<ItemIcon> ItemIconList { get; private set; } = new List<ItemIcon>();

	private void Awake()
	{
		UseItemBtn.onClick.AddListener(OnClickUseItemButton);
	}

	protected override void OnEnableAndInited()
	{
		Bindings.MainPlayer.OnItemCountUpdate += PlayerOnItemCountUpdate;
		Refresh(mItems);
	}

	protected override void OnDisableAndInited()
	{
		if (Bindings.MainPlayer != null)
		{
			Bindings.MainPlayer.OnItemCountUpdate -= PlayerOnItemCountUpdate;
		}
	}

	public void Init(Dictionary<int, long> items, Action<List<ItemIcon>> itemIconInitHandler = null, bool isShowMergeTip = false)
	{
		InitWrapper(() =>
		{
			mItems = items;
			mItemIconInitHandler = itemIconInitHandler;
			mIsShowMergeTip = isShowMergeTip;
		});
	}

	public void SetUseItemBtnHandler(Action handler)
	{
		mUseItemBtnHandler = handler;
	}

	public void Refresh(Dictionary<int, long> items)
	{
		CleanItemIconList();
		mItems = items;
		if (mItems != null)
		{
			foreach (KeyValuePair<int, long> mItem in mItems)
			{
				ItemIcon item = Bindings.GetIcon(mItem.Key, MaterialSlot).SetRequireCount(mItem.Value).SetClickPopupInfoDialog();
				ItemIconList.Add(item);
			}
		}
		if (mIsShowMergeTip)
		{
			ShowItemIsCanMerge(items);
		}
		mItemIconInitHandler?.Invoke(ItemIconList);
	}

	private void CleanItemIconList()
	{
		foreach (ItemIcon itemIcon in ItemIconList)
		{
			Bindings.RecycleIcon(itemIcon);
		}
		ItemIconList.Clear();
		MaterialSlot.DestroyActivedChildren();
	}

	private void OnClickUseItemButton()
	{
		int moneyId = Bindings.MoneyId;
		if (mItems != null && mItems.TryGetValue(moneyId, out var value))
		{
			IProfessionUpgradePlayer mainPlayer = Bindings.MainPlayer;
			long itemAmount = mainPlayer.GetItemAmount(Bindings.MoneyId);
			if (value > itemAmount)
			{
				bool flag = true;
				int num = default;
				long num2 = default;
				foreach (KeyValuePair<int, long> mItem in mItems)
				{
					mItem.Deconstruct(out num, out num2);
					int num3 = num;
					long num4 = num2;
					if (num3 != moneyId && mainPlayer.GetItemAmount(num3) < num4)
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					Bindings.ShowLackOfItem(Bindings.MoneyId, value - itemAmount);
					return;
				}
			}
		}
		mUseItemBtnHandler?.Invoke();
	}

	private void PlayerOnItemCountUpdate(IProfessionItemData itemData, long preCount, object sourceData)
	{
		if (mItems != null && mItems.ContainsKey(itemData.Id))
		{
			Refresh(mItems);
		}
	}

	public void SetUseItemState(UseItemState state)
	{
		UseItemStateSwitcher.SetState(state);
	}

	public void SetLimitKey(string key, params object[] arr)
	{
		CannotMakeText.SetKey(key, arr);
	}

	public void SetUseKey(string key, params object[] arr)
	{
		UseBtnTxt.SetKey(key, arr);
	}

	public void SetUseTitleKey(string key, params object[] arr)
	{
		UseTitleTxt.SetKey(key, arr);
	}

	public void SetUseBtnTipKey(ProfessionItemText textKeyArgWrap)
	{
		UseBtnTipTxt.SetKey(textKeyArgWrap.Key, textKeyArgWrap.Args);
	}

	public void ClearItemIconList()
	{
		if (mItems != null)
		{
			mItems.Clear();
		}
		ItemIconList.Clear();
	}

	private void ShowItemIsCanMerge(Dictionary<int, long> costDict)
	{
		IProfessionUpgradePlayer mainPlayer = Bindings.MainPlayer;
		int num = default;
		long num2 = default;
		foreach (KeyValuePair<int, long> item in costDict)
		{
			item.Deconstruct(out num, out num2);
			int num3 = num;
			long num4 = num2;
			long itemAmount = mainPlayer.GetItemAmount(num3);
			if (itemAmount >= num4 || !Bindings.IsItemSourceFromMerge(mainPlayer, num3) || itemAmount + Bindings.CalcCanMergeAmount(mainPlayer, num3, recursion: true) < num4)
			{
				continue;
			}
			foreach (ItemIcon itemIcon in ItemIconList)
			{
				if (itemIcon.myInfo.ClassId == num3)
				{
					itemIcon.SetAttachment(ItemIconAttachmentType.MergeTip, show: true);
				}
			}
		}
	}
}

}
