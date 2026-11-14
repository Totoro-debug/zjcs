using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // The user confirmed 10563/25030 follows the skill description: damage
        // must break the last shield. Expiry, dispelling and manual removal do
        // not trigger. A remaining forbidden shield still prevents this event.
        internal static int CollectRippleImpact(FightBattleUnitState owner,
            FightBattleSkillDefinition incomingSkill, FightBattleHitResult hit,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            List<FightBattleStatusSkillInvocation> configured, float delay)
        {
            if (owner == null || owner.Hp <= 0 || owner.Ended || owner.Statuses == null
                || hit == null || hit.IsCure || hit.ShieldDamage <= 0
                || hit.BrokenShields == null || !hit.BrokenShields.Any(value => value != null && value.ActionType == 60)
                || owner.Statuses.Any(value => value != null && value.ActionType == 60)) return 0;
            int count = 0;
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
            {
                if (status == null || status.ClassId != 25030 || status.ForbiddenType != 0
                    || !status.HasActionEndTrigger || !status.ActionEndRulesSupported
                    || !FightSkillTriggerPolicy.AllowsStatus(incomingSkill, status)) continue;
                var trigger = status.ActionEndSkills.Count == 1 ? status.ActionEndSkills[0] : null;
                if (!status.ActionEndTargetTypeAllEnd || !status.ActionEndActionTypes.SequenceEqual(new[] { 60 })
                    || status.ActionEndRate != 1f || trigger == null || trigger.EntityId != 11330
                    || trigger.Source != 2 || trigger.Target != 2 || trigger.TargetPos != 1
                    || trigger.BasePercent != 1f || trigger.AffectedByProp || trigger.UseAI
                    || trigger.StackedCountToDamageFactor)
                {
                    status.ActionEndRulesSupported = false;
                    continue;
                }
                int start = configured.Count;
                if (!AppendThresholdSkill(owner, status, trigger, random, configured, null, units)) continue;
                for (int i = start; i < configured.Count; i++) configured[i].Delay = delay;
                status.ActionEndTriggeredCount++;
                count++;
            }
            return count;
        }
    }
}
