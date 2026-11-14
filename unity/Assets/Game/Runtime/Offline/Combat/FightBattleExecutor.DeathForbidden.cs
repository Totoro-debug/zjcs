using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // Runtime equivalent of FightStatusAgentComponent.IsApplicatorDie. Checkpoints
        // are settled boundaries, so restore derives this from HP without adding save data.
        private readonly HashSet<int> confirmedDeadUnits = new HashSet<int>();

        private bool SynchronizeDeathForbidden(FightBattleUnitState owner)
        {
            if (owner == null) return false;
            bool changed = false;
            bool dead = owner.Hp <= 0;
            RecordUnitLife(owner, dead);
            if (dead) confirmedDeadUnits.Add(owner.Id);
            else confirmedDeadUnits.Remove(owner.Id);
            // The original agent snapshots its statuses before changing forbidden bits.
            // Release callbacks may add/remove statuses or execute another skill.
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
            {
                if (confirmedDeadUnits.Contains(owner.Id) != dead) break;
                changed |= FightStatusLifecycle.SetForbidden(owner, status,
                    FightStatusLifecycle.ForbiddenApplicatorDie, dead);
            }
            return changed;
        }

        private void InitializeStatusDeathForbidden(FightBattleUnitState owner,
            FightBattleStatusState status)
        {
            // BackToLife sets positive HP before OnStatusStart. Clear the old death
            // reason before the new status's startup consumers run. Zero HP alone is
            // insufficient here: a hit's HP triggers/instant revival are not settled yet.
            if (owner.Hp > 0 && confirmedDeadUnits.Contains(owner.Id))
                SynchronizeDeathForbidden(owner);
            FightStatusLifecycle.SetForbidden(owner, status,
                FightStatusLifecycle.ForbiddenApplicatorDie, confirmedDeadUnits.Contains(owner.Id));
        }
    }
}
