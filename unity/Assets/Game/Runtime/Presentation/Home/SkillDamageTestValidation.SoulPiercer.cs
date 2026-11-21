using System;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredSoulPiercer(FightBattleStepEvent step, FightBattleDamageEvent[] strikes,
            FightBattleUnitState actor, float delay, ref int cures)
        {
            Require(strikes.Length > 0 && strikes.All(hit => hit.TargetId != actor.Id && hit.HitEntityId == 11921
                && hit.DamageEntityId == 10027 && hit.ElementType == 104 && hit.DamageType == 1 && hit.Damage < 0
                && !hit.TrueDamage && !hit.IsCure && Math.Abs(hit.Delay - delay - .57f) < .0001f),
                "The acquired soul piercer lost its original dark hit.");
            var sources = strikes.Where(hit => (hit.BeforeHp - hit.AfterHp) / 10 > 0).ToArray();
            var healing = step.DamageEvents.Where(hit => hit.SourceId == actor.Id && hit.SkillItemId == 10089
                && hit.IsCure && hit.SkillEntityId == 10110).ToArray();
            Require(sources.Length == healing.Length, "The acquired soul piercer lost a native lifesteal child.");
            for (int i = 0; i < healing.Length; i++)
            {
                var cure = healing[i];
                Require(cure.TargetId == actor.Id && cure.HitEntityId == 10111 && cure.DamageEntityId == 10028
                    && Math.Abs(cure.Delay - sources[i].Delay) < .0001f && cure.AfterHp <= actor.MaxHp,
                    "The acquired soul piercer lost lifesteal ownership, timing or maximum-HP cap.");
                if (cure.AfterHp > cure.BeforeHp) cures++;
            }
        }
    }
}
