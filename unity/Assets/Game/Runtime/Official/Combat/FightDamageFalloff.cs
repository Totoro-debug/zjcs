using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightDamageFalloff
    {
        public static void BeginCast(FightBattleSkillDefinition skill, ref int castSerial)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            unchecked { castSerial++; }
            if (castSerial <= 0) castSerial = 1;
            skill.FalloffCastId = castSerial;
            if (skill.FalloffCounts == null) skill.FalloffCounts = new List<FightBattleFalloffCounter>();
            skill.FalloffCounts.Clear();
        }

        public static long Apply(FightRandomStream random, FightBattleUnitState source,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, long amount, List<int> processedStatuses = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            var processed = processedStatuses ?? new List<int>();
            long result = amount;
            ApplyStatuses(random, source, target, skill, hit, source.Statuses, processed, ref result);
            if (target.Id != source.Id)
                ApplyStatuses(random, source, target, skill, hit, target.Statuses, processed, ref result);
            return result;
        }

        /// <summary>Counts the hit which first applied a same-cast, per-target falloff status.</summary>
        public static void RecordApplyingHit(FightBattleUnitState source, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            FightBattleStatusState status, FightBattleDamageEvent damage)
        {
            if (source != null && target != null && skill != null && hit != null && status != null && damage != null
                && !damage.IsCure && !damage.Dodge && !damage.Blinding && !damage.Immunity
                && status.OwnerId == target.Id && status.Scope != 1 && status.Rate >= 1.0
                && FightDamageFalloffRules.UsesLifetimeCounter(status))
            {
                foreach (var counter in status.Counts)
                    if (counter != null && counter.CastId == 0 && counter.StatusClassId == status.ClassId) return;
                FightDamageFalloffRules.For(status).Apply(new FightDamageFalloffContext {
                    Source = source, Target = target, Skill = skill, Hit = hit, Status = status }, 0);
                return;
            }
            if (source == null || target == null || skill == null || hit == null || status == null || damage == null
                || damage.IsCure || damage.Dodge || damage.Blinding || damage.Immunity
                || skill.FalloffCastId <= 0 || status.Scope == 1 || status.OwnerId != target.Id
                || status.FalloffTarget != 1 || !status.RemoveOnSourceSkillStart || !(status.Rate >= 1.0)
                || status.DamageId != 0 || status.RootSkillIds == null || status.RootSkillIds.Length == 0
                || target.FalloffCounts == null) return;
            foreach (var counter in target.FalloffCounts)
                if (counter != null && counter.CastId == skill.FalloffCastId && counter.StatusClassId == status.ClassId)
                    return;
            // The original status is installed after this hit settles. Count that hit once
            // so the next occurrence observes the configured starting count.
            FightDamageFalloffRules.For(status).Apply(new FightDamageFalloffContext {
                Source = source, Target = target, Skill = skill, Hit = hit, Status = status }, 0);
        }

        public static void ResetOnSkillStart(IEnumerable<FightBattleUnitState> units, int rootSkillId)
        {
            if (units == null || rootSkillId <= 0) return;
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null || unit.Statuses == null) continue;
                foreach (FightBattleStatusState status in unit.Statuses)
                {
                    if (status == null || !status.RemoveOnSourceSkillStart || status.RootSkillId != rootSkillId)
                        continue;
                    RemoveCounter(status.Counts, 0, 0);
                }
            }
        }

        public static void EndCast(IEnumerable<FightBattleUnitState> units, FightBattleSkillDefinition skill)
        {
            if (skill == null || skill.FalloffCastId <= 0) return;
            int castId = skill.FalloffCastId;
            if (skill.FalloffCounts != null) RemoveCast(skill.FalloffCounts, castId);
            if (units == null) return;
            foreach (FightBattleUnitState unit in units)
                if (unit != null) RemoveCast(unit.FalloffCounts, castId);
            skill.FalloffCastId = 0;
        }

        private static void ApplyStatuses(FightRandomStream random, FightBattleUnitState source,
            FightBattleUnitState target, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, List<FightBattleStatusState> statuses,
            List<int> processed, ref long amount)
        {
            if (statuses == null) return;
            foreach (var status in statuses)
                if (status != null)
                    amount = FightDamageFalloffRules.For(status).Apply(new FightDamageFalloffContext
                    {
                        Source = source, Target = target, Skill = skill, Hit = hit,
                        Status = status, Random = random, ProcessedStatuses = processed
                    }, amount);
        }

        private static void RemoveCounter(List<FightBattleFalloffCounter> counters, int castId, int statusClassId)
        {
            if (counters == null) return;
            for (int i = counters.Count - 1; i >= 0; i--)
                if (counters[i] != null && counters[i].CastId == castId
                    && (statusClassId == 0 || counters[i].StatusClassId == statusClassId))
                    counters.RemoveAt(i);
        }

        private static void RemoveCast(List<FightBattleFalloffCounter> counters, int castId)
        {
            if (counters == null) return;
            for (int i = counters.Count - 1; i >= 0; i--)
                if (counters[i] != null && counters[i].CastId == castId) counters.RemoveAt(i);
        }


    }
}
