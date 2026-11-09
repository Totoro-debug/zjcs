using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // An isolated pet/owner topology exercises the original graph through the
    // public battle host. It does not grant or unlock this skill in a user save.
    public sealed class OfficialBattleMasterChildValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition finishingAttack;
        private bool settled, captured, animated;
        private int petId;
        private int nativeCasts;
        private float castTime;
        private string lastNativeState;
        public string CaptureName { get; private set; }

        private OfficialBattleMasterChildValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition finishingAttack)
        { this.catalog = catalog; this.finishingAttack = finishingAttack; }

        public static OfficialBattleMasterChildValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var owner = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Id != 1);
            var probe = new OfficialBattleMasterChildValidation(catalog, owner.Skills.First(value => value.Slot == 0));
            int petId = checkpoint.Units.Max(value => value.Id) + 1;
            var pet = new FightBattleUnitState {
                Id = petId, MasterId = owner.Id, Side = owner.Side,
                MonsterId = 9320, MonsterGroupNo = 0,
                MonsterTemplateId = catalog.MonsterTemplateId(9320),
                CharacterType = catalog.MonsterCharacterType(9320),
                MonsterType = catalog.MonsterType(9320), AiType = catalog.MonsterAiType(9320),
                MaxHpPlies = catalog.MonsterHpPliesFor(9320, 0, owner.Level),
                BodyOffsets = catalog.MonsterBodyOffsets(9320),
                Level = owner.Level, SubRank = owner.SubRank, Profession = owner.Profession,
                MoveDist = owner.MoveDist, MoveMask = owner.MoveMask,
                Properties = owner.Properties.Select(value => new FightPropertyValue {
                    Id = value.Id, Value = value.Value }).ToList(),
                BaseProperties = (owner.BaseProperties ?? owner.Properties).Select(value => new FightPropertyValue {
                    Id = value.Id, Value = value.Value }).ToList(),
                Turn = new FightTurnUnit { Id = petId, MasterId = owner.Id }
            };
            Require(pet.MonsterTemplateId == 9320 && pet.CharacterType == 7 && pet.BodyOffsets.Count == 1,
                "Original evolved pet template/body identity is missing.");
            probe.petId = pet.Id;
            pet.X = enemy.X - 1; pet.Y = enemy.Y - 3;
            Require(checkpoint.Grid.Contains(new FightGridPoint(pet.X, pet.Y))
                && (pet.X != owner.X || pet.Y != owner.Y), "The pet has no independent grid cell.");
            checkpoint.Units.Add(pet);
            // This fixture supplies standable burning terrain; the production
            // spawner still checks the official mask for every selected cell.
            checkpoint.Grid.DefaultGroundType = 1;
            foreach (var cell in checkpoint.Grid.Cells) cell.GroundType = 1;
            foreach (var unit in checkpoint.Units)
            {
                unit.Statuses.Clear(); unit.Skills.Clear(); unit.SkillState.Slots.Clear();
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 1000000000;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000;
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = unit == pet ? 1000 : 1;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 1000000000;
                unit.Turn.Speed = unit == pet ? 1000 : 1;
            }
            var skill = catalog.CreateSkill(54370, 0, 16442, 5, 20, pet.SubRank,
                catalog.CalculateSkillProps(54370, 5, 20, pet.SubRank)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            skill.LimitedTimes = 1;
            pet.Skills.Add(skill); pet.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
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
                if (cast.SkillEntityId != 54370) return;
                nativeCasts++; castTime = Time.realtimeSinceStartup;
            };
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (settled) return;
            var checkpoint = controller.Checkpoint;
            var step = checkpoint.Receipts.Last().Event;
            var cast = step.SkillCasts.FirstOrDefault(value => value.SkillEntityId == 54370);
            if (cast == null) return;
            var pet = checkpoint.Units.Single(value => value.Id == cast.ActorId);
            var children = step.SkillCasts.Where(value => value.SkillEntityId == 54373 || value.SkillEntityId == 54375).ToArray();
            Require(children.Length == 2 && children.All(value => value.ActorId == pet.Id
                && value.SkillItemId == 16442 && value.TargetIds.SequenceEqual(new[] { pet.MasterId })
                && value.Slot == -1), "Child graph lost its caster, explicit master target or root item.");
            Require(checkpoint.GridItems.Any(value => !value.Ended && value.Runtime.Statuses.Any(status =>
                status.ClassId == 54372 && status.CreatorId == pet.Id && status.Origin?.SkillEntityId == 54370)),
                "The original child graph did not create its burning grid status.");
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            Require(restored.RandomSeed == checkpoint.RandomSeed && restored.GridItems.Count == checkpoint.GridItems.Count
                && pet.SkillState.Slots[0].ReleasedTimes == 1, "Grid/random checkpoint or ordinary release differs.");
            Debug.Log("[Master child] original 54370 -> 54373 -> 54375, explicit master, burning tiles, one release and checkpoint passed.");
            settled = true;
            pet.Skills.Clear(); pet.SkillState.Slots.Clear();
            var owner = checkpoint.Units.Single(value => value.Id == pet.MasterId);
            var enemy = checkpoint.Units.First(value => value.Side != owner.Side);
            owner.X = enemy.X; owner.Y = enemy.Y - 3;
            foreach (var property in pet.Properties.Concat(pet.BaseProperties ?? pet.Properties))
                if (property.Id == catalog.PropertyType("Speed")) property.Value = 1;
            pet.Turn.Speed = 1;
            owner.Skills.Add(finishingAttack); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(finishingAttack, 0));
            foreach (var property in owner.Properties.Concat(owner.BaseProperties ?? owner.Properties))
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000000000;
        }

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (captured) return;
            var pet = controller.TryGetUnitTransform(petId);
            var native = pet ? pet.GetComponentInChildren<BattleNativeMonsterRenderer>(true) : null;
            string nativeState = !pet ? "missing transform" : !native ? "missing native renderer: " + pet.name
                : native.name + "/" + native.Animation + "/active=" + native.gameObject.activeInHierarchy
                    + "/visible=" + native.GetComponent<MeshRenderer>().isVisible;
            if (nativeState != lastNativeState)
            { lastNativeState = nativeState; Debug.Log("[Master child native] " + nativeState); }
            Require(controller.Checkpoint.Units.Single(value => value.Id == petId).MonsterTemplateId == 9320,
                "The native pet view has a foreign template identity.");
            if (native) Require(native.name == "PetModel_9320", "The original pet model node is not bound.");
            // playable54370 sends skill_03; the original 9320 fight controller
            // routes that parameter to state 1102634108722742392, whose motion
            // is skill_02 (b07d11110465eb4a340705c8987ae233).
            animated |= native && native.Animation == "skill_02" && native.AnimatorFrame.Current.Time > .04f;
            if (!animated || !pet || !pet.GetComponentsInChildren<MeshRenderer>().Any(value => value.enabled
                && value.isVisible && !value.forceRenderingOff
                && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)) return;
            if (!settled || nativeCasts == 0 || Time.realtimeSinceStartup - castTime < .8f) return;
            captured = true; CaptureName = "master-child-54370";
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(settled && animated && captured && nativeCasts == 1 && checkpoint.Finished,
                "The graph, native cast playback, simulated input or settlement was incomplete.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Master-child diagnostic: " + message); }
    }
}
