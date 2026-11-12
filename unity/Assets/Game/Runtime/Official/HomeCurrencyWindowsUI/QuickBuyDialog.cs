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

public class QuickBuyDialog : UIBase
{
    public IHomeCurrencyWindowServices Services { private get; set; }
	public enum EBuyState
	{
		KeepCountLimit,
		BuyCountLimit,
		Available
	}

	[SerializeField]
	private MultiLanguageText mTitleTxt;

	[SerializeField]
	private RectTransform mIconSlot;

	[SerializeField]
	private MultiLanguageText mCurrentCountTxt;

	[SerializeField]
	private Image mItemImg;

	[SerializeField]
	private MultiLanguageText mBuyCountTxt;

	[SerializeField]
	private SetCountSlider mSetCountSlider;

	[SerializeField]
	private SetCountButtonGroup mSetCountButtonGroup;

	[SerializeField]
	private GameObject mLimitedBuyGo;

	[SerializeField]
	private MultiLanguageText mLimitedBuyTxt;

	[SerializeField]
	private PlayerCostMoneyContent mCostMoneyContent;

	[SerializeField]
	private Button mCancelBtn;

	[SerializeField]
	private TopBarHandler mTopBar;

	private int mItemId;

	private long mRequireItemCount;

	private long mCurrentValue;

	private long mMaxBuyCount;

	private long mMinBuyCount;

	private QuickBuyInfo mInfo;

	private ItemIcon mIcon;

	private long mMaxKeepItemCount;

	private List<PlayerItemType> mItemTypes = new List<PlayerItemType>();

	private bool mIsLimitByRes;

	private bool mShowMaxBtn;

	private HomeCurrencyDisposableBag mDisposableBag;

	private Action mOnBuyCancel;

	private void Awake()
	{
		PopupWindow.SetMaskClickAction(OnCancelBtnClick);
		mCancelBtn.onClick.AddListener(OnCancelBtnClick);
	}

