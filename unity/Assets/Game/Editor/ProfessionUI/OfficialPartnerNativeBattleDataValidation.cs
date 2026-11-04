using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class OfficialPartnerNativeBattleDataValidation
    {
        private const string Root = "Assets/Official/Combat/NativePartners/";
        [Serializable] private sealed class Catalog { public string manifest; public Fighter[] fighters; }
        [Serializable] private sealed class Fighter
        {
            public int npcId;
            public string package;
            public string[] skins, animationNames;
        }
        [Serializable] private sealed class Probe
        {
            public int npcId;
            public string animation, error;
            public bool supported;
        }
        [Serializable] private sealed class Probes { public Probe[] animations; }

        public static void Validate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Catalog catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Root + "catalog.json"));
            if (catalog?.manifest != "884_156692" || catalog.fighters?.Length != 34)
                throw new InvalidDataException("Full-binary partner battle source identity differs.");
            var results = new List<Probe>();
            foreach (Fighter fighter in catalog.fighters)
            {
                byte[] bytes = File.ReadAllBytes(Root + fighter.package);
                ProfessionNativeSkeletonData data = ProfessionNativeSkeletonData.LoadPartnerBattle(bytes, fighter.npcId);
                if (!data.AnimationNames.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                        fighter.animationNames.OrderBy(value => value, StringComparer.Ordinal)))
                    throw new InvalidDataException("Native partner loader changed original animation names: " + fighter.npcId);
                bool rejected = false;
                try { ProfessionNativeSkeletonData.LoadPartnerBattle(bytes, fighter.npcId + 100000); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new InvalidDataException("Native partner loader accepted another NPC identity.");
                var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                skeleton.SetSkin(data.CreateCombinedNpcSkin(fighter.skins));
                using var batches = new ProfessionNativeMeshBatches(data.SlotCount);
                foreach (string name in data.AnimationNames)
                {
                    var result = new Probe { npcId = fighter.npcId, animation = name };
                    try
                    {
                        var channels = new ProfessionNativeAnimationChannels(data, name);
                        foreach (float time in new[] { 0f, channels.Duration * .5f, channels.Duration })
                        {
                            ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, true);
                            foreach (var batch in batches.Batches)
                                foreach (Vector3 vertex in batch.Mesh.vertices)
                                    if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x)
                                        || float.IsNaN(vertex.y) || float.IsInfinity(vertex.y))
                                        throw new InvalidDataException("Native battle animation produced a non-finite vertex.");
                        }
                        result.supported = true;
                    }
                    catch (NotSupportedException error) { result.error = error.Message; }
                    catch (Exception error)
                    {
                        throw new InvalidDataException("Original partner battle probe failed: "
                            + fighter.npcId + "/" + name, error);
                    }
                    results.Add(result);
                }
            }
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output))
                throw new InvalidDataException("Native battle capability probe requires an isolated output directory.");
            File.WriteAllText(Path.Combine(output, "native-partner-animation-probes.json"),
                JsonUtility.ToJson(new Probes { animations = results.ToArray() }, true));
            Debug.Log("Original full-binary partner data loaded: 34 fighters, " + results.Count
                + " original animations; native constraint/mesh probes supported=" + results.Count(value => value.supported)
                + ", unsupported=" + results.Count(value => !value.supported)
                + ". Materials, Animator playback and battle rendering are not validated by this data check.");
        }
    }
}
