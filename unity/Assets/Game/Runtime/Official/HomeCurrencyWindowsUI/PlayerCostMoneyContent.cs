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

public sealed class PlayerCostMoneyContent : UIBase
{
    public IHomeCurrencyWindowServices Services { private get; set; }
	public delegate void ItemNotEnoughDelegate(long currentCount, double price);

	public GameObject MoneyContent;

	public Image CostItemIcon;

	public MultiLanguageText CostText;

	public MultiLanguageText AmountText;

	public Button Btn;

	public Button ImageBtn;

	public MultiLanguageText BtnText;

	public Color EnoughColor;

	public Color LackColor;

	[SerializeField]
	private string mCountFormatKey = "common_format_args1";

	public bool HideMoneyContentWhenEmpty = true;

	[SerializeField]
	private StateSwitcher mIsEnoughSwitcher;

	private Action mItemEnoughHandler;

	private Action<MultiLanguageText> mCostTextHandler;

	private Func<bool, Color> mCustomColorHandler;

	private Func<bool> mConditionChecker;

	private HomeCurrencyDisposableBag mDisposable;

	private ItemNotEnoughDelegate mItemNotEnoughHandler;

	private Coroutine mCoroutine;

	public int CostItemId { get; set; }

	public long CostItemCount { get; set; }

	private void Awake()
	{










		if ((Object)(object)Btn != (Object)null)
		{
			((UnityEvent)Btn.onClick).AddListener((UnityAction)OnBtnClick);
		}
		if ((Object)(object)ImageBtn != (Object)null)
		{
			((UnityEvent)ImageBtn.onClick).AddListener((UnityAction)OnImageBtnClick);
		}
		EnoughColor = ((EnoughColor.a <= 0f) ? Services.White : EnoughColor);
		LackColor = ((LackColor.a <= 0f) ? Services.LightRed : LackColor);
	}

	protected override void OnEnableAndInited()
	{

		base.OnEnableAndInited();
		mDisposable = default;
		if (CostItemId > 0 && CostItemCount > 0)
		{
			Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		}
		Refresh();
		RefreshCostItemIcon();
		TryAutoRefreshCountColor();
	}

