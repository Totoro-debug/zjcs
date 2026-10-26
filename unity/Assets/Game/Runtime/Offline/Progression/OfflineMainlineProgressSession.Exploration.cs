using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineMainlineProgressSession
    {
        private static void NormalizeExploration(OfflineWorldProgress world)
        {
            world.Exploration ??= new OfflineWorldExplorationProgress();
            var value = world.Exploration;
            value.Day ??= string.Empty;
            value.Records ??= new List<OfflineExploreRecord>();
            value.GroupCounts ??= new List<OfflineExploreGroupCount>();
            value.Entities ??= new List<OfflineExploreEntity>();
            value.History ??= new List<OfflineExploreHistory>();
            if (value.NextSerial == 0 && value.TotalCount == 0 && value.Entities.Count == 0) value.NextSerial = 1;
            foreach (var entity in value.Entities)
            {
                if (entity == null) continue;
                entity.Extra ??= new List<OfflineExploreExtra>();
                foreach (var extra in entity.Extra)
                {
                    if (extra == null) continue;
                    extra.StageIds ??= Array.Empty<int>();
                    extra.ActivityIds ??= Array.Empty<int>();
                }
            }
        }
        private static bool ValidateExploration(OfflineWorldExplorationProgress value)
        {
            if (value == null || value.NextSerial < 1 || value.NextSerial > 1147483647 || value.TotalCount < 0
                || value.DailyCount < 0 || value.DailyCount > value.TotalCount || value.Day == null
                || value.Day.Length != 0 && (value.Day.Length != 8 || !value.Day.All(char.IsDigit))
                || value.Records == null || value.GroupCounts == null || value.Entities == null || value.History == null) return false;
            var records = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in value.Records)
                if (row == null || string.IsNullOrEmpty(row.Key) || row.Key.Length > 100 || row.Index < 0 || row.HitCount < 0
                    || row.KeepMissCount < 0 || row.FireGuaranteedCount < 0 || row.FireGuaranteedCount > row.HitCount
                    || !records.Add(row.Key + ":" + row.Index)) return false;
            var groups = new HashSet<int>();
            foreach (var row in value.GroupCounts)
                if (row == null || row.Group <= 0 || row.Count < 0 || !groups.Add(row.Group)) return false;
            var serials = new HashSet<int>();
            foreach (var entity in value.Entities)
            {
                if (entity == null || entity.Serial < 1 || entity.Serial >= value.NextSerial || !serials.Add(entity.Serial)
                    || entity.MapClassId <= 0 || entity.Area < 0 || entity.Region < 0 || entity.EntityClassId <= 0
                    || entity.Group <= 0 || entity.Variant <= 0 || entity.X < 0 || entity.Y < 0 || entity.Level < 0 || entity.SubRank < 0
                    || entity.CreatedAt <= 0 || entity.ExpiresAt != 0 && entity.ExpiresAt < entity.CreatedAt || entity.Extra == null) return false;
                foreach (var extra in entity.Extra)
                    if (extra == null || string.IsNullOrEmpty(extra.Kind) || extra.StageIds == null || extra.ActivityIds == null
                        || extra.ItemId < 0 || extra.Count < 0 || extra.StageIds.Any(id => id <= 0) || extra.ActivityIds.Any(id => id <= 0)) return false;
            }
            foreach (var row in value.History)
                if (row == null || string.IsNullOrEmpty(row.EventId) || row.EventId.Length > 160 || row.CreatedAt <= 0
                    || row.MapClassId <= 0 || row.Region < 0 || row.Area < 0 || row.X < 0 || row.Y < 0 || row.TokenItemId < 0
                    || row.ExploreGroup <= 0 || row.EntityGroup <= 0 || row.Count <= 0 || row.EntitySerials == null
                    || row.EntitySerials.Length != row.Count || row.EntitySerials.Any(id => id <= 0)) return false;
            return true;
        }
    }
}
