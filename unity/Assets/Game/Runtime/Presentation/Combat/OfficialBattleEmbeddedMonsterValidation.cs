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
        private readonly int[] skills;
        private readonly int sourceEntity;
        private bool sourceModelChecked;
        private readonly HashSet<int> casts = new HashSet<int>();
        private readonly HashSet<EmbeddedMonsterNativeSkeleton> observed = new HashSet<EmbeddedMonsterNativeSkeleton>();
        private readonly HashSet<EmbeddedMonsterNativeSkeleton> traced = new HashSet<EmbeddedMonsterNativeSkeleton>();
        private readonly HashSet<ParticleSystem> particles = new HashSet<ParticleSystem>();
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private int next;
        public string CaptureName { get; private set; }

        private OfficialBattleEmbeddedMonsterValidation(FightOfficialCombatCatalog catalog, FightBattleSkillDefinition attack, int actorId, int sourceEntity)
        {
            this.catalog = catalog; this.attack = attack; this.actorId = actorId; this.sourceEntity = sourceEntity;
            skills = new[] { sourceEntity switch { 0 or 3620 => 18620, 3331 => 59470, 3481 => 18160,
                _ => throw new ArgumentOutOfRangeException(nameof(sourceEntity)) } };
        }

        public static OfficialBattleEmbeddedMonsterValidation Configure(FightBattleCheckpoint checkpoint, int sourceEntity = 0)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var enemy = checkpoint.Units.First(value => value.Id != 1);
            var attack = enemy.Skills.First(value => value.Slot == 0);
            if (sourceEntity != 0)
            {
                Require(new[] { 3331, 3481, 3620 }.Contains(sourceEntity), "Unknown original source actor.");
                enemy.MonsterId = sourceEntity; enemy.MonsterGroupNo = 0;
                OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
                Require(enemy.MonsterTemplateId == sourceEntity, "Original source template identity differs.");
                // Bound the isolated fixture; the public host still owns actual
                // turn-limit settlement and the native start interaction.
                checkpoint.MaxRound = 3;
                if (checkpoint.IsPlayerRoundLimit) checkpoint.PlayerRoundLimit = 3;
            }
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.BaseProperties == null ? unit.Properties : unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 5000000;
            }
            var probe = new OfficialBattleEmbeddedMonsterValidation(catalog, attack, enemy.Id, sourceEntity);
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

        public void ObserveFrame(OfficialBattleRuntimeController controller)
        {
            CaptureName = null;
            if (sourceEntity != 0 && !sourceModelChecked)
            {
                var root = controller.TryGetUnitTransform(actorId);
                var row = BattleNativeMonsterAssets.Load().Find(sourceEntity);
                var nodes = ((List<object>)row["transforms"]).Cast<Dictionary<string, object>>().ToArray();
                var model = root ? root.Find(string.Join("/", nodes.Skip(1).Select(value => (string)value["name"]))) : null;
                Require(model && model.GetComponent<BattleNativeMonsterRenderer>(), "Public battle did not mount the original source model.");
                if (sourceEntity == 3620)
                {
                    var portrait = OfficialBattleMonsterPortraitCatalog.TryLoadDefault()?.Find(3620);
                    var card = GameObject.Find("GameRoundCharacterItem_" + actorId);
                    Require(portrait && portrait.name == "G_3620"
                        && portrait.texture.format == TextureFormat.RGBA32 && card
                        && card.GetComponentsInChildren<SpriteRenderer>(true).Any(value =>
                            value.gameObject.activeInHierarchy && value.sprite == portrait),
                        "Public timeline did not bind the original 3620 portrait.");
                }
                sourceModelChecked = true;
            }
            foreach (var native in Resources.FindObjectsOfTypeAll<EmbeddedMonsterNativeSkeleton>()
                .Where(value => value.gameObject.scene.IsValid()))
            {
                var geometry = native.VisibleRenderer ? native.VisibleRenderer.GetComponent<MeshFilter>() : null;
                if (traced.Add(native))
                    Debug.Log("[Embedded monster trace] bound " + native.SkillEntityId + " active=" + native.gameObject.activeInHierarchy
                        + " elapsed=" + native.Elapsed + " position=" + native.transform.position);
                // Original 18160 leaves effect (1) out of myPlayables. Its embedded
                // skeleton must stay dormant; the scheduled effect uses particles.
                if (native.SkillEntityId == 18160)
                    Require(!native.gameObject.activeSelf && native.Elapsed == 0f,
                        "The original unscheduled 18160 skeleton was activated.");
                if (!native.gameObject.activeInHierarchy) continue;
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
            if (skills[0] == 18160 && particles.Count == 0)
                foreach (var particle in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
                {
                    Transform graph = particle.transform;
                    while (graph && graph.name != "playable18160(Clone)") graph = graph.parent;
                    var renderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (!graph || particle.particleCount == 0 || !renderer || !renderer.isVisible) continue;
                    Require(renderer.sharedMaterials.All(value => value && value.mainTexture), "Missing original 18160 particle atlas.");
                    particles.Add(particle);
                    CaptureName = "original-18160-particles";
                    Debug.Log("[Embedded monster] original scheduled 18160 particle branch advanced and visible; unscheduled skeleton stays inactive.");
                    break;
                }
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Require(checkpoint.Finished && casts.SetEquals(skills), "Public embedded casts were incomplete.");
            counts.TryGetValue(skills[0], out int count);
            if (skills[0] == 18160)
                Require(count == 0 && traced.Count == 1 && particles.Count == 1,
                    "Original scheduled particles or dormant skeleton were incomplete.");
            else Require(count == (skills[0] == 18620 ? 5 : 1), "Public embedded instances were incomplete.");
            Require(sourceEntity == 0 || sourceModelChecked, "Original public source model was not checked.");
            Require(observed.All(value => !value), "Embedded graph renderers survived battle release.");
            Require(traced.All(value => !value) && particles.All(value => !value), "Original graph branches survived battle release.");
            Debug.Log("[Embedded monster] public " + skills[0] + " source=" + sourceEntity + ": " + count
                + " active instances, original graph, clocks, geometry, textures, settlement and release passed. This isolated fixture does not verify natural acquisition or full original loadouts.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original embedded battle: " + message); }
    }
}
