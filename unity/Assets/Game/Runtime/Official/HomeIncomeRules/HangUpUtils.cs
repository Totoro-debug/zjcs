// Selected official Console.dll; parser/player boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeIncomeRules
{
public static class HangUpUtils
{
	public static double CalcRewardCount(HangUpInfo hangUpInfo, HangUpRewardInfo hangUpRewardInfo, BattleFormulaHandler formula, IReadOnlyDictionary<int, long> props, double hangUpHour, double scaledNum)
	{
		formula.GetHangUpProp(hangUpInfo, props, out var addPercent, out var addFixed);
		double num = hangUpHour * scaledNum * addPercent;
		double num2 = (double)addFixed / scaledNum / addPercent;
		return ((double)hangUpRewardInfo.HourlyOutput + num2) * num;
	}

	// Original GetUnsettledRewardCount arithmetic after unlock and row lookup.
	public static long CalcUnsettledRewardCount(HangUpInfo hangUpInfo, HangUpRewardInfo hangUpRewardInfo, BattleFormulaHandler formula, IReadOnlyDictionary<int, long> props, double levelNum, double elapsedSeconds, double lastNotSettleSeconds, double extraRewardSeconds, out double remainderSeconds)
	{
		double num2 = elapsedSeconds + lastNotSettleSeconds + extraRewardSeconds;
		double scaledNum = num2 / 3600.0;
		long num = 0L;
		double num3 = CalcRewardCount(hangUpInfo, hangUpRewardInfo, formula, props, levelNum, scaledNum);
			num = (long)num3;
			double num4 = num3 - (double)num;
			remainderSeconds = num4 * num2 / num3;
		return num;
	}
	public static long ClampAwardCount(long settledRewardCount, long unsettledRewardCount, long hangUpMaxRewardStore)
	{
		long val = settledRewardCount + unsettledRewardCount;
		return Math.Max(settledRewardCount, Math.Min(val, hangUpMaxRewardStore));
	}
}
}
