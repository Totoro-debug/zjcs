using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void ExecuteOwnStatusEndActions(FightBattleUnitState owner, FightBattleStatusState ended)
        {
            var context = movementStartContext;
            if (context == null || owner == null || ended == null || statusCatalog == null
                || !statusCatalog.TryReadOwnStatusEndActions(ended.ClassId, out bool checkAlive,
                    out var skills, out var statuses) || checkAlive && !owner.IsAlive) return;
            ApplyComponentStatuses(ended, statuses, owner, owner);
            var invocations = new List<FightBattleStatusSkillInvocation>();
            foreach (var trigger in skills)
                AppendComponentInvocation(ended, trigger, owner, owner, invocations, true);
            foreach (var invocation in invocations) invocation.AllowDeadSource = !checkAlive;
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, context.Casts, context.DamageEvents,
                context.Movements, ref delay, out float duration, out float actionDuration);
            context.Duration = Math.Max(context.Duration, Math.Max(delay, duration));
            context.ActionDuration = Math.Max(context.ActionDuration, actionDuration);
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        internal bool TryReadOwnStatusEndActions(int classId, out bool checkAlive,
            out List<FightBattleStatusStateTrigger> skills, out List<FightBattleStatusStateTrigger> statuses)
        {
            checkAlive = false;
            skills = new List<FightBattleStatusStateTrigger>();
            statuses = new List<FightBattleStatusStateTrigger>();
            if (!entities.TryGetValue(classId, out var entity)) return false;
            var component = Component(entity, "StatusEndComponent");
            if (component == null || Bool(component, "NotStatusMutex")) return false;
            var autoRemove = Component(component, "AutoRemove");
            var forbidden = Component(component, "ForbiddenStatus");
            if (autoRemove != null && (Float(autoRemove, "Duration", -1f) >= 0f
                    || Int(autoRemove, "MaxCount", -1) >= 0 || ArrayCount(autoRemove, "RemoveStatusCfgs") > 0)
                || forbidden != null && Int(forbidden, "StatusEntityClassId") != 0) return false;
            checkAlive = Bool(component, "CheckApplyAlive");
            AddHpAccTriggerEntries(component, "StatusTriggerSkillCfgs", "SkillId", skills, true);
            AddHpAccTriggerEntries(component, "StatusTriggerStatusCfgs", "StatusId", statuses, false);
            // These direct end actions have no hit source/target context. Preserve
            // explicit owner/creator selection and reject a hit-dependent query.
            if (!skills.TrueForAll(trigger => trigger.Source >= 1 && trigger.Source <= 4
                && trigger.Target >= 1 && trigger.Target <= 4 && !trigger.UseAI
                && trigger.PropTarget == null && !trigger.AffectedByProp)) return false;
            return skills.Count > 0 || statuses.Count > 0;
        }
    }
}
