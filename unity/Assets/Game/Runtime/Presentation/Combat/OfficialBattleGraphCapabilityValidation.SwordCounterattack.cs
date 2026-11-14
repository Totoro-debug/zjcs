using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateSwordCounterattack => Environment.GetEnvironmentVariable("ZJCS_SWORD_COUNTERATTACK") == "1";
        private bool SwordCounterattackShield => Environment.GetEnvironmentVariable("ZJCS_SWORD_COUNTERATTACK_SHIELD") == "1";
        private const uint SwordCounterattackSound = 1876531539u;
        private readonly HashSet<uint> swordCounterattackPlaying = new HashSet<uint>();
        private float swordCounterattackClock;
        private int swordCounterattackFrames, swordCounterattackEnemy;
        private bool swordCounterattackNative;

        private static OfficialBattleGraphCapabilityValidation ConfigureSwordCounterattack(FightBattleCheckpoint checkpoint)
        {
            Require(Environment.GetEnvironmentVariable("ZJCS_FIRST_PROMOTION_SKILL") == "10008",
                "Counterattack uses the original warrior profile and directional slash fixture.");
            var basis = ConfigureFirstPromotion(checkpoint);
            var data = JsonUtility.FromJson<OfficialBattleSkillPlayableCatalogData>(
                Resources.Load<TextAsset>("Combat/skill-playables").text);
            string clip = data.skills.Single(row => row.skill_entity_id == 11210).playables
                .First(value => value.kind == 1 && !string.IsNullOrEmpty(value.animation)).animation;
            var probe = new OfficialBattleGraphCapabilityValidation(basis.catalog, basis.finisher, 11210, clip)
                { previousCaptureFramerate = basis.previousCaptureFramerate };
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var enemy = checkpoint.Units.First(unit => unit.Side != player.Side);
            probe.swordCounterattackEnemy = enemy.Id;
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                // Keep the isolated native fixture adjacent. Original 3331's
                // ranged AI otherwise retreats outside the secret's 12 cells.
                unit.MoveDist = 0;
                int speed = unit == player ? 900 : 1000;
                foreach (var prop in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (prop.Id == basis.catalog.PropertyType("Speed")) prop.Value = speed;
                    if (prop.Id == basis.catalog.PropertyType("DodgePercent")
                        || prop.Id == basis.catalog.PropertyType("BlindingPercent")
                        || prop.Id == basis.catalog.PropertyType("MoveDist")
                        || prop.Id == basis.catalog.PropertyType("FixedMoveDist")
                        || prop.Id == basis.catalog.PropertyType("MoveDistPercent")) prop.Value = 0;
                }
                var baseProps = unit.BaseProperties ?? unit.Properties;
                if (!baseProps.Any(prop => prop.Id == basis.catalog.PropertyType("MoveDist")))
                    baseProps.Add(new FightPropertyValue { Id = basis.catalog.PropertyType("MoveDist"), Value = 0 });
                unit.Turn.Speed = speed;
            }
            player.Statuses.Add(basis.catalog.CreateStatus(25017, player.Id, player.Id, 9925017,
                25017, 10537, 7, 30, 8));
            if (probe.SwordCounterattackShield)
            {
                var shield = basis.catalog.CreateStatus(24435, player.Id, player.Id, 9924435);
                shield.ShieldUsesCustomValue = true; shield.CustomShieldValue = 100000000;
                player.Statuses.Add(shield);
            }
            var attack = basis.catalog.CreateSkill(59460, 0, 14678, 2, 20, 1,
                basis.catalog.CalculateSkillProps(59460, 2, 20, 1)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            enemy.Skills.Add(attack); enemy.SkillState.Slots.Add(basis.catalog.CreateSkillSlot(attack, 0));
            // Seed 2 naturally counters while original 24435 is still alive;
            // keep its duration and the secret's original 50% filter intact.
            checkpoint.RandomSeed = probe.SwordCounterattackShield ? 2 : 1; checkpoint.MaxRound = 24;
            return probe;
        }

        private bool ObserveSwordCounterattackStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateSwordCounterattack) return false;
            if (settled) return true;
            var checkpoint = controller.Checkpoint;
            var step = checkpoint.Receipts.Last().Event;
            var casts = step.SkillCasts.Where(cast => cast.SkillEntityId == 11210).ToArray();
            if (casts.Length == 0) return true;
            var cast = casts.Single();
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var incoming = step.DamageEvents.Single(hit => hit.SkillEntityId == 59460);
            var reaction = step.DamageEvents.Single(hit => hit.SkillEntityId == 11210);
            Require(cast.ActorId == 1 && cast.Slot == -1 && cast.SkillItemId == 10537
                && cast.TargetIds.SequenceEqual(new[] { swordCounterattackEnemy })
                && reaction.SourceId == 1 && reaction.TargetId == swordCounterattackEnemy
                && reaction.Damage < 0 && reaction.Delay >= incoming.Delay + .08f - .0001f,
                "Actual original enemy hit did not dispatch the non-equipped counterattack toward its attacker.");
            Require(!incoming.Dodge && !incoming.Blinding && !incoming.Immunity && player.Hp > 0,
                "An invalid incoming result triggered the counterattack.");
            if (SwordCounterattackShield)
                Require(incoming.Damage == 0 && incoming.ShieldDamage > 0 && player.Hp == player.MaxHp,
                    "The native shield combination did not absorb the entire incoming hit.");
            var status = player.Statuses.Single(value => value.ClassId == 25017);
            Require(status.ApplyingSkillItemId == 10537 && status.SourceSkillRank == 7
                && status.SourceSkillLevel == 30 && status.SourceSubRank == 8
                && status.LogicTriggeredCount == 1 && player.SkillState.Slots.Count == 0,
                "Counterattack source cultivation, count or equipment slot changed.");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                "Counterattack checkpoint round trip differs.");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "sword-counterattack-checkpoint.json"), saved);
            foreach (var unit in checkpoint.Units) { unit.Skills.Clear(); unit.SkillState.Slots.Clear(); }
            settled = true;
            Debug.Log("[Sword counterattack] Original enemy hit, 11210 reaction, cultivation and checkpoint passed; shield="
                + SwordCounterattackShield);
            return true;
        }

        private bool ObserveSwordCounterattackFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateSwordCounterattack || nativeCasts == 0 || finisherScheduled) return false;
            var body = controller.TryGetUnitTransform(1);
            var enemy = controller.TryGetUnitTransform(swordCounterattackEnemy);
            foreach (var skill in UnityEngine.Object.FindObjectsOfType<MyFightSkill>())
                if (skill.Component?.Entity?.ClassId == 11210)
                {
                    Require(skill.Source && skill.Source.transform == body
                        && skill.Target && skill.Target.transform == enemy,
                        "Original counterattack view reversed its source and target.");
                    swordCounterattackNative = true;
                }
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
                if (sound.SoundEvent?.Id == SwordCounterattackSound && sound.SoundEvent.PlayingId != 0
                    && ArcNativeSoundEvent(sound.SoundEvent.PlayingId) == SwordCounterattackSound)
                {
                    Require(sound.mySource == body.gameObject,
                        "Original Skill_10537_1 sound did not bind the counterattacking owner.");
                    if (swordCounterattackPlaying.Add(sound.SoundEvent.PlayingId))
                        Debug.Log("[Sword counterattack sound] Original Skill_10537_1 is playing in the native engine.");
                }
            if (swordCounterattackFrames < FirstPromotionFrameTimes.Length
                && Time.time - swordCounterattackClock >= FirstPromotionFrameTimes[swordCounterattackFrames])
            {
                CaptureName = "sword-counterattack-phase-" + (++swordCounterattackFrames);
                return true;
            }
            return swordCounterattackFrames < FirstPromotionFrameTimes.Length
                || Time.time - swordCounterattackClock < .85f;
        }

        private void VerifySwordCounterattack()
        {
            if (!ValidateSwordCounterattack) return;
            Time.captureFramerate = previousCaptureFramerate;
            Require(swordCounterattackNative && swordCounterattackFrames == 4
                && swordCounterattackPlaying.Count == 1
                && swordCounterattackPlaying.All(id => ArcNativeSoundEvent(id) == 0),
                "Original counterattack view, four action frames or native sound completion is incomplete.");
            Debug.Log("[Sword counterattack] Original 11210 native view, body action and sound completion passed.");
        }
    }
}
