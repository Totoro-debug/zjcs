namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CharacterSkinDyeInfo
{
	public DyeType DyeType;

	public int BaseH;

	public int BaseS;

	public int BaseV;

	public List<int> HSVRangeList;

	public List<int> FixedColorIdList;

	public List<int> RandomWeightList;

	public List<DyeSkinTypeInfo> DyeSkinTypeInfoList;

	public const int __ID__ = 1609130040;

	public int FixedCount => FixedColorIdList.Count;

	public bool IsHsvValid(int h, int s, int v)
	{
		if (h < HSVRangeList[0] || h > HSVRangeList[1])
		{
			return false;
		}
		if (s < HSVRangeList[2] || s > HSVRangeList[3])
		{
			return false;
		}
		if (v < HSVRangeList[4] || v > HSVRangeList[5])
		{
			return false;
		}
		return true;
	}

}

}
