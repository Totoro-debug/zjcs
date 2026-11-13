using System;
using System.Collections.Generic;

namespace Zjcs.Official.BattleBgm
{
public sealed class FightThemeInfoParser
{
    private readonly IReadOnlyDictionary<int, FightThemeInfo> configs;
    private readonly Func<int, bool?> conditionIsMet;
    private readonly Func<int, int> next;

    public FightThemeInfoParser(IReadOnlyDictionary<int, FightThemeInfo> configurations,
        Func<int, bool?> evaluateCondition, Func<int, int> nextRandom)
    {
        configs = configurations ?? throw new ArgumentNullException(nameof(configurations));
        conditionIsMet = evaluateCondition ?? throw new ArgumentNullException(nameof(evaluateCondition));
        next = nextRandom ?? throw new ArgumentNullException(nameof(nextRandom));
    }

	public FightThemeInfo GetThemeIdByStageInfo(int stageType)
	{
		foreach (KeyValuePair<int, FightThemeInfo> config in configs)
		{
			FightThemeInfo value = config.Value;
			if (value.StageTypes.Contains(stageType))
			{
				bool? condition = conditionIsMet(value.ConditionId);
				if (condition == true && next(10000) < value.TriggerPercent)
				{
					return value;
				}
			}
		}
		return null;
	}

}
}
