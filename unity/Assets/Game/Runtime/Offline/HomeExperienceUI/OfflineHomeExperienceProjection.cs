using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;

namespace Zjcs.Offline.HomeExperienceUI
{
    public static class OfflineHomeExperienceProjection
    {
        // Accept the transaction's candidate player, never reread the committed
        // profile while an award is being evaluated on a clone.
        public static HomeExperiencePlayer Capture(OfflinePlayerProgress saved, HomeExperienceTables tables,
            OfflineHomeExperienceEnvironment state)
        {
            if (saved == null || tables == null || state == null) throw new ArgumentNullException(nameof(saved));
            var player = new HomeExperiencePlayer(tables) {
                SubRank = (SubRank)saved.SubRank, BaseLevel = saved.SeasonBaseLevel, Level = saved.Level,
                Experience = saved.Experience, ServerRecordSeason = state.ServerRecordSeason, PlanesRank = state.PlanesRank,
                CurrentRecordData = state.CurrentRecordData == null ? null : new HomeExperienceSeasonRecord {
                    Season = state.CurrentRecordData.Season, RecordRank = state.CurrentRecordData.RecordRank },
                PlayerExpLimitFunctionLimited = state.PlayerExpLimitFunctionLimited,
                PlayerLevelLimitProperty = state.PlayerLevelLimitProperty
            };
            int season = tables.AstrologicalSeasonConfigParser.GetSeasonByRank((Rank)saved.SeasonRank);
            if (saved.HasActiveSeasonRecord && season > 0)
                player.BlessRecords.Add(season, new HomeExperienceBlessRecord { PlayerBlessExp = saved.BlessingExperience });
            player.TryRefreshLevel();
            return player;
        }

        public static CharacterExperienceState GrowthState(HomeExperiencePlayer player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            bool stored = player.ShowPlayerStoreExpLimit();
            return new CharacterExperienceState(player.Level, player.BaseLevel, player.IsBlessLevelOpen(), stored,
                stored ? player.Tables.LevelExpParser.GetExp(1, player.GetMaxStoreExpLevel()) : 0);
        }
    }
}
