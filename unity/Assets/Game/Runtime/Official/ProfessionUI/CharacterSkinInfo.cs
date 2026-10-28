namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CharacterSkinInfo
{
	public Dictionary<SkinType, Dictionary<SkinUseType, List<int>>> TypeDic = new Dictionary<SkinType, Dictionary<SkinUseType, List<int>>>();

	public Sex Gender;

	public Profession ProfessionLimit;

	public int Body;

	public int Ethnicity;

	public int FaceShape;

	public int Eye;

	public int Brow;

	public int Mouth;

	public int Hair;

	public int FaceDecoration;

	public int FacePaint;

	public int HeadDecoration;

	public int BackDecoration;

	public int PWeapon;

	public int SWeapon;

	public const int __ID__ = 480566772;

	public bool ContainsSkin(SkinType skinType, int skinId)
	{
		if (!TypeDic.TryGetValue(skinType, out var value))
		{
			return false;
		}
		foreach (KeyValuePair<SkinUseType, List<int>> item in value)
		{
			if (item.Value.Contains(skinId))
			{
				return true;
			}
		}
		return false;
	}

	public void BuildTypeDic(CharacterSkinIdGroupInfoParser idGroupParser, SpineSkinInfoParser spineSkinInfoParser)
	{
		AddTypeData(SkinType.body, Body, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.ethnicity, Ethnicity, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.faceShape, FaceShape, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.eye, Eye, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.brow, Brow, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.mouth, Mouth, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.hair, Hair, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.faceDecoration, FaceDecoration, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.facePaint, FacePaint, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.headDecoration, HeadDecoration, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.backDecoration, BackDecoration, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.pWeapon, PWeapon, idGroupParser, spineSkinInfoParser);
		AddTypeData(SkinType.sWeapon, SWeapon, idGroupParser, spineSkinInfoParser);
	}

	public void MergeTypeDict(CharacterSkinInfo characterSkinInfo)
	{
		SkinType skinType = default;
		Dictionary<SkinUseType, List<int>> dictionary = default;
		SkinUseType skinUseType = default;
		List<int> list = default;
		foreach (KeyValuePair<SkinType, Dictionary<SkinUseType, List<int>>> item in characterSkinInfo.TypeDic)
		{
			skinType = item.Key; dictionary = item.Value;
			SkinType key = skinType;
			Dictionary<SkinUseType, List<int>> dictionary2 = dictionary;
			Dictionary<SkinUseType, List<int>> dictionary3 = TypeDic[key];
			foreach (KeyValuePair<SkinUseType, List<int>> item2 in dictionary2)
			{
				skinUseType = item2.Key; list = item2.Value;
				SkinUseType key2 = skinUseType;
				List<int> collection = list;
				if (!dictionary3.TryGetValue(key2, out var value))
				{
					value = new List<int>();
					dictionary3.Add(key2, value);
				}
				value.AddRange(collection);
				value.Sort();
			}
		}
	}

	private void AddTypeData(SkinType skinType, int groupId, CharacterSkinIdGroupInfoParser idGroupParser, SpineSkinInfoParser parser)
	{
		if (!TypeDic.TryGetValue(skinType, out var value))
		{
			value = new Dictionary<SkinUseType, List<int>>();
			TypeDic.Add(skinType, value);
		}
		if (groupId <= 0)
		{
			return;
		}
		CharacterSkinIdGroupInfo characterSkinIdGroupInfo = idGroupParser.Get(groupId);
		if (characterSkinIdGroupInfo == null)
		{
			Debug.LogError("获取不到Group Id:" + (groupId) + "，检查character_skin_id_group.csv配置表");
			return;
		}
		foreach (int id in characterSkinIdGroupInfo.IdList)
		{
			SpineSkinInfo spineSkinInfo = parser.Get(id);
			if (spineSkinInfo != null)
			{
				if (!value.TryGetValue(spineSkinInfo.SkinUseType, out var value2))
				{
					value2 = new List<int>();
					value.Add(spineSkinInfo.SkinUseType, value2);
				}
				value2.Add(id);
			}
		}
	}

}

}
