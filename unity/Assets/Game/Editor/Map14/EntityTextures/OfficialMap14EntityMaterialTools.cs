using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map14EntityTextures
{
    public static class OfficialMap14EntityMaterialTools
    {
        private const string ReceiptPath = "assets/reference/official-map/map14-native-entity-materials-native-source-manifest.json";
        private const string CatalogPath = "Assets/Game/Resources/Map14/Native/native-entity-materials-catalog.asset";

        [Serializable] private sealed class MaterialRecord { public string cab, path_id, name; public Output output; }
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Receipt { public int mapClassId; public string manifest, generator; public MaterialRecord[] materials; }

        public static void ImportAndValidate()
        {
            var root = new DirectoryInfo(Application.dataPath);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "official-source.json"))) root = root.Parent;
            if (root == null) throw new InvalidDataException("Official Map14 material repository was not found.");
            string repository = root.FullName;
            Receipt source = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(repository, ReceiptPath)));
            if (source.mapClassId != 14 || source.manifest != "871_156598" || source.materials.Length != 2034)
                throw new InvalidDataException("Official Map14 native material closure changed.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var bindings = source.materials.Select(record =>
            {
                string assetPath = record.output.path.Substring("unity/".Length);
                Verify(repository, record.output);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (!material || material.name != record.name || !material.shader || ShaderUtil.ShaderHasError(material.shader))
                    throw new InvalidDataException("Official Map14 material did not import with its converted shader: " + record.cab + ":" + record.path_id);
                return new NativeMap13AssetCatalog.Binding
                {
                    OfficialPath = record.cab + ":Material:" + record.path_id,
                    Asset = material
                };
            }).ToArray();

            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
            if (!catalog)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            if (bindings.Length != 2034 || bindings.Any(item => catalog.Load<Material>(item.OfficialPath) != item.Asset))
                throw new InvalidDataException("Official Map14 materials are not explicitly bound by CAB/PathID.");

            Debug.Log("Map14 native materials imported: " + bindings.Length + " APK CAB/PathID materials, converted original shaders, and explicit local catalog bindings.");
        }

        private static void Verify(string root, Output output)
        {
            if (Hash(File.ReadAllBytes(Path.Combine(root, output.path))) != output.sha256 ||
                Hash(File.ReadAllBytes(Path.Combine(root, output.path + ".meta"))) != output.meta_sha256)
                throw new InvalidDataException("Official Map14 material source hash changed: " + output.path);
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

    }
}
