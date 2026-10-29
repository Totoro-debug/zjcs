using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class Map12NpcDataValidation
    {
        [Serializable] private sealed class NpcDataCatalog
        { public string manifest, prefabSourceSha256; public NpcDataRow[] models; public NpcRendererRow[] renderers; }
        [Serializable] private sealed class NpcDataRow
        {
            public string name, resource, sha256, skeletonHash, atlasHash;
            public float scale;
            public int bones, slots, skins, animations, regions, attachments, constraints;
            public string[] startingAnimations;
            public NpcPathRow[] paths;
        }
        [Serializable] private sealed class NpcPathRow
        {
            public string skin, key;
            public int slot, vertices;
            public float[] lengths;
            public bool closed, constantSpeed;
        }
        [Serializable] private sealed class NpcRendererRow
        { public int npcClassId, skeletonIndex; public string startingAnimation; public string[] combineSkins; }
        private const string NpcDataActive = "Zjcs.Map12.NpcData.Active";

        [InitializeOnLoadMethod]
        private static void RegisterNpcDataVerification()
        {
            EditorApplication.playModeStateChanged += NpcDataModeChanged;
            EditorApplication.update += FinishNpcDataVerification;
        }

        private static void FinishNpcDataVerification()
        {
            if (!SessionState.GetBool(NpcDataActive, false) || !SessionState.GetBool(NpcDataActive + ".Done", false) ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.EraseBool(NpcDataActive);
            EditorApplication.Exit(SessionState.GetInt(NpcDataActive + ".Exit", 1));
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(NpcDataActive, true);
            SessionState.SetBool(NpcDataActive + ".Done", false);
            SessionState.SetInt(NpcDataActive + ".Exit", 1);
            EditorApplication.EnterPlaymode();
        }

        private static void NpcDataModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(NpcDataActive, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try { VerifyOriginalNpcData(); SessionState.SetInt(NpcDataActive + ".Exit", 0); }
                catch (Exception error) { Debug.LogException(error); }
                finally
                {
                    SessionState.SetBool(NpcDataActive + ".Done", true);
                    EditorApplication.isPlaying = false;
                }
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }

        private static void VerifyOriginalNpcData()
        {
            var asset = Resources.Load<TextAsset>("Map12/TeamNpcSkeletons/catalog");
            if (!asset) throw new InvalidDataException("Original native NPC data catalog is unbound.");
            var catalog = JsonUtility.FromJson<NpcDataCatalog>(asset.text);
            Require(catalog.manifest == "871_156598" && catalog.models.Length == 34 &&
                catalog.renderers.Length == 92 && catalog.prefabSourceSha256.Length == 64,
                "Original native NPC skeleton inventory differs.");
            TextAsset sourcePrefabs = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Official/World/Map/12/Source/team-npc-prefabs.json");
            Require(sourcePrefabs, "Original NPC prefab source is not imported.");
            string prefabHash;
            using (var digest = System.Security.Cryptography.SHA256.Create())
                prefabHash = BitConverter.ToString(digest.ComputeHash(sourcePrefabs.bytes)).Replace("-", "").ToLowerInvariant();
            Require(prefabHash == catalog.prefabSourceSha256, "Original NPC prefab source hash differs.");
            int attachments = 0, paths = 0, starts = 0;
            var loaded = new List<ProfessionNativeSkeletonData>(catalog.models.Length);
            foreach (var row in catalog.models)
            {
                var payload = Resources.Load<TextAsset>(row.resource);
                Require(payload, "Original native NPC data is unbound: " + row.name);
                string hash;
                using (var digest = System.Security.Cryptography.SHA256.Create())
                    hash = BitConverter.ToString(digest.ComputeHash(payload.bytes)).Replace("-", "").ToLowerInvariant();
                Require(hash == row.sha256, "Original native NPC package hash differs: " + row.name);
                var data = ProfessionNativeSkeletonData.LoadNpc(payload.bytes);
                loaded.Add(data);
                Require(data.Manifest == catalog.manifest && data.Sex == "npc:" + row.name &&
                    data.DataScale == row.scale && data.SkeletonSha256 == row.skeletonHash && data.AtlasSha256 == row.atlasHash,
                    "Original native NPC source bytes, identity or scale differ: " + row.name);
                Require(data.BoneCount == row.bones && data.SlotCount == row.slots && data.SkinCount == row.skins &&
                    data.AnimationCount == row.animations && data.AtlasRegionCount == row.regions &&
                    data.AttachmentCount == row.attachments && data.Constraints.Count == row.constraints,
                    "Original native NPC graph inventory differs: " + row.name);
                Require(data.CreateBonePose().Count == row.bones && data.CreateSlotPose(data.CreateBonePose()).Count == row.slots,
                    "Original native NPC bone/slot factories differ: " + row.name);
                foreach (string name in row.startingAnimations)
                { Require(data.GetAnimation(name) != null, "Original NPC initial animation is unresolved: " + name); starts++; }
                foreach (var path in row.paths)
                {
                    var skin = data.SkinCatalog.FindSkin(path.skin);
                    var value = skin.GetAttachment(path.slot, path.key) as ProfessionNativePathAttachment;
                    Require(value != null && value.VertexCount == path.vertices && value.Closed == path.closed &&
                        value.ConstantSpeed == path.constantSpeed && value.Lengths.SequenceEqual(path.lengths),
                        "Original NPC path control layout or curve lengths differ: " + row.name + "/" + path.key);
                    paths++;
                }
                attachments += data.AttachmentCount;
                VerifyCombinedSkins(data, row.name);
            }
            int rendererBindings = 0;
            foreach (var renderer in catalog.renderers)
            {
                Require(renderer.npcClassId > 0 && renderer.skeletonIndex >= 0 &&
                    renderer.skeletonIndex < loaded.Count && !string.IsNullOrEmpty(renderer.startingAnimation) &&
                    renderer.combineSkins != null,
                    "Original NPC renderer binding is incomplete: " + renderer.npcClassId);
                ProfessionNativeSkeletonData data = loaded[renderer.skeletonIndex];
                Require(data.AnimationNames.Contains(renderer.startingAnimation),
                    "Original NPC prefab animation is unresolved: " + renderer.npcClassId + "/" + renderer.startingAnimation);
                ProfessionNativeSkin combined = data.CreateCombinedNpcSkin(renderer.combineSkins);
                Require(combined.Name == "combineSkin",
                    "Original NPC prefab combined skin is missing: " + renderer.npcClassId);
                rendererBindings++;
            }
            // Keep the existing format's real resource decoder in this regression.
            foreach (string sex in new[] { "male", "female" })
                Require(ProfessionNativeSkeletonData.LoadResource(sex).Sex == sex, "Original player data compatibility differs.");
            Map12NpcAnimationValidation.Verify();
            Debug.Log("Map12 native NPC data passed: 34 skeletons, " + attachments + " attachments, " + paths +
                " paths, " + starts + " initial animation references, " + rendererBindings +
                " original renderer-to-animation/skin bindings; ordered prefab skin composition passed. Existing male/female packages loaded. Rendering/constraint evaluation remains separate.");
        }

        private static void VerifyCombinedSkins(ProfessionNativeSkeletonData data, string model)
        {
            var expected = new Dictionary<(int slot, string key), object>();
            var expectedBones = new HashSet<int>();
            var expectedConstraints = new HashSet<int>();
            foreach (string name in data.NpcSkinNames)
            {
                ProfessionNativeSkin source = data.SkinCatalog.FindSkin(name) as ProfessionNativeSkin;
                Require(source != null, "Original NPC skin name is unresolved: " + model + "/" + name);
                foreach (ProfessionNativeSkin.Entry entry in source.Entries)
                    expected[(entry.Slot, entry.Key)] = entry.Attachment;
                foreach (int bone in source.Bones) expectedBones.Add(bone);
                foreach (int constraint in source.Constraints) expectedConstraints.Add(constraint);
            }

            ProfessionNativeSkin combined = data.CreateCombinedNpcSkin(data.NpcSkinNames);
            Require(combined.Name == "combineSkin" && combined.AttachmentCount == expected.Count,
                "Original NPC combined skin attachment count differs: " + model);
            foreach (var entry in expected)
                Require(ReferenceEquals(combined.GetAttachment(entry.Key.slot, entry.Key.key), entry.Value),
                    "Original NPC combined skin precedence differs: " + model + "/" + entry.Key.key);
            Require(combined.Bones.Count == expectedBones.Count && combined.Constraints.Count == expectedConstraints.Count,
                "Original NPC combined skin membership differs: " + model);

            ProfessionNativeSkin missingOnly = data.CreateCombinedNpcSkin(new[] { "__missing_original_skin__" });
            Require(missingOnly.AttachmentCount == 0 && missingOnly.Bones.Count == 0 && missingOnly.Constraints.Count == 0,
                "Original combine-skin lookup must ignore missing skins: " + model);
        }
    }
}
