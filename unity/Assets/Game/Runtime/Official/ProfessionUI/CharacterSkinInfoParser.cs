namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Linq;

public class CharacterSkinInfoParser
{
	public CharacterSkinInfo[] Rows;

	public Dictionary<int, CharacterSkinInfo> SkinDict = new Dictionary<int, CharacterSkinInfo>();

	private static List<int> sWeightList = new List<int>();

	public void PostParse(ProfessionAppearanceTables tables)
	{
		SpineSkinInfoParser parser = tables.SpineSkinInfoParser;
		CharacterSkinIdGroupInfoParser parser2 = tables.CharacterSkinIdGroupInfoParser;
		ProfessionAppearanceBaseInfoParser parser3 = tables.ProfessionBaseInfoParser;
		List<CharacterSkinInfo> list = new List<CharacterSkinInfo>();
		CharacterSkinInfo[] array = Rows;
		foreach (CharacterSkinInfo characterSkinInfo in array)
		{
			int key = GetKey(characterSkinInfo.Gender, characterSkinInfo.ProfessionLimit);
			characterSkinInfo.BuildTypeDic(parser2, parser);
			if (!ProfessionAppearanceBaseInfo.BaseProfessions.Contains(characterSkinInfo.ProfessionLimit))
			{
				list.Add(characterSkinInfo);
			}
			SkinDict.Add(key, characterSkinInfo);
		}
		foreach (CharacterSkinInfo item in list)
		{
			ProfessionAppearanceBaseInfo professionBaseInfo = parser3.Get(item.ProfessionLimit);
			CharacterSkinInfo skinInfo = GetSkinInfo(item.Gender, professionBaseInfo.PrePro);
			item.MergeTypeDict(skinInfo);
		}
	}

	public CharacterSkinInfo GetSkinInfo(Sex gender, Profession profession)
	{
		int key = GetKey(gender, profession);
		SkinDict.TryGetValue(key, out var value); return value;
	}

	public static int GetKey(Sex gender, Profession profession)
	{
		return ((int)gender + 1) * 10000 + (int)profession * 10;
	}

}

}
