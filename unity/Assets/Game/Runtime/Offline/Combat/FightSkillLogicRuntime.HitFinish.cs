using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightSkillLogicRuntime
    {
        private interface IFightSkillHitFinish
        {
            void CaptureStart(FightSkillLogicRuntime context);
            List<ChildSkillRequest> Finish(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, decimal removedHp, float delay);
        }

        private readonly IFightSkillHitFinish hitFinishSource;

        internal List<ChildSkillRequest> CollectHitFinishSkills(FightBattleHitDefinition hit,
            decimal removedHp, float delay)
        {
            if (!skill.CanTriggerChild || hitFinishSource == null)
                return new List<ChildSkillRequest>();
            return hitFinishSource.Finish(this, hit, removedHp, delay);
        }

        private sealed class HemophagiaHitFinish : IFightSkillHitFinish
        {
            private const int CounterKey = 2081371075;
            private bool captured;
            private int childId, scale;

            public void CaptureStart(FightSkillLogicRuntime context)
            {
                if (captured) return;
                captured = true;
                var condition = Object(context.startFlow, "FilterCondition");
                var startHandles = Array(context.startFlow, "HandleCfgs");
                var ids = Array(condition, "StatusClassIds");
                var finishCondition = Object(context.finishFlow, "FilterCondition");
                var finishHandles = Array(context.finishFlow, "HandleCfgs");
                if (String(condition, "_type") != "EntityHasAnyStatusByEntityKeyFilter"
                    || !IsPort(condition, "EntityKey", 1, false)
                    || ids.Count != 1 || Convert.ToInt32(ids[0]) != 24721
                    || startHandles.Count != 1 || !(startHandles[0] is Dictionary<string, object> reset)
                    || String(reset, "_type") != "FightHandleCfgCounterReset"
                    || !IsPort(reset, "CounterBBKey", CounterKey, false) || Int(reset, "ResetValue") != 1
                    || String(finishCondition, "_type") != "CounterGreaterEqualFilter"
                    || !IsPort(finishCondition, "CounterBBKey", CounterKey, true)
                    || Int(finishCondition, "Value") != 1
                    || finishHandles.Count != 1 || !(finishHandles[0] is Dictionary<string, object> trigger)
                    || String(trigger, "_type") != "FightHandleCfgTriggerHemophagiaSkill"
                    || !IsPort(trigger, "SourceEntityKey", 1, false)
                    || !IsPort(trigger, "SkillEntityKey", 4, false)
                    || !IsPort(trigger, "ParentEntityKey", 2001, false)
                    || !IsPort(trigger, "DamageKey", 1026, false)
                    || Int(trigger, "ScaleValue") != 5000 || Int(trigger, "SkillId") != 13965) return;
                childId = Int(trigger, "SkillId"); scale = Int(trigger, "ScaleValue");
                foreach (var status in context.actor.Statuses)
                    if (status != null && status.ClassId == 24721
                        && status.ForbiddenType == 0 && status.StackCount > 0)
                    {
                        // CounterReset's official contract writes the persistent
                        // blackboard even though this output port carries false.
                        context.persistentBlackboard[CounterKey] = Int(reset, "ResetValue");
                        break;
                    }
            }

            public List<ChildSkillRequest> Finish(FightSkillLogicRuntime context,
                FightBattleHitDefinition hit, decimal removedHp, float delay)
            {
                var result = new List<ChildSkillRequest>();
                if (!captured || childId == 0 || hit == null || hit.EntityId != 13961
                    || !context.persistentBlackboard.TryGetValue(CounterKey, out long count)
                    || count < 1 || removedHp <= 0m) return result;
                decimal cure = decimal.Truncate(removedHp * scale / 10000m);
                if (cure <= 0m) return result;
                var request = Request(childId, context.actor, Math.Max(0f, delay));
                request.HasCustomCure = true;
                request.CustomCure = cure >= long.MaxValue ? long.MaxValue : (long)cure;
                result.Add(request);
                return result;
            }
        }
    }
}
