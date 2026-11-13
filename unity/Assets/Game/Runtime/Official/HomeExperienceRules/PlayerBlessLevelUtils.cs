// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
public static class PlayerBlessLevelUtils
{
	public static bool IsBlessLevelOpen(this HomeExperiencePlayer player)
	{
		if (!player.TryGetSeasonBlessData(player.ServerRecordSeason, out var _))
		{
			return false;
		}
		if (player.SubRank != player.PlanesRank.GetHighSubRank())
		{
			return false;
		}
		if (player.Tables.SystemLevelLimitInfoParser.GetPlayerBlessLevelLimit(player.Rank) <= 0)
		{
			return false;
		}
		if (player.IsMaxLevelByRankLimit())
		{
			return true;
		}
		return false;
	}

	public static void UpdateShowLevel(this HomeExperiencePlayer player)
	{
		int num = player.BaseLevel + player.GetCurBlessPlayerLevel();
		if (num != player.Level)
		{
			player.Level = num;
		}
	}

	public static int GetCurBlessPlayerLevel(this HomeExperiencePlayer player)
	{
		return player.GetBlessPlayerLevel(player.ServerRecordSeason);
	}

	public static int GetBlessPlayerLevel(this HomeExperiencePlayer player, Rank rank)
	{
		int seasonByRank = player.Tables.AstrologicalSeasonConfigParser.GetSeasonByRank(rank);
		return player.GetBlessPlayerLevel(seasonByRank);
	}

	public static int GetBlessPlayerLevel(this HomeExperiencePlayer player, int season)
	{
		if (!player.TryGetSeasonBlessData(season, out var data))
		{
			return 0;
		}
		return player.Tables.LevelBlessInfoParser.GetBlessLevel(player.Rank, data.PlayerBlessExp);
	}

	public static long GetBlessPlayerExp(this HomeExperiencePlayer player, int season)
	{
		if (!player.TryGetSeasonBlessData(season, out var data))
		{
			return 0L;
		}
		return data.PlayerBlessExp;
	}

	public static long GetCurBlessPlayerExp(this HomeExperiencePlayer player)
	{
		int serverRecordSeason = player.ServerRecordSeason;
		return player.GetBlessPlayerExp(serverRecordSeason);
	}

	public static long GetMaxBlessExp(this HomeExperiencePlayer player, Rank planeRank)
	{
		Rank rank = player.Rank;
		if (rank < planeRank)
		{
			return 0L;
		}
		int playerBlessLevelLimit = player.Tables.SystemLevelLimitInfoParser.GetPlayerBlessLevelLimit(rank);
		playerBlessLevelLimit = Math.Min(playerBlessLevelLimit, player.Tables.LevelBlessInfoParser.GetMaxLevel(rank));
		return player.Tables.LevelBlessInfoParser.GetExp(rank, playerBlessLevelLimit);
	}

	public static bool IsMaxBlessLevel(this HomeExperiencePlayer player)
	{
		Rank rank = player.Rank;
		int playerBlessLevelLimit = player.Tables.SystemLevelLimitInfoParser.GetPlayerBlessLevelLimit(rank);
		return player.GetCurBlessPlayerLevel() >= playerBlessLevelLimit;
	}

	public static bool HasBlessLevel(this HomeExperiencePlayer player)
	{
		return player.Level > player.BaseLevel;
	}

	private static int GetSeason(this HomeExperiencePlayer player, HomeExperienceSeasonRecord seasonRecordData)
	{
		return seasonRecordData?.Season ?? player.ServerRecordSeason;
	}

	private static Rank GetPlaneRank(this HomeExperiencePlayer player, HomeExperienceSeasonRecord seasonRecordData)
	{
		return seasonRecordData?.RecordRank ?? player.PlanesRank;
	}

	public static bool PlayerExpLimit(this HomeExperiencePlayer player)
	{
		if (player.PlayerExpLimitFunctionLimited)
		{
			return false;
		}
		HomeExperienceSeasonRecord currentRecordData = player.CurrentRecordData;
		if (player.GetSeason(currentRecordData) > 0)
		{
			Rank planeRank = player.GetPlaneRank(currentRecordData);
			if (player.SubRank == planeRank.GetHighSubRank())
			{
				return false;
			}
		}
		return true;
	}

	public static long GetPlayerExpLimitCount(this HomeExperiencePlayer player)
	{
		LevelExpParser levelExpParser = player.Tables.LevelExpParser;
		int maxStoreExpLevel = player.GetMaxStoreExpLevel();
		return levelExpParser.GetExp(1, maxStoreExpLevel);
	}

	public static bool ShowPlayerStoreExpLimit(this HomeExperiencePlayer player)
	{
		if (!player.PlayerExpLimit())
		{
			return false;
		}
		int playerBaseLevelLimit = player.Tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(player.SubRank);
		return player.Level == playerBaseLevelLimit;
	}

	public static int GetMaxStoreExpLevel(this HomeExperiencePlayer player)
	{
		return player.Tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(player.SubRank + 1);
	}

	public static long GetCurrentExp(this HomeExperiencePlayer player)
	{
		long itemAmount = player.GetItemAmount(player.Tables.PlayerExpItemId);
		if (player.IsBlessLevelOpen())
		{
			return player.GetCurBlessPlayerExp() + itemAmount;
		}
		return itemAmount;
	}

	public static long GetExpRequestByBlessLevel(this HomeExperiencePlayer player, int blessLevel)
	{
		if (blessLevel <= 0)
		{
			return 0L;
		}
		return player.Tables.LevelBlessInfoParser.Get(player.Rank, blessLevel)?.Exp ?? player.GetMaxBlessExp(player.Rank);
	}
	public static bool IsMaxLevelByRankLimit(this HomeExperiencePlayer player)
	{
		if (player.SubRank >= player.Tables.FirstBlessRank.GetHighSubRank() && player.SubRank == player.Rank.GetHighSubRank())
		{
			int playerBaseLevelLimit = player.Tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(player.SubRank);
			return player.BaseLevel >= playerBaseLevelLimit;
		}
		return player.Level >= player.MaxLevelLimit;
	}
}

}
