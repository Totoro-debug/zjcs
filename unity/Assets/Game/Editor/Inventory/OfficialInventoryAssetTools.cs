using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Presentation.Inventory;

namespace Zjcs.Editor.Inventory
{
    public static class OfficialInventoryAssetTools
    {
        private const string Payload = "Assets/Official/Inventory/";
        private const string ResourcesRoot = "Assets/Game/Resources/Inventory";
        private const string CatalogPath = ResourcesRoot + "/OfficialInventoryAssetCatalog.asset";
        [Serializable] private sealed class NativeBindings { public string manifest, asset_hash_encoding; public NativeBinding[] aliases; }
        [Serializable] private sealed class NativeBinding { public string alias, path, guid, sha256; }

        [MenuItem("Zjcs/Inventory/Import and Validate Official Assets")]
        public static void ImportAndValidate()
        {
            TextAsset data = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "bag-items.json");
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "source-manifest.json");
            TextAsset itemSourceData = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "item-source-catalog.json");
            TextAsset itemSourceManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "item-source-manifest.json");
            if (data == null || manifest == null || itemSourceData == null || itemSourceManifest == null)
                throw new BuildFailedException("Run scripts/prepare_unity_inventory_assets.py and import the official inventory payload first.");

            string spritesRoot = Payload + "Art/sprites";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            TextAsset nativeMap = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "current-sprites.json");
            NativeBinding[] bindings;
            if (nativeMap != null)
            {
                NativeBindings current = JsonUtility.FromJson<NativeBindings>(nativeMap.text);
                if (string.IsNullOrEmpty(current.manifest) || current.asset_hash_encoding != "utf8-lf" || current.aliases == null)
                    throw new BuildFailedException("The current inventory Sprite binding map is incomplete.");
                bindings = current.aliases;
            }
            else
            {
                ConfigureSpriteImporters(spritesRoot);
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spritesRoot });
                bindings = new NativeBinding[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    bindings[i] = new NativeBinding { alias = Path.GetFileName(path), path = path, guid = guids[i] };
                }
            }
            if (bindings.Length < 1500)
                throw new BuildFailedException("The restored official inventory sprite set is incomplete: " + bindings.Length);

            EnsureFolders();
            OfficialInventoryAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialInventoryAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialInventoryAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("uiData").objectReferenceValue = data;
            serialized.FindProperty("sourceManifest").objectReferenceValue = manifest;
            serialized.FindProperty("itemSourceUiData").objectReferenceValue = itemSourceData;
            serialized.FindProperty("itemSourceManifest").objectReferenceValue = itemSourceManifest;
            SerializedProperty sprites = serialized.FindProperty("sprites");
            sprites.arraySize = bindings.Length;
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bindings.Length; i++)
            {
                NativeBinding binding = bindings[i];
                if (string.IsNullOrEmpty(binding.alias) || !aliases.Add(binding.alias)
                    || AssetDatabase.AssetPathToGUID(binding.path) != binding.guid)
                    throw new BuildFailedException("Inventory Sprite alias or GUID is invalid: " + binding.alias);
                if (!string.IsNullOrEmpty(binding.sha256))
                {
                    using (var hash = SHA256.Create())
                    {
                        byte[] nativeText = Encoding.UTF8.GetBytes(File.ReadAllText(binding.path).Replace("\r\n", "\n"));
                        string actual = BitConverter.ToString(hash.ComputeHash(nativeText)).Replace("-", "").ToLowerInvariant();
                        if (actual != binding.sha256)
                            throw new BuildFailedException("Current inventory native Sprite changed: " + binding.path);
                    }
                }
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(binding.path);
                if (sprite == null) throw new BuildFailedException("Inventory sprite did not import: " + binding.path);
                SerializedProperty entry = sprites.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sourcePath").stringValue = binding.alias;
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssetIfDirty(catalog);
            catalog.ValidateBindings();
            if (catalog.LoadTemplate("bag") == null
                || catalog.LoadRowTemplate("bag_scroll_row_0") == null
                || catalog.LoadRowTemplate("bag", "/PlayerBagPanel/BgPanel1/Content/MiddleGo/ScrollRect ") == null
                || catalog.LoadItemSourceTemplate() == null
                || catalog.FindItemSourceText("ItemSourceType.PreciousChestGacha") != "国度扭蛋获取"
                || catalog.FindItemSourceText("ItemSourceType.ElementChestGacha") != "元素扭蛋获取")
                throw new BuildFailedException("Official inventory panel or scroll row metadata is missing.");
            Debug.Log("Official inventory UI, source rows, row templates and sprite bindings imported and validated.");
        }

        private static void ConfigureSpriteImporters(string spritesRoot)
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { spritesRoot });
            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || (importer.textureType == TextureImporterType.Sprite
                    && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled)) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources")) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Inventory");
        }
    }
}
