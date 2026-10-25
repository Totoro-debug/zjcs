using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // Port of the official CircleSpriteRenderer. Geometry, UV aspect correction,
    // offset and triangle order follow the client implementation.
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WorldCircleSpriteRenderer : MonoBehaviour
    {
        public Sprite Sprite;
        public Color Color = Color.white;
        public float Radius = 1;
        [Range(12, 128)] public int Segments = 64;
        public Vector2 UVOffset;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer.sharedMaterial == null)
                meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            if (Sprite != null) UpdateCircleMesh();
        }

        public void SetSprite(Sprite value)
        {
            if (Sprite == value) return;
            Sprite = value;
            UpdateCircleMesh();
        }

        public void SetColor(Color value)
        {
            if (Color == value) return;
            Color = value;
            UpdateCircleMesh();
        }

        public void UpdateCircleMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (Segments < 3) Segments = 3;
            if (mesh == null) mesh = new Mesh { name = "CircleSpriteMesh" };
            mesh.Clear();
            var vertices = new Vector3[Segments + 1];
            var triangles = new int[Segments * 3];
            var uvs = new Vector2[Segments + 1];
            var colors = new Color[Segments + 1];
            Rect rect = new Rect(0, 0, 1, 1);
            Vector2 aspect = Vector2.one;
            var properties = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(properties);
            if (Sprite != null)
            {
                float width = Mathf.Max(1, Sprite.texture.width);
                float height = Mathf.Max(1, Sprite.texture.height);
                rect = new Rect(Sprite.rect.x / width, Sprite.rect.y / height,
                    Sprite.rect.width / width, Sprite.rect.height / height);
                float ratio = Sprite.rect.width / Sprite.rect.height;
                if (ratio > 1) aspect.x = 1 / ratio;
                else aspect.y = ratio;
                properties.SetTexture("_MainTex", Sprite.texture);
            }
            else properties.SetTexture("_MainTex", null);
            meshRenderer.SetPropertyBlock(properties);
            float centerX = rect.x + rect.width * .5f + UVOffset.x * rect.width;
            float centerY = rect.y + rect.height * .5f + UVOffset.y * rect.height;
            uvs[0] = new Vector2(centerX, centerY);
            colors[0] = Color;
            float step = Mathf.PI * 2 / Segments;
            for (int i = 0; i < Segments; i++)
            {
                float x = Mathf.Cos(i * step), y = Mathf.Sin(i * step);
                vertices[i + 1] = new Vector3(x * Radius, y * Radius, 0);
                colors[i + 1] = Color;
                uvs[i + 1] = new Vector2(centerX + x * .5f * aspect.x * rect.width,
                    centerY + y * .5f * aspect.y * rect.height);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == Segments - 1 ? 1 : i + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
