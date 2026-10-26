using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13
{
    // Independent weighted-point deformation driven by native Unity bone curves.
    // Original map instances retain their own Transform, Animator and Renderer.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(Animator))]
    public sealed class NativeMap13FlagMesh : MonoBehaviour
    {
        public const string BoneRootName = "NativeMap13FlagBones";
        public NativeMap13FlagData skeletonDataAsset;
        private Transform[] bones;
        private Matrix4x4[] matrices;
        private Vector3[] positions;
        private Mesh mesh;
        private Animator animator;
        private MeshRenderer meshRenderer;

        public Transform[] Bones => bones;
        public Mesh DeformedMesh => mesh;

        private void Awake()
        {
            var data = skeletonDataAsset;
            if (!data || data.Bones == null || data.Bones.Length == 0 || data.Vertices.Length != data.UV.Length)
                throw new InvalidOperationException("Original Map13 flag native geometry is not bound.");
            var root = new GameObject(BoneRootName).transform;
            root.SetParent(transform, false);
            bones = new Transform[data.Bones.Length]; matrices = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var source = data.Bones[i];
                if (source.Parent >= i) throw new InvalidOperationException("Original flag parent order differs.");
                var bone = new GameObject(source.Name).transform;
                bone.SetParent(source.Parent < 0 ? root : bones[source.Parent], false);
                bone.localPosition = source.Position * data.Scale;
                bone.localRotation = Quaternion.Euler(0, 0, source.Rotation);
                bones[i] = bone;
            }
            positions = new Vector3[data.Vertices.Length];
            mesh = new Mesh { name = data.name + " native weighted mesh" };
            mesh.MarkDynamic(); mesh.vertices = positions; mesh.uv = data.UV; mesh.triangles = data.Triangles;
            var colors = new Color32[positions.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            // The source has one default skin/slot, white PMA colors, no normals,
            // tangents, deforms, constraints, events, masks or slot depth.
            animator = GetComponent<Animator>();
            if (animator.runtimeAnimatorController != data.Controller)
                throw new InvalidOperationException("Original flag native controller is not explicitly bound.");
            animator.Rebind();
            ApplyAnimationPose();
            ApplyBonePose();
        }

        // The pinned APK enum identifies timing 1 as InUpdate and invisible
        // mode 0 as Nothing. Native Animator still owns the original loop clock.
        private void Update() { if (meshRenderer.isVisible) ApplyAnimationPose(); }
        private void LateUpdate() { if (meshRenderer.isVisible) ApplyBonePose(); }

        private void ApplyAnimationPose()
        {
            var data = skeletonDataAsset;
            // Animator's compiled Euler clips resample rotation curves. Keep the
            // unchanged original loop controller as the clock and evaluate its
            // original weighted Bezier curves directly, before deforming points.
            float time = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime * data.Duration, data.Duration);
            foreach (var track in data.RotationTracks)
                bones[track.Bone].localRotation = Quaternion.Euler(0, 0, track.Curve.Evaluate(time));
        }

        public void ApplyBonePose()
        {
            if (!mesh) return;
            var data = skeletonDataAsset;
            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                var local = Matrix4x4.TRS(bone.localPosition, bone.localRotation, bone.localScale);
                matrices[i] = data.Bones[i].Parent < 0 ? local : matrices[data.Bones[i].Parent] * local;
            }
            for (int i = 0; i < positions.Length; i++)
            {
                var vertex = data.Vertices[i];
                Vector3 point = Vector3.zero;
                for (int j = 0; j < vertex.Count; j++)
                {
                    var influence = data.Influences[vertex.First + j];
                    point += matrices[influence.Bone].MultiplyPoint3x4(influence.Position * data.Scale) * influence.Weight;
                }
                positions[i] = point;
            }
            mesh.vertices = positions;
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }
    }
}
