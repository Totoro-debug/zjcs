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
public interface IHomePeriodicServices
{
    bool HasPlayer { get; }
    bool TimeSynced { get; }
    DateTime GetDateTime();
    HomeQuickBuyCalendar Calendar { get; }
    void PublishEnterNewDay(HomeCurrencyEnterNewDay message);
}
public sealed class HomePeriodicManager
{
    private const float kDateCheckInterval=1f;
    private DateTime mLastDate;
    private bool mInited;
    private float mDateCheckTick;
    private readonly IHomePeriodicServices Services;
    public HomePeriodicManager(IHomePeriodicServices services)
    { Services=services??throw new ArgumentNullException(nameof(services)); }
	public void Update(float time)
	{
		if (mDateCheckTick > time)
		{
			return;
		}
		mDateCheckTick = time + 1f;
		if (!Services.HasPlayer || !Services.TimeSynced)
		{
			mInited = false;
			return;
		}
		DateTime dateTime = Services.GetDateTime();
		if (!mInited)
		{
			mLastDate = Services.GetDateTime();
			mInited = true;
		}
		else if (!(dateTime <= mLastDate))
		{
			bool flag = IsNewWeek(mLastDate, dateTime);
			if (flag)
			{
				EnterNewWeek();
			}
			if (IsNewDay(mLastDate, dateTime))
			{
				EnterNewDay(flag);
			}
			mLastDate = dateTime;
		}
	}

	private bool IsNewWeek(DateTime from, DateTime to)
	{
		return !Services.Calendar.IsSameWeek(from, to);
	}

	private bool IsNewDay(DateTime from, DateTime to)
	{
		return Services.Calendar.CalcDays(from, to) != 0;
	}

    private void EnterNewWeek()
    {
        // The original friend-coin server synchronization is outside offline scope.
    }
    private void EnterNewDay(bool isNewWeek)
    {
        Services.PublishEnterNewDay(null);
        // Original logging and friend-coin synchronization are omitted.
    }
}

}
