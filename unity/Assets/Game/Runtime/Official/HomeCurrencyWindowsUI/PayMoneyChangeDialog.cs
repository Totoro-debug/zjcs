// Selected original currency-window implementation; typed local service boundaries.
using Object=UnityEngine.Object;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
using StateSwitcher=Zjcs.Official.WorldUI.WorldStateSwitcher<bool>;
using PlayerItemDataWrap=Zjcs.Official.ProfessionUI.IProfessionItemData;
using AwardSourceData=System.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeCurrencyWindowsUI
{

public class PayMoneyChangeDialog : UIBase
{
    public IHomeCurrencyWindowServices Services { private get; set; }
	public ItemIcon FlareStarItemIcon;

	public ItemIcon DiamondItemIcon;

	public SetCountSlider SetCountSlider;

	public MultiLanguageText ExchangeTxt;

	public GameObject CountSliderGo;

	public MultiLanguageText ExchangeFixedTxt;

	public Button CancelBtn;

	public Button ConfirmBtn;

	private long mCurrentCount;

	protected override void Start()
	{




		((UnityEvent)CancelBtn.onClick).AddListener((UnityAction)CloseWindow);
		((UnityEvent)ConfirmBtn.onClick).AddListener((UnityAction)OnConfirmBtnClick);
		SetCountSlider.OnCurrentCountUpdate += OnCurrentCountUpdate;
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		FlareStarItemIcon.Init(Services.FlareStarId).SetBottomName();
		DiamondItemIcon.Init(Services.DiamonId).SetBottomName();
		Services.RegisterAwardHandler(AwardHandle);
		Services.Player.OnItemCountUpdate += OnItemCountUpdate;
		if (mCurrentCount > 0)
		{
			ShowChangeCount(mCurrentCount);
		}
	}

	protected override void OnDisableAndInited()
	{
		if (Services.AwardServiceAvailable)
		{
			Services.RemoveAwardHandler(AwardHandle);
		}
		if (Services.Player != null)
		{
			Services.Player.OnItemCountUpdate -= OnItemCountUpdate;
		}
		base.OnDisableAndInited();
	}

	private void OnCurrentCountUpdate(long currentCount)
	{
		mCurrentCount = currentCount;
		ShowChangeCount(currentCount);
	}

	public void Init(long count = 0L)
	{
		InitWrapper(() =>
		{
			RefreshMax(count);
		});
	}

	private void ShowChangeCount(long count)
	{
		FlareStarItemIcon.SetCount(count);
		DiamondItemIcon.SetCount(count * Services.Pay.DiamonChangeScale);
	}

	private void OnConfirmBtnClick()
    {
        long itemAmount = Services.Player.GetItemAmount(Services.FlareStarId);
        if (itemAmount <= 0 || mCurrentCount > itemAmount)
        {
            Services.PopupConfirm(this, "UI_Pay_Change_Item_Not_Enough_Confirm", () =>
            {
                Services.PopupDirectPay(this, () => Services.Close(gameObject));
            });
        }
        else
        {
            Services.PlayerDiamonChange(mCurrentCount, () => CloseWindow());
        }
    }

	private void CloseWindow()
	{
		Services.ClosePayMoneyChangeDialog();
	}

	private bool AwardHandle(HomeCurrencyAwardMessage msg)
    {
        if (!Services.IsPayAward(msg)) return false;
        Services.PopupAward(msg);
        return true;
    }

	private void OnItemCountUpdate(PlayerItemDataWrap itemData, long preCount, AwardSourceData sourceData)
	{
		if (itemData.Id == Services.FlareStarId)
		{
			RefreshMax(mCurrentCount);
		}
	}

	private void RefreshMax(long count)
	{
		if (count > 0)
		{
			mCurrentCount = count;
			ExchangeFixedTxt.Format(mCurrentCount);
			((Component)ExchangeFixedTxt).gameObject.SetActive(true);
			CountSliderGo.SetActive(false);
		}
		else
		{
			HomeCurrencyPaySettings pay = Services.Pay;
			long itemAmount = Services.Player.GetItemAmount(Services.FlareStarId);
			long num = Math.Min(itemAmount, pay.DiamonChangeMax);
			mCurrentCount = ((itemAmount > 0) ? MathUtils.Clamp(count, pay.DiamonChangeMin, num) : 0);
			SetCountSlider.Init(pay.DiamonChangeMin, num, mCurrentCount, 1L);
			CountSliderGo.SetActive(true);
			((Component)ExchangeFixedTxt).gameObject.SetActive(false);
		}
	}
}

}
