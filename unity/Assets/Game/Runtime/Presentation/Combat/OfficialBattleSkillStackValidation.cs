using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Explicit isolated input; the public host still owns input, rules and settlement.
    public sealed class OfficialBattleSkillStackValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition attack;
        private readonly int skillId, itemId, statusId;
        private readonly string animationName;
        private bool stacked, animated, captured;
        private int nativeCasts;
        private int explosionNativeCasts;
        private float castTime;
        public string CaptureName { get; private set; }

        private OfficialBattleSkillStackValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition attack, int skillId)
        {
            this.catalog = catalog; this.attack = attack; this.skillId = skillId;
            itemId = skillId == 13730 ? 10192 : skillId == 13740 ? 10193 : skillId == 50210 ? 16020 : skillId == 13820 ? 10196 : skillId == 13830 ? 10197 : 10198;
            statusId = skillId == 50210 ? 24823 : skillId == 13840 ? 24697 : 24031;
            animationName = skillId == 13730 ? "200/skill_40" : skillId == 13740 ? "200/skill_42_1" : skillId == 50210 ? "skill_01" : skillId == 13820 ? "100/skill_71"
                : skillId == 13830 ? "100/skill_11_2" : "100/skill_17";
        }

        public static OfficialBattleSkillStackValidation Configure(FightBattleCheckpoint checkpoint)
        {
            Require(int.TryParse(Environment.GetEnvironmentVariable("ZJCS_SKILL_STACK_ENTITY"), out int id)
                && (id == 13730 || id == 13740 || id == 13820 || id == 13830 || id == 13840 || id == 50210),
                "Select original 13730, 13740, 13820, 13830, 13840 or 50210.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Id != 1);
            var probe = new OfficialBattleSkillStackValidation(catalog,
                player.Skills.First(value => value.Slot == 0), id);
            enemy.MonsterId = 3665; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(enemy.MonsterId);
            enemy.MonsterType = catalog.MonsterType(enemy.MonsterId);
            enemy.AiType = catalog.MonsterAiType(enemy.MonsterId);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(enemy.MonsterId, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            Require(enemy.BodyOffsets.Count == 9, "Retain the original nine-cell target.");
            player.X = enemy.X - 2; player.Y = enemy.Y;
            Require(checkpoint.Grid.Contains(new FightGridPoint(player.X, player.Y)), "Source is outside the battlefield.");
            long fixtureHp = id == 13740 ? 1000000000L : 5000000L;
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear();
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = fixtureHp;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == player ? 1000 : 1;
                    if (property.Id == catalog.PropertyType("DodgePercent")
                        || property.Id == catalog.PropertyType("BlindingPercent")) property.Value = 0;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = fixtureHp;
                unit.Turn.Speed = unit == player ? 1000 : 1;
            }
            if (id == 13840) enemy.Statuses.Add(catalog.CreateStatus(24266, enemy.Id, player.Id, 9913840));
            if (id == 13730 || id == 13740)
            {
                Require(checkpoint.GridItems.Count == 0, "The isolated burn fixture expects an empty grid-item list.");
                for (int offset = -2; offset <= 0; offset++)
                {
                    var position = new FightGridPoint(enemy.X + offset, enemy.Y);
                    Require(checkpoint.Grid.Contains(position), "The burn fixture is outside the battlefield.");
                    var item = FightGridItemRuntime.Create(9913732 + offset, 3320,
                        player, player, position, 0);
                    item.Runtime.Statuses.Add(catalog.CreateStatus(13052, item.Id, player.Id,
                        9913742 + offset, id, probe.itemId, 5, 20, player.SubRank, player.Properties));
                    checkpoint.GridItems.Add(item);
                }
            }
            if (id == 13820)
            {
                var own = catalog.CreateStatus(24031, player.Id, player.Id, 9913820);
                var other = catalog.CreateStatus(24031, player.Id, enemy.Id, 9913821);
                own.StackCount = 2; other.StackCount = 3;
                player.Statuses.Add(own); player.Statuses.Add(other);
            }
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            var skillProperties = catalog.CalculateSkillProps(id, 5, 20, player.SubRank)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
            var skill = catalog.CreateSkill(id, id == 50210 ? -2 : 0, probe.itemId, 5, 20, player.SubRank, skillProperties);
            skill.LimitedTimes = 1;
            if (id == 50210)
            {
                var charge = catalog.CreateStatus(24823, player.Id, player.Id, 9950210);
                charge.StackCount = 5; player.Statuses.Add(charge);
                player.PetEnergy = new FightBattlePetEnergyState {
                    OwnerUnitId = player.Id, PetUnitId = player.Id, PetClassId = 1620, PetEvolutionPhase = 1
                };
                player.PetEnergy.SupportSkills.Add(skill);
                player.PetEnergy.SupportSkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                player.ReportSkillSnapshots.Add(skill);
                player.Skills.Add(probe.attack);
                player.SkillState.Slots.Add(catalog.CreateSkillSlot(probe.attack, 0));
            }
            else
            {
                player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            }
            checkpoint.MaxRound = 8;
            return probe;
        }

        public void BindPresentation(OfficialBattleRuntimeController controller)
        {
            var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var entities = (OfficialBattleSkillEntityPlayback)typeof(OfficialBattleSkillGraphPlayback)
                .GetField("entityViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
            entities.CastStarted += cast => {
                if (skillId == 13740 && cast.ActorId == 1 && cast.SkillEntityId == 13745) explosionNativeCasts++;
                if (cast.ActorId != 1 || cast.SkillEntityId != skillId) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
                if (skillId == 50210)
                {
                    Require(cast.IsPetSupport, "50210 must use the original pet support source.");
                    return;
                }
                var source = controller.TryGetUnitTransform(1);
                var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
                Require(renderer, "The public actor is not the original native player.");
                renderer.OnClipApplied += (clip, name, layer, weight, time, previous, backward) => {
                    if (name == animationName && time > .04f && weight > 0) animated = true;
                };
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            var checkpoint = controller.Checkpoint;
            var step = checkpoint.Receipts.Last().Event;
            if (stacked || !step.SkillCasts.Any(value => value.ActorId == 1 && value.SkillEntityId == skillId)) return;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var hits = step.DamageEvents.Where(value => value.SourceId == 1 && value.SkillEntityId == skillId).ToArray();
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            if (skillId == 13730 || skillId == 13740)
            {
                Require(hits.Length > 0 && checkpoint.GridItems.Count == 3
                    && checkpoint.GridItems.All(value => !value.Ended && value.Runtime.Side == player.Side
                        && value.Runtime.Statuses.Any(status => status.ClassId == 13052)),
                    "The original burn skill did not retain the three friendly grid entities.");
                Require(saved.GridItems.Count == 3 && saved.GridItems.All(value => !value.Ended
                    && value.HasRuntime && value.Runtime.Side == player.Side
                    && value.Runtime.Statuses.Any(status => status.ClassId == 13052))
                    && saved.Units.Single(value => value.Id == 1).SkillState.Slots[0].ReleasedTimes == 1,
                    "Burn tiles, factions or the single equipped release were lost in the checkpoint.");
                if (skillId == 13740)
                {
                    var explosions = step.SkillCasts.Where(value => value.SkillEntityId == 13745).ToArray();
                    var explosionHits = step.DamageEvents.Where(value => value.SkillEntityId == 13745).ToArray();
                    int bossId = checkpoint.Units.Single(value => value.MonsterId == 3665).Id;
                    Require(hits.Length == 9 && hits.All(value => value.Damage < 0 && value.AfterHp < value.BeforeHp)
                        && explosions.Length == 2 && explosionHits.Length == 9
                        && explosionHits.All(value => value.TargetId == bossId && value.HasHitGridPosition
                            && value.Damage < 0 && value.AfterHp < value.BeforeHp),
                        "The 12-cell parent or two cross explosions did not settle all occupied body cells.");
                    Require(saved.Receipts.Last().Event.DamageEvents.Count(value => value.SkillEntityId == 13745
                        && value.HasHitGridPosition) == 9, "Explosion cells were lost on checkpoint restore.");
                }
            }
            else if (skillId == 50210)
            {
                Require(hits.Length > 0 && player.Statuses.Single(value => value.InstanceId == 9950210).StackCount == 5,
                    "The pet support hit consumed its current charge layers.");
                var savedPlayer = saved.Units.Single(value => value.Id == 1);
                Require(savedPlayer.Statuses.Single(value => value.InstanceId == 9950210).StackCount == 5
                    && savedPlayer.Statuses.Where(value => value.ClassId == 24823).Sum(value => value.StackCount)
                        == player.Statuses.Where(value => value.ClassId == 24823).Sum(value => value.StackCount)
                    && savedPlayer.PetEnergy.SupportSkillState.Slots.Single().ReleasedTimes == 1,
                    "Pet support release or retained charge layers were lost in the checkpoint.");
            }
            else if (skillId == 13820)
            {
                Require(hits.Length > 0 && !player.Statuses.Any(value => value.ClassId == 24031),
                    "The real blast did not consume all five source charge layers.");
                var savedPlayer = saved.Units.Single(value => value.Id == 1);
                Require(!savedPlayer.Statuses.Any(value => value.ClassId == 24031)
                    && savedPlayer.SkillState.Slots[0].ReleasedTimes == 1,
                    "Consumed charge or the single blast release was lost in the checkpoint.");
            }
            else
            {
                var status = player.Statuses.Single(value => value.ClassId == statusId);
                Require(hits.Length > 0 && status.StackCount == hits.Length
                    && (skillId != 13830 || hits.Length == 3), "Actual transient target count and charge layers differ.");
                Require(status.CreatorId == 1 && status.RootSkillId == skillId && status.ApplyingSkillItemId == itemId
                    && status.Origin.SkillEntityId == skillId && status.SourceSkillRank == 5,
                    "Created status lost its actual skill identity or cultivation.");
                Require(saved.Units.Single(value => value.Id == 1).Statuses.Single(value => value.ClassId == statusId)
                    .StackCount == hits.Length && saved.Units.Single(value => value.Id == 1).SkillState.Slots[0].ReleasedTimes == 1,
                    "The actual charge layers or single equipped release were lost in the checkpoint.");
    
            }
            stacked = true;
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            player.Skills.Add(attack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                if (property.Id == catalog.PropertyType("Attack")) property.Value = skillId == 13740 ? 1000000000L : 100000000L;
            Debug.Log("[Skill stack] actual " + skillId + " hit count=" + hits.Length
                + (skillId == 13740 ? "; two original explosions; nine parent and nine child body-cell hits; retained three tiles" : skillId == 13730 ? "; retained three friendly burn tiles" : skillId == 13820 ? "; consumed five charge layers" : skillId == 50210
                    ? "; preserved five original layers and subsequent grants" : "; per-hit charge layers passed")
                + "; original item/cultivation, one release and checkpoint passed.");
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured) return;
            if (skillId == 50210 && nativeCasts > 0)
            {
                var pet = PetTransform(controller);
                var native = pet ? pet.GetComponentInChildren<BattleNativeMonsterRenderer>(true) : null;
                animated |= native && native.Animation == animationName && native.AnimatorFrame.Current.Time > .04f;
            }
            if (!animated || nativeCasts == 0 || Time.realtimeSinceStartup - castTime < (skillId == 13740 ? .95f : .2f)) return;
            if (skillId == 13740 && explosionNativeCasts < 2) return;
            var source = skillId == 50210 ? PetTransform(controller) : controller.TryGetUnitTransform(1);
            var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
            if (!source || (skillId != 50210 && (!renderer || renderer.PlayerPhysicsTime <= 0))
                || !source.GetComponentsInChildren<MeshRenderer>()
                .Any(value => value.enabled && value.isVisible && !value.forceRenderingOff
                    && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "skill-stack-" + skillId;
        }

        private static Transform PetTransform(OfficialBattleRuntimeController controller)
        {
            var scene = (OfficialBattleSceneView)typeof(OfficialBattleRuntimeController)
                .GetField("sceneView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            return scene.TryGetPetTransform(1);
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(stacked && animated && captured && nativeCasts == 1 && checkpoint.Finished,
                "Original skill graph, native action, charge state or public settlement was incomplete.");
            Debug.Log("[Skill stack] public " + skillId + ": original native action, transient stacks, input and settlement passed.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Skill stack diagnostic: " + message); }
    }
}
