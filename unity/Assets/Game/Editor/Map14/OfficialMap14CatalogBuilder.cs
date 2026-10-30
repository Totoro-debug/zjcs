using System;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14CatalogBuilder
    {
        private const string CatalogPath = "Assets/Game/Resources/World/OfficialWorldMapAssetCatalog.asset";
        private const string DefinitionPath = "Assets/Official/World/Map/14/map14.json";
        private const string ManifestPath = "Assets/Official/World/Map/14/source-manifest.json";

        public static void EnsureBinding()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficialWorldMapAssetCatalog>(CatalogPath);
            if (!catalog) throw new InvalidOperationException("Official world map catalog is missing.");
            var definition = AssetDatabase.LoadAssetAtPath<TextAsset>(DefinitionPath);
            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (!definition || !manifest) throw new InvalidOperationException("Map14 definition or source manifest is missing.");

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            int index = -1;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("ClassId").intValue == 14) index = i;
            if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("ClassId").intValue = 14;
            entry.FindPropertyRelative("GroundPrefab").objectReferenceValue = null;
            entry.FindPropertyRelative("MiniMapPrefab").objectReferenceValue = null;
            entry.FindPropertyRelative("EntityPrefab").objectReferenceValue = null;
            entry.FindPropertyRelative("DefinitionJson").objectReferenceValue = definition;
            entry.FindPropertyRelative("SourceManifestJson").objectReferenceValue = manifest;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Official Map14 resource binding installed from APK-exported definition and source manifest.");
        }
    }
}
