using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    public interface IMapRegionTime
    {
        int MapClassId { get; }
        DateTime Now { get; }
        int Rank { get; }
        bool IsUnlockTimeReached(int region);
        DateTime GetUnlockTime(int region);
    }

    /// <summary>Original region time branches with the confirmed local character-rank date source.</summary>
    public sealed class OfflineCharacterRankRegionTime : IMapRegionTime
    {
        private readonly Dictionary<int, RegionConfig> regions;
        private readonly Func<OfflinePlayerProgress> player;
        private readonly Func<DateTime> now;

        public OfflineCharacterRankRegionTime(OfficialMapDefinition definition,
            Func<OfflinePlayerProgress> player, Func<DateTime> now)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            MapClassId = definition.ClassId;
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            regions = new Dictionary<int, RegionConfig>();
            foreach (var record in definition.Configs ?? Array.Empty<OfficialWorldConfigRecord>())
            {
                if (record.TableName != "TbMapRegionInfo") continue;
                var region = JsonUtility.FromJson<RegionConfig>(record.Json);
                if (region == null || region.ClassId != definition.ClassId || region.Region < 0
                    || regions.ContainsKey(region.Region) || region.LimitServerRank < 0)
                    throw new InvalidDataException("Original timed-region identity is malformed.");
                // Release groups require their own original calendar and a confirmed
                // local policy. Character-rank dates cannot replace that calendar.
                if (region.ReleaseTimeId > 0)
                    throw new NotSupportedException("Original region release calendar is not bound.");
                regions.Add(region.Region, region);
            }
            if (regions.Count == 0 || !new HashSet<int>(regions.Keys).SetEquals(
                definition.MaxRegionProgress.Select(value => value.Region)))
                throw new InvalidDataException("Original timed-region closure differs from the map.");
        }

        public int MapClassId { get; }
        public DateTime Now => now();
        public int Rank => player().Rank;

        public bool IsUnlockTimeReached(int region)
        {
            var config = Get(region);
            if (config.LimitServerRank != 0)
            {
                var current = player() ?? throw new InvalidOperationException("Character rank state is unavailable.");
                if (current.Rank < config.LimitServerRank) return false;
                if (TryGetRankUnlockTime(config, current, out var unlockTime) && Now < unlockTime)
                    return false;
            }
            return true;
        }

        public DateTime GetUnlockTime(int region)
        {
            var config = Get(region);
            var current = player() ?? throw new InvalidOperationException("Character rank state is unavailable.");
            return TryGetRankUnlockTime(config, current, out var time) ? time : DateTime.MinValue;
        }

        private static bool TryGetRankUnlockTime(RegionConfig config, OfflinePlayerProgress current, out DateTime time)
        {
            time = DateTime.MaxValue;
            if (config.LimitServerRank == 0) return false;
            if (current.Rank > config.LimitServerRank)
            {
                time = DateTime.MinValue;
                return true;
            }
            var acquired = current.RankUnlockTimes?.SingleOrDefault(value => value.Rank == config.LimitServerRank);
            if (acquired == null || acquired.UnixSeconds <= 0) return true;
            // Original MapUtils adds the configured days to the rank date.
            // The user's local policy supplies the character's first rank date
            // and omits server-merge acceleration; it does not flatten offsets.
            time = DateTimeOffset.FromUnixTimeSeconds(acquired.UnixSeconds).LocalDateTime
                .AddDays(config.LimitServerRankUnlockDayOffset);
            return true;
        }

        private RegionConfig Get(int id) => regions.TryGetValue(id, out var value) ? value
            : throw new InvalidDataException("Unknown original timed region: " + id);

        [Serializable]
        private sealed class RegionConfig
        {
            public int ClassId, Region, LimitServerRank, LimitServerRankUnlockDayOffset, ReleaseTimeId, ReleaseTimeOffset;
        }
    }
}
