// Migrated from the selected official code; see Map13 entity presentation source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.World.Map13Entities
{
public sealed class MyEntityIndexHandler : IDisposable
{
    private static Dictionary<int, string> sEntityPathCache = new Dictionary<int, string>(2048);
    private Dictionary<int, string> mIdDirDict;
    private Dictionary<string, int[]> mDirIdDict;
    public MyEntityIndexHandler(Dictionary<string, int[]> directories)
    {
        mDirIdDict = directories;
        mIdDirDict = new Dictionary<int, string>();
        foreach (KeyValuePair<string, int[]> item in mDirIdDict)
        {
            int[] value = item.Value;
            foreach (int num in value)
                if (!mIdDirDict.TryAdd(num, item.Key))
                    Debug.LogError("MyEntityManager添加物件路径信息错误，检查是不是有重名Entity，EntityClassId:" + num + ", Path:" + item.Key);
        }
    }
    public void Dispose() { mIdDirDict?.Clear(); mDirIdDict?.Clear(); }
public string GetDir(int entClassId)
	{
		mIdDirDict.TryGetValue(entClassId, out var value);
		return value;
	}

public int[] GetIdsByDir(string dir)
	{
		mDirIdDict.TryGetValue(dir, out var value);
		return value;
	}

public string GetEntityPath(int entClassId)
	{
		if (!sEntityPathCache.TryGetValue(entClassId, out var value))
		{
			string text = string.Format("{0}EcEntity/{1}/{2}.prefab", "Assets/Prefabs/", GetDir(entClassId), entClassId);
			sEntityPathCache.Add(entClassId, text);
			return text;
		}
		return value;
	}
}
}
