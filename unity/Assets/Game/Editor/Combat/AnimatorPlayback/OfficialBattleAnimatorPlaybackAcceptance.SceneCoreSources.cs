using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        [Serializable] private sealed class SceneCoreReceipt
        {
            public string manifest;
            public SceneCoreIdentity hierarchy;
            public SceneCoreProfile[] profiles;
        }
        [Serializable] private sealed class SceneCoreIdentity { public string sha256; }
        [Serializable] private sealed class SceneCoreProfile
        {
            public string sex, skeleton_sha256, atlas_sha256;
            public SceneCoreCounts counts;
        }
        [Serializable] private sealed class SceneCoreCounts
        {
            public int bones, slots, skins, animations, ik, transform, path, physics, regions, attachments;
        }
        [Serializable] private sealed class SceneCoreHierarchy { public SceneCoreNode[] nodes; }
        [Serializable] private sealed class SceneCoreNode { public string name; public SceneCoreComponent[] components; }
        [Serializable] private sealed class SceneCoreComponent { public string type; }

        public static void RunSceneCoreSources()
        {
            Run();
            SessionState.SetBool(Key + ".sceneCoreSources", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneCoreSourceSteps()
        {
            string root = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            Require(!string.IsNullOrEmpty(root), "explicit source repository root");
            var receipt = JsonUtility.FromJson<SceneCoreReceipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/battle-animation/scene-core-source-manifest.json")));
            Require(receipt.manifest == ProfessionNativeSceneCharacterAsset.SourceManifest && receipt.profiles.Length == 2,
                "current original scene source receipt");
            int animations = 0, samples = 0;
            foreach (var profile in receipt.profiles)
            {
                var asset = ProfessionNativeSceneCharacterAsset.LoadResource(profile.sex);
                var data = asset.Data;
                Require(ReferenceEquals(data, asset.Data), "bound source data cache: " + profile.sex);
                var count = profile.counts;
                Require(data.Manifest == receipt.manifest && data.Sex == profile.sex
                    && data.SkeletonSha256 == profile.skeleton_sha256 && data.AtlasSha256 == profile.atlas_sha256,
                    "current source identity: " + profile.sex);
                Require(data.BoneCount == count.bones && data.SlotCount == count.slots && data.SkinCount == count.skins
                    && data.AnimationCount == count.animations && data.AtlasRegionCount == count.regions
                    && data.AttachmentCount == count.attachments, "complete current source graph: " + profile.sex);
                string[] kinds = { "ik", "transform", "path", "physics" };
                int[] counts = { count.ik, count.transform, count.path, count.physics };
                for (int i = 0; i < kinds.Length; i++)
                    Require(data.Constraints.Count(row => row.Kind == kinds[i]) == counts[i], "current source constraints: " + kinds[i]);
                using (var sha = SHA256.Create())
                    Require(BitConverter.ToString(sha.ComputeHash(asset.Hierarchy.bytes)).Replace("-", "").ToLowerInvariant()
                        == receipt.hierarchy.sha256, "original scene hierarchy binding");
                var hierarchy = JsonUtility.FromJson<SceneCoreHierarchy>(asset.Hierarchy.text);
                Require(hierarchy.nodes.Length == 3 && hierarchy.nodes[0].name == "PlayerModel_Game"
                    && hierarchy.nodes.Any(node => node.name == "PartSlot")
                    && hierarchy.nodes.Any(node => node.name == "Dimianzhezhao"), "complete original scene hierarchy");
                Require(hierarchy.nodes[0].components.Any(component => component.type == "Spine.Unity.SkeletonMecanim")
                    && hierarchy.nodes[0].components.Any(component => component.type == "AppearanceSystem.Runtime.AppearanceMecanim"),
                    "original scene component identities");
                var currentBytes = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Combat/SceneCharacterSources/" + profile.sex + ".bytes");
                var historicalBytes = Resources.Load<TextAsset>("ProfessionUI/NativeSkeletons/" + profile.sex);
                Require(currentBytes && historicalBytes, "current and historical source payloads");
                bool rejected = false;
                try { ProfessionNativeSkeletonData.Load(currentBytes.bytes); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "historical loader rejects current source identity");
                rejected = false;
                try { ProfessionNativeSkeletonData.Load(historicalBytes.bytes, receipt.manifest); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "current loader rejects historical source identity");
                var bones = data.CreateBonePose();
                var slots = data.CreateSlotPose(bones);
                var colors = new Color[data.SlotCount];
                foreach (string name in data.AnimationNames)
                {
                    var clip = new ProfessionNativeAnimationChannels(data, name);
                    _ = clip.Constraints; _ = clip.Deforms; _ = clip.Physics;
                    foreach (float fraction in new[] { 0f, .5f, 1f })
                    {
                        clip.ApplyLocalPose(bones, slots, colors, clip.Duration * fraction, false);
                        for (int i = 0; i < bones.Count; i++)
                        {
                            Vector2 position = bones.LocalPosition(i), scale = bones.LocalScale(i), shear = bones.Shear(i);
                            Require(SceneCoreFinite(position.x) && SceneCoreFinite(position.y) && SceneCoreFinite(scale.x)
                                && SceneCoreFinite(scale.y) && SceneCoreFinite(shear.x) && SceneCoreFinite(shear.y)
                                && SceneCoreFinite(bones.LocalRotation(i)), "finite current local pose: " + name);
                        }
                        var order = new bool[data.SlotCount];
                        for (int i = 0; i < slots.Count; i++)
                        {
                            int slot = slots.DrawSlot(i);
                            Require(slot >= 0 && slot < order.Length && !order[slot], "current original draw-order permutation: " + name);
                            order[slot] = true;
                            for (int axis = 0; axis < 4; axis++)
                                Require(SceneCoreFinite(colors[i][axis]), "finite original slot color: " + name);
                        }
                        samples++;
                    }
                    animations++;
                    if (animations % 32 == 0) yield return null;
                }
                Debug.Log("[Current scene sources] " + profile.sex + ": " + count.bones + " bones, " + count.skins
                    + " skins, " + count.animations + " complete animations and version separation passed.");
                yield return null;
            }
            Require(animations == 903 && samples == 2709, "complete current character animation coverage");
            Debug.Log("[Current scene sources] " + animations + " animations, " + samples + " local-pose samples; no production rendering claim.");
        }

        private static bool SceneCoreFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
