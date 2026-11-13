// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
public sealed class LevelBlessInfoParser
{
    private readonly LevelBlessInfo[] rows;
    private readonly Dictionary<Rank, Dictionary<int, LevelBlessInfo>> lookup = new Dictionary<Rank, Dictionary<int, LevelBlessInfo>>();
    private Dictionary<Rank, List<LevelBlessInfo>> mLevelBlessInfos;
    public LevelBlessInfoParser(IEnumerable<LevelBlessInfo> source)
    {
        rows = new List<LevelBlessInfo>(source).ToArray();
        foreach (var row in rows) {
            if (!lookup.TryGetValue(row.Rank, out var group)) lookup.Add(row.Rank, group = new Dictionary<int, LevelBlessInfo>());
            group.Add(row.BlessLevel, row);
        }
        PostParse();
    }
    private LevelBlessInfo[] GetAll() => rows;
    public LevelBlessInfo Get(Rank rank, int level) => lookup.TryGetValue(rank, out var group) && group.TryGetValue(level, out var row) ? row : null;
	private void PostParse()
	{
		mLevelBlessInfos = new Dictionary<Rank, List<LevelBlessInfo>>();
		LevelBlessInfo[] all = GetAll();
		foreach (LevelBlessInfo levelBlessInfo in all)
		{
			if (!mLevelBlessInfos.TryGetValue(levelBlessInfo.Rank, out var value))
			{
				value = new List<LevelBlessInfo>();
				mLevelBlessInfos[levelBlessInfo.Rank] = value;
			}
			value.Add(levelBlessInfo);
		}
		foreach (KeyValuePair<Rank, List<LevelBlessInfo>> mLevelBlessInfo in mLevelBlessInfos)
		{
			mLevelBlessInfo.Value.Sort((LevelBlessInfo x, LevelBlessInfo y) => x.BlessLevel.CompareTo(y.BlessLevel));
		}
	}
	public int GetMaxLevel(Rank rank)
	{
		if (!mLevelBlessInfos.TryGetValue(rank, out var value))
		{
			return 0;
		}
		return value[value.Count - 1].BlessLevel;
	}

	public long GetExp(Rank rank, int level)
	{
		int maxLevel = GetMaxLevel(rank);
		if (level >= maxLevel)
		{
			level = maxLevel;
		}
		return Get(rank, level)?.Exp ?? 0;
	}

	public int GetBlessLevel(Rank rank, long exp)
	{
		if (!mLevelBlessInfos.TryGetValue(rank, out var value))
		{
			return 0;
		}
		int count = value.Count;
		int num = 0;
		int num2 = count - 1;
		int num3 = -1;
		while (num <= num2)
		{
			int num4 = (num + num2) / 2;
			if (value[num4].Exp <= exp)
			{
				num3 = num4;
				num = num4 + 1;
			}
			else
			{
				num2 = num4 - 1;
			}
		}
		if (num3 < 0)
		{
			return 0;
		}
		return value[num3].BlessLevel;
	}
}

}
