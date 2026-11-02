using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Reassemble the exported four-node application prefab with explicit native
    // script bindings. Every transform and follower field comes from its source.
    public sealed class ProfessionNativeSceneWeapon : IDisposable
    {
        [Serializable] private sealed class Hierarchy { public Node[] nodes; }
        [Serializable] private sealed class Node { public string parent, name; public Go game_object; public Script[] components; }
        [Serializable] private sealed class Go { public Id source; public GoFields fields; }
        [Serializable] private sealed class Id { public string path_id; }
        [Serializable] private sealed class GoFields { public int m_Layer; public bool m_IsActive; }
        [Serializable] private sealed class Script { public string type; public Fields runtime_fields; }
        [Serializable] private sealed class Pointer { public long m_PathID; }
        [Serializable] private sealed class Fields
        {
            public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation;
            public string BoneName, SlotName, AttachmentName;
            public bool FollowZSpacing, ZFront, FollowLocalScale, FollowBoneWorldFlip, FollowRotation,
                FollowSkeletonTransformFlipX, InitializeOnAwake;
            public int LevelOfResolution, SkinType; public Pointer[] Gos, RenderExistingMeshes;
        }
        public GameObject GameObject { get; private set; }
        public AppearanceSkeletonPart Part { get; private set; }
        public ProfessionNativeSceneWeaponRenderer Renderer { get; private set; }
        public SkeletonRendererFollower Follower => Part.mSkeletonFollower as SkeletonRendererFollower;
        private IDisposable placement;
        private ProfessionNativeSceneRenderer target;

        public static ProfessionNativeSceneWeapon Create(ProfessionNativeSceneWeaponCatalog catalog, string skin,
            Transform parent, ProfessionNativeSceneRenderer target, IProfessionAppearanceUILiteBindings materials, IProfessionAppearancePartBindings parts,
            IProfessionNativeSceneWeaponPoseProducer poseProducer)
        {
            if (!catalog || !parent || !target || materials == null || parts == null || poseProducer == null) throw new ArgumentNullException();
            var entry = catalog.Find(skin);
            var source = JsonUtility.FromJson<Hierarchy>(entry.Hierarchy.text);
            if (source.nodes.Length != 4 || source.nodes[0].name != skin + "_Integrated"
                || source.nodes[1].name != skin || source.nodes[2].name != "Spine" || source.nodes[3].name != "Effect")
                throw new NotSupportedException("Bind the complete selected original independent weapon hierarchy.");
            var supported = new HashSet<string> { "Transform", "AppearanceSkeletonPart", "SkeletonRendererFollower",
                "MeshFilter", "MeshRenderer", "Spine.Unity.SkeletonAnimation", "AppearanceSystem.AppearanceRendererLite" };
            if (source.nodes.SelectMany(n => n.components).Any(c => !supported.Contains(c.type)))
                throw new NotSupportedException("Bind additional original independent weapon scripts before assembly.");
            var result = new ProfessionNativeSceneWeapon();
            var nodes = new Dictionary<string, GameObject>();
            try
            {
                foreach (var node in source.nodes)
                {
                    var go = new GameObject(node.name); go.SetActive(false); go.layer = node.game_object.fields.m_Layer;
                    if (!result.GameObject) result.GameObject = go;
                    go.transform.SetParent(string.IsNullOrEmpty(node.parent) ? parent : nodes[node.parent].transform, false);
                    var transform = node.components.Single(c => c.type == "Transform").runtime_fields;
                    go.transform.localPosition = transform.m_LocalPosition;
                    go.transform.localRotation = transform.m_LocalRotation;
                    go.transform.localScale = transform.m_LocalScale;
                    nodes.Add(node.game_object.source.path_id, go);
                }
                var spineNode = source.nodes[2]; var spine = nodes[spineNode.game_object.source.path_id];
                result.Renderer = spine.AddComponent<ProfessionNativeSceneWeaponRenderer>();
                result.Renderer.Configure(catalog, skin, poseProducer); result.Renderer.Initialize(false, false);
                var liteFields = spineNode.components.Single(c => c.type == "AppearanceSystem.AppearanceRendererLite").runtime_fields;
                if (liteFields.RenderExistingMeshes.Length != 0) throw new NotSupportedException("Bind original independent weapon existing-mesh references.");
                var lite = spine.AddComponent<AppearanceRendererLite>(); lite.SkeletonRenderer = result.Renderer;
                lite.LevelOfResolution = (LevelOfResolution)liteFields.LevelOfResolution;
                lite.RenderExistingMeshes = new List<Component>(); lite.Configure(materials);
                foreach (var node in source.nodes)
                    foreach (var script in node.components.Where(c => c.type == "SkeletonRendererFollower"))
                    {
                        var go = nodes[node.game_object.source.path_id]; var f = script.runtime_fields;
                        var follower = go.AddComponent<SkeletonRendererFollower>();
                        follower.BoneName = f.BoneName; follower.SlotName = f.SlotName; follower.AttachmentName = f.AttachmentName;
                        follower.FollowZSpacing = f.FollowZSpacing; follower.ZFront = f.ZFront;
                        follower.FollowLocalScale = f.FollowLocalScale; follower.FollowBoneWorldFlip = f.FollowBoneWorldFlip;
                        follower.FollowRotation = f.FollowRotation; follower.FollowSkeletonTransformFlipX = f.FollowSkeletonTransformFlipX;
                        follower.InitializeOnAwake = f.InitializeOnAwake;
                        follower.Gos = f.Gos.Select(p => nodes[p.m_PathID.ToString()]).ToArray();
                        if (node.name == "Effect") follower.SkeletonRenderer = result.Renderer;
                        else follower.SkeletonRenderer = target;
                    }
                result.Part = result.GameObject.AddComponent<AppearanceSkeletonPart>();
                result.Part.Skeleton = spine; result.Part.mSkeletonFollower = result.GameObject.GetComponent<SkeletonRendererFollower>();
                result.Part.SkinType = (SkinType)source.nodes[0].components.Single(c => c.type == "AppearanceSkeletonPart").runtime_fields.SkinType;
                if (result.Part.SkinType != SkinType.pWeapon) throw new NotSupportedException("Bind the original independent non-weapon part type.");
                result.Part.ConfigurePart(parts);
                result.PrepareTarget(target);
                foreach (var node in source.nodes.Reverse()) nodes[node.game_object.source.path_id].SetActive(node.game_object.fields.m_IsActive);
                result.Renderer.UpdateAnimationAndMesh();
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        internal void PrepareTarget(ProfessionNativeSceneRenderer next)
        {
            if (target == next && placement != null) return;
            Detach(); target = next ? next : throw new ArgumentNullException(nameof(next));
            foreach (var follower in GameObject.GetComponentsInChildren<SkeletonRendererFollower>(true))
            {
                follower.SkeletonRenderer = follower.gameObject == GameObject ? (Component)target : Renderer;
                follower.Initialize();
            }
            placement = target.BindScenePart(Part);
        }

        internal void Detach() { placement?.Dispose(); placement = null; target = null; }
        public void Dispose() => Release(true);
        internal void Release(bool tryUnloadAllAssets)
        {
            if (!GameObject) return;
            GameObject.SetActive(false);
            Detach();
            if (tryUnloadAllAssets)
                foreach (var lite in GameObject.GetComponentsInChildren<AppearanceRendererLite>(true)) lite.MarkUnloadTextureImmediately();
            foreach (var follower in GameObject.GetComponentsInChildren<BaseSkeletonFollower>(true)) follower.Clear();
            if (Application.isPlaying) Object.Destroy(GameObject); else Object.DestroyImmediate(GameObject);
            GameObject = null; Part = null; Renderer = null;
        }
    }
}
