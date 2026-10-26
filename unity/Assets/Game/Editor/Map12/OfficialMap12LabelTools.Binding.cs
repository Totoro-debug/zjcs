using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;
namespace Zjcs.Editor.Map12
{
    public static partial class OfficialMap12LabelTools
    {
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Root { public string path, native_path; }
        [Serializable] private sealed class Source { public Output[] outputs, adapters; public Root[] roots; }
        [Serializable] private sealed class Binding
        {
            public int mapClassId = 12, prefabs = 80;
            public string manifest = "871_156598", source_sha256, importer_sha256;
            public string component_code_source_sha256;
            public Output[] imported;
            public Output[] component_sources;
            public Output catalog;
        }
        private static string Repository => OfficialMap12EnvironmentSceneTools.Repository;
        public static void RestoreAndValidate()
        {
            string root = Repository;
            string sourcePath = Path.Combine(root, "assets/reference/official-map/map12-native-region-labels-source-manifest.json");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
            string codePath = Path.Combine(root, "assets/reference/official-map/map12-label-components-code-source-manifest.json");
            var codeSource = JsonUtility.FromJson<Source>(File.ReadAllText(codePath));
            var codeOutputs = codeSource.outputs.Concat(codeSource.adapters ?? Array.Empty<Output>()).ToArray();
            foreach (var output in codeOutputs)
                if (Hash(File.ReadAllBytes(Path.Combine(root, output.path))) != output.sha256 || Hash(File.ReadAllBytes(Path.Combine(root, output.path + ".meta"))) != output.meta_sha256)
                    throw new InvalidDataException("Migrated original label code/config bytes differ: " + output.path);
            if (source.roots.Length != 80) throw new InvalidDataException("Original region-label partition dependency count differs.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
            const string catalogPath = "Assets/Game/Resources/Map12/Native/native-region-labels-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            if (Resources.Load<NativeMap13AssetCatalog>("Map12/Native/native-region-labels-catalog") != catalog || bindings.Any(b => catalog.Load<GameObject>(b.OfficialPath) != b.Asset))
                throw new InvalidDataException("Original region-label partition local resource path is not bound.");
            Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var binding = new Binding { imported = source.outputs.Select(p => Record(p.path.Substring(6))).ToArray(), catalog = Record(catalogPath),
                component_code_source_sha256 = Hash(File.ReadAllBytes(codePath)), component_sources = codeOutputs,
                source_sha256 = Hash(File.ReadAllBytes(sourcePath)), importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map12/OfficialMap12LabelTools.Binding.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-flag-scene-binding-source-manifest.json" :
                "assets/reference/official-map/map12-native-region-labels-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map12 original region-label partition bound: " + source.outputs.Length + " native assets and 80 original non-label partition prefabs with explicit migrated script/material references and no missing scripts. Actual full-partition lifecycle and rendering remain unverified.");
        }
        public static void ImportAndValidate()
        {
            OfficialMap12EnvironmentSceneTools.RestoreAndValidate();
            RestoreAndValidate();
            var projectTools = Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true);
            projectTools.GetMethod("Validate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
            Debug.Log("Map12 original labels import and project validation passed.");
        }
        private static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
