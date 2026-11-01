using System;
using System.Collections.Generic;
using System.Linq;

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
                    result.Add(trigger.WithSource(status));
                    added++;
                }
                if (added > 0) status.RoundStartTriggeredCount++;
            }
            return result;
        }

        public static int ApplyRoundStartStatuses(FightBattleStatusState source,
            FightBattleUnitState listener, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (source == null || listener == null || units == null || catalog == null
                || !source.HasRoundStartTrigger || !source.RoundStartRulesSupported
                || source.RoundStartStatuses == null
                || (source.RoundStartMaxCount >= 0
                    && source.RoundStartTriggeredCount >= source.RoundStartMaxCount)
                || FightStatusLifecycle.ResolveTargetUnitId(source,
                    source.RoundStartListenTarget, units) != listener.Id) return 0;
            FightBattleUnitState target = units.FirstOrDefault(unit => unit != null
                && unit.Id == source.OwnerId);
            int applied = ApplyStatusTriggers(target, source, source.RoundStartStatuses,
                catalog, random);
            if (applied > 0) source.RoundStartTriggeredCount++;
            return applied;
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
                            Trigger = trigger.WithSource(status),
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
                            Trigger = trigger.WithSource(status),
                            SourceUnitId = owner.Id,
                            TargetUnitId = owner.Id,
                            TargetPositionUnitId = owner.Id
                        });
                    }
                }
            }
            return result;
        }

        public static List<FightBattleStatusSkillInvocation> CollectRoundCheckSkills(
            FightBattleUnitState listener, IReadOnlyList<FightBattleUnitState> units,
            FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (listener == null || units == null || listener.Statuses == null) return result;
            foreach (FightBattleUnitState owner in units)
            {
                if (owner == null || owner.Statuses == null) continue;
                foreach (FightBattleStatusState status in owner.Statuses)
                {
                    if (status == null || !status.HasRoundCheckSkill
                        || !status.RoundCheckSkillRulesSupported
                        || FightStatusLifecycle.ResolveTargetUnitId(status,
                            status.RoundCheckSkillListenTarget, units) != listener.Id) continue;
                    foreach (FightBattleStatusStateTrigger trigger in status.RoundCheckSkillSkills)
                    {
                        if (trigger == null || trigger.EntityId <= 0 || !Roll(trigger.BasePercent, random))
                            continue;
                        result.Add(new FightBattleStatusSkillInvocation
                        {
                            Trigger = trigger.WithSource(status),
                            SourceUnitId = owner.Id,
                            TargetUnitId = owner.Id,
                            TargetPositionUnitId = owner.Id
                        });
                    }
                }
            }
            return result;
        }

        public static int ApplyRoleSummonCount(FightBattleUnitState owner,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random)
        {
            if (owner == null || units == null || catalog == null || owner.Statuses == null)
                return 0;
            int count = 0;
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.MasterId == owner.Id && unit.Hp > 0) count++;
            foreach (FightBattleStatusState sourceStatus in new List<FightBattleStatusState>(owner.Statuses))
            {
                if (sourceStatus == null || !sourceStatus.HasRoleSummonCount
                    || !sourceStatus.RoleSummonCountRulesSupported
                    || sourceStatus.RoleSummonCountStatus == null) continue;
                int desired = sourceStatus.RoleSummonCountLimit > 0
                    ? Math.Min(count, sourceStatus.RoleSummonCountLimit) : count;
                FightBattleStatusState existing = owner.Statuses.FirstOrDefault(status =>
                    status != null && status.ClassId == sourceStatus.RoleSummonCountStatus.EntityId
                    && status.CreatorId == owner.Id);
                if (desired <= 0)
                {
                    if (existing != null) FightStatusLifecycle.End(owner, existing);
                    sourceStatus.RoleSummonCountLastCount = 0;
                    continue;
                }
                if (sourceStatus.RoleSummonCountUpdateStack && existing != null
                    && existing.StackCount != desired)
                {
                    int previous = existing.StackCount;
                    existing.StackCount = desired;
                    FightStatusPropertyRuntime.OnStackChanged(existing, previous);
                    sourceStatus.RoleSummonCountLastCount = desired;
                    continue;
                }
                if (existing != null)
                {
                    sourceStatus.RoleSummonCountLastCount = desired;
                    continue;
                }
                int instanceId = owner.Statuses.Count == 0 ? 1
                    : owner.Statuses.Max(status => status == null ? 0 : status.InstanceId) + 1;
                FightBattleStatusState template = catalog.CreateStatus(
                    sourceStatus.RoleSummonCountStatus.EntityId, owner.Id, owner.Id, instanceId,
                    sourceStatus.RootSkillId, sourceStatus.ApplyingSkillItemId,
                    sourceStatus.SourceSkillRank, sourceStatus.SourceSkillLevel,
                    sourceStatus.SourceSubRank, owner.Properties, 0,
                    sourceStatus.ReplacementProperties, sourceStatus.BlessPropertyScales);
                if (sourceStatus.RoleSummonCountUpdateStack) template.StackCount = desired;
                template.ReportSource = FightBattleStatusReportSource.Resolve(sourceStatus).Copy();
                FightStatusLifecycle.Apply(owner, template, -1, instanceId, random);
                sourceStatus.RoleSummonCountLastCount = desired;
            }
            return count;
        }

        public static List<FightBattleStatusSkillInvocation> CollectRoleDieSkills(
            FightBattleUnitState dead, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (dead == null || units == null || catalog == null || dead.Hp > 0) return result;
            foreach (FightBattleUnitState owner in units)
            {
                if (owner == null || owner.Statuses == null || owner.Hp <= 0) continue;
                foreach (FightBattleStatusState status in new List<FightBattleStatusState>(owner.Statuses))
                {
                    if (status == null || !status.HasRoleDieTrigger || !status.RoleDieRulesSupported
                        || !status.RoleDieTryAtRemove
                        || status.RoleDieCharacterTypes.Count > 0
                            && !status.RoleDieCharacterTypes.Contains(dead.CharacterType)
                        || status.RoleDieSameFaction && owner.Side != dead.Side
                        || status.RoleDieUniqueRole && !status.RoleDieSeenUnitIds.Add(dead.Id)
                        || status.RoleDieMaxCount >= 0
                            && status.RoleDieTriggeredCount >= status.RoleDieMaxCount) continue;
                    status.RoleDieMeetProgress++;
                    int meet = Math.Max(1, status.RoleDieMeetCount);
                    if (status.RoleDieMeetProgress < meet) continue;
                    status.RoleDieMeetProgress = 0;
                    if (!Roll(1f, random)) continue;
                    int added = 0;
                    foreach (FightBattleStatusStateTrigger trigger in status.RoleDieSkills)
                    {
                        if (trigger == null || trigger.EntityId <= 0 || !Roll(trigger.BasePercent, random))
                            continue;
                        result.Add(new FightBattleStatusSkillInvocation
                        {
                            Trigger = trigger.WithSource(status),
                            SourceUnitId = owner.Id,
                            TargetUnitId = trigger.Target == 5 ? dead.Id : owner.Id,
                            TargetPositionUnitId = dead.Id,
                            AllowDeadTarget = trigger.Target == 5
                        });
                        added++;
                    }
                    if (ApplyRoleDieStatuses(owner, dead, status, catalog, random)) added++;
                    if (added > 0) status.RoleDieTriggeredCount++;
                }
            }
            return result;
        }

        private static bool ApplyRoleDieStatuses(FightBattleUnitState owner,
            FightBattleUnitState dead, FightBattleStatusState sourceStatus,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            bool applied = false;
            foreach (FightBattleStatusStateTrigger trigger in sourceStatus.RoleDieStatuses)
            {
                if (trigger == null || trigger.EntityId <= 0 || !Roll(trigger.BasePercent, random))
                    continue;
                FightBattleUnitState target = trigger.ApplyTarget == 5 ? dead : owner;
                if (target == null) continue;
                int instanceId = target.Statuses.Count == 0 ? 1
                    : target.Statuses.Max(status => status == null ? 0 : status.InstanceId) + 1;
                FightBattleStatusState template = catalog.CreateStatus(trigger.EntityId, target.Id,
                    owner.Id, instanceId, sourceStatus.RootSkillId, sourceStatus.ApplyingSkillItemId,
                    sourceStatus.SourceSkillRank, sourceStatus.SourceSkillLevel,
                    sourceStatus.SourceSubRank, owner.Properties, 0,
                    sourceStatus.ReplacementProperties, sourceStatus.BlessPropertyScales);
                template.ReportSource = FightBattleStatusReportSource.Resolve(sourceStatus).Copy();
                FightStatusLifecycle.Apply(target, template, -1, instanceId, random);
                applied = true;
            }
            return applied;
        }

        public static List<FightBattleStatusSkillInvocation> CollectSkillStartSkills(
            FightBattleUnitState actor, FightBattleSkillDefinition skill,
            IReadOnlyList<FightBattleUnitState> skillTargets,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            FightOfficialCombatCatalog catalog = null)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (actor == null || skill == null || skillTargets == null || skillTargets.Count == 0
                || actor.Statuses == null || !FightBattleTargeting.IsSelectable(actor)) return result;
            foreach (FightBattleStatusState status in actor.Statuses)
            {
                if (!FightSkillTriggerPolicy.AllowsStatus(skill, status) || !status.HasSkillStartTrigger
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
                        || !Roll(catalog == null ? trigger.BasePercent
                            : catalog.CalculateSkillStartTriggerRate(status, actor,
                                trigger.BasePercent), random)) continue;
                    result.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = trigger.WithSource(status),
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

        public static List<FightBattleStatusSkillInvocation> CollectActionStartSkills(
            FightBattleUnitState actor, FightBattleSkillDefinition skill,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (actor == null || skill == null || units == null || actor.Statuses == null
                || !FightBattleTargeting.IsSelectable(actor)) return result;
            foreach (FightBattleStatusState status in actor.Statuses)
            {
                if (!FightSkillTriggerPolicy.AllowsStatus(skill, status)
                    || !status.HasActionStartTrigger || !status.ActionStartRulesSupported
                    || status.ActionStartActionTypes == null
                    || !status.ActionStartActionTypes.Contains(skill.ActionType)) continue;
                if (!Roll(status.ActionStartRate, random)) continue;
                int added = 0;
                foreach (FightBattleStatusStateTrigger trigger in status.ActionStartSkills)
                {
                    if (trigger == null || trigger.EntityId <= 0 || !Roll(trigger.BasePercent, random))
                        continue;
                    int sourceId = trigger.Source == 3
                        ? FightStatusLifecycle.ResolveTargetUnitId(status,
                            FightStatusLifecycle.TargetCreator, units) : status.OwnerId;
                    if (sourceId <= 0) continue;
                    result.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = trigger.WithSource(status),
                        SourceUnitId = sourceId,
                        TargetUnitId = status.OwnerId,
                        TargetPositionUnitId = actor.Id
                    });
                    added++;
                }
                if (added > 0) status.ActionStartTriggeredCount++;
            }
            return result;
        }

        public static int ApplySkillEndStatuses(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random,
            List<FightBattleStatusSkillInvocation> configuredSkillInvocations = null)
        {
            if (actor == null || skill == null || units == null || catalog == null
                || actor.Statuses == null || !FightBattleTargeting.IsSelectable(actor)) return 0;
            var statuses = new List<FightBattleStatusState>(actor.Statuses);
            int applied = 0;
            foreach (FightBattleStatusState status in statuses)
            {
                if (!FightSkillTriggerPolicy.AllowsStatus(skill, status)) continue;
                if (status.HasSkillEndTrigger && status.SkillEndRulesSupported
                    && !status.SkillEndFilteredActionTypes.Contains(skill.ActionType))
                {
                    status.SkillEndMetCountProgress++;
                    if (status.SkillEndMetCountProgress >= Math.Max(1, status.SkillEndMetCount))
                    {
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
                                skill.Rank, skill.Level, skill.SubRank, actor.Properties, 0, null,
                                skill.BlessPropertyScales);
                            template.ReportSource = FightBattleStatusReportSource.Resolve(status).Copy();
                            FightStatusLifecycle.Apply(target, template, -1, instanceId, random);
                            applied++;
                        }
                    }
                }
                if (status.HasActionEndTrigger && status.ActionEndRulesSupported
                    && (status.ActionEndActionTypes.Count == 0
                        || status.ActionEndActionTypes.Contains(skill.ActionType))
                    && Roll(status.ActionEndRate, random))
                {
                    int triggered = 0;
                    foreach (FightBattleStatusStateTrigger trigger in status.ActionEndSkills)
                        if (AppendThresholdSkill(actor, status, trigger, random,
                            configuredSkillInvocations, null, units)) triggered++;
                    if (triggered > 0)
                    {
                        status.ActionEndTriggeredCount++;
                        applied += triggered;
                    }
                }
                if (status.ConsumeStackSkillDamageTriggered
                    && status.ConsumeStackSkillDamageRemoveOnSkillEnd)
                {
                    FightStatusLifecycle.End(actor, status);
                    status.ConsumeStackSkillDamageTriggered = false;
                }
            }
            return applied;
        }

        /// <summary>
        /// Official FightStatusConsumeStackSkillDmgScaleComponent: consume the configured stack
        /// source once per matching skill cast and return its additive damage multiplier.
        /// </summary>
        public static double PrepareConsumeStackSkillDamageScale(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
        {
            if (actor == null || skill == null || actor.Statuses == null
                || !FightBattleTargeting.IsSelectable(actor)) return 1.0;
            double scale = 1.0;
            foreach (FightBattleStatusState effect in new List<FightBattleStatusState>(actor.Statuses))
            {
                if (effect == null || !effect.HasConsumeStackSkillDamageScale
                    || !effect.ConsumeStackSkillDamageScaleRulesSupported
                    || effect.ConsumeStackSkillDamageTriggerSkillIds == null
                    || !effect.ConsumeStackSkillDamageTriggerSkillIds.Contains(skill.EntityId)) continue;
                int statusId = effect.ConsumeStackSkillDamageStatusId > 0
                    ? effect.ConsumeStackSkillDamageStatusId : effect.ClassId;
                FightBattleStatusState source = actor.Statuses.Find(value => value != null
                    && value.ClassId == statusId);
                if (source == null || source.StackCount < effect.ConsumeStackSkillDamageMinStackCount)
                    continue;
                int consumed = effect.ConsumeStackSkillDamageConsumeCount < 0
                    ? source.StackCount
                    : Math.Min(source.StackCount, effect.ConsumeStackSkillDamageConsumeCount);
                if (consumed <= 0) continue;
                int previous = source.StackCount;
                source.StackCount -= consumed;
                FightStatusPropertyRuntime.OnStackChanged(source, previous);
                effect.ConsumeStackSkillDamageConsumedCount = consumed;
                effect.ConsumeStackSkillDamageTriggered = true;
                scale *= 1.0 + effect.ConsumeStackSkillDamageScaleValuePerStack
                    * consumed / 10000.0;
                if (source.StackCount <= 0) FightStatusLifecycle.End(actor, source);
            }
            return Math.Max(0.0, scale);
        }

        /// <summary>
        /// Official FightStatusActionExistComponent: maintain the configured status while the
        /// owner has at least one status whose ActionType matches a configured condition.
        /// </summary>
        public static int ApplyActionExistStatuses(IReadOnlyList<FightBattleUnitState> units,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (units == null || catalog == null) return 0;
            int changed = 0;
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null || unit.Statuses == null) continue;
                var statuses = new List<FightBattleStatusState>(unit.Statuses);
                foreach (FightBattleStatusState generated in statuses)
                {
                    if (generated == null || !generated.IsActionExistGenerated) continue;
                    bool sourceExists = unit.Statuses.Exists(source => source != null
                        && source.InstanceId == generated.ActionExistSourceInstanceId
                        && source.HasActionExist);
                    if (!sourceExists && FightStatusLifecycle.End(unit, generated)) changed++;
                }
                statuses = new List<FightBattleStatusState>(unit.Statuses);
                foreach (FightBattleStatusState source in statuses)
                {
                    if (source == null || !source.HasActionExist
                        || !source.ActionExistRulesSupported
                        || source.ActionExistStatusId <= 0
                        || source.ActionExistConditions == null) continue;
                    bool conditionMet = source.ActionExistConditions.Exists(actionType =>
                        unit.Statuses.Exists(candidate => candidate != null
                            && candidate != source && candidate.ActionType == actionType));
                    FightBattleStatusState applied = source.ActionExistAppliedStatusInstanceId <= 0
                        ? null : unit.Statuses.Find(candidate => candidate != null
                            && candidate.InstanceId == source.ActionExistAppliedStatusInstanceId
                            && candidate.IsActionExistGenerated
                            && candidate.ActionExistSourceInstanceId == source.InstanceId);
                    if (!conditionMet)
                    {
                        if (applied != null && FightStatusLifecycle.End(unit, applied)) changed++;
                        source.ActionExistAppliedStatusInstanceId = 0;
                        continue;
                    }
                    if (applied != null) continue;
                    int instanceId = 1;
                    foreach (FightBattleStatusState existing in unit.Statuses)
                        if (existing != null && existing.InstanceId >= instanceId)
                            instanceId = existing.InstanceId + 1;
                    FightBattleStatusState template = catalog.CreateStatus(source.ActionExistStatusId,
                        unit.Id, source.CreatorId, instanceId, source.RootSkillId,
                        source.ApplyingSkillItemId, source.SourceSkillRank, source.SourceSkillLevel,
                        source.SourceSubRank, source.SourceProperties, 0,
                        source.ReplacementProperties, source.BlessPropertyScales);
                    template.IsActionExistGenerated = true;
                    template.ActionExistSourceInstanceId = source.InstanceId;
                    template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
                    FightBattleStatusState created = FightStatusLifecycle.Apply(unit, template, -1,
                        instanceId, random);
                    if (created != null)
                    {
                        source.ActionExistAppliedStatusInstanceId = created.InstanceId;
                        changed++;
                    }
                }
            }
            return changed;
        }

        public static int ApplyConsumeStackCounts(FightBattleUnitState owner,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (owner == null || owner.Statuses == null || catalog == null) return 0;
            int triggered = 0;
            foreach (FightBattleStatusState source in new List<FightBattleStatusState>(owner.Statuses))
            {
                if (source == null || !source.HasConsumeStackCount
                    || !source.ConsumeStackCountRulesSupported
                    || source.ConsumeStackCountTargetStatusId <= 0
                    || source.ConsumeStackCountThreshold <= 0) continue;
                FightBattleStatusState target = owner.Statuses.Find(status => status != null
                    && status.ClassId == source.ConsumeStackCountTargetStatusId);
                if (target == null) continue;
                while (target.StackCount >= source.ConsumeStackCountThreshold)
                {
                    int previous = target.StackCount;
                    target.StackCount -= source.ConsumeStackCountThreshold;
                    FightStatusPropertyRuntime.OnStackChanged(target, previous);
                    triggered++;
                    triggered += ApplyStatusTriggers(owner, source,
                        source.ConsumeStackCountStatuses, catalog, random);
                    if (target.StackCount <= 0)
                    {
                        FightStatusLifecycle.End(owner, target);
                        break;
                    }
                }
            }
            return triggered;
        }

        private static int ApplyStatusTriggers(FightBattleUnitState target,
            FightBattleStatusState source, List<FightBattleStatusStateTrigger> triggers,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            if (target == null || source == null || triggers == null || catalog == null)
                return 0;
            int applied = 0;
            foreach (FightBattleStatusStateTrigger trigger in triggers)
            {
                if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                    || trigger.Target != FightStatusLifecycle.TargetApplicator
                    || !Roll(trigger.BasePercent, random)) continue;
                int instanceId = 1;
                foreach (FightBattleStatusState existing in target.Statuses)
                    if (existing != null && existing.InstanceId >= instanceId)
                        instanceId = existing.InstanceId + 1;
                FightBattleStatusState template = catalog.CreateStatus(trigger.EntityId,
                    target.Id, source.CreatorId, instanceId, source.RootSkillId,
                    source.ApplyingSkillItemId, source.SourceSkillRank, source.SourceSkillLevel,
                    source.SourceSubRank, source.SourceProperties, 0,
                    source.ReplacementProperties, source.BlessPropertyScales);
                template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
                FightStatusLifecycle.Apply(target, template, -1, instanceId, random);
                applied++;
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

        public static int ApplyKillSkills(FightBattleUnitState killer,
            FightBattleUnitState defeated, FightBattleSkillDefinition sourceSkill,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random,
            List<FightBattleStatusSkillInvocation> configuredSkillInvocations = null)
        {
            if (killer == null || defeated == null || killer.Statuses == null || catalog == null
                || !FightBattleTargeting.IsSelectable(killer) || defeated.Hp > 0) return 0;
            var statuses = new List<FightBattleStatusState>(killer.Statuses);
            int applied = 0;
            foreach (FightBattleStatusState status in statuses)
            {
                if (!FightSkillTriggerPolicy.AllowsStatus(sourceSkill, status)
                    || !status.HasKillSkill || !status.KillSkillRulesSupported
                    || status.KillSkillMaxCount >= 0
                        && status.KillSkillTriggeredCount >= status.KillSkillMaxCount
                    || status.KillSkillFilterSelf && defeated.Id == killer.Id
                    || status.KillSkillFilterClassIds.Count > 0
                        && !MatchesKillClass(defeated, status.KillSkillFilterClassIds)
                    || !Roll(status.KillSkillRate, random)) continue;
                bool fired = false;
                if (status.KillSkillStatusEntityId > 0)
                {
                    int instanceId = 1;
                    foreach (FightBattleStatusState existing in killer.Statuses)
                        if (existing != null && existing.InstanceId >= instanceId)
                            instanceId = existing.InstanceId + 1;
                    FightBattleStatusState template = catalog.CreateStatus(
                        status.KillSkillStatusEntityId, killer.Id, status.CreatorId,
                        instanceId, status.RootSkillId, status.ApplyingSkillItemId,
                        status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank,
                        status.SourceProperties, 0, status.ReplacementProperties,
                        status.BlessPropertyScales);
                    template.ReportSource = FightBattleStatusReportSource.Resolve(status).Copy();
                    FightStatusLifecycle.Apply(killer, template, -1, instanceId, random);
                    fired = true;
                }
                else if (status.KillSkillEntityId > 0 && configuredSkillInvocations != null)
                {
                    int targetId = status.KillSkillTargetDiePos ? defeated.Id : killer.Id;
                    configuredSkillInvocations.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = new FightBattleStatusStateTrigger
                        {
                            EntityId = status.KillSkillEntityId,
                            SourceSkillItemId = FightBattleStatusReportSource.Resolve(status).SkillItemId,
                            SourceSkillRank = Math.Max(1, status.SourceSkillRank),
                            SourceSkillLevel = Math.Max(1, status.SourceSkillLevel),
                            SourceSubRank = Math.Max(0, status.SourceSubRank),
                            BlessPropertyScales = FightStatusPropertyModifiers.Copy(
                                status.BlessPropertyScales)
                        },
                        SourceUnitId = killer.Id,
                        TargetUnitId = targetId,
                        TargetPositionUnitId = targetId,
                        AllowDeadTarget = status.KillSkillTargetDiePos
                    });
                    fired = true;
                }
                if (fired)
                {
                    status.KillSkillTriggeredCount++;
                    applied++;
                }
            }
            return applied;
        }

        private static bool MatchesKillClass(FightBattleUnitState unit, List<int> classIds)
        {
            if (unit == null || classIds == null) return false;
            foreach (int classId in classIds)
                if (classId > 0 && (unit.MonsterId == classId
                    || unit.MonsterTemplateId == classId || unit.NpcClassId == classId))
                    return true;
            return false;
        }

        public const int MoveNearEventRound = 1;
        public const int MoveNearEventMoved = 2;
        public const int MoveNearEventEnterRange = 3;
        public const int MoveNearEventStandRound = 4;
        public const int MoveNearEventStart = 5;
        public const int MoveNearEventInRangeMove = 6;

        public static List<FightBattleStatusSkillInvocation> CollectMoveNearSkills(
            FightBattleUnitState roundActor, IReadOnlyList<FightBattleUnitState> units,
            int eventKind, FightBattleMovementEvent movement, FightRandomStream random)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (units == null || random == null) return result;
            int movedId = movement == null ? 0 : movement.UnitId;
            foreach (FightBattleUnitState source in units)
            {
                if (source == null || source.Statuses == null) continue;
                if ((eventKind == MoveNearEventRound || eventKind == MoveNearEventStart)
                    && (roundActor == null || source.Id != roundActor.Id)) continue;
                if (eventKind == MoveNearEventStandRound
                    && (roundActor == null || source.Id == roundActor.Id)) continue;
                if ((eventKind == MoveNearEventMoved || eventKind == MoveNearEventEnterRange
                        || eventKind == MoveNearEventInRangeMove)
                    && movedId <= 0) continue;
                if (eventKind == MoveNearEventMoved && source.Id != movedId) continue;
                foreach (FightBattleStatusState status in new List<FightBattleStatusState>(source.Statuses))
                {
                    if (status == null || !status.HasMoveNear || !status.MoveNearRulesSupported
                        || !MoveNearEventEnabled(status, eventKind)
                        || status.MoveNearMaxCount >= 0
                            && status.MoveNearTriggeredCount >= status.MoveNearMaxCount
                        || status.MoveNearMaxCountEachRound >= 0
                            && status.MoveNearRoundTriggeredCount >= status.MoveNearMaxCountEachRound)
                        continue;
                    if (eventKind == MoveNearEventStart)
                    {
                        if (status.MoveNearStartTriggered) continue;
                        status.MoveNearStartTriggered = true;
                    }
                    FightBattleUnitState moved = movedId <= 0 ? null : FindUnit(units, movedId);
                    var candidates = new List<FightBattleUnitState>();
                    var positions = new HashSet<FightGridPoint>();
                    foreach (FightBattleUnitState candidate in units)
                    {
                        if (candidate == null || candidate.Id == source.Id
                            || !MoveNearTargetMatches(source, candidate, status.MoveNearTargetType)
                            || status.MoveNearCharacterTypes.Count > 0
                                && !status.MoveNearCharacterTypes.Contains(candidate.CharacterType)
                            || status.MoveNearNoTriggerIfRoleDie && candidate.Hp <= 0
                            || eventKind == MoveNearEventStandRound
                                && roundActor != null && candidate.Id != roundActor.Id
                            || (eventKind == MoveNearEventEnterRange
                                && (moved == null || candidate.Id != moved.Id
                                    || !WasOutsideThenInside(source, candidate, movement,
                                        status.MoveNearRange)))
                            || (eventKind == MoveNearEventInRangeMove
                                && (moved == null || candidate.Id != moved.Id
                                    || !WasInsideThenInside(source, candidate, movement,
                                        status.MoveNearRange)))
                            || !IsMoveNearInRange(source, candidate, status.MoveNearRange)
                            || status.MoveNearOnlyFirstInPosition
                                && !positions.Add(new FightGridPoint(candidate.X, candidate.Y)))
                            continue;
                        candidates.Add(candidate);
                    }
                    foreach (FightBattleUnitState candidate in candidates)
                    {
                        if (status.MoveNearMaxCount >= 0
                                && status.MoveNearTriggeredCount >= status.MoveNearMaxCount
                            || status.MoveNearMaxCountEachRound >= 0
                                && status.MoveNearRoundTriggeredCount
                                    >= status.MoveNearMaxCountEachRound
                            || !Roll(status.MoveNearRate, random)) continue;
                        int added = 0;
                        foreach (FightBattleStatusStateTrigger trigger in status.MoveNearSkills)
                        {
                            if (trigger == null || trigger.EntityId <= 0
                                || !Roll(trigger.BasePercent, random)) continue;
                            int sourceId = trigger.Source == 3 ? candidate.Id : source.Id;
                            int targetId = trigger.Target == 2 ? source.Id : candidate.Id;
                            result.Add(new FightBattleStatusSkillInvocation
                            {
                                Trigger = trigger.WithSource(status),
                                SourceUnitId = sourceId,
                                TargetUnitId = targetId,
                                TargetPositionUnitId = candidate.Id,
                                Delay = status.MoveNearSkillTriggerImmediately
                                    ? 0f : Math.Max(0f, status.MoveNearSkillTriggerDelayTime)
                            });
                            added++;
                        }
                        if (added > 0)
                        {
                            status.MoveNearTriggeredCount += added;
                            status.MoveNearRoundTriggeredCount += added;
                        }
                    }
                }
            }
            return result;
        }

        private static bool MoveNearEventEnabled(FightBattleStatusState status, int eventKind)
        {
            switch (eventKind)
            {
                case MoveNearEventRound: return status.MoveNearTryAtRound;
                case MoveNearEventMoved: return status.MoveNearTryAtMoved;
                case MoveNearEventEnterRange: return status.MoveNearTryAtEnterRange;
                case MoveNearEventStandRound: return status.MoveNearTryAtStandRound;
                case MoveNearEventStart: return status.MoveNearTryAtStart;
                case MoveNearEventInRangeMove: return status.MoveNearTryAtInRangeMove;
                default: return false;
            }
        }

        private static bool MoveNearTargetMatches(FightBattleUnitState source,
            FightBattleUnitState candidate, int targetType)
        {
            if (source == null || candidate == null) return false;
            switch (targetType)
            {
                case 1: return source.Side != candidate.Side;
                case 2: return source.Side == candidate.Side;
                case 3: return source.Side == candidate.Side && source.Id != candidate.Id;
                case 4: return true;
                default: return false;
            }
        }

        private static bool IsMoveNearInRange(FightBattleUnitState source,
            FightBattleUnitState candidate, List<FightGridPoint> range)
        {
            if (range == null || range.Count == 0) return false;
            int dx = candidate.X - source.X, dy = candidate.Y - source.Y;
            foreach (FightGridPoint offset in range)
                if (offset.X == dx && offset.Y == dy) return true;
            return false;
        }

        private static bool WasOutsideThenInside(FightBattleUnitState source,
            FightBattleUnitState candidate, FightBattleMovementEvent movement,
            List<FightGridPoint> range)
        {
            if (movement == null || movement.Path == null || movement.Path.Count < 2) return false;
            FightGridPoint start = movement.Path[0];
            bool outside = !IsMoveNearInRange(source.X, source.Y, start.X, start.Y, range);
            return outside && IsMoveNearInRange(source, candidate, range);
        }

        private static bool WasInsideThenInside(FightBattleUnitState source,
            FightBattleUnitState candidate, FightBattleMovementEvent movement,
            List<FightGridPoint> range)
        {
            if (movement == null || movement.Path == null || movement.Path.Count < 2) return false;
            FightGridPoint start = movement.Path[0];
            return IsMoveNearInRange(source.X, source.Y, start.X, start.Y, range)
                && IsMoveNearInRange(source, candidate, range);
        }

        private static bool IsMoveNearInRange(int sourceX, int sourceY,
            int candidateX, int candidateY, List<FightGridPoint> range)
        {
            if (range == null) return false;
            int dx = candidateX - sourceX, dy = candidateY - sourceY;
            foreach (FightGridPoint offset in range)
                if (offset.X == dx && offset.Y == dy) return true;
            return false;
        }

        /// <summary>
        /// Applies the configured MoveRange handler after a movement or round check. The server
        /// handler is not present in the client package, so this keeps the official target filters
        /// and status ownership explicit in the offline battle state.
        /// </summary>
        public static int ApplyMoveRange(FightBattleUnitState source,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random, bool roundCheck)
        {
            if (source == null || units == null || catalog == null || source.Statuses == null)
                return 0;
            int applied = 0;
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(source.Statuses))
            {
                if (status == null || !status.HasMoveRange || !status.MoveRangeRulesSupported)
                    continue;
                if (roundCheck && status.MoveRangeHandleType == 1 && !status.MoveRangeCheckEveryRound)
                    continue;
                var candidates = new List<FightBattleUnitState>();
                foreach (FightBattleUnitState candidate in units)
                {
                    if (!MoveRangeTargetMatches(source, candidate, status.MoveRangeTargetType)
                        || status.MoveRangeExcludeDead && candidate.Hp <= 0
                        || status.MoveRangeCharacterTypes.Count > 0
                            && !status.MoveRangeCharacterTypes.Contains(candidate.CharacterType)
                        || status.MoveRangeFilterClassIds.Count > 0
                            && !status.MoveRangeFilterClassIds.Contains(MoveRangeClassId(candidate))
                        || !status.MoveRangeUnlimited
                            && !IsMoveNearInRange(source, candidate, status.MoveRangeOffsets))
                        continue;
                    candidates.Add(candidate);
                }
                if (status.MoveRangeHandleType == 1)
                {
                    var inside = new HashSet<int>(candidates.Select(candidate => candidate.Id));
                    foreach (FightBattleUnitState candidate in candidates)
                    {
                        bool entered = status.MoveRangeInsideUnitIds.Add(candidate.Id);
                        if ((entered || status.MoveRangeCheckEveryRound)
                            && ApplyMoveRangeStatuses(candidate, source, status,
                                status.MoveRangeCloseStatuses, catalog, random)) applied++;
                    }
                    foreach (int oldId in new List<int>(status.MoveRangeInsideUnitIds))
                        if (!inside.Contains(oldId))
                        {
                            status.MoveRangeInsideUnitIds.Remove(oldId);
                            FightBattleUnitState oldTarget = units.FirstOrDefault(unit =>
                                unit != null && unit.Id == oldId);
                            if (oldTarget != null)
                                RemoveMoveRangeStatuses(new[] { oldTarget }, source.Id,
                                    status.MoveRangeCloseStatuses);
                        }
                }
                else if (status.MoveRangeHandleType == 2)
                {
                    bool meets = candidates.Count <= status.MoveRangeTargetCount;
                    List<FightBattleStatusStateTrigger> desired = meets
                        ? status.MoveRangeMeetStatuses : status.MoveRangeNotMeetStatuses;
                    List<FightBattleStatusStateTrigger> other = meets
                        ? status.MoveRangeNotMeetStatuses : status.MoveRangeMeetStatuses;
                    RemoveMoveRangeStatuses(new[] { source }, source.Id, other);
                    if (status.MoveRangeLastTargetCount != candidates.Count
                        || !roundCheck && status.MoveRangeLastTargetCount < 0)
                        if (ApplyMoveRangeStatuses(source, source, status, desired, catalog, random))
                            applied++;
                    status.MoveRangeLastTargetCount = candidates.Count;
                }
                else if (status.MoveRangeHandleType == 3)
                {
                    int desiredCount = status.MoveRangeMaxStack < 0
                        ? candidates.Count : Math.Min(candidates.Count, status.MoveRangeMaxStack);
                    int currentCount = CountMoveRangeStatuses(source, source.Id,
                        status.MoveRangeCountStatuses);
                    while (currentCount < desiredCount
                        && ApplyMoveRangeStatuses(source, source, status,
                            status.MoveRangeCountStatuses, catalog, random))
                    {
                        applied++; currentCount++;
                    }
                    if (desiredCount == 0)
                        RemoveMoveRangeStatuses(new[] { source }, source.Id,
                            status.MoveRangeCountStatuses);
                }
            }
            return applied;
        }

        private static bool MoveRangeTargetMatches(FightBattleUnitState source,
            FightBattleUnitState candidate, int targetType)
        {
            if (source == null || candidate == null || candidate.Id == source.Id) return false;
            switch (targetType)
            {
                case 0: return false;
                case 1: return source.Side != candidate.Side;
                case 2: return source.Side == candidate.Side;
                case 3: return source.Side == candidate.Side;
                case 4: return true;
                default: return source.Side == candidate.Side;
            }
        }

        private static int MoveRangeClassId(FightBattleUnitState unit)
        {
            return unit.MonsterId != 0 ? unit.MonsterId
                : unit.NpcClassId != 0 ? unit.NpcClassId : unit.MonsterTemplateId;
        }

        private static bool ApplyMoveRangeStatuses(FightBattleUnitState owner,
            FightBattleUnitState source, FightBattleStatusState sourceStatus,
            List<FightBattleStatusStateTrigger> triggers, FightOfficialCombatCatalog catalog,
            FightRandomStream random)
        {
            bool applied = false;
            if (triggers == null) return false;
            foreach (FightBattleStatusStateTrigger trigger in triggers)
            {
                if (trigger == null || trigger.EntityId <= 0 || !Roll(trigger.BasePercent, random))
                    continue;
                int instanceId = 1;
                foreach (FightBattleStatusState existing in owner.Statuses)
                    if (existing != null && existing.InstanceId >= instanceId) instanceId = existing.InstanceId + 1;
                FightBattleStatusState template = catalog.CreateStatus(trigger.EntityId, owner.Id,
                    source.Id, instanceId, sourceStatus.RootSkillId, sourceStatus.ApplyingSkillItemId,
                    sourceStatus.SourceSkillRank, sourceStatus.SourceSkillLevel, sourceStatus.SourceSubRank,
                    source.Properties, 0, sourceStatus.ReplacementProperties,
                    sourceStatus.BlessPropertyScales);
                template.ReportSource = FightBattleStatusReportSource.Resolve(sourceStatus).Copy();
                FightStatusLifecycle.Apply(owner, template, -1, instanceId, random);
                applied = true;
            }
            return applied;
        }

        private static int CountMoveRangeStatuses(FightBattleUnitState owner, int sourceId,
            List<FightBattleStatusStateTrigger> triggers)
        {
            if (owner == null || triggers == null) return 0;
            var ids = new HashSet<int>(triggers.Where(trigger => trigger != null)
                .Select(trigger => trigger.EntityId));
            return owner.Statuses.Count(status => status != null && status.CreatorId == sourceId
                && ids.Contains(status.ClassId));
        }

        private static void RemoveMoveRangeStatuses(IEnumerable<FightBattleUnitState> units,
            int sourceId, List<FightBattleStatusStateTrigger> triggers)
        {
            if (units == null || triggers == null) return;
            var ids = new HashSet<int>(triggers.Where(trigger => trigger != null)
                .Select(trigger => trigger.EntityId));
            foreach (FightBattleUnitState unit in units)
                for (int i = unit.Statuses.Count - 1; i >= 0; i--)
                    if (unit.Statuses[i] != null && unit.Statuses[i].CreatorId == sourceId
                        && ids.Contains(unit.Statuses[i].ClassId)) FightStatusLifecycle.End(unit, unit.Statuses[i]);
        }

        public static int Apply(FightBattleUnitState unit, FightOfficialCombatCatalog catalog,
            bool atStart, List<FightBattleStatusStateTrigger> triggerSkills = null,
            FightRandomStream random = null, FightBattleSkillDefinition sourceSkill = null,
            IReadOnlyList<FightBattleUnitState> units = null,
            List<FightBattleStatusSkillInvocation> configuredTriggerSkills = null)
        {
            if (unit == null || catalog == null || unit.Statuses == null || unit.MaxHp <= 0) return 0;
            int applied = 0;
            for (int i = unit.Statuses.Count - 1; i >= 0; i--)
            {
                FightBattleStatusState status = unit.Statuses[i];
                if (!FightSkillTriggerPolicy.AllowsStatus(sourceSkill, status)) continue;
                if (status.HpTriggerPer > 0f)
                {
                    bool hasStatuses = status.HpTriggerStatuses != null && status.HpTriggerStatuses.Count > 0;
                    bool hasSkills = status.HpTriggerSkills != null && status.HpTriggerSkills.Count > 0;
                    if ((hasStatuses || hasSkills) && (!atStart || status.HpTriggerAtStart)
                        && (status.HpTriggerMaxCount < 0
                            || status.HpTriggeredCount < status.HpTriggerMaxCount)
                        && unit.Hp / (double)unit.MaxHp <= status.HpTriggerPer)
                    {
                        foreach (int statusId in status.HpTriggerStatuses ?? new List<int>())
                        {
                            if (statusId <= 0) continue;
                            int instanceId = 1;
                            foreach (FightBattleStatusState existing in unit.Statuses)
                                if (existing != null && existing.InstanceId >= instanceId)
                                    instanceId = existing.InstanceId + 1;
                            FightBattleStatusState template = catalog.CreateStatus(statusId, unit.Id,
                                status.CreatorId <= 0 ? unit.Id : status.CreatorId, instanceId,
                                status.RootSkillId, status.ApplyingSkillItemId, status.SourceSkillRank,
                                status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                                0, status.ReplacementProperties, status.BlessPropertyScales);
                            template.ReportSource = FightBattleStatusReportSource.Resolve(status).Copy();
                            FightStatusLifecycle.Apply(unit, template, -1, instanceId, random);
                        }
                        if (triggerSkills != null && status.HpTriggerSkills != null)
                            foreach (int skillId in status.HpTriggerSkills)
                                if (skillId > 0)
                                    triggerSkills.Add(new FightBattleStatusStateTrigger
                                    {
                                        EntityId = skillId,
                                        SourceSkillRank = Math.Max(1, status.SourceSkillRank),
                                        SourceSkillLevel = Math.Max(1, status.SourceSkillLevel),
                                        SourceSubRank = Math.Max(0, status.SourceSubRank),
                                        BlessPropertyScales = FightStatusPropertyModifiers.Copy(
                                            status.BlessPropertyScales)
                                    });
                        status.HpTriggeredCount++;
                        applied++;
                    }
                }
                if (ApplyHpLessThan(unit, status, catalog, atStart, units, random,
                    triggerSkills, configuredTriggerSkills)) applied++;
                if (ApplyHpLimit(unit, status, catalog, atStart, units, random,
                    configuredTriggerSkills)) applied++;
            }
            return applied;
        }

        private static bool ApplyHpLessThan(FightBattleUnitState unit,
            FightBattleStatusState status, FightOfficialCombatCatalog catalog, bool atStart,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            List<FightBattleStatusStateTrigger> triggerSkills,
            List<FightBattleStatusSkillInvocation> configuredTriggerSkills)
        {
            if (status == null || !status.HasHpLessThan
                || atStart != status.HpLessThanTryAtStart
                || status.HpLessThanTargetType > 0 && units != null
                    && FightStatusLifecycle.ResolveTargetUnitId(status,
                        status.HpLessThanTargetType, units) != unit.Id
                || status.HpLessThanMaxCount >= 0
                    && status.HpLessThanTriggeredCount >= status.HpLessThanMaxCount
                || status.HpLessThanCharacterTypes.Count > 0
                    && !status.HpLessThanCharacterTypes.Contains(unit.CharacterType)) return false;
            if (status.HpLessThanCheckExistForbidden && HasForbiddenTriggerStatus(unit)) return false;
            bool absolute = status.HpLessThan >= 0 && unit.Hp <= status.HpLessThan;
            bool percentage = status.HpLessThanPercentage >= 0f
                && unit.Hp / (double)Math.Max(1L, unit.MaxHp) <= status.HpLessThanPercentage;
            if (!absolute && !percentage || unit.Hp <= 0 && !status.HpLessThanInvokeOnDie)
            {
                if (status.HpLessThanNotMeetRemoveStatus)
                    RemoveHpLessThanTriggeredStatuses(unit, status);
                return false;
            }
            int fired = 0;
            int immediateChecks = 0;
            do
            {
                immediateChecks++;
                foreach (int statusId in status.HpLessThanDeactivatePassiveSkills)
                    unit.Statuses.RemoveAll(value => value != null && value.ClassId == statusId);
                foreach (int statusId in status.HpLessThanActivatePassiveSkills)
                    ActivateStatus(unit, status, statusId, catalog, random);
                foreach (FightBattleStatusStateTrigger trigger in status.HpLessThanStatuses)
                    if (ApplyThresholdStatus(unit, status, trigger, catalog, units, random)) fired++;
                foreach (FightBattleStatusStateTrigger trigger in status.HpLessThanSkills)
                    if (AppendThresholdSkill(unit, status, trigger, random, configuredTriggerSkills,
                        triggerSkills, units)) fired++;
                status.HpLessThanTriggeredCount++;
                if (status.HpLessThanAutoDestroy)
                {
                    FightStatusLifecycle.End(unit, status);
                    break;
                }
                if (!status.HpLessThanTryAfterInvoke || immediateChecks >= 1
                    && status.HpLessThanMaxCount < 0) break;
                absolute = status.HpLessThan >= 0 && unit.Hp <= status.HpLessThan;
                percentage = status.HpLessThanPercentage >= 0f
                    && unit.Hp / (double)Math.Max(1L, unit.MaxHp) <= status.HpLessThanPercentage;
            }
            while ((absolute || percentage) && (status.HpLessThanMaxCount < 0
                || status.HpLessThanTriggeredCount < status.HpLessThanMaxCount));
            return fired > 0;
        }

        private static bool HasForbiddenTriggerStatus(FightBattleUnitState unit)
        {
            if (unit == null || unit.Statuses == null) return false;
            foreach (FightBattleStatusState status in unit.Statuses)
                if (status != null && status.ActionType == 70) return true;
            return false;
        }

        private static void RemoveHpLessThanTriggeredStatuses(FightBattleUnitState unit,
            FightBattleStatusState source)
        {
            if (unit == null || source == null || unit.Statuses == null) return;
            var ids = new HashSet<int>();
            foreach (FightBattleStatusStateTrigger trigger in source.HpLessThanStatuses)
                if (trigger != null && trigger.EntityId > 0) ids.Add(trigger.EntityId);
            if (ids.Count == 0) return;
            unit.Statuses.RemoveAll(value => value != null && ids.Contains(value.ClassId));
        }

        private static void ActivateStatus(FightBattleUnitState owner,
            FightBattleStatusState source, int statusId, FightOfficialCombatCatalog catalog,
            FightRandomStream random)
        {
            if (statusId <= 0 || owner.Statuses.Any(value => value != null && value.ClassId == statusId))
                return;
            int instanceId = 1;
            foreach (FightBattleStatusState existing in owner.Statuses)
                if (existing != null && existing.InstanceId >= instanceId) instanceId = existing.InstanceId + 1;
            FightBattleStatusState template = catalog.CreateStatus(statusId, owner.Id, source.CreatorId,
                instanceId, source.RootSkillId, source.ApplyingSkillItemId, source.SourceSkillRank,
                source.SourceSkillLevel, source.SourceSubRank, source.SourceProperties, 0,
                source.ReplacementProperties, source.BlessPropertyScales);
            template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
            FightStatusLifecycle.Apply(owner, template, -1, instanceId, random);
        }

        private static bool ApplyHpLimit(FightBattleUnitState unit,
            FightBattleStatusState status, FightOfficialCombatCatalog catalog, bool atStart,
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            List<FightBattleStatusSkillInvocation> configuredTriggerSkills)
        {
            if (status == null || !status.HasHpLimit || (atStart && status.HpLimitMaxCount == 0)
                || status.HpLimitListenerType == 0 && status.OwnerId > 0
                    && status.OwnerId != unit.Id
                || unit.Hp > status.HpLimitLowerLimit
                || status.HpLimitMaxCount >= 0
                    && status.HpLimitTriggeredCount >= status.HpLimitMaxCount)
                return false;
            foreach (FightBattleStatusStateTrigger trigger in status.HpLimitStatuses)
                ApplyThresholdStatus(unit, status, trigger, catalog, units, random);
            foreach (FightBattleStatusStateTrigger trigger in status.HpLimitSkills)
                AppendThresholdSkill(unit, status, trigger, random, configuredTriggerSkills, null, units);
            foreach (FightBattleStatusInvoke invoke in status.HpLimitInvokes)
                if (invoke != null)
                {
                    if (invoke.Type == 1)
                    {
                        int targetId = invoke.Target == 0 ? unit.Id
                            : FightStatusLifecycle.ResolveTargetUnitId(status, invoke.Target, units);
                        FightBattleUnitState target = FindUnit(units, targetId) ?? unit;
                        FightStatusCooldownRuntime.ChangeCooldown(target, invoke.ChangeCooldown);
                    }
                    else if (invoke.Type == 2)
                        ApplyAroundInvoke(unit, status, invoke, catalog, units, random,
                            configuredTriggerSkills);
                }
            status.HpLimitTriggeredCount++;
            if (status.HpLimitAutoRemove) FightStatusLifecycle.End(unit, status);
            return true;
        }

        private static int ApplyAroundInvoke(FightBattleUnitState owner,
            FightBattleStatusState source, FightBattleStatusInvoke invoke,
            FightOfficialCombatCatalog catalog, IReadOnlyList<FightBattleUnitState> units,
            FightRandomStream random, List<FightBattleStatusSkillInvocation> configured)
        {
            if (owner == null || source == null || invoke == null || units == null) return 0;
            int applied = 0;
            foreach (FightBattleUnitState target in SelectAroundTargets(owner, invoke, units))
            {
                foreach (FightBattleStatusStateTrigger trigger in invoke.Statuses)
                    if (ApplyThresholdStatus(owner, source, trigger, catalog, units, random, target))
                        applied++;
                foreach (FightBattleStatusStateTrigger trigger in invoke.Skills)
                    if (AppendThresholdSkill(owner, source, trigger, random, configured, null,
                        units, target)) applied++;
            }
            return applied;
        }

        private static List<FightBattleUnitState> SelectAroundTargets(
            FightBattleUnitState owner, FightBattleStatusInvoke invoke,
            IReadOnlyList<FightBattleUnitState> units)
        {
            var result = new List<FightBattleUnitState>();
            if (invoke.TargetType == 5) return result;
            var offsets = invoke.Range == null || invoke.Range.Count == 0
                ? new List<FightGridPoint> { new FightGridPoint(0, 0) }
                : invoke.Range;
            var seen = new HashSet<int>();
            foreach (FightGridPoint offset in offsets)
                foreach (FightBattleUnitState candidate in units)
                {
                    if (candidate == null || !FightBattleTargeting.IsSelectable(candidate)
                        || !AroundTargetTypeMatches(owner, candidate, invoke.TargetType)
                        || invoke.CharacterTypes != null && invoke.CharacterTypes.Length > 0
                            && !invoke.CharacterTypes.Contains(candidate.CharacterType)
                        || invoke.CharacterEntityClassIds != null
                            && invoke.CharacterEntityClassIds.Length > 0
                            && !MatchesEntityClass(candidate, invoke.CharacterEntityClassIds)
                        || offset.X != candidate.X - owner.X || offset.Y != candidate.Y - owner.Y
                        || !seen.Add(candidate.Id)) continue;
                    result.Add(candidate);
                    if (invoke.MaxCount > 0 && result.Count >= invoke.MaxCount) return result;
                }
            return result;
        }

        private static bool AroundTargetTypeMatches(FightBattleUnitState owner,
            FightBattleUnitState candidate, int targetType)
        {
            switch (targetType)
            {
                case 0: return candidate.Id == owner.Id;
                case 1: return candidate.Side != owner.Side;
                case 2: return candidate.Side == owner.Side;
                case 3: return candidate.Side == owner.Side && candidate.Id != owner.Id;
                case 4: return true;
                case 6: return candidate.Id == owner.MasterId;
                case 7: return candidate.MasterId == owner.Id;
                case 8: return candidate.Side == owner.Side && candidate.Id != owner.Id
                    && candidate.CharacterType == 1;
                case 9: return candidate.Side == owner.Side
                    && (candidate.CharacterType == 1 || candidate.CharacterType == 4);
                case 10: return candidate.Side == owner.Side && candidate.Id != owner.Id
                    && (candidate.CharacterType == 1 || candidate.CharacterType == 4);
                default: return false;
            }
        }

        private static bool MatchesEntityClass(FightBattleUnitState candidate, int[] classIds)
        {
            foreach (int classId in classIds)
                if (classId > 0 && (candidate.MonsterId == classId
                    || candidate.MonsterTemplateId == classId || candidate.NpcClassId == classId))
                    return true;
            return false;
        }

        private static bool ApplyThresholdStatus(FightBattleUnitState owner,
            FightBattleStatusState source, FightBattleStatusStateTrigger trigger,
            FightOfficialCombatCatalog catalog, IReadOnlyList<FightBattleUnitState> units,
            FightRandomStream random, FightBattleUnitState targetOverride = null)
        {
            if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                || !Roll(trigger.BasePercent, random)) return false;
            FightBattleUnitState filterTarget = targetOverride ?? owner;
            if (trigger.TargetWithoutStatusIds != null
                && trigger.TargetWithoutStatusIds.Any(id => filterTarget.Statuses.Exists(value =>
                    value != null && value.ClassId == id))) return false;
            int targetId = trigger.ApplyTarget == 0 ? filterTarget.Id
                : FightStatusLifecycle.ResolveTargetUnitId(source, trigger.ApplyTarget, units);
            FightBattleUnitState target = FindUnit(units, targetId) ?? filterTarget;
            if (!FightBattleTargeting.IsSelectable(target) && target.Hp > 0) return false;
            int instanceId = 1;
            foreach (FightBattleStatusState existing in target.Statuses)
                if (existing != null && existing.InstanceId >= instanceId) instanceId = existing.InstanceId + 1;
            FightBattleStatusState template = catalog.CreateStatus(trigger.EntityId, target.Id, source.CreatorId,
                instanceId, source.RootSkillId, source.ApplyingSkillItemId, source.SourceSkillRank,
                source.SourceSkillLevel, source.SourceSubRank, source.SourceProperties, 0,
                trigger.StatusReplacementProperties != null
                    && trigger.StatusReplacementProperties.Count > 0
                    ? trigger.StatusReplacementProperties
                    : source.HpLessThanReplacementProperties != null
                        && source.HpLessThanReplacementProperties.Count > 0
                        ? source.HpLessThanReplacementProperties
                        : source.HpLimitReplacementProperties != null
                            && source.HpLimitReplacementProperties.Count > 0
                            ? source.HpLimitReplacementProperties
                            : source.ReplacementProperties,
                source.BlessPropertyScales);
            template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
            FightStatusLifecycle.Apply(target, template, -1, instanceId, random);
            return true;
        }

        private static bool AppendThresholdSkill(FightBattleUnitState owner,
            FightBattleStatusState source, FightBattleStatusStateTrigger trigger,
            FightRandomStream random, List<FightBattleStatusSkillInvocation> configured,
            List<FightBattleStatusStateTrigger> simple,
            IReadOnlyList<FightBattleUnitState> units,
            FightBattleUnitState targetOverride = null)
        {
            if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                || !Roll(trigger.BasePercent, random)) return false;
            FightBattleStatusStateTrigger copy = trigger.WithSource(source);
            if (configured == null || trigger.Source == 0 && trigger.Target == 0)
            {
                if (simple != null) simple.Add(copy);
                return true;
            }
            int sourceId = trigger.Source == 0 ? owner.Id
                : FightStatusLifecycle.ResolveTargetUnitId(source, trigger.Source, units);
            int targetId = trigger.Target == 0 ? (targetOverride == null ? owner.Id : targetOverride.Id)
                : FightStatusLifecycle.ResolveTargetUnitId(source, trigger.Target, units);
            int targetPositionId = trigger.TargetPos == 1 ? targetId
                : targetOverride == null ? owner.Id : targetOverride.Id;
            configured.Add(new FightBattleStatusSkillInvocation
            {
                Trigger = copy, SourceUnitId = sourceId > 0 ? sourceId : owner.Id,
                TargetUnitId = targetId > 0 ? targetId : owner.Id,
                TargetPositionUnitId = targetPositionId > 0 ? targetPositionId : owner.Id,
                AllowDeadSource = source.HpLessThanInvokeOnDie && owner.Hp <= 0,
                AllowDeadTarget = source.HpLessThanInvokeOnDie && owner.Hp <= 0
            });
            return true;
        }

        /// <summary>
        /// Official <c>FightStatusHpAccDecreaseComponent</c> local rule: accumulate actual HP lost,
        /// never reduce the accumulator for healing, and consume one configured threshold per trigger.
        /// </summary>
        public static int ApplyAccumulatedDamage(FightBattleUnitState unit, long actualHpDamage,
            FightOfficialCombatCatalog catalog, FightRandomStream random,
            List<FightBattleStatusStateTrigger> triggerSkills = null,
            FightBattleSkillDefinition sourceSkill = null)
        {
            if (unit == null || catalog == null || actualHpDamage <= 0 || unit.MaxHp <= 0
                || unit.Statuses == null) return 0;
            int triggered = 0;
            int initialStatusCount = unit.Statuses.Count;
            for (int i = initialStatusCount - 1; i >= 0; i--)
            {
                FightBattleStatusState status = unit.Statuses[i];
                if (!FightSkillTriggerPolicy.AllowsStatus(sourceSkill, status)
                    || !status.HasHpAccDecrease) continue;
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
                    unit.Id, instanceId, source.RootSkillId, source.ApplyingSkillItemId,
                    source.SourceSkillRank, source.SourceSkillLevel, source.SourceSubRank,
                    source.SourceProperties, 0, source.ReplacementProperties,
                    source.BlessPropertyScales);
                template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
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
                output.Add(trigger.WithSource(status));
            }
        }

        private static bool Roll(double chance, FightRandomStream random)
        {
            if (chance >= 1f) return true;
            if (chance <= 0f || random == null) return false;
            return random.Next() < chance;
        }
    }
}
