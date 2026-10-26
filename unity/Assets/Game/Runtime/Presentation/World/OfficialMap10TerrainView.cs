using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10TerrainView : MonoBehaviour
    {
        private const string ResourceTexturePrefix = "Map10/Textures/";
        private const int Map10BatchCount = 1404;
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly Dictionary<Material, int> nativeSortingLayers = new Dictionary<Material, int>();
        private Transform generatedRoot;

        [SerializeField] private TextAsset geometry;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private string resourceTexturePrefix = ResourceTexturePrefix;
        [SerializeField] private int expectedBatchCount = Map10BatchCount;

        public int BatchCount { get; private set; }

        private void Awake()
        {
            if (geometry != null) Initialize(geometry, terrainMaterial, resourceTexturePrefix, expectedBatchCount);
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }

        public void Initialize(TextAsset source, Material materialTemplate = null)
        {
            Initialize(source, materialTemplate, ResourceTexturePrefix, Map10BatchCount);
        }

        public void Initialize(TextAsset source, Material materialTemplate, string texturePrefix, int batchCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(texturePrefix) || texturePrefix.StartsWith("/") || batchCount <= 0)
                throw new ArgumentException("Terrain texture prefix and batch count are invalid.");
            ClearGenerated();

            Material template = materialTemplate != null ? materialTemplate : terrainMaterial;
            if (template == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("Unity's Sprites/Default shader is unavailable.");
                template = new Material(shader);
                generatedMaterials.Add(template);
            }

            byte[] data = source.bytes;
            ValidateHeader(data, batchCount);
            int offset = 16;
            int batchIndex = 0;
            uint firstMetadataLength = (uint)(data[12] | data[13] << 8 | data[14] << 16)
                | (uint)data[15] << 24;
            generatedRoot = new GameObject("Official Map10 terrain batches").transform;
            generatedRoot.SetParent(transform, false);

            while (offset < data.Length)
            {
                string metadataJson = ReadMetadata(data, ref offset);
                if (batchIndex == 0 && Encoding.UTF8.GetByteCount(metadataJson) != firstMetadataLength)
                    throw new InvalidDataException("Map10 first metadata length differs from its header.");
                BatchMetadata metadata = JsonUtility.FromJson<BatchMetadata>(metadataJson);
                if (metadata == null || string.IsNullOrEmpty(metadata.texture))
                    throw new InvalidDataException("Map10 terrain batch has no texture binding.");

                int vertexCount = ReadCount(data, ref offset, "vertex");
                Vector3[] positions = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    positions[i] = new Vector3(ReadSingle(data, ref offset), ReadSingle(data, ref offset), 0f);

                Vector2[] uvs = new Vector2[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    uvs[i] = new Vector2(ReadSingle(data, ref offset), ReadSingle(data, ref offset));

                Color[] colors = new Color[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                    colors[i] = new Color(ReadSingle(data, ref offset), ReadSingle(data, ref offset),
                        ReadSingle(data, ref offset), ReadSingle(data, ref offset));

                int indexCount = ReadCount(data, ref offset, "index");
                if (indexCount % 3 != 0) throw new InvalidDataException("Map10 triangle index count is invalid.");
                int[] indices = new int[indexCount];
                for (int i = 0; i < indexCount; i++)
                {
                    uint index = ReadUInt32(data, ref offset);
                    if (index >= vertexCount) throw new InvalidDataException("Map10 triangle index exceeds its vertex count.");
                    indices[i] = (int)index;
                }

                Texture2D texture = LoadTexture(metadata.texture, texturePrefix);
                CreateBatch(batchIndex, metadata, positions, uvs, colors, indices, texture, template);
                batchIndex++;

                if (offset == data.Length) break;
                RequireBytes(data, offset, sizeof(uint) + 1);
                offset += sizeof(uint);
                if (data[offset] != (byte)'{') throw new InvalidDataException("Map10 batch separator is invalid.");
            }

            if (batchIndex != batchCount) throw new InvalidDataException("Terrain batch count differs from its pinned source.");
            BatchCount = batchIndex;
            resourceTexturePrefix = texturePrefix;
            expectedBatchCount = batchCount;
            if (texturePrefix == "Map11/Textures/")
            {
                TextAsset baseGround = Resources.Load<TextAsset>("Map11/BaseGround/geometry");
                if (baseGround == null) throw new InvalidDataException("Native Map11 base ground is not bound.");
                var nativeGround = new GameObject("Official base Ground Tilemap polygons");
                nativeGround.transform.SetParent(generatedRoot, false);
                nativeGround.AddComponent<OfficialMap10TerrainView>().Initialize(baseGround, template,
                    "Map11/BaseGround/", ReadHeaderCount(baseGround.bytes));
            }
        }

        private void CreateBatch(int index, BatchMetadata metadata, Vector3[] positions, Vector2[] uvs,
            Color[] colors, int[] indices, Texture2D texture, Material template)
        {
            var mesh = new Mesh { name = "Map10 terrain " + index };
            if (positions.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);

            var batch = new GameObject("Terrain " + index.ToString("D4"));
            batch.transform.SetParent(generatedRoot, false);
            var filter = batch.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = batch.AddComponent<MeshRenderer>();
            var material = new Material(template) { name = "Map10 " + Path.GetFileName(metadata.texture) };
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            renderer.sharedMaterial = material;
            renderer.sortingOrder = metadata.layer * 1000 + metadata.order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            generatedMaterials.Add(material);
            nativeSortingLayers.Add(material, metadata.layer);
        }

        public void BindRegionLighting(Material nativeLitMaterial)
        {
            if (nativeLitMaterial == null) throw new ArgumentNullException(nameof(nativeLitMaterial));
            // Native Map11 Light2D targets sorting layers 16, 17 and 18. Water
            // masks and other layers retain their own material path.
            foreach (var pair in nativeSortingLayers)
            {
                if (pair.Value < 16 || pair.Value > 18) continue;
                Texture main = pair.Key.mainTexture;
                pair.Key.shader = nativeLitMaterial.shader;
                pair.Key.CopyPropertiesFromMaterial(nativeLitMaterial);
                pair.Key.mainTexture = main;
            }
        }

        private static Texture2D LoadTexture(string uri, string resourceTexturePrefix)
        {
            const string uriPrefix = "res://assets/official/sprites/";
            if (!uri.StartsWith(uriPrefix, StringComparison.Ordinal))
                throw new InvalidDataException("Map10 batch references a texture outside its official sprite payload.");
            string resource = resourceTexturePrefix + Path.GetFileNameWithoutExtension(uri);
            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null) throw new InvalidDataException("Official Map10 texture is not bound in Resources: " + resource);
            return texture;
        }

        private static string ReadMetadata(byte[] data, ref int offset)
        {
            if (offset >= data.Length || data[offset] != (byte)'{')
                throw new InvalidDataException("Map10 batch metadata does not start with an object.");
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
            throw new InvalidDataException("Map10 batch metadata is truncated.");
        }

        private static void ValidateHeader(byte[] data, int expectedBatchCount)
        {
            if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 8) != "ZJMAP001")
                throw new InvalidDataException("Map10 terrain header is invalid.");
            if (ReadHeaderCount(data) == expectedBatchCount) return;
            throw new InvalidDataException("Terrain geometry header does not match its expected format or batch count.");
        }

        private static int ReadHeaderCount(byte[] data) =>
            data[8] | data[9] << 8 | data[10] << 16 | data[11] << 24;

        private static int ReadCount(byte[] data, ref int offset, string label)
        {
            uint count = ReadUInt32(data, ref offset);
            if (count == 0 || count > 1000000) throw new InvalidDataException("Map10 " + label + " count is invalid.");
            return (int)count;
        }

        private static uint ReadUInt32(byte[] data, ref int offset)
        {
            RequireBytes(data, offset, sizeof(uint));
            uint value = (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16)
                | (uint)data[offset + 3] << 24;
            offset += sizeof(uint);
            return value;
        }

        private static float ReadSingle(byte[] data, ref int offset)
        {
            RequireBytes(data, offset, sizeof(float));
            byte[] value = { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
            offset += sizeof(float);
            if (!BitConverter.IsLittleEndian) Array.Reverse(value);
            return BitConverter.ToSingle(value, 0);
        }

        private static void RequireBytes(byte[] data, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > data.Length - count)
                throw new InvalidDataException("Map10 terrain geometry payload is truncated.");
        }

        private void ClearGenerated()
        {
            if (generatedRoot != null)
            {
                if (Application.isPlaying) Destroy(generatedRoot.gameObject);
                else DestroyImmediate(generatedRoot.gameObject);
            }
            generatedRoot = null;
            foreach (Mesh mesh in generatedMeshes)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            foreach (Material material in generatedMaterials)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            generatedMeshes.Clear();
            generatedMaterials.Clear();
            nativeSortingLayers.Clear();
            BatchCount = 0;
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
