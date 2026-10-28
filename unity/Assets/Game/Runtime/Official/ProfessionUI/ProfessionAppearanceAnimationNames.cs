namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
public static class ProfessionAppearanceAnimationNames
{
    private static readonly Dictionary<int, object[]> sFormatArrayDict = new Dictionary<int, object[]>();
    private static readonly Dictionary<int, string> sIdDict = new Dictionary<int, string>();
	public static string GetAnimName(IProfessionPlayerAppearanceBindings bindings, AppearanceAnimNameCombineRule appearanceAnimNameCombineRule, string ruleKey, List<string> inputList, Sex sex = Sex.Female, int profession = 0)
	{
		AnimCombineRule rule = appearanceAnimNameCombineRule.GetRule(ruleKey);
		if (rule == null || rule.FillList == null)
		{
			return string.Empty;
		}
		int count = rule.FillList.Count;
		if (!sFormatArrayDict.TryGetValue(count, out var value))
		{
			value = new object[count];
			sFormatArrayDict.Add(count, value);
		}
		int num = 0;
		int i = 0;
		for (int count2 = rule.FillList.Count; i < count2; i++)
		{
			switch (rule.FillList[i])
			{
			case RuleFillType.InputStr:
				value[i] = inputList[num++];
				break;
			case RuleFillType.InputId:
				value[i] = GetFormatId(int.Parse(inputList[num++]));
				break;
			case RuleFillType.Profession:
				value[i] = bindings.GetProfessionIdStr(profession);
				break;
			case RuleFillType.Sex:
				value[i] = sex == Sex.Male ? "M" : sex == Sex.Female ? "F" : null;
				break;
			}
		}
		return string.Format(rule.Content, value);
	}

	public static string GetFormatId(int id)
	{
		if (!sIdDict.TryGetValue(id, out var value))
		{
			value = ((id < 10) ? $"0{id}" : id.ToString());
			sIdDict.Add(id, value);
		}
		return value;
	}

}

}
