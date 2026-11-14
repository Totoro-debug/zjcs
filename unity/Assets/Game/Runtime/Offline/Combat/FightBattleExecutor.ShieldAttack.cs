namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RefreshShieldAttackPresence(FightBattleUnitState owner,
            FightBattleStatusState changedStatus = null)
        {
            if (statusCatalog == null || owner == null || applyingActionExistStatuses
                || changedStatus != null && changedStatus.ClassId != 25020 && changedStatus.ActionType != 60
                || !owner.Statuses.Exists(status => status != null && (status.ClassId == 25020
                    || status.ClassId == 24212 && status.IsActionExistGenerated))) return;

            // Confirmed offline presence rule: an action-60 status still counts while
            // forbidden. Its removal, rather than its remaining value, ends the bonus.
            // Keep the existing definition, original cultivation and generated provenance.
            int changed;
            applyingActionExistStatuses = true;
            try
            {
                changed = FightStatusTriggerRuntime.ApplyActionExistStatuses(
                    new[] { owner }, statusCatalog, random, 25020, 24212);
            }
            finally { applyingActionExistStatuses = false; }
            if (changed != 0) FightStatusPropertyRuntime.RebuildAll(units, statusCatalog, formula);
        }
    }
}
