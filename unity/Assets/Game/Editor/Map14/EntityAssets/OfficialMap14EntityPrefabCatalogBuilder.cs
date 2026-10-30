using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14EntityAssets;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityPrefabCatalogBuilder
    {
        private const string SourceManifest = "assets/reference/official-map/map14-native-entity-input-source-manifest.json";
        private const string CatalogPath = "Assets/Game/Resources/Map14/Native/entity-prefab-catalog.asset";
        private const string Root = "Assets/Official/World/Map/14/NativeEntityPrefabs/Prefabs";

        public static void Build()
        {
            var repository = Directory.GetParent(Application.dataPath).Parent.FullName;
            var sourcePath = Path.Combine(repository, SourceManifest.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath)) throw new InvalidOperationException("Map14 entity source manifest is absent: " + sourcePath);
            var selection = JsonUtility.FromJson<Selection>(File.ReadAllText(sourcePath));
            if (selection == null || selection.manifest != "871_156598" || selection.selection == null)
                throw new InvalidDataException("Map14 entity source manifest is malformed.");

            var entries = new List<NativeMap14EntityPrefabCatalog.Entry>();
            foreach (var prefab in selection.selection.prefabs)
            {
                const string prefix = "Assets/Prefabs/";
                if (!prefab.path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Map14 prefab path is outside the original prefab root: " + prefab.path);
                var relative = prefab.path.Substring(prefix.Length);
                var assetPath = Root + "/" + relative;
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (!loaded) throw new InvalidDataException("Map14 entity prefab did not import: " + assetPath);
                var classUse = prefab.uses?.FirstOrDefault(use => use != null && use.kind == "ec_entity");
                entries.Add(new NativeMap14EntityPrefabCatalog.Entry
                {
                    OriginalPath = prefab.path,
                    ClassId = classUse?.class_id ?? 0,
                    Prefab = loaded
                });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap14EntityPrefabCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<NativeMap14EntityPrefabCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Manifest = selection.manifest;
            catalog.Entries = entries.ToArray();
            catalog.ResetLookup();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Map14 original entity prefab catalog built: " + entries.Count + " roots.");
        }

        [Serializable] private sealed class Selection
        { public string manifest; public PrefabSelection selection; }
        [Serializable] private sealed class PrefabSelection
        { public Prefab[] prefabs; }
        [Serializable] private sealed class Prefab
        { public string path; public Use[] uses; }
        [Serializable] private sealed class Use
        { public string kind; public int class_id; }
    }
}
