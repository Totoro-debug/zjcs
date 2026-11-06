using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.ProfessionUI
{
    public static partial class OfficialBattleMonsterSceneValidation
    {
        private static IEnumerable<int> SelectedSteps()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var selected = (Environment.GetEnvironmentVariable("ZJCS_BATTLE_MONSTER_ENTITIES") ?? "3803,3206,9306")
                .Split(',').Select(int.Parse).Distinct().ToArray();
            var mapper = InitializeRenderScene();
            var assets = BattleNativeMonsterAssets.Load();
            scene = new OfficialBattleSceneView(null, root.transform, null, mapper, null, null);
            int actions = 0, poses = 0, copies = 0;
            foreach (int id in selected)
            {
                var unit = new FightBattleUnitState { Id = id, MonsterTemplateId = id, Hp = 100, MaxHp = 100, X = 2, Y = 2 };
                var checkpoint = new FightBattleCheckpoint(); checkpoint.Units.Add(unit);
                scene.Refresh(checkpoint); scene.Tick(.1f);
                Transform entity = scene.TryGetUnitTransform(id);
                var native = entity.GetComponentInChildren<BattleNativeMonsterRenderer>();
                Require(native && native.EntityRoot == entity && native.Attachments != null && native.Attachments.Agent,
                    "Selected scene unit lacks its full original model/attachments: " + id);
                var follower = entity.GetComponentInChildren<OfficialBattleShadowBoneFollower>();
                Require(follower && follower.skeletonRenderer == native, "Selected monster shadow is unbound: " + id);
                var renderer = native.GetComponent<MeshRenderer>();
                var source = assets.Find(id);
                model = OfficialBattleLiveMonsterModelView.TryCreate(unit, root.transform);
                foreach (string name in native.AnimationNames)
                {
                    if (!model.HasAnimation(name)) continue;
                    Require(model.Play(name, false, true), "Original state refused its selected action: " + id + "/" + name);
                    float duration = new ProfessionNativeAnimationChannels(native.State.Source, name, originalBezierSegments: true).Duration;
                    bool sawGeometry = false;
                    for (int sample = 0; sample < 5; sample++)
                    {
                        model.Tick(sample == 0 ? 0 : duration / 4);
                        var own = model.ModelTransform.GetComponent<BattleNativeMonsterRenderer>();
                        var filter = own.GetComponent<MeshFilter>();
                        Require(filter.sharedMesh
                            && filter.sharedMesh.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)),
                            "Selected original pose has invalid geometry: " + id + "/" + name);
                        sawGeometry |= filter.sharedMesh.vertexCount > 0;
                        if (filter.sharedMesh.vertexCount == 0)
                            Require(!own.State.CreateDrawSlots().Any(slot => slot.Active
                                && slot.Attachment is ProfessionNativeRenderAttachment
                                && own.State.Pose.IsActive(slot.Attachment.SlotBone)),
                                "Visible original attachments produced no geometry: " + id + "/" + name);
                        CheckFollower(own, model.Transform.GetComponentInChildren<OfficialBattleShadowBoneFollower>());
                        VerifySelectedMeshCopies(own, source, ref copies);
                        poses++;
                    }
                    Require(sawGeometry, "Original action produced no visible pose: " + id + "/" + name);
                    actions++;
                }
                model.Dispose(); model = null;
                // Capture the production scene instance rather than the action probe.
                var bounds = renderer.bounds;
                foreach (var other in entity.GetComponentsInChildren<MeshRenderer>())
                    if (other.enabled && other.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0) bounds.Encapsulate(other.bounds);
                camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.2f;
                yield return 0; yield return 0;
                Color32[] beforeEffects = Capture(Path.Combine(output, "selected-monster-" + id + ".png"));
                if (id == 3803)
                {
                    var secondPhaseParticles = EnableSelectedSecondPhaseParticles(native);
                    float until = UnityEngine.Time.time + 2f;
                    while (UnityEngine.Time.time < until) yield return 0;
                    VerifySelectedBodyEffects(native, secondPhaseParticles);
                    var effectBounds = renderer.bounds;
                    foreach (var particleRenderer in native.EntityRoot.GetComponentsInChildren<ParticleSystemRenderer>())
                        if (particleRenderer.enabled) effectBounds.Encapsulate(particleRenderer.bounds);
                    camera.transform.position = effectBounds.center + new Vector3(0, 0, -10);
                    camera.orthographicSize = Mathf.Max(effectBounds.extents.y, effectBounds.extents.x / camera.aspect) * 1.2f;
                    yield return 0; yield return 0;
                    Color32[] withEffects = Capture(Path.Combine(output, "selected-monster-3803-particles-aged.png"));
                    int changed = beforeEffects.Zip(withEffects, (a, b) => Math.Abs(a.r - b.r) > 2
                        || Math.Abs(a.g - b.g) > 2 || Math.Abs(a.b - b.b) > 2).Count(value => value);
                    Require(changed > 30, "Original second-phase particles changed no rendered pixels: " + changed);
                }
                unit.X = 3; unit.Y = 3; scene.Refresh(checkpoint); scene.Tick(.1f);
                Require(Vector3.Distance(entity.position, mapper.GridToWorld(new FightGridPoint(3, 3))) < .001f,
                    "Selected native monster did not follow the shared grid.");
                CheckFollower(native, follower);
                scene.Refresh(new FightBattleCheckpoint());
                yield return 0;
                Require(!entity, "Selected native monster/copies leaked after removal.");
            }
            File.WriteAllText(Path.Combine(output, "selected-monster-result.json"),
                "{\"models\":" + selected.Length + ",\"actions\":" + actions + ",\"poses\":" + poses + ",\"copyChecks\":" + copies + "}");
            Debug.Log("[Selected native monsters] models=" + selected.Length + " actions=" + actions + " poses=" + poses + " copyChecks=" + copies);
        }

        private static ParticleSystem[] EnableSelectedSecondPhaseParticles(BattleNativeMonsterRenderer native)
        {
            Transform stage = native.EntityRoot.Find("0template3x3_view/body/MonsterModel_3803/Stage2");
            Require(stage, "Original second-phase model stage is absent.");
            var particles = stage.GetComponentsInChildren<ParticleSystem>(true);
            var renderers = stage.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Require(particles.Length == 33 && renderers.Length == 39,
                "Original second-phase particle graph is incomplete: " + particles.Length + "/" + renderers.Length);
            // This diagnostic activates the source-authored stage subtree and
            // starts its source ParticleSystems. The original stage trigger
            // caller remains a separate battle-rule binding.
            stage.gameObject.SetActive(true);
            foreach (var particle in particles) particle.Play(true);
            return particles;
        }

        private static void VerifySelectedBodyEffects(BattleNativeMonsterRenderer native, ParticleSystem[] secondPhaseParticles)
        {
            var names = new[] { "LeftWings01", "LeftWings01_1", "RightWings01", "RightWings01_1" };
            var all = native.EntityRoot.GetComponentsInChildren<Transform>(true);
            foreach (string name in names)
            {
                var effect = all.Single(node => node.name == "Eff_Npc_ChaosMonarch_" + name);
                Require(effect.GetComponentsInChildren<ParticleSystem>(true).Length > 0,
                    "Original embedded wing effect has no native particles: " + name);
            }
            Require(native.EntityRoot.GetComponentsInChildren<ParticleSystem>()
                .Any(particles => particles.particleCount > 0), "Original wing particles did not emit in Play Mode.");
            Require(secondPhaseParticles.Any(particles => particles.particleCount > 0),
                "Original second-phase particle systems emitted no particles in Play Mode.");
            var nodes = (List<object>)native.Attachments.OriginalRow["nodes"];
            var first = (Dictionary<string, object>)nodes[0];
            var transforms = new Dictionary<string, Transform> { [(string)first["sourceKey"]] = native.EntityRoot };
            foreach (Dictionary<string, object> node in nodes.Skip(1))
                transforms[(string)node["sourceKey"]] = transforms[(string)node["parentKey"]].Find((string)node["name"]);
            foreach (Dictionary<string, object> node in nodes)
            foreach (Dictionary<string, object> component in (List<object>)node["components"])
            {
                if (!Equals(component["type"], "AttachmentFollower")) continue;
                var references = (Dictionary<string, object>)component["references"];
                var follower = transforms[(string)node["sourceKey"]].GetComponent<Zjcs.Official.BattleSpinePlayables.AttachmentFollower>();
                foreach (var reference in references.Where(value => value.Key.StartsWith("/Gos/", StringComparison.Ordinal)))
                {
                    int index = int.Parse(reference.Key.Substring("/Gos/".Length));
                    string sourceKey = (string)((Dictionary<string, object>)reference.Value)["sourceKey"];
                    var targetNode = nodes.Cast<Dictionary<string, object>>().Single(value => Equals(value["gameObjectKey"], sourceKey));
                    Require(follower.Gos[index] && follower.Gos[index].name == (string)targetNode["name"]
                        && follower.Gos[index].transform.IsChildOf(native.EntityRoot),
                        "Original follower visibility target was lost while replacing a particle subtree.");
                }
            }
        }

        private static void VerifySelectedMeshCopies(BattleNativeMonsterRenderer native, Dictionary<string, object> source, ref int count)
        {
            if (!source.TryGetValue("meshCopies", out object value)) return;
            var nodes = (List<object>)native.Attachments.OriginalRow["nodes"];
            var rootNode = (Dictionary<string, object>)nodes[0];
            var transforms = new Dictionary<string, Transform> { [(string)rootNode["sourceKey"]] = native.EntityRoot };
            foreach (Dictionary<string, object> node in nodes.Skip(1))
                transforms[(string)node["sourceKey"]] = transforms[(string)node["parentKey"]].Find((string)node["name"]);
            var materials = (List<object>)source["materials"];
            var referenceMesh = native.GetComponent<MeshFilter>().sharedMesh;
            var referenceMaterials = native.GetComponent<MeshRenderer>().sharedMaterials;
            foreach (Dictionary<string, object> copy in (List<object>)value)
            {
                Transform node = transforms[(string)copy["transform"]];
                var filter = node.GetComponent<MeshFilter>();
                var renderer = node.GetComponent<MeshRenderer>();
                bool enabled = Convert.ToBoolean(((Dictionary<string, object>)copy["fields"])["m_Enabled"]);
                if (!enabled) { Require(!filter.sharedMesh, "Disabled original mesh copy acquired dynamic geometry."); continue; }
                if (!node.gameObject.activeInHierarchy) continue;
                Require(filter.sharedMesh == referenceMesh && renderer.sharedMaterials.Length == referenceMaterials.Length
                    && (referenceMesh.vertexCount == 0 || renderer.sharedMaterials.Length == referenceMesh.subMeshCount),
                    "Original mesh copy lost shared geometry/submesh order.");
                var names = new Dictionary<string, string>();
                foreach (Dictionary<string, object> pair in (List<object>)copy["replacements"])
                {
                    string Name(int index) => (string)((Dictionary<string, object>)((Dictionary<string, object>)materials[index])["fields"])["m_Name"];
                    names.Add(Name(Convert.ToInt32(pair["original"])), Name(Convert.ToInt32(pair["replacement"])));
                }
                for (int i = 0; i < referenceMaterials.Length; i++)
                    Require(renderer.sharedMaterials[i].name == names[referenceMaterials[i].name], "Original mesh copy replaced the wrong atlas page.");
                var originalRenderer = (Dictionary<string, object>)copy["renderer"];
                Require(renderer.sortingLayerID == Convert.ToInt32(originalRenderer["m_SortingLayerID"])
                    && renderer.sortingOrder == Convert.ToInt32(originalRenderer["m_SortingOrder"]), "Original mesh-copy sorting changed.");
                count++;
            }
        }
    }
}
