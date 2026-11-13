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

public class FurnitureUseItemSliderGroup : MonoBehaviour, IGameObjectPoolItem
{
    public IHomeFastHangUpServices Services { protected get; set; }
	[SerializeField]
	private Transform mItemSlot;

	[SerializeField]
	private SetUseCountSlider mSlider;

	[SerializeField]
	private MultiLanguageText mNameTxt;

	public Action<int, long> OnValueChangedEvent;

	private int mItemId;

	private ItemIcon mItemIcon;

	private long mMaxUseCount;

	public long UseCount => (long)mSlider.Slider.value;

	public int ItemId => mItemId;

	private void Awake()
	{
		mSlider.OnCurrentCountUpdate += OnValueChanged;
	}

	private void OnDisable()
	{
		OnValueChangedEvent = null;
		RecycleItemIcon();
	}

	public void Init(int itemId, long useCount, long maxCount, long maxUseCount)
	{
		mItemId = itemId;
		mItemIcon = Services.GetIcon(itemId, mItemSlot).SetClickPopupInfoDialog().HideText();
		mNameTxt.SetKey(Services.Window.GetItemNameKey(itemId));
		mMaxUseCount = maxUseCount;
		mSlider.Init(0L, maxCount, useCount, mMaxUseCount, "ui_fast_hang_up_max_count_tip");
	}

	public void UpdateMaxCount(long maxCount)
	{
		mSlider.Init(0L, maxCount, maxCount, mMaxUseCount, "ui_fast_hang_up_max_count_tip");
	}

	private void OnValueChanged(long value)
	{
		OnValueChangedEvent?.Invoke(mItemId, value);
	}

	private void RecycleItemIcon()
	{
		if (mItemIcon != null)
		{
			Services.RecycleIcon(mItemIcon);
			mItemIcon = null;
		}
	}

	private long ClampCurrentCount(long count)
	{
		if (mMaxUseCount < 0)
		{
			return count;
		}
		if (count >= mMaxUseCount)
		{
			return mMaxUseCount;
		}
		return count;
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
