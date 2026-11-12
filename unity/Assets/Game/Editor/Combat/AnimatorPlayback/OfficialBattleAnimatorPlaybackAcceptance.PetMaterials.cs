using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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
            => RunActorMaterials();

        public static void RunActorMaterials()
        {
            Run();
            AudioListener.volume = 0;
            SessionState.SetBool(Key + ".petMaterials", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
        }

        public static void RunTrainingMonsterMaterials()
        {
            RunActorMaterials();
            SessionState.SetBool(Key + ".trainingMonsterMaterials", true);
        }

        private static IEnumerator PetMaterialSteps()
        {
            AudioListener.volume = 0;
            int[] entities = { 3803, 3181, 3182, 3183, 3198, 3204, 3206, 3207, 3331, 3481, 3620, 3709,
                3903, 3904, 3905, 3909, 3910, 3911, 3912, 3913, 3914, 3915, 3917, 3920,
                3923, 3925, 3948, 3949, 9201, 9202, 9203, 9215, 9204, 9205, 9206, 9207, 9208, 9209,
                9210, 9211, 9212, 9213, 9214, 9216, 9217, 9218, 9219, 9220, 9222,
                9301, 9302, 9303, 9304, 9305, 9306, 9307, 9308, 9309, 9310, 9311,
                9312, 9314, 9315, 9316, 9317, 9318, 9320, 9322 };
            if (SessionState.GetBool(Key + ".trainingMonsterMaterials", false)) entities = new[] { 3204 };
            SessionState.EraseBool(Key + ".trainingMonsterMaterials");
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
                    Require((property.MeshRenderer != null) == (entity < 9204 || entity == 9215), "original nullable renderer field " + entity);
                    property.Init(); peerProperty.Init(); actor.Tick(0); peer.Tick(0);
                    var materials = appearance.GetIndependentMaterials();
                    var peerMaterials = peerRenderer.GetComponent<AppearanceRendererLite>().GetIndependentMaterials();
                    Require(property.Valid && materials.Length > 0 && !materials.Intersect(peerMaterials).Any(),
                        "per-instance original material collection " + entity);
                    Require(materials.SequenceEqual(Private<System.Collections.Generic.IEnumerable<Material>>(property, "mMaterials")),
                        "original appearance material order " + entity);
                    if (entity == 3803)
                    {
                        var special = SpecialMeshMaterialSteps(world, scope, actor, peer, configuration);
                        while (special.MoveNext()) yield return special.Current;
                        continue;
                    }
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

        private static IEnumerator SpecialMeshMaterialSteps(GameObject world, OfflineBattleNativePlayableScope scope,
            OfficialBattleLiveMonsterModelView actor, OfficialBattleLiveMonsterModelView peer,
            MyPlayableMaterialProperty configuration)
        {
            var renderer = actor.Transform.GetComponentInChildren<BattleNativeMonsterRenderer>();
            var appearance = renderer.GetComponent<AppearanceRendererLite>();
            var property = actor.Transform.GetComponent<MeshMaterialProperty>();
            var materials = appearance.GetIndependentMaterials();
            var original = BattleNativeMonsterAppearanceCatalog.Find(3803).Materials;
            Require(appearance.LevelOfResolution == LevelOfResolution.None && appearance.TextureLoader == null,
                "3803 original None and absent loader branch");
            Require(original.Length == 8 && materials.Length == 3 && renderer.CustomMaterialOverride.Count == 3,
                "3803 clones the three skin pages, excluding five replacement materials");
            Require(materials.All(material => original.Any(source => source.mainTexture == material.mainTexture
                    && source.shader == material.shader)), "3803 keeps original textures and shaders");
            Require(!property.HasFloat(configuration.FloatPropertyName) && !property.HasColor(configuration.ColorPropertyName),
                "3803 mixed original shaders reject unsupported shared damage fields");
            foreach (var animator in world.GetComponentsInChildren<Animator>(true)) animator.speed = 0;
            foreach (var particles in world.GetComponentsInChildren<ParticleSystem>(true)) particles.Pause(true);
            var copies = appearance.RenderExistingMeshes.Cast<BattleNativeMonsterMeshCopy>().ToArray();
            Require(copies.Length == 4 && copies.Count(copy => copy.enabled) == 3,
                "3803 four original copy references and disabled copy");
            void VerifyCopies()
            {
                var reference = renderer.GetComponent<MeshRenderer>();
                foreach (var copy in copies.Where(copy => copy.isActiveAndEnabled))
                {
                    Require(copy.ReferenceRenderer == reference && copy.GetComponent<MeshFilter>().sharedMesh
                        == reference.GetComponent<MeshFilter>().sharedMesh, "3803 exact shared copy mesh");
                    var expected = reference.sharedMaterials.Select(material => {
                        var source = renderer.CustomMaterialOverride.Single(pair => pair.Value == material).Key;
                        return copy.ReplacementMaterialDict[source];
                    }).ToArray();
                    Require(copy.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(expected),
                        "3803 original-to-runtime copy replacement forwarding");
                }
            }
            actor.Tick(0); VerifyCopies();
            var bounds = renderer.GetComponent<MeshRenderer>().bounds;
            CaptureSceneRendererCamera(bounds, "actor-material-3803-visibility");
            CaptureSceneRendererCamera(bounds, "actor-material-3803-before");
            var baseline = CaptureSpecialMaterialRaster(renderer, bounds, "before");
            var keywords = materials.Select(material => material.IsKeywordEnabled(configuration.Keyword)).ToArray();
            var peerMaterials = peer.Transform.GetComponentInChildren<AppearanceRendererLite>().GetIndependentMaterials();
            var peerKeywords = peerMaterials.Select(material => material.IsKeywordEnabled(configuration.Keyword)).ToArray();
            var first = scope.Play(configuration.gameObject, actor.Transform.gameObject, actor.Transform.gameObject);
            for (int i = 0; i < 4; i++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
            actor.Tick(0); VerifyCopies();
            var leaf = first.GetComponentInChildren<MyPlayableMaterialProperty>(true);
            var counters = Private<System.Collections.Generic.Dictionary<string, int>>(property, "mCounterDic");
            bool OriginalKeywordSupport() => materials.All(material => material.IsKeywordEnabled(leaf.Keyword)
                == (material.shader.name == "Universal Render Pipeline/Spine/SkeletonEffect-2D"));
            Require(leaf.myApplyComponent == property && counters.TryGetValue(leaf.Keyword, out int count) && count == 1
                && OriginalKeywordSupport(), "3803 original keyword lease and mixed shader keyword support");
            Require(!Private<bool>(leaf, "mHasFloat") && !Private<bool>(leaf, "mHasColor"),
                "3803 original damage node keeps unsupported float/color disabled");
            Require(peerMaterials.Select(material => material.IsKeywordEnabled(leaf.Keyword)).SequenceEqual(peerKeywords),
                "3803 peer material isolation");
            CaptureSceneRendererCamera(bounds, "actor-material-3803-active");
            var second = scope.Play(configuration.gameObject, actor.Transform.gameObject, actor.Transform.gameObject);
            for (int i = 0; i < 4; i++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
            first.Stop(true);
            for (int i = 0; i < 4; i++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
            Require(counters.TryGetValue(leaf.Keyword, out count) && count == 1 && OriginalKeywordSupport(),
                "3803 overlapping original keyword leases retain remaining owner");
            second.Stop(true);
            for (int i = 0; i < 4; i++) { yield return null; scope.Tick(.01f); scope.LateTick(); }
            actor.Tick(0); VerifyCopies();
            Require(materials.Select(material => material.IsKeywordEnabled(leaf.Keyword)).SequenceEqual(keywords),
                "3803 original keyword restoration");
            Require(!counters.ContainsKey(leaf.Keyword), "3803 returns the final original keyword lease");
            CaptureSceneRendererCamera(bounds, "actor-material-3803-restored");
            Require(baseline.SequenceEqual(CaptureSpecialMaterialRaster(renderer, bounds, "restored")),
                "3803 original full raster restoration");
            Debug.Log("Original actor material passed: 3803; None, direct textures, three independent skin pages, four exact mesh-copy references, mixed shader capabilities and GPU restoration.");
        }

        private static byte[] CaptureSpecialMaterialRaster(BattleNativeMonsterRenderer source, Bounds bounds, string name)
        {
            // Draw the production meshes/materials with a deterministic shader
            // clock. The camera captures above retain the original moving noise.
            var renderers = source.EntityRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(value => value.enabled && value.gameObject.activeInHierarchy && value.GetComponent<MeshFilter>()?.sharedMesh)
                .OrderBy(value => SortingLayer.GetLayerValueFromID(value.sortingLayerID)).ThenBy(value => value.sortingOrder);
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f;
            var view = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var image = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(view, Matrix4x4.identity);
                    string[] clocks = { "_Time", "_SinTime", "_CosTime", "_TimeParameters" };
                    var saved = clocks.Select(Shader.GetGlobalVector).ToArray();
                    for (int i = 0; i < clocks.Length; i++)
                        commands.SetGlobalVector(clocks[i], i == 2 ? Vector4.one : i == 3 ? new Vector4(0, 0, 1, 0) : Vector4.zero);
                    foreach (var renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) commands.DrawRenderer(renderer, materials[i], i, 0);
                    }
                    for (int i = 0; i < clocks.Length; i++) commands.SetGlobalVector(clocks[i], saved[i]);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
                Require(image.GetPixels32().Count(pixel => pixel.a > 8) > 100, "3803 deterministic GPU draw is visible");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                    "scene-renderer-actor-material-3803-" + name + ".png"), image.EncodeToPNG());
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image);
            }
        }
    }
}
