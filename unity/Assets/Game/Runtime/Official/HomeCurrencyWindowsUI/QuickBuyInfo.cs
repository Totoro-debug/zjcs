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
public sealed class QuickBuyInfo
{
    public long MaxBuyCount;
    public int ItemId, CurrencyId, SubRankScaleId, LevelScaleId;
    public long BaseCount;
    public EQuickBuyRefreshType RefreshType;
    public List<long> PriceList, PriceBuyCount;
    public Func<int,int,double> GetNumberDouble;
	public bool GetPrice(long alreadyBuyCount, long willBuyCount, out long price)
	{
		price = 0L;
		if (MaxBuyCount != -1 && alreadyBuyCount + willBuyCount > MaxBuyCount)
		{
			return false;
		}
		long num = willBuyCount;
		for (int i = 0; i < PriceList.Count; i++)
		{
			long num2 = PriceList[i];
			long num3 = PriceBuyCount[i];
			if (num3 < 0)
			{
				price += num2 * num;
				break;
			}
			if (alreadyBuyCount >= num3)
			{
				alreadyBuyCount -= num3;
				continue;
			}
			long num4 = num3;
			if (alreadyBuyCount > 0)
			{
				num4 -= alreadyBuyCount;
				alreadyBuyCount = 0L;
			}
			if (num > num4)
			{
				price += num2 * num4;
				num -= num4;
				continue;
			}
			price += num2 * num;
			break;
		}
		return true;
	}

	public long GetMaxBuyCount(long alreadyBuy, long currencyCount, bool isLimitByCost = true)
	{
		if (isLimitByCost)
		{
			if (PriceList.Count == 1)
			{
				long num = currencyCount / PriceList[0];
				if (MaxBuyCount == -1)
				{
					return num;
				}
				return Math.Min(num, MaxBuyCount);
			}
			long num2 = 0L;
			for (int i = 0; i < PriceList.Count; i++)
			{
				long num3 = PriceBuyCount[i];
				if (num3 != -1 && alreadyBuy >= num3)
				{
					alreadyBuy -= num3;
					continue;
				}
				long num4 = PriceList[i];
				long num5 = currencyCount / num4;
				if (num3 == -1)
				{
					num2 += num5;
					break;
				}
				long val = num3 - alreadyBuy;
				alreadyBuy = 0L;
				long num6 = Math.Min(val, num5);
				num2 += num6;
				currencyCount -= num6 * num4;
			}
			return num2;
		}
		if (PriceList.Count == 1)
		{
			if (MaxBuyCount == -1)
			{
				return long.MaxValue;
			}
			return MaxBuyCount;
		}
		long num7 = 0L;
		for (int j = 0; j < PriceList.Count; j++)
		{
			long num8 = PriceBuyCount[j];
			if (num8 == -1)
			{
				return long.MaxValue;
			}
			num7 += num8;
		}
		return num7 - alreadyBuy;
	}

	public long GetAwardItemCount(int level, int subRank)
	{
		double num = BaseCount;
		if (SubRankScaleId > 0)
		{
			double numberDouble = GetNumberDouble(SubRankScaleId, (int)subRank);
			if (numberDouble > 0.0)
			{
				num *= numberDouble;
			}
		}
		if (LevelScaleId > 0)
		{
			double numberDouble2 = GetNumberDouble(LevelScaleId, level);
			if (numberDouble2 > 0.0)
			{
				num *= numberDouble2;
			}
		}
		return (long)num;
	}

}

}
