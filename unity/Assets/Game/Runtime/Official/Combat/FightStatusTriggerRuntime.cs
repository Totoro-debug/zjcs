using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Official <c>FightStatusHpDecreaseUnitComponent</c>: while the owner HP ratio stays at or below
    /// <c>UnitHpPer</c>, the configured statuses are applied again, up to <c>MaxCount</c> times. Only
    /// the status list is executed; the component's trigger skills are not part of this pass.
    /// </summary>
    public static class FightStatusTriggerRuntime
    {
        public static int Apply(FightBattleUnitState unit, FightOfficialCombatCatalog catalog,
            bool atStart, List<int> triggerSkills = null)
        {
            if (unit == null || catalog == null || unit.Statuses == null || unit.MaxHp <= 0) return 0;
            int applied = 0;
            for (int i = unit.Statuses.Count - 1; i >= 0; i--)
            {
                FightBattleStatusState status = unit.Statuses[i];
                if (status == null || status.HpTriggerPer <= 0f) continue;
                bool hasStatuses = status.HpTriggerStatuses != null && status.HpTriggerStatuses.Count > 0;
                bool hasSkills = status.HpTriggerSkills != null && status.HpTriggerSkills.Count > 0;
                if (!hasStatuses && !hasSkills) continue;
                if (atStart && !status.HpTriggerAtStart) continue;
                if (status.HpTriggerMaxCount >= 0 && status.HpTriggeredCount >= status.HpTriggerMaxCount)
                    continue;
                if (unit.Hp / (double)unit.MaxHp > status.HpTriggerPer) continue;
                foreach (int statusId in status.HpTriggerStatuses ?? new List<int>())
                {
                    if (statusId <= 0) continue;
                    int instanceId = 1;
                    foreach (FightBattleStatusState existing in unit.Statuses)
                        if (existing != null && existing.InstanceId >= instanceId)
                            instanceId = existing.InstanceId + 1;
                    FightBattleStatusState template = catalog.CreateStatus(statusId, unit.Id,
                        status.CreatorId <= 0 ? unit.Id : status.CreatorId, instanceId);
                    FightStatusLifecycle.Apply(unit, template, -1, instanceId);
                }
                if (triggerSkills != null && status.HpTriggerSkills != null)
                    foreach (int skillId in status.HpTriggerSkills)
                        if (skillId > 0) triggerSkills.Add(skillId);
                status.HpTriggeredCount++;
                applied++;
            }
            return applied;
        }
    }
}
