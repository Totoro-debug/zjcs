using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zjcs.Editor.Home
{
    public static class OfficialHomeAssetTools
    {
        [MenuItem("Zjcs/Home/Bind Runtime Shaders")]
        public static void BindRuntimeShaders()
        {
            const string path = "Assets/Game/Resources/Home/RuntimeShaderBindings.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var binding = AssetDatabase.LoadAssetAtPath<Zjcs.Presentation.Home.HomeRuntimeShaderBindings>(path);
            if (binding == null) {
                binding = ScriptableObject.CreateInstance<Zjcs.Presentation.Home.HomeRuntimeShaderBindings>();
                AssetDatabase.CreateAsset(binding, path);
            }
            binding.Effect = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Presentation/World/OfficialMap10PlayableView.shader");
            binding.SkyColorShift = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/UniversalRenderPipeline2DSpriteLitColorShift_8de0c09e.shader");
            binding.SkyDefault = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/2D/Sprite-Lit-Default.shader");
            if (!binding.Effect || !binding.SkyColorShift || !binding.SkyDefault)
                throw new BuildFailedException("Home runtime requires the migrated effect, verified native ColorShift and URP Sprite-Lit shaders.");
            EditorUtility.SetDirty(binding);
            AssetDatabase.SaveAssets();
        }

        private const string PayloadRoot = "Assets/Official/Home";
        private const string ScenePath = PayloadRoot + "/Scenes/Main.unity";
        private const string SourceManifestPath = "assets/reference/home-unity/source-manifest.json";

        [Serializable]
        private sealed class MigrationManifest
        {
            public string module;
            public string source_manifest;
            public MigrationOutput[] outputs;
        }

        [Serializable]
        private sealed class MigrationOutput
        {
            public string path;
            public long bytes;
            public string sha256;
            public string unity_import_sha256;
            public long unity_import_bytes;
            public string guid;
        }

        [MenuItem("Zjcs/Home/Validate Migrated Official Assets")]
        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ValidateMigratedAssets();
        }

        [MenuItem("Zjcs/Home/Validate Imported Official Assets")]
        public static void ValidateMigratedAssets()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string manifestPath = Path.Combine(repositoryRoot, SourceManifestPath);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Run scripts/prepare_unity_home_assets.py after AssetRipper export.", manifestPath);

            MigrationManifest manifest = JsonUtility.FromJson<MigrationManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.module != "city-and-home" || manifest.source_manifest != "854_156383"
                || manifest.outputs == null || manifest.outputs.Length < 100)
                throw new BuildFailedException("The Home Unity migration manifest is missing or incomplete.");

            var expectedGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int prefabCount = 0;
            int materialCount = 0;
            foreach (MigrationOutput output in manifest.outputs)
            {
                if (string.IsNullOrEmpty(output.path) || output.path.Contains("..") || output.path.StartsWith("/"))
                    throw new BuildFailedException("Home migration manifest contains an invalid output path.");
                string assetPath = PayloadRoot + "/" + output.path;
                string physicalPath = Path.Combine(Application.dataPath,
                    assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
                string expectedHash = string.IsNullOrEmpty(output.unity_import_sha256)
                    ? output.sha256 : output.unity_import_sha256;
                long expectedBytes = string.IsNullOrEmpty(output.unity_import_sha256)
                    ? output.bytes : output.unity_import_bytes;
                if (!File.Exists(physicalPath) || new FileInfo(physicalPath).Length != expectedBytes
                    || !string.Equals(Hash(File.ReadAllBytes(physicalPath)), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Migrated Home asset does not match its source manifest: " + assetPath);

                string metaPath = physicalPath + ".meta";
                string guid = ReadGuid(metaPath);
                if (string.IsNullOrEmpty(output.guid) || !string.Equals(guid, output.guid, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Migrated Home asset GUID differs from its source manifest: " + assetPath);
                UnityEngine.Object importedAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (importedAsset == null)
                    throw new BuildFailedException("Unity did not import the migrated Home asset: " + assetPath);
                expectedGuids.Add(output.guid, assetPath);

                if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    ValidatePrefabScripts(assetPath);
                    prefabCount++;
                }
                else if (assetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    Material material = importedAsset as Material;
                    if (material == null || material.shader == null)
                        throw new BuildFailedException("Migrated Home material has no imported shader: " + assetPath);
                    materialCount++;
                }
            }

            RequireScene();
            RequirePrefab("Prefabs/UI/Home/HomeUI.prefab");
            RequirePrefab("Prefabs/UI/Home/Furniture/Bed.prefab");
            RequirePrefab("Prefabs/UI/Home/Furniture/Cart.prefab");
            RequirePrefab("Prefabs/UI/Mail/MailDialog.prefab");
            RequirePrefab("Prefabs/Home/Dynamic/RandomReward/Leave1View.prefab");
            RequirePrefab("Prefabs/Home/Dynamic/Theme/Background/Background.prefab");
            Debug.Log("Migrated Home assets imported: " + manifest.outputs.Length + " output files, "
                + prefabCount + " prefabs with scripts resolved, " + materialCount
                + " materials with shaders resolved, source and GUID hashes verified.");
        }

        private static void ValidatePrefabScripts(string assetPath)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                if (prefab == null || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
                    throw new BuildFailedException("Migrated Home prefab contains a missing script: " + assetPath);
            }
            finally
            {
                if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void RequireScene()
        {
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (asset == null) throw new BuildFailedException("The official main scene was not imported: " + ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject home = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (string.Equals(root.name, "HomeRoot", StringComparison.Ordinal))
                    {
                        home = root;
                        break;
                    }
                }
                if (home == null || home.transform.childCount == 0)
                    throw new BuildFailedException("The official main scene is missing its HomeRoot hierarchy.");
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) > 0)
                        throw new BuildFailedException("The official main scene contains a missing script: " + root.name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RequirePrefab(string relativePath)
        {
            string path = PayloadRoot + "/" + relativePath;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.transform.childCount == 0)
                throw new BuildFailedException("The migrated official Home prefab could not be loaded: " + path);
        }

        private static string ReadGuid(string path)
        {
            if (!File.Exists(path)) return null;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("guid: ", StringComparison.Ordinal))
                    return line.Substring("guid: ".Length).Trim();
            }
            return null;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
