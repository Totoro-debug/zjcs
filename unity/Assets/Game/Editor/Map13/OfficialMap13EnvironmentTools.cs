using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World.NativeEnvironment;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13EnvironmentTools
    {
        public const string ConfigPath = "Assets/Game/Resources/Map13/Native/map-env-data.asset";
        [Serializable] private sealed class FileRecord { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class BindingReceipt
        {
            public int mapClassId = 13;
            public string manifest = "871_156598";
            public FileRecord source, output, validator;
        }

        public static void RestoreAndValidate()
        {
            const string source = "Assets/Official/World/Map/13/Source/native-environment-config.json";
            string json = File.ReadAllText(source);
            var expected = ScriptableObject.CreateInstance<MapEnvData>();
            JsonUtility.FromJsonOverwrite(json, expected);
            var actual = AssetDatabase.LoadAssetAtPath<MapEnvData>(ConfigPath);
            if (!actual)
            {
                actual = ScriptableObject.CreateInstance<MapEnvData>();
                actual.name = "MapEnvData";
                AssetDatabase.CreateAsset(actual, ConfigPath);
            }
            JsonUtility.FromJsonOverwrite(json, actual);
            EditorUtility.SetDirty(actual);
            AssetDatabase.SaveAssets();
            if (actual.DisappearDelayTime != expected.DisappearDelayTime || actual.DisappearTime != expected.DisappearTime
                || actual.FadeTime != expected.FadeTime || Resources.Load<MapEnvData>("Map13/Native/map-env-data") != actual)
                throw new InvalidDataException("Original environment configuration binding differs.");
            UnityEngine.Object.DestroyImmediate(expected);
            string project = Path.GetFullPath(".").Replace('\\', '/');
            bool isolated = project.Contains("/.tools/");
            string root = Path.GetFullPath(isolated ? "../../.." : "..");
            FileRecord Record(string path) => new FileRecord {
                path = Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/'),
                sha256 = Hash(path), meta_sha256 = File.Exists(path + ".meta") ? Hash(path + ".meta") : null };
            var receipt = new BindingReceipt { source = Record(source), output = Record(ConfigPath),
                validator = Record("Assets/Game/Editor/Map13/OfficialMap13EnvironmentTools.cs") };
            string receiptPath = Path.Combine(root, isolated
                ? ".tools/map13-native-cache-871_156598/environment-binding-source-manifest.json"
                : "assets/reference/official-map/map13-environment-binding-source-manifest.json");
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true) + "\n", new System.Text.UTF8Encoding(false));
            Debug.Log("Map13 original environment configuration bound: delay 0.5s, disappearance 1s, light fade 2s. Scene rendering remains unverified.");
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
