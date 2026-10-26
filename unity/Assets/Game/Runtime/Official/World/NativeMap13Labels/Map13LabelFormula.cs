// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System.Collections.Generic;
public static class Map13LabelFormula
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
public static Rank ToRank(this SubRank subRank)
	{
		switch (subRank)
		{
		case SubRank.None:
			return Rank.None;
		case SubRank.Norank:
			return Rank.Norank;
		case SubRank.Blackiron1:
		case SubRank.Blackiron2:
		case SubRank.Blackiron3:
			return Rank.Blackiron;
		case SubRank.Bronze1:
		case SubRank.Bronze2:
		case SubRank.Bronze3:
			return Rank.Bronze;
		case SubRank.Silver1:
		case SubRank.Silver2:
		case SubRank.Silver3:
			return Rank.Silver;
		case SubRank.Gold1:
		case SubRank.Gold2:
		case SubRank.Gold3:
			return Rank.Gold;
		case SubRank.Saint1:
		case SubRank.Saint2:
		case SubRank.Saint3:
			return Rank.Saint;
		case SubRank.Legend1:
		case SubRank.Legend2:
		case SubRank.Legend3:
			return Rank.Legend;
		case SubRank.Angel1:
		case SubRank.Angel2:
		case SubRank.Angel3:
			return Rank.Angel;
		case SubRank.Godtouched1_1:
		case SubRank.Godtouched1_2:
		case SubRank.Godtouched1_3:
			return Rank.Godtouched1;
		case SubRank.Godtouched2_1:
		case SubRank.Godtouched2_2:
		case SubRank.Godtouched2_3:
			return Rank.Godtouched2;
		case SubRank.Godtouched3_1:
		case SubRank.Godtouched3_2:
		case SubRank.Godtouched3_3:
			return Rank.Godtouched3;
		case SubRank.Demigod1_1:
		case SubRank.Demigod1_2:
		case SubRank.Demigod1_3:
			return Rank.Demigod1;
		case SubRank.Demigod2_1:
		case SubRank.Demigod2_2:
		case SubRank.Demigod2_3:
			return Rank.Demigod2;
		case SubRank.Demigod3_1:
		case SubRank.Demigod3_2:
		case SubRank.Demigod3_3:
			return Rank.Demigod3;
		case SubRank.Stargod1_1:
		case SubRank.Stargod1_2:
		case SubRank.Stargod1_3:
			return Rank.Stargod1;
		case SubRank.Stargod2_1:
		case SubRank.Stargod2_2:
		case SubRank.Stargod2_3:
			return Rank.Stargod2;
		case SubRank.Stargod3_1:
		case SubRank.Stargod3_2:
		case SubRank.Stargod3_3:
			return Rank.Stargod3;
		case SubRank.Lordgod1_1:
		case SubRank.Lordgod1_2:
		case SubRank.Lordgod1_3:
			return Rank.Lordgod1;
		case SubRank.Lordgod2_1:
		case SubRank.Lordgod2_2:
		case SubRank.Lordgod2_3:
			return Rank.Lordgod2;
		case SubRank.Lordgod3_1:
		case SubRank.Lordgod3_2:
		case SubRank.Lordgod3_3:
			return Rank.Lordgod3;
		case SubRank.Eternal1_1:
		case SubRank.Eternal1_2:
		case SubRank.Eternal1_3:
			return Rank.Eternal1;
		case SubRank.Eternal2_1:
		case SubRank.Eternal2_2:
		case SubRank.Eternal2_3:
			return Rank.Eternal2;
		case SubRank.Eternal3_1:
		case SubRank.Eternal3_2:
		case SubRank.Eternal3_3:
			return Rank.Eternal3;
		case SubRank.Nihility1_1:
		case SubRank.Nihility1_2:
		case SubRank.Nihility1_3:
			return Rank.Nihility1;
		case SubRank.Nihility2_1:
		case SubRank.Nihility2_2:
		case SubRank.Nihility2_3:
			return Rank.Nihility2;
		case SubRank.Nihility3_1:
		case SubRank.Nihility3_2:
		case SubRank.Nihility3_3:
			return Rank.Nihility3;
		case SubRank.Chaos1_1:
		case SubRank.Chaos1_2:
		case SubRank.Chaos1_3:
			return Rank.Chaos1;
		case SubRank.Chaos2_1:
		case SubRank.Chaos2_2:
		case SubRank.Chaos2_3:
			return Rank.Chaos2;
		case SubRank.Chaos3_1:
		case SubRank.Chaos3_2:
		case SubRank.Chaos3_3:
			return Rank.Chaos3;
		default:
		{
			UnityEngine.Debug.LogError("Original sub-rank has no rank mapping: " + subRank);
			return Rank.None;
		}
		}
	}
}
}
