using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private static bool ValidateChargedBurst => Environment.GetEnvironmentVariable("ZJCS_CHARGED_BURST_ACCEPTANCE") == "1";
        private bool chargedApplied, chargedCaptured, chargedAttackScheduled, chargedConsumed;

        private static OfficialBattleGraphCapabilityValidation ConfigureChargedBurst(FightBattleCheckpoint checkpoint)
        {
            Require(Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_SKILL") == "10041",
                "Charged burst uses the original 10041 equipped skill.");
            var probe = ConfigureFirstPromotion(checkpoint);
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            player.Skills.Single(skill => skill.Slot == 0).LimitedTimes = 1;
            player.SkillState.Slots.Single(slot => slot.Slot == 0).LimitedTimes = 1;
            return probe;
        }

        private bool ObserveChargedBurstStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateChargedBurst) return false;
            var checkpoint = controller.Checkpoint;
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var step = checkpoint.Receipts.Last().Event;
            if (!chargedApplied && step.SkillCasts.Any(cast => cast.ActorId == 1 && cast.SkillEntityId == 11000))
            {
                var precision = player.Statuses.Single(status => status.ClassId == 24191);
                var damage = player.Statuses.Single(status => status.ClassId == 24219);
                Require(precision.ApplyingSkillItemId == 10041 && damage.ApplyingSkillItemId == 10041
                    && precision.Components.HitDmgAddPer.DurationSkillCount == 1
                    && damage.Components.HitDmgAddPer.DurationSkillCount == 1
                    && step.StatusViewEvents.Any(value => value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24191)
                    && step.StatusViewEvents.Any(value => value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24219),
                    "Original charged-burst status applications differ.");
                foreach (var unit in checkpoint.Units) { unit.Skills.Clear(); unit.SkillState.Slots.Clear(); }
                chargedApplied = true;
                Debug.Log("[Charged burst] Original cast and both status events passed.");
            }
            else if (chargedAttackScheduled && !chargedConsumed
                && step.SkillCasts.Any(cast => cast.ActorId == 1 && cast.SkillEntityId == 10560))
            {
                Require(step.DamageEvents.Any(hit => hit.SkillEntityId == 10560 && hit.TargetId != 1 && hit.Damage < 0)
                    && !player.Statuses.Any(status => status.ClassId == 24191 || status.ClassId == 24219),
                    "The next original damaging cast did not consume both statuses.");
                player.Skills.Clear(); player.SkillState.Slots.Clear();
                chargedConsumed = true;
                Debug.Log("[Charged burst] Next original damage cast consumed both statuses.");
            }
            return true;
        }

        private bool ObserveChargedBurstFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateChargedBurst) return false;
            if (!chargedApplied) return true;
            if (!chargedCaptured)
            {
                var views = UnityEngine.Object.FindObjectsOfType<MyFightStatus>();
                var precision = views.FirstOrDefault(value => value.GetComponent<MyEntity>()?.ClassId == 24191);
                var damage = views.FirstOrDefault(value => value.GetComponent<MyEntity>()?.ClassId == 24219);
                if (!precision || !damage || !animated) return true;
                var body = controller.TryGetUnitTransform(1);
                Require(body && precision.Context.Owner == body.gameObject && damage.Context.Owner == body.gameObject,
                    "Charged-burst status views are not bound to the player.");
                chargedCaptured = true; CaptureName = "charged-burst-native"; return true;
            }
            var player = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            if (!chargedAttackScheduled)
            {
                var props = catalog.CalculateSkillProps(10560, 2, 20, player.SubRank)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();
                var attack = catalog.CreateSkill(10560, 0, 10004, 2, 20, player.SubRank, props);
                player.Skills.Add(attack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
                chargedAttackScheduled = true;
            }
            else if (chargedConsumed && !finisherScheduled)
            {
                player.Skills.Add(finisher); player.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
                foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
                finisherScheduled = true;
            }
            return true;
        }

        private void VerifyChargedBurst(FightBattleCheckpoint checkpoint)
        {
            Time.captureFramerate = previousCaptureFramerate;
            Require(chargedApplied && chargedCaptured && chargedAttackScheduled && chargedConsumed
                && animated && checkpoint.Finished, "Charged-burst native battle is incomplete.");
            Debug.Log("[Charged burst] Native status views, action, next cast and result passed.");
        }
    }
}
