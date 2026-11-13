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

public class SetUseCountSlider : SetCountSlider
{
    public IHomeFastHangUpServices Services { protected get; set; }
	private long mSingleMaxUseCount;

	private string mTipKey;

	private DateTime mLastShowTipTime = DateTime.MinValue;

	public void Init(long minValue, long maxValue, long currentValue, long singleMaxUseCount = -1L, string tipKey = "")
	{
		mSingleMaxUseCount = ((singleMaxUseCount <= -1) ? maxValue : singleMaxUseCount);
		mTipKey = tipKey;
		base.Init(minValue, maxValue, currentValue, 1L);
	}

	protected override void OnSliderValueChangedHandle(ref long value)
	{
		base.OnSliderValueChangedHandle(ref value);
		if (value > mSingleMaxUseCount)
		{
			if (!string.IsNullOrEmpty(mTipKey))
			{
				TryShowMsg();
			}
			value = mSingleMaxUseCount;
			Slider.value = value;
		}
	}

	protected override long GetCurrentCount(long currentValue, long minValue)
	{
		return HomeFastHangUpMath.Clamp(currentValue, minValue, mSingleMaxUseCount);
	}

	private void TryShowMsg()
	{
		if (!((DateTime.Now - mLastShowTipTime).TotalSeconds < (double)Services.FastItemMaxCountMessageColdDown))
		{
			string textFormated = Services.Window.TextFormatted(mTipKey, mSingleMaxUseCount);
			Services.Window.SystemMessage(textFormated);
			mLastShowTipTime = DateTime.Now;
		}
	}
}

}
