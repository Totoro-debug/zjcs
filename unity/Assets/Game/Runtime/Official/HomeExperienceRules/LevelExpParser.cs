// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
public sealed class LevelExpParser
{
    private readonly Dictionary<int, LevelExp> rows = new Dictionary<int, LevelExp>();
    public LevelExpParser(IEnumerable<LevelExp> source) { foreach (var row in source) rows.Add(row.Id, row); }
    public LevelExp Get(int id) { rows.TryGetValue(id, out var row); return row; }
	public const int PLAYER_LEVEL_UP_ID = 1;

	public const int PLAYER_PET_LEVEL_UP_ID = 2;

	public const int FRIENDSHIP_LEVEL_UP_ID = 4;

	public const int APPEARANCE_LEVEL_UP_ID = 6;

	public int GetLevelsCount(int id)
	{
		return Get(id)?.Exp.Count ?? 0;
	}

	public bool IsInLevelRange(int id, int level)
	{
		if (level >= GetLevelMin(id))
		{
			return level <= GetLevelMax(id);
		}
		return false;
	}

	public int GetLevelMax(int id)
	{
		LevelExp levelExp = Get(id);
		if (levelExp == null)
		{
			return int.MinValue;
		}
		return levelExp.Exp.Count - 1;
	}

	public int GetLevelMin(int id)
	{
		if (Get(id) == null)
		{
			return int.MaxValue;
		}
		return 0;
	}

	public long GetExp(int id, int level)
	{
		LevelExp levelExp = Get(id);
		if (levelExp == null)
		{
			return long.MaxValue;
		}
		List<long> exp = levelExp.Exp;
		if (level <= 0)
		{
			return 0L;
		}
		if (level >= exp.Count)
		{
			return long.MaxValue;
		}
		return exp[level];
	}

	public long GetLevelUpExp(int id, int level)
	{
		return GetExp(id, level + 1) - GetExp(id, level);
	}

	public long GetLevelUpExp(int id, int fromLevel, int toLevel)
	{
		return GetExp(id, toLevel) - GetExp(id, fromLevel);
	}

	public int CalcLevelByExp(int id, long exp)
	{
		List<long> exp2 = Get(id).Exp;
		if (exp2.Count == 0)
		{
			return 0;
		}
		int num = 0;
		int num2 = exp2.Count - 1;
		int result = 0;
		while (num <= num2)
		{
			int num3 = num + (num2 - num) / 2;
			if (exp2[num3] <= exp)
			{
				result = num3;
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		return result;
	}

}

}
