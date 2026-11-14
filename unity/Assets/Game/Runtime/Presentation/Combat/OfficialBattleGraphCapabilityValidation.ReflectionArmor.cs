using System;
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
        private bool ValidateReflectionArmor => Environment.GetEnvironmentVariable("ZJCS_REFLECTION_ARMOR") == "1";
        private int reflectionEnemy, reflectionFrames;
        private float reflectionClock;
        private bool reflectionView, reflectionParticles, reflectionSound;

        private static OfficialBattleGraphCapabilityValidation ConfigureReflectionArmor(FightBattleCheckpoint checkpoint)
        {
            // Reuse the original warrior/enemy arrangement. The fixture equips
            // only the approved secret; it changes no production unlocks or saves.
            var basis = ConfigureSwordCounterattack(checkpoint);
            var probe = new OfficialBattleGraphCapabilityValidation(basis.catalog, basis.finisher, 11480, "")
                { previousCaptureFramerate = basis.previousCaptureFramerate };
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            probe.reflectionEnemy = checkpoint.Units.First(unit => unit.Side != player.Side).Id;
            player.Statuses.Clear();
            player.Statuses.Add(basis.catalog.CreateStatus(25035, player.Id, player.Id, 9925035,
                25035, 10567, 7, 30, 8));
            var shield = basis.catalog.CreateStatus(24435, player.Id, player.Id, 9924435);
            shield.ShieldUsesCustomValue = true;
            shield.CustomShieldValue = Environment.GetEnvironmentVariable("ZJCS_REFLECTION_ARMOR_FULL_SHIELD") == "1"
                ? 100000000 : 1;
            player.Statuses.Add(shield);
            checkpoint.RandomSeed = 43;
            return probe;
        }

        private void ObserveReflectionArmorCast(FightBattleSkillCastEvent cast, OfficialBattleRuntimeController controller)
        {
            if (!ValidateReflectionArmor || cast.SkillEntityId != 11480) return;
            Require(cast.ActorId == 1 && cast.TargetIds.SequenceEqual(new[] { reflectionEnemy }),
                "Original reflection view reversed source and target.");
            var native = UnityEngine.Object.FindObjectsOfType<MyFightSkill>().Single(value =>
                value.Component?.Entity?.ClassId == 11480);
            Require(native.Source && native.Source.transform == controller.TryGetUnitTransform(1)
                && native.Target && native.Target.transform == controller.TryGetUnitTransform(reflectionEnemy),
                "Original 11480 entity is not bound to the actual holder and attacker.");
            reflectionView = true; reflectionClock = Time.time;
        }

        private bool ObserveReflectionArmorStep(OfficialBattleRuntimeController controller)
        {
            if (!ValidateReflectionArmor) return false;
            if (settled) return true;
            var cp = controller.Checkpoint;
            var step = cp.Receipts.Last().Event;
            if (!step.SkillCasts.Any(cast => cast.SkillEntityId == 11480)) return true;
            var incoming = step.DamageEvents.Single(hit => hit.SkillEntityId == 59460);
            var reflected = step.DamageEvents.Single(hit => hit.SkillEntityId == 11480);
            bool fullShield = Environment.GetEnvironmentVariable("ZJCS_REFLECTION_ARMOR_FULL_SHIELD") == "1";
            Debug.Log($"[Reflection armor] incoming damage={incoming.Damage} shield={incoming.ShieldDamage} delay={incoming.Delay}; reflected source={reflected.SourceId} target={reflected.TargetId} damage={reflected.Damage} element={reflected.ElementType} hit={reflected.HitEntityId} entity={reflected.DamageEntityId} delay={reflected.Delay}; full={fullShield}");
            Require((fullShield ? incoming.ShieldDamage > 0 && incoming.Damage == 0
                    : incoming.ShieldDamage > 0 && incoming.Damage <= 0) && reflected.Damage < 0
                && reflected.SourceId == 1 && reflected.TargetId == reflectionEnemy && reflected.ElementType == 103
                && reflected.HitEntityId == 11481 && reflected.DamageEntityId == 11482
                && reflected.Delay >= incoming.Delay, "Original shield absorption/reflection result or timing differs.");
            var player = cp.Units.Single(unit => unit.Id == 1);
            var status = player.Statuses.Single(value => value.ClassId == 25035);
            Require(status.SourceSkillRank == 7 && status.SourceSkillLevel == 30 && status.SourceSubRank == 8
                && status.ApplyingSkillItemId == 10567 && status.LogicTriggeredCount == 1
                && player.SkillState.Slots.Count == 0, "Reflection cultivation, count or equipped slot changed.");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                "Reflection checkpoint round trip differs.");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "reflection-armor-checkpoint.json"), saved);
            foreach (var unit in cp.Units) { unit.Skills.Clear(); unit.SkillState.Slots.Clear(); }
            settled = true;
            Debug.Log("[Reflection armor] Actual shield absorption, 11480/11481/11482, cultivation and checkpoint passed.");
            return true;
        }

        private bool ObserveReflectionArmorFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidateReflectionArmor || nativeCasts == 0 || finisherScheduled) return false;
            foreach (var particle in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
                if (particle.particleCount > 0)
                    for (var parent = particle.transform; parent; parent = parent.parent)
                        if (parent.name.Contains("11480") || parent.name.Contains("11481") || parent.name.Contains("11482"))
                            reflectionParticles = true;
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
                if (sound.SoundEvent?.Id == 1983424188u && sound.SoundEvent.PlayingId > 0
                    && sound.mySource == controller.TryGetUnitTransform(1)?.gameObject
                    && ArcNativeSoundEvent(sound.SoundEvent.PlayingId) == sound.SoundEvent.Id)
                    reflectionSound = true;
            // Original 11480 is an effect/sound graph with no authored body
            // animation. Verify its actual particle playback in the shared gate.
            animated |= reflectionView && reflectionParticles;
            if (reflectionFrames < FirstPromotionFrameTimes.Length
                && Time.time - reflectionClock >= FirstPromotionFrameTimes[reflectionFrames])
            {
                CaptureName = "reflection-armor-phase-" + (++reflectionFrames);
                return true;
            }
            return reflectionFrames < FirstPromotionFrameTimes.Length || Time.time - reflectionClock < .85f;
        }

        private void VerifyReflectionArmor()
        {
            if (!ValidateReflectionArmor) return;
            Time.captureFramerate = previousCaptureFramerate;
            Require(reflectionView && reflectionParticles && reflectionSound && reflectionFrames == 4,
                "Original reflection view, particles, sound or four sampled frames are incomplete.");
            Debug.Log("[Reflection armor] Original reaction entity, particles, sound and four frames passed.");
        }
    }
}
