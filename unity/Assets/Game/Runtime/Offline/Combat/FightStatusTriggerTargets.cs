using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    // Common.StatusTriggerTarget is distinct from the status lifetime RoundTarget enum.
    public static class FightStatusTriggerTargets
    {
        public static FightBattleUnitState Resolve(FightBattleStatusState status, FightBattleUnitState applicator,
            FightBattleUnitState triggerSource, FightBattleUnitState triggerTarget, int targetType,
            IReadOnlyList<FightBattleUnitState> units, FightBattleUnitState defaultTarget)
        {
            switch (targetType)
            {
                case 0: return defaultTarget;
                case 1: return triggerSource;
                case 2: return applicator;
                case 3: return Find(status?.CreatorId ?? 0, triggerSource, triggerTarget, units);
                case 5: return triggerTarget;
                case 8: return Find(applicator?.MasterId ?? 0, triggerSource, triggerTarget, units);
                case 4:
                    return Friends(applicator, units).OrderBy(unit => Math.Abs(unit.X - applicator.X)
                        + Math.Abs(unit.Y - applicator.Y)).FirstOrDefault();
                case 7:
                    return Friends(applicator, units).Where(unit => unit.CharacterType == 1)
                        .OrderBy(unit => unit.Hp / (double)Math.Max(1L, unit.MaxHp)).FirstOrDefault();
                default: return null; // A status entity is not a character target.
            }
        }

        private static IEnumerable<FightBattleUnitState> Friends(FightBattleUnitState owner,
            IReadOnlyList<FightBattleUnitState> units) => owner == null || units == null
            ? Enumerable.Empty<FightBattleUnitState>() : units.Where(unit => unit != null && unit.Id != owner.Id
                && unit.Side == owner.Side && FightBattleTargeting.IsSelectable(unit));

        private static FightBattleUnitState Find(int id, FightBattleUnitState source, FightBattleUnitState target,
            IReadOnlyList<FightBattleUnitState> units) => id == 0 ? null : source?.Id == id ? source
                : target?.Id == id ? target : units?.FirstOrDefault(unit => unit != null && unit.Id == id);
    }
}
