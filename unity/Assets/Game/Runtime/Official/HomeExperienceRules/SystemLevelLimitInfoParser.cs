// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
public sealed class SystemLevelLimitInfoParser
{
    private Dictionary<SubRank, int> mPlayerBaseLevelLimitByRank = new Dictionary<SubRank, int>();
    private Dictionary<Rank, int> mPlayerBlessLevelLimitByRank = new Dictionary<Rank, int>();
    private HashSet<Rank> mAllRanks = new HashSet<Rank>();
    private Rank mFirstPlayerBlessLevelRank;
    public Rank FirstPlayerBlessLevelRank => mFirstPlayerBlessLevelRank;
    public IEnumerable<Rank> GetAllRanks => mAllRanks;
    public SystemLevelLimitInfoParser(IEnumerable<SystemLevelLimitInfo> rows)
    {
        mFirstPlayerBlessLevelRank = Rank.None;
        foreach (var row in rows) SetPlayerMaxLevelLimit(row);
    }
	private void SetPlayerMaxLevelLimit(SystemLevelLimitInfo info)
	{
		SubRank subRank = info.SubRank;
		Rank rank = subRank.ToRank();
		if (info.PlayerBlessLevel == 0)
		{
			if (!mPlayerBaseLevelLimitByRank.TryGetValue(subRank, out var value))
			{
				mAllRanks.Add(rank);
				mPlayerBaseLevelLimitByRank[subRank] = info.PlayerLevel;
			}
			else
			{
				mPlayerBaseLevelLimitByRank[subRank] = Math.Max(value, info.PlayerLevel);
			}
			return;
		}
		bool flag = mFirstPlayerBlessLevelRank == Rank.None || mFirstPlayerBlessLevelRank > rank;
		mFirstPlayerBlessLevelRank = (flag ? rank : mFirstPlayerBlessLevelRank);
		if (!mPlayerBlessLevelLimitByRank.TryGetValue(rank, out var value2))
		{
			mPlayerBlessLevelLimitByRank[rank] = info.PlayerBlessLevel;
		}
		else
		{
			mPlayerBlessLevelLimitByRank[rank] = Math.Max(value2, info.PlayerBlessLevel);
		}
	}

	public int GetPlayerBaseLevelLimit(SubRank subRank)
	{
		if (mPlayerBaseLevelLimitByRank.TryGetValue(subRank, out var value))
		{
			return value;
		}
		return 0;
	}

	public int GetPlayerBlessLevelLimit(Rank rank)
	{
		if (mPlayerBlessLevelLimitByRank.TryGetValue(rank, out var value))
		{
			return value;
		}
		return 0;
	}
}

}
