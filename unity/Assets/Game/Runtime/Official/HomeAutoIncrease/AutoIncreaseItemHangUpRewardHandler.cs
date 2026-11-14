// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
public static class AutoIncreaseItemHangUpRewardHandler
{
public static void SettleHangUpReward(HomeAutoIncreaseItem itemById, double hangUpHour, bool hasOutputAddition, int outputAdditionPropValue, Action<double> recordRemainder)
	{
		ItemParamAutoIncrease playerItemParamAutoIncreaseWrap = itemById?.Parameter;
		if (playerItemParamAutoIncreaseWrap == null)
		{
			return;
		}
		if (!hasOutputAddition)
		{
			hangUpHour = AutoIncreaseItemPropUtils.CalcOffsetOutputAdditionTimeDuration(outputAdditionPropValue, hangUpHour);
		}
		long actualCountWithoutAutoIncrease = itemById.StoredCount;
		double num = playerItemParamAutoIncreaseWrap.Decimal;
		double sec = itemById.Clock.CalcElapsedSecond(playerItemParamAutoIncreaseWrap.LastUpdateTime) + playerItemParamAutoIncreaseWrap.BonusSeconds;
		double num2 = playerItemParamAutoIncreaseWrap.IncreaseSpeed * (hangUpHour + TimeUtils.SecondToHour(sec)) + (double)actualCountWithoutAutoIncrease + num;
		long maxCount = playerItemParamAutoIncreaseWrap.MaxCount;
		double num3 = TimeUtils.HourToSecond(hangUpHour);
		if (num2 < (double)maxCount)
		{
			long num4 = (long)num2;
			if (num4 <= 0)
			{
				playerItemParamAutoIncreaseWrap.BonusSeconds += num3;
				return;
			}
			itemById.SetCount(num4);
			playerItemParamAutoIncreaseWrap.Decimal = 0.0;
			double num5 = TimeUtils.HourToSecond((num2 - (double)num4) / playerItemParamAutoIncreaseWrap.IncreaseSpeed);
			playerItemParamAutoIncreaseWrap.BonusSeconds += ((num5 > 0.0) ? num5 : 0.0);
			return;
		}
		num3 += playerItemParamAutoIncreaseWrap.BonusSeconds;
		playerItemParamAutoIncreaseWrap.BonusSeconds = 0.0;
		if (actualCountWithoutAutoIncrease < maxCount)
		{
			double num6 = (double)actualCountWithoutAutoIncrease + num;
			double num7 = TimeUtils.HourToSecond(((double)maxCount - num6) / playerItemParamAutoIncreaseWrap.IncreaseSpeed) - itemById.Clock.CalcElapsedSecond(playerItemParamAutoIncreaseWrap.LastUpdateTime);
			num3 -= num7;
			itemById.SetCount(maxCount);
			playerItemParamAutoIncreaseWrap.Decimal = 0.0;
		}
		if (!(num3 <= 0.0))
		{
			double num8 = TimeUtils.SecondToHour(num3) * playerItemParamAutoIncreaseWrap.IncreaseSpeed;
			long num9 = (long)num8;
			itemById.SetCount(itemById.StoredCount + num9);
			double num10 = TimeUtils.HourToSecond((num8 - (double)num9) / playerItemParamAutoIncreaseWrap.IncreaseSpeed);
			if (num10 > 0.0)
			{
				recordRemainder(num10);
			}
		}
	}
}

}
