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
    public static class OfficialMap13FlagSceneTools
    {
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Root { public string path; public int instances; }
        [Serializable] private sealed class Source { public Output[] outputs; public Root[] roots; }
        [Serializable] private sealed class Binding
        {
            public int mapClassId = 13, instances = 46;
            public string manifest = "871_156598", source_sha256, importer_sha256;
            public Output[] imported;
            public Output catalog;
        }
        public const string OriginalPath = "Assets/Partition/MapGround_13/Prefabs/2_-5";
        public const string PrefabPath = "Assets/Official/World/Map/13/NativeFlagScene/Partition/MapGround_13/Prefabs/2_-5.prefab";

        public static void ValidateProject()
        {
            OfficialMap13FlagTools.RestoreAndValidate();
            RestoreAndValidate();
            OfficialMap13RenderPipelineTools.ValidateProject();
        }

        public static void RestoreAndValidate()
        {
            string root = OfficialMap13FlagTools.Repository;
            string sourcePath = Path.Combine(root, "assets/reference/official-map/map13-native-flag-scene-source-manifest.json");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
            if (source.roots.Length != 7 || source.roots.Sum(p => p.instances) != 46) throw new InvalidDataException("Original shrine partition dependency count differs.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var record in source.outputs)
            {
                string path = record.path.Substring(6);
                if (Hash(File.ReadAllBytes(path)) != record.sha256) throw new InvalidDataException("Original shrine partition bytes differ: " + path);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset) throw new InvalidDataException("Original shrine partition asset failed to import: " + path);
                var field = new SerializedObject(asset).GetIterator();
                while (field.Next(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue && field.objectReferenceInstanceIDValue != 0)
                        throw new InvalidDataException("Original shrine partition reference is missing: " + path + ":" + field.propertyPath);
            }
            var bindings = source.roots.Select(record =>
            {
                string path = "Assets/Official/World/Map/13/NativeFlagScene/" + record.path.Substring(7);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new InvalidDataException("Original shrine prefab is unavailable: " + path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.GetComponents<MonoBehaviour>().Any(c => !c)) throw new InvalidDataException("Original shrine has a missing script: " + transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        var field = new SerializedObject(component).GetIterator();
                        while (field.Next(true))
                            if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue && field.objectReferenceInstanceIDValue != 0)
                                throw new InvalidDataException("Original shrine component reference is missing: " + path + ":" + transform.name + ":" + field.propertyPath);
                    }
                }
                var flags = prefab.GetComponentsInChildren<NativeMap13FlagMesh>(true);
                if (flags.Length != record.instances) throw new InvalidDataException("Original shrine flag instance count differs: " + path + " actual " + flags.Length + " expected " + record.instances);
                foreach (var flag in flags)
                {
                    var data = flag.skeletonDataAsset;
                    if (!data || flag.GetComponent<Animator>().runtimeAnimatorController != data.Controller ||
                        flag.GetComponent<MeshRenderer>().sharedMaterial != data.Material || !flag.GetComponent<MeshFilter>())
                        throw new InvalidDataException("Original shrine flag data/controller/material is not explicitly bound: " + flag.name);
                }
                return new NativeMap13AssetCatalog.Binding { OfficialPath = record.path.Substring(0, record.path.Length - 7), Asset = prefab };
            }).ToArray();
            const string catalogPath = "Assets/Game/Resources/Map13/Native/native-flag-scene-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            if (Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-flag-scene-catalog") != catalog || bindings.Any(b => catalog.Load<GameObject>(b.OfficialPath) != b.Asset))
                throw new InvalidDataException("Original shrine partition local resource path is not bound.");
            Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var binding = new Binding { imported = source.outputs.Select(p => Record(p.path.Substring(6))).ToArray(), catalog = Record(catalogPath),
                source_sha256 = Hash(File.ReadAllBytes(sourcePath)), importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13/OfficialMap13FlagSceneTools.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-flag-scene-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-flag-scene-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 original shrine partition bound: " + source.outputs.Length + " native assets, seven original prefabs and 46 original flag instances with native data/controller/material references and no missing scripts. Actual scene rendering and animation remain unverified.");
        }
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
