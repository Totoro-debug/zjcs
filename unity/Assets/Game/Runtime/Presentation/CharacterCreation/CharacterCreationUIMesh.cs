using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.CharacterCreation
{
    [RequireComponent(typeof(CanvasRenderer), typeof(MeshRenderer))]
    public sealed class CharacterCreationUIMesh : MaskableGraphic
    {
        [SerializeField, HideInInspector] private MeshRenderer mMeshRenderer;
        public Mesh Mesh;
        private MaterialPropertyBlock mMPB;

        public override Material materialForRendering
        {
            get
            {
                if (Application.isPlaying) return mMeshRenderer != null ? mMeshRenderer.material : null;
                return mMeshRenderer != null ? mMeshRenderer.sharedMaterial : null;
            }
        }

        public void Configure(Mesh mesh, MeshRenderer renderer, Material material, Color tint)
        {
            Mesh = mesh;
            mMeshRenderer = renderer;
            color = tint;
            if (mMeshRenderer != null && material != null) mMeshRenderer.sharedMaterial = material;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public override void Rebuild(CanvasUpdate update)
        {
            if (Mesh != null && update == CanvasUpdate.PreRender)
            {
                canvasRenderer.SetMesh(Mesh);
                UpdateMaterial();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyMaterialPropertyBlock();
        }

        protected override void UpdateMaterial()
        {
            if (mMeshRenderer == null || mMeshRenderer.sharedMaterial == null || canvasRenderer == null) return;
            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(materialForRendering, 0);
            canvasRenderer.SetTexture(null);
        }

        private void Update()
        {
            ApplyMaterialPropertyBlock();
        }

        private void ApplyMaterialPropertyBlock()
        {
            if (mMeshRenderer == null || materialForRendering == null) return;
            if (mMPB == null) mMPB = new MaterialPropertyBlock();
            mMPB.Clear();
            mMeshRenderer.GetPropertyBlock(mMPB);
            CharacterCreationUIMeshUtils.ApplyMaterialPropertyBlock(materialForRendering, mMPB);
        }
    }
}
