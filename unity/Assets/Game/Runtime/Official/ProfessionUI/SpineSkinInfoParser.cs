namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public class SpineSkinInfoParser : ProfessionAppearanceKeyTable<SpineSkinInfo>
{
	public Dictionary<string, SpineSkinInfo> EffectSkinDict = new Dictionary<string, SpineSkinInfo>();

	public Dictionary<SkinType, HashSet<int>> SkinTypeIdDict = new Dictionary<SkinType, HashSet<int>>();

	public Dictionary<string, HashSet<int>> SkinCustomTypeIdDict = new Dictionary<string, HashSet<int>>();

	public void PostParse()
	{
		foreach (KeyValuePair<int, SpineSkinInfo> config in configs)
		{
			SpineSkinInfo value = config.Value;
			if (value.HasEffect)
			{
				EffectSkinDict[value.SkinName] = value;
			}
			if (!SkinTypeIdDict.TryGetValue(value.SkinType, out var value2))
			{
				value2 = new HashSet<int>();
				SkinTypeIdDict.Add(value.SkinType, value2);
			}
			value2.Add(value.Id);
			if (!string.IsNullOrEmpty(value.SkinCustomType))
			{
				if (!SkinCustomTypeIdDict.TryGetValue(value.SkinCustomType, out var value3))
				{
					value3 = new HashSet<int>();
					SkinCustomTypeIdDict.Add(value.SkinCustomType, value3);
				}
				value3.Add(value.Id);
			}
		}
		foreach (KeyValuePair<int, SpineSkinInfo> config2 in configs)
		{
			SpineSkinInfo value4 = config2.Value;
			value4.BlackIdSet.UnionWith(value4.BlackIdList);
			foreach (SkinType blackType in value4.BlackTypeList)
			{
				if (SkinTypeIdDict.TryGetValue(blackType, out var value5))
				{
					value4.BlackIdSet.UnionWith(value5);
				}
			}
			foreach (string blackCustomType in value4.BlackCustomTypeList)
			{
				if (SkinCustomTypeIdDict.TryGetValue(blackCustomType, out var value6))
				{
					value4.BlackIdSet.UnionWith(value6);
				}
			}
			value4.WhiteIdSet.UnionWith(value4.WhiteIdList);
			foreach (string whiteCustomType in value4.WhiteCustomTypeList)
			{
				if (SkinCustomTypeIdDict.TryGetValue(whiteCustomType, out var value7))
				{
					value4.WhiteIdSet.UnionWith(value7);
				}
			}
		}
	}

	public bool HasEffect(string skinName)
	{
		return EffectSkinDict.ContainsKey(skinName);
	}

}

}
