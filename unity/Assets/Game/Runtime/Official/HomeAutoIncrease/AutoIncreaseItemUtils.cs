// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
public static class AutoIncreaseItemUtils
{
	public static double CalcAutoIncreaseCount(double increaseSpeed, double hour)
	{
		return increaseSpeed * hour;
	}

	public static double GetActualCountWithAutoIncreaseSegmented(ItemParamAutoIncrease itemParam, long count, List<SegmentedIncreaseData> segmentedList)
	{
		if (itemParam == null || segmentedList == null)
		{
			return count;
		}
		double num = itemParam.Decimal;
		int i = 0;
		for (int count2 = segmentedList.Count; i < count2; i++)
		{
			SegmentedIncreaseData segmentedIncreaseData = segmentedList[i];
			double num2 = (segmentedIncreaseData.EndTime - segmentedIncreaseData.StartTime).TotalHours;
			if (i == 0 && itemParam.BonusSeconds > 0.0)
			{
				num2 += TimeUtils.SecondToHour(itemParam.BonusSeconds);
			}
			num2 = Math.Max(0.0, num2);
			(long count, double decimalValue) tuple = CalcActualCountWithAutoIncrease(count, num, segmentedIncreaseData.IncreaseSpeed, segmentedIncreaseData.MaxCount, num2);
			count = tuple.count;
			num = tuple.decimalValue;
		}
		return (double)count + num;
	}

	public static (long count, double decimalValue) CalcActualCountWithAutoIncrease(long count, double decimalValue, double spd, long max, double hour)
	{
		if (spd > 0.0 && count >= max)
		{
			return (count: count, decimalValue: decimalValue);
		}
		double num = CalcAutoIncreaseCount(spd, hour) + decimalValue;
		long num2 = (long)Math.Floor(num);
		decimalValue = num - (double)num2;
		return (count: MathUtils.Clamp(count + num2, long.MinValue, max), decimalValue: decimalValue);
	}
}
}
