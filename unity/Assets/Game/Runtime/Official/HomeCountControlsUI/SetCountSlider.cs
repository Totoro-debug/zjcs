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

public class SetCountSlider : MonoBehaviour
{
	public Button MaxBtn;

	public Button AddBtn;

	public Button MinusBtn;

	public Slider Slider;

	public WorldMultiLanguageText CurrentCountText;

	private bool mInited;

	private bool mCanSelect;

	private bool mCanOperate;

	private long mMinValue;

	private long mMaxValue;

	private long mCurrentCount = -1L;

	private long mAmountPerTrade = 1L;

	public LongTouch AddLongTouch;

	public LongTouch MinusLongTouch;

	private string mZeroLightRed;

	public IHomeCountColors GlobalColors { get; set; }

	public long CurrentCount => mCurrentCount;

	public event Action<long> OnCurrentCountUpdate;

	protected virtual void Awake()
	{






		((UnityEvent)AddBtn.onClick).AddListener((UnityAction)OnAddBtnClick);
		((UnityEvent)MinusBtn.onClick).AddListener((UnityAction)OnMinusBtnClick);
		((UnityEvent<float>)(object)Slider.onValueChanged).AddListener((UnityAction<float>)OnSliderValueChanged);
		if ((Object)(object)MaxBtn != (Object)null)
		{
			((UnityEvent)MaxBtn.onClick).AddListener((UnityAction)OnMaxBtnClick);
		}
		if ((Object)(object)AddLongTouch != (Object)null)
		{
			AddLongTouch.OnLongTouch += AddLongTouch_OnLongTouch;
		}
		if ((Object)(object)MinusLongTouch != (Object)null)
		{
			MinusLongTouch.OnLongTouch += MinusLongTouch_OnLongTouch;
		}
		Slider.wholeNumbers = true;
	}

	private void OnDestroy()
	{
		if ((Object)(object)AddLongTouch != (Object)null)
		{
			AddLongTouch.OnLongTouch -= AddLongTouch_OnLongTouch;
		}
		if ((Object)(object)MinusLongTouch != (Object)null)
		{
			MinusLongTouch.OnLongTouch -= MinusLongTouch_OnLongTouch;
		}
	}

	private void AddLongTouch_OnLongTouch(long changeValue)
	{
		OnAddBtnClick(changeValue);
	}

	private void MinusLongTouch_OnLongTouch(long changeValue)
	{
		OnMinusBtnClick(changeValue);
	}

	public virtual void Init(long minValue, long maxValue, long currentValue, long amountPerTrade = 1L)
	{
		mInited = false;
		mMinValue = minValue;
		mMaxValue = maxValue;
		mAmountPerTrade = ((amountPerTrade > 0) ? amountPerTrade : 1);
		mCanSelect = mMinValue <= mMaxValue;
		mCanOperate = mMinValue < mMaxValue;
		if (!mCanSelect)
		{
			mCurrentCount = 0L;
		}
		else
		{
			mCurrentCount = GetCurrentCount(currentValue, minValue);
		}
		((Selectable)Slider).interactable = mCanOperate;
		if (!mCanOperate)
		{
			Slider.minValue = 0f;
			Slider.maxValue = 1f;
			Slider.value = (mCanSelect ? 1 : 0);
		}
		else
		{
			Slider.minValue = minValue;
			Slider.maxValue = maxValue;
			Slider.value = mCurrentCount;
		}
		mInited = true;
		OnSliderValueChanged(mCurrentCount);
	}

	private void OnSliderValueChanged(float arg0)
	{
		if (mInited)
		{
			long value = (long)arg0;
			OnSliderValueChangedHandle(ref value);
			mCurrentCount = value;
			RefreshCountTxt();
			OnCurrentCountUpdate?.Invoke(mCurrentCount);
		}
	}

	protected virtual long GetCurrentCount(long currentValue, long minValue)
	{
		return MathUtils.Clamp(currentValue, minValue, mMaxValue);
	}

	protected virtual void OnSliderValueChangedHandle(ref long value)
	{
	}

	private void OnMaxBtnClick()
	{
		if (mCanOperate)
		{
			Slider.value = Slider.maxValue;
		}
	}

	private void OnMinusBtnClick()
	{
		OnMinusBtnClick(1L);
	}

	private void OnMinusBtnClick(long minusValue)
	{
		if (mCanOperate)
		{
			Slider slider = Slider;
			slider.value -= (float)minusValue;
		}
	}

	private void OnAddBtnClick()
	{
		OnAddBtnClick(1L);
	}

	private void OnAddBtnClick(long addValue)
	{
		if (mCanOperate)
		{
			Slider slider = Slider;
			slider.value += (float)addValue;
		}
	}

	private void RefreshCountTxt()
	{


		if ((Object)(object)CurrentCountText == (Object)null)
		{
			return;
		}
		string text = null;
		if (mCanSelect)
		{
			text = LanguageManager.Instance.GetTextFormated("ui_common_contrast_1", CultureNumberArg.Compact(mCurrentCount * mAmountPerTrade), CultureNumberArg.Compact(mMaxValue * mAmountPerTrade));
		}
		else
		{
			if (mZeroLightRed == null)
			{
				mZeroLightRed = GlobalColors.WrapColor("0", GlobalColors.LightRed);
			}
			text = mZeroLightRed;
		}
		CurrentCountText.Format(text);
	}
}

}
