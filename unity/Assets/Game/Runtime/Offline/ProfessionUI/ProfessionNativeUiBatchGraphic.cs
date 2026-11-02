using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Offline.ProfessionUI
{
    // Each ordered atlas/blend batch needs its own mask-aware CanvasRenderer.
    // Geometry comes only from the independent native mesh generator.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProfessionNativeUiBatchGraphic : MaskableGraphic
    {
        private Mesh mesh;
        private Texture page;
        private float scale;
        private Vector2 offset;
        public override Texture mainTexture => page ? page : s_WhiteTexture;
        internal void Assign(Mesh value, Material sourceMaterial, Texture texture, float meshScale, Vector2 meshOffset, Color tint)
        {
            mesh = value; page = texture; scale = meshScale; offset = meshOffset;
            material = sourceMaterial; color = tint; raycastTarget = false;
            SetAllDirty();
        }
        protected override void OnPopulateMesh(VertexHelper target)
        {
            target.Clear();
            if (!mesh) return;
            var points = mesh.vertices; var uvs = mesh.uv; var colors = mesh.colors32;
            Color32 tint = color;
            for (int i = 0; i < points.Length; i++)
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = points[i] * scale + (Vector3)offset;
                vertex.uv0 = uvs[i];
                Color32 value = colors[i];
                vertex.color = new Color32((byte)((value.r * tint.r + 127) / 255),
                    (byte)((value.g * tint.g + 127) / 255), (byte)((value.b * tint.b + 127) / 255),
                    (byte)((value.a * tint.a + 127) / 255));
                target.AddVert(vertex);
            }
            int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3) target.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
        }
        internal void Clear()
        { mesh = null; page = null; canvasRenderer.Clear(); SetVerticesDirty(); }
    }
}
