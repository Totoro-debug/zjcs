using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap12Tools
    {
        private const string Root = "Assets/Official/World/Map/12";

        [MenuItem("Zjcs/World/Import and Validate Map12")]
        public static void ImportAndValidate()
        {
            if (Application.unityVersion != "2022.3.57f1c2")
                throw new InvalidOperationException("Map12 requires Unity 2022.3.57f1c2.");
            AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive
                | ImportAssetOptions.ForceSynchronousImport);
            var imports = JsonUtility.FromJson<TextureImports>(File.ReadAllText(Root + "/texture-imports.json"));
            foreach (var textureEntry in imports.textures)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(textureEntry.path);
                int maximum = Mathf.NextPowerOfTwo(Mathf.Max(textureEntry.width, textureEntry.height));
                if (!importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None
                    && importer.textureCompression == TextureImporterCompression.Uncompressed
                    && importer.maxTextureSize == maximum && importer.filterMode == (FilterMode)textureEntry.filter
                    && importer.wrapModeU == (TextureWrapMode)textureEntry.wrapU && importer.wrapModeV == (TextureWrapMode)textureEntry.wrapV
                    && importer.sRGBTexture == textureEntry.srgb && !importer.alphaIsTransparency) continue;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = maximum;
                importer.filterMode = (FilterMode)textureEntry.filter;
                importer.wrapModeU = (TextureWrapMode)textureEntry.wrapU;
                importer.wrapModeV = (TextureWrapMode)textureEntry.wrapV;
                importer.sRGBTexture = textureEntry.srgb;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }

            BuildView<OfficialMap10TerrainView>("Map12Terrain", view =>
            {
                view.FindProperty("geometry").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/map12-native.geom.bytes");
                view.FindProperty("resourceTexturePrefix").stringValue = "Map12/Textures/";
                view.FindProperty("expectedBatchCount").intValue = 18683;
            });
            BuildView<OfficialMap10MiniMapView>("NativeMiniMap_12", view =>
            {
                view.FindProperty("mapClassId").intValue = 12;
                view.FindProperty("catalogResource").stringValue = "Map12/MiniMap/catalog";
            });
            BuildView<OfficialMap10EntityView>("Map12Entities", view =>
            {
                view.FindProperty("mapClassId").intValue = 12;
                view.FindProperty("catalogResource").stringValue = "Map12/Entities/native-catalog";
            });

            var catalog = AssetDatabase.LoadAssetAtPath<OfficialWorldMapAssetCatalog>(
                "Assets/Game/Resources/World/OfficialWorldMapAssetCatalog.asset");
            if (catalog == null) throw new InvalidDataException("World map catalog is missing.");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            int index = -1;
            for (int candidate = 0; candidate < entries.arraySize; candidate++)
                if (entries.GetArrayElementAtIndex(candidate).FindPropertyRelative("ClassId").intValue == 12)
                {
                    index = candidate;
                    break;
                }
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("ClassId").intValue = 12;
            Bind<GameObject>(entry, "GroundPrefab", "Map12Terrain.prefab");
            Bind<GameObject>(entry, "MiniMapPrefab", "NativeMiniMap_12.prefab");
            Bind<GameObject>(entry, "EntityPrefab", "Map12Entities.prefab");
            Bind<TextAsset>(entry, "DefinitionJson", "map12.json");
            Bind<TextAsset>(entry, "SourceManifestJson", "source-manifest.json");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            ValidateMap();
        }

        [Serializable] private sealed class TextureImports { public TextureImport[] textures; }
        [Serializable] private sealed class TextureImport
        {
            public string path;
            public int width, height, filter, wrapU, wrapV;
            public bool srgb;
        }

        private static void BuildView<T>(string name, Action<SerializedObject> configure) where T : Component
        {
            var root = new GameObject(name);
            try
            {
                var serialized = new SerializedObject(root.AddComponent<T>());
                configure(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab") == null)
                    throw new InvalidOperationException("Cannot save " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Bind<T>(SerializedProperty entry, string field, string file) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + "/" + file);
            if (asset == null) throw new InvalidDataException("Map12 binding is missing: " + file);
            entry.FindPropertyRelative(field).objectReferenceValue = asset;
        }

        public static void ValidateMap()
        {
            var session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), 12);
            var definition = session.Definition;
            if (definition.MaxRegionProgress.Length != 99 || definition.Entities.Length != 2591
                || definition.EntityComponents.Length != 2591 || definition.Navigation.Width != 280
                || definition.Navigation.Height != 210
                || definition.Entities.Count(value => value.EntityId == 6010) != 18
                || definition.Entities.Count(value => value.EntityId == 6011) != 68)
                throw new InvalidDataException("Map12 source definition is incomplete.");
            var source = Resources.Load<TextAsset>("Map12/Entities/native-catalog");
            int count = OfficialMap10EntityView.ValidateCatalog(source, 12,
                out int models, out int batches, out int spines, out int textures);
            if (count != 2591 || models != 139 || spines != 42)
                throw new InvalidDataException("Map12 entity catalog does not match its source.");
            Debug.Log($"Map12 resources passed: 99 regions, {count} entities, {models} displays, "
                + $"{batches} entity batches, {spines} Spine instances, {textures} textures. "
                + "This validates imported payloads, not complete gameplay or visual fidelity.");
        }
    }
}
