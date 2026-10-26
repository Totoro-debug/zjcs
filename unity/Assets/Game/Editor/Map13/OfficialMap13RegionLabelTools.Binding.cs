using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;
namespace Zjcs.Editor.Map13
{
    public static partial class OfficialMap13RegionLabelTools
    {
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Root { public string path, native_path; }
        [Serializable] private sealed class Source { public Output[] outputs; public Root[] roots; }
        [Serializable] private sealed class Binding
        {
            public int mapClassId = 13, prefabs = 88;
            public string manifest = "871_156598", source_sha256, importer_sha256;
            public string component_code_source_sha256;
            public Output[] imported;
            public Output[] component_sources;
            public Output catalog;
        }
        private static string Repository => OfficialMap13FlagTools.Repository;
        public static void ValidateProject()
        {
            RestoreAndValidate();
            var tools = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Zjcs.Editor.ProjectTools", false)).FirstOrDefault(t => t != null);
            if (tools == null) throw new InvalidDataException("Global project validator is unavailable.");
            tools.GetMethod("Validate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
        }
        public static void RestoreAndValidate()
        {
            string root = Repository;
            string sourcePath = Path.Combine(root, "assets/reference/official-map/map13-native-region-labels-source-manifest.json");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
            string codePath = Path.Combine(root, "assets/reference/official-map/map13-label-components-code-source-manifest.json");
            var codeSource = JsonUtility.FromJson<Source>(File.ReadAllText(codePath));
            foreach (var output in codeSource.outputs)
                if (Hash(File.ReadAllBytes(Path.Combine(root, output.path))) != output.sha256 || Hash(File.ReadAllBytes(Path.Combine(root, output.path + ".meta"))) != output.meta_sha256)
                    throw new InvalidDataException("Migrated original label code/config bytes differ: " + output.path);
            if (source.roots.Length != 88) throw new InvalidDataException("Original region-label partition dependency count differs.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string colorPath = "Assets/Game/Resources/Map13/Native/combat-rating-config.asset";
            var colors = AssetDatabase.LoadAssetAtPath<Zjcs.Official.World.NativeMap13Labels.CombatRatingConfig>(colorPath);
            if (!colors)
            {
                colors = ScriptableObject.CreateInstance<Zjcs.Official.World.NativeMap13Labels.CombatRatingConfig>();
                AssetDatabase.CreateAsset(colors, colorPath);
            }
            JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Official/World/Map/13/Source/native-label-combat-colors.json"), colors);
            EditorUtility.SetDirty(colors);
            foreach (var record in source.outputs)
            {
                string path = record.path.Substring(6);
                if (Hash(File.ReadAllBytes(path)) != record.sha256) throw new InvalidDataException("Original region-label partition bytes differ: " + path);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset) throw new InvalidDataException("Original region-label partition asset failed to import: " + path);
                var field = new SerializedObject(asset).GetIterator();
                while (field.Next(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue && field.objectReferenceInstanceIDValue != 0)
                        throw new InvalidDataException("Original region-label partition reference is missing: " + path + ":" + field.propertyPath);
            }
            var bindings = source.roots.Select(record =>
            {
                string path = record.native_path.Substring(6);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new InvalidDataException("Original region-label prefab is unavailable: " + path);
                if (PrefabUtility.HasManagedReferencesWithMissingTypes(prefab)) throw new InvalidDataException("Original label managed-reference type is missing: " + path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.GetComponents<MonoBehaviour>().Any(c => !c)) throw new InvalidDataException("Original region-label has a missing script: " + transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        var field = new SerializedObject(component).GetIterator();
                        while (field.Next(true))
                            if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue && field.objectReferenceInstanceIDValue != 0)
                                throw new InvalidDataException("Original region-label component reference is missing: " + path + ":" + transform.name + ":" + field.propertyPath);
                    }
                }
                return new NativeMap13AssetCatalog.Binding { OfficialPath = record.path.Substring(0, record.path.Length - 7), Asset = prefab };
            }).ToArray();
            const string catalogPath = "Assets/Game/Resources/Map13/Native/native-region-labels-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            if (Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-region-labels-catalog") != catalog || bindings.Any(b => catalog.Load<GameObject>(b.OfficialPath) != b.Asset))
                throw new InvalidDataException("Original region-label partition local resource path is not bound.");
            Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var binding = new Binding { imported = source.outputs.Select(p => Record(p.path.Substring(6))).Concat(new[] { Record(colorPath) }).ToArray(), catalog = Record(catalogPath),
                component_code_source_sha256 = Hash(File.ReadAllBytes(codePath)), component_sources = codeSource.outputs,
                source_sha256 = Hash(File.ReadAllBytes(sourcePath)), importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13/OfficialMap13RegionLabelTools.Binding.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-region-labels-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-region-labels-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 original region-label partition bound: " + source.outputs.Length + " native assets and 88 original label partition prefabs with explicit migrated script/material references and no missing scripts. Full-partition execution/rendering are checked separately by OfficialMap13RegionLabelAcceptance.");
        }
        private static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
