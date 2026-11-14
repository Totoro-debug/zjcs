using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // Confirmed offline SelfTargetPosResolver uses the hit entity's creation
        // pose; the APK contains the resolver type but no server assignment.
        private void CollectHitTriggerSkill(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, List<FightBattleStatusSkillInvocation> output,
            FightGridPoint hitOrigin, FightBattleSkillCastPresentation hitCast, float hitDelay)
        {
            FightBattleHitTriggerSkillState trigger = hit?.TriggerSkill;
            if (trigger == null || output == null || actor == null || target == null) return;
            foreach (int skillId in trigger.SkillIds ?? new List<int>())
            {
                if (skillId <= 0) continue;
                output.Add(new FightBattleStatusSkillInvocation
                {
                    Trigger = new FightBattleStatusStateTrigger
                    {
                        EntityId = skillId,
                        SourceSkillRank = Math.Max(1, skill?.Rank ?? actor.Level),
                        SourceSkillLevel = Math.Max(1, skill?.Level ?? actor.Level),
                        SourceSubRank = Math.Max(0, skill?.SubRank ?? actor.SubRank),
                        Origin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy()
                    },
                    SourceUnitId = actor.Id,
                    TargetUnitId = target.Id,
                    TargetPositionUnitId = target.Id,
                    Delay = hitDelay,
                    HitOrigin = trigger.UseTargetPosition ? (FightGridPoint?)null : hitOrigin,
                    HitFacing = hitCast.FacingIndex,
                    HitFlipX = hitCast.FlipX
                });
            }
        }

        private void ApplyMissingStatusComponentsOnHit(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, FightBattleHitResult resolved, long actualHpLoss,
            List<FightBattleStatusSkillInvocation> invocations)
        {
            if (statusCatalog == null || actor == null || target == null || invocations == null)
                return;
            FightStatusHitScope.Collect(actor, target, skill, resolved,
                movementStartContext?.StatusStartTime ?? 0f, invocations, random);
            ApplyDarkHitStatuses(actor, target, skill, resolved);
            CollectHitCustomCures(actor, target, skill, hit, resolved,
                movementStartContext?.StatusStartTime ?? 0f, invocations);
            var owners = new List<FightBattleUnitState> { actor };
            if (!ReferenceEquals(actor, target)) owners.Add(target);
            foreach (FightBattleUnitState owner in owners)
            {
                if (owner.Statuses == null) continue;
                foreach (FightBattleStatusState status in new List<FightBattleStatusState>(owner.Statuses))
                {
                    if (status == null || !owner.Statuses.Contains(status)
                        || !FightSkillTriggerPolicy.AllowsStatus(skill, status)) continue;
                    FightBattleStatusHitSkillComponentState hitSkill = status.Components?.HitSkill;
                    if (hitSkill != null && HitSourceMatches(status, hitSkill.HitSourceType, actor)
                        && HitRuleMatches(hitSkill.Rate, hitSkill.Block, hitSkill.Crit,
                            hitSkill.Damage, hitSkill.Cure, hitSkill.IsTrueDamage,
                            hitSkill.HitSkillTypes, hitSkill.HitSkillElementTypes,
                            hitSkill.HitSkillEntityClassIds, hitSkill.TargetActionTypes,
                            skill, hit, resolved, target)
                        && AdvanceCounter(hitSkill.Rate, hitSkill.ConditionCount, hitSkill.MaxInvokeCount,
                            ref hitSkill.ConditionProgress, ref hitSkill.InvokeCount, random))
                    {
                        foreach (FightBattleStatusStateTrigger trigger in hitSkill.SkillTriggers)
                            AppendComponentInvocation(status, trigger, actor, target, invocations,
                                hitSkill.IsSkillTargetPos);
                        if (hitSkill.SkillId > 0)
                            AppendComponentInvocation(status, new FightBattleStatusStateTrigger
                            {
                                EntityId = hitSkill.SkillId, Source = 0, Target = 0,
                                TargetPos = hitSkill.IsSkillTargetPos ? 1 : 0
                            }, actor, target, invocations, hitSkill.IsSkillTargetPos);
                        ApplyComponentStatuses(status, hitSkill.StatusTriggers, actor, target);
                        ApplyComponentStatus(status, hitSkill.TargetStatusId, target, skill);
                        ApplyComponentStatus(status, hitSkill.StatusId, owner, skill);
                        if (hitSkill.DisperseActionTypes != null && hitSkill.DisperseActionTypes.Count > 0)
                            DisperseActionStatuses(target, hitSkill.DisperseActionTypes);
                        if (hitSkill.AutoRemove && hitSkill.MaxInvokeCount >= 0
                            && hitSkill.InvokeCount >= hitSkill.MaxInvokeCount)
                            FightStatusLifecycle.End(owner, status);
                    }

                    if (TryApplyReturnStrikeStatus(status, owner, target, skill, hit, resolved)) continue;
                    FightBattleStatusDamageSkillComponentState damageSkill = status.Components?.DamageSkill;
                    if (damageSkill != null && status.Components.Seconds == null && resolved != null
                        && (resolved.Formula != null || resolved.Cure != null || resolved.Settlement != null)
                        && HitSourceMatches(status,
                            damageSkill.IsAttackerTarget ? 0 : 2, actor)
                        && HitRuleMatches(damageSkill.Rate, damageSkill.Block, damageSkill.Crit,
                            damageSkill.Damage, damageSkill.Cure, false, null,
                            damageSkill.ElementTypes, null, null, skill, hit, resolved, target)
                        && (damageSkill.GreaterThanMaxHpPer < 0f
                            || Math.Abs(resolved?.Formula?.AbsoluteDamage ?? actualHpLoss)
                                > target.MaxHp * damageSkill.GreaterThanMaxHpPer)
                        && (damageSkill.EachRoundMaxCount < 0
                            || damageSkill.RoundTriggeredCount < damageSkill.EachRoundMaxCount))
                    {
                        if (AdvanceCounter(damageSkill.Rate, damageSkill.ConditionCount, damageSkill.MaxCount,
                            ref damageSkill.ConditionProgress, ref damageSkill.RoundTriggeredCount,
                            random))
                        {
                            foreach (FightBattleStatusStateTrigger trigger in damageSkill.SkillTriggers)
                                AppendComponentInvocation(status, trigger, actor, target, invocations,
                                    !damageSkill.IsAttackerTarget);
                            if (damageSkill.SkillId > 0)
                                AppendComponentInvocation(status, new FightBattleStatusStateTrigger
                                {
                                    EntityId = damageSkill.SkillId, Source = 0, Target = 0,
                                    TargetPos = damageSkill.IsAttackerTarget ? 0 : 1
                                }, actor, target, invocations, !damageSkill.IsAttackerTarget);
                            ApplyComponentStatuses(status, damageSkill.StatusTriggers, actor, target);
                            ApplyComponentStatus(status, damageSkill.SourceStatusId, actor, skill);
                            ApplyComponentStatus(status, damageSkill.StatusId,
                                damageSkill.IsAttackerTarget ? actor : target, skill);
                            if (damageSkill.AutoDestroy && damageSkill.MaxCount >= 0
                                && damageSkill.RoundTriggeredCount >= damageSkill.MaxCount)
                                FightStatusLifecycle.End(owner, status);
                        }
                    }
                }
            }
        }

        private static bool HitRuleMatches(float rate, bool block, bool crit, bool damage,
            bool cure, bool trueDamage, List<int> skillTypes, List<int> elementTypes,
            List<int> skillIds, List<int> targetActions, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, FightBattleHitResult result,
            FightBattleUnitState target)
        {
            bool isCure = result != null && result.IsCure || hit != null && hit.ActionType == 5;
            bool hasResult = result != null
                && (result.Formula != null || result.Cure != null || result.Settlement != null);
            if ((damage || cure) && !hasResult) return false;
            if (damage && isCure || cure && !isCure) return false;
            if (block && (result == null || !result.Type.Block)
                || crit && (result == null || !result.Type.Critical)
                || trueDamage && (hit == null || !hit.TrueDamage)) return false;
            if (skill != null && skillTypes != null && skillTypes.Count > 0
                && !skillTypes.Contains(skill.SkillType)) return false;
            if (skill != null && elementTypes != null && elementTypes.Count > 0
                && !elementTypes.Contains(skill.ElementType)) return false;
            if (skill != null && skillIds != null && skillIds.Count > 0
                && !skillIds.Contains(skill.EntityId)) return false;
            if (skill != null && targetActions != null && targetActions.Count > 0
                && !targetActions.Contains(skill.HitTargetActionType)) return false;
            return rate >= 1f || rate > 0f;
        }

        private bool AdvanceCounter(float rate, int threshold, int maxCount, ref int progress,
            ref int invoked, FightRandomStream stream)
        {
            if (maxCount >= 0 && invoked >= maxCount) return false;
            if (!RollComponent(rate)) return false;
            progress++;
            int required = Math.Max(1, threshold);
            if (progress < required) return false;
            progress = 0;
            invoked++;
            return true;
        }

        private bool HitSourceMatches(FightBattleStatusState status, int sourceType,
            FightBattleUnitState actor)
        {
            if (status == null || actor == null) return false;
            if (sourceType == 0) return true;
            int sourceId = sourceType == FightStatusLifecycle.TargetCreator
                ? status.CreatorId : sourceType == FightStatusLifecycle.TargetApplicator
                    ? status.OwnerId : sourceType == FightStatusLifecycle.TargetCreatorMaster
                        ? FindMaster(status.CreatorId) : FindMaster(status.OwnerId);
            return sourceId == actor.Id;
        }

        private int FindMaster(int unitId)
        {
            return unitById.TryGetValue(unitId, out FightBattleUnitState unit) ? unit.MasterId : 0;
        }

        private void AppendComponentInvocation(FightBattleStatusState status,
            FightBattleStatusStateTrigger trigger, FightBattleUnitState actor,
            FightBattleUnitState target, List<FightBattleStatusSkillInvocation> output,
            bool targetPositionIsHitTarget)
        {
            if (status == null || trigger == null || trigger.EntityId <= 0
                || !RollComponent(trigger.BasePercent)) return;
            FightBattleStatusStateTrigger copy = trigger.WithSource(status);
            int sourceId = ResolveComponentUnitId(status, trigger.Source, actor, target, true);
            int targetId = ResolveComponentUnitId(status, trigger.Target, actor, target, false);
            if (sourceId <= 0) sourceId = actor.Id;
            if (targetId <= 0) targetId = target.Id;
            output.Add(new FightBattleStatusSkillInvocation
            {
                Trigger = copy,
                SourceUnitId = sourceId,
                TargetUnitId = targetId,
                TargetPositionUnitId = trigger.TargetPos == 1
                    ? targetPositionIsHitTarget ? target.Id : targetId : actor.Id,
                DamageFactor = trigger.StackedCountToDamageFactor ? Math.Max(0, status.StackCount) : 1.0
            });
        }

        private int ResolveComponentUnitId(FightBattleStatusState status, int targetType,
            FightBattleUnitState actor, FightBattleUnitState target, bool source)
        {
            if (targetType == 0) return source ? actor.Id : target.Id;
            if (targetType == 5) return target.Id;
            int resolved = FightStatusLifecycle.ResolveTargetUnitId(status, targetType, units);
            return resolved > 0 ? resolved : source ? actor.Id : target.Id;
        }

        private void ApplyComponentStatuses(FightBattleStatusState source,
            List<FightBattleStatusStateTrigger> triggers, FightBattleUnitState actor,
            FightBattleUnitState target, long customParam = 0)
        {
            if (triggers == null) return;
            foreach (FightBattleStatusStateTrigger trigger in triggers)
            {
                if (trigger == null || trigger.EntityId <= 0 || trigger.AffectedByProp
                    || !RollComponent(trigger.BasePercent)) continue;
                FightBattleUnitState destination = trigger.ApplyTarget == 1 ? actor
                    : trigger.ApplyTarget == 3 ? ResolveUnit(source.CreatorId) : target;
                ApplyComponentStatus(source, trigger.EntityId, destination, null,
                    trigger.StatusReplacementProperties, customParam);
            }
        }

        private void ApplyComponentStatus(FightBattleStatusState source, int classId,
            FightBattleUnitState destination, FightBattleSkillDefinition skill,
            List<FightStatusReplacementValue> replacements = null, long customParam = 0)
        {
            if (source == null || classId <= 0 || destination == null || statusCatalog == null
                || destination.Hp <= 0 && !statusCatalog.HasBackToLifeComponent(classId)) return;
            int instanceId = 1;
            foreach (FightBattleStatusState existing in destination.Statuses)
                if (existing != null && existing.InstanceId >= instanceId) instanceId = existing.InstanceId + 1;
            FightBattleStatusState template = statusCatalog.CreateStatus(classId, destination.Id,
                source.CreatorId > 0 ? source.CreatorId : source.OwnerId, instanceId,
                source.RootSkillId, source.ApplyingSkillItemId, source.SourceSkillRank,
                source.SourceSkillLevel, source.SourceSubRank, source.SourceProperties, customParam,
                replacements ?? source.ReplacementProperties, source.BlessPropertyScales);
            template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
            template.Origin = FightBattleStatusOrigin.Resolve(source).Copy();
            FightStatusLifecycle.Apply(destination, template, -1, instanceId, random);
        }

        private FightBattleUnitState ResolveUnit(int id)
        {
            return id > 0 && unitById.TryGetValue(id, out FightBattleUnitState unit) ? unit : null;
        }

        private bool RollComponent(float rate)
        {
            if (rate >= 1f) return true;
            if (rate <= 0f) return false;
            return random == null || random.Next() <= rate;
        }

        private static void DisperseActionStatuses(FightBattleUnitState target, List<int> actions)
        {
            if (target?.Statuses == null || actions == null) return;
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(target.Statuses))
                if (status != null && actions.Contains(status.ActionType))
                    FightStatusLifecycle.End(target, status);
        }

        private void ApplyMissingStatusStart(FightBattleUnitState owner,
            FightBattleStatusState status)
        {
            if (statusCatalog == null || owner == null || status == null
                || status.Components?.Start == null) return;
            foreach (FightBattleStatusStateTrigger trigger in status.Components.Start.Statuses)
            {
                if (trigger == null || !RollComponent(trigger.BasePercent)) continue;
                FightBattleUnitState target = trigger.ApplyTarget == 1
                    ? ResolveUnit(status.CreatorId) : owner;
                ApplyComponentStatus(status, trigger.EntityId, target, null,
                    trigger.StatusReplacementProperties);
            }
            if (status.Components.Start.ForbiddenStatusId > 0
                && status.Components.Start.Forbidden)
            {
                FightBattleUnitState target = status.Components.Start.ForbiddenTarget == 1
                    ? ResolveUnit(status.CreatorId) : owner;
                FightBattleStatusState forbidden = target?.Statuses?.Find(value => value != null
                    && value.ClassId == status.Components.Start.ForbiddenStatusId);
                if (forbidden != null) forbidden.ForbiddenType = 1;
            }
        }

        private void ApplyMissingStatusStackChange(FightBattleStatusState changed)
        {
            if (changed?.ClassId == 25131) return;
            FightBattleStatusStackChangeComponentState component = changed?.Components?.StackChange;
            if (component == null || component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount)
                return;
            foreach (FightBattleStatusStackChangeRule entry in component.Changes)
            {
                if (entry == null || entry.StatusId <= 0 || entry.ChangeValue == 0) continue;
                FightBattleUnitState owner = ResolveUnit(changed.OwnerId);
                FightBattleStatusState target = owner?.Statuses?.Find(value => value != null
                    && value.ClassId == entry.StatusId);
                if (target == null) continue;
                FightStatusLifecycle.AddStack(owner, target, entry.ChangeValue);
            }
            component.TriggeredCount++;
        }

        private void ExecuteMissingRoundInterval(FightBattleUnitState owner,
            FightBattleUnitState listener)
        {
            if (movementStartContext == null || statusCatalog == null || owner?.Statuses == null)
                return;
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(owner.Statuses))
            {
                FightBattleStatusRoundIntervalComponentState component = status?.Components?.RoundInterval;
                if (component == null || component.HasUnsupportedFilter
                    || FightStatusLifecycle.ResolveTargetUnitId(status, units) != listener?.Id)
                    continue;
                if (component.ExcludedApplyCharacterTypes != null
                    && component.ExcludedApplyCharacterTypes.Contains(owner.CharacterType)) continue;
                int round = listener.SkillState == null ? turns.GlobalRound() : listener.SkillState.Round;
                if (component.LastTriggeredRound == round) continue;
                // The two official round-start phases share one listener clock. Consume the
                // probability once so a failed phase-0 roll cannot be retried in phase 5.
                component.LastTriggeredRound = round;
                if (!RollComponent(component.Rate)) continue;
                if (component.RoundIntervalPercent != 0f && owner.Turn != null)
                    turns.ChangeInterval(owner.Id, component.FastForward
                        ? component.RoundIntervalPercent : -component.RoundIntervalPercent);
                var invocations = new List<FightBattleStatusSkillInvocation>();
                foreach (FightBattleStatusStateTrigger trigger in component.SkillTriggers)
                    AppendComponentInvocation(status, trigger, owner, listener, invocations, true);
                component.TriggeredCount++;
                if (invocations.Count == 0) continue;
                float delay = 0f;
                ExecuteConfiguredStatusSkills(invocations, movementStartContext.Casts,
                    movementStartContext.DamageEvents, movementStartContext.Movements, ref delay,
                    out float duration, out float actionDuration);
                movementStartContext.Duration = Math.Max(movementStartContext.Duration,
                    Math.Max(delay, duration));
                movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration,
                    actionDuration);
            }
        }

        /// <summary>
        /// Applies the official FightStatusHitSummonComponent at the two summon lifecycle
        /// boundaries that are observable by the offline battle loop. Unknown filter records
        /// remain disabled until their official query semantics have been recovered.
        /// </summary>
        private void ExecuteMissingHitSummon(FightBattleUnitState summon, bool created)
        {
            if (statusCatalog == null || summon == null || summon.MasterId <= 0
                || summon.Statuses == null) return;
            FightBattleUnitState owner = ResolveUnit(summon.MasterId);
            if (owner?.Statuses == null) return;
            summon.HitSummonHasLifeDuration |= HasSummonLifeDuration(summon);
            var invocations = new List<FightBattleStatusSkillInvocation>();
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(owner.Statuses))
            {
                FightBattleStatusHitSummonComponentState component = status?.Components?.HitSummon;
                if (component == null || component.HasUnsupportedFilter
                    || status.ForbiddenType != 0 || !owner.Statuses.Contains(status)
                    || (created ? !component.Create : !component.Remove)
                    || component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount)
                    continue;
                if (component.FilterSubstitute && summon.IsVoodooDoll) continue;
                if (component.CheckSummonLifeDuration
                    && !summon.HitSummonHasLifeDuration) continue;
                if (!MatchesSummonEntityTags(component, summon)) continue;
                if (!RollComponent(component.Rate)) continue;

                component.TriggeredCount++;
                if (created && component.SummonStatusId > 0)
                    ApplyComponentStatus(status, component.SummonStatusId, summon, null);
                if (component.SkillId > 0)
                {
                    AppendComponentInvocation(status, new FightBattleStatusStateTrigger
                    {
                        EntityId = component.SkillId,
                        Source = 0,
                        Target = 0,
                        TargetPos = created ? 1 : 0
                    }, owner, summon, invocations, created);
                    if (!created && invocations.Count > 0)
                    {
                        // The confirmed offline rule casts from the summoner's unit while
                        // retaining the removed summon as the position anchor. Target selection
                        // therefore follows the skill's own side/action filters around that cell.
                        invocations[invocations.Count - 1].SelectTargets = true;
                        invocations[invocations.Count - 1].UseTargetPositionWithSelection = true;
                        invocations[invocations.Count - 1].AllowDeadTarget = true;
                        invocations[invocations.Count - 1].TargetPositionUnitId = summon.Id;
                        invocations[invocations.Count - 1].Delay = movementStartContext == null
                            ? 0f : Math.Max(0f, movementStartContext.StatusStartTime);
                    }
                }
            }
            if (invocations.Count == 0 || movementStartContext == null) return;
            float delay = 0f;
            ExecuteConfiguredStatusSkills(invocations, movementStartContext.Casts,
                movementStartContext.DamageEvents, movementStartContext.Movements, ref delay,
                out float duration, out float actionDuration);
            movementStartContext.Duration = Math.Max(movementStartContext.Duration,
                Math.Max(delay, duration));
            movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration,
                actionDuration);
        }

        private bool MatchesSummonEntityTags(FightBattleStatusHitSummonComponentState component,
            FightBattleUnitState summon)
        {
            if (!component.HasSummonEntityTagFilter) return true;
            // The confirmed offline binding is the newly created role entity, not its skill.
            int entityId = summon.MonsterTemplateId > 0 ? summon.MonsterTemplateId
                : statusCatalog.MonsterTemplateId(summon.MonsterId);
            if (!statusCatalog.TryGetEntityGameplayTags(entityId, out var tags)
                || component.SummonEntityTagQuery == null) return false;
            foreach (var clause in component.SummonEntityTagQuery)
                if (!clause.Matches(tags)) return false;
            return true;
        }

        private void RestoreRoundIntervalDefinition(FightBattleStatusState status)
        {
            var previous = status?.Components?.RoundInterval;
            if (previous == null || previous.DefinitionResolved || statusCatalog == null
                || !previous.HasUnsupportedFilter && previous.RoundIntervalPercent == 0f
                    && (previous.SkillTriggers == null || previous.SkillTriggers.Count == 0)) return;
            var template = statusCatalog.CreateStatus(status.ClassId, status.OwnerId, status.CreatorId,
                status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId, status.SourceSkillRank,
                status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties,
                blessPropertyScales: status.BlessPropertyScales);
            var definition = template.Components?.RoundInterval;
            if (definition == null) return;
            definition.TriggeredCount = previous.TriggeredCount;
            definition.LastTriggeredRound = previous.LastTriggeredRound;
            status.Components.RoundInterval = definition;
        }

        private void RestoreHitSummonDefinition(FightBattleStatusState status)
        {
            var previous = status?.Components?.HitSummon;
            if (previous == null || previous.DefinitionResolved
                || !previous.Create && !previous.Remove || statusCatalog == null) return;
            var template = statusCatalog.CreateStatus(status.ClassId, status.OwnerId, status.CreatorId,
                status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId, status.SourceSkillRank,
                status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties,
                blessPropertyScales: status.BlessPropertyScales);
            var definition = template.Components?.HitSummon;
            if (definition == null) return;
            definition.TriggeredCount = previous.TriggeredCount;
            status.Components.HitSummon = definition;
        }

        private static bool HasSummonLifeDuration(FightBattleUnitState summon)
        {
            if (summon?.Statuses == null) return false;
            foreach (FightBattleStatusState status in summon.Statuses)
                if (status != null && status.RemoveApplyEntityOnEnd
                    && status.DurationRound >= 0) return true;
            return false;
        }

        private void ApplyMissingHitSummonRemovalTriggers(FightBattleUnitState settledUnit = null)
        {
            if (movementStartContext == null) return;
            foreach (FightBattleUnitState summon in new List<FightBattleUnitState>(units))
            {
                if (summon == null || summon.MasterId <= 0 || summon.MonsterId <= 0
                    || settledUnit != null && summon != settledUnit
                    || summon.Hp > 0 && !summon.Ended || summon.HitSummonRemoveProcessed) continue;
                summon.HitSummonRemoveProcessed = true;
                ExecuteMissingHitSummon(summon, false);
            }
        }
    }
}
