using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Original application component: AppearanceSystem.SpineExt.
    // Its mesh-bake calls use the independently implemented native attachment API.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class SkeletonRendererAttachmentBacker : MonoBehaviour
    {
        public ProfessionNativeSceneRenderer SkeletonRenderer;
        public MeshRenderer MeshRenderer;
        public MeshFilter MeshFilter;
        public string SlotName, AttachmentName;
        private object attachment;
        private IProfessionSkinClipSlot slot;
        private Mesh ownedMesh;
        public object Attachment => attachment;
        public IProfessionSkinClipSlot Slot => slot;

        private void OnEnable()
        {
            if (!SkeletonRenderer) SkeletonRenderer = GetComponentInParent<ProfessionNativeSceneRenderer>();
            if (!SkeletonRenderer) return;
            SkeletonRenderer.OnMeshAndMaterialsUpdated += OnMeshAndMaterialsUpdated;
            if (!MeshFilter.sharedMesh)
            {
                ownedMesh = new Mesh { name = "Skeleton mesh" };
                MeshFilter.sharedMesh = ownedMesh;
            }
        }

        private void OnDisable()
        {
            if (SkeletonRenderer) SkeletonRenderer.OnMeshAndMaterialsUpdated -= OnMeshAndMaterialsUpdated;
        }

        private void OnMeshAndMaterialsUpdated(ProfessionNativeSceneRenderer renderer)
        {
            var skeleton = renderer.State;
            if (slot == null) slot = skeleton.FindSlot(SlotName);
            if (slot == null || !((IProfessionFollowerSlot)slot).Bone.Active) return;
            int index = skeleton.Source.FindSlot(SlotName);
            if (attachment == null) attachment = skeleton.GetAttachment(index, AttachmentName);
            if (attachment != null && MeshFilter) Bake(slot, MeshFilter.sharedMesh);
        }

        // SpineMeshBakeUtils.BackMeshes bakes the slot's CURRENT attachment;
        // AttachmentName only gates it in the original caller above.
        public static void Bake(IProfessionSkinClipSlot source, Mesh mesh)
        {
            var native = (IProfessionNativeDeformSlot)source;
            mesh.Clear();
            if (!(source.Attachment is ProfessionNativeRenderAttachment render)) return;
            var vertices = new Vector3[render.VertexCount];
            var uvs = new Vector2[render.VertexCount];
            var indices = new int[render.IndexCount];
            render.ComputeVertices(native.Pose, vertices, native.Deform);
            for (int i = 0; i < uvs.Length; i++) uvs[i] = render.Uv(i);
            for (int i = 0; i < indices.Length; i++) indices[i] = render.Index(i);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0, true);
        }

        private void OnDestroy()
        {
            if (!ownedMesh) return;
            if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
            ownedMesh = null;
        }
    }
}
