using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private static bool ValidateHitPassive => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_HIT_PASSIVE_ITEM"));
        private int hitPassiveItem, hitPassiveStatus, hitPassiveChild, hitPassiveNativeCasts;
        private string hitPassiveMode;
        private float hitPassiveNativeClock;
        private bool hitPassiveStepObserved, hitPassiveCaptured;

        private static OfficialBattleGraphCapabilityValidation ConfigureHitPassive(FightBattleCheckpoint checkpoint)
        {
            Require(int.TryParse(Environment.GetEnvironmentVariable("ZJCS_HIT_PASSIVE_ITEM"), out int item)
                && new[] { 10512, 10560, 10562 }.Contains(item), "Unknown hit passive fixture.");
            string mode = Environment.GetEnvironmentVariable("ZJCS_HIT_PASSIVE_MODE");
            Require(new[] { "damage", "shield", "dodge", "blinding", "immunity" }.Contains(mode),
                "Unknown hit passive result fixture.");
            int active = item == 10562 ? 10008 : 10000;
            Require(Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_SKILL") == active.ToString(),
                "The fixture must use the original matching active skill.");
            var probe = ConfigureFirstPromotion(checkpoint);
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var target = checkpoint.Units.First(value => value.Side != player.Side);
            int statusId = item == 10512 ? 25005 : item == 10560 ? 25026 : 25025;
            int childId = item == 10512 ? 10850 : item == 10560 ? 11300 : 11290;
            var passive = probe.catalog.CreateStatus(statusId, player.Id, player.Id, 9925000 + statusId,
                statusId, item, 2, 20, player.SubRank,
                probe.catalog.CalculateSkillProps(statusId, 2, 20, player.SubRank)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            passive.Components.HitSkill.Rate = 1f;
            player.Statuses.Add(passive);
            if (mode == "dodge" || mode == "blinding")
            {
                var unit = mode == "dodge" ? target : player;
                int id = probe.catalog.PropertyType(mode == "dodge" ? "DodgePercent" : "BlindingPercent");
                foreach (var list in new[] { unit.Properties, unit.BaseProperties }.Where(value => value != null))
                {
                    var property = list.FirstOrDefault(value => value.Id == id);
                    if (property == null) list.Add(new FightPropertyValue { Id = id, Value = 10000 });
                    else property.Value = 10000;
                }
            }
            if (mode == "immunity") target.Statuses.Add(probe.catalog.CreateStatus(24007, target.Id, target.Id, 9924007));
            if (mode == "shield")
            {
                var shield = probe.catalog.CreateStatus(24435, target.Id, target.Id, 9924435,
                    customShieldValue: 100000000);
                shield.ShieldInitialized = true; shield.ShieldRemainValue = 100000000;
                target.Statuses.Add(shield);
            }
            probe.hitPassiveItem = item; probe.hitPassiveStatus = statusId;
            probe.hitPassiveChild = childId; probe.hitPassiveMode = mode;
            probe.firstPromotionCaptureAfter = 1.5f;
            return probe;
        }

        private void ObserveHitPassiveStep(FightBattleCheckpoint checkpoint, FightBattleStepEvent step)
        {
            if (!ValidateHitPassive) return;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var hit = step.DamageEvents.Single(value => value.SourceId == player.Id && value.SkillEntityId == skillId);
            Require(hitPassiveMode != "dodge" || hit.Dodge, "Expected an actual dodge.");
            Require(hitPassiveMode != "blinding" || hit.Blinding, "Expected an actual blinded attack.");
            Require(hitPassiveMode != "immunity" || hit.Immunity, "Expected actual immunity.");
            Require(hitPassiveMode != "shield" || hit.Damage == 0 && hit.ShieldDamage > 0,
                "The original hit was not absorbed by the shield.");
            bool expected = hitPassiveMode == "damage" || hitPassiveMode == "shield";
            var children = step.SkillCasts.Where(value => value.SkillEntityId == hitPassiveChild).ToArray();
            Require(children.Length == (expected ? 1 : 0), "Hit passive child dispatch differs.");
            if (expected) Require(children[0].ActorId == player.Id && children[0].SkillItemId == hitPassiveItem
                && children[0].TargetIds.Contains(hit.TargetId), "Hit passive lost its source or target.");
            if (expected) Require(step.DamageEvents.Any(value => value.SkillEntityId == hitPassiveChild
                && value.SkillItemId == hitPassiveItem && value.SourceId == player.Id && value.TargetId == hit.TargetId),
                "Hit passive child damage lost its source or target.");
            var component = player.Statuses.Single(value => value.ClassId == hitPassiveStatus).Components.HitSkill;
            Require(component.InvokeCount == children.Length, "Hit passive counter differs.");
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(saved.Units.Single(value => value.Id == player.Id).Statuses.Single(value => value.ClassId == hitPassiveStatus)
                .Components.HitSkill.InvokeCount == children.Length, "Hit passive checkpoint lost its counter.");
            hitPassiveStepObserved = true;
            Debug.Log("[Hit passive] Original child dispatch and checkpoint passed: " + hitPassiveItem + "/" + hitPassiveMode);
        }

        private void ObserveHitPassiveCast(FightBattleSkillCastEvent cast)
        {
            if (!ValidateHitPassive || finisherScheduled || cast.ActorId != 1 || cast.SkillEntityId != hitPassiveChild) return;
            hitPassiveNativeCasts++; hitPassiveNativeClock = Time.time;
        }

        private bool CaptureHitPassiveFrame()
        {
            if (!ValidateHitPassive || hitPassiveNativeCasts == 0 || hitPassiveCaptured
                || Time.time - hitPassiveNativeClock < .35f) return false;
            hitPassiveCaptured = true; CaptureName = "hit-passive-native"; return true;
        }

        private void VerifyHitPassive()
        {
            if (!ValidateHitPassive) return;
            bool expected = hitPassiveMode == "damage" || hitPassiveMode == "shield";
            Require(hitPassiveStepObserved && hitPassiveNativeCasts == (expected ? 1 : 0)
                && (!expected || hitPassiveCaptured), "Hit passive native cast or captured frame differs.");
        }
    }
}
