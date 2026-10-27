using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.Map13Navigation;

namespace Zjcs.Editor.Map13Navigation
{
    public static class OfficialMap13NavigationTools
    {
        public const string CatalogPath = "Assets/Game/Resources/Map13/Native/navigation-catalog.asset";
        private const string MapPath = "Assets/Official/World/Map/13/";
        private const string ProofPath = "assets/reference/official-map/map13-navigation-source-manifest.json";
        [Serializable] private sealed class FileHash { public string path; public string sha256; }
        [Serializable] private sealed class Receipt { public FileHash[] inputs; public FileHash[] outputs; public FileHash[] original_code; public FileHash generator; }
        [Serializable] private sealed class BindingReceipt
        {
            public int mapClassId = 13;
            public string manifest = "871_156598";
            public string source_receipt_sha256;
            public string catalog_path;
            public string catalog_sha256;
            public string catalog_meta_sha256;
            public string water_sha256;
            public string movers_sha256;
        }

        public static string Repository => Directory.Exists(Path.GetFullPath("../scripts")) ? Path.GetFullPath("..") : Path.GetFullPath("../../..");

        public static void RestoreAndValidate()
        {
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Repository, ProofPath)));
            foreach (var file in receipt.inputs.Concat(receipt.outputs).Concat(receipt.original_code).Append(receipt.generator))
                if (Hash(File.ReadAllBytes(Path.Combine(Repository, file.path))) != file.sha256)
                    throw new InvalidDataException("Changed Map13 navigation source/output: " + file.path);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var catalog = AssetDatabase.LoadAssetAtPath<Map13NavigationAssets>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<Map13NavigationAssets>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.DefinitionJson = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath + "map13.json");
            catalog.SourceManifestJson = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath + "source-manifest.json");
            catalog.WaterJson = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath + "Resources/Map13/water-rules.json");
            catalog.MoversJson = AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath + "Resources/Map13/navigation-movers.json");
            catalog.WaterSha256 = Hash(catalog.WaterJson.bytes);
            catalog.MoversSha256 = Hash(catalog.MoversJson.bytes);
            _ = catalog.Open();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (Map13NavigationAssets.LoadDefault() != catalog) throw new InvalidDataException("Map13 navigation resource binding differs.");
            File.WriteAllText(Path.Combine(Repository, "assets/reference/official-map/map13-navigation-binding-source-manifest.json"),
                JsonUtility.ToJson(new BindingReceipt
                {
                    source_receipt_sha256 = Hash(File.ReadAllBytes(Path.Combine(Repository, ProofPath))),
                    catalog_path = "unity/" + CatalogPath,
                    catalog_sha256 = Hash(File.ReadAllBytes(CatalogPath)),
                    catalog_meta_sha256 = Hash(File.ReadAllBytes(CatalogPath + ".meta")),
                    water_sha256 = catalog.WaterSha256, movers_sha256 = catalog.MoversSha256
                }, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 current navigation binding passed: 57600 cells / 2690 placements / original AStar / water occupancy. Mainline entry remains pending.");
        }

        public static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public static void ValidateProject()
        {
            RestoreAndValidate();
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true)
                .GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                .Invoke(null, null);
        }
    }
}
