using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap11Tools
    {
        private const string PayloadRoot = "Assets/Official/World/Map/11";
        private const string GroundAsset = PayloadRoot + "/Map11Terrain.prefab";
        private const string MiniMapAsset = PayloadRoot + "/NativeMiniMap_11.prefab";
        private const string EntityAsset = PayloadRoot + "/Map11Entities.prefab";
        private const string EntityCatalogResource = "Assets/Official/World/Map/11/Resources/Map11/Entities/catalog.json";
        private const string CatalogAsset = "Assets/Game/Resources/World/OfficialWorldMapAssetCatalog.asset";
        private const string DefinitionAsset = PayloadRoot + "/map11.json";
        private const string SourceManifestAsset = PayloadRoot + "/source-manifest.json";

        [MenuItem("Zjcs/World/Import and Validate Map11")]
        public static void ImportAndValidate()
        {
            RequireEditorVersion();
            string resources = PayloadRoot + "/Resources";
            if (Resources.Load<TextAsset>("Map11/MiniMap/catalog") == null || Resources.Load<TextAsset>("Map11/Audio/bgm") == null)
                AssetDatabase.ImportAsset(resources, ImportAssetOptions.ImportRecursive
                    | ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            BuildPrefabs();
            BindCatalog();
            ValidateMap();
        }

        private static void BuildPrefabs()
        {
            TextAsset geometry = AssetDatabase.LoadAssetAtPath<TextAsset>(PayloadRoot + "/map11.geom.bytes");
            if (geometry == null) throw new InvalidOperationException("Map11 geometry payload is missing.");

            var groundRoot = new GameObject("Map11Terrain");
            OfficialMap10TerrainView terrain = groundRoot.AddComponent<OfficialMap10TerrainView>();
            var terrainSerialized = new SerializedObject(terrain);
            terrainSerialized.FindProperty("geometry").objectReferenceValue = geometry;
            terrainSerialized.FindProperty("resourceTexturePrefix").stringValue = "Map11/Textures/";
            terrainSerialized.FindProperty("expectedBatchCount").intValue = 14427;
            terrainSerialized.ApplyModifiedPropertiesWithoutUndo();
            SavePrefab(groundRoot, GroundAsset);

            var miniRoot = new GameObject("NativeMiniMap_11");
            OfficialMap10MiniMapView miniMap = miniRoot.AddComponent<OfficialMap10MiniMapView>();
            var miniSerialized = new SerializedObject(miniMap);
            miniSerialized.FindProperty("mapClassId").intValue = 11;
            miniSerialized.FindProperty("catalogResource").stringValue = "Map11/MiniMap/catalog";
            miniSerialized.ApplyModifiedPropertiesWithoutUndo();
            SavePrefab(miniRoot, MiniMapAsset);

            var entityRoot = new GameObject("Map11Entities");
            OfficialMap10EntityView entityView = entityRoot.AddComponent<OfficialMap10EntityView>();
            var entitySerialized = new SerializedObject(entityView);
            entitySerialized.FindProperty("mapClassId").intValue = 11;
            entitySerialized.FindProperty("catalogResource").stringValue = "Map11/Entities/catalog";
            entitySerialized.ApplyModifiedPropertiesWithoutUndo();
            SavePrefab(entityRoot, EntityAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void SavePrefab(GameObject source, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            if (saved == null) throw new InvalidOperationException("Unity could not create Map11 prefab: " + path);
        }

        private static void BindCatalog()
        {
            var definition = AssetDatabase.LoadAssetAtPath<TextAsset>(DefinitionAsset);
            var sourceManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceManifestAsset);
            var ground = AssetDatabase.LoadAssetAtPath<GameObject>(GroundAsset);
            var miniMap = AssetDatabase.LoadAssetAtPath<GameObject>(MiniMapAsset);
            var entity = AssetDatabase.LoadAssetAtPath<GameObject>(EntityAsset);
            if (definition == null || sourceManifest == null || ground == null || miniMap == null || entity == null)
                throw new InvalidOperationException("Map11 catalog requires its definition, source manifest, and visual prefabs.");

            OfficialWorldMapAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialWorldMapAssetCatalog>(CatalogAsset);
            if (catalog == null) throw new InvalidOperationException("Official world map catalog is missing.");
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            if (entries == null) throw new InvalidOperationException("Official map catalog entries field is missing.");
            int index = -1;
            for (int candidate = 0; candidate < entries.arraySize; candidate++)
                if (entries.GetArrayElementAtIndex(candidate).FindPropertyRelative("ClassId").intValue == 11)
                {
                    index = candidate;
                    break;
                }
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("ClassId").intValue = 11;
            entry.FindPropertyRelative("GroundPrefab").objectReferenceValue = ground;
            entry.FindPropertyRelative("MiniMapPrefab").objectReferenceValue = miniMap;
            entry.FindPropertyRelative("EntityPrefab").objectReferenceValue = entity;
            entry.FindPropertyRelative("DefinitionJson").objectReferenceValue = definition;
            entry.FindPropertyRelative("SourceManifestJson").objectReferenceValue = sourceManifest;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateMap()
        {
            OfficialWorldMapSession session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), 11);
            if (session.Definition.MaxRegionProgress == null || session.Definition.MaxRegionProgress.Length != 77
                || session.Definition.Entities == null || session.Definition.Entities.Length != 1803
                || session.Definition.EntityComponents == null || session.Definition.EntityComponents.Length != 1803
                || session.Definition.Navigation == null || session.Definition.Navigation.Width <= 0
                || session.Definition.Navigation.Height <= 0
                || CountEntityClass(session.Definition, 6010) != 17
                || CountEntityClass(session.Definition, 6011) != 49)
                throw new InvalidDataException("Map11 official map definition is incomplete.");

            ValidateAdjacentPath(session);
            GameObject ground = AssetDatabase.LoadAssetAtPath<GameObject>(GroundAsset);
            GameObject miniMap = AssetDatabase.LoadAssetAtPath<GameObject>(MiniMapAsset);
            GameObject entityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntityAsset);
            TextAsset entityCatalog = AssetDatabase.LoadAssetAtPath<TextAsset>(EntityCatalogResource);
            OfficialMap10TerrainView terrainView = ground == null ? null : ground.GetComponent<OfficialMap10TerrainView>();
            OfficialMap10MiniMapView miniMapView = miniMap == null ? null : miniMap.GetComponent<OfficialMap10MiniMapView>();
            OfficialMap10EntityView entityView = entityPrefab == null ? null : entityPrefab.GetComponent<OfficialMap10EntityView>();
            if (terrainView == null || miniMapView == null || entityView == null || entityCatalog == null)
                throw new InvalidDataException("Map11 visual prefabs or entity catalog are missing their native views.");
            int entityCount = OfficialMap10EntityView.ValidateCatalog(entityCatalog, 11,
                out int modelCount, out int geometryBatches, out int spineCount, out int textureCount);
            Debug.Log("Map11 resources passed: 77 regions, " + entityCount + " entities, " + modelCount
                + " displays, " + geometryBatches + " entity geometry batches, " + spineCount + " Spine instances, "
                + textureCount + " textures, 14,427 terrain batches, 85 minimap batches.");
        }

        private static int CountEntityClass(OfficialMapDefinition definition, int entityClassId)
        {
            int count = 0;
            foreach (OfficialMapEntityPlacement entity in definition.Entities)
                if (entity != null && entity.EntityId == entityClassId) count++;
            return count;
        }

        private static void ValidateAdjacentPath(OfficialWorldMapSession session)
        {
            var directions = new[] { new MapGridPoint(0, -1), new MapGridPoint(-1, 0),
                new MapGridPoint(0, 1), new MapGridPoint(1, 0) };
            for (int y = 0; y < session.Definition.Navigation.Height; y++)
            for (int x = 0; x < session.Definition.Navigation.Width; x++)
            {
                var start = new MapGridPoint(x, y);
                if (!session.Movement.IsFit(start)) continue;
                foreach (MapGridPoint direction in directions)
                {
                    MapGridPoint target = start + direction;
                    if (!session.Movement.IsFit(target)) continue;
                    var path = session.FindPath(new OfficialMapPathRequest
                    {
                        Start = start,
                        End = target,
                        PreferDistance = 1,
                        PreciseTarget = true,
                        BodyOffsets = new[] { new MapGridPoint(0, 0) }
                    });
                    if (path != null && path.Count >= 2 && path[path.Count - 1].Equals(target)) return;
                }
            }
            throw new InvalidOperationException("Map11 official ground rules did not produce a neighboring-cell route.");
        }

        private static void RequireEditorVersion()
        {
            if (!string.Equals(Application.unityVersion, "2022.3.57f1c2", StringComparison.Ordinal))
                throw new InvalidOperationException("Map11 import requires Unity 2022.3.57f1c2; found " + Application.unityVersion);
        }
    }
}
