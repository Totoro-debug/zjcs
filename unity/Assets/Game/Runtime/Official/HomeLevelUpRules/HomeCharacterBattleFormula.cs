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
public sealed class HomeCharacterBattleFormula
{
    private readonly HomeCharacterBattleTables Tables;
    public HomeCharacterBattleFormula(HomeCharacterBattleTables tables) { Tables = tables ?? throw new ArgumentNullException(nameof(tables)); }
	public Dictionary<PropType, long> CalcCharacterBattlePropsWithoutProfession(int level, SubRank subRank)
	{
		LevelPropParser levelPropParser = Tables.LevelPropParser;
		IReadOnlyDictionary<PropType, long> props = levelPropParser.GetProps(Tables.Settings.PlayerLevelBattlePropId, level);
		IReadOnlyDictionary<PropType, long> props2 = levelPropParser.GetProps(Tables.Settings.PlayerSubRankBattlePropId, (int)subRank);
		Dictionary<PropType, long> dictionary = AddProps(props, props2);
		int playerFightRolePropGroupId = Tables.Settings.PlayerFightRolePropGroupId;
		RolePropGroupInfo rolePropGroupInfo = Tables.RolePropGroupInfoParser.Get(playerFightRolePropGroupId, subRank);
		if (rolePropGroupInfo != null)
		{
			Dictionary<PropType, long> props3 = rolePropGroupInfo.GetProps(level);
			dictionary = AddProps(dictionary, props3);
		}
		return dictionary;
	}

	public Dictionary<PropType, long> CalcCharacterBattleProps(Profession pro, IDictionary<Profession, int> professionLevels, int level, SubRank jingJie)
	{
		Dictionary<PropType, long> props = CalcCharacterBattlePropsWithoutProfession(level, jingJie);
		if (professionLevels != null)
		{
			props = AddProps(props, CalcTotalProfessionLevelProps(professionLevels));
		}
		return AddProps(props, CalcProfessionFeatureProps(pro));
	}

	public Dictionary<PropType, long> CalcTotalProfessionLevelProps(IDictionary<Profession, int> dict)
	{
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		ProfessionBaseInfoParser professionBaseInfoParser = Tables.ProfessionBaseInfoParser;
		LevelPropParser levelPropParser = Tables.LevelPropParser;
		foreach (KeyValuePair<Profession, int> item in dict)
		{
			int levelPropId = professionBaseInfoParser.Get(item.Key).LevelPropId;
			foreach (KeyValuePair<PropType, long> prop in levelPropParser.GetProps(levelPropId, item.Value))
			{
				PropType key = prop.Key;
				long value = prop.Value;
				AddProp(dictionary, key, value);
			}
		}
		return dictionary;
	}

	public Dictionary<PropType, long> CalcProfessionFeatureProps(Profession pro)
	{
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		if (pro == Profession.None)
		{
			return dictionary;
		}
		ProfessionBaseInfo professionBaseInfo = Tables.ProfessionBaseInfoParser.Get(pro);
		dictionary.AddRange(professionBaseInfo.FeatureProps);
		return dictionary;
	}

	public long GetRawProp(Dictionary<PropType, long> props, PropType type)
	{
		if (props == null || !props.TryGetValue(type, out var value))
		{
			return 0L;
		}
		return value;
	}

	public Dictionary<PropType, long> AddProps(IReadOnlyDictionary<PropType, long> props, IReadOnlyDictionary<PropType, long> addProps)
	{
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		if (props == null)
		{
			return dictionary;
		}
		MergePropsInPlace(dictionary, props);
		if (addProps == null)
		{
			return dictionary;
		}
		foreach (KeyValuePair<PropType, long> addProp in addProps)
		{
			AddProp(dictionary, addProp.Key, addProp.Value);
		}
		return dictionary;
	}

	public void AddProp(Dictionary<PropType, long> props, PropType propType, long value)
	{
		props[propType] = GetRawProp(props, propType) + value;
	}

	public void MergePropsInPlace(Dictionary<PropType, long> dest, IReadOnlyDictionary<PropType, long> add)
	{
		if (dest == null)
		{

			HomeLevelUpDiagnostics.Error("BattleFormulaHandler original input error");
		}
		if (add == null || add.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<PropType, long> item in add)
		{
			if (!dest.TryGetValue(item.Key, out var value))
			{
				dest.Add(item.Key, item.Value);
			}
			else
			{
				dest[item.Key] = value + item.Value;
			}
		}
	}
}

}
