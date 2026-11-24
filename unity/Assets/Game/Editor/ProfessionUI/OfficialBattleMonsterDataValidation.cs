using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class OfficialBattleMonsterDataValidation
    {
        [Serializable] private sealed class Catalog { public string manifest; public Model[] models; }
        [Serializable] private sealed class Model { public int entityId; public string package; public string[] skins; }
        [Serializable] private sealed class Motion { public int entityId, samples; public string name, error; }
        [Serializable] private sealed class Results { public Motion[] motions; public int models, vertices; }

        public static void ExportTextureTemplate()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            AssetDatabase.CreateAsset(texture, "Assets/monster-texture-template.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new[] { "Assets/monster-texture-template.asset" });
            string target = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "monster-texture-template.asset");
            File.WriteAllText(target, File.ReadAllText("Assets/monster-texture-template.asset").Replace("--- !u!28 &2800000", "--- !u!28 &1"));
            EditorApplication.Exit(0);
        }

        public static void Run()
        {
            try
            {
                string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (string.IsNullOrEmpty(work) || !Path.IsPathRooted(work)) throw new InvalidDataException("Owned monster validation workspace required.");
                string root = Path.Combine(work, "live-monsters");
                var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(root, "catalog.json")));
                var installedIds = BattleNativeMonsterAssets.Load().EntityIds.OrderBy(value => value).ToArray();
                if (catalog?.manifest != "884_156692" || catalog.models == null || catalog.models.Length == 0
                    || !catalog.models.Select(value => value.entityId).OrderBy(value => value).SequenceEqual(installedIds))
                    throw new InvalidDataException("Configured original monster shadow inventory differs.");
                var results = new Results { models = catalog.models.Length };
                var motions = new List<Motion>();
                foreach (var model in catalog.models)
                {
                    var bytes = File.ReadAllBytes(Path.Combine(root, model.package));
                    var source = ProfessionNativeSkeletonData.LoadBattleMonster(bytes, model.entityId);
                    bool rejected = false;
                    try { ProfessionNativeSkeletonData.LoadBattleMonster(bytes, model.entityId + 1); }
                    catch (InvalidDataException) { rejected = true; }
                    if (!rejected) throw new InvalidDataException("Monster package accepted a different entity identity.");
                    var state = new ProfessionNativeAppearanceSkeleton(source);
                    state.SetSkin(source.CreateCombinedNpcSkin(model.skins));
                    using var batches = new ProfessionNativeMeshBatches(source.SlotCount);
                    foreach (string name in source.AnimationNames)
                    {
                        var result = new Motion { entityId = model.entityId, name = name }; motions.Add(result);
                        try
                        {
                            var animation = new ProfessionNativeAnimationChannels(source, name, originalBezierSegments: true);
                            var physics = source.Constraints.Any(value => value.Kind == "physics") ? new ProfessionNativeOrderedPhysicsPose(source) : null;
                            for (int i = 0; i <= 8; i++)
                            {
                                float time = animation.Duration * i / 8;
                                if (physics != null) physics.Apply(state, animation, time, false, animation.Duration / 8);
                                else ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(state, animation, time, false);
                                batches.Build(state.Pose, state.CreateDrawSlots(), true, true, allowConcaveClipping: true);
                                foreach (var batch in batches.Batches)
                                {
                                    var vertices = batch.Mesh.vertices;
                                    if (vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z)))
                                        throw new InvalidDataException("Original monster pose produced a nonfinite vertex.");
                                    results.vertices += vertices.Length;
                                }
                                result.samples++;
                            }
                        }
                        catch (Exception error) { result.error = error.ToString(); }
                    }
                }
                results.motions = motions.ToArray();
                File.WriteAllText(Path.Combine(work, "monster-pose-results.json"), JsonUtility.ToJson(results, true));
                int failed = motions.Count(value => !string.IsNullOrEmpty(value.error));
                Debug.Log("[Original monster native poses] models=" + results.models + " motions=" + motions.Count
                    + " poses=" + motions.Sum(value => value.samples) + " vertices=" + results.vertices + " failed=" + failed);
                foreach (var failure in motions.Where(value => !string.IsNullOrEmpty(value.error)))
                    Debug.LogWarning(failure.entityId + "/" + failure.name + ": " + failure.error);
                EditorApplication.Exit(failed == 0 ? 0 : 1);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void RunSkillActors()
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string selection = Environment.GetEnvironmentVariable("ZJCS_BATTLE_SKILL_ACTOR_IDS");
            if (string.IsNullOrEmpty(work) || !Path.IsPathRooted(work) || string.IsNullOrEmpty(selection))
                throw new InvalidDataException("Select installed skill actors in an owned validation workspace.");
            int[] ids = selection.Split(',').Select(int.Parse).ToArray();
            if (ids.Length == 0 || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
                throw new InvalidDataException("Skill actor selection contains invalid or repeated identities.");
            var results = new Results { models = ids.Length };
            var motions = new List<Motion>();
            ProfessionNativeSkeletonData previous = null;
            foreach (int id in ids)
            {
                var assets = BattleNativeMonsterAssets.Locate(id);
                if (!assets) throw new InvalidDataException("Original skill actor is not installed: " + id);
                var row = assets.Find(id);
                var bytes = assets.Resolve<TextAsset>((string)row["package"]).bytes;
                var source = ProfessionNativeSkeletonData.LoadBattleMonster(bytes, id, assets.SourceManifest);
                bool rejected = false;
                try { ProfessionNativeSkeletonData.LoadBattleMonster(bytes, id, assets.SourceManifest + "-wrong"); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new InvalidDataException("The skill actor accepted a different source identity.");
                var state = new ProfessionNativeAppearanceSkeleton(source);
                state.SetSkin(source.CreateCombinedNpcSkin(((List<object>)row["skins"]).Cast<string>().ToArray()));
                using var batches = new ProfessionNativeMeshBatches(source.SlotCount);
                foreach (string name in source.AnimationNames)
                {
                    var result = new Motion { entityId = id, name = name }; motions.Add(result);
                    var animation = new ProfessionNativeAnimationChannels(source, name, originalBezierSegments: true);
                    var physics = source.Constraints.Any(value => value.Kind == "physics")
                        ? new ProfessionNativeOrderedPhysicsPose(source) : null;
                    for (int sample = 0; sample <= 8; sample++)
                    {
                        float time = animation.Duration * sample / 8;
                        if (physics != null) physics.Apply(state, animation, time, false, animation.Duration / 8);
                        else ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(state, animation, time, false);
                        batches.Build(state.Pose, state.CreateDrawSlots(), true, true, allowConcaveClipping: true);
                        foreach (var batch in batches.Batches)
                        {
                            var vertices = batch.Mesh.vertices;
                            if (vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z)))
                                throw new InvalidDataException("Original skill actor produced a nonfinite animated vertex.");
                            results.vertices += vertices.Length;
                        }
                        result.samples++;
                    }
                }
                if (previous != null)
                {
                    string shared = previous.AnimationNames.Intersect(source.AnimationNames).First();
                    var other = new ProfessionNativeAnimationChannels(source, shared, originalBezierSegments: true);
                    rejected = false;
                    try { ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(
                        new ProfessionNativeAppearanceSkeleton(previous), other, 0, false); }
                    catch (ArgumentException) { rejected = true; }
                    if (!rejected) throw new InvalidDataException("The constraint solver accepted another actor's animation.");
                }
                previous = source;
            }
            results.motions = motions.ToArray();
            File.WriteAllText(Path.Combine(work, "skill-actor-pose-results.json"), JsonUtility.ToJson(results, true));
            Debug.Log("[Original skill actor poses] models=" + results.models + " motions=" + motions.Count
                + " samples=" + motions.Sum(value => value.samples) + " vertices=" + results.vertices);
        }
    }
}
