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
        private const int StatusTriggerTargetCreator = 3;

        public static List<FightBattleStatusStateTrigger> CollectRoundStartSkills(
            FightBattleUnitState source, FightBattleUnitState listener,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random)
        {
            var result = new List<FightBattleStatusStateTrigger>();
            if (source == null || listener == null || source.Statuses == null) return result;
            foreach (FightBattleStatusState status in source.Statuses)
            {
                if (status == null || !status.HasRoundStartTrigger
                    || !status.RoundStartRulesSupported || status.RoundStartTryAtStart
                    || status.RoundStartDelayRound != 0 || status.RoundStartMeetCount != 1
                    || (status.RoundStartMaxCount >= 0
                        && status.RoundStartTriggeredCount >= status.RoundStartMaxCount)
                    || FightStatusLifecycle.ResolveTargetUnitId(status,
                        status.RoundStartListenTarget, units) != listener.Id
                    || !Roll(status.RoundStartRate, random)) continue;

                int added = 0;
                foreach (FightBattleStatusStateTrigger trigger in status.RoundStartSkills)
                {
                    if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                        || trigger.Source != 2 || trigger.Target != 2 || trigger.UseAI
                        || trigger.StackedCountToDamageFactor
                        || (trigger.TargetPos != 0 && trigger.TargetPos != 1)
                        || (trigger.TargetPos == 1 && source.Id != listener.Id)
                        || !Roll(trigger.BasePercent, random)) continue;
                    result.Add(trigger);
                    added++;
                }
                if (added > 0) status.RoundStartTriggeredCount++;
            }
            return result;
        }

        public static List<FightBattleStatusSkillInvocation> CollectRoundEndSkills(
            FightBattleUnitState listener, IReadOnlyList<FightBattleUnitState> units,
            FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (listener == null || units == null || !FightBattleTargeting.IsSelectable(listener))
                return result;
            foreach (FightBattleUnitState statusOwner in units)
            {
                if (statusOwner == null || !FightBattleTargeting.IsSelectable(statusOwner)
                    || statusOwner.Statuses == null) continue;
                foreach (FightBattleStatusState status in statusOwner.Statuses)
                {
                    if (status == null || !status.HasRoundEndTrigger
                        || !status.RoundEndRulesSupported
                        || (status.RoundEndMaxCount >= 0
                            && status.RoundEndTriggeredCount >= status.RoundEndMaxCount)
                        || FightStatusLifecycle.ResolveTargetUnitId(status,
                            status.RoundEndListenTarget, units) != listener.Id) continue;

                    int added = 0;
                    foreach (FightBattleStatusStateTrigger trigger in status.RoundEndSkills)
                    {
                        if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                            || trigger.Source != 2 || trigger.Target != 5 || trigger.UseAI
                            || trigger.TargetPos != 1 || trigger.StackedCountToDamageFactor
                            || !Roll(trigger.BasePercent, random)) continue;
                        result.Add(new FightBattleStatusSkillInvocation
                        {
                            Trigger = trigger,
                            SourceUnitId = status.OwnerId,
                            TargetUnitId = listener.Id,
                            TargetPositionUnitId = listener.Id
                        });
                        added++;
                    }
                    if (added > 0) status.RoundEndTriggeredCount++;
                }
            }
            return result;
        }

        public static List<FightBattleStatusSkillInvocation> CollectCooldownModifySkills(
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (units == null) return result;
            foreach (FightBattleUnitState owner in units)
            {
                if (owner == null || !FightBattleTargeting.IsSelectable(owner)
                    || owner.Statuses == null) continue;
                foreach (FightBattleStatusState status in owner.Statuses)
                {
                    if (status == null || !status.SkillCooldownModifySkillsPending) continue;
                    status.SkillCooldownModifySkillsPending = false;
                    foreach (FightBattleStatusStateTrigger trigger in status.SkillCooldownModifySkills)
                    {
                        if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                            || trigger.Source != 2 || trigger.Target != 2 || trigger.UseAI
                            || trigger.TargetPos != 1 || trigger.StackedCountToDamageFactor
                            || !Roll(trigger.BasePercent, random)) continue;
                        result.Add(new FightBattleStatusSkillInvocation
                        {
                            Trigger = trigger,
                            SourceUnitId = owner.Id,
                            TargetUnitId = owner.Id,
                            TargetPositionUnitId = owner.Id
                        });
                    }
                }
            }
            return result;
        }

        public static List<FightBattleStatusSkillInvocation> CollectSkillStartSkills(
            FightBattleUnitState actor, FightBattleSkillDefinition skill,
            IReadOnlyList<FightBattleUnitState> skillTargets,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (actor == null || skill == null || skillTargets == null || skillTargets.Count == 0
                || actor.Statuses == null || !FightBattleTargeting.IsSelectable(actor)) return result;
            foreach (FightBattleStatusState status in actor.Statuses)
            {
                if (status == null || !status.HasSkillStartTrigger
                    || !status.SkillStartRulesSupported || status.SkillStartSource != 2
                    || (status.SkillStartMaxCount >= 0
                        && status.SkillStartTriggeredCount >= status.SkillStartMaxCount)
                    || FightStatusLifecycle.ResolveTargetUnitId(status,
                        status.SkillStartSource, units) != actor.Id
                    || status.SkillStartActionTypes == null
                    || !status.SkillStartActionTypes.Contains(skill.ActionType)) continue;

                int sourceId = FightStatusLifecycle.ResolveTargetUnitId(status,
                    FightStatusLifecycle.TargetCreator, units);
                int targetId = FightStatusLifecycle.ResolveTargetUnitId(status,
                    FightStatusLifecycle.TargetApplicator, units);
                if (sourceId == 0 || targetId == 0) continue;
                FightBattleUnitState targetPosition = null;
                foreach (FightBattleUnitState candidate in skillTargets)
                    if (candidate != null && FightBattleTargeting.IsSelectable(candidate))
                    {
                        targetPosition = candidate;
                        break;
                    }
                if (targetPosition == null) continue;

                int added = 0;
                foreach (FightBattleStatusStateTrigger trigger in status.SkillStartSkills)
                {
                    if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                        || trigger.Source != 3 || trigger.Target != 2 || trigger.UseAI
                        || trigger.TargetPos != 1 || trigger.StackedCountToDamageFactor
                        || !Roll(trigger.BasePercent, random)) continue;
                    result.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = trigger,
                        SourceUnitId = sourceId,
                        TargetUnitId = targetId,
                        TargetPositionUnitId = targetPosition.Id
                    });
                    added++;
                }
                if (added > 0) status.SkillStartTriggeredCount++;
            }
            return result;
        }

        public static int ApplySkillEndStatuses(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (actor == null || skill == null || units == null || catalog == null
                || actor.Statuses == null || !FightBattleTargeting.IsSelectable(actor)) return 0;
            var statuses = new List<FightBattleStatusState>(actor.Statuses);
            int applied = 0;
            foreach (FightBattleStatusState status in statuses)
            {
                if (status == null || !status.HasSkillEndTrigger || !status.SkillEndRulesSupported
                    || status.SkillEndFilteredActionTypes.Contains(skill.ActionType)) continue;
                status.SkillEndMetCountProgress++;
                if (status.SkillEndMetCountProgress < Math.Max(1, status.SkillEndMetCount)) continue;
                status.SkillEndMetCountProgress = 0;
                status.SkillEndTriggeredCount++;
                foreach (FightBattleStatusStateTrigger trigger in status.SkillEndStatuses)
                {
                    if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                        || (trigger.Target != FightStatusLifecycle.TargetApplicator
                            && trigger.Target != StatusTriggerTargetCreator)
                        || !Roll(trigger.BasePercent, random)) continue;
                    int lifecycleTarget = trigger.Target == FightStatusLifecycle.TargetApplicator
                        ? FightStatusLifecycle.TargetApplicator
                        : FightStatusLifecycle.TargetCreator;
                    int targetId = FightStatusLifecycle.ResolveTargetUnitId(status,
                        lifecycleTarget, units);
                    FightBattleUnitState target = FindUnit(units, targetId);
                    if (target == null || !FightBattleTargeting.IsSelectable(target)) continue;
                    int instanceId = 1;
                    foreach (FightBattleStatusState existing in target.Statuses)
                        if (existing != null && existing.InstanceId >= instanceId)
                            instanceId = existing.InstanceId + 1;
                    FightBattleStatusState template = catalog.CreateStatus(trigger.EntityId,
                        target.Id, actor.Id, instanceId, skill.EntityId, skill.ItemId,
                        skill.Rank, skill.Level, skill.SubRank, actor.Properties);
                    FightStatusLifecycle.Apply(target, template, -1, instanceId, random);
                    applied++;
                }
            }
            return applied;
        }

        private static FightBattleUnitState FindUnit(
            IReadOnlyList<FightBattleUnitState> units, int unitId)
        {
            if (units == null || unitId <= 0) return null;
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.Id == unitId) return unit;
            return null;
        }

        public static int Apply(FightBattleUnitState unit, FightOfficialCombatCatalog catalog,
            bool atStart, List<int> triggerSkills = null, FightRandomStream random = null)
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
                    FightStatusLifecycle.Apply(unit, template, -1, instanceId, random);
                }
                if (triggerSkills != null && status.HpTriggerSkills != null)
                    foreach (int skillId in status.HpTriggerSkills)
                        if (skillId > 0) triggerSkills.Add(skillId);
                status.HpTriggeredCount++;
                applied++;
            }
            return applied;
        }

        /// <summary>
        /// Official <c>FightStatusHpAccDecreaseComponent</c> local rule: accumulate actual HP lost,
        /// never reduce the accumulator for healing, and consume one configured threshold per trigger.
        /// </summary>
        public static int ApplyAccumulatedDamage(FightBattleUnitState unit, long actualHpDamage,
            FightOfficialCombatCatalog catalog, FightRandomStream random,
            List<FightBattleStatusStateTrigger> triggerSkills = null)
        {
            if (unit == null || catalog == null || actualHpDamage <= 0 || unit.MaxHp <= 0
                || unit.Statuses == null) return 0;
            int triggered = 0;
            int initialStatusCount = unit.Statuses.Count;
            for (int i = initialStatusCount - 1; i >= 0; i--)
            {
                FightBattleStatusState status = unit.Statuses[i];
                if (status == null || !status.HasHpAccDecrease) continue;
                long threshold = Threshold(status, unit.MaxHp);
                if (threshold <= 0) continue;
                if (status.HpAccMaxCount >= 0
                    && status.HpAccTriggeredCount >= status.HpAccMaxCount) continue;
                status.HpAccDamageAccumulated = actualHpDamage > long.MaxValue
                    - status.HpAccDamageAccumulated
                    ? long.MaxValue : status.HpAccDamageAccumulated + actualHpDamage;
                while (status.HpAccDamageAccumulated >= threshold
                    && (status.HpAccMaxCount < 0
                        || status.HpAccTriggeredCount < status.HpAccMaxCount))
                {
                    status.HpAccDamageAccumulated -= threshold;
                    status.HpAccTriggeredCount++;
                    triggered++;
                    ApplyConfiguredStatuses(unit, status, catalog, random);
                    AppendConfiguredSkills(status, triggerSkills, random);
                }
            }
            return triggered;
        }

        private static long Threshold(FightBattleStatusState status, long maxHp)
        {
            if (status.HpAccUnitHp >= 0) return Math.Max(1L, status.HpAccUnitHp);
            if (status.HpAccUnitHpPercentage <= 0f) return 0;
            double percentage = Math.Round(status.HpAccUnitHpPercentage, 7,
                MidpointRounding.AwayFromZero);
            double amount = maxHp * percentage;
            return amount >= long.MaxValue ? long.MaxValue : Math.Max(1L, (long)Math.Ceiling(amount));
        }

        private static void ApplyConfiguredStatuses(FightBattleUnitState unit,
            FightBattleStatusState source, FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (source.HpAccTriggerStatuses == null) return;
            foreach (FightBattleStatusStateTrigger trigger in source.HpAccTriggerStatuses)
            {
                if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                    || trigger.Target != 2 || !Roll(trigger.BasePercent, random)) continue;
                int instanceId = 1;
                foreach (FightBattleStatusState existing in unit.Statuses)
                    if (existing != null && existing.InstanceId >= instanceId)
                        instanceId = existing.InstanceId + 1;
                FightBattleStatusState template = catalog.CreateStatus(trigger.EntityId, unit.Id,
                    unit.Id, instanceId);
                FightStatusLifecycle.Apply(unit, template, -1, instanceId, random);
            }
        }

        private static void AppendConfiguredSkills(FightBattleStatusState status,
            List<FightBattleStatusStateTrigger> output, FightRandomStream random)
        {
            if (output == null || status.HpAccTriggerSkills == null) return;
            foreach (FightBattleStatusStateTrigger trigger in status.HpAccTriggerSkills)
            {
                if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                    || trigger.Source != 2 || trigger.Target != 2 || trigger.UseAI
                    || trigger.StackedCountToDamageFactor || !Roll(trigger.BasePercent, random)) continue;
                output.Add(trigger);
            }
        }

        private static bool Roll(float chance, FightRandomStream random)
        {
            if (chance >= 1f) return true;
            if (chance <= 0f || random == null) return false;
            return random.Next() < chance;
        }
    }
}
