using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunPetMaterials()
        {
            Run();
            AudioListener.volume = 0;
            SessionState.SetBool(Key + ".petMaterials", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
        }

        private static IEnumerator PetMaterialSteps()
        {
            AudioListener.volume = 0;
            int[] entities = { 3181, 3182, 3183, 9204, 9205, 9206, 9207, 9208, 9209,
                9210, 9211, 9212, 9213, 9214, 9216, 9217, 9218, 9219, 9220, 9222,
                9301, 9302, 9303, 9304, 9305, 9306, 9307, 9308, 9309, 9310, 9311,
                9312, 9314, 9315, 9316, 9317, 9318, 9320, 9322 };
            var damage = Resources.Load<GameObject>("Combat/SkillGraphs/GameObject/playable_damage_wuli_bigger");
            Require(damage, "original damage material graph");
            var configuration = damage.GetComponentInChildren<MyPlayableMaterialProperty>(true);
            Require(configuration && configuration.Keyword == "_HITEFFECT" && configuration.FloatPropertyName == "_HitEffectBlend"
                && configuration.Duration == .15f && configuration.NeedResume, "original damage material fields");
            var prefab = configuration.gameObject;
            foreach (int entity in entities)
            {
                var world = new GameObject("Original actor material validation " + entity);
                using var scope = new OfflineBattleNativePlayableScope(world.transform, null);
                IEnumerator Pump()
                {
                    for (int frame = 0; frame < 4; frame++)
                    {
                        yield return null;
                        scope.Tick(.01f); scope.LateTick();
                    }
                }
                using var actor = OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState {
                    Id = 1, CharacterType = 5, MonsterId = entity, MonsterTemplateId = entity, Hp = 100 }, world.transform);
                using var peer = OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState {
                    Id = 2, CharacterType = 5, MonsterId = entity, MonsterTemplateId = entity, Hp = 100 }, world.transform);
                try
                {
                    Require(actor != null && peer != null, "installed original actor " + entity);
                    peer.Transform.position = new Vector3(1000, 1000, 0);
                    var renderer = actor.Transform.GetComponentInChildren<BattleNativeMonsterRenderer>();
                    var peerRenderer = peer.Transform.GetComponentInChildren<BattleNativeMonsterRenderer>();
                    var appearance = renderer.GetComponent<AppearanceRendererLite>();
                    var property = actor.Transform.GetComponent<MeshMaterialProperty>();
                    var peerProperty = peer.Transform.GetComponent<MeshMaterialProperty>();
                    Require(appearance && property && peerProperty, "original appearance and material property " + entity);
                    Require((property.MeshRenderer != null) == (entity < 9000), "original nullable renderer field " + entity);
                    property.Init(); peerProperty.Init(); actor.Tick(0); peer.Tick(0);
                    var materials = appearance.GetIndependentMaterials();
                    var peerMaterials = peerRenderer.GetComponent<AppearanceRendererLite>().GetIndependentMaterials();
                    Require(property.Valid && materials.Length > 0 && !materials.Intersect(peerMaterials).Any(),
                        "per-instance original material collection " + entity);
                    Require(materials.SequenceEqual(Private<System.Collections.Generic.IEnumerable<Material>>(property, "mMaterials")),
                        "original appearance material order " + entity);
                    Require(property.HasFloat(configuration.FloatPropertyName) && property.HasColor(configuration.ColorPropertyName)
                        && !property.HasFloat("_DitherAlpha"), "original shader supported properties " + entity);
                    foreach (var animator in world.GetComponentsInChildren<Animator>(true)) animator.speed = 0;
                    var defaults = materials.Select(material => material.GetFloat(configuration.FloatPropertyName)).ToArray();
                    var colorDefaults = materials.Select(material => material.GetColor(configuration.ColorPropertyName)).ToArray();
                    var keywords = materials.Select(material => material.IsKeywordEnabled(configuration.Keyword)).ToArray();
                    var peerDefaults = peerMaterials.Select(material => material.GetFloat(configuration.FloatPropertyName)).ToArray();
                    foreach (var particles in world.GetComponentsInChildren<ParticleSystem>(true)) particles.Pause(true);
                    var bounds = renderer.GetComponent<MeshRenderer>().bounds;
                    string label = "actor-material-" + entity;
                    CaptureSceneRendererCamera(bounds, label + "-visibility");
                    var baseline = CaptureSceneRendererCamera(bounds, label + "-before");
                    var first = scope.Play(prefab, actor.Transform.gameObject, actor.Transform.gameObject);
                    yield return Pump(); actor.Tick(0);
                    var leaf = first.GetComponentInChildren<MyPlayableMaterialProperty>(true);
                    float expected = leaf.FloatCurve.Evaluate(.04f / leaf.Duration);
                    Require(leaf.myApplyComponent == property, "original graph selects the actor collector " + entity);
                    Require(materials.All(material =>
                            material.IsKeywordEnabled(leaf.Keyword) && Mathf.Approximately(material.GetFloat(leaf.FloatPropertyName), expected)),
                        "original damage node updates the appearance-only materials " + entity
                        + "; expected=" + expected + "; actual=" + string.Join(",", materials.Select(material =>
                            material.GetFloat(leaf.FloatPropertyName) + "/" + material.IsKeywordEnabled(leaf.Keyword))));
                    Require(peerMaterials.Select(material => material.GetFloat(leaf.FloatPropertyName)).SequenceEqual(peerDefaults),
                        "material graph does not alter the peer " + entity);
                    var changed = CaptureSceneRendererCamera(bounds, label + "-active");
                    Require(!baseline.SequenceEqual(changed), "actual original actor raster changes " + entity);
                    var second = scope.Play(prefab, actor.Transform.gameObject, actor.Transform.gameObject);
                    yield return Pump();
                    first.Stop(true); yield return Pump();
                    Require(materials.All(material => material.IsKeywordEnabled(leaf.Keyword)
                            && Mathf.Approximately(material.GetFloat(leaf.FloatPropertyName), leaf.FloatCurve.Evaluate(.08f / leaf.Duration))),
                        "overlapping original material leases " + entity);
                    second.Stop(true); yield return Pump(); actor.Tick(0);
                    Require(materials.Select(material => material.GetFloat(leaf.FloatPropertyName)).SequenceEqual(defaults)
                        && materials.Select(material => material.GetColor(leaf.ColorPropertyName)).SequenceEqual(colorDefaults)
                        && materials.Select(material => material.IsKeywordEnabled(leaf.Keyword)).SequenceEqual(keywords),
                        "last original graph restores each material " + entity);
                    Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-restored")),
                        "actual original actor raster is restored " + entity);
                    Debug.Log("Original actor material passed: " + entity + "; independent materials, original nullable field, graph overlap, peer isolation and GPU restoration.");
                }
                finally
                {
                    scope.Dispose(); actor?.Dispose(); peer?.Dispose();
                    UnityEngine.Object.DestroyImmediate(world);
                }
                yield return null;
            }
        }
    }
}
