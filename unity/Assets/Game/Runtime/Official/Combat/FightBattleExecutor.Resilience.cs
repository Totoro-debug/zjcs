using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private FightBattleResilienceDamageContext activeResilienceDamageContext;

        private sealed class FightBattleResilienceDamageContext
        {
            public FightBattleUnitState Owner;
            public FightBattleUnitState Breaker;
            public FightBattleResilienceInfo Info;
        }

        private void InitializeUnitResilience(FightBattleUnitState unit)
        {
            if (unit == null || unit.MonsterId <= 0 || unit.MaxResilience > 0) return;
            long maxResilience = GetProperty(unit, formula.PropertyId("Resilience"));
            if (maxResilience <= 0) return;
            unit.MaxResilience = maxResilience;
            if (unit.BrokenRound < 0 && unit.Resilience <= 0)
                unit.Resilience = maxResilience;
        }

        private void ApplyResilienceDamageOverride(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit)
        {
            FightBattleResilienceDamageContext context = activeResilienceDamageContext;
            if (context == null || actor.Id != context.Owner.Id || skill == null || hit == null
                || skill.EntityId != context.Info.DisappearSkillId) return;
            hit.HasCustomDamage = true;
            hit.CustomDamage = FightResilienceFormula.CalculateBrokenDamage(formula,
                target.Properties, context.Breaker.Properties,
                context.Info.SkillMaxHpPercent, context.Info.SkillAtkPercent);
        }

        private void ApplyResilienceHit(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, FightBattleHitResult resolved,
            List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (!hit.BreakResilience || target.Hp <= 0 || target.MaxResilience <= 0
                || target.Resilience <= 0 || resolved.Type.Dodge || resolved.Type.Immunity
                || resolved.Type.Blinding) return;

            int change = FightResilienceFormula.CalculateChange(formula,
                ToPropertyDictionary(actor.Properties), 1.0, hit.DamageParam);
            target.Resilience = Math.Max(0L, Math.Min(target.MaxResilience,
                target.Resilience + change));
            if (target.Resilience > 0) return;

            if (actor.Side != target.Side)
                checkpoint.Statistics.RecordResilienceBreak(actor.Id);

            FightBattleResilienceInfo info = statusCatalog == null ? null
                : statusCatalog.MonsterResilienceInfo(target.MonsterId,
                    target.MonsterGroupNo);
            if (info == null) return;
            target.BrokenRound = info.ResetRound > 0
                ? target.SkillState.Round + (actor.Id == target.Id ? 1 : 0) : -1;
            foreach (int statusId in info.DisappearStatusIds)
                ApplyResilienceStatus(actor, target, skill, statusId);
            if (info.DisappearSkillId > 0)
                ExecuteResilienceTriggerSkill(target, info, actor,
                    casts, damageEvents, movements, ref lastDelay);
        }

        private void ApplyResilienceStatus(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill, int statusId)
        {
            if (statusCatalog == null || statusId <= 0 || target.Hp <= 0) return;
            int instanceId = 1;
            foreach (FightBattleStatusState existing in target.Statuses)
                if (existing != null && existing.InstanceId >= instanceId)
                    instanceId = existing.InstanceId + 1;
            FightBattleStatusState template = statusCatalog.CreateStatus(statusId,
                target.Id, actor.Id, instanceId, skill == null ? 0 : skill.EntityId,
                skill == null ? 0 : skill.ItemId, skill == null ? 1 : skill.Rank,
                skill == null ? 1 : skill.Level, skill == null ? actor.SubRank : skill.SubRank,
                actor.Properties);
            FightStatusLifecycle.Apply(target, template, -1, instanceId, random);
        }

        private void ExecuteResilienceTriggerSkill(FightBattleUnitState owner,
            FightBattleResilienceInfo info, FightBattleUnitState breaker,
            List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            FightBattleResilienceDamageContext previousContext = activeResilienceDamageContext;
            activeResilienceDamageContext = new FightBattleResilienceDamageContext
            {
                Owner = owner,
                Breaker = breaker,
                Info = info
            };
            try
            {
                ExecuteTriggerSkills(owner, new List<int> { info.DisappearSkillId },
                    casts, damageEvents, movements, ref lastDelay);
            }
            finally
            {
                activeResilienceDamageContext = previousContext;
            }
        }

        private void RestoreResilienceAfterPersonalRound(FightBattleUnitState unit,
            int actorRound)
        {
            if (unit == null || unit.BrokenRound < 0 || unit.Resilience > 0
                || statusCatalog == null) return;
            FightBattleResilienceInfo info = statusCatalog.MonsterResilienceInfo(
                unit.MonsterId, unit.MonsterGroupNo);
            if (info == null || info.ResetRound <= 0
                || actorRound - unit.BrokenRound < info.ResetRound) return;
            unit.Resilience = unit.MaxResilience;
            unit.BrokenRound = -1;
        }

        private static Dictionary<int, long> ToPropertyDictionary(
            IList<FightPropertyValue> properties)
        {
            var result = new Dictionary<int, long>();
            if (properties == null) return result;
            for (int i = 0; i < properties.Count; i++)
            {
                FightPropertyValue property = properties[i];
                if (property != null) result[property.Id] = property.Value;
            }
            return result;
        }
    }
}
