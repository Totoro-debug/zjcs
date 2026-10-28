// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System.Collections.Generic;
using UnityEngine;




namespace Zjcs.Official.World.Map13GridVisibility
{
public static class VisibilityRangeMath
{
	public delegate bool LoopOutCallback(Pos2d pos, int dist);

	private const int InitCacheRange = 10;

	private const int InitCacheDistance = 20;

	private static List<List<Pos2d>> PosListInDistances;

	private static List<List<Pos2d>> PosListInRanges;

	static VisibilityRangeMath()
	{
		PosListInDistances = new List<List<Pos2d>>();
		PosListInRanges = new List<List<Pos2d>>();
		PosListInDistances.Add(new List<Pos2d> { Pos2d.Zero });
		PosListInDistances.Add(Pos2d.FourDirs);
		for (int i = 2; i < 20; i++)
		{
			PosListInDistances.Add(GetInDistancePosList(i));
		}
		PosListInRanges.Add(new List<Pos2d> { Pos2d.Zero });
		for (int j = 1; j < 10; j++)
		{
			PosListInRanges.Add(GetLoopOutPosList(j));
		}
	}

	private static List<Pos2d> GetInDistancePosList(int dist)
	{
		if (dist < 0)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			dist = 0;
		}
		if (dist >= PosListInDistances.Count)
		{
			HashSet<Pos2d> hashSet = new HashSet<Pos2d>();
			List<Pos2d> inDistancePosList = GetInDistancePosList(dist - 1);
			List<Pos2d> inDistancePosList2 = GetInDistancePosList(dist - 2);
			List<Pos2d> list = new List<Pos2d>(dist * 4);
			{
				foreach (Pos2d item2 in inDistancePosList)
				{
					foreach (Pos2d fourDir in Pos2d.FourDirs)
					{
						Pos2d item = item2 + fourDir;
						if (!inDistancePosList.Contains(item) && !inDistancePosList2.Contains(item) && hashSet.Add(item))
						{
							list.Add(item);
						}
					}
				}
				return list;
			}
		}
		return PosListInDistances[dist];
	}

	public static List<Pos2d> GetLoopOutPosList(int maxDist)
	{
		if (maxDist < 0)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			maxDist = 0;
		}
		if (maxDist >= PosListInRanges.Count)
		{
			List<Pos2d> loopOutPosList = GetLoopOutPosList(maxDist - 1);
			List<Pos2d> inDistancePosList = GetInDistancePosList(maxDist);
			List<Pos2d> list = new List<Pos2d>(loopOutPosList.Count + inDistancePosList.Count);
			list.AddRange(loopOutPosList);
			list.AddRange(inDistancePosList);
			return list;
		}
		return PosListInRanges[maxDist];
	}

public static List<Pos2d> GetLoopOutList(Pos2d center, int maxDist, bool includingCenter = true)
	{
		List<Pos2d> loopOutPosList = GetLoopOutPosList(maxDist);
		int count = loopOutPosList.Count;
		List<Pos2d> list = new List<Pos2d>(count);
		for (int i = 0; i < count; i++)
		{
			list.Add(loopOutPosList[i] + center);
		}
		if (!includingCenter)
		{
			list.Remove(center);
		}
		return list;
	}
}

}
