// Selected original acceleration UI; explicit local services only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Player = Zjcs.Official.HomeFastHangUpUI.IHomeFastHangUpPlayer;
using FastHangUpInfo = Zjcs.Official.HomeFastHangUpUI.HomeFastHangUpInfo;
using HangUpInfo = Zjcs.Official.HomeHangUpWindowsUI.HomeWindowHangUpInfo;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpUI
{

public class FurnitureFastHangUpDialog : UIBase
{
    public IHomeFastHangUpServices Services { protected get; set; }
	public MultiLanguageText HangUpSecondsText;

	public Button FastBtn;

	public Transform ItemSlot;

	public FurnitureUseItemSliderGroup ItemPrefab;

	public ScrollRect PreviewScrollRect;

	[NonSerialized]
	public GameObject ParentPanel;

	private GameObjectPool<FurnitureUseItemSliderGroup> mItemSliderPool;

	private readonly List<FurnitureUseItemSliderGroup> mItems = new List<FurnitureUseItemSliderGroup>();

	private readonly Dictionary<int, long> mItemCost = new Dictionary<int, long>();

	private readonly Dictionary<int, ItemIcon> mPreviewItemDic = new Dictionary<int, ItemIcon>();

	private readonly Dictionary<int, double> mHourOutputDic = new Dictionary<int, double>();

	private readonly Dictionary<int, double> mHourUnsettledDic = new Dictionary<int, double>();

	private int mFastHangUpId;

	private bool mAutoReceive;

	private EFurniture mFurniture;

	[SerializeField]
	private WorldStateSwitcher<bool> mEmptyStateSwitcher;

	private GameObjectPool<FurnitureUseItemSliderGroup> ItemSliderPool => mItemSliderPool ?? (mItemSliderPool = new GameObjectPool<FurnitureUseItemSliderGroup>(transform, ItemSlot, ItemPrefab));

	private bool IsItemEmpty => mItems.Count == 0;

	private void Awake()
	{
		FastBtn.onClick.AddListener(OnFastBtnClick);
	}

	protected override void OnDisableAndInited()
	{
		base.OnEnableAndInited();
		ItemSliderPool.ReturnMany(mItems);
		if (Services != null)
		{
			foreach (KeyValuePair<int, ItemIcon> item in mPreviewItemDic)
			{
				Services.RecycleIcon(item.Value);
			}
		}
		mPreviewItemDic.Clear();
		mHourOutputDic.Clear();
	}

	public void Init(int fastHangUpId, Action<long> fastHangUpCallback)
	{
		InitWrapper(null);
		ParentPanel = null;
		FastHangUpInfo fastHangUpInfo = Services.GetFastHangUpInfo(fastHangUpId);
		mFastHangUpId = fastHangUpInfo.FastHangUpId;
		mAutoReceive = fastHangUpInfo.AutoReceive;
		mFurniture = fastHangUpInfo.BindFurniture;
		ItemSliderPool.ReturnMany(mItems);
		foreach (int key in fastHangUpInfo.FastHangUpItemDic.Keys)
		{
			long itemAmount = Services.Player.GetItemAmount(key);
			if (itemAmount != 0L)
			{
				FurnitureUseItemSliderGroup furnitureUseItemSliderGroup = ItemSliderPool.Borrow();
				long num = fastHangUpInfo.MaxUseItemCount;
				int num2 = Mathf.Clamp(Services.DefaultFastItemUseCount, 1, (int)num);
				furnitureUseItemSliderGroup.Init(key, num2, itemAmount, num);
				furnitureUseItemSliderGroup.OnValueChangedEvent = OnItemValueChanged;
				mItems.Add(furnitureUseItemSliderGroup);
			}
		}
		Player mainPlayer = Services.Player;
		PreviewScrollRect.horizontalNormalizedPosition = 0f;
		ItemIcon value;
		foreach (int hangUpId in fastHangUpInfo.HangUpIdList)
		{
			if (mainPlayer.HasUnlockedHangUp(hangUpId) && !mPreviewItemDic.TryGetValue(hangUpId, out value))
			{
				ItemInfo hangUpRewardItem = Services.GetHangUpRewardItem(hangUpId);
				ItemIcon icon = Services.GetIcon(hangUpRewardItem, PreviewScrollRect.content);
				icon.SetClickPopupInfoDialog();
				mPreviewItemDic.Add(hangUpId, icon);
				double hangUpReward = Services.Window.GetHangUpReward(hangUpId, 1.0);
				mHourOutputDic.Add(hangUpId, hangUpReward);
				Services.GetHangUpUnsettledRewardCount(hangUpId, out var remainderSeconds);
				mHourUnsettledDic.Add(hangUpId, (remainderSeconds / 3600.0));
			}
		}
		foreach (int extraHangUpReward in fastHangUpInfo.ExtraHangUpRewardList)
		{
			HangUpInfo hangUpInfo = Services.Window.GetHangUpInfo(extraHangUpReward);
			if (mainPlayer.HasFurniture(hangUpInfo.BindFurniture) && !mPreviewItemDic.TryGetValue(extraHangUpReward, out value))
			{
				ItemInfo hangUpRewardItem2 = Services.GetHangUpRewardItem(extraHangUpReward);
				if (hangUpRewardItem2 != null)
				{
					ItemIcon icon2 = Services.GetIcon(hangUpRewardItem2, PreviewScrollRect.content);
					icon2.SetClickPopupInfoDialog();
					mPreviewItemDic.Add(extraHangUpReward, icon2);
					double hangUpReward2 = Services.Window.GetHangUpReward(extraHangUpReward, 1.0, 1);
					mHourOutputDic.Add(extraHangUpReward, hangUpReward2);
					mHourUnsettledDic.Add(extraHangUpReward, 0.0);
				}
			}
		}
		Refresh();
	}

	private void OnFastBtnClick()
	{
		mItemCost.Clear();
		foreach (FurnitureUseItemSliderGroup mItem in mItems)
		{
			long useCount = mItem.UseCount;
			if (useCount > 0)
			{
				mItemCost.Add(mItem.ItemId, useCount);
			}
		}
		if (mItemCost.Count == 0)
		{
			Services.Window.SystemMessage(Services.Window.Text("fast_hang_up_no_item_tip"));
			return;
		}
		Player mainPlayer = Services.Player;
		bool flag = true;
		foreach (KeyValuePair<int, double> item in mHourOutputDic)
		{
			if (!mainPlayer.TryGetFastHangUpTip(item.Key, CalcSpeedHours(), out var _, out var titleKey, out var key))
			{
				continue;
			}
			Services.Window.Confirm(key, titleKey, FastBedHangUp);
			flag = false;
			break;
		}
		if (flag)
		{
			FastBedHangUp();
		}
	}

	private void OnItemValueChanged(int _, long __)
	{
		Refresh();
	}

	private void FastBedHangUp()
	{
		HangUpUIUtils.OperFastHangUp(Services, mFastHangUpId, mAutoReceive, mItemCost, null, () =>
		{
			if (ParentPanel != null)
			{
				Services.Window.Close(ParentPanel);
			}
			if (mFurniture == EFurniture.Bed)
			{
				Services.Window.Close(gameObject);
			}
		});
	}

	private void Refresh()
	{
		bool isItemEmpty = IsItemEmpty;
		mEmptyStateSwitcher.SetState(isItemEmpty);
		if (!isItemEmpty)
		{
			int num = (int)CalcSpeedHours();
			Preview(num);
			HangUpSecondsText.Format(CultureNumberArg.Compact(num));
		}
	}

	private long CalcSpeedHours()
	{
		long num = 0L;
		foreach (FurnitureUseItemSliderGroup mItem in mItems)
		{
			if (mItem.UseCount == 0L)
			{
				continue;
			}
			if (Services.TryGetFastHangUpItemHours(mItem.ItemId, out var hangUpHour))
			{
				num += hangUpHour * mItem.UseCount;
				continue;
			}
		}
		return num;
	}

	private void Preview(int hour)
	{
		foreach (KeyValuePair<int, ItemIcon> item in mPreviewItemDic)
		{
			if (mHourOutputDic.TryGetValue(item.Key, out var value))
			{
				double num = mHourUnsettledDic[item.Key];
				long count = (long)(value * (num + (double)hour));
				item.Value.SetCount(count);
			}
		}
	}
}

}
