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
            events.Add(damage);
            casts.Add(new FightBattleSkillCastEvent { ActorId = doll.Id, Slot = -1,
                SkillEntityId = damage.SkillEntityId, Delay = damage.Delay, TargetIds = new List<int> {target.Id} });
            checkpoint.Statistics.RecordCast(doll.Id, damage.SkillEntityId, 0);
            checkpoint.Statistics.RecordHit(damage, new HashSet<int>());
            FightStatusAccumulatedDamageRuntime.RecordHit(target, damage, syncTriggerContext);
            CollectUnflinchingActions(target, Math.Max(0, damage.BeforeHp - damage.AfterHp),
                syncTriggerContext, damage.DamageId);
            FightStatusTriggerRuntime.ApplyAccumulatedDamage(target,
                Math.Max(0, damage.BeforeHp - damage.AfterHp), statusCatalog, random,
                sourceSkill: syncTriggerContext);
            ApplyHpChangeTriggers(target, syncTriggerContext);
            SynchronizeDeathForbidden(target);
            TransmitVoodooDamage(target, damage, events, casts, visited);
        }

        private void FinishVoodooDolls()
        {
            foreach (FightBattleUnitState doll in units)
            {
                if (!doll.IsVoodooDoll) continue;
                unitById.TryGetValue(doll.VoodooDoll.TargetId, out var target);
                if (target == null || target.Hp <= 0 || target.Ended) doll.Ended = true;
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
