using System.Collections.Generic;
using UnityEngine;

using System;
using Zjcs.Official.EntityLoading;
namespace Zjcs.Official.EntityIndex
{

public class MyEntityIndexHandler : IMyEntityIndexHandler, IDisposable
{
	private static string sIndexPath = "Assets/Config/EC/index.g.txt";

	private static Dictionary<int, string> sEntityPathCache = new Dictionary<int, string>(2048);

	private Dictionary<int, string> mIdDirDict;

	private Dictionary<string, int[]> mDirIdDict;

	public IEntityIndexContext Context { get; set; }

	public void Initialize()
	{
		Init();
	}

	public void Dispose()
	{
		mIdDirDict?.Clear();
		mDirIdDict?.Clear();
	}

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

	private void Init()
	{
		string context = GetContext();
		mDirIdDict = Context.Decode(context);
		mIdDirDict = new Dictionary<int, string>();
		foreach (KeyValuePair<string, int[]> item in mDirIdDict)
		{
			int[] value = item.Value;
			foreach (int num in value)
			{
				if (!mIdDirDict.TryAdd(num, item.Key))
				{
					UnityEngine.Debug.LogError("Original entity loading diagnostic");
				}
			}
		}
	}

	private string GetContext()
	{
		return Context.LoadText(sIndexPath);
	}
}

}
