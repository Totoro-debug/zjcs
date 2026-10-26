using System;
using System.Collections.Generic;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    public sealed class OfficialMapEntityProgressRule
    {
        public int EntityClassId;
        public int Progress;
        public int CompleteState;
    }

    public static class OfficialMapExplorationProgress
    {
        public static Dictionary<int, int> CalculateRegionPoints(
            OfficialMapDefinition map,
            IReadOnlyDictionary<int, OfficialMapEntityProgressRule> entityRules,
            IReadOnlyDictionary<int, int> privateEntityStates,
            ISet<int> completedEntityIds)
        {
            if (map == null || map.ClassId <= 0 || map.Entities == null || entityRules == null || completedEntityIds == null)
                throw new ArgumentException("Map, entity rules and completion state are required.");

            var points = new Dictionary<int, int>();
            foreach (OfficialMapRegionProgress maximum in map.MaxRegionProgress ?? Array.Empty<OfficialMapRegionProgress>())
                if (maximum != null)
                    points[maximum.Region] = 0;

            foreach (OfficialMapEntityPlacement entity in map.Entities)
            {
                if (entity == null || entity.Private == null || entity.Private.Length == 0 || !entity.Private[0])
                    continue;
                if (!entityRules.TryGetValue(entity.EntityId, out OfficialMapEntityProgressRule rule))
                    throw new InvalidOperationException("Missing official entity progress rule for EntityClassId " + entity.EntityId);
                if (rule.Progress <= 0)
                    continue;

                privateEntityStates.TryGetValue(entity.Id, out int state);
                bool ended = completedEntityIds.Contains(entity.Id)
                    || state < 0
                    || rule.CompleteState >= 0 && state >= rule.CompleteState;
                if (!ended)
                    continue;

                points.TryGetValue(entity.Region, out int current);
                points[entity.Region] = current + rule.Progress;
            }

            return points;
        }
    }
}
