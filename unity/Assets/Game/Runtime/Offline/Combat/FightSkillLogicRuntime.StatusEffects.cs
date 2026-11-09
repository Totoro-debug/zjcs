using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        private interface IFightSkillStartEffect
        {
            bool Apply(FightSkillLogicRuntime context, FightBattleUnitState target);
        }

        private interface IFightSkillTransientEffect
        {
            bool Apply(FightSkillLogicRuntime context, FightBattleUnitState target,
                FightBattleHitDefinition hit, FightDamageTypeData type);
        }

        private readonly IFightSkillStartEffect startEffect;
        private readonly IFightSkillTransientEffect transientEffect;

        internal bool OnConfiguredSkillStart(FightBattleUnitState target)
            => startEffect != null && startEffect.Apply(this, target);

        internal bool OnConfiguredTransient(FightBattleUnitState target,
            FightBattleHitDefinition hit, FightDamageTypeData type)
            => transientEffect != null && transientEffect.Apply(this, target, hit, type);

        private sealed class MasterStartStatus : IFightSkillStartEffect
        {
            private bool handled;
            public bool Apply(FightSkillLogicRuntime context, FightBattleUnitState target)
            {
                if (handled || context.skill.EntityId != 50213 || !EmptyFilter(context.startFlow)
                    || target == null || context.actor.MasterId <= 0 || target.Id != context.actor.MasterId
                    || !FightBattleTargeting.IsSelectable(target)) return false;
                if (!TryReadExplicitStatus(context.startFlow, 2003, 24825, 1d, out _)) return false;
                handled = true;
                // The approved exception is the effect explicitly authored in this
                // skill's own graph; it grants no permission to downstream triggers.
                return context.ApplyExplicitStatus(target, 24825);
            }
        }

        private sealed class UnmarkedTargetStatus : IFightSkillTransientEffect
        {
            public bool Apply(FightSkillLogicRuntime context, FightBattleUnitState target,
                FightBattleHitDefinition hit, FightDamageTypeData type)
            {
                if (context.skill.EntityId != 64450 || hit == null || target == null
                    || target.Ended || target.Hp <= 0 || type.Dodge || type.Blinding) return false;
                var filter = Object(context.flow, "FilterCondition");
                var inner = Object(filter, "TargetFilter");
                if (String(filter, "_type") != "LogicNotFilter"
                    || String(inner, "_type") != "EntityStatusStackCountByEntityKeyFilter"
                    || !IsPort(inner, "EntityKey", 2002, false)
                    || Int(inner, "StatusId") != 24773 || Int(inner, "StackCount") != 1
                    || Int(inner, "Relation") != 1
                    || !TryReadExplicitStatus(context.flow, 2002, 24772, .5d, out double rate)) return false;
                // 24773 is a non-stacking original status. GE 1 means a live,
                // effective instance blocks this effect, including across sources.
                foreach (var status in target.Statuses)
                    if (status != null && status.ClassId == 24773 && status.ForbiddenType == 0
                        && status.StackCount >= 1) return false;
                if (context.random != null && context.random.Next() > rate) return false;
                return context.ApplyExplicitStatus(target, 24772);
            }
        }

        private static bool TryReadExplicitStatus(Dictionary<string, object> flow,
            int targetPort, int statusId, double rate, out double configuredRate)
        {
            configuredRate = 0d;
            var handles = Array(flow, "HandleCfgs");
            if (handles.Count != 1 || !(handles[0] is Dictionary<string, object> handle)
                || String(handle, "_type") != "FightHandleCfgApplyStatussBySkill"
                || !IsPort(handle, "SkillEntityKey", 4, false)
                || !IsPort(handle, "SourceEntityKey", 1, false)
                || !IsPort(handle, "TargetEntityKey", targetPort, false)) return false;
            var statuses = Array(handle, "TriggerStatusCfgs");
            if (statuses.Count != 1 || !(statuses[0] is Dictionary<string, object> status)
                || Int(status, "StatusId") != statusId || Bool(status, "AffectedByProp")
                || !status.TryGetValue("BasePercent", out object rawRate)
                || Convert.ToDouble(rawRate) != rate) return false;
            // The official handler explicitly ignores ApplyTarget and other status
            // item fields; TargetEntityKey is its authoritative destination.
            configuredRate = Convert.ToDouble(rawRate);
            return true;
        }

        private bool ApplyExplicitStatus(FightBattleUnitState target, int statusId)
        {
            if (target == null || target.Hp <= 0 || target.Ended) return false;
            int instance = 1;
            foreach (var existing in target.Statuses)
                if (existing != null && existing.InstanceId >= instance) instance = existing.InstanceId + 1;
            var template = CreateStatus(statusId, instance);
            template.OwnerId = target.Id;
            template.RootSkillId = skill.RootEntityId > 0 ? skill.RootEntityId : skill.EntityId;
            var applied = FightStatusLifecycle.Apply(target, template, -1, instance, random);
            return applied != null;
        }
    }
}
