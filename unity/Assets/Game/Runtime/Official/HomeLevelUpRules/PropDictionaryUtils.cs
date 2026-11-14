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

public static class PropDictionaryUtils
{
	public const int kDefaultToNullValue = 0;

	private static bool EnableMemoryOptimization => true;

	public static bool IsValueInIntRange(long value)
	{
		if (value <= int.MaxValue)
		{
			return value >= int.MinValue;
		}
		return false;
	}

	public static IPropDictionary Create()
	{
		_ = EnableMemoryOptimization;
		return new PropDictionaryForNormal();
	}
}

}
