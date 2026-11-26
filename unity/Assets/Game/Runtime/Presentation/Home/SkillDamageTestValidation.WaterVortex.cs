using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static WaterVortexReceipt waterVortex;
        private static readonly Dictionary<int, Vector3> waterVortexPositions = new Dictionary<int, Vector3>();

        private static void PrepareAcquiredWaterVortex()
        { waterVortex = new WaterVortexReceipt(); waterVortexPositions.Clear(); }

        private static void VerifyAcquiredWaterVortex(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] damage, FightBattleStepEvent step, FightBattleCheckpoint checkpoint, string output)
        {
            Require(cast.HitSelections.Count == 1 && cast.HitSelections[0].HitEntityId == 11341
                && Math.Abs(cast.HitSelections[0].Delay - .6f) < .0001f,
                "The acquired water vortex lost its original impact graph or timing.");
            Require(damage.Length > 0 && damage.All(hit => hit.SkillItemId == 10062 && hit.SkillEntityId == 11340
                && hit.HitEntityId == 11341 && hit.DamageEntityId == 11342 && hit.ElementType == 101
                && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure),
                "The acquired water vortex lost its normal cultivated water damage.");
            var moves = step.Movements.Where(move => move.MoveBehaviourId == 11343).ToArray();
            var enemies = new HashSet<int>(checkpoint.Units.Where(unit => unit.Side != 1).Select(unit => unit.Id));
            Require(moves.All(move => enemies.Contains(move.UnitId) && Math.Abs(move.MoveDuration - .4f) < .0001f),
                "The acquired water vortex changed its target or original per-segment movement duration.");
            waterVortex.casts++; waterVortex.hits += damage.Length; waterVortex.pulls += moves.Length;
            foreach (var target in checkpoint.Units.Where(unit => unit.Id != 1))
                foreach (var status in target.Statuses.Where(status => status.ClassId == 24012
                    && status.ApplyingSkillItemId == 10062))
                {
                    Require(status.CreatorId == 1 && status.RootSkillId == 11340 && status.DurationRound == 2,
                        "The acquired wet status lost its source or original two-round duration.");
                    waterVortex.wet++;
                }
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var runner = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring water vortex repeated damage, inward movement or wet rolls.");
            waterVortex.restored = true;
            File.WriteAllText(Path.Combine(output, "acquired-water-vortex-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "acquired-water-vortex-strikes.json"), JsonUtility.ToJson(cast, true));
        }

        private static bool ObserveAcquiredWaterVortexMovement()
        {
            foreach (var driver in FindObjectsOfType<ECViewDriver>())
            {
                var view = driver.GetView<ECFightActionSkillMoveView>();
                if (view == null || float.IsPositiveInfinity(view.RemainingMoveTime)) continue;
                var entity = driver.GetComponentInParent<MyEntity>();
                if (!entity || entity.Entity == null || entity.Entity.ClassId != 11343) continue;
                var shell = entity.GetComponent<MyFightActionSkillMove>();
                var action = entity.GetComponent<MyAction>();
                var owner = action ? action.myApplyEntity : null;
                Require(shell && ReferenceEquals(shell.CurrentView, view) && owner && owner.Id.EntityId > 1,
                    "The current vortex movement lost its original shell delegation or actual target entity.");
                Vector3 position = owner.transform.position;
                if (!waterVortexPositions.TryGetValue(owner.Id.EntityId, out Vector3 previous))
                    waterVortexPositions.Add(owner.Id.EntityId, position);
                else if ((position - previous).sqrMagnitude > 1e-8f) return true;
            }
            return false;
        }

        private static bool ObserveAcquiredWaterVortexWet()
        {
            foreach (var status in FindObjectsOfType<MyFightStatus>())
            {
                var entity = status.GetComponent<MyEntity>();
                if (!entity || entity.Entity == null || entity.Entity.ClassId != 24012 || !status.Context?.Owner) continue;
                var owner = status.Context.Owner.GetComponent<MyEntity>();
                if (!owner || owner.Id.EntityId <= 1) continue;
                Require(CurrentStatusViewLookup.Get(status) != null && status.Context.HasIcon,
                    "The current wet status lost its official playable view or above-unit status icon.");
                bool image = FindObjectsOfType<Image>().Any(icon => icon.isActiveAndEnabled && icon.sprite
                    && icon.sprite.name.Contains("Abnorma_Damp") && !icon.canvasRenderer.cull
                    && icon.canvasRenderer.GetAlpha() > 0f && icon.color.a > 0f);
                bool sprite = FindObjectsOfType<SpriteRenderer>().Any(icon => icon.enabled && icon.isVisible && icon.sprite
                    && icon.sprite.name.Contains("Abnorma_Damp") && icon.color.a > 0f);
                if (image || sprite) return true;
            }
            return false;
        }

        private static void CompleteAcquiredWaterVortex(string output)
        {
            File.WriteAllText(Path.Combine(output, "acquired-water-vortex-receipt.json"), JsonUtility.ToJson(waterVortex, true));
            Require(waterVortex.casts > 0 && waterVortex.hits > 0 && waterVortex.pulls > 0 && waterVortex.wet > 0
                && waterVortex.moveCaptured && waterVortex.wetCaptured && waterVortex.restored,
                "The acquired water vortex did not exercise its original current movement, wet icon and restore.");
            Debug.Log("[Acquired active combat] 10062 water damage, current inward movement, wet icon and restore passed.");
        }

        [Serializable]
        private sealed class WaterVortexReceipt
        {
            public int casts, hits, pulls, wet;
            public bool moveCaptured, wetCaptured, restored;
        }
    }
}
