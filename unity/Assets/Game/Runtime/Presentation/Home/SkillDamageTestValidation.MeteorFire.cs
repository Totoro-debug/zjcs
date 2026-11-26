using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static MeteorFireReceipt meteorFire;
        private static void PrepareAcquiredMeteorFire() => meteorFire = new MeteorFireReceipt();

        private static void VerifyAcquiredMeteorFire(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] damage, FightBattleCheckpoint checkpoint, string output)
        {
            Require(cast.HitSelections.Count == 1 && cast.HitSelections[0].HitEntityId == 11541
                && Math.Abs(cast.HitSelections[0].Delay - .5f) < .0001f,
                "The acquired meteor lost its original root impact timing.");
            Require(damage.Length > 0 && damage.All(hit => hit.SkillItemId == 10070
                && hit.SkillEntityId == 11540 && hit.HitEntityId == 11541 && hit.DamageEntityId == 10007
                && hit.ElementType == 102 && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure
                && Math.Abs(hit.Delay - cast.Delay - 1f) < .0001f),
                "The acquired meteor lost its cultivated fire damage or per-cell impact delay.");
            meteorFire.casts++; meteorFire.hits += damage.Length;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var runner = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring meteor fire repeated its damage, burning rolls or grid creation.");
            meteorFire.restored = true;
            File.WriteAllText(Path.Combine(output, "acquired-meteor-fire-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "acquired-meteor-fire-strikes.json"), JsonUtility.ToJson(cast, true));
        }

        private static bool ObserveAcquiredMeteorBurn()
        {
            var camera = Camera.main;
            if (!camera) return false;
            foreach (var status in FindObjectsOfType<MyFightStatus>())
            {
                var entity = status.GetComponent<MyEntity>();
                if (!entity || entity.Entity == null || entity.Entity.ClassId != 11542 || !status.Context?.Owner) continue;
                Require(CurrentStatusViewLookup.Get(status) != null,
                    "The meteor burning ground did not use its original current status view.");
                // This original ground effect uses two SpriteRenderers, not particles.
                foreach (var playable in FindObjectsOfType<MyPlayable>())
                {
                    if (playable.mySource != status.Context.Owner || !playable.myApplyComponent
                        || !playable.GetComponentsInParent<Transform>().Any(parent =>
                            parent.name.StartsWith("playable11542", StringComparison.Ordinal))) continue;
                    foreach (var sprite in playable.myApplyComponent.GetComponentsInChildren<SpriteRenderer>())
                    {
                        if (!sprite.enabled || !sprite.isVisible || !sprite.sprite || sprite.color.a <= 0
                            || (camera.cullingMask & (1 << sprite.gameObject.layer)) == 0) continue;
                        var point = camera.WorldToViewportPoint(sprite.bounds.center);
                        if (point.z > 0 && point.x > .08f && point.x < .92f && point.y > .15f && point.y < .85f)
                            return true;
                    }
                }
            }
            return false;
        }

        private static void CompleteAcquiredMeteorFire(FightBattleCheckpoint checkpoint, string output)
        {
            var steps = checkpoint.Receipts.Select(receipt => receipt.Event).Where(step => step != null).ToArray();
            meteorFire.burningHits = steps.Sum(step => step.DamageEvents.Count(hit => hit.SkillItemId == 10070
                && hit.SkillEntityId == 11600 && hit.HitEntityId == 11601 && hit.DamageEntityId == 10021
                && hit.ElementType == 102 && hit.Damage < 0));
            meteorFire.grids = steps.Sum(step => step.GridViewEvents.Count(value =>
                value.Kind == FightBattleGridViewEventKind.StatusStart && value.Status.ClassId == 11542));
            meteorFire.ended = steps.Sum(step => step.GridViewEvents.Count(value =>
                value.Kind == FightBattleGridViewEventKind.StatusEnd && value.Status.ClassId == 11542));
            File.WriteAllText(Path.Combine(output, "acquired-meteor-fire-receipt.json"), JsonUtility.ToJson(meteorFire, true));
            Require(meteorFire.casts > 0 && meteorFire.hits > 0 && meteorFire.burningHits > 0
                && meteorFire.grids > 0 && meteorFire.ended > 0 && meteorFire.restored
                && meteorFire.burnCaptured && meteorFire.impactCaptured,
                "The acquired meteor did not exercise its main impact, burning child, current native ground view and restore.");
            Debug.Log("[Acquired active combat] 10070 fire impact, burning ground, current status view and restore passed.");
        }

        [Serializable]
        private sealed class MeteorFireReceipt
        {
            public int casts, hits, burningHits, grids, ended;
            public bool restored, burnCaptured, impactCaptured;
        }
    }
}
