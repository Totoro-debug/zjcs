using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;

namespace Zjcs.Offline.HomeExperienceUI
{
    // The existing local content-stage field is the world rank. Personal
    // breakthroughs and season selection never advance this independent value.
    public sealed class OfflineHomeExperienceEnvironmentSource
    {
        readonly Func<OfflinePlayerProgress> player;
        readonly HomeExperienceTables tables;
        readonly Func<bool> expLimitFunctionLimited;
        readonly Func<OfflinePlayerProgress, double> levelLimitProperty;

        public OfflineHomeExperienceEnvironmentSource(Func<OfflinePlayerProgress> player,
            HomeExperienceTables tables, Func<bool> expLimitFunctionLimited,
            Func<OfflinePlayerProgress, double> levelLimitProperty)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
            this.expLimitFunctionLimited = expLimitFunctionLimited ?? throw new ArgumentNullException(nameof(expLimitFunctionLimited));
            this.levelLimitProperty = levelLimitProperty ?? throw new ArgumentNullException(nameof(levelLimitProperty));
        }

        public OfflineHomeExperienceEnvironment Capture()
        {
            var saved = player() ?? throw new InvalidOperationException("Selected home player is unavailable.");
            int worldRank = saved.CharacterProperties?.TreasurePlaneRank ?? 0;
            if (worldRank <= 0 || !Enum.IsDefined(typeof(Rank), worldRank))
                throw new InvalidOperationException("The local world content rank is invalid.");
            int season = tables.AstrologicalSeasonConfigParser.GetSeasonByRank(((SubRank)saved.SubRank).ToRank());
            HomeExperienceSeasonRecord record = null;
            if (saved.HasActiveSeasonRecord)
            {
                int recordedSeason = tables.AstrologicalSeasonConfigParser.GetSeasonByRank((Rank)saved.SeasonRank);
                if (recordedSeason <= 0)
                    throw new InvalidOperationException("The saved active season record has no selected official season mapping.");
                record = new HomeExperienceSeasonRecord { Season = recordedSeason, RecordRank = (Rank)saved.SeasonRank };
            }
            return new OfflineHomeExperienceEnvironment {
                PlanesRank = (Rank)worldRank,
                ServerRecordSeason = season,
                CurrentRecordData = record,
                PlayerExpLimitFunctionLimited = expLimitFunctionLimited(),
                PlayerLevelLimitProperty = levelLimitProperty(saved)
            };
        }
    }
}
