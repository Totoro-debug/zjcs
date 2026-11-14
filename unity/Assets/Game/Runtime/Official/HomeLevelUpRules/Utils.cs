// Selected original 884 implementation; configuration and logging boundaries only.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeLevelUpRules
{
internal static class Utils
{
	public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)
	{
		foreach (KeyValuePair<TKey, TValue> item in source)
		{
			target.Add(item.Key, item.Value);
		}
	}
}

}
