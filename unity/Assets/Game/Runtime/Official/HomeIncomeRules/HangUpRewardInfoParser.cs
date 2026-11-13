// Selected Console.dll: original PostParse/Get and double-key lookup.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeIncomeRules
{
public sealed class HangUpRewardInfoParser<T> where T : class
{
    private readonly Dictionary<int, Dictionary<int, T>> configs;
    private readonly Dictionary<int, List<int>> mOrderedIndex = new Dictionary<int, List<int>>();
    private readonly Func<T, bool> conditionIsMet;
    public HangUpRewardInfoParser(Dictionary<int, Dictionary<int, T>> configs, Func<T, bool> conditionIsMet)
    {
        this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
        this.conditionIsMet = conditionIsMet ?? throw new ArgumentNullException(nameof(conditionIsMet));
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
				if (key2 > 0)
				{
					if (!mOrderedIndex.TryGetValue(key, out var value))
					{
						value = new List<int>();
						mOrderedIndex.Add(key, value);
					}
					value.Add(key2);
				}
			}
		}
		foreach (KeyValuePair<int, Dictionary<int, T>> config2 in configs)
		{
			if (mOrderedIndex.TryGetValue(config2.Key, out var value2))
			{
				value2.Sort();
			}
		}
	}

	public T Get(int id)
	{
		if (!mOrderedIndex.TryGetValue(id, out var value))
		{
			return Get(id, -1);
		}
		int num = 0;
		int num2 = value.Count - 1;
		while (num < num2)
		{
			int num3 = (num2 + num) / 2;
			int key = value[num3];
			if (!conditionIsMet(Get(id, key)))
			{
				num = num3 + 1;
			}
			else
			{
				num2 = num3;
			}
		}
		return Get(id, value[num2]);
	}

}
}
