using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using Zjcs.Official.World.NativeMap14Labels;

namespace Zjcs.Editor.MainlineNativeGround
{
    public static partial class NativeGroundAcceptance
    {
        private static void VerifyRegionTimes(OfficialMapDefinition definition, string output)
        {
            var slots = new ProfileSlotCatalog(Path.Combine(output, "rank-time-saves"));
            string slot = slots.CreateSlot();
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            long first = 1700000000;
            Require(session.SetPlayerProgress(100, 3, 7, 4, "", Array.Empty<int>(), first - 86400).IsApplied,
                "Persist initial character rank.");
            Require(session.SetPlayerProgress(100, 4, 10, 4, "", Array.Empty<int>(), first).IsApplied,
                "Persist first rank four acquisition.");
            session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(session.Save.Player.RankUnlockTimes.Single(r => r.Rank == 4).UnixSeconds == first,
                "Cold save retains first rank date.");
            Require(session.SetPlayerProgress(100, 4, 10, 4, "", Array.Empty<int>(), first + 86400).IsApplied,
                "Repeated same-rank update.");
            Require(session.Save.Player.RankUnlockTimes.Single(r => r.Rank == 4).UnixSeconds == first,
                "Repeated update preserves first rank date.");
            var current = session.Save.Player;
            DateTime now = DateTimeOffset.FromUnixTimeSeconds(first).LocalDateTime;
            var time = new OfflineCharacterRankRegionTime(definition, () => current, () => now);
            var rows = definition.Configs.Where(r => r.TableName == "TbMapRegionInfo")
                .Select(r => JsonUtility.FromJson<MapRegionInfo>(r.Json)).ToArray();
            Require(rows.Length == 43, "Actual whale-island region closure.");
            Require(rows.Where(r => r.LimitServerRank == 4).Min(r => r.LimitServerRankUnlockDayOffset) == 14
                && rows.Where(r => r.LimitServerRank == 4).Max(r => r.LimitServerRankUnlockDayOffset) == 32,
                "Actual configured offsets retain fourteen through thirty-two days.");
            foreach (var row in rows)
            {
                if (row.LimitServerRank == 0)
                {
                    Require(time.IsUnlockTimeReached(row.Region), "Original region without time limit.");
                    continue;
                }
                DateTime expected = DateTimeOffset.FromUnixTimeSeconds(first).LocalDateTime
                    .AddDays(row.LimitServerRankUnlockDayOffset);
                Require(time.GetUnlockTime(row.Region) == expected, "Original per-region day offset.");
                now = expected.AddSeconds(-1);
                Require(!time.IsUnlockTimeReached(row.Region), "One second before unlock.");
                row.BindUnlockTime(() => time.IsUnlockTimeReached(row.Region), () => time.GetUnlockTime(row.Region));
                Require(row.IsLimitedByType(-1) == MapRegionLimitType.RegionUnlockTime, "Original time-before-level order.");
                now = expected;
                Require(time.IsUnlockTimeReached(row.Region), "Exact unlock boundary is inclusive.");
                Require(row.IsLimitedByType(-1) == MapRegionLimitType.Level, "Level remains after time expires.");
                Require(row.IsLimitedByType(row.LimitLevel) == MapRegionLimitType.None, "Exact level boundary.");
                current.Rank = 3;
                Require(!time.IsUnlockTimeReached(row.Region), "Lower rank remains blocked despite old date.");
                current = new OfflinePlayerProgress { Rank = 4 };
                Require(!time.IsUnlockTimeReached(row.Region) && time.GetUnlockTime(row.Region) == DateTime.MaxValue,
                    "Missing date stays closed.");
                current.Rank = 5;
                Require(time.IsUnlockTimeReached(row.Region) && time.GetUnlockTime(row.Region) == DateTime.MinValue,
                    "Higher rank passes immediately without a date.");
                current = session.Save.Player;
                current.Rank = 4;
            }
            session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(session.Save.Player.Rank == 4 && session.Save.Player.RankUnlockTimes.Single(r => r.Rank == 4).UnixSeconds == first,
                "Rule probes leave persisted character and first date intact.");
            Debug.Log("Whale-island timed regions passed: all 43 original rows, per-region 14–32 day boundaries, time-before-level, lower/higher rank, missing date and isolated profile cold save.");
        }
    }
}
