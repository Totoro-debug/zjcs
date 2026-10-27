using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14PuzzleTools
    {
        public const string JsonPath = "Assets/Official/World/Map/14/Resources/Map14/puzzle-rules.json";
        public const string CatalogPath = "Assets/Game/Resources/Map14/Native/puzzle-catalog.asset";

        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<Map14PuzzleAssets>(CatalogPath);
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<Map14PuzzleAssets>();
                AssetDatabase.CreateAsset(asset, CatalogPath);
            }
            asset.RulesJson = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            asset.RulesSha256 = Hash(JsonPath);
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            var data = asset.Load();
            if (data.Rules.Length != 109 || data.Costs.Length != 12) throw new InvalidDataException("Map14 puzzle dependency closure changed.");
            OfficialMap14PuzzleAcceptance.Validate();
            Invoke("Zjcs.Editor.OfficialMap11PuzzleValidation");
            Invoke("Zjcs.Editor.OfficialMap12PuzzleValidation");
            Invoke("Zjcs.Editor.ProjectTools");
            var proof = new BindingReceipt
            {
                mapClassId = 14, manifest = "871_156598",
                sourceProof = FileRecord("../assets/reference/official-map/map14-puzzles-source-manifest.json"),
                outputs = new[] { FileRecord(JsonPath), FileRecord(CatalogPath), FileRecord("Assets/Game/Runtime/Offline/World/Map14PuzzleAssets.cs"),
                    FileRecord("Assets/Game/Runtime/Offline/World/OfflineMapPuzzleSession.cs"), FileRecord("Assets/Game/Editor/Map14/OfficialMap14PuzzleTools.cs"),
                    FileRecord("Assets/Game/Editor/Map14/OfficialMap14PuzzleAcceptance.cs") }
            };
            File.WriteAllText("../assets/reference/official-map/map14-puzzles-binding-source-manifest.json", JsonUtility.ToJson(proof, true) + "\n");
            Debug.Log("Map14 puzzle local catalog imported: 109 original entities, 12 terminal costs; Map11/Map12 regressions and ProjectTools.Validate passed. UI/payment/disk persistence remain unwired.");
        }

        private static void Invoke(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).Single(t => t != null);
            type.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }
        private static Record FileRecord(string path) => new Record
        {
            path = Path.GetFullPath(path).Substring(Path.GetFullPath("..").Length + 1).Replace('\\', '/'),
            sha256 = Hash(path), meta_sha256 = File.Exists(path + ".meta") ? Hash(path + ".meta") : ""
        };
        private static string Hash(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        [Serializable] private sealed class Record { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class BindingReceipt { public int mapClassId; public string manifest; public Record sourceProof; public Record[] outputs; }
    }
}
