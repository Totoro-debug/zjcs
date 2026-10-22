using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10MiniMapView : MonoBehaviour
    {
        private const string CatalogResource = "Map10/MiniMap/catalog";
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private Transform generatedRoot;

        public int BatchCount { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }

        public void Initialize()
        {
            TextAsset catalog = Resources.Load<TextAsset>(CatalogResource);
            if (catalog == null) throw new InvalidOperationException("Official Map10 minimap catalog is missing: " + CatalogResource);
            Initialize(catalog);
        }

        public void Initialize(TextAsset catalogSource)
        {
            if (catalogSource == null) throw new ArgumentNullException(nameof(catalogSource));
            ClearGenerated();
            MiniMapCatalog catalog = JsonUtility.FromJson<MiniMapCatalog>(catalogSource.text);
            if (catalog == null || catalog.format != 1 || catalog.mapClassId != 10 || catalog.geometryVersion != 9
                || catalog.geometryBatchCount != 9 || string.IsNullOrEmpty(catalog.geometryResource) || catalog.textures == null)
                throw new InvalidDataException("Official Map10 minimap catalog is malformed.");
            TextAsset geometry = Resources.Load<TextAsset>(catalog.geometryResource);
            if (geometry == null) throw new InvalidDataException("Official Map10 minimap geometry is missing: " + catalog.geometryResource);
            generatedRoot = new GameObject("Official Map10 minimap geometry").transform;
            generatedRoot.SetParent(transform, false);
            try
            {
                ReadGeometry(geometry.bytes, catalog, CreateMaterialTemplate());
            }
            catch
            {
                ClearGenerated();
                throw;
            }
        }

        private void ReadGeometry(byte[] data, MiniMapCatalog catalog, Material template)
        {
            if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 8) != "ZJMAP001")
                throw new InvalidDataException("Official Map10 minimap geometry header is invalid.");
            int version = data[8] == (byte)'|'
                ? data[9] | data[10] << 8 | data[11] << 16
                : BitConverter.ToInt32(data, 8);
            if (version != catalog.geometryVersion)
                throw new InvalidDataException("Official Map10 minimap geometry version differs from its catalog.");

            int offset = 16;
            uint firstMetadataLength = ReadUInt32(data, ref offset, 12);
            while (offset < data.Length)
            {
                string json = ReadMetadata(data, ref offset);
                if (BatchCount == 0 && Encoding.UTF8.GetByteCount(json) != firstMetadataLength)
                    throw new InvalidDataException("Official Map10 minimap metadata length is invalid.");
                BatchMetadata metadata = JsonUtility.FromJson<BatchMetadata>(json);
                if (metadata == null || string.IsNullOrEmpty(metadata.texture))
                    throw new InvalidDataException("Official Map10 minimap batch has no texture URI.");
                TextureBinding binding = FindTexture(catalog, metadata.texture);
                Texture2D texture = Resources.Load<Texture2D>(binding.resource);
                if (texture == null) throw new InvalidDataException("Official Map10 minimap texture is missing: " + binding.resource);

                int vertexCount = ReadCount(data, ref offset, "vertex");
                var vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = new Vector3(ReadSingle(data, ref offset), ReadSingle(data, ref offset), 0f);
                var uvs = new Vector2[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    uvs[i] = new Vector2(ReadSingle(data, ref offset), ReadSingle(data, ref offset));
                var colors = new Color[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    colors[i] = new Color(ReadSingle(data, ref offset), ReadSingle(data, ref offset),
                        ReadSingle(data, ref offset), ReadSingle(data, ref offset));
                int indexCount = ReadCount(data, ref offset, "index");
                if (indexCount % 3 != 0) throw new InvalidDataException("Official Map10 minimap index count is not triangular.");
                var indices = new int[indexCount];
                for (int i = 0; i < indexCount; i++)
                {
                    uint index = ReadUInt32(data, ref offset);
                    if (index >= vertexCount) throw new InvalidDataException("Official Map10 minimap index exceeds its vertex count.");
                    indices[i] = (int)index;
                }

                var mesh = new Mesh { name = "Map10 minimap batch " + BatchCount };
                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.colors = colors;
                mesh.triangles = indices;
                mesh.RecalculateBounds();
                generatedMeshes.Add(mesh);
                var material = new Material(template) { name = "Map10 minimap material " + BatchCount };
                material.mainTexture = texture;
                material.renderQueue = (int)RenderQueue.Transparent;
                generatedMaterials.Add(material);
                var batch = new GameObject("Minimap " + BatchCount.ToString("D2"));
                batch.transform.SetParent(generatedRoot, false);
                batch.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = batch.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = metadata.layer * 1000 + metadata.order;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                BatchCount++;

                if (offset == data.Length) break;
                if (offset > data.Length - 5 || data[offset + 4] != (byte)'{')
                    throw new InvalidDataException("Official Map10 minimap geometry separator is invalid.");
                offset += 4;
            }
            if (BatchCount != catalog.geometryBatchCount)
                throw new InvalidDataException("Official Map10 minimap batch count differs from its catalog.");
            DestroyGeneratedObject(template);
        }

        private static TextureBinding FindTexture(MiniMapCatalog catalog, string uri)
        {
            foreach (TextureBinding texture in catalog.textures)
                if (texture != null && string.Equals(texture.uri, uri, StringComparison.Ordinal)) return texture;
            throw new InvalidDataException("Official Map10 minimap texture is not in its resource catalog: " + uri);
        }

        private static Material CreateMaterialTemplate()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Unity's Sprites/Default shader is unavailable.");
            return new Material(shader);
        }

        private static string ReadMetadata(byte[] data, ref int offset)
        {
            if (offset >= data.Length || data[offset] != (byte)'{')
                throw new InvalidDataException("Official Map10 minimap metadata does not start with an object.");
            int start = offset;
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (; offset < data.Length; offset++)
            {
                byte value = data[offset];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (value == (byte)'\\') escaped = true;
                    else if (value == (byte)'"') inString = false;
                    continue;
                }
                if (value == (byte)'"') inString = true;
                else if (value == (byte)'{') depth++;
                else if (value == (byte)'}' && --depth == 0)
                {
                    int length = offset - start + 1;
                    offset++;
                    return Encoding.UTF8.GetString(data, start, length);
                }
            }
            throw new InvalidDataException("Official Map10 minimap metadata is truncated.");
        }

        private static int ReadCount(byte[] data, ref int offset, string label)
        {
            uint count = ReadUInt32(data, ref offset);
            if (count == 0 || count > 1000000) throw new InvalidDataException("Official Map10 minimap " + label + " count is invalid.");
            return (int)count;
        }

        private static uint ReadUInt32(byte[] data, ref int offset, int explicitOffset = -1)
        {
            if (explicitOffset >= 0) offset = explicitOffset;
            RequireBytes(data, offset, sizeof(uint));
            uint value = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16)
                | (uint)data[offset + 3] << 24;
            offset += sizeof(uint);
            return value;
        }

        private static float ReadSingle(byte[] data, ref int offset)
        {
            RequireBytes(data, offset, sizeof(float));
            byte[] bytes = { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
            offset += sizeof(float);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static void RequireBytes(byte[] data, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > data.Length - count)
                throw new InvalidDataException("Official Map10 minimap geometry is truncated.");
        }

        private void ClearGenerated()
        {
            if (generatedRoot != null) DestroyGeneratedObject(generatedRoot.gameObject);
            generatedRoot = null;
            foreach (Mesh mesh in generatedMeshes) DestroyGeneratedObject(mesh);
            foreach (Material material in generatedMaterials) DestroyGeneratedObject(material);
            generatedMeshes.Clear();
            generatedMaterials.Clear();
            BatchCount = 0;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        [Serializable]
        private sealed class MiniMapCatalog
        {
            public int format;
            public int mapClassId;
            public string geometryResource;
            public int geometryVersion;
            public int geometryBatchCount;
            public TextureBinding[] textures;
        }

        [Serializable]
        private sealed class TextureBinding
        {
            public string uri;
            public string resource;
        }

        [Serializable]
        private sealed class BatchMetadata
        {
            public string texture;
            public int layer;
            public int order;
        }
    }
}
