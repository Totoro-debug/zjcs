using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        [Serializable] private sealed class ExtinctionBladeReceipt
        {
            public int casts, hits, armour;
            public bool restored, launchCaptured, recoveryCaptured, armourCaptured;
        }
        private static ExtinctionBladeReceipt extinctionBlade;

        private static void PrepareAcquiredExtinctionBlade(FightBattleUnitState actor)
        {
            extinctionBlade = new ExtinctionBladeReceipt();
            // Guarantee the status branch only within this isolated rendering diagnostic.
            // The rule harness separately covers the original 50% and resistance branches.
            SetAcquiredBattleProperty(actor, "EffectRateValue", 20000);
        }

        private static void VerifyAcquiredExtinctionBlade(FightBattleStepEvent step,
            FightBattleSkillCastEvent cast, FightBattleDamageEvent[] hits,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == cast.ActorId);
            var move = step.Movements.Single(value => value.UnitId == cast.ActorId && value.MoveBehaviourId == 11532);
            var source = new FightGridPoint(cast.Presentation.SourceX, cast.Presentation.SourceY);
            var landing = move.Path.Last();
            Require(cast.SkillEntityId == 11530 && move.Path.Count == 2 && move.Path[0].Equals(source)
                && Math.Abs(landing.X-source.X)+Math.Abs(landing.Y-source.Y) <= 5
                && Math.Abs(move.Delay-cast.Delay-.07f) < .0001f
                && Math.Abs(move.MoveDuration-.5f) < .0001f && hits.Length > 0,
                "Extinction Blade lost its original jump range, direct path or movement timing.");
            foreach (var hit in hits)
            {
                int distance = Math.Abs(hit.DamageX-landing.X)+Math.Abs(hit.DamageY-landing.Y);
                Require(hit.SkillItemId == 10069 && hit.SourceId == actor.Id && hit.TargetId != actor.Id
                    && hit.HasDamagePosition && distance <= 2 && hit.HitEntityId == 11531
                    && hit.DamageEntityId == 11534 && hit.ElementType == 104 && hit.DamageType == 1
                    && !hit.TrueDamage && !hit.IsCure && hit.Damage < 0
                    && Math.Abs(hit.Delay-cast.Delay-(.42f+.08f*distance)) < .0001f,
                    "Extinction Blade lost its dark impact, body-cell scope or original hit timing.");
                if (!hit.IsDamageTriggerStatus) continue;
                var target = checkpoint.Units.Single(unit => unit.Id == hit.TargetId);
                var armour = target.Statuses.Single(status => status.ClassId == 24113);
                var skill = actor.Skills.Single(value => value.Slot == 0);
                Require(armour.CanDispersed && armour.DurationRound == 2 && armour.CreatorId == actor.Id
                    && armour.ApplyingSkillItemId == 10069 && armour.RootSkillId == 11530
                    && armour.SourceSkillRank == skill.Rank && armour.SourceSkillLevel == 2
                    && armour.SourceSubRank == actor.SubRank,
                    "Extinction Blade's two-round armour reduction lost its cultivated source.");
                extinctionBlade.armour++;
            }
            extinctionBlade.casts++; extinctionBlade.hits += hits.Length;
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Restoring Extinction Blade repeated its jump, damage or status application.");
            extinctionBlade.restored = true;
            File.WriteAllText(Path.Combine(output, "acquired-extinction-blade-checkpoint.json"), saved);
        }

        private static bool ObserveAcquiredExtinctionPhase(int id)
        {
            foreach (var status in FindObjectsOfType<MyFightStatus>())
            {
                var entity = status.GetComponent<MyEntity>();
                if (!entity || entity.Entity == null || entity.Entity.ClassId != id || !status.Context?.Owner) continue;
                var owner = status.Context.Owner;
                var body = owner.GetComponent<MyEntity>();
                if (!body || body.Id.EntityId != 2) continue;
                Require(CurrentStatusViewLookup.Get(status) != null,
                    "Extinction Blade lost the declared current status view.");
                var renderer = owner.GetComponentInChildren<BattleNativeMonsterRenderer>();
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                var visible = renderer ? renderer.GetComponent<MeshRenderer>() : null;
                if (!renderer || !mesh || mesh.vertexCount == 0 || !visible || !visible.isVisible
                    || !visible.enabled || visible.forceRenderingOff) continue;
                if (id == 24269 && renderer.Animation != "floating_01") continue;
                return true;
            }
            return false;
        }

        private static void CompleteAcquiredExtinctionBlade(string output)
        {
            Require(extinctionBlade.casts > 0 && extinctionBlade.hits > 0 && extinctionBlade.armour > 0
                && extinctionBlade.restored && extinctionBlade.launchCaptured
                && extinctionBlade.recoveryCaptured && extinctionBlade.armourCaptured,
                "Extinction Blade must render its dark impact, armour reduction and both airborne phases.");
            File.WriteAllText(Path.Combine(output, "acquired-extinction-blade-receipt.json"),
                JsonUtility.ToJson(extinctionBlade, true));
            Debug.Log("[Acquired active combat] 10069 cultivated direct jump, dark impact, armour reduction, native airborne phases and restoration passed.");
        }
    }
}
