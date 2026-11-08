using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Offline.ProfessionUI;

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
            FightBattleStatusState logic = null;
            FightBattleStatusState[] stackLogic = null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_LOGIC_ACCEPTANCE") == "1")
            {
                logic = catalog.CreateStatus(24669, 1, 1, 9924669);
                player.Statuses.Add(logic);
                stackLogic = new[] {
                    catalog.CreateStatus(24745, 1, 1, 9924745, 64280, 10594, 5, 20, 1),
                    catalog.CreateStatus(24774, 1, 1, 9924774, 64280, 10594, 5, 20, 1)
                };
                player.Statuses.AddRange(stackLogic);
            }
            return new HpStartupProbe(summon, counted, logic, stackLogic);
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

        private static HpSubstituteProbe ConfigureHpSubstituteProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HP_SUBSTITUTE_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Hp > 0);
            var attack = player.Skills.First(value => value.Slot == 0);
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit == player ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit != player && property.Id == catalog.PropertyType("Attack"))
                        property.Value = Math.Min(property.Value, 100L);
                }
                unit.MaxHp = unit.LastMaxHp = hp;
                unit.Hp = unit.Turn.Hp = unit == player ? hp : 900000L;
            }
            var occupied = new HashSet<FightGridPoint>();
            foreach (var unit in checkpoint.Units.Where(value => value != enemy && value.Hp > 0))
                foreach (var offset in unit.BodyOffsets) occupied.Add(new FightGridPoint(unit.X + offset.X, unit.Y + offset.Y));
            var nearby = new[] { new FightGridPoint(1, 0), new FightGridPoint(-1, 0),
                new FightGridPoint(0, -1), new FightGridPoint(0, 1) };
            var position = nearby.Select(value => new FightGridPoint(player.X + value.X, player.Y + value.Y))
                .First(value => enemy.BodyOffsets.All(offset => checkpoint.Grid.CanStand(value + offset, enemy.MoveMask, occupied)));
            enemy.X = position.X; enemy.Y = position.Y;
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            var summon = catalog.CreateSkill(12200, 0, 10097, 5, 20, 1, null);
            summon.LimitedTimes = 1;
            player.Skills.Add(summon); player.SkillState.Slots.Add(catalog.CreateSkillSlot(summon, 0));
            foreach (var entry in checkpoint.Timeline.Entries)
                if (entry.Id == 1) entry.Next = 1;
            checkpoint.Timeline.Entries.Sort((left, right) => left.Next.CompareTo(right.Next));
            return new HpSubstituteProbe(catalog, enemy.Id, attack);
        }

        private sealed class HpSubstituteProbe
        {
            private readonly FightOfficialCombatCatalog catalog;
            private readonly int enemyId;
            private readonly FightBattleSkillDefinition attack;
            private int dollId;
            private bool transmitted, rendered;
            private OfficialBattleSceneView scene;
            public bool ReadyToCapture => rendered;
            public bool MayAccelerate => transmitted && rendered;
            public HpSubstituteProbe(FightOfficialCombatCatalog catalog, int enemyId, FightBattleSkillDefinition attack)
            { this.catalog = catalog; this.enemyId = enemyId; this.attack = attack; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var enemy = cp.Units.Single(value => value.Id == enemyId);
                if (dollId == 0)
                {
                    var doll = cp.Units.SingleOrDefault(value => value.MasterId == 1 && value.MonsterId == 4600);
                    if (doll == null)
                    {
                        Require(!step.SkillCasts.Any(value => value.ActorId == 1 && value.SkillEntityId == 12200),
                            "Original 12200 did not create its substitute on the actual arena grid.");
                        return;
                    }
                    dollId = doll.Id;
                    scene = (OfficialBattleSceneView)typeof(OfficialBattleRuntimeController)
                        .GetField("sceneView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                    // The real summon supplies identity, placement and rendering. Arm the next
                    // isolated input only after that entity exists; the runner advances normally.
                    doll.Hp = doll.Turn.Hp = doll.MaxHp = doll.LastMaxHp = 500000L;
                    foreach (var property in doll.Properties.Concat(doll.BaseProperties))
                        if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 500000L;
                    enemy.Statuses.Add(catalog.CreateStatus(25067, enemyId, enemyId, 9925067));
                    enemy.Statuses.Add(catalog.CreateStatus(24202, enemyId, enemyId, 9924202));
                    var player = cp.Units.Single(value => value.Id == 1);
                    ConfigureHpRecoverySkill(attack, 800000L, false);
                    attack.Range.Clear(); attack.Range.Add(new FightGridPoint(doll.X - player.X, doll.Y - player.Y));
                    attack.LimitedTimes = 1;
                    player.Skills.Clear(); player.SkillState.Slots.Clear();
                    player.Skills.Add(attack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
                    return;
                }
                if (transmitted) return;
                var damage = step.DamageEvents.FirstOrDefault(value => value.SkillEntityId == 10140 && value.TargetId == enemyId);
                if (damage == null) return;
                var cast = step.SkillCasts.Single(value => value.SkillEntityId == 10140);
                var suffered = step.DamageEvents.Single(value => value.TargetId == dollId && !value.IsCure);
                Require(damage.DamageValue == suffered.DamageValue && damage.AfterHp > 0L
                    && damage.AfterHp <= enemy.MaxHp * 3 / 10
                    && Math.Abs(damage.Delay - 0.4f) < 0.0001f && Math.Abs(cast.Delay - damage.Delay) < 0.0001f
                    && enemy.Statuses.All(value => value.ClassId != 24315)
                    && enemy.Statuses.Any(value => value.ClassId == 24202)
                    && step.SkillCasts.All(value => value.SkillEntityId != 10170),
                    "Real substitute bypassed original 10140 HP trigger gates or lost its hit time.");
                transmitted = true;
                Debug.Log("[HP substitute hosts] original 4600 -> 10140: full input=" + damage.DamageValue
                    + ", afterHp=" + damage.AfterHp + " at 0.4s; original HP response gate passed.");
            }
            public void ObservePresentation()
            {
                if (scene == null || rendered || dollId == 0) return;
                var body = (Transform)typeof(OfficialBattleSceneView).GetMethod("Body",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scene, new object[] { dollId });
                rendered = body != null && body.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                    && !value.forceRenderingOff && value.isVisible && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0);
                if (rendered)
                {
                    var source = Resources.Load<OfficialSummonSkeletonData>("Combat/Summons/4600-data");
                    Require(source != null && !source.Source.showOnRoundProgress
                        && Root("GameRoundCharacterItem_" + dollId) == null
                        && body.GetComponentsInChildren<Transform>().Any(value => value.name == "MonsterModel_" + source.Source.modelId),
                        "Substitute lost its official model or appeared on the hidden round progress.");
                }
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(transmitted && rendered, host + " did not render its real substitute and verify the HP gate.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its substitute HP checkpoint after reload.");
                Debug.Log("[HP substitute hosts] " + host + ": native 4600 geometry and checkpoint reload passed.");
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
            var threshold = catalog.CreateStatus(25067, 1, 1, 9925067, 25067, 14521, 5, 20, 1);
            threshold.Origin = new FightBattleStatusOrigin { IsSet = true, UnitId = 1,
                EntityKind = FightBattleStatusOrigin.Equipment, EntityId = 8801,
                SkillItemId = 14521, SkillEntityId = 25067 };
            player.Statuses.Add(threshold);
            var other = catalog.CreateStatus(24315, 1, 1, 9924315, 25067, 14521, 5, 20, 1);
            other.Origin = threshold.Origin.Copy(); other.Origin.EntityId = 8802;
            other.StackCount = 7;
            player.Statuses.Add(other);
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
            return new HpRecoveryProbe(enemy.Id, other);
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
            private readonly FightBattleStatusState otherContribution;
            private bool activated, recovered, rendered;
            public bool ReadyToCapture { get; private set; }
            public HpRecoveryProbe(int enemyId, FightBattleStatusState otherContribution)
            { this.enemyId = enemyId; this.otherContribution = otherContribution; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var player = cp.Units.Single(value => value.Id == 1);
                if (!activated)
                {
                    Require(step.ActorId == enemyId && player.Hp > 0L && player.Hp <= player.MaxHp * 3 / 10
                        && player.Statuses.Count(value => value.ClassId == 24315) == 2
                        && player.Statuses.Contains(otherContribution) && otherContribution.StackCount == 7,
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
                    && player.Statuses.Count(value => value.ClassId == 24315) == 1
                    && player.Statuses.Contains(otherContribution) && otherContribution.StackCount == 7,
                    "Lifesteal did not clear only its own original 24315 at its parent hit time.");
                recovered = true;
                Debug.Log("[HP recovery hosts] original 25067 -> 24315: enemy hit activated; lifesteal="
                    + heal.Damage + ", afterHp=" + heal.AfterHp + "; other equipment contribution retained.");
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
            private readonly bool requiresSummon;
            private int summonId;
            private OfficialBattleSceneView sceneView;
            private bool renderedSummon;
            private readonly FightBattleStatusState counted;
            private readonly FightBattleStatusState logic;
            private readonly FightBattleStatusState[] stackLogic;
            private ProfessionNativeSceneRenderer nativeRenderer;
            private Matrix4x4[] firstSkillPose;
            private float firstSkillTime = -1;
            private int skillPoseFrames;
            private bool nativePoseAdvanced, nativeGeometryVisible;
            private int originalNativeCasts, movingNativeCasts;
            public bool ReadyToCapture => nativePoseAdvanced && nativeGeometryVisible;
            public HpStartupProbe(bool requiresSummon, FightBattleStatusState counted,
                FightBattleStatusState logic = null, FightBattleStatusState[] stackLogic = null)
            {
                this.requiresSummon = requiresSummon; this.counted = counted;
                this.logic = logic; this.stackLogic = stackLogic;
            }
            public void BindPresentation(OfficialBattleRuntimeController controller)
            {
                if (logic != null) return;
                var body = controller.TryGetUnitTransform(1);
                nativeRenderer = body == null ? null : body.GetComponentInChildren<ProfessionNativeSceneRenderer>(true);
                Require(nativeRenderer != null, "HP startup requires the actual native player renderer.");
                nativeRenderer.OnClipApplied += (animation, name, layer, weight, time, previous, backward) =>
                {
                    if (name != "200/skill_41" || weight <= 0) return;
                    skillPoseFrames++;
                    if (firstSkillPose == null)
                    {
                        firstSkillTime = time;
                        firstSkillPose = Enumerable.Range(0, nativeRenderer.State.Pose.Count)
                            .Select(nativeRenderer.State.Pose.World).ToArray();
                    }
                    else if (time > firstSkillTime && firstSkillPose.Where((pose, index) =>
                        pose != nativeRenderer.State.Pose.World(index)).Any()) nativePoseAdvanced = true;
                };
                var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                    .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var entities = (OfficialBattleSkillEntityPlayback)typeof(OfficialBattleSkillGraphPlayback)
                    .GetField("entityViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
                entities.CastStarted += cast =>
                {
                    if (cast.ActorId != 1) return;
                    if (cast.SkillEntityId == 10170) originalNativeCasts++;
                    else if (cast.SkillEntityId == 13500) movingNativeCasts++;
                };
            }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                if (observedStep) return;
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                Require(cp.Units.All(value => value.SkillState.Round == 0), "HP startup consumed a personal turn.");
                if (counted != null)
                    Require(counted.HpLessThanTriggeredCount == 3 && !cp.Units[0].Statuses.Contains(counted),
                        "HP automatic removal did not wait for its configured three invocations.");
                if (logic != null)
                {
                    VerifyStackLogic(cp);
                    Require(logic.LogicSelfInitialized && logic.LogicStartInitialized
                        && logic.LogicBlackboard.Single(value => value.KeyId == 619624960).LongValue == -1,
                        "Actual battle startup did not initialize the original 24669 blackboard.");
                    string logicCheckpoint = FightBattleCheckpointSerializer.Save(cp);
                    var restoredLogic = FightBattleCheckpointSerializer.Load(logicCheckpoint).Units
                        .Single(value => value.Id == 1).Statuses.Single(value => value.InstanceId == logic.InstanceId);
                    Require(restoredLogic.LogicSelfInitialized && restoredLogic.LogicStartInitialized
                        && restoredLogic.LogicDefinitionJson == logic.LogicDefinitionJson
                        && restoredLogic.LogicBlackboard.Single(value => value.KeyId == 619624960).LongValue == -1,
                        "The real battle checkpoint lost the original 24669 graph/blackboard.");
                    Debug.Log("[Status logic hosts] " + controller.GetType().Name
                        + ": original 24669 startup and Unity battle checkpoint passed.");
                }
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
                if (logic == null)
                    Require(originalNativeCasts == 1 && movingNativeCasts == 1,
                        "HP startup did not create both original native skill graph entities.");
                observedStep = true;
            }

            public void ObservePresentation()
            {
                if (nativePoseAdvanced && nativeRenderer != null)
                    nativeGeometryVisible |= nativeRenderer.GetComponentsInChildren<MeshRenderer>(true)
                        .Any(value => value.enabled && value.isVisible && !value.forceRenderingOff
                            && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0);
                if (requiresSummon && summonId > 0 && !renderedSummon && sceneView != null)
                {
                    var body = (Transform)typeof(OfficialBattleSceneView).GetMethod("Body",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sceneView, new object[] { summonId });
                    renderedSummon = body != null && body.GetComponentsInChildren<MeshRenderer>()
                        .Any(value => value.enabled && !value.forceRenderingOff && value.isVisible
                            && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0);
                }
            }

            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observedStep && (logic != null || originalNativeCasts == 1 && movingNativeCasts == 1
                    && skillPoseFrames >= 2 && nativePoseAdvanced && nativeGeometryVisible),
                    host + " did not complete its configured startup probe.");
                Require(!requiresSummon || renderedSummon, host + " did not render its real HP startup summon.");
                Require(checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Count(value => value.ActorId == 1 && value.SkillEntityId == 10170) == 1,
                    host + " repeated the one-shot HP startup skill.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " lost HP startup markers after checkpoint reload.");
                if (logic != null)
                {
                    VerifyStackLogic(checkpoint);
                    var restored = FightBattleCheckpointSerializer.Load(saved).Units
                        .Single(value => value.Id == 1).Statuses.Single(value => value.InstanceId == logic.InstanceId);
                    Require(restored.LogicSelfInitialized && restored.LogicStartInitialized
                        && restored.LogicDefinitionJson == logic.LogicDefinitionJson
                        && restored.LogicBlackboard.Single(value => value.KeyId == 619624960).LongValue == -1,
                        host + " lost the original logic graph/blackboard in the Unity checkpoint.");
                    Debug.Log("[Status logic hosts] " + host
                        + ": original 24669 self-init and Unity checkpoint graph/blackboard passed.");
                    return; // This mode does not assert the HP skill's scene animation binding.
                }
                Debug.Log("[HP startup hosts] " + host + ": original 24202 -> 10170; legacy 13500 at zero time;"
                    + " no personal turn consumed; both native MyFightSkill graphs started; 200/skill_41"
                    + " advanced through real renderer poses with visible geometry; checkpoint passed.");
                if (requiresSummon) Debug.Log("[HP startup hosts] " + host + ": original 10710 -> 3181 spawn event and visible native geometry passed.");
                if (counted != null) Debug.Log("[HP startup hosts] " + host + ": finite HP threshold invoked three times before automatic removal.");
            }

            private void VerifyStackLogic(FightBattleCheckpoint checkpoint)
            {
                var player = checkpoint.Units.Single(value => value.Id == 1);
                foreach (var source in stackLogic)
                {
                    Require(source.LogicSelfInitialized && source.LogicStartInitialized,
                        "Original stack graph was not initialized by the battle lifecycle: " + source.ClassId);
                    int id = source.ClassId == 24745 ? 24748 : 24752;
                    int layers = source.ClassId == 24745 ? 60 : 100;
                    var status = player.Statuses.Single(value => value.ClassId == id);
                    Require(status.StackCount == layers && status.CreatorId == 1
                        && status.RootSkillId == 64280 && status.ApplyingSkillItemId == 10594
                        && status.SourceSkillRank == 5 && status.SourceSkillLevel == 20,
                        "Original stack graph lost requested layers/source: " + source.ClassId);
                    var restored = FightBattleCheckpointSerializer.Load(
                        FightBattleCheckpointSerializer.Save(checkpoint)).Units.Single(value => value.Id == 1)
                        .Statuses.Single(value => value.InstanceId == status.InstanceId);
                    Require(restored.StackCount == layers && restored.RootSkillId == 64280,
                        "Created status layers/source were lost in the actual Unity checkpoint.");
                }
                Debug.Log("[Status stack hosts] original 24745 -> 24748 x60 and 24774 -> 24752 x100;"
                    + " startup lifecycle, source inheritance and Unity checkpoint passed.");
            }
        }
    }
}
