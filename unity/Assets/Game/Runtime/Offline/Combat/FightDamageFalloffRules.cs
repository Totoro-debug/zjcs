using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public interface IFightDamageFalloffRule
    {
        long Apply(FightDamageFalloffContext context, long amount);
    }

    public sealed class FightDamageFalloffContext
    {
        public FightBattleUnitState Source, Target;
        public FightBattleSkillDefinition Skill;
        public FightBattleHitDefinition Hit;
        public FightBattleStatusState Status;
        public FightRandomStream Random;
        public List<int> ProcessedStatuses;
    }

    // The official component retains configuration only. These adapters preserve
    // the existing offline execution, including float rounding and RNG ordering.
    public static class FightDamageFalloffRules
    {
        private static readonly IFightDamageFalloffRule PerTarget = new PerTargetRule();
        private static readonly IFightDamageFalloffRule AcrossTargets = new CastRule();

        public static IFightDamageFalloffRule For(FightBattleStatusState status)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            return status.Scope == 1 ? AcrossTargets : PerTarget;
        }

        private sealed class PerTargetRule : Rule
        {
            protected override bool Accepts(FightBattleStatusState status, FightBattleUnitState target)
                => status.OwnerId == target.Id;
            protected override List<FightBattleFalloffCounter> CastCounters(
                FightBattleSkillDefinition skill, FightBattleUnitState target) => target.FalloffCounts;
        }

        private sealed class CastRule : Rule
        {
            protected override bool Accepts(FightBattleStatusState status, FightBattleUnitState target) => true;
            protected override List<FightBattleFalloffCounter> CastCounters(
                FightBattleSkillDefinition skill, FightBattleUnitState target) => skill.FalloffCounts;
        }

        private abstract class Rule : IFightDamageFalloffRule
        {
            protected abstract bool Accepts(FightBattleStatusState status, FightBattleUnitState target);
            protected abstract List<FightBattleFalloffCounter> CastCounters(
                FightBattleSkillDefinition skill, FightBattleUnitState target);

            public long Apply(FightDamageFalloffContext context, long amount)
            {
                if (context == null || context.Source == null || context.Target == null
                    || context.Hit == null || context.Status == null)
                    throw new ArgumentException("Falloff requires source, target, hit and status.");
                var source = context.Source; var target = context.Target;
                var skill = context.Skill; var hit = context.Hit; var status = context.Status;
                var random = context.Random;
                var processed = context.ProcessedStatuses ?? new List<int>();
                int statusKey = StatusKey(status);
                if (status.ClassId <= 0 || status.FalloffTarget == 0
                    || Contains(processed, statusKey)) return amount;
                if (!Accepts(status, target)) return amount;
                if (status.DamageId > 0 && status.DamageId != hit.DamageId) return amount;
                if (status.RootSkillIds != null && status.RootSkillIds.Length > 0
                    && !Contains(status.RootSkillIds, RootSkillId(skill))) return amount;
                if ((status.FalloffTarget & 1) != 0
                    && (status.RootSkillId <= 0 || status.RootSkillId != RootSkillId(skill))) return amount;
                if ((status.FalloffTarget & 2) != 0
                    && (status.SkillEntityClassId <= 0 || status.SkillEntityClassId != ItemId(skill))) return amount;
                if ((status.FalloffTarget & 4) != 0 && status.CreatorId != source.Id) return amount;
                if ((status.FalloffTarget & 8) != 0 && status.ApplyingSkillItemId != ItemId(skill)) return amount;
                if (status.IsCheckSkillTrigger
                    && !FightSkillTriggerPolicy.AllowsStatus(skill, status)) return amount;

                processed.Add(statusKey);
                if (status.Rate <= 0 || (status.Rate < 1.0 && (random == null || random.Next() >= status.Rate)))
                    return amount;

                List<FightBattleFalloffCounter> counters;
                int castId;
                if (skill != null && skill.FalloffCastId > 0)
                {
                    castId = skill.FalloffCastId;
                    counters = CastCounters(skill, target);
                }
                else
                {
                    castId = 0;
                    counters = status.Counts;
                }
                if (counters == null) return amount;
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
                return amount;
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

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++)
                if (values[i] == value) return true;
            return false;
        }
    }
}
