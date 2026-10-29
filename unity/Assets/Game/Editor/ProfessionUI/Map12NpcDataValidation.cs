using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    public static class Map12NpcDataValidation
    {
        [Serializable] private sealed class NpcDataCatalog { public string manifest; public NpcDataRow[] models; }
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
            Require(catalog.manifest == "871_156598" && catalog.models.Length == 34,
                "Original native NPC skeleton inventory differs.");
            int attachments = 0, paths = 0, starts = 0;
            foreach (var row in catalog.models)
            {
                var payload = Resources.Load<TextAsset>(row.resource);
                Require(payload, "Original native NPC data is unbound: " + row.name);
                string hash;
                using (var digest = System.Security.Cryptography.SHA256.Create())
                    hash = BitConverter.ToString(digest.ComputeHash(payload.bytes)).Replace("-", "").ToLowerInvariant();
                Require(hash == row.sha256, "Original native NPC package hash differs: " + row.name);
                var data = ProfessionNativeSkeletonData.LoadNpc(payload.bytes);
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
            }
            // Keep the existing format's real resource decoder in this regression.
            foreach (string sex in new[] { "male", "female" })
                Require(ProfessionNativeSkeletonData.LoadResource(sex).Sex == sex, "Original player data compatibility differs.");
            Map12NpcAnimationValidation.Verify();
            Debug.Log("Map12 native NPC data passed: 34 skeletons, " + attachments + " attachments, " + paths +
                " paths, " + starts + " distinct initial animations; existing male/female packages loaded. Rendering/constraint evaluation remains separate.");
        }
    }
}
