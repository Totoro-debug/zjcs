using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Opening
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OpeningUiMeshGraphic : MaskableGraphic
    {
        private Mesh sourceMesh;
        private Material animatedMaterial;

        public static string ShaderName(string pathId)
        {
            switch (pathId)
            {
                case "-1287703601585291557": return "Zjcs/Opening/UIEffectDistort";
                case "7678014756277099682": return "Zjcs/Opening/UIEffectSequeue";
                case "-9135115673700106106": return "Zjcs/Opening/UIEffectDissolve";
                case "-1464726073327024352": return "Zjcs/Opening/UIParticle";
                default: throw new System.InvalidOperationException(
                    "Unknown official UI shader path ID: " + pathId);
            }
        }

        public override Texture mainTexture => material != null && material.mainTexture != null
            ? material.mainTexture : s_WhiteTexture;

        public void Configure(Mesh mesh, Material sourceMaterial, Color tint)
        {
            sourceMesh = mesh;
            material = sourceMaterial;
            color = tint;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void SetAnimatedProperty(string property, float value)
        {
            if (material == null || string.IsNullOrEmpty(property)) return;
            if (animatedMaterial == null)
            {
                animatedMaterial = new Material(material);
                material = animatedMaterial;
            }

            int separator = property.LastIndexOf('.');
            string component = separator >= 0 ? property.Substring(separator + 1) : string.Empty;
            string name = separator >= 0 ? property.Substring(0, separator) : property;
            if (!animatedMaterial.HasProperty(name)) return;
            if (component.Length == 1 && "rgbaxyzw".IndexOf(component[0]) >= 0)
            {
                Vector4 vector = animatedMaterial.GetVector(name);
                int index = "rgba".IndexOf(component[0]);
                if (index < 0) index = "xyzw".IndexOf(component[0]);
                vector[index] = value;
                animatedMaterial.SetVector(name, vector);
            }
            else animatedMaterial.SetFloat(name, value);
            SetMaterialDirty();
        }

        protected override void OnDestroy()
        {
            if (animatedMaterial != null) Destroy(animatedMaterial);
            base.OnDestroy();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (sourceMesh == null) return;

            Vector3[] vertices = sourceMesh.vertices;
            Vector2[] uvs = sourceMesh.uv;
            Color[] colors = sourceMesh.colors;
            int[] triangles = sourceMesh.triangles;
            UIVertex vertex = UIVertex.simpleVert;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 position = vertices[index];
                vertex.position = new Vector3(position.x, position.y, position.z);
                vertex.color = colors.Length > index ? (Color32)(colors[index] * color) : color;
                if (uvs.Length > index) vertex.uv0 = uvs[index];
                vertexHelper.AddVert(vertex);
            }
            for (int index = 0; index + 2 < triangles.Length; index += 3)
                vertexHelper.AddTriangle(triangles[index], triangles[index + 1], triangles[index + 2]);
        }
    }
}
