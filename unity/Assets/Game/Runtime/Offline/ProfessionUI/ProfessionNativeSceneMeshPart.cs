using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;

namespace Zjcs.Offline.ProfessionUI
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProfessionNativeSceneMeshPart : MonoBehaviour, IProfessionSceneMeshPart
    {
        private Mesh mesh;
        public GameObject GameObject => gameObject;
        public MeshRenderer MeshRenderer => GetComponent<MeshRenderer>();
        public Mesh Mesh
        {
            get
            {
                if (!mesh)
                {
                    mesh = new Mesh { name = "Native scene separator part" };
                    mesh.MarkDynamic();
                    GetComponent<MeshFilter>().sharedMesh = mesh;
                }
                return mesh;
            }
        }
        public void Clear() { if (mesh) mesh.Clear(); }
        public void Assign(IReadOnlyList<ProfessionNativeMeshBatches.Batch> batches,
            Func<ProfessionNativeMeshBatches.Batch, Material> material, Action<Material[]> updateMaterials = null)
        {
            if (batches == null || material == null) throw new ArgumentNullException();
            var combines = new CombineInstance[batches.Count];
            var materials = new Material[batches.Count];
            int vertices = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch == null || !batch.Mesh) throw new ArgumentException("Completed native geometry is unavailable.");
                combines[i] = new CombineInstance { mesh = batch.Mesh, subMeshIndex = 0 };
                materials[i] = material(batch);
                if (!materials[i]) throw new InvalidOperationException("Original scene atlas page material is unbound.");
                vertices = checked(vertices + batch.Mesh.vertexCount);
            }
            var output = Mesh; output.Clear();
            output.indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            if (combines.Length > 0) output.CombineMeshes(combines, false, false);
            if (materials.Length > 0) updateMaterials?.Invoke(materials);
            MeshRenderer.sharedMaterials = materials;
        }
        private void OnDestroy()
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
