using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    // Independent UGUI carrier for the original dialog's setup geometry. These
    // three source SkeletonGraphic components select no starting animation.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NativeMap12BuildingGraphic : MaskableGraphic
    {
        public Mesh setupMesh;
        public Texture2D atlas;
        public float skeletonScale;
        public override Texture mainTexture => atlas != null ? atlas : s_WhiteTexture;
        private float UnitScale => skeletonScale * (canvas != null ? canvas.referencePixelsPerUnit : 100f);

        public override void Cull(Rect clipRect, bool validRect)
        {
            if (setupMesh == null || canvas == null) { base.Cull(clipRect, validRect); return; }
            Bounds bounds = setupMesh.bounds;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Transform root = canvas.rootCanvas.transform;
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y);
                Vector2 canvasPoint = root.InverseTransformPoint(rectTransform.TransformPoint(point * UnitScale));
                min = Vector2.Min(min, canvasPoint); max = Vector2.Max(max, canvasPoint);
            }
            bool culled = !validRect || !clipRect.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y), true);
            if (canvasRenderer.cull == culled) return;
            canvasRenderer.cull = culled;
            onCullStateChanged.Invoke(culled);
            OnCullingChanged();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (setupMesh == null) return;
            Vector3[] positions = setupMesh.vertices;
            Vector2[] uv = setupMesh.uv;
            int[] triangles = setupMesh.triangles;
            float scale = UnitScale;
            for (int i = 0; i < positions.Length; i++) vertices.AddVert(positions[i] * scale, color, uv[i]);
            for (int i = 0; i < triangles.Length; i += 3) vertices.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
