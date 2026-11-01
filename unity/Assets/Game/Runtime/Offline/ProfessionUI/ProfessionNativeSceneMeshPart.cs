using UnityEngine;
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
        private void OnDestroy()
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
