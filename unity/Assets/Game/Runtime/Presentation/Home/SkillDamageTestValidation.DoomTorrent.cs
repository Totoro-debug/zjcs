using System;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredDoomTorrent(FightBattleStepEvent step, FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, ref int pushes)
        {
            var delays = new[] { .6f, 1f, 1.4f }.SelectMany(wave =>
                new[] { .08f, .24f, .34f, .5f, .74f }.Select(row => wave + row)).ToArray();
            Require(hits.Length >= 3 && hits.All(hit => hit.SourceId == 1 && hit.TargetId != 1
                && hit.SkillItemId == 10056 && hit.HitEntityId == 11081 && hit.DamageEntityId == 11082
                && hit.ElementType == 101 && hit.DamageType == 1 && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure
                && delays.Any(delay => Math.Abs(hit.Delay - cast.Delay - delay) < .0001f)),
                "The cultivated doom torrent lost its original three water waves or row timing.");
            foreach (var move in step.Movements.Where(move => move.UnitId != 1 && move.MoveBehaviourId == 11083))
            {
                Require(move.Path.Count == 2 && Math.Abs(move.MoveDuration - .25f) < .0001f
                    && hits.Any(hit => hit.TargetId == move.UnitId && Math.Abs(move.Delay - hit.Delay - .05f) < .0001f),
                    "The cultivated doom torrent lost its original post-hit knockback timing.");
                if (!move.Path[0].Equals(move.Path[1])) pushes++;
            }
        }
    }
}
