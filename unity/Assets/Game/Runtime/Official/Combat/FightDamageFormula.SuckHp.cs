using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // BattleFormula.SuckHpInfo: the passive and damaging skill have separate props.
    public sealed class FightSuckHpFormulaRequest
    {
        public IDictionary<int, long> SourceProps;
        public int SourceType;
        public int SourceMasterType;
        public IDictionary<int, long> SkillProps;
        public int SuckPropType;
        public int FixedSuckPropType;
        public long Damage;
        public IDictionary<int, long> DamageSkillProps;
        public int DamageHitPropType;
    }

    public sealed partial class FightDamageFormula
    {
        // Pinned BattleFormulaHandler.SuckHp; event selection belongs to the caller.
        public long CalculateSuckHp(FightSuckHpFormulaRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            double amount = (double)Math.Abs(request.Damage)
                * Prop(request.SkillProps, request.SuckPropType);
            double fixedAmount = Prop(request.SkillProps, request.FixedSuckPropType)
                * Prop(request.DamageSkillProps, request.DamageHitPropType);
            if (IsCharacter(request.SourceType, request.SourceMasterType))
            {
                fixedAmount /= OneAdd(request.SourceProps, "PlayerSkillDmgReduceScale", 1.0);
                fixedAmount /= OneAdd(request.SourceProps, "ProSkillDmgReduceScale", 1.0);
            }
            amount += fixedAmount;
            amount *= 1.0 + SuckHpCureAdd(request.SourceProps);
            return (long)Math.Max(0.0, amount);
        }

        // Pinned BattleFormulaHandler.SuckHpByOnceHit (without FinalCureScale).
        public long CalculateSuckHpByOnceHit(long damage,
            IDictionary<int, long> sourceProps, int customPropType)
        {
            damage = Math.Abs(damage);
            double amount = (double)damage * Prop(sourceProps, customPropType);
            amount *= 1.0 + SuckHpCureAdd(sourceProps);
            return (long)Math.Max(0.0, amount);
        }

        private double SuckHpCureAdd(IDictionary<int, long> source)
        {
            // Keep the original divisions, including their IEEE zero-base behavior.
            double add = Prop(source, "CureAddPercent") + Prop(source, "BeCureAddPercent");
            double value = add + Prop(source, "CureAdd") / Prop(source, "BaseCureAdd")
                + Prop(source, "BeCureAdd") / Prop(source, "BaseBeCureAdd");
            if (value <= config.CureAddSoftCapThreshold) return value;
            return config.CureAddSoftCapThreshold
                + (value - config.CureAddSoftCapThreshold) * config.CureAddOverflowScale / value;
        }
    }
}
