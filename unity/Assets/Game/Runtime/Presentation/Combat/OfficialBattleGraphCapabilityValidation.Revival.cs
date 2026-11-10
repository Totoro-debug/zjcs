using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateFatalRevival => ValidateRevival
            && Environment.GetEnvironmentVariable("ZJCS_REVIVAL_FATAL") == "1";
        private float fatalRevivalAttackClock;
        private int fatalRevivalAttackActor;
        private bool fatalRevivalHitCaptured, fatalRevivalObserved;

        private void ConfigureFatalRevival(FightBattleUnitState player, FightBattleUnitState enemy)
        {
            // Original 3331 group 0 attack 14678 -> 59460 -> 59461. Only the
            // isolated roster's HP/speed is adjusted to expose the fatal boundary.
            Require(catalog.MonsterActiveSkillItems(3331, 0).Contains(14678),
                "Original monster attack binding changed.");
            var attack = catalog.CreateSkill(59460, 0, 14678, 2, 20, player.SubRank,
                catalog.CalculateSkillProps(59460, 2, 20, player.SubRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            attack.LimitedTimes = 1;
            enemy.Skills.Add(attack);
            enemy.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            enemy.Turn.Speed = 2000;
            foreach (var value in enemy.Properties.Concat(enemy.BaseProperties ?? enemy.Properties))
                if (value.Id == catalog.PropertyType("Speed")) value.Value = 2000;
            enemy.BodyOffsets = new System.Collections.Generic.List<FightGridPoint> { new FightGridPoint(0, 0) };
            fatalRevivalAttackActor = enemy.Id;
            previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
        }

        private void ObserveFatalRevivalCast(FightBattleSkillCastEvent cast)
        {
            if (ValidateFatalRevival && cast.ActorId == fatalRevivalAttackActor
                && cast.SkillEntityId == 59460)
                fatalRevivalAttackClock = Time.time;
        }

        private void ObserveFatalRevivalStep(FightBattleCheckpoint checkpoint, FightBattleStepEvent step)
        {
            if (!ValidateFatalRevival) return;
            var lethal = step.DamageEvents.SingleOrDefault(value => value.SourceId == fatalRevivalAttackActor
                && value.TargetId == 1 && value.SkillEntityId == 59460 && !value.IsCure
                && value.BeforeHp == 1 && value.AfterHp == 0);
            Require(lethal != null && !checkpoint.Finished
                && step.SkillCasts.Any(value => value.ActorId == fatalRevivalAttackActor
                    && value.SkillEntityId == 59460 && value.Slot == 0)
                && checkpoint.Units.Single(value => value.Id == fatalRevivalAttackActor)
                    .SkillState.Slots[0].ReleasedTimes == 1,
                "Original enemy attack did not cause death before revival and outcome settlement.");
            var revive = step.SkillCasts.Single(value => value.ActorId == 1 && value.SkillEntityId == 11470);
            var cure = step.SkillCasts.Single(value => value.ActorId == 1 && value.SkillEntityId == 11490);
            Require(revive.Delay >= lethal.Delay && cure.Delay + .00001f >= revive.Delay + 1.2f,
                "Revival preceded the lethal hit or lost the original sequential duration.");
            fatalRevivalObserved = true;
        }

        private bool CaptureFatalRevivalFrame()
        {
            if (!ValidateFatalRevival || fatalRevivalHitCaptured || fatalRevivalAttackClock <= 0
                || Time.time - fatalRevivalAttackClock < .6f) return false;
            fatalRevivalHitCaptured = true;
            CaptureName = "revival-fatal-hit-59460";
            return true;
        }

        private void VerifyFatalRevival()
        {
            if (ValidateFatalRevival) Require(fatalRevivalObserved && fatalRevivalHitCaptured,
                "Fatal revival was not settled and captured during the original enemy attack.");
        }
    }
}
