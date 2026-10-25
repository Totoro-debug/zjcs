using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleEnvironmentView : IDisposable
    {
        private const string CatalogResource = "BattleScenes/catalog";
        private readonly Transform root;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private bool disposed;

        private OfficialBattleEnvironmentView(Transform parent, int gameId, string sceneName, bool? isDay)
        {
            TextAsset catalogAsset = Resources.Load<TextAsset>(CatalogResource);
            if (catalogAsset == null)
                throw new InvalidOperationException("Official battle-scene catalog is missing: " + CatalogResource);
            SceneCatalog catalog = JsonUtility.FromJson<SceneCatalog>(catalogAsset.text);
            if (catalog == null || catalog.manifest != "854_156383" || catalog.scenes == null)
                throw new InvalidDataException("Official battle-scene catalog is malformed or not pinned.");

            SceneBinding scene = null;
            foreach (SceneBinding candidate in catalog.scenes)
                if (candidate != null && candidate.gameId == gameId) { scene = candidate; break; }
            if (scene == null || string.IsNullOrEmpty(scene.sceneName)
                || (!string.IsNullOrEmpty(sceneName) && !string.Equals(scene.sceneName, sceneName, StringComparison.Ordinal))
                || string.IsNullOrEmpty(scene.geometryResource) || scene.textures == null
                || scene.rootOffset == null || scene.rootOffset.Length != 3
                || scene.batchCount <= 0 || (scene.defaultTime != 0 && scene.defaultTime != 1))
                throw new InvalidDataException("Official battle scene name or resources do not match game ID " + gameId + ".");

            SceneName = scene.sceneName;
            SourceParticleCount = scene.sourceParticleCount;
            SourceEnvironmentParticleCount = scene.sourceEnvironmentParticleCount;
            SourceSkeletonCount = scene.sourceSkeletonCount;
            GeometryResource = scene.geometryResource;
            RootOffset = new Vector3(scene.rootOffset[0], scene.rootOffset[1], scene.rootOffset[2]);
            OfficialBattleSceneBindings.CameraBinding camera = OfficialBattleSceneBindings.Camera(gameId);
            if (camera == null || camera.sceneName != scene.sceneName)
                throw new InvalidDataException("Official battle scene and camera bindings differ.");
            CameraPosition = new Vector3(camera.position[0], camera.position[1], camera.position[2]);
            CameraOrthographicSize = camera.orthographicSize;
            IsDay = isDay ?? scene.defaultTime == 0;

            TextAsset geometry = Resources.Load<TextAsset>(scene.geometryResource);
            if (geometry == null)
                throw new InvalidDataException("Official battle-scene geometry is missing: " + scene.geometryResource);
            foreach (TextureBinding binding in scene.textures)
            {
                if (binding == null || string.IsNullOrEmpty(binding.uri) || string.IsNullOrEmpty(binding.resource)
                    || textures.ContainsKey(binding.uri))
                    throw new InvalidDataException("Official battle-scene texture binding is malformed or duplicated.");
                Texture2D texture = Resources.Load<Texture2D>(binding.resource);
                if (texture == null)
                    throw new InvalidDataException("Official battle-scene texture is missing: " + binding.resource);
                textures.Add(binding.uri, texture);
            }

            GameObject rootObject = new GameObject("Official Battle Environment " + gameId);
            rootObject.transform.SetParent(parent, false);
            root = rootObject.transform;
            try
            {
                ReadGeometry(geometry.bytes, scene.batchCount);
                GeometryLayerBinding[] environmentLayers = IsDay ? scene.dayLayers : scene.nightLayers;
                foreach (GeometryLayerBinding layer in environmentLayers ?? Array.Empty<GeometryLayerBinding>())
                {
                    if (layer == null || string.IsNullOrEmpty(layer.geometryResource) || layer.batchCount <= 0)
                        throw new InvalidDataException("Official battle-scene environment layer is malformed.");
                    TextAsset environmentGeometry = Resources.Load<TextAsset>(layer.geometryResource);
                    if (environmentGeometry == null)
                        throw new InvalidDataException("Official battle-scene environment geometry is missing: "
                            + layer.geometryResource);
                    ReadGeometry(environmentGeometry.bytes, layer.batchCount);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string GeometryResource { get; }
        public string SceneName { get; }
        public int SourceParticleCount { get; }
        public int SourceEnvironmentParticleCount { get; }
        public int SourceSkeletonCount { get; }
        public Vector3 RootOffset { get; }
        public Vector3 CameraPosition { get; }
        public float CameraOrthographicSize { get; }
        public bool IsDay { get; }
        public int BatchCount { get; private set; }
        public int TextureCount => textures.Count;
        public Transform Root => root;

        public static OfficialBattleEnvironmentView Load(Transform parent, int gameId, string sceneName = null,
            bool? isDay = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            return new OfficialBattleEnvironmentView(parent, gameId, sceneName, isDay);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                else UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
            foreach (Mesh mesh in meshes)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            foreach (Material material in materials)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                else UnityEngine.Object.DestroyImmediate(material);
            }
            meshes.Clear();
            materials.Clear();
            textures.Clear();
            BatchCount = 0;
        }

        private void ReadGeometry(byte[] data, int expectedBatchCount)
        {
            if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 8) != "ZJMAP001")
                throw new InvalidDataException("Official battle-scene geometry header is invalid.");
            uint encodedBatchCount = ReadUInt32(data, 8);
            uint firstMetadataLength = ReadUInt32(data, 12);
            if (encodedBatchCount != expectedBatchCount)
                throw new InvalidDataException("Official battle-scene batch count differs from its catalog.");
            int firstBatchIndex = BatchCount;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Unity's Sprites/Default shader is unavailable.");
            var template = new Material(shader) { name = "Official Battle Environment" };
            int offset = 16;
            try
            {
                while (offset < data.Length)
                {
                    int metadataStart = offset;
                    string metadataJson = ReadMetadata(data, ref offset);
                    if (BatchCount == firstBatchIndex && Encoding.UTF8.GetByteCount(metadataJson) != firstMetadataLength)
                        throw new InvalidDataException("Official battle-scene first metadata length is invalid.");
                    BatchMetadata metadata = JsonUtility.FromJson<BatchMetadata>(metadataJson);
                    if (metadata == null || string.IsNullOrEmpty(metadata.texture)
                        || !textures.TryGetValue(metadata.texture, out Texture2D texture))
                        throw new InvalidDataException("Official battle-scene texture is not bound: "
                            + (metadata == null ? "<null>" : metadata.texture));

                    int vertexCount = ReadCount(data, ref offset, "vertex");
                    var vertices = new Vector3[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        // The retained 2D export stores downward Y; the prefab and camera use Unity Y.
                        vertices[i] = new Vector3(ReadSingle(data, ref offset) - RootOffset.x,
                            -ReadSingle(data, ref offset) - RootOffset.y, metadata.depth - RootOffset.z);
                    var uvs = new Vector2[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        uvs[i] = new Vector2(ReadSingle(data, ref offset), 1f - ReadSingle(data, ref offset));
                    var colors = new Color[vertexCount];
                    for (int i = 0; i < vertexCount; i++)
                        colors[i] = new Color(ReadSingle(data, ref offset), ReadSingle(data, ref offset),
                            ReadSingle(data, ref offset), ReadSingle(data, ref offset));

                    int indexCount = ReadCount(data, ref offset, "index");
                    if (indexCount % 3 != 0)
                        throw new InvalidDataException("Official battle-scene index count is not triangular.");
                    var indices = new int[indexCount];
                    for (int i = 0; i < indexCount; i++)
                    {
                        uint index = ReadUInt32(data, ref offset);
                        if (index >= vertexCount)
                            throw new InvalidDataException("Official battle-scene index exceeds its vertex count.");
                        indices[i] = (int)index;
                    }

                    var mesh = new Mesh { name = "Official Battle Scene Batch " + BatchCount.ToString("D3") };
                    if (vertexCount > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = vertices;
                    mesh.uv = uvs;
                    mesh.colors = colors;
                    mesh.triangles = indices;
                    mesh.RecalculateBounds();
                    meshes.Add(mesh);

                    var batchObject = new GameObject("Batch " + BatchCount.ToString("D3"));
                    batchObject.transform.SetParent(root, false);
                    batchObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = batchObject.AddComponent<MeshRenderer>();
                    var material = new Material(template)
                    {
                        name = "Official Battle Scene " + BatchCount.ToString("D3"),
                        mainTexture = texture,
                    };
                    renderer.sharedMaterial = material;
                    renderer.sortingOrder = metadata.layer * 1000 + metadata.order;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    materials.Add(material);
                    BatchCount++;

                    if (offset == data.Length) break;
                    if (offset > data.Length - 5 || data[offset + 4] != (byte)'{')
                        throw new InvalidDataException("Official battle-scene batch separator is invalid.");
                    offset += 4;
                }
                if (BatchCount - firstBatchIndex != expectedBatchCount)
                    throw new InvalidDataException("Official battle-scene batch count was not fully read.");
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(template);
                else UnityEngine.Object.DestroyImmediate(template);
                throw;
            }
            if (Application.isPlaying) UnityEngine.Object.Destroy(template);
            else UnityEngine.Object.DestroyImmediate(template);
        }

        private static string ReadMetadata(byte[] data, ref int offset)
        {
            if (offset >= data.Length || data[offset] != (byte)'{')
                throw new InvalidDataException("Official battle-scene metadata does not start with an object.");
            int start = offset;
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            while (offset < data.Length)
            {
                byte value = data[offset];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (value == (byte)'\\') escaped = true;
                    else if (value == (byte)'"') quoted = false;
                }
                else if (value == (byte)'"') quoted = true;
                else if (value == (byte)'{') depth++;
                else if (value == (byte)'}' && --depth == 0)
                {
                    offset++;
                    return Encoding.UTF8.GetString(data, start, offset - start);
                }
                offset++;
            }
            throw new InvalidDataException("Official battle-scene metadata is truncated.");
        }

        private static int ReadCount(byte[] data, ref int offset, string label)
        {
            uint count = ReadUInt32(data, ref offset);
            if (count == 0 || count > 3000000)
                throw new InvalidDataException("Official battle-scene " + label + " count is invalid.");
            return (int)count;
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            RequireBytes(data, offset, sizeof(uint));
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16)
                | (uint)data[offset + 3] << 24;
        }

        private static uint ReadUInt32(byte[] data, ref int offset)
        {
            uint value = ReadUInt32(data, offset);
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
                throw new InvalidDataException("Official battle-scene geometry payload is truncated.");
        }

        [Serializable]
        private sealed class SceneCatalog
        {
            public string manifest;
            public SceneBinding[] scenes;
        }

        [Serializable]
        private sealed class SceneBinding
        {
            public int gameId;
            public string sceneName;
            public string geometryResource;
            public int batchCount;
            public float[] rootOffset;
            public float[] cameraPosition;
            public float cameraOrthographicSize;
            public int defaultTime;
            public int sourceParticleCount;
            public int sourceEnvironmentParticleCount;
            public int sourceSkeletonCount;
            public GeometryLayerBinding[] dayLayers;
            public GeometryLayerBinding[] nightLayers;
            public TextureBinding[] textures;
        }

        [Serializable]
        private sealed class GeometryLayerBinding
        {
            public string geometryResource;
            public int batchCount;
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
            public float depth;
        }
    }
}
