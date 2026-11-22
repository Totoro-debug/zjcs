using System;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredJudgmentThunder(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits)
        {
            Require(hits.Length == 3 && hits.Select(hit => hit.TargetId).Distinct().Count() == 1
                && hits.All(hit => hit.SourceId == 1 && hit.TargetId != 1 && hit.SkillItemId == 10093
                    && hit.HitEntityId == 11971 && hit.DamageEntityId == 11972 && hit.ElementType == 103
                    && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure),
                "The cultivated judgment thunder lost its three ordinary light hits on one target.");
            var times = hits.Select(hit => hit.Delay - cast.Delay).OrderBy(value => value).ToArray();
            Require(times.Zip(new[] { .85f, 1.05f, 1.2f },
                (actual, expected) => Math.Abs(actual - expected) < .0001f).All(value => value),
                "The cultivated judgment thunder lost its original lightning hit times.");
        }
    }
}
