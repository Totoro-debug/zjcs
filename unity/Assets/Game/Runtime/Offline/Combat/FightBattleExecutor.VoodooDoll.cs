using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private FightBattleUnitState CreateVoodooDoll(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, FightBattleSummonPool pool,
            FightBattleUnitState target, FightGridPoint position)
        {
            FightVoodooDollDefinition config = statusCatalog.VoodooDollDefinition(pool.MonsterId);
            var source = new Dictionary<int, long>();
            foreach (FightPropertyValue property in target.Properties) source[property.Id] = property.Value;
            var inheritance = statusCatalog.CalculateSummonInheritance(pool.MonsterId,
                skill.Level, skill.Rank, skill.SubRank, source);
            var properties = CopyProperties(inheritance.Fixed);
            foreach (var pair in inheritance.Summoner)
                properties[pair.Key] = checked(Property(properties, pair.Key) + pair.Value);
            int hpId = formula.PropertyId("MaxHp");
            properties[hpId] = Math.Max(1, Property(properties, hpId));
            var doll = new FightBattleUnitState {
                Id = NextSummonUnitId(), MasterId = actor.Id, Side = target.Side,
                CharacterType = target.CharacterType, MonsterId = pool.MonsterId,
                Level = skill.Level, SubRank = skill.SubRank, Profession = target.Profession,
                Wave = actor.Wave, X = position.X, Y = position.Y, Direction = target.Direction,
                Hp = properties[hpId], MaxHp = properties[hpId], LastMaxHp = properties[hpId],
                BaseProperties = ToPropertyList(properties), Properties = ToPropertyList(properties),
                BodyOffsets = config.Body, MoveMask = config.MoveMask,
                VoodooDoll = new FightVoodooDollLink { TargetId = target.Id,
                    SyncSkillEntityId = config.SyncSkillEntityId, VoodooStatusId = config.VoodooStatusId,
                    FollowDieStatusId = config.FollowDieStatusId, SyncHitEntityId = config.SyncHitEntityId,
                    SyncDamageEntityId = config.SyncDamageEntityId }
            };
            doll.Turn = new FightTurnUnit { Id = doll.Id, MasterId = doll.MasterId,
                Hp = doll.Hp, Speed = (int)Math.Max(1, Property(properties, formula.PropertyId("Speed"))),
                RoundType = config.RoundType };
            return doll;
        }

        private void TransmitVoodooDamage(FightBattleUnitState doll, FightBattleDamageEvent suffered,
            List<FightBattleDamageEvent> events, List<FightBattleSkillCastEvent> casts,
            HashSet<int> visited = null)
        {
            if (!doll.IsVoodooDoll) return;
            if (visited == null) visited = new HashSet<int>();
            if (!visited.Add(doll.Id)) throw new InvalidOperationException("Cyclic substitute link.");
            if (!unitById.TryGetValue(doll.VoodooDoll.TargetId, out var target)) return;
            FightBattleDamageEvent damage = FightVoodooDollRules.Transmit(doll, target, suffered,
                NextDamageId());
            if (damage == null) return;
            var syncTriggerContext = statusCatalog?.CreateSkillTriggerContext(damage.SkillEntityId);
            var syncSkill = statusCatalog.CreateSkill(damage.SkillEntityId, -1, 0, 1,
                doll.Level, doll.SubRank, null);
            var prepared = SkillCasts.PrepareRecordedHit(doll, syncSkill, target, damage);
            float localHitDelay = damage.Delay - prepared.Cast.Delay;
            prepared.Cast.Presentation.Duration = ResolveDuration(prepared.Skill, localHitDelay);
            prepared.Cast.Presentation.ActionDuration = ResolveActionDuration(prepared.Skill, localHitDelay);
            events.Add(damage);
            casts.Add(prepared.Cast);
            if (movementStartContext != null)
            {
                movementStartContext.Duration = Math.Max(movementStartContext.Duration,
                    prepared.Cast.Delay + prepared.Cast.Presentation.Duration);
                if (prepared.Cast.Presentation.ActionDuration >= 0f)
                    movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration,
                        prepared.Cast.Delay + prepared.Cast.Presentation.ActionDuration);
            }
            checkpoint.Statistics.RecordCast(doll.Id, damage.SkillEntityId, 0);
            checkpoint.Statistics.RecordHit(damage, new HashSet<int>());
            FightStatusAccumulatedDamageRuntime.RecordHit(target, damage, syncTriggerContext);
            CollectUnflinchingActions(target, Math.Max(0, damage.BeforeHp - damage.AfterHp),
                syncTriggerContext, damage.DamageId);
            FightStatusTriggerRuntime.ApplyAccumulatedDamage(target,
                Math.Max(0, damage.BeforeHp - damage.AfterHp), statusCatalog, random,
                sourceSkill: syncTriggerContext, statistics: checkpoint.Statistics);
            ApplyHpChangeTriggers(target, syncTriggerContext);
            if (target.Hp <= 0 && movementStartContext != null)
            {
                float delay = Math.Max(0f, damage.Delay), duration = 0f, actionDuration = -1f;
                ExecuteRoleDeathSkills(target, damage.Delay, casts, events, movementStartContext.Movements,
                    ref delay, ref duration, ref actionDuration);
                movementStartContext.Duration = Math.Max(movementStartContext.Duration, Math.Max(delay, duration));
                movementStartContext.ActionDuration = Math.Max(movementStartContext.ActionDuration, actionDuration);
            }
            else SynchronizeDeathForbidden(target);
            ApplyMissingHitSummonRemovalTriggers(target);
            TransmitVoodooDamage(target, damage, events, casts, visited);
        }

        private void OnNonHitHpDecreased(FightBattleUnitState unit, long amount)
        {
            var context = movementStartContext;
            if (context == null || unit == null || !unit.IsVoodooDoll || unit.Ended || amount <= 0) return;
            // Confirmed offline rule: actual HP lost outside a hit also travels
            // through 10140. Hit damage keeps its separate overflow-aware input.
            TransmitVoodooDamage(unit, new FightBattleDamageEvent {
                TargetId = unit.Id, HasDamageValue = true, DamageValue = -amount,
                BeforeHp = checked(unit.Hp + amount), AfterHp = unit.Hp, MaxHp = unit.MaxHp,
                Delay = Math.Max(0f, context.StatusStartTime)
            }, context.DamageEvents, context.Casts);
        }

        private void NormalizeVoodooDeathStatuses()
        {
            foreach (var doll in units)
            {
                if (!doll.IsVoodooDoll || doll.Ended || doll.Hp <= 0
                    || !unitById.TryGetValue(doll.VoodooDoll.TargetId, out var target)
                    || target.Ended || target.Hp <= 0) continue;
                // Legacy saves attached FollowDieId at spawn. The target is alive,
                // so remove only that source's premature death action on restore.
                foreach (var stale in new List<FightBattleStatusState>(doll.Statuses))
                    if (stale.ClassId == doll.VoodooDoll.FollowDieStatusId
                        && stale.CreatorId == target.Id && stale.ActionType == 3)
                        FightStatusLifecycle.End(doll, stale);
            }
        }

        private void FinishVoodooDolls()
        {
            foreach (FightBattleUnitState doll in units)
            {
                if (!doll.IsVoodooDoll) continue;
                unitById.TryGetValue(doll.VoodooDoll.TargetId, out var target);
                if (target == null || target.Hp <= 0 || target.Ended)
                {
                    if (target != null && target.Hp <= 0 && doll.Hp > 0 && !doll.Ended
                        && doll.VoodooDoll.FollowDieStatusId > 0 && statusCatalog != null)
                    {
                        var source = doll.Statuses.Find(status => status.CreatorId == doll.MasterId
                            && status.RootSkillId > 0 && status.ApplyingSkillItemId > 0);
                        if (source != null)
                        {
                            // FollowDieId is the linked death status, not a starting status.
                            var death = statusCatalog.CreateStatus(doll.VoodooDoll.FollowDieStatusId,
                                doll.Id, target.Id, NextStatusInstanceId(), source.RootSkillId,
                                source.ApplyingSkillItemId, source.SourceSkillRank, source.SourceSkillLevel,
                                source.SourceSubRank, target.Properties, 0, source.ReplacementProperties,
                                source.BlessPropertyScales);
                            death.CreatorCharacterType = target.CharacterType;
                            death.Origin = FightBattleStatusOrigin.Resolve(source).Copy();
                            death.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
                            FightStatusLifecycle.Apply(doll, death, StatusClockRound(death), death.InstanceId, random);
                        }
                    }
                    doll.Ended = true;
                }
                if (doll.Hp > 0 && !doll.Ended) continue;
                FightVoodooDollRules.TransferDebuffs(doll, target, units,
                    NextStatusInstanceId, random);
                if (target != null && doll.VoodooDoll.VoodooStatusInstanceId > 0)
                {
                    FightBattleStatusState mark = target.Statuses.Find(status =>
                        status.InstanceId == doll.VoodooDoll.VoodooStatusInstanceId);
                    if (mark != null) FightStatusLifecycle.End(target, mark);
                    doll.VoodooDoll.VoodooStatusInstanceId = 0;
                }
                doll.Ended = true;
            }
        }
    }
}
