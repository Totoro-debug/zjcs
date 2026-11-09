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
        private float castTime;
        public string CaptureName { get; private set; }

        private OfficialBattleSkillStackValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition attack, int skillId)
        {
            this.catalog = catalog; this.attack = attack; this.skillId = skillId;
            itemId = skillId == 13820 ? 10196 : skillId == 13830 ? 10197 : 10198;
            statusId = skillId == 13840 ? 24697 : 24031;
            animationName = skillId == 13820 ? "100/skill_71"
                : skillId == 13830 ? "100/skill_11_2" : "100/skill_17";
        }

        public static OfficialBattleSkillStackValidation Configure(FightBattleCheckpoint checkpoint)
        {
            Require(int.TryParse(Environment.GetEnvironmentVariable("ZJCS_SKILL_STACK_ENTITY"), out int id)
                && (id == 13820 || id == 13830 || id == 13840), "Select original 13820, 13830 or 13840.");
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
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear();
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == player ? 1000 : 1;
                    if (property.Id == catalog.PropertyType("DodgePercent")
                        || property.Id == catalog.PropertyType("BlindingPercent")) property.Value = 0;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 5000000;
                unit.Turn.Speed = unit == player ? 1000 : 1;
            }
            if (id == 13840) enemy.Statuses.Add(catalog.CreateStatus(24266, enemy.Id, player.Id, 9913840));
            if (id == 13820)
            {
                var own = catalog.CreateStatus(24031, player.Id, player.Id, 9913820);
                var other = catalog.CreateStatus(24031, player.Id, enemy.Id, 9913821);
                own.StackCount = 2; other.StackCount = 3;
                player.Statuses.Add(own); player.Statuses.Add(other);
            }
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            var skill = catalog.CreateSkill(id, 0, probe.itemId, 5, 20, player.SubRank, null);
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
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
                if (cast.ActorId != 1 || cast.SkillEntityId != skillId) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
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
            if (skillId == 13820)
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
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 100000000;
            Debug.Log("[Skill stack] actual " + skillId + " hit count=" + hits.Length
                + (skillId == 13820 ? "; consumed five charge layers" : "; per-hit charge layers passed")
                + "; original item/cultivation, one release and checkpoint passed.");
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured || !animated || nativeCasts == 0 || Time.realtimeSinceStartup - castTime < .2f) return;
            var source = controller.TryGetUnitTransform(1);
            var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
            if (!renderer || renderer.PlayerPhysicsTime <= 0 || !source.GetComponentsInChildren<MeshRenderer>()
                .Any(value => value.enabled && value.isVisible && !value.forceRenderingOff
                    && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "skill-stack-" + skillId;
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
