namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CharacterSkinIdGroupInfo
{
	public List<int> IdList = new List<int>();

	public int GroupId;

	public List<int[]> NestedIdList;

	public const int __ID__ = -8731412;

	public void BuildList()
	{
		foreach (int[] nestedId in NestedIdList)
		{
			int num = nestedId[0];
			int num2 = ((nestedId.Length == 2) ? nestedId[1] : nestedId[0]);
			for (int i = num; i <= num2; i++)
			{
				IdList.Add(i);
			}
		}
	}

}

}
