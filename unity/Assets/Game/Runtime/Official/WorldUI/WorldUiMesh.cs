using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // launch-source/UIMesh.cs. Configure restores the original serialized references.
    [RequireComponent(typeof(CanvasRenderer), typeof(MeshRenderer))]
    public sealed class WorldUiMesh : MaskableGraphic
    {
        [SerializeField, HideInInspector] private MeshRenderer mMeshRenderer;
        public Mesh Mesh;
        private MaterialPropertyBlock mMPB;

        public void Configure(Mesh mesh, MeshRenderer renderer)
        { Mesh = mesh; mMeshRenderer = renderer; }

        public override Material materialForRendering => Application.isPlaying
            ? mMeshRenderer.material : mMeshRenderer.sharedMaterial;

        private bool IsValid() { return Mesh != null; }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyMaterialPropertyBlock();
        }

        public override void Rebuild(CanvasUpdate update)
        {
            if (IsValid() && update == CanvasUpdate.PreRender)
            {
                canvasRenderer.SetMesh(Mesh);
                UpdateMaterial();
            }
        }

        protected override void UpdateMaterial()
        {
            if (mMeshRenderer != null && mMeshRenderer.sharedMaterial != null && canvasRenderer != null)
            {
                canvasRenderer.materialCount = 1;
                canvasRenderer.SetMaterial(materialForRendering, 0);
                canvasRenderer.SetTexture(null);
            }
        }

        private void ApplyMaterialPropertyBlock()
        {
            if (mMPB == null) mMPB = new MaterialPropertyBlock();
            mMPB.Clear();
            mMeshRenderer.GetPropertyBlock(mMPB);
            WorldUiMeshUtils.ApplyMaterialPropertyBlock(materialForRendering, mMPB);
        }

        private void Update() { ApplyMaterialPropertyBlock(); }
    }
}
