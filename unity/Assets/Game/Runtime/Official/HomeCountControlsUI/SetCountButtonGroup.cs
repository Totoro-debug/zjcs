// Original quantity control; language/color/input services explicitly bound.
namespace Zjcs.Official.HomeCountControlsUI
{
using Object=UnityEngine.Object;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SetCountButtonGroup : MonoBehaviour
{
	[SerializeField]
	private Button mMinusBtn;

	[SerializeField]
	private Button mAddBtn;

	[SerializeField]
	private Button mMaxBtn;

	[SerializeField]
	private WorldMultiLanguageText mCurrentValueTxt;

	private long mMinValue;

	private long mMaxValue;

	private long mCurrentValue;

	private Action<long> mValueChangeCb;

	private Action<long> mAddOnMaxCb;

	private void Awake()
	{






		((UnityEvent)mMinusBtn.onClick).AddListener((UnityAction)OnMinusBtnClick);
		((UnityEvent)mAddBtn.onClick).AddListener((UnityAction)OnAddBtnClick);
		((UnityEvent)mMaxBtn.onClick).AddListener((UnityAction)OnMaxBtnClick);
	}

	public void Init(long minValue, long maxValue, Action<long> valueChangeCb, long initValue = 0L, Action<long> addOnMaxCb = null, bool showMaxBtn = false)
	{
		mMinValue = minValue;
		mMaxValue = maxValue;
		mCurrentValue = initValue;
		mValueChangeCb = valueChangeCb;
		mAddOnMaxCb = addOnMaxCb;
		RefreshText();
		((Component)mMaxBtn).gameObject.SetActive(showMaxBtn);
	}

	private void OnAddBtnClick()
	{
		long num = mCurrentValue + 1;
		if (num > mMaxValue)
		{
			mAddOnMaxCb?.Invoke(num);
			return;
		}
		mCurrentValue = num;
		mValueChangeCb?.Invoke(mCurrentValue);
		RefreshText();
	}

	private void OnMinusBtnClick()
	{
		long num = mCurrentValue - 1;
		if (num >= mMinValue)
		{
			mCurrentValue = num;
			mValueChangeCb?.Invoke(mCurrentValue);
			RefreshText();
		}
	}

	private void OnMaxBtnClick()
	{
		if (mCurrentValue != mMaxValue)
		{
			mCurrentValue = mMaxValue;
			mValueChangeCb?.Invoke(mCurrentValue);
			RefreshText();
		}
	}

	private void RefreshText()
	{
		mCurrentValueTxt.Format(CultureNumberArg.Compact(mCurrentValue));
	}
}

}
