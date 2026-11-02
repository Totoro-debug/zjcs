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

        private static HpForbiddenProbe ConfigureHpForbiddenProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_FORBIDDEN_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            player.Hp = player.Turn.Hp = Math.Max(1L, player.MaxHp * 3 / 10);
            foreach (var enemy in checkpoint.Units.Where(value => value.Side != player.Side))
            {
                foreach (var property in enemy.Properties.Concat(enemy.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 1000000L;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = Math.Min(property.Value, 100L);
                }
                enemy.MaxHp = enemy.Hp = enemy.Turn.Hp = 1000000L;
            }
            player.Statuses.Add(catalog.CreateStatus(24202, 1, 1, 9914202));
            player.Statuses.Add(catalog.CreateStatus(24456, 1, 1, 9914456));
            return new HpForbiddenProbe();
        }

        private sealed class HpForbiddenProbe
        {
            private bool observed;
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 10170).ToArray();
                if (casts.Length == 0) return;
                Require(!observed && casts.Length == 1 && cp.Units[0].SkillState.Round >= 3
                    && cp.Units[0].Statuses.All(value => value.ClassId != 24456 && value.ClassId != 24202),
                    "Forbidden exit did not perform exactly one original HP recheck at expiry.");
                Require(step.DamageEvents.Any(value => value.SkillEntityId == 10170),
                    "Forbidden exit dropped its original skill damage event.");
                observed = true;
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observed, host + " never performed its original forbidden-exit HP recheck.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its forbidden-exit checkpoint after reload.");
                Debug.Log("[HP forbidden hosts] " + host + ": original 24456 expiry -> 24202 -> 10170 once; damage and checkpoint passed.");
            }
        }

        private static HpCostProbe ConfigureHpCostProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_COST_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 1000000L;
                    if (unit != player && property.Id == catalog.PropertyType("Attack"))
                        property.Value = Math.Min(property.Value, 100L);
                }
                unit.MaxHp = unit.LastMaxHp = 1000000L;
                unit.Hp = unit.Turn.Hp = unit == player ? 600000L : 1000000L;
            }
            player.Statuses.Add(catalog.CreateStatus(25067, 1, 1, 9925067));
            player.Statuses.Add(catalog.CreateStatus(24202, 1, 1, 9924202));
            // One paid cast in this isolated checkpoint; its original hit graph and presentation
            // stay intact. This verifies cost notifications without draining HP again next turn.
            var skill = player.Skills.First(value => value.Slot == 0);
            skill.CostType = 1; skill.SkillCost = 400000L; skill.CanTriggerChild = true;
            skill.LimitedTimes = 1;
            var slot = player.SkillState.Slots.First(value => value.Slot == 0);
            slot.Cooldown = 0; slot.LimitedTimes = 1;
            foreach (var entry in checkpoint.Timeline.Entries)
                entry.Next = entry.Id == player.Id ? 1 : entry.Next;
            checkpoint.Timeline.Entries.Sort((left, right) => left.Next.CompareTo(right.Next));
            return new HpCostProbe();
        }

        private sealed class HpCostProbe
        {
            private bool observed;
            public bool ReadyToCapture => observed;
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                if (observed) return;
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var player = cp.Units.Single(value => value.Id == 1);
                var cost = step.DamageEvents.SingleOrDefault(value => value.IsSkillCost);
                var cast = step.SkillCasts.SingleOrDefault(value => value.SkillEntityId == 10170);
                Require(step.ActorId == 1 && cost != null && cost.BeforeHp == 600000L && cost.AfterHp == 200000L
                    && player.Statuses.Any(value => value.ClassId == 24315)
                    && player.Statuses.All(value => value.ClassId != 24202)
                    && cast != null && cast.ActorId == 1 && cast.Delay == 0f,
                    "Real paid cast did not execute original 25067/24202 thresholds immediately after payment.");
                Require(step.DamageEvents[0].IsSkillCost && step.DamageEvents[1].SkillEntityId == 10170,
                    "Real paid cast ran its HP skill before payment or after the parent hit.");
                observed = true;
                Debug.Log("[HP cost hosts] original paid cast: 600000 -> 200000; 25067 -> 24315 and 24202 -> 10170 before parent hit passed.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observed && checkpoint.Receipts.SelectMany(value => value.Event.DamageEvents)
                    .Count(value => value.IsSkillCost) == 1, host + " lost or repeated its one paid cast.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its HP cost checkpoint after reload.");
                Debug.Log("[HP cost hosts] " + host + ": one paid cast, original HP triggers and checkpoint reload passed.");
            }
        }

        private static HpRecoveryProbe ConfigureHpRecoveryProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_RECOVERY_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Hp > 0);
            foreach (var unit in new[] { player, enemy })
            {
                long max = unit == player ? 1000000L : 3000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = max;
                unit.MaxHp = unit.LastMaxHp = max;
                unit.Hp = unit.Turn.Hp = unit == player ? 400000L : max;
            }
            player.Properties.Add(new FightPropertyValue { Id = 990999, Value = 10000 });
            player.BaseProperties.Add(new FightPropertyValue { Id = 990999, Value = 10000 });
            player.Statuses.Add(catalog.CreateStatus(25067, 1, 1, 9925067));
            // Isolated checkpoint only: deterministic fixed hits retain the real player skill's
            // identity/presentation. The original status and shipped rule tables stay untouched.
            var playerSkill = player.Skills.First(value => value.Slot == 0);
            ConfigureHpRecoverySkill(playerSkill, 500000, true);
            player.SkillState.Slots.First(value => value.Slot == 0).Cooldown = 0;
            var enemySkill = enemy.Skills.First(value => value.Slot == 0);
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
            ConfigureHpRecoverySkill(enemySkill, 200000, false);
            enemy.Skills.Add(enemySkill);
            enemy.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            foreach (var entry in checkpoint.Timeline.Entries)
                entry.Next = entry.Id == enemy.Id ? 1 : entry.Id == player.Id ? 2 : entry.Next;
            checkpoint.Timeline.Entries.Sort((left, right) => left.Next.CompareTo(right.Next));
            return new HpRecoveryProbe(enemy.Id);
        }

        private static void ConfigureHpRecoverySkill(FightBattleSkillDefinition skill, long damage, bool lifesteal)
        {
            var originalHit = FightBattleHitCollector.CollectAll(skill).First(value => value.HasFixedDamage);
            skill.HitGraphRoots.Clear(); skill.HitGraphRootEntityIds.Clear(); skill.HitGraphRootEntityId = 0;
            skill.HitGraphEntities.Clear(); skill.Hits.Clear(); skill.Range.Clear();
            for (int x = -12; x <= 12; x++)
                for (int y = -12; y <= 12; y++) skill.Range.Add(new FightGridPoint(x, y));
            skill.CanTriggerChild = true;
            skill.TargetType = 1; skill.SkillTargetType = 0;
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = originalHit.EntityId, DamageId = originalHit.DamageId,
                DamageType = 8, HasCustomDamage = true, CustomDamage = damage, Delay = 0.4f,
                HemophagiaProperty = lifesteal ? 990999 : 0 });
        }

        private sealed class HpRecoveryProbe
        {
            private readonly int enemyId;
            private bool activated, recovered, rendered;
            public bool ReadyToCapture { get; private set; }
            public HpRecoveryProbe(int enemyId) { this.enemyId = enemyId; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var player = cp.Units.Single(value => value.Id == 1);
                if (!activated)
                {
                    Require(step.ActorId == enemyId && player.Hp > 0L && player.Hp <= player.MaxHp * 3 / 10
                        && player.Statuses.Any(value => value.ClassId == 24315),
                        "Enemy hit did not activate original 25067 -> 24315 below 30% HP: actor="
                        + step.ActorId + ", hp=" + player.Hp + ", maxHp=" + player.MaxHp
                        + ", statuses=" + string.Join(",", player.Statuses.Select(value => value.ClassId))
                        + ", casts=" + string.Join(",", step.SkillCasts.Select(value => value.SkillEntityId))
                        + ", damage=" + string.Join(",", step.DamageEvents.Select(value => value.SourceId
                            + ">" + value.TargetId + ":" + value.Damage)));
                    activated = true;
                    return;
                }
                if (recovered) return;
                var heal = step.DamageEvents.FirstOrDefault(value => value.SourceId == 1 && value.IsCure);
                if (heal == null) return;
                Require(heal.TargetId == 1 && heal.AfterHp > 300000L && Math.Abs(heal.Delay - 0.4f) < 0.0001f
                    && player.Statuses.All(value => value.ClassId != 24315),
                    "Lifesteal did not clear original 24315 at its parent hit time.");
                recovered = true;
                Debug.Log("[HP recovery hosts] original 25067 -> 24315: enemy hit activated; lifesteal="
                    + heal.Damage + ", afterHp=" + heal.AfterHp + "; threshold cleanup passed.");
            }
            public void ObservePresentation()
            {
                if (!recovered) return;
                var floats = UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .Where(value => value.TargetId == 1 && value.EffectType == 3
                        && value.GetComponentsInChildren<Renderer>().Any(renderer => renderer.isVisible)).ToArray();
                rendered |= floats.Length > 0;
                ReadyToCapture = floats.Any(value => (float)typeof(OfficialBattleDamageFloat)
                    .GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value) >= 0.15f);
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(activated && recovered && rendered, host + " did not execute/render its lifesteal recovery.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its HP recovery checkpoint after reload.");
                Debug.Log("[HP recovery hosts] " + host + ": native cure float and checkpoint reload passed.");
            }
        }

        private static HpListenerProbe ConfigureHpListenerProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_LISTENER_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            foreach (var property in player.Properties.Concat(player.BaseProperties))
                if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 1000000L;
            player.MaxHp = player.LastMaxHp = 1000000L;
            player.Hp = player.Turn.Hp = 300000L;
            var status = catalog.CreateStatus(24478, 1, 1, 9914478, 0, 0, 5, 20, 1);
            status.StackCount = 5;
            player.Statuses.Add(status);
            return new HpListenerProbe(status);
        }

        private sealed class HpListenerProbe
        {
            private readonly FightBattleStatusState status;
            private bool observed;
            public bool RenderedCure { get; private set; }
            public bool ReadyToCapture { get; private set; }
            public HpListenerProbe(FightBattleStatusState status) { this.status = status; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                if (observed) return;
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var heals = step.DamageEvents.Where(value => value.SkillEntityId == 12620).ToArray();
                Require(heals.Length == 5 && heals.All(value => value.IsCure && value.Damage > 0
                    && value.SourceId == 1 && value.TargetId == 1 && value.Delay == 0f),
                    "Original 24478 did not execute five immediate positive 12620 cures.");
                Require(status.HpLessThanTriggeredCount == 5 && status.StackCount == 0
                    && !cp.Units[0].Statuses.Contains(status) && cp.Units.All(value => value.SkillState.Round == 0),
                    "Original 24478 did not exhaust its five stacks before the first personal turn.");
                Require(cp.Units[0].Hp == 300000L + heals.Sum(value => value.Damage),
                    "Original 24478 did not retain its real HP change.");
                observed = true;
                Debug.Log("[HP listeners hosts] original 24478 -> 12620: five cures, total="
                    + heals.Sum(value => value.Damage) + "; stack exhaustion and no personal turn passed.");
            }
            public void ObservePresentation()
            {
                if (!observed) return;
                var floats = UnityEngine.Object.FindObjectsOfType<OfficialBattleDamageFloat>()
                    .Where(value => value.TargetId == 1 && value.EffectType == 3
                        && value.GetComponentsInChildren<Renderer>().Any(renderer => renderer.isVisible)).ToArray();
                RenderedCure |= floats.Length > 0;
                ReadyToCapture = floats.Any(value => (float)typeof(OfficialBattleDamageFloat)
                    .GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value) >= 0.15f);
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observed && RenderedCure, host + " did not render its native HP listener cure float.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its HP listener checkpoint after reload.");
                Debug.Log("[HP listeners hosts] " + host + ": native cure float and checkpoint reload passed.");
            }
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
