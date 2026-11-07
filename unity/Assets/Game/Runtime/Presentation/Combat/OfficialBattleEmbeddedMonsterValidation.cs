using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Isolated fixture for the public battle host; never edits a user profile.
    public sealed class OfficialBattleEmbeddedMonsterValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition attack;
        private readonly int actorId;
        // The public weekly actor has the original skill_04 contract. Other
        // embedded graphs need their own authored source bones/controllers.
        private readonly int[] skills = { 18620 };
        private readonly HashSet<int> casts = new HashSet<int>();
        private readonly HashSet<EmbeddedMonsterNativeSkeleton> observed = new HashSet<EmbeddedMonsterNativeSkeleton>();
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private int next;
        public string CaptureName { get; private set; }

        private OfficialBattleEmbeddedMonsterValidation(FightOfficialCombatCatalog catalog, FightBattleSkillDefinition attack, int actorId)
        { this.catalog = catalog; this.attack = attack; this.actorId = actorId; }

        public static OfficialBattleEmbeddedMonsterValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var enemy = checkpoint.Units.First(value => value.Id != 1);
            var attack = enemy.Skills.First(value => value.Slot == 0);
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 5000000;
            }
            var probe = new OfficialBattleEmbeddedMonsterValidation(catalog, attack, enemy.Id);
            probe.Assign(enemy, probe.skills[0]);
            return probe;
        }

        private void Assign(FightBattleUnitState actor, int entity)
        {
            actor.Skills.Clear(); actor.SkillState.Slots.Clear();
            var skill = entity == 0 ? attack : catalog.CreateSkill(entity, 0, 0, 1, 1, 1, null);
            if (entity != 0) skill.LimitedTimes = 1;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (next >= skills.Length || !controller.Checkpoint.Receipts.Last().Event.SkillCasts
                .Any(value => value.ActorId == actorId && value.SkillEntityId == skills[next])) return;
            casts.Add(skills[next++]);
            var actor = controller.Checkpoint.Units.Single(value => value.Id == actorId);
            Assign(actor, next < skills.Length ? skills[next] : 0);
            if (next == skills.Length)
                foreach (var property in actor.Properties.Concat(actor.BaseProperties))
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100000000;
        }

        public void ObserveFrame()
        {
            CaptureName = null;
            foreach (var native in UnityEngine.Object.FindObjectsOfType<EmbeddedMonsterNativeSkeleton>())
            {
                if (native.Elapsed <= .05f || !native.VisibleRenderer || !native.VisibleRenderer.isVisible) continue;
                var mesh = native.VisibleRenderer.GetComponent<MeshFilter>();
                if (!mesh || !mesh.sharedMesh || mesh.sharedMesh.vertexCount == 0 || observed.Contains(native)) continue;
                int id = native.SkillEntityId;
                Require(skills.Contains(id), "Unexpected embedded skeleton in this fixture.");
                Require(mesh.sharedMesh.vertices.All(value => float.IsFinite(value.x) && float.IsFinite(value.y)), "Nonfinite public geometry.");
                Require(native.VisibleRenderer.sharedMaterials.All(value => value && value.mainTexture), "Missing public original atlas.");
                observed.Add(native); counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
                CaptureName = "embedded-" + id + "-" + counts[id];
                Debug.Log("[Embedded monster] public original graph " + id + " / " + native.AnimationName + " advanced and visible.");
            }
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(checkpoint.Finished && casts.SetEquals(skills), "Public embedded casts were incomplete.");
            Require(counts.TryGetValue(18620, out int second) && second == 5, "Public embedded instances were incomplete.");
            Require(observed.All(value => !value), "Embedded graph renderers survived battle release.");
            Debug.Log("[Embedded monster] public 18620: 5 active instances, original graph, clocks, geometry, textures, settlement and release passed. Other embedded graphs require their authored monster/partner sources.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original embedded battle: " + message); }
    }
}
