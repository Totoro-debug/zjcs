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

}
}
