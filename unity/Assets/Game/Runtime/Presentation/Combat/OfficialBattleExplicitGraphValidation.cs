using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Controlled topology tests only the three approved explicit-body exceptions.
    public sealed class OfficialBattleExplicitGraphValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition finisher;
        private readonly int skillId, sourceId;
        private bool settled, animated, captured, inspected;
        private int nativeCasts, nativeCures;
        private float castTime;
        public string CaptureName { get; private set; }
        private OfficialBattleExplicitGraphValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition finisher, int skillId, int sourceId)
        { this.catalog = catalog; this.finisher = finisher; this.skillId = skillId; this.sourceId = sourceId; }

        public static OfficialBattleExplicitGraphValidation Configure(FightBattleCheckpoint checkpoint)
        {
            Require(int.TryParse(Environment.GetEnvironmentVariable("ZJCS_EXPLICIT_GRAPH_ACCEPTANCE"), out int id)
                && (id == 50213 || id == 50230 || id == 64450), "Unknown original exception graph.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var owner = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != owner.Side);
            var finisher = owner.Skills.First(value => value.Slot == 0);
            enemy.MonsterId = 3665; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(3665);
            enemy.MonsterType = catalog.MonsterType(3665); enemy.AiType = catalog.MonsterAiType(3665);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(3665, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            owner.X = enemy.X; owner.Y = enemy.Y - 2;
            var source = owner;
            if (id != 64450)
            {
                int template = id == 50213 ? 9220 : 9222;
                int unitId = checkpoint.Units.Max(value => value.Id) + 1;
                source = new FightBattleUnitState {
                    Id = unitId, MasterId = owner.Id, Side = owner.Side,
                    MonsterId = template, MonsterTemplateId = template,
                    CharacterType = 5,
                    MonsterType = catalog.MonsterType(template), AiType = catalog.MonsterAiType(template),
                    BodyOffsets = catalog.MonsterBodyOffsets(template),
                    MaxHpPlies = catalog.MonsterHpPliesFor(template, 0, owner.Level),
                    // Original 50213 has a zero-cell range and targets its master.
                    // This controlled topology exercises that graph at its authored anchor.
                    X = id == 50213 ? owner.X : owner.X - 1, Y = owner.Y,
                    Level = owner.Level, SubRank = owner.SubRank,
                    Properties = owner.Properties.Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList(),
                    BaseProperties = (owner.BaseProperties ?? owner.Properties).Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList(),
                    Turn = new FightTurnUnit { Id = unitId, MasterId = owner.Id }
                };
                Require(source.MonsterTemplateId == template, "Original pet template is absent.");
                Require(catalog.HasComponent(template, "FightRolePetComponent"),
                    "The original follower must be a pet entity, not a monster-table row.");
                checkpoint.Units.Add(source);
            }
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                long maxHp = unit.Side == owner.Side ? 5000000 : 1000000000;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = maxHp;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == source ? 1000 : 1;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = maxHp;
                unit.Turn.Speed = unit == source ? 1000 : 1;
            }
            if (id == 50230) owner.Hp = owner.Turn.Hp = 2500000;
            int item = id == 50213 ? 16020 : id == 50230 ? 16021 : 15153;
            var skill = catalog.CreateSkill(id, 0, item, 5, 20, owner.SubRank,
                catalog.CalculateSkillProps(id, 5, 20, owner.SubRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            Require(!skill.CanTriggerChild, "Original exception no longer has its automatic-child gate.");
            skill.LimitedTimes = 1;
            source.Skills.Add(skill); source.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.MaxRound = 12; checkpoint.RandomSeed = 91;
            return new OfficialBattleExplicitGraphValidation(catalog, finisher, id, source.Id);
        }

        public void BindPresentation(OfficialBattleRuntimeController controller)
        {
            var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var entities = (OfficialBattleSkillEntityPlayback)typeof(OfficialBattleSkillGraphPlayback)
                .GetField("entityViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
            entities.CastStarted += cast => {
                if (cast.SkillEntityId == 50235 && cast.ActorId == sourceId) nativeCures++;
                if (cast.SkillEntityId != skillId || cast.ActorId != sourceId) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (settled) return;
            var checkpoint = controller.Checkpoint; var step = checkpoint.Receipts.Last().Event;
            if (!step.SkillCasts.Any(value => value.ActorId == sourceId && value.SkillEntityId == skillId)) return;
            var source = checkpoint.Units.Single(value => value.Id == sourceId);
            var owner = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != owner.Side);
            if (skillId == 50213)
            {
                Require(step.DamageEvents.Count == 0 && owner.Statuses.Any(value => value.ClassId == 24825
                    && value.CreatorId == sourceId && value.Origin.SkillEntityId == 50213), "Pure-logic master status differs.");
            }
            else if (skillId == 50230)
            {
                var hits = step.DamageEvents.Where(value => value.SkillEntityId == 50230).ToArray();
                var cures = step.DamageEvents.Where(value => value.SkillEntityId == 50235).ToArray();
                Require(hits.Length > 1 && hits.All(value => value.HasHitGridPosition)
                    && cures.Length == 1 && cures[0].TargetId == owner.Id && cures[0].Damage > 0,
                    "Multiple body cells did not produce exactly one master recovery.");
            }
            else
                Require(step.DamageEvents.Count(value => value.SkillEntityId == 64450) == 3
                    && enemy.Statuses.Any(value => value.ClassId == 24772), "Original three mark nodes differ.");
            Require(source.SkillState.Slots[0].ReleasedTimes == 1, "Explicit graph spent extra ordinary releases.");
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(restored.RandomSeed == checkpoint.RandomSeed
                && restored.Receipts.Last().Event.SkillCasts.Count == step.SkillCasts.Count, "Graph checkpoint differs.");
            Debug.Log("[Explicit graph] " + skillId + ": original effect, one release and checkpoint passed.");
            settled = true; source.Skills.Clear(); source.SkillState.Slots.Clear();
            foreach (var property in source.Properties.Concat(source.BaseProperties ?? source.Properties))
                if (property.Id == catalog.PropertyType("Speed")) property.Value = 1;
            source.Turn.Speed = 1;
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured || !settled || nativeCasts != 1) return;
            var source = controller.TryGetUnitTransform(sourceId);
            var native = source ? source.GetComponentInChildren<BattleNativeMonsterRenderer>(true) : null;
            if (!inspected && Time.realtimeSinceStartup - castTime > .1f)
            {
                inspected = true;
                Debug.Log("[Explicit native frame] " + skillId + ": source=" + sourceId
                    + "; renderer=" + (native ? native.name : "absent")
                    + "; animation=" + (native ? native.Animation : "absent")
                    + "; time=" + (native ? native.AnimatorFrame.Current.Time : 0)
                    + "; casts=" + nativeCasts + "; cures=" + nativeCures);
            }
            if (skillId == 50230)
                animated |= native && native.State.Source.Sex == "battle-monster:9222" && native.Animation == "skill_01"
                    && native.AnimatorFrame.Current.Time > .02f;
            // 50213 and 64450 have no original body Playable; do not invent a cast action.
            else animated = true;
            if (!animated || skillId == 50230 && nativeCures != 1
                || Time.realtimeSinceStartup - castTime < .25f || !source
                || !source.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                    && value.isVisible && !value.forceRenderingOff
                    && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            captured = true; CaptureName = "explicit-graph-" + skillId;
            var owner = controller.Checkpoint.Units.Single(value => value.Id == 1);
            owner.Skills.Add(finisher); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
            foreach (var property in owner.Properties.Concat(owner.BaseProperties ?? owner.Properties))
            {
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
                if (property.Id == catalog.PropertyType("Speed")) property.Value = 1000;
            }
            owner.Turn.Speed = 1000;
        }
        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(settled && animated && captured && nativeCasts == 1 && checkpoint.Finished
                && (skillId != 50230 || nativeCures == 1), "Original effect, presentation, input or settlement is incomplete.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Explicit-graph diagnostic: " + message); }
    }
}
