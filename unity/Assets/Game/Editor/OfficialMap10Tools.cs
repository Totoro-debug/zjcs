using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap10Tools
    {
        private const string PayloadRoot = "Assets/Official/World/Map/10";
        private const string GroundAsset = PayloadRoot + "/Map10Terrain.prefab";
        private const string MiniMapAsset = PayloadRoot + "/NativeMiniMap_10.prefab";
        private const string MiniMapGeometryAsset = PayloadRoot + "/Resources/Map10/MiniMap/geometry.geom.bytes";
        private const string MiniMapCatalogAsset = PayloadRoot + "/Resources/Map10/MiniMap/catalog.json";
        private const string PrefabManifestAsset = PayloadRoot + "/native-prefab-source-manifest.json";
        private const string CatalogAsset = "Assets/Game/Resources/World/OfficialWorldMapAssetCatalog.asset";

        [Serializable]
        private sealed class PrefabInput
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        private sealed class PrefabOutput
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class PrefabManifest
        {
            public int format;
            public string manifest;
            public string unityVersion;
            public int mapClassId;
            public PrefabInput[] inputs;
            public PrefabOutput[] outputs;
        }

        [MenuItem("Zjcs/World/Import and Validate Map10")]
        public static void Validate()
        {
            EnsureMap10Prefabs();
            BindMap10Catalog();
            if (OfficialWorldMapAssetCatalog.LoadDefault() == null)
                throw new InvalidOperationException("Official Map10 Resources catalog could not be loaded.");

            var ground = AssetDatabase.LoadAssetAtPath<GameObject>(GroundAsset);
            var miniMap = AssetDatabase.LoadAssetAtPath<GameObject>(MiniMapAsset);
            if (ground == null || miniMap == null)
                throw new InvalidOperationException("Map10 native ground or minimap prefab is missing.");
            var groundGeometry = AssetDatabase.LoadAssetAtPath<TextAsset>(PayloadRoot + "/map10.geom.bytes");
            var miniMapCatalog = AssetDatabase.LoadAssetAtPath<TextAsset>(MiniMapCatalogAsset);
            var groundInstance = PrefabUtility.InstantiatePrefab(ground) as GameObject;
            var miniMapInstance = PrefabUtility.InstantiatePrefab(miniMap) as GameObject;
            if (groundInstance == null || miniMapInstance == null)
                throw new InvalidOperationException("Map10 native rendering prefabs could not be instantiated.");
            try
            {
                OfficialMap10TerrainView groundView = groundInstance.GetComponent<OfficialMap10TerrainView>();
                if (groundView == null) throw new InvalidOperationException("Map10 ground prefab lost its terrain renderer.");
                groundView.Initialize(groundGeometry);
                OfficialMap10MiniMapView miniMapView = miniMapInstance.GetComponent<OfficialMap10MiniMapView>();
                if (miniMapView == null) throw new InvalidOperationException("Map10 minimap prefab lost its native renderer.");
                miniMapView.Initialize(miniMapCatalog);
                RequireVisualAsset(groundInstance, "Map10Terrain", 1404);
                RequireVisualAsset(miniMapInstance, "NativeMiniMap_10", 9);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(groundInstance);
                UnityEngine.Object.DestroyImmediate(miniMapInstance);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<OfficialWorldMapAssetCatalog>(CatalogAsset);
            var host = new GameObject("Map10 validation provider");
            try
            {
                OfficialWorldMapAssetProvider provider = host.AddComponent<OfficialWorldMapAssetProvider>();
                var providerData = new SerializedObject(provider);
                providerData.FindProperty("catalog").objectReferenceValue = catalog;
                providerData.ApplyModifiedPropertiesWithoutUndo();

                OfficialWorldMapSession session = OfficialWorldMapSession.Load(provider, 10);
                OfficialMapNavigation navigation = session.Definition.Navigation;
                if (navigation.Width != 60 || navigation.Height != 60 || session.Definition.CharacterMover.MoveMask != 65
                    || navigation.MaxSteps != 27)
                    throw new InvalidOperationException("Map10 official navigation identity or movement settings differ.");
                ValidateAdjacentPath(session);
                Debug.Log("Map10 official session passed: 60x60 navigation, mask 65, max 27 steps, neighboring route, 1,404 ground batches, and 9 official minimap batches.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void EnsureMap10Prefabs()
        {
            OfficialMap10TerrainTools.BuildAndValidate();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            TextAsset miniMapCatalog = AssetDatabase.LoadAssetAtPath<TextAsset>(MiniMapCatalogAsset);
            if (miniMapCatalog == null || AssetDatabase.LoadAssetAtPath<TextAsset>(MiniMapGeometryAsset) == null)
                throw new InvalidOperationException("Run scripts/prepare_unity_map10_minimap.py to prepare verified Map10 minimap resources.");

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string manifestPath = Path.Combine(root, "unity", PrefabManifestAsset.Replace('/', Path.DirectorySeparatorChar));
            string groundPath = Path.Combine(root, "unity", GroundAsset.Replace('/', Path.DirectorySeparatorChar));
            string miniMapPath = Path.Combine(root, "unity", MiniMapAsset.Replace('/', Path.DirectorySeparatorChar));
            var inputs = new[]
            {
                Input(root, PayloadRoot + "/map10.geom.bytes"),
                Input(root, MiniMapGeometryAsset),
                Input(root, MiniMapCatalogAsset)
            };
            if (File.Exists(manifestPath))
            {
                PrefabManifest previous = JsonUtility.FromJson<PrefabManifest>(File.ReadAllText(manifestPath));
                if (previous == null || previous.format != 1 || previous.manifest != "854_156383"
                    || previous.mapClassId != 10 || !File.Exists(groundPath) || !File.Exists(miniMapPath)
                    || previous.inputs == null || previous.inputs.Length != inputs.Length
                    || previous.outputs == null || previous.outputs.Length != 2
                    || previous.inputs[0].sha256 != inputs[0].sha256
                    || previous.inputs[1].sha256 != inputs[1].sha256
                    || previous.inputs[2].sha256 != inputs[2].sha256)
                    throw new InvalidOperationException("Existing Map10 native prefab manifest is incomplete or its inputs changed.");
                foreach (PrefabOutput output in previous.outputs)
                {
                    string fullPath = Path.Combine(root, "unity", output.path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(fullPath) || new FileInfo(fullPath).Length != output.bytes || HashFile(fullPath) != output.sha256)
                        throw new InvalidOperationException("Existing Map10 native prefab differs from its manifest: " + output.path);
                }
                return;
            }

            if (File.Exists(miniMapPath) || File.Exists(miniMapPath + ".meta"))
                throw new InvalidOperationException("Map10 native minimap prefab exists without its source manifest; inspect it before import.");
            string minimapFolder = PayloadRoot + "/MiniMap";
            if (!AssetDatabase.IsValidFolder(minimapFolder)) AssetDatabase.CreateFolder(PayloadRoot, "MiniMap");
            var source = new GameObject("NativeMiniMap_10");
            source.SetActive(false);
            source.AddComponent<OfficialMap10MiniMapView>();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, MiniMapAsset);
            UnityEngine.Object.DestroyImmediate(source);
            if (saved == null) throw new InvalidOperationException("Unity could not create the native Map10 minimap prefab.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var prefabManifest = new PrefabManifest
            {
                format = 1,
                manifest = "854_156383",
                mapClassId = 10,
                unityVersion = Application.unityVersion,
                inputs = inputs,
                outputs = new[] { Output(root, GroundAsset), Output(root, MiniMapAsset) }
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(prefabManifest, true) + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(PrefabManifestAsset, ImportAssetOptions.ForceUpdate);
        }

        private static void BindMap10Catalog()
        {
            const string resources = "Assets/Game/Resources";
            const string worldFolder = resources + "/World";
            if (!AssetDatabase.IsValidFolder(resources)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(worldFolder)) AssetDatabase.CreateFolder(resources, "World");

            var definition = AssetDatabase.LoadAssetAtPath<TextAsset>(PayloadRoot + "/map10.json");
            var sourceManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(PayloadRoot + "/source-manifest.json");
            var ground = AssetDatabase.LoadAssetAtPath<GameObject>(GroundAsset);
            var miniMap = AssetDatabase.LoadAssetAtPath<GameObject>(MiniMapAsset);
            if (definition == null || sourceManifest == null || ground == null || miniMap == null)
                throw new InvalidOperationException("Map10 Resources catalog requires imported definition, manifest, and both prefabs.");

            OfficialWorldMapAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialWorldMapAssetCatalog>(CatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialWorldMapAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAsset);
            }
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            if (entries == null) throw new InvalidOperationException("Official map catalog entries field is missing.");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("ClassId").intValue = 10;
            entry.FindPropertyRelative("GroundPrefab").objectReferenceValue = ground;
            entry.FindPropertyRelative("MiniMapPrefab").objectReferenceValue = miniMap;
            entry.FindPropertyRelative("DefinitionJson").objectReferenceValue = definition;
            entry.FindPropertyRelative("SourceManifestJson").objectReferenceValue = sourceManifest;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateAdjacentPath(OfficialWorldMapSession session)
        {
            OfficialMapNavigation navigation = session.Definition.Navigation;
            var offsets = new[] { new MapGridPoint(0, 0) };
            var directions = new[] { new MapGridPoint(0, -1), new MapGridPoint(-1, 0),
                new MapGridPoint(0, 1), new MapGridPoint(1, 0) };
            for (int y = 0; y < navigation.Height; y++)
            for (int x = 0; x < navigation.Width; x++)
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
                        BodyOffsets = offsets
                    });
                    if (path != null && path.Count >= 2 && path[path.Count - 1].Equals(target)) return;
                }
            }
            throw new InvalidOperationException("Map10 official ground rules did not produce a neighboring-cell route.");
        }

        private static void RequireVisualAsset(GameObject prefab, string expectedName, int expectedBatchCount)
        {
            if (prefab == null || prefab.name != expectedName)
                throw new InvalidOperationException("Map10 native prefab has the wrong identity: " + expectedName);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != expectedBatchCount || Array.Exists(renderers, renderer => renderer.sharedMaterial == null
                || renderer.sharedMaterial.mainTexture == null))
                throw new InvalidOperationException("Map10 native prefab has missing mesh/material/texture bindings: " + expectedName);
        }

        private static PrefabInput Input(string root, string path)
        {
            string fullPath = Path.Combine(root, "unity", path.Replace('/', Path.DirectorySeparatorChar));
            return new PrefabInput { path = path, sha256 = HashFile(fullPath) };
        }

        private static PrefabOutput Output(string root, string path)
        {
            string fullPath = Path.Combine(root, "unity", path.Replace('/', Path.DirectorySeparatorChar));
            return new PrefabOutput { path = path, bytes = new FileInfo(fullPath).Length, sha256 = HashFile(fullPath) };
        }

        private static string HashFile(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
