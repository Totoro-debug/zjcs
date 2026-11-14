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
[Serializable] public sealed class RolePropGroupInfo
{
    public int ID, LevelPropID, ExtraLevelPropID;
    public SubRank SubRank;
    internal HomeCharacterBattleTables Tables;
	public Dictionary<PropType, long> GetProps(int level)
	{
		LevelPropParser levelPropParser = Tables.LevelPropParser;
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		IReadOnlyDictionary<PropType, long> props = levelPropParser.GetProps(LevelPropID, level);
		Tables.Formula.MergePropsInPlace(dictionary, props);
		if (ExtraLevelPropID != 0)
		{
			IReadOnlyDictionary<PropType, long> props2 = levelPropParser.GetProps(ExtraLevelPropID, level);
			Tables.Formula.MergePropsInPlace(dictionary, props2);
		}
		return dictionary;
	}
}

}
