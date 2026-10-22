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
            foreach (FightBattleStatusState status in statuses)
            {
                if (status == null) continue;
                int statusKey = StatusKey(status);
                if (status.ClassId <= 0 || status.FalloffTarget == 0
                    || Contains(processed, statusKey)) continue;
                int scope = status.Scope == 1 ? 1 : 0;
                if (scope == 0 && status.OwnerId != target.Id) continue;
                if (status.DamageId > 0 && status.DamageId != hit.DamageId) continue;
                if (status.RootSkillIds != null && status.RootSkillIds.Length > 0
                    && !Contains(status.RootSkillIds, RootSkillId(skill))) continue;
                if ((status.FalloffTarget & 1) != 0
                    && (status.RootSkillId <= 0 || status.RootSkillId != RootSkillId(skill))) continue;
                if ((status.FalloffTarget & 2) != 0
                    && (status.SkillEntityClassId <= 0 || status.SkillEntityClassId != ItemId(skill))) continue;
                if ((status.FalloffTarget & 4) != 0 && status.CreatorId != source.Id) continue;
                if ((status.FalloffTarget & 8) != 0 && status.ApplyingSkillItemId != ItemId(skill)) continue;
                if (status.IsCheckSkillTrigger && skill != null && !skill.CanTriggerChild) continue;

                processed.Add(statusKey);
                if (status.Rate <= 0 || (status.Rate < 1.0 && (random == null || random.Next() >= status.Rate)))
                    continue;

                List<FightBattleFalloffCounter> counters;
                int castId;
                if (skill != null && skill.FalloffCastId > 0)
                {
                    castId = skill.FalloffCastId;
                    counters = scope == 1 ? skill.FalloffCounts : target.FalloffCounts;
                }
                else
                {
                    castId = 0;
                    counters = status.Counts;
                }
                if (counters == null) continue;
                FightBattleFalloffCounter counter = FindCounter(counters, castId, status.ClassId);
                int count = counter == null ? 0 : counter.Count;
                int factor = Math.Max(0, count + 1 - status.NumOfStart);
                if (status.MaxFalloffCount >= 0) factor = Math.Min(factor, status.MaxFalloffCount);
                if (counter == null)
                {
                    counter = new FightBattleFalloffCounter { CastId = castId, StatusClassId = status.ClassId };
                    counters.Add(counter);
                }
                counter.Count = count + 1;
                float percent = (float)(status.FalloffPercent / 10000.0);
                float retained = 1.0f - percent;
                amount = (long)(amount * Math.Pow(retained, factor));
            }
        }

        private static int RootSkillId(FightBattleSkillDefinition skill)
        {
            return skill == null ? 0 : skill.RootEntityId > 0 ? skill.RootEntityId : skill.EntityId;
        }

        private static int ItemId(FightBattleSkillDefinition skill) => skill == null ? 0 : skill.ItemId;

        private static int StatusKey(FightBattleStatusState status)
        {
            return status.InstanceId > 0 ? status.InstanceId : -status.ClassId;
        }

        private static FightBattleFalloffCounter FindCounter(List<FightBattleFalloffCounter> counters,
            int castId, int statusClassId)
        {
            for (int i = 0; i < counters.Count; i++)
                if (counters[i] != null && counters[i].CastId == castId
                    && counters[i].StatusClassId == statusClassId) return counters[i];
            return null;
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

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
                if (values[i] == value) return true;
            return false;
        }
    }
}
