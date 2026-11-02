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
        private static HpStartupProbe ConfigureHpStartupProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_STARTUP_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            player.Hp = player.Turn.Hp = Math.Max(1L, player.MaxHp * 3 / 10);
            int maxHp = catalog.PropertyType("MaxHp"), attack = catalog.PropertyType("Attack");
            foreach (var enemy in checkpoint.Units.Where(value => value.Side != player.Side))
            {
                foreach (var property in enemy.Properties.Concat(enemy.BaseProperties))
                {
                    if (property.Id == maxHp) property.Value = 1000000L;
                    if (property.Id == attack) property.Value = Math.Min(property.Value, 100L);
                }
                enemy.MaxHp = enemy.Hp = enemy.Turn.Hp = 1000000L;
            }
            player.Statuses.Add(catalog.CreateStatus(24202, 1, 1, 9914202));
            // Isolated legacy SkillId fixture: original skill 13500 supplies a visible clip and
            // movement while exercising the same HP startup queue. The original 24202 is untouched.
            var legacy = new FightBattleStatusState { InstanceId = 9914203, ClassId = 9914203,
                OwnerId = 1, CreatorId = 1, DurationRound = -1, CurDurationRound = -1, StartRound = 0,
                HpTriggerPer = 0.5f, HpTriggerAtStart = true, HpTriggerMaxCount = 1 };
            legacy.HpTriggerSkills.Add(13500);
            bool summon = Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_STARTUP_SUMMON_ACCEPTANCE") == "1";
            if (summon) legacy.HpTriggerSkills.Insert(0, 10710);
            player.Statuses.Add(legacy);
            FightBattleStatusState counted = null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_COUNT_ACCEPTANCE") == "1")
            {
                // Exercise finite automatic removal in the actual startup scheduler. This
                // isolated copy omits effects so the existing real-skill probes stay distinct.
                counted = catalog.CreateStatus(24202, 1, 1, 9914204);
                counted.ClassId = 9914204;
                counted.HpLessThanMaxCount = 3;
                counted.HpLessThanTryAfterInvoke = true;
                counted.HpLessThanSkills.Clear();
                counted.HpLessThanStatuses.Clear();
                player.Statuses.Add(counted);
            }
            return new HpStartupProbe(summon, counted);
        }

        private sealed class HpStartupProbe
        {
            private bool observedStep;
            private bool queuedAnimation;
            private bool boundAnimation;
            private readonly bool requiresSummon;
            private int summonId;
            private OfficialBattleSceneView sceneView;
            private bool renderedSummon;
            private readonly FightBattleStatusState counted;
            public HpStartupProbe(bool requiresSummon, FightBattleStatusState counted)
            { this.requiresSummon = requiresSummon; this.counted = counted; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                if (observedStep) return;
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                Require(cp.Units.All(value => value.SkillState.Round == 0), "HP startup consumed a personal turn.");
                if (counted != null)
                    Require(counted.HpLessThanTriggeredCount == 3 && !cp.Units[0].Statuses.Contains(counted),
                        "HP automatic removal did not wait for its configured three invocations.");
                var original = step.SkillCasts.Where(value => value.SkillEntityId == 10170).ToArray();
                var moving = step.SkillCasts.Where(value => value.SkillEntityId == 13500).ToArray();
                Require(original.Length == 1 && moving.Length == 1
                    && original[0].Delay == 0f && moving[0].Delay == 0f,
                    "The real runner dropped or delayed an initial HP-triggered skill.");
                Require(step.DamageEvents.Any(value => value.SkillEntityId == 10170)
                    && cp.Units[0].Statuses.All(value => value.ClassId != 24202),
                    "Original 24202 did not execute its skill and configured removal.");
                var scene = typeof(OfficialBattleRuntimeController).GetField("sceneView",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                sceneView = (OfficialBattleSceneView)scene;
                if (requiresSummon)
                {
                    var summon = cp.Units.Single(value => value.MonsterId == 3181 && value.MasterId == 1);
                    summonId = summon.Id;
                    Require(step.SpawnedUnitIds.Contains(summonId), "HP startup did not publish the real 10710 summon.");
                }
                var pending = (IEnumerable)typeof(OfficialBattleSceneView).GetField("pendingUnitAnimations",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scene);
                foreach (object item in pending)
                {
                    var cue = (OfficialBattleAnimationCue)item.GetType().GetField("Cue").GetValue(item);
                    if (cue.UnitId == 1 && cue.SkillPlayable?.animation == "200/skill_41")
                    {
                        Require(cue.Delay == 0f, "HP startup animation acquired a personal-turn delay.");
                        queuedAnimation = true;
                    }
                }
                Require(queuedAnimation, "The HP startup skill did not reach the real scene animation queue.");
                observedStep = true;
            }

            public void ObservePresentation()
            {
                if (requiresSummon && summonId > 0 && !renderedSummon && sceneView != null)
                {
                    var body = (Transform)typeof(OfficialBattleSceneView).GetMethod("Body",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sceneView, new object[] { summonId });
                    renderedSummon = body != null && body.GetComponentsInChildren<MeshRenderer>()
                        .Any(value => value.enabled && !value.forceRenderingOff && value.isVisible
                            && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0);
                }
                if (!queuedAnimation || boundAnimation) return;
                var player = GameObject.Find("HomePlayer");
                var animator = player == null ? null : player.GetComponentInChildren<Animator>(true);
                if (animator == null || !(animator.runtimeAnimatorController is AnimatorOverrideController controller)) return;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                boundAnimation = overrides.Any(value => value.Value != null
                    && value.Value.name.StartsWith("200/skill_41", StringComparison.Ordinal));
            }

            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observedStep && queuedAnimation && boundAnimation, host + " did not queue and bind HP startup animation.");
                Require(!requiresSummon || renderedSummon, host + " did not render its real HP startup summon.");
                Require(checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Count(value => value.ActorId == 1 && value.SkillEntityId == 10170) == 1,
                    host + " repeated the one-shot HP startup skill.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " lost HP startup markers after checkpoint reload.");
                Debug.Log("[HP startup hosts] " + host + ": original 24202 -> 10170; legacy 13500 at zero time;"
                    + " no personal turn consumed; original marker bound by real Animator; checkpoint passed."
                    + " Skill 41 bone motion remains unconverted and is not asserted by this probe.");
                if (requiresSummon) Debug.Log("[HP startup hosts] " + host + ": original 10710 -> 3181 spawn event and visible native geometry passed.");
                if (counted != null) Debug.Log("[HP startup hosts] " + host + ": finite HP threshold invoked three times before automatic removal.");
            }
        }
    }
}
