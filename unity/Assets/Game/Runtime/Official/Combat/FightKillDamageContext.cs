using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleKillDamageScale
    {
        public int GameCharacterType;
        public int Scale;

        public static List<FightBattleKillDamageScale> CopyAll(IList<FightBattleKillDamageScale> source)
        {
            var result = new List<FightBattleKillDamageScale>();
            foreach (var row in source ?? new List<FightBattleKillDamageScale>())
                if (row != null) result.Add(new FightBattleKillDamageScale
                    { GameCharacterType = row.GameCharacterType, Scale = row.Scale });
            return result;
        }
    }

    // The original FightKillEventArgs carries separate damage, overflow and
    // damage-position values. Freeze them before death responses move a unit.
    public sealed class FightKillDamageContext
    {
        public readonly bool KilledTarget, IsPvp, HasDamagePosition;
        public readonly long Damage, OverflowDamage;
        public readonly FightGridPoint DamagePosition;
        public readonly float Delay;
        public readonly int FacingIndex;
        public readonly bool FlipX;
        private readonly FightDamageFormula formula;

        public FightKillDamageContext(FightBattleDamageEvent hit, FightBattleSkillCastPresentation cast,
            bool isPvp, FightDamageFormula formula)
        {
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
            KilledTarget = !hit.IsCure && !hit.IsSkillCost && hit.HasDamageValue
                && hit.BeforeHp > 0 && hit.AfterHp <= 0;
            IsPvp = isPvp;
            HasDamagePosition = hit.HasDamagePosition;
            DamagePosition = new FightGridPoint(hit.DamageX, hit.DamageY);
            Delay = hit.Delay;
            FacingIndex = cast?.FacingIndex ?? 0;
            FlipX = cast?.FlipX ?? false;
            if (!KilledTarget) return;
            decimal magnitude = Math.Abs((decimal)hit.DamageValue);
            Damage = Clamp(magnitude);
            // User-confirmed offline rule: DamageValue is already after shields.
            // Exact lethal damage contributes zero overflow.
            OverflowDamage = Clamp(magnitude - hit.BeforeHp);
        }

        public long Resolve(FightBattleStatusState status, FightBattleUnitState defeated)
        {
            long amount = status.KillSkillCustomOverflowDamage ? OverflowDamage : Damage;
            if (status.KillSkillUseCustomDamage)
            {
                var properties = new Dictionary<int, long>();
                foreach (var value in status.StatusProperties ?? status.StatusConfigProperties
                    ?? status.ResolvedProperties ?? new List<FightPropertyValue>())
                    if (value != null) properties[value.Id] = value.Value;
                amount = -formula.CalculateStatusDamage(properties,
                    status.KillSkillCustomDamagePropertyId, 0, amount);
            }
            // User-confirmed PVP order: calculate/truncate the status multiplier
            // first, then apply the original character-type scale and truncate.
            if (IsPvp && status.KillSkillScaleDamageByCharacterTypeOnPvp)
                foreach (var row in status.KillSkillCharacterTypeDamageScales
                    ?? new List<FightBattleKillDamageScale>())
                    if (row != null && row.GameCharacterType == defeated.CharacterType)
                        return Clamp((decimal)amount * row.Scale / 10000m);
            return amount;
        }

        private static long Clamp(decimal value) => (long)Math.Max(0m, Math.Min(long.MaxValue, value));
    }
}
