using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.HomePlayerStatusRules;

namespace Zjcs.Offline.Progression
{
    // Storage validation only. Gain/reset/count/occupancy rules are migrated
    // from PlayerStatusEffectDataWrap and PlayerStatusEffectManager.
    public static class OfflinePlayerStatusProgress
    {
        public static bool IsValid(IReadOnlyList<PlayerStatusEffectData> records, long playerId)
        {
            if (records == null) return false;
            var ids = new HashSet<int>();
            foreach (var record in records)
            {
                if (record == null || record.PlayerId <= 0 || record.PlayerId != playerId
                    || record.ClassId < 0 || !ids.Add(record.ClassId) || record.ActivedTimes < 0
                    || !Enum.IsDefined(typeof(EStatusSourceType), record.StatusSourceType)
                    || record.LastGainedTimeTicks < DateTime.MinValue.Ticks
                    || record.LastGainedTimeTicks > DateTime.MaxValue.Ticks
                    || !Enum.IsDefined(typeof(DateTimeKind), record.LastGainedTimeKind)
                    || record.OccupiedFightIds == null
                    || record.OccupiedFightIds.Count != record.OccupiedFightIds.Distinct().Count()) return false;
            }
            return true;
        }
    }
}
