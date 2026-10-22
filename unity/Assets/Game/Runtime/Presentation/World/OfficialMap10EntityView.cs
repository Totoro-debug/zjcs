using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10EntityView : MonoBehaviour
    {
        private const string CatalogResource = "Map10/Entities/catalog";
        private readonly Dictionary<string, GeneratedModel> models = new Dictionary<string, GeneratedModel>();
        private readonly Dictionary<Collider2D, OfficialMap10EntityInfo> hitTargets =
            new Dictionary<Collider2D, OfficialMap10EntityInfo>();
        private readonly Dictionary<int, GameObject> entityObjects = new Dictionary<int, GameObject>();
        private readonly List<OfficialMap10EntityInfo> entityInfos = new List<OfficialMap10EntityInfo>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private Transform generatedRoot;
        private Camera inputCamera;
        private Vector2 mapOrigin;

        public event Action<OfficialMap10EntityInfo> EntitySelected;

        public int EntityCount { get; private set; }
        public IReadOnlyList<OfficialMap10EntityInfo> EntityInfos => entityInfos;

        public bool SetEntityActive(int placementId, bool active)
        {
            if (!entityObjects.TryGetValue(placementId, out GameObject entity) || entity == null) return false;
            entity.SetActive(active);
            return true;
        }

        public GameObject FindEntityObject(int placementId)
        {
            return entityObjects.TryGetValue(placementId, out GameObject entity) ? entity : null;
        }

        public bool TrySelectScreenPosition(Vector2 screenPosition)
        {
            if (inputCamera == null) return false;
            Vector3 screen = screenPosition;
            screen.z = Mathf.Abs(inputCamera.transform.position.z);
            Vector3 world = inputCamera.ScreenToWorldPoint(screen);
            foreach (KeyValuePair<Collider2D, OfficialMap10EntityInfo> target in hitTargets)
                if (target.Key != null && target.Key.OverlapPoint(world))
                {
                    EntitySelected?.Invoke(target.Value);
                    return true;
                }
            return false;
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }

        public void Initialize(Transform mapRoot, Vector2 mapOrigin, Camera camera = null)
        {
            TextAsset source = Resources.Load<TextAsset>(CatalogResource);
            if (source == null) throw new InvalidOperationException("Official Map10 entity catalog is missing: " + CatalogResource);
            Initialize(source, mapRoot, mapOrigin, camera);
        }

        public void Initialize(TextAsset source, Transform mapRoot, Vector2 mapOrigin, Camera camera = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (mapRoot == null) throw new ArgumentNullException(nameof(mapRoot));
            ClearGenerated();
            inputCamera = camera != null ? camera : Camera.main;
            this.mapOrigin = mapOrigin;

            EntityCatalogPayload catalog = JsonUtility.FromJson<EntityCatalogPayload>(source.text);
            if (catalog == null || catalog.mapClassId != 10 || catalog.entities == null || catalog.entities.Length != 82
                || catalog.models == null || catalog.models.Length == 0)
                throw new InvalidDataException("Official Map10 entity catalog is malformed.");

            generatedRoot = new GameObject("Official Map10 entities").transform;
            generatedRoot.SetParent(mapRoot, false);
            try
            {
                foreach (EntityModel model in catalog.models)
                    if (model != null && !string.IsNullOrEmpty(model.modelPath)) models.Add(model.modelPath, BuildModel(model));

                foreach (EntityRecord record in catalog.entities)
                {
                    if (record == null || record.grid == null || record.grid.Length != 2 || record.placementId <= 0)
                        throw new InvalidDataException("Official Map10 entity placement is malformed.");
                    CreateEntity(record, mapOrigin);
                }
            }
            catch
            {
                ClearGenerated();
                throw;
            }
        }

        public GameObject SpawnStoryEntity(int entityClassId, string modelPath, Vector2 grid)
        {
            if (generatedRoot == null) throw new InvalidOperationException("Map10 entities are not initialized.");
            if (entityClassId <= 0 || string.IsNullOrWhiteSpace(modelPath) || !models.ContainsKey(modelPath))
                throw new InvalidDataException("Official Map10 story entity model is missing: " + modelPath);
            return CreateEntity(new EntityRecord
            {
                placementId = 0,
                entityClassId = entityClassId,
                grid = new[] { Mathf.RoundToInt(grid.x), Mathf.RoundToInt(grid.y) },
                active = true,
                modelPath = modelPath,
                stageIds = Array.Empty<int>()
            }, mapOrigin, false);
        }

        public void RemoveStoryEntity(GameObject entity)
        {
            if (entity == null || generatedRoot == null || !entity.transform.IsChildOf(generatedRoot)) return;
            DestroyGeneratedObject(entity);
        }

        public void PlayStoryEntityAnimation(GameObject entity, string animation)
        {
            if (entity == null || generatedRoot == null || !entity.transform.IsChildOf(generatedRoot))
                throw new ArgumentException("Official Map10 story actor is not part of this map.", nameof(entity));
            if (animation != "appear_01" && animation != "disappear_01")
                throw new ArgumentOutOfRangeException(nameof(animation));
            TextAsset source = Resources.Load<TextAsset>("Map10/StoryActors/8402-" + animation);
            if (source == null)
                throw new InvalidOperationException("Official Map10 story actor animation is missing: " + animation);
            OfficialMap10MonsterView[] views = entity.GetComponentsInChildren<OfficialMap10MonsterView>(true);
            if (views.Length == 0)
                throw new InvalidOperationException("Official Map10 story actor has no Spine view.");
            foreach (OfficialMap10MonsterView view in views)
                view.PlayAnimation(source, false, animation == "appear_01");
        }

        public static int ValidateCatalog(TextAsset source, out int modelCount, out int geometryBatchCount,
            out int spineCount, out int textureCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EntityCatalogPayload catalog = JsonUtility.FromJson<EntityCatalogPayload>(source.text);
            if (catalog == null || catalog.mapClassId != 10 || catalog.entities == null || catalog.entities.Length != 82
                || catalog.models == null || catalog.models.Length == 0 || catalog.textureUris == null)
                throw new InvalidDataException("Official Map10 entity catalog is malformed.");
            foreach (EntityRecord entity in catalog.entities)
                if (entity == null || entity.placementId <= 0 || entity.grid == null || entity.grid.Length != 2)
                    throw new InvalidDataException("Official Map10 entity placement is malformed.");
            foreach (EntityModel model in catalog.models)
                if (model == null || string.IsNullOrEmpty(model.modelPath) || model.spines == null)
                    throw new InvalidDataException("Official Map10 entity model binding is malformed.");
            modelCount = catalog.models.Length;
            geometryBatchCount = 0;
            spineCount = 0;
            foreach (EntityModel model in catalog.models)
            {
                if (model.geometryBatchCount < 0 || (model.geometryBatchCount > 0 && string.IsNullOrEmpty(model.geometryResource)))
                    throw new InvalidDataException("Official Map10 entity geometry binding is malformed: " + model.modelPath);
                geometryBatchCount += model.geometryBatchCount;
                spineCount += model.spines.Length;
            }
            textureCount = catalog.textureUris.Length;
            return catalog.entities.Length;
        }

        private GeneratedModel BuildModel(EntityModel model)
        {
            var batches = new List<GeneratedBatch>();
            if (!string.IsNullOrEmpty(model.geometryResource))
            {
                TextAsset source = Resources.Load<TextAsset>(model.geometryResource);
                if (source == null)
                    throw new InvalidDataException("Official Map10 entity geometry is missing: " + model.geometryResource);
                ReadGeometry(source.bytes, model, batches);
            }

            foreach (SpineReference spine in model.spines)
            {
                if (spine == null || string.IsNullOrEmpty(spine.resource))
                    throw new InvalidDataException("Official Map10 entity Spine binding is malformed: " + model.modelPath);
            }
            return new GeneratedModel { batches = batches.ToArray(), spines = model.spines };
        }

        private GameObject CreateEntity(EntityRecord record, Vector2 mapOrigin, bool interactable = true)
        {
            var info = new OfficialMap10EntityInfo(record);
            var entity = new GameObject("Entity " + record.placementId + " (" + record.entityClassId + ")");
            entity.transform.SetParent(generatedRoot, false);
            entity.transform.localPosition = new Vector3(mapOrigin.x + record.grid[0], mapOrigin.y + record.grid[1],
                -record.grid[1] * 0.001f);
            entity.transform.localRotation = Quaternion.Euler(0f, 0f, record.direction * 90f);
            entity.SetActive(record.active);
            if (record.placementId > 0)
            {
                entityObjects.Add(record.placementId, entity);
                entityInfos.Add(info);
            }

            if (!string.IsNullOrEmpty(record.modelPath) && models.TryGetValue(record.modelPath, out GeneratedModel model))
            {
                foreach (GeneratedBatch batch in model.batches)
                {
                    var child = new GameObject("Geometry " + batch.sortingOrder);
                    child.transform.SetParent(entity.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = batch.mesh;
                    MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = batch.material;
                    renderer.sortingOrder = batch.sortingOrder;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                foreach (SpineReference spine in model.spines)
                {
                    TextAsset source = Resources.Load<TextAsset>(spine.resource);
                    if (source == null)
                        throw new InvalidDataException("Official Map10 entity Spine payload is missing: " + spine.resource);
                    var spineObject = new GameObject("Spine " + spine.key, typeof(MeshFilter), typeof(MeshRenderer));
                    spineObject.transform.SetParent(entity.transform, false);
                    OfficialMap10MonsterView view = spineObject.AddComponent<OfficialMap10MonsterView>();
                    view.Initialize(source, record.entityClassId);
                }
            }

            if (interactable)
            {
                var collider = entity.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;
                collider.isTrigger = true;
                hitTargets.Add(collider, info);
                EntityCount++;
            }
            return entity;
        }

        private void ReadGeometry(byte[] data, EntityModel model, List<GeneratedBatch> batches)
        {
            if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 8) != "ZJMAP001")
                throw new InvalidDataException("Official Map10 entity geometry header is invalid: " + model.modelPath);
            int version = data[8] == (byte)'|'
                ? data[9] | data[10] << 8 | data[11] << 16
                : BitConverter.ToInt32(data, 8);
            if (version != 1 && version != 2 && version != 3 && version != 8)
                throw new InvalidDataException("Unsupported official Map10 entity geometry version: " + version);

            int offset = 16;
            uint firstMetadataLength = ReadUInt32(data, ref offset, 12);
            Material template = CreateMaterialTemplate();
            while (offset < data.Length)
            {
                string metadataJson = ReadMetadata(data, ref offset);
                if (batches.Count == 0 && Encoding.UTF8.GetByteCount(metadataJson) != firstMetadataLength)
                    throw new InvalidDataException("Official Map10 entity geometry metadata length is invalid.");
                BatchMetadata metadata = JsonUtility.FromJson<BatchMetadata>(metadataJson);
                if (metadata == null || string.IsNullOrEmpty(metadata.texture))
                    throw new InvalidDataException("Official Map10 entity geometry has no texture binding.");
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
                if (indexCount % 3 != 0) throw new InvalidDataException("Official Map10 entity index count is not triangular.");
                var indices = new int[indexCount];
                for (int i = 0; i < indexCount; i++)
                {
                    uint index = ReadUInt32(data, ref offset);
                    if (index >= vertexCount) throw new InvalidDataException("Official Map10 entity index exceeds its vertex count.");
                    indices[i] = (int)index;
                }

                string textureResource = TextureResource(metadata.texture);
                Texture2D texture = Resources.Load<Texture2D>(textureResource);
                if (texture == null)
                    throw new InvalidDataException("Official Map10 entity texture is missing: " + textureResource);
                var mesh = new Mesh { name = model.modelId + " geometry " + batches.Count };
                if (vertexCount > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.colors = colors;
                mesh.triangles = indices;
                mesh.RecalculateBounds();
                generatedMeshes.Add(mesh);

                Material material = new Material(template) { name = model.modelId + " material " + batches.Count };
                material.mainTexture = texture;
                material.renderQueue = (int)RenderQueue.Transparent;
                generatedMaterials.Add(material);
                batches.Add(new GeneratedBatch
                {
                    mesh = mesh,
                    material = material,
                    sortingOrder = metadata.layer * 1000 + metadata.order
                });

                if (offset == data.Length) break;
                if (offset > data.Length - 5 || data[offset + 4] != (byte)'{')
                    throw new InvalidDataException("Official Map10 entity geometry separator is invalid.");
                offset += 4;
            }
            if (batches.Count != model.geometryBatchCount)
                throw new InvalidDataException("Official Map10 entity geometry batch count differs: " + model.modelPath);
            DestroyGeneratedObject(template);
        }

        private static Material CreateMaterialTemplate()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Unity's Sprites/Default shader is unavailable.");
            return new Material(shader);
        }

        private static string TextureResource(string uri)
        {
            const string prefix = "res://assets/official/sprites/";
            if (!uri.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("Map10 entity texture URI is outside the official sprite package.");
            return "Map10/Textures/" + Path.GetFileNameWithoutExtension(uri);
        }

        private static string ReadMetadata(byte[] data, ref int offset)
        {
            if (offset >= data.Length || data[offset] != (byte)'{')
                throw new InvalidDataException("Official Map10 entity metadata does not start with an object.");
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
            throw new InvalidDataException("Official Map10 entity metadata is truncated.");
        }

        private static int ReadCount(byte[] data, ref int offset, string label)
        {
            uint count = ReadUInt32(data, ref offset);
            if (count == 0 || count > 1000000)
                throw new InvalidDataException("Official Map10 entity " + label + " count is invalid.");
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
                throw new InvalidDataException("Official Map10 entity geometry is truncated.");
        }

        private void ClearGenerated()
        {
            if (generatedRoot != null) DestroyGeneratedObject(generatedRoot.gameObject);
            generatedRoot = null;
            foreach (Mesh mesh in generatedMeshes) DestroyGeneratedObject(mesh);
            foreach (Material material in generatedMaterials) DestroyGeneratedObject(material);
            generatedMeshes.Clear();
            generatedMaterials.Clear();
            models.Clear();
            hitTargets.Clear();
            entityObjects.Clear();
            entityInfos.Clear();
            EntityCount = 0;
        }

        private static void DestroyGeneratedObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        [Serializable]
        private sealed class EntityCatalogPayload
        {
            public int mapClassId;
            public EntityRecord[] entities;
            public EntityModel[] models;
            public string[] textureUris;
        }

        [Serializable]
        internal sealed class EntityRecord
        {
            public int placementId;
            public int entityClassId;
            public int typeId;
            public int[] grid;
            public int region;
            public int direction;
            public bool unique;
            public int area;
            public int state;
            public bool active;
            public int lockType;
            public int[] lockedByEntityGroups;
            public bool conditionEntityNeverDestroy;
            public bool conditionEntityInactiveInUnlock;
            public Map10RuntimeAwardItem[] awardItems;
            public int changeMapClassId;
            public string modelPath;
            public int[] stageIds;
        }

        [Serializable]
        private sealed class EntityModel
        {
            public string modelPath;
            public string modelId;
            public string geometryResource;
            public int geometryBatchCount;
            public SpineReference[] spines;
            public string interactionsJson;
            public string monsterSlotsJson;
            public bool overlapDisplay;
        }

        [Serializable]
        private sealed class SpineReference
        {
            public string key;
            public string resource;
        }

        [Serializable]
        private sealed class BatchMetadata
        {
            public string texture;
            public int layer;
            public int order;
        }

        private sealed class GeneratedModel
        {
            public GeneratedBatch[] batches;
            public SpineReference[] spines;
        }

        private sealed class GeneratedBatch
        {
            public Mesh mesh;
            public Material material;
            public int sortingOrder;
        }
    }

    public sealed class OfficialMap10EntityInfo
    {
        public int PlacementId { get; }
        public int EntityClassId { get; }
        public int TypeId { get; }
        public int Region { get; }
        public int Direction { get; }
        public int Area { get; }
        public int State { get; }
        public bool Active { get; }
        public int LockType { get; }
        public int[] LockedByEntityGroups { get; }
        public bool ConditionEntityNeverDestroy { get; }
        public bool ConditionEntityInactiveInUnlock { get; }
        public Map10RuntimeAwardItem[] AwardItems { get; }
        public int ChangeMapClassId { get; }
        public bool Unique { get; }
        public int[] Grid { get; }
        public int[] StageIds { get; }
        public string ModelPath { get; }

        internal OfficialMap10EntityInfo(OfficialMap10EntityView.EntityRecord record)
        {
            PlacementId = record.placementId;
            EntityClassId = record.entityClassId;
            TypeId = record.typeId;
            Region = record.region;
            Direction = record.direction;
            Area = record.area;
            State = record.state;
            Active = record.active;
            LockType = record.lockType;
            LockedByEntityGroups = record.lockedByEntityGroups ?? Array.Empty<int>();
            ConditionEntityNeverDestroy = record.conditionEntityNeverDestroy;
            ConditionEntityInactiveInUnlock = record.conditionEntityInactiveInUnlock;
            AwardItems = record.awardItems ?? Array.Empty<Map10RuntimeAwardItem>();
            ChangeMapClassId = record.changeMapClassId;
            Unique = record.unique;
            Grid = record.grid;
            StageIds = record.stageIds;
            ModelPath = record.modelPath;
        }
    }
}
