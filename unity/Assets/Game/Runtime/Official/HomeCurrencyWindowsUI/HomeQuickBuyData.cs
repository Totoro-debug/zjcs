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
public sealed class HomeQuickBuyData
{
    public readonly Dictionary<EQuickBuyRefreshType,HomeQuickBuyCounter> BuyDatas = new Dictionary<EQuickBuyRefreshType,HomeQuickBuyCounter>();
    readonly HomeQuickBuyCalendar calendar;
    public event Action<int> OnCountUpdate;
    public HomeQuickBuyData(HomeQuickBuyCalendar calendar) { this.calendar=calendar; }
	public long QuickBuyGetItemBuyCount(int itemId, EQuickBuyRefreshType type, DateTime now)
	{
		if (!BuyDatas.TryGetValue(type, out var value))
		{
			return 0L;
		}
		return value.GetBuyCount(itemId, now);
	}

	public void QuickBuyAddItemBuyCount(EQuickBuyRefreshType type, int itemId, long buyCount, DateTime time)
	{
		if (!BuyDatas.TryGetValue(type, out var value))
		{
			value = CreateCounter(type);
			if (value == null)
			{
				return;
			}
			BuyDatas.Add(type, value);
		}
		value.AddBuyCount(itemId, buyCount, time);
		OnCountUpdate?.Invoke(itemId);
	}

	private HomeQuickBuyCounter CreateCounter(EQuickBuyRefreshType type)
	{
		return type switch
		{
			EQuickBuyRefreshType.None => (HomeQuickBuyCounter)new HomeQuickBuyCounterNonRefresh(),
			EQuickBuyRefreshType.Daily => new HomeQuickBuyCounterDaily(calendar),
			EQuickBuyRefreshType.Weekly => new HomeQuickBuyCounterWeekly(calendar),
			_ => null,
		};
	}

}

}