	protected override void OnDisableAndInited()
	{
		if (Services.Player != null && CostItemId > 0 && CostItemCount > 0)
		{
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		mDisposable.Dispose();
		ResetCoroutine();
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnItemCountUpdate(PlayerItemDataWrap itemData, long preCount, AwardSourceData sourceData)
	{
		if (itemData.Id == CostItemId)
		{
			Refresh();
		}
	}

	private void HandleItemEnough()
	{
		mItemEnoughHandler?.Invoke();
	}

	private void HandleItemNotEnough(long currentCount, double price)
	{
		if (mItemNotEnoughHandler != null)
		{
			mItemNotEnoughHandler(currentCount, price);
		}
		else
		{
			BroadcastItemNotEnough((long)price - currentCount);
		}
	}

	public void BroadcastItemNotEnough(long lackCount = 0L)
	{
		Services.ShowLackOfItem(CostItemId, lackCount, this);
	}

	private void OnBtnClick()
	{
		if (Services.Player == null || (mConditionChecker != null && !mConditionChecker()))
		{
			return;
		}
		if (CostItemCount != 0L)
		{
			long itemAmount = Services.Player.GetItemAmount(CostItemId);
			if (itemAmount < CostItemCount)
			{
				HandleItemNotEnough(itemAmount, CostItemCount);
				return;
			}
			if (Services.Player.IsBanCostItem(CostItemId))
			{
				HomeCurrencyWindowItemInfo itemInfo = Services.GetItemInfo(CostItemId);
				Services.ShowError(1659, itemInfo.Name);
				return;
			}
		}
		HandleItemEnough();
	}

	private void OnImageBtnClick()
    {
        Services.PopupItemDetail(this, CostItemId, 0L);
    }

	public PlayerCostMoneyContent Init(int moneyId, long count)
	{
		InitWrapper(() =>
		{
			CostItemId = moneyId;
			CostItemCount = count;
		});
		return this;
	}

	public PlayerCostMoneyContent SetItemEnoughHandler(Action handler)
	{
		mItemEnoughHandler = handler;
		return this;
	}

	public PlayerCostMoneyContent SetItemNotEnoughHandler(ItemNotEnoughDelegate handler)
	{
		mItemNotEnoughHandler = handler;
		return this;
	}

	public PlayerCostMoneyContent SetCostItemCountColorHandler(Func<bool, Color> handler)
	{
		mCustomColorHandler = handler;
		return this;
	}

	public PlayerCostMoneyContent SetConditionChecker(Func<bool> checker)
	{
		mConditionChecker = checker;
		return this;
	}

	private void RefreshCostItemIcon()
	{
		if ((Object)(object)CostItemIcon != (Object)null && CostItemId > 0)
		{
			HomeCurrencyWindowItemInfo itemInfo = Services.GetItemInfo(CostItemId);
			CostItemIcon.sprite = mDisposable.Add(Services.LoadSprite(itemInfo.Icon)).Result;
		}
	}

	private void Refresh()
	{
		if ((Object)(object)MoneyContent != (Object)null)
		{
			if (!ShowMoneyContent())
			{
				MoneyContent.SetActive(false);
				return;
			}
			MoneyContent.SetActive(true);
		}
		RefreshCostText();
		RefreshAmountText();
	}

	private void RefreshCostText()
	{

		if (!((Object)(object)CostText == (Object)null))
		{
			if (mCostTextHandler != null)
			{
				mCostTextHandler?.Invoke(CostText);
				return;
			}
			CostText.SetKey((CostItemCount == 0L) ? "ui_common_free" : mCountFormatKey, CultureNumberArg.Compact(CostItemCount));
			CostText.Color = GetColorByWhetherItemEnough();
		}
	}

	private void RefreshAmountText()
	{

		long itemAmount = Services.Player.GetItemAmount(CostItemId);
		bool state = itemAmount >= CostItemCount;
		mIsEnoughSwitcher.SetState(state);
		if ((Object)(object)AmountText != (Object)null)
		{
			AmountText.SetKey((CostItemCount == 0L) ? "ui_common_free" : mCountFormatKey, CultureNumberArg.Compact(itemAmount));
			AmountText.Color = GetColorByWhetherItemEnough();
		}
	}

	private bool ShowMoneyContent()
	{
		if (CostItemId <= 0)
		{
			return false;
		}
		if (CostItemCount <= 0 && HideMoneyContentWhenEmpty)
		{
			return false;
		}
		return true;
	}

	private void TryAutoRefreshCountColor()
	{
		HomeCurrencyWindowItemInfo itemInfo = Services.GetItemInfo(CostItemId);
		if (itemInfo != null && itemInfo.HasAutoIncrease)
		{
			ResetCoroutine();
			mCoroutine = ((MonoBehaviour)this).StartCoroutine(AutoRefreshCountColor());
		}
	}

	private IEnumerator AutoRefreshCountColor()
	{
		WaitForSeconds waitForSeconds = new WaitForSeconds(1f);
		while (true)
		{
			if (Services.Player != null)
			{
				CostText.Color = GetColorByWhetherItemEnough();
			}
			yield return waitForSeconds;
		}
	}

	private void ResetCoroutine()
	{
		if (mCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(mCoroutine);
			mCoroutine = null;
		}
	}

	public void SetBtnText(string btnWord, params object[] args)
	{
		if ((Object)(object)BtnText != (Object)null)
		{
			BtnText.SetKey(btnWord, args);
		}
	}

	public void ResetContent()
	{
		CostItemId = 0;
		CostItemCount = 0L;
		if ((Object)(object)MoneyContent != (Object)null)
		{
			MoneyContent.SetActive(false);
		}
	}

	public void SetCostTextHandler(Action<MultiLanguageText> handler)
	{
		mCostTextHandler = handler;
	}

	public Color GetColorByWhetherItemEnough()
	{


		bool flag = Services.Player.HasItem(CostItemId, CostItemCount);
		if (mCustomColorHandler != null)
		{
			return mCustomColorHandler(flag);
		}
		return GetDefaultColorByIsEnough(flag);
	}

	public Color GetDefaultColorByIsEnough(bool isEnough)
	{


		if (isEnough)
		{
			return EnoughColor;
		}
		return LackColor;
	}

	public void RefreshCostTextColor()
	{

		if (!((Object)(object)CostText == (Object)null))
		{
			CostText.Color = GetColorByWhetherItemEnough();
		}
	}
}

}
