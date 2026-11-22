using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredBattlecry(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output, ref int advances)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var active = actor.Skills.Single(skill => skill.Slot == 0);
            var buff = actor.Statuses.Single(status => status.ClassId == 24192 && status.CreatorId == actor.Id);
            Require(buff.ApplyingSkillItemId == 10092 && buff.SourceSkillLevel == 2
                && buff.SourceSkillRank == active.Rank && buff.SourceSubRank == 8
                && buff.DurationRound == 2 && buff.RoundTarget == 2 && buff.RoundUpdateTiming == 1,
                "The cultivated Battlecry lost its original attack status or beneficiary clock.");
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            var props = combat.CalculateSkillProps(24192, active.Rank, active.Level, active.SubRank);
            long sourceAttack = buff.SourceProperties.Single(value => value.Id == 1).Value;
            long bonus = (long)(sourceAttack * (decimal)props[2000] / 10000m + props[2010]);
            Require(buff.ResolvedProperties.Single(value => value.Id == 1).Value == bonus
                && step.DamageEvents.Where(hit => hit.SkillEntityId == 12010).All(hit => hit.Damage == 0),
                "The cultivated Battlecry lost its fixed caster snapshot or inflicted damage.");
            var advance = actor.Statuses.SingleOrDefault(status => status.ClassId == 24384);
            if (advance == null) return;
            var entry = checkpoint.Timeline.Entries.Single(value => value.Id == actor.Id);
            long expected = checkpoint.Timeline.Current + actor.Turn.IntervalTicks
                - (long)Math.Round((double)0.2f * actor.Turn.IntervalTicks, MidpointRounding.AwayFromZero);
            Require(advance.Components.RoundInterval.TriggeredCount > 0 && entry.Next == expected,
                "The cultivated Battlecry did not immediately advance the next action by 20%.");
            advances++;
            File.WriteAllText(Path.Combine(output, "acquired-battlecry-checkpoint.json"),
                FightBattleCheckpointSerializer.Save(checkpoint));
        }
    }
}
