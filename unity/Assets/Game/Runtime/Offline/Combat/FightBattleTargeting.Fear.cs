using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightBattleTargeting
    {
        // Common.ActionType.Fear. The original entry deprioritizes its
        // applicator; it does not make that unit an illegal attack target.
        private const int FearActionType = 78;

        private static void RestrictFearSources(FightBattleUnitState actor,
            List<FightBattleUnitState> candidates, int targetType)
        {
            if (targetType != 1 || candidates.Count < 2) return;
            var sources = FearSources(actor);
            if (sources == null || candidates.TrueForAll(unit => sources.Contains(unit.Id))) return;
            candidates.RemoveAll(unit => sources.Contains(unit.Id));
        }

        private static void DeprioritizeFearSources(FightBattleUnitState actor,
            List<FightBattleUnitState> candidates, int targetType)
        {
            if (targetType != 1 || candidates.Count < 2 || actor.Statuses == null) return;
            var sources = FearSources(actor);
            if (sources == null) return;
            var avoided = new List<FightBattleUnitState>();
            for (int index = 0; index < candidates.Count;)
            {
                if (!sources.Contains(candidates[index].Id)) { index++; continue; }
                avoided.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            candidates.AddRange(avoided);
        }

        private static HashSet<int> FearSources(FightBattleUnitState actor)
        {
            if (actor.Statuses == null || actor.Statuses.Count == 0) return null;
            HashSet<int> result = null;
            foreach (var status in actor.Statuses)
                if (status != null && status.ActionType == FearActionType
                    && status.ForbiddenType == 0 && status.CreatorId > 0)
                {
                    if (result == null) result = new HashSet<int>();
                    result.Add(status.CreatorId);
                }
            return result;
        }
    }
}
