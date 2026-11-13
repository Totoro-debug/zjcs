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
public abstract class HomeQuickBuyCounter
{
    public readonly Dictionary<int,long> BuyCount = new Dictionary<int,long>();
    public abstract long GetBuyCount(int itemId,DateTime cmpTime);
    public abstract void AddBuyCount(int itemId,long count,DateTime buyTime);
}
public abstract class HomeQuickBuyCounterPeriodic : HomeQuickBuyCounter
{
    public DateTime LastUpdateTime;
    protected readonly IHomeQuickBuyCalendar Calendar;
    protected HomeQuickBuyCounterPeriodic(IHomeQuickBuyCalendar calendar) { Calendar=calendar; }
    public abstract bool InPeriodic(DateTime cmpTime);
	public override void AddBuyCount(int itemId, long count, DateTime buyTime)
	{
		if (!InPeriodic(buyTime))
		{
			BuyCount.Clear();
		}
		LastUpdateTime = buyTime;
		BuyCount.TryGetValue(itemId, out var value);
		BuyCount[itemId] = value + count;
	}

	public override long GetBuyCount(int itemId, DateTime cmpTime)
	{
		if (!InPeriodic(cmpTime))
		{
			return 0L;
		}
		BuyCount.TryGetValue(itemId, out var value);
		return value;
	}

}
public sealed class HomeQuickBuyCounterNonRefresh : HomeQuickBuyCounter
{
	public override long GetBuyCount(int itemId, DateTime cmpTime)
	{
		BuyCount.TryGetValue(itemId, out var value);
		return value;
	}

	public override void AddBuyCount(int itemId, long count, DateTime buyTime)
	{
		if (!BuyCount.TryGetValue(itemId, out var value))
		{
			BuyCount.Add(itemId, count);
		}
		else
		{
			BuyCount[itemId] = value + count;
		}
	}

}
public sealed class HomeQuickBuyCounterDaily : HomeQuickBuyCounterPeriodic
{
public HomeQuickBuyCounterDaily(IHomeQuickBuyCalendar calendar) : base(calendar) {}
	public override bool InPeriodic(DateTime cmpTime)
	{
		return Calendar.IsSameDay(LastUpdateTime, cmpTime);
	}

}
public sealed class HomeQuickBuyCounterWeekly : HomeQuickBuyCounterPeriodic
{
public HomeQuickBuyCounterWeekly(IHomeQuickBuyCalendar calendar) : base(calendar) {}
	public override bool InPeriodic(DateTime cmpTime)
	{
		return Calendar.IsSameWeek(LastUpdateTime, cmpTime);
	}

}

}
