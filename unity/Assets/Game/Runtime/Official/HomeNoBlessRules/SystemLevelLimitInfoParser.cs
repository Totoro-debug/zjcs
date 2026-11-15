// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeNoBlessRules
{
public sealed class SystemLevelLimitInfoParser
{
	private Dictionary<Rank, int> mSkillBaseLevelLimitByRank = new Dictionary<Rank, int>();
	private Dictionary<Rank, int> mSkillBlessLevelLimitByRank = new Dictionary<Rank, int>();
	private Dictionary<Rank, int> mEquipBaseLevelLimitByRank = new Dictionary<Rank, int>();
	private Dictionary<Rank, int> mEquipBlessLevelLimitByRank = new Dictionary<Rank, int>();
	private Dictionary<SubRank, int> mPlayerBaseLevelLimitByRank = new Dictionary<SubRank, int>();
	private Dictionary<Rank, int> mPlayerBlessLevelLimitByRank = new Dictionary<Rank, int>();
	private HashSet<Rank> mAllRanks = new HashSet<Rank>();
	private Rank mFirstPlayerBlessLevelRank;
	private Dictionary<SubRank, Dictionary<int, int>> mPetLevelLimitByRank = new Dictionary<SubRank, Dictionary<int, int>>();
	private Dictionary<SubRank, int> mPetBaseLevelLimitByRank = new Dictionary<SubRank, int>();
	private Dictionary<Rank, Dictionary<int, int>> mPetBlessLevelLimitByRank = new Dictionary<Rank, Dictionary<int, int>>();
    public SystemLevelLimitInfoParser(IEnumerable<SystemLevelLimitInfo> configs)
    {
        foreach(var info in configs) {
            SetSkillMaxLevelLimit(info);SetEquipMaxLevelLimit(info);
            SetPlayerMaxLevelLimit(info);SetPetMaxLevelLimit(info);
        }
    }
	private void SetSkillMaxLevelLimit(SystemLevelLimitInfo info)
	{
		Rank key = info.SubRank.ToRank();
		int value2;
		if (info.SkillBlessLevel == 0)
		{
			if (!mSkillBaseLevelLimitByRank.TryGetValue(key, out var value))
			{
				mSkillBaseLevelLimitByRank[key] = info.SkillLevel;
			}
			else
			{
				mSkillBaseLevelLimitByRank[key] = Math.Max(value, info.SkillLevel);
			}
		}
		else if (!mSkillBlessLevelLimitByRank.TryGetValue(key, out value2))
		{
			mSkillBlessLevelLimitByRank[key] = info.SkillBlessLevel;
		}
		else
		{
			mSkillBlessLevelLimitByRank[key] = Math.Max(value2, info.SkillBlessLevel);
		}
	}

	private void SetEquipMaxLevelLimit(SystemLevelLimitInfo info)
	{
		Rank key = info.SubRank.ToRank();
		int value2;
		if (info.EquipBlessLevel == 0)
		{
			if (!mEquipBaseLevelLimitByRank.TryGetValue(key, out var value))
			{
				mEquipBaseLevelLimitByRank[key] = info.EquipLevel;
			}
			else
			{
				mEquipBaseLevelLimitByRank[key] = Math.Max(value, info.EquipLevel);
			}
		}
		else if (!mEquipBlessLevelLimitByRank.TryGetValue(key, out value2))
		{
			mEquipBlessLevelLimitByRank[key] = info.EquipBlessLevel;
		}
		else
		{
			mEquipBlessLevelLimitByRank[key] = Math.Max(value2, info.EquipBlessLevel);
		}
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

	private void SetPetMaxLevelLimit(SystemLevelLimitInfo info)
	{
		SubRank subRank = info.SubRank;
		Rank key = subRank.ToRank();
		if (!mPetLevelLimitByRank.TryGetValue(subRank, out var value))
		{
			value = new Dictionary<int, int>();
			mPetLevelLimitByRank[subRank] = value;
		}
		value.Add(info.PlayerLevel + info.PlayerBlessLevel, info.PetLevel + info.PetBlessLevel);
		if (info.PetBlessLevel == 0)
		{
			if (!mPetBaseLevelLimitByRank.TryGetValue(subRank, out var value2))
			{
				mPetBaseLevelLimitByRank[subRank] = info.PetLevel;
			}
			else
			{
				mPetBaseLevelLimitByRank[subRank] = Math.Max(value2, info.PetLevel);
			}
			return;
		}
		if (!mPetBlessLevelLimitByRank.TryGetValue(key, out var value3))
		{
			value3 = new Dictionary<int, int>();
			mPetBlessLevelLimitByRank[key] = value3;
		}
		int playerBlessLevel = info.PlayerBlessLevel;
		int petBlessLevel = info.PetBlessLevel;
		if (!value3.TryGetValue(playerBlessLevel, out var value4))
		{
			value3[playerBlessLevel] = petBlessLevel;
		}
		else
		{
			value3[playerBlessLevel] = Math.Max(value4, petBlessLevel);
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

	public int GetSkillBaseLevelLimit(SubRank subRank)
	{
		if (mSkillBaseLevelLimitByRank.TryGetValue(subRank.ToRank(), out var value))
		{
			return value;
		}
		return 0;
	}

	public int GetEquipBaseLevelLimit(SubRank subRank)
	{
		if (mEquipBaseLevelLimitByRank.TryGetValue(subRank.ToRank(), out var value))
		{
			return value;
		}
		return 0;
	}

	public int GetPetLevelLimit(SubRank subRank, int playerLevel)
	{
		if (mPetLevelLimitByRank.TryGetValue(subRank, out var value) && value.TryGetValue(playerLevel, out var value2))
		{
			return value2;
		}
		return 0;
	}

	public int GetPetBaseLevelLimit(SubRank subRank)
	{
		if (mPetBaseLevelLimitByRank.TryGetValue(subRank, out var value))
		{
			return value;
		}
		return 0;
	}

}

}
