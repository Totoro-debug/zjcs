using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static TriggerTimingProbe ConfigureTriggerTimingProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TRIGGER_TIMING_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Hp > 0);
            var occupied = new HashSet<FightGridPoint>();
            foreach (var unit in checkpoint.Units.Where(value => value != enemy && value.Hp > 0 && !value.Ended))
                foreach (var offset in unit.BodyOffsets)
                    occupied.Add(new FightGridPoint(unit.X + offset.X, unit.Y + offset.Y));
            var nearby = new[] { new FightGridPoint(0, -1), new FightGridPoint(-1, 0),
                new FightGridPoint(0, 1), new FightGridPoint(1, 0) };
            var position = nearby.Select(value => new FightGridPoint(player.X + value.X, player.Y + value.Y))
                .First(value => enemy.BodyOffsets.All(offset => checkpoint.Grid.CanStand(value + offset, enemy.MoveMask, occupied)));
            // The isolated fixture starts within the original trap range. Arena AI can retreat
            // indefinitely without producing the opponent-entered-range event configured here.
            enemy.X = position.X; enemy.Y = position.Y;
            int maxHp = catalog.PropertyType("MaxHp");
            int attack = catalog.PropertyType("Attack");
            foreach (var property in enemy.Properties.Concat(enemy.BaseProperties))
            {
                if (property.Id == maxHp) property.Value = 1000000L;
                if (property.Id == attack) property.Value = Math.Min(property.Value, 100L);
            }
            enemy.MaxHp = enemy.Hp = enemy.Turn.Hp = 1000000L;
            foreach (int statusId in new[] { 12353, 12593 })
            {
                var status = catalog.CreateStatus(statusId, enemy.Id, enemy.Id, 9900980 + statusId);
                // The second fixture substitutes a moving skill to exercise its original clip.
                if (statusId == 12593) status.MoveNearSkills[0].EntityId = 13500;
                enemy.Statuses.Add(status);
            }
            return new TriggerTimingProbe();
        }

        private sealed class TriggerTimingProbe
        {
            private bool queuedAnimation;
            private bool playedAnimation;
            private bool delayedDamage;
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var step = controller.Checkpoint.Receipts.Last().Event;
                var cast = step.SkillCasts.FirstOrDefault(value => value.SkillEntityId == 13500);
                if (cast == null) return;
                Require(cast.Delay == 1f, "The delayed range trigger lost its cast start time.");
                var hits = step.DamageEvents.Where(value => value.SkillEntityId == 12570).ToArray();
                Require(hits.Length > 0, "The original trap did not produce a hit inside its configured range.");
                Require(hits.All(value => value.Delay >= 1.1999f),
                    "The original trap damage played before its one-second trigger delay.");
                delayedDamage = true;
                var scene = typeof(OfficialBattleRuntimeController).GetField("sceneView",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var pending = (IEnumerable)typeof(OfficialBattleSceneView).GetField("pendingUnitAnimations",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scene);
                foreach (object item in pending)
                {
                    var cue = (OfficialBattleAnimationCue)item.GetType().GetField("Cue").GetValue(item);
                    if (cue.UnitId == cast.ActorId && cue.SkillPlayable?.animation == "200/skill_41")
                    {
                        Require(cue.Delay == 1f, "The original skill animation was queued without the trigger delay.");
                        queuedAnimation = true;
                    }
                }
                Require(queuedAnimation, "The real scene did not queue the delayed original animation.");
            }
            public void ObservePresentation()
            {
                if (!queuedAnimation || playedAnimation) return;
                var player = GameObject.Find("HomePlayer");
                var animator = player == null ? null : player.GetComponentInChildren<Animator>(true);
                if (animator == null || !(animator.runtimeAnimatorController is AnimatorOverrideController controller)) return;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                playedAnimation = overrides.Any(value => value.Value != null
                    && value.Value.name.StartsWith("200/skill_41", StringComparison.Ordinal));
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(delayedDamage && queuedAnimation && playedAnimation,
                    host + " did not execute the delayed trap and play its queued original animation: "
                    + delayedDamage + "/" + queuedAnimation + "/" + playedAnimation);
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " did not retain cast start times in the checkpoint.");
                Debug.Log("[Trigger timing hosts] " + host
                    + ": original trap hit at 1.2s; skill animation queued at 1s and consumed by the real Animator; checkpoint passed.");
            }
        }
    }
}
