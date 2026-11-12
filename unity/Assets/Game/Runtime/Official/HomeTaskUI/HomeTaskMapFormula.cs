// Selected official methods; explicit settings and enum projection.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
public static class HomeTaskMapFormula
{
public static int GetCombatRatingLevel(long combatRating, IReadOnlyList<long> combatRatingLevelValues)
	{
		if (combatRatingLevelValues == null || combatRatingLevelValues.Count == 0)
		{
			return 0;
		}
		int i = 0;
		int count;
		for (count = combatRatingLevelValues.Count; i < count && combatRating >= combatRatingLevelValues[i]; i++)
		{
		}
		return count - i;
	}
public static int CalcRankSuppression(int mySubRank, int otherSubRank, int suppressUnlockRank, Func<int,int> toRank)
	{
		if (mySubRank < suppressUnlockRank)
		{
			return 0;
		}
		return toRank(mySubRank) - toRank(otherSubRank);
	}
}
}
