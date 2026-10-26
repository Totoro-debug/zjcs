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
    public static class OfficialMap12EnvironmentSceneTools
    {
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Root { public string path, native_path; }
        [Serializable] private sealed class Source { public Output[] outputs; public Root[] roots; }
        [Serializable] private sealed class Binding
        {
            public int mapClassId = 12, prefabs = 187;
            public string manifest = "871_156598", source_sha256, importer_sha256;
            public Output[] imported;
            public Output catalog;
        }
        public static string Repository => Path.GetFullPath(Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? "../../.." : "..");
        [Serializable] private sealed class SharedSource { public Output[] outputs; public string pipeline_catalog, environment_data; }
        public static T SharedAsset<T>(bool pipeline) where T : UnityEngine.Object
        {
            var proof = JsonUtility.FromJson<SharedSource>(File.ReadAllText(Path.Combine(Repository,
                "assets/reference/official-map/map12-shared-environment-source-manifest.json")));
            foreach (var output in proof.outputs)
                if (Hash(File.ReadAllBytes(Path.Combine(Repository, output.path))) != output.sha256)
                    throw new InvalidDataException("Shared original environment source changed: " + output.path);
            string path = (pipeline ? proof.pipeline_catalog : proof.environment_data).Substring(6);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidDataException("Shared original environment binding failed: " + path);
            return asset;
        }
        public static void RestoreAndCompile() { ValidateShader(); RestoreAndValidate(); }

        public static void ValidateProject()
        {
            ValidateShader();
            RestoreAndValidate();

        }

        public static void RestoreAndValidate()
        {
            string root = Repository;
            string sourcePath = Path.Combine(root, "assets/reference/official-map/map12-native-environment-scene-source-manifest.json");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(sourcePath));
            if (source.roots.Length != 187) throw new InvalidDataException("Original environment partition dependency count differs.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var record in source.outputs)
            {
                string path = record.path.Substring(6);
                if (Hash(File.ReadAllBytes(path)) != record.sha256) throw new InvalidDataException("Original environment partition bytes differ: " + path);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset) throw new InvalidDataException("Original environment partition asset failed to import: " + path);
                var field = new SerializedObject(asset).GetIterator();
                while (field.Next(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue && field.objectReferenceInstanceIDValue != 0)
                        throw new InvalidDataException("Original environment partition reference is missing: " + path + ":" + field.propertyPath);
            }
            var bindings = source.roots.Select(record =>
            {
                string path = record.native_path.Substring(6);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new InvalidDataException("Original environment prefab is unavailable: " + path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.GetComponents<MonoBehaviour>().Any(c => !c)) throw new InvalidDataException("Original environment has a missing script: " + transform.name);
                    foreach (var component in transform.GetComponents<Component>())
                    {
                        var field = new SerializedObject(component).GetIterator();
                        while (field.Next(true))
                            if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue && field.objectReferenceInstanceIDValue != 0)
                                throw new InvalidDataException("Original environment component reference is missing: " + path + ":" + transform.name + ":" + field.propertyPath);
                    }
                }
                return new NativeMap13AssetCatalog.Binding { OfficialPath = record.path.Substring(0, record.path.Length - 7), Asset = prefab };
            }).ToArray();
            const string catalogPath = "Assets/Game/Resources/Map12/Native/native-environment-scene-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            if (Resources.Load<NativeMap13AssetCatalog>("Map12/Native/native-environment-scene-catalog") != catalog || bindings.Any(b => catalog.Load<GameObject>(b.OfficialPath) != b.Asset))
                throw new InvalidDataException("Original environment partition local resource path is not bound.");
            Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var binding = new Binding { imported = source.outputs.Select(p => Record(p.path.Substring(6))).ToArray(), catalog = Record(catalogPath),
                source_sha256 = Hash(File.ReadAllBytes(sourcePath)), importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map12/OfficialMap12EnvironmentSceneTools.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-flag-scene-binding-source-manifest.json" :
                "assets/reference/official-map/map12-native-environment-scene-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map12 original environment partition bound: " + source.outputs.Length + " native assets and 187 original non-label partition prefabs with explicit migrated script/material references and no missing scripts. Actual full-partition lifecycle and rendering remain unverified.");
        }
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; }
        [Serializable] private sealed class ShaderInput { public Program[] programs; }
        [Serializable] private sealed class ShaderSource { public Output[] outputs; public ShaderInput[] inputs; }
        public static void ValidateShader()
        {
            var source = JsonUtility.FromJson<ShaderSource>(File.ReadAllText(Path.Combine(Repository,
                "assets/reference/official-map/map12-environment-shader-source-manifest.json")));
            var record = source.outputs.Single();
            string path = record.path.Substring(6);
            if (Hash(File.ReadAllBytes(path)) != record.sha256) throw new InvalidDataException("Original environment shader bytes differ.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            var data = ShaderUtil.GetShaderData(shader);
            foreach (var variant in source.inputs.Single().programs)
                foreach (var stage in new[] { UnityEditor.Rendering.ShaderType.Vertex, UnityEditor.Rendering.ShaderType.Fragment })
                {
                    var compiled = data.GetSubshader(0).GetPass(variant.pass_index).CompileVariant(stage, variant.keywords,
                        UnityEditor.Rendering.ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, UnityEngine.Rendering.GraphicsTier.Tier2, true);
                    if (!compiled.Success) throw new InvalidDataException("Original environment shader Windows compile failed: " + variant.pass_index + ":" + stage + ":" +
                        string.Join("\n", compiled.Messages.Select(m => m.message)));
                }
            Debug.Log("Map12 original lava edge shader passed Windows D3D compilation: 2 variants, both GPU stages.");
        }
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
