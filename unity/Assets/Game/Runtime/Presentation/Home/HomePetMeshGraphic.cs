using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Home
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomePetMeshGraphic : MaskableGraphic
    {
        private static readonly int DyeTextureId = Shader.PropertyToID("_DyeTex");
        private static readonly int DyeHsv1Id = Shader.PropertyToID("_DyeHsv1");
        private static readonly int DyeHsv2Id = Shader.PropertyToID("_DyeHsv2");
        private static readonly int DyeHsv3Id = Shader.PropertyToID("_DyeHsv3");
        [SerializeField] private MeshRenderer sourceRenderer;
        private Material materialInstance;
        public Mesh Mesh;

        public override Material material
        {
            get => materialInstance != null ? materialInstance
                : sourceRenderer != null ? sourceRenderer.sharedMaterial : base.material;
            set => base.material = value;
        }

        public void Configure(Mesh mesh, MeshRenderer renderer)
        {
            Mesh = mesh;
            sourceRenderer = renderer;
            if (materialInstance != null) Destroy(materialInstance);
            materialInstance = sourceRenderer != null && sourceRenderer.sharedMaterial != null
                ? new Material(sourceRenderer.sharedMaterial) { name = sourceRenderer.sharedMaterial.name + " (Canvas)" }
                : null;
            ApplyDyeProperties();
            color = Color.white;
            raycastTarget = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public override void Rebuild(CanvasUpdate update)
        {
            if (Mesh == null || update != CanvasUpdate.PreRender) return;
            canvasRenderer.SetMesh(Mesh);
            UpdateMaterial();
        }

        protected override void UpdateMaterial()
        {
            Material material = materialForRendering;
            if (material == null || canvasRenderer == null) return;
            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(material, 0);
            canvasRenderer.SetTexture(null);
        }

        private void ApplyDyeProperties()
        {
            if (sourceRenderer == null || materialInstance == null) return;
            var properties = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(properties);
            if (materialInstance.HasProperty(DyeTextureId))
            {
                Texture texture = properties.GetTexture(DyeTextureId);
                if (texture != null) materialInstance.SetTexture(DyeTextureId, texture);
            }
            if (materialInstance.HasProperty(DyeHsv1Id)) materialInstance.SetVector(DyeHsv1Id, properties.GetVector(DyeHsv1Id));
            if (materialInstance.HasProperty(DyeHsv2Id)) materialInstance.SetVector(DyeHsv2Id, properties.GetVector(DyeHsv2Id));
            if (materialInstance.HasProperty(DyeHsv3Id)) materialInstance.SetVector(DyeHsv3Id, properties.GetVector(DyeHsv3Id));
        }

        protected override void OnDestroy()
        {
            if (materialInstance != null) Destroy(materialInstance);
            base.OnDestroy();
        }
    }
}
