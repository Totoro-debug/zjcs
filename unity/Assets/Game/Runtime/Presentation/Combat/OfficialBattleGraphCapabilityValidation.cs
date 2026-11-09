using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Isolated original-graph inputs for the public weekly host, never user unlocks.
    public sealed class OfficialBattleGraphCapabilityValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition finisher;
        private readonly int skillId;
        private readonly string clipName;
        private bool settled, animated, captured;
        private int nativeCasts, nativeChildren;
        private float castTime;
        public string CaptureName { get; private set; }

        private OfficialBattleGraphCapabilityValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition finisher, int skillId, string clipName)
        { this.catalog = catalog; this.finisher = finisher; this.skillId = skillId; this.clipName = clipName; }

        public static OfficialBattleGraphCapabilityValidation Configure(FightBattleCheckpoint checkpoint)
        {
            string mode = Environment.GetEnvironmentVariable("ZJCS_GRAPH_CAPABILITY_ACCEPTANCE");
            Require(int.TryParse(mode, out int id) && (id == 13640 || id == 13870 || id == 13970), "Unknown original graph.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side);
            var probe = new OfficialBattleGraphCapabilityValidation(catalog,
                player.Skills.First(value => value.Slot == 0), id,
                id == 13640 ? "200/skill_07" : id == 13870 ? "100/skill_72" : "100/skill_02");
            enemy.MonsterId = 3665; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(3665);
            enemy.MonsterType = catalog.MonsterType(3665); enemy.AiType = catalog.MonsterAiType(3665);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(3665, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            player.X = enemy.X; player.Y = enemy.Y - 2;
            if (id == 13640)
            {
                var ally = new FightBattleUnitState {
                    Id = checkpoint.Units.Max(value => value.Id) + 1,
                    MasterId = player.Id, Side = player.Side,
                    MonsterId = 9320, MonsterTemplateId = catalog.MonsterTemplateId(9320),
                    CharacterType = catalog.MonsterCharacterType(9320), MonsterType = catalog.MonsterType(9320),
                    AiType = catalog.MonsterAiType(9320), BodyOffsets = catalog.MonsterBodyOffsets(9320),
                    X = player.X - 1, Y = player.Y,
                    Level = player.Level, SubRank = player.SubRank,
                    Properties = player.Properties.Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList(),
                    BaseProperties = (player.BaseProperties ?? player.Properties).Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList(),
                    Turn = new FightTurnUnit()
                };
                ally.Turn.Id = ally.Id; ally.Turn.MasterId = player.Id;
                checkpoint.Units.Add(ally);
            }
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                long maxHp = unit.Side == player.Side ? 5000000 : 1000000000;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = maxHp;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == player ? 1000 : 1;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = maxHp;
                unit.Turn.Speed = unit == player ? 1000 : 1;
            }
            if (id == 13640)
            {
                var ally = checkpoint.Units.Single(value => value.Side == player.Side && value.Id != player.Id);
                ally.Hp = ally.Turn.Hp = 2500000;
                player.Statuses.Add(catalog.CreateStatus(25136, player.Id, player.Id, 9913640,
                    13970, 10209, 7, 30, player.SubRank));
            }
            if (id == 13870) enemy.Hp = enemy.Turn.Hp = 200000000;
            if (id == 13970) player.Statuses.Add(catalog.CreateStatus(24721, player.Id, player.Id, 9913970));
            int item = id == 13640 ? 10204 : id == 13870 ? 10201 : 10209;
            var skill = catalog.CreateSkill(id, 0, item, 5, 20, player.SubRank,
                catalog.CalculateSkillProps(id, 5, 20, player.SubRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
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
                if (cast.ActorId != 1) return;
                if (cast.SkillEntityId == 13643) nativeChildren++;
                if (cast.SkillEntityId != skillId) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
                var source = controller.TryGetUnitTransform(1);
                var renderer = source ? source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true) : null;
                Require(renderer, "Original player renderer is absent.");
                renderer.OnClipApplied += (clip, name, layer, weight, time, previous, backward) => {
                    if (name == clipName && time > .04f && weight > 0) animated = true;
                };
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (settled) return;
            var checkpoint = controller.Checkpoint; var step = checkpoint.Receipts.Last().Event;
            if (!step.SkillCasts.Any(value => value.ActorId == 1 && value.SkillEntityId == skillId)) return;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            if (skillId == 13640)
            {
                var ally = checkpoint.Units.Single(value => value.Side == player.Side && value.Id != player.Id);
                var children = step.SkillCasts.Where(value => value.SkillEntityId == 13643).ToArray();
                Require(children.Length == 2 && children[0].TargetIds.SequenceEqual(new[] { ally.Id })
                    && children[1].TargetIds.SequenceEqual(new[] { player.Id }), "HP ordering/full-HP second target differs.");
                Require(step.DamageEvents.Count(value => value.SkillEntityId == 13643) == 2
                    && ally.Hp > 2500000 && player.Hp == player.MaxHp
                    && player.Statuses.Any(value => value.ClassId == 24435 && value.ShieldRemainValue > 0),
                    "Explicit child recovery or full-HP conversion differs.");
            }
            else
            {
                var hits = step.DamageEvents.Where(value => value.SkillEntityId == skillId).ToArray();
                Require(hits.Length > 0 && hits.All(value => value.Damage < 0),
                    "Conditional graph has no actual damage settlement.");
                if (skillId == 13970) Require(hits.Length > 1 && hits.All(value => value.HasHitGridPosition),
                    "Original area lost large-target cell occurrences.");
            }
            Require(player.SkillState.Slots[0].ReleasedTimes == 1, "Graph spent extra ordinary releases.");
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(restored.RandomSeed == checkpoint.RandomSeed
                && restored.Receipts.Last().Event.SkillCasts.Count == step.SkillCasts.Count,
                "Graph checkpoint differs.");
            Debug.Log("[Graph capability] " + skillId + ": actual original graph, one release and checkpoint passed.");
            settled = true; player.Skills.Clear(); player.SkillState.Slots.Clear();
            player.Skills.Add(finisher); player.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
            foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured || !settled || !animated || nativeCasts != 1
                || skillId == 13640 && nativeChildren != 2 || Time.realtimeSinceStartup - castTime < .85f) return;
            var source = controller.TryGetUnitTransform(1);
            if (!source || !source.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                && value.isVisible && !value.forceRenderingOff
                && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "graph-capability-" + skillId;
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(settled && animated && captured && nativeCasts == 1 && checkpoint.Finished
                && (skillId != 13640 || nativeChildren == 2), "Native graph, action, input or settlement is incomplete.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Graph-capability diagnostic: " + message); }
    }
}
