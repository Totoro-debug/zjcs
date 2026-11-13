// Selected Console.dll: original PostParse/Get and double-key lookup.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeIncomeRules
{
public sealed class RewardRuleInfoParser<T> where T : class
{
    private readonly Dictionary<int, Dictionary<int, T>> configs;
    private readonly Dictionary<int, List<int>> mOrderedIndex = new Dictionary<int, List<int>>();
    private readonly Func<T, bool> conditionIsMet;
    private readonly Func<T, bool> hasCondition;
    public RewardRuleInfoParser(Dictionary<int, Dictionary<int, T>> configs, Func<T, bool> conditionIsMet, Func<T, bool> hasCondition)
    {
        this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
        this.conditionIsMet = conditionIsMet ?? throw new ArgumentNullException(nameof(conditionIsMet));
        this.hasCondition = hasCondition ?? throw new ArgumentNullException(nameof(hasCondition));
        PostParse(configs);
    }
	public T Get(int key0, int key1)
	{
		if (configs.TryGetValue(key0, out var value) && value.TryGetValue(key1, out var value2))
		{
			return value2;
		}
		return default;
	}

	private void PostParse(Dictionary<int, Dictionary<int, T>> configs)
	{
		mOrderedIndex.Clear();
		foreach (KeyValuePair<int, Dictionary<int, T>> config in configs)
		{
			int key = config.Key;
			foreach (KeyValuePair<int, T> item in config.Value)
			{
				int key2 = item.Key;
				_ = item.Value;
				if (!mOrderedIndex.TryGetValue(key, out var value))
				{
					value = new List<int>();
					mOrderedIndex.Add(key, value);
				}
				value.Add(key2);
			}
			mOrderedIndex[key].Sort();
		}
	}

	public T Get(int id, bool ignoreCondition = false)
	{
		if (!mOrderedIndex.TryGetValue(id, out var value))
		{
			return null;
		}
		bool flag = false;
		for (int i = 0; i < value.Count; i++)
		{
			int num = value[i];
			if (num == -1)
			{
				flag = true;
				continue;
			}
			T rewardRuleInfo = Get(id, num);
			if (ignoreCondition || !hasCondition(rewardRuleInfo) || conditionIsMet(rewardRuleInfo))
			{
				return rewardRuleInfo;
			}
		}
		if (flag)
		{
			T rewardRuleInfo2 = Get(id, -1);
			if (ignoreCondition || !hasCondition(rewardRuleInfo2) || conditionIsMet(rewardRuleInfo2))
			{
				return rewardRuleInfo2;
			}
		}
		return null;
	}

}
}