	public void Init(int itemId, long requireItemCount = 0L, bool isLimitByRes = true, Action onBuyCancel = null, bool showMaxBtn = false)
	{
		InitWrapper(() =>
		{
			mItemId = itemId;
			mRequireItemCount = requireItemCount;
			mIsLimitByRes = isLimitByRes;
			mOnBuyCancel = onBuyCancel;
			mInfo = Services.GetQuickBuyInfo(itemId);
			mShowMaxBtn = showMaxBtn;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mDisposableBag = default;
		mSetCountSlider.OnCurrentCountUpdate += OnCurrentValueChange;
		IHomeCurrencyWindowPlayer mainPlayer = Services.Player;
		mainPlayer.OnItemCountUpdate += OnItemCountUpdate;
		mCostMoneyContent.SetItemEnoughHandler(() =>
		{
			OnConfirmBuy().Forget();
		});
		mCostMoneyContent.SetItemNotEnoughHandler(HandleCurrencyNotEnough);
		HomeCurrencyWindowItemInfo itemInfo = Services.GetItemInfo(mItemId);
		if (itemInfo.HasAutoIncrease)
		{
			mMaxKeepItemCount = Services.GetItemCountLimit(mItemId);
		}
		else
		{
			mMaxKeepItemCount = -1L;
		}
		if (mInfo.RefreshType != EQuickBuyRefreshType.None)
		{
			mDisposableBag.Add(Services.SubscribeEnterNewDay(OnEnterNewDay));
			mDisposableBag.Add(Services.SubscribeAutoIncrease(OnAutoIncreaseItemUpdate));
		}
		mTitleTxt.Format(new CultureTextArg(Services.GetItemNameKey(mItemId)));
		mIcon = Services.GetIcon(itemInfo, mIconSlot);
		mIcon.HideAllAttachment();
		mIcon.HideBottomText();
		mIcon.SetClickPopupInfoDialog();
		mIcon.SetCount(mInfo.GetAwardItemCount(mainPlayer.Level, mainPlayer.SubRank));
		mItemImg.sprite = mDisposableBag.Add(Services.LoadSprite(itemInfo.Icon)).Result;
		mCurrentValue = Math.DivRem(mRequireItemCount, mInfo.GetAwardItemCount(mainPlayer.Level, mainPlayer.SubRank), out var result);
		if (result != 0L)
		{
			mCurrentValue++;
		}
		RefreshCurrentCount();
		RefreshBuyInfo();
		RefreshBuyCount();
		RefreshLimitedCount();
		ResetItemTypes(mInfo.CurrencyId);
	}

	protected override void OnDisableAndInited()
	{
		if (mIcon != null && Services.IconServiceAvailable)
		{
			Services.RecycleIcon(mIcon);
		}
		mSetCountSlider.OnCurrentCountUpdate -= OnCurrentValueChange;
		if (Services.Player != null)
		{
			Services.Player.OnItemCountUpdate -= OnItemCountUpdate;
		}
		mDisposableBag.Dispose();
		mOnBuyCancel = null;
		base.OnDisableAndInited();
	}

	private void RefreshCurrentCount()
	{
		long itemAmount = Services.Player.GetItemAmount(mItemId);
		mCurrentCountTxt.Format(CultureNumberArg.Compact(itemAmount));
	}

	private void RefreshBuyInfo()
	{
		bool flag = mInfo.PriceList.Count > 1;
		mSetCountButtonGroup.gameObject.SetActive(flag);
		mSetCountSlider.gameObject.SetActive(!flag);
		if (GetState(0L) == EBuyState.Available)
		{
			IHomeCurrencyWindowPlayer mainPlayer = Services.Player;
			long alreadyBuy = mainPlayer.QuickBuyGetItemBuyCount(mItemId, mInfo.RefreshType);
			long itemAmount = mainPlayer.GetItemAmount(mInfo.CurrencyId);
			mMaxBuyCount = mInfo.GetMaxBuyCount(alreadyBuy, itemAmount, mIsLimitByRes);
			if (mMaxKeepItemCount != -1)
			{
				long itemAmount2 = mainPlayer.GetItemAmount(mItemId);
				mMaxBuyCount = Math.Min(mMaxBuyCount, mMaxKeepItemCount - itemAmount2);
			}
			mMinBuyCount = 1L;
			if (mMaxBuyCount < mMinBuyCount)
			{
				mMaxBuyCount = mMinBuyCount;
			}
		}
		else
		{
			mMinBuyCount = 0L;
			mMaxBuyCount = 0L;
		}
		if (mCurrentValue > mMaxBuyCount)
		{
			mCurrentValue = mMaxBuyCount;
		}
		if (mCurrentValue < mMinBuyCount)
		{
			mCurrentValue = mMinBuyCount;
		}
		if (flag)
		{
			mSetCountButtonGroup.Init(mMinBuyCount, mMaxBuyCount, OnCurrentValueChange, mCurrentValue, HandleOnExceedMaxCount, mShowMaxBtn);
		}
		else
		{
			mSetCountSlider.Init(mMinBuyCount, mMaxBuyCount, mCurrentValue, 1L);
		}
	}

	private void RefreshBuyCount()
	{
		IHomeCurrencyWindowPlayer mainPlayer = Services.Player;
		long value = mInfo.GetAwardItemCount(mainPlayer.Level, mainPlayer.SubRank) * mCurrentValue;
		mBuyCountTxt.Format(CultureNumberArg.Compact(value));
		long alreadyBuyCount = mainPlayer.QuickBuyGetItemBuyCount(mItemId, mInfo.RefreshType);
		mInfo.GetPrice(alreadyBuyCount, mCurrentValue, out var price);
		mCostMoneyContent.Init(mInfo.CurrencyId, price);
	}

	private void OnItemCountUpdate(PlayerItemDataWrap itemData, long preCount, AwardSourceData source)
	{
		if (itemData.Id == mItemId)
		{
			RefreshCurrentCount();
		}
		else if (itemData.Id == mInfo.CurrencyId)
		{
			RefreshBuyInfo();
		}
	}

	private void OnCurrentValueChange(long currentValue)
	{
		mCurrentValue = currentValue;
		RefreshBuyCount();
	}

	private void RefreshLimitedCount()
	{
		bool flag = mInfo.MaxBuyCount != -1;
		mLimitedBuyGo.SetActive(flag);
		if (flag)
		{
			string key = Services.QuickBuyLimitedCountTextKey(mInfo.RefreshType);
			long num = Services.Player.QuickBuyGetItemBuyCount(mItemId, mInfo.RefreshType);
			mLimitedBuyTxt.SetKey(key, num, mInfo.MaxBuyCount);
		}
	}

	private async UniTask OnConfirmBuy()
    {
        if (mCurrentValue == 0L)
        {
            EBuyState state = GetState(0L);
            NotifyForBuyLimited(state);
            return;
        }
        IHomeCurrencyWindowPlayer mainPlayer = Services.Player;
        long awardItemCount = mInfo.GetAwardItemCount(mainPlayer.Level, mainPlayer.SubRank);
        if (await Services.CheckBuyLimitCountItem(mItemId, awardItemCount))
        {
            Services.PlayerQuickBuy(mItemId, mCurrentValue);
            Services.Close(gameObject);
        }
    }

	private void HandleCurrencyNotEnough(long currenct, double price)
	{
		Services.ShowLackOfItem(mInfo.CurrencyId, (long)price - currenct, null);
	}

	private void OnAutoIncreaseItemUpdate(HomeCurrencyAutoIncreaseEvent data)
	{
		if (data.ItemId == mItemId)
		{
			RefreshBuyInfo();
			RefreshBuyCount();
		}
	}

	private void OnEnterNewDay(HomeCurrencyEnterNewDay _)
	{
		RefreshBuyInfo();
		RefreshBuyCount();
		RefreshLimitedCount();
	}

	private void OnCancelBtnClick()
	{
		mOnBuyCancel?.Invoke();
		Services.Close(gameObject);
	}

	private EBuyState GetState(long buyCount = 0L)
	{
		IHomeCurrencyWindowPlayer mainPlayer = Services.Player;
		if (mMaxKeepItemCount != -1)
		{
			long num = buyCount * mInfo.GetAwardItemCount(mainPlayer.Level, mainPlayer.SubRank);
			if (mainPlayer.GetItemAmount(mItemId) + num >= mMaxKeepItemCount)
			{
				return EBuyState.KeepCountLimit;
			}
		}
		if (mInfo.MaxBuyCount != -1 && mainPlayer.QuickBuyGetItemBuyCount(mItemId, mInfo.RefreshType) + buyCount >= mInfo.MaxBuyCount)
		{
			return EBuyState.BuyCountLimit;
		}
		return EBuyState.Available;
	}

	private void HandleOnExceedMaxCount(long count)
	{
		EBuyState state = GetState(count);
		if (state != EBuyState.Available)
		{
			NotifyForBuyLimited(state);
			return;
		}
		string textFormated = LanguageManager.Instance.GetTextFormated(Services.GetItemNameKey(mInfo.CurrencyId));
		Services.SystemMessage(LanguageManager.Instance.GetTextFormated("ui_quick_buy_currency_not_enough", textFormated));
	}

	private void NotifyForBuyLimited(EBuyState state)
	{
		switch (state)
		{
		case EBuyState.KeepCountLimit:
			Services.SystemMessage(LanguageManager.Instance.GetText($"ui_quick_buy_limit_{state}"));
			break;
		case EBuyState.BuyCountLimit:
			Services.SystemMessage(LanguageManager.Instance.GetText($"ui_quick_buy_limit_{state}_{mInfo.RefreshType}"));
			break;
		}
	}

	private void ResetItemTypes(int currencyId)
	{
		mItemTypes.Clear();
		if (currencyId == Services.MoneyId)
		{
			mItemTypes.Add(PlayerItemType.Money);
		}
		else if (currencyId == Services.DiamonId)
		{
			mItemTypes.Add(PlayerItemType.Diamon);
		}
		else
		{
			Debug.LogError("QuickBuyDialog " + currencyId + " unknown item types");
		}
		mTopBar.UpdateCurrencyData(mItemTypes);
	}
}

}
