// Two original Common.Utils methods; see attachment runtime source receipt.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.StoryAttachment
{
internal static class AttachmentRegistryUtils
{
public static void FillMapByList<T1, T2>(List<T1> keyList, List<T2> valueList, ref Dictionary<T1, T2> map)
	{
		if (map == null)
		{
			map = new Dictionary<T1, T2>();
		}
		else
		{
			map.Clear();
		}
		for (int i = 0; i < keyList.Count; i++)
		{
			if (valueList[i] != null)
			{
				map[keyList[i]] = valueList[i];
			}
		}
	}

public static void FillListByMapSort<T1, T2>(Dictionary<T1, T2> map, List<T1> keyList, List<T2> valueList)
	{
		T1[] array = new T1[map.Keys.Count];
		map.Keys.CopyTo(array, 0);
		Array.Sort(array);
		keyList.Clear();
		valueList.Clear();
		keyList.Capacity = array.Length;
		valueList.Capacity = array.Length;
		T1[] array2 = array;
		foreach (T1 val in array2)
		{
			if (map[val] != null)
			{
				keyList.Add(val);
				valueList.Add(map[val]);
			}
		}
	}
}
}
