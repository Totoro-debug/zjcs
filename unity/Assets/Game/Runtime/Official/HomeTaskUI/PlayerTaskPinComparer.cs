// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using System;
using System.Collections.Generic;
internal static class PlayerTaskPinComparer
{
    private enum ComparerType { Duration, AutoTrace, Completed, Order }
    public static int PinComparer(IHomeTaskPlayer player, PlayerTask x, PlayerTask y) => Comparer(player,x,y,new List<(ComparerType,ComparerOrderType)> {
        (ComparerType.Duration,ComparerOrderType.Asc),(ComparerType.AutoTrace,ComparerOrderType.Desc),
        (ComparerType.Completed,ComparerOrderType.Desc),(ComparerType.Order,ComparerOrderType.Asc) });
	private static int Comparer(IHomeTaskPlayer player, PlayerTask x, PlayerTask y, List<(ComparerType comparerType, ComparerOrderType orderType)> priorityList)
	{
		foreach (var priority in priorityList)
		{
			int score = GetScore(player, x, priority.comparerType);
			int score2 = GetScore(player, y, priority.comparerType);
			int num = ComparerResult(score, score2, priority.orderType);
			if (num != 0)
			{
				return num;
			}
		}
		return 0;
	}

    private static int GetScore(IHomeTaskPlayer player, PlayerTask playerTask, ComparerType comparerType)
    { TaskInfo taskInfo=playerTask.Info(); switch(comparerType) {
		case ComparerType.Order:
			return taskInfo.Order;
		case ComparerType.Completed:
			if (playerTask.IsCompletedState())
			{
				if (taskInfo.FinishCondition is SubRankCondition)
				{
					return 2;
				}
				return 1;
			}
			return 0;
		case ComparerType.Duration:
		{
			TimeSpan? remainTime = playerTask.GetRemainTime();
			if (!remainTime.HasValue)
			{
				return int.MaxValue;
			}
			return (int)remainTime.Value.TotalSeconds;
		}
		case ComparerType.AutoTrace:
			if (!taskInfo.AutoTracing)
			{
				return 0;
			}
			return 1;
 default: throw new System.ArgumentOutOfRangeException(nameof(comparerType)); } }
    private static int ComparerResult(long x,long y,ComparerOrderType type) => ConvertLong2IntForSort(type==ComparerOrderType.Asc ? x-y : y-x);
	public static int ConvertLong2IntForSort(long longValue)
	{
		if (longValue == 0L)
		{
			return 0;
		}
		if (longValue <= 0)
		{
			return -1;
		}
		return 1;
	}
}
}
