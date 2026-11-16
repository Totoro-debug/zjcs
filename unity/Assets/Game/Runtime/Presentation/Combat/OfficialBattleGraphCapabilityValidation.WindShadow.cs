using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private static bool ValidateWindShadow => Environment.GetEnvironmentVariable("ZJCS_WIND_SHADOW_ACCEPTANCE") == "1";
        private int windShadowCasts;
        private float windShadowClock;
        private bool windShadowCaptured;

        private static OfficialBattleGraphCapabilityValidation ConfigureWindShadow(FightBattleCheckpoint checkpoint)
        {
            var probe = ConfigureFirstPromotion(checkpoint);
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var passive = probe.catalog.CreateStatus(25038, player.Id, player.Id, 9925038,
                25038, 10569, 2, 20, player.SubRank,
                probe.catalog.CalculateSkillProps(25038, 2, 20, player.SubRank)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            passive.Components.HitSkill.ConditionProgress = 2;
            passive.Components.HitSkill.Rate = 1f;
            player.Statuses.Add(passive);
            // The original attack provides the native action; this diagnostic isolates
            // the wind passive by making that attack's element match its filter.
            player.Skills.Single(skill => skill.Slot == 0).ElementType = 100;
            return probe;
        }

        private void ObserveWindShadowCast(FightBattleSkillCastEvent cast)
        {
            if (!ValidateWindShadow || cast.ActorId != 1 || finisherScheduled || cast.SkillEntityId != 11670) return;
            windShadowCasts++;
            windShadowClock = Time.time;
        }

        private void ObserveWindShadowStep(FightBattleStepEvent step)
        {
            if (!ValidateWindShadow) return;
            var child = step.SkillCasts.Single(value => value.SkillEntityId == 11670);
            Require(child.ActorId == 1 && child.SkillItemId == 10569
                && step.DamageEvents.Any(value => value.SkillEntityId == 11670
                    && value.TargetId != 1 && value.Damage < 0),
                "Wind shadow child source or damage differs.");
            Debug.Log("[Wind shadow] Original child source and damage passed.");
        }

        private bool CaptureWindShadowFrame()
        {
            if (!ValidateWindShadow || windShadowCasts == 0 || windShadowCaptured
                || Time.time - windShadowClock < .35f) return false;
            windShadowCaptured = true;
            CaptureName = "wind-shadow-native";
            return true;
        }

        private void VerifyWindShadow()
        {
            if (!ValidateWindShadow) return;
            Require(windShadowCasts == 1 && windShadowCaptured,
                "Original wind shadow child or captured frame is missing.");
        }
    }
}
