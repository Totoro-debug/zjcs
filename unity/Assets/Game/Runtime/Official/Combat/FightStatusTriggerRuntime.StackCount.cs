using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // StatusStackCountComponentInfo specifies the layer threshold, trigger
        // quota and removal condition; effect application uses the shared rules.
        public static void ApplyStackCount(FightBattleUnitState owner,
            FightBattleStatusState status, FightOfficialCombatCatalog catalog,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            System.Action<List<FightBattleStatusSkillInvocation>> executeSkills = null)
        {
            if (owner == null || status == null || catalog == null || !owner.Statuses.Contains(status)
                || !status.HasStackCountTrigger || !status.StackCountRulesSupported
                || status.ForbiddenType != 0 || status.StackCount < status.StackCountThreshold) return;
            // Do not consume a skill-bearing threshold without an execution context.
            if (status.StackCountSkills.Count > 0 && executeSkills == null) return;
            if (status.StackCountMaxTriggers >= 0 && status.StackCountTriggered >= status.StackCountMaxTriggers)
            {
                if (status.StackCountRemoveOnMax) FightStatusLifecycle.End(owner, status);
                return;
            }
            // Reserve the quota before nested status-start callbacks.
            status.StackCountTriggered++;
            foreach (var trigger in status.StackCountStatuses)
                ApplyThresholdStatus(owner, status, trigger, catalog, units, random);
            var skills = new List<FightBattleStatusSkillInvocation>();
            foreach (var trigger in status.StackCountSkills)
            {
                if ((trigger.Source == 3 || trigger.Target == 3)
                    && FindUnit(units, status.CreatorId) == null) continue;
                // StatusTriggerTarget.Creator (3) is distinct from the round
                // clock enum whose value 3 means CreatorMaster.
                int before = skills.Count;
                if (!AppendThresholdSkill(owner, status, trigger, random, skills, null, units)) continue;
                var invocation = skills[before];
                invocation.SourceUnitId = trigger.Source == 3 ? status.CreatorId : owner.Id;
                invocation.TargetUnitId = trigger.Target == 3 ? status.CreatorId : owner.Id;
                invocation.TargetPositionUnitId = trigger.TargetPos == 1 ? invocation.TargetUnitId : owner.Id;
            }
            if (skills.Count > 0) executeSkills(skills);
            if (status.StackCountRemoveOnMax && status.StackCountMaxTriggers >= 0
                && status.StackCountTriggered >= status.StackCountMaxTriggers)
                FightStatusLifecycle.End(owner, status);
        }
    }
}
