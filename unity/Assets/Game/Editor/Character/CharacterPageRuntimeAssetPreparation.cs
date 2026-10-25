using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Editor.Character
{
    public static class CharacterPageRuntimeAssetPreparation
    {
        private const string CatalogPath = "Assets/Game/Resources/Character/OfficialCharacterAssetCatalog.asset";
        private const string SourcePanel = "Assets/Official/Character/PetSelector/Prefabs/UI/Pet/PetPanel/PetPlanSelectPanel.prefab";
        private const string SourceItem = "Assets/Official/Character/PetSelector/Prefabs/UI/Pet/PetPanel/PetPlanSelectPanelItem.prefab";
        private const string RuntimeFolder = "Assets/Game/Resources/Character/PetSelector";
        private const string RuntimePanel = RuntimeFolder + "/PetPlanSelectPanel.prefab";
        private const string RuntimeItem = RuntimeFolder + "/PetPlanSelectPanelItem.prefab";
        private const string ConversionVersion = "character-pet-selector-ugui-v2";

        private static readonly System.Collections.Generic.Dictionary<string, string> ScriptGuidMappings =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "18d0a90695249463551c00f45766e642", "4e29b1a8efbd4b44bb3f3716e73f07ff" }, // Button
                { "16c0ec1e295995dd9a2d2df2d7b6cc7e", "31a19414c41e5ae4aae2af33fee712f6" }, // Mask
                { "19fbb4a32b59286cd89b624f52ff7943", "30649d3a9faa99c48a7b1166b86bf2a0" }, // HorizontalLayoutGroup
                { "21c7954052da7655d96bff866c2b7662", "3245ec927659c4140ac4f8d17403cc18" }, // ContentSizeFitter
                { "4293fd42559bc8bb1c81715179adf536", "306cc8c2b49d7114eaa3623786fc2126" }, // LayoutElement
                { "f18f2cc2fe71a3a6d76a570588d5047c", "59f8146938fff824cb5fd77236b75775" }, // VerticalLayoutGroup
                { "3cf5a44414476512e00c3e7a2569a919", "fe87c0e1cc204ed48ad3b37840f39efc" }, // Image
                { "3f96b1d166d19b209697e35b35d65c76", "f4688fdb7df04437aeb418b961361dc5" }, // TextMeshProUGUI
                { "86fe8f3fc59dc06ea6b45a1bbee64682", "dc42784cf147c0c48a680349fa168899" }, // GraphicRaycaster
                { "69dacd7a039c12e90cde90bbae65247a", "3312d7739989d2b4e91e6319e9a96d76" }, // RectMask2D
                { "76ccfb4bfe5eebba5766d6f50f76ae37", "cfabb0440166ab443bba8876756fdfa9" }, // Shadow
                { "c0467acf10a7d1a8e81912a9a7988750", "1aa08ab6e0800fa44ae55d278d1423e3" } // ScrollRect
            };

        [MenuItem("Zjcs/Character/Prepare Runtime Pet Selector")]
        public static void Prepare()
        {
            EnsureFolder();
            CopyOfficialPrefab(SourcePanel, RuntimePanel);
            CopyOfficialPrefab(SourceItem, RuntimeItem);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RemoveMissingScripts(RuntimePanel);
            RemoveMissingScripts(RuntimeItem);
            RecordConversionReceipt(SourcePanel, RuntimePanel);
            RecordConversionReceipt(SourceItem, RuntimeItem);
            AssetDatabase.SaveAssets();

            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePanel);
            GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeItem);
            OfficialCharacterAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialCharacterAssetCatalog>(CatalogPath);
            if (panel == null || item == null || catalog == null)
                throw new InvalidOperationException("The native character pet selector resources did not import.");

            var serialized = new SerializedObject(catalog);
            SerializedProperty panelField = serialized.FindProperty("petPlanSelectorPanel");
            SerializedProperty itemField = serialized.FindProperty("petPlanSelectorItem");
            if (panelField == null || itemField == null)
                throw new InvalidOperationException("The character asset catalog has no pet selector bindings.");
            panelField.objectReferenceValue = panel;
            itemField.objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Character assets] native pet selector bound from exact official prefab copies in Resources.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(RuntimeFolder)
                && AssetDatabase.CreateFolder("Assets/Game/Resources/Character", "PetSelector").Length == 0)
                throw new IOException("Could not create the character pet selector Resources folder.");
        }

        private static void CopyOfficialPrefab(string source, string destination)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException("The pinned official pet selector prefab is missing.", source);

            if (!File.Exists(destination))
            {
                if (!AssetDatabase.CopyAsset(source, destination))
                    throw new IOException("Could not stage the official pet selector prefab: " + source);
            }

            AssetImporter importer = AssetImporter.GetAtPath(destination);
            bool hasCurrentConversion = importer != null && HasCurrentConversion(source, destination, importer.userData);
            if (!hasCurrentConversion)
            {
                byte[] convertedBytes = new UTF8Encoding(false).GetBytes(ConvertScriptGuids(File.ReadAllText(source)));
                if (!File.ReadAllBytes(destination).SequenceEqual(convertedBytes))
                    File.WriteAllBytes(destination, convertedBytes);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);
            }

            importer = AssetImporter.GetAtPath(destination);
            if (importer != null && (!string.IsNullOrEmpty(importer.assetBundleName)
                || !string.IsNullOrEmpty(importer.assetBundleVariant)))
            {
                importer.assetBundleName = string.Empty;
                importer.assetBundleVariant = string.Empty;
                importer.SaveAndReimport();
            }
        }

        private static bool HasCurrentConversion(string source, string destination, string receipt)
        {
            if (!File.Exists(destination)) return false;
            string[] values = (receipt ?? string.Empty).Split('|');
            return values.Length == 3 && values[0] == ConversionVersion
                && values[1] == HashFile(source) && values[2] == HashFile(destination);
        }

        private static void RecordConversionReceipt(string source, string destination)
        {
            AssetImporter importer = AssetImporter.GetAtPath(destination);
            if (importer == null)
                throw new InvalidOperationException("The converted official pet selector importer is missing: " + destination);
            importer.userData = ConversionVersion + "|" + HashFile(source) + "|" + HashFile(destination);
            AssetDatabase.WriteImportSettingsIfDirty(destination);
        }

        private static string HashFile(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
        }

        private static string ConvertScriptGuids(string yaml)
        {
            foreach (var mapping in ScriptGuidMappings)
                yaml = yaml.Replace("guid: " + mapping.Key + ", type: 3",
                    "guid: " + mapping.Value + ", type: 3");
            return yaml;
        }

        private static void RemoveMissingScripts(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int removed = 0;
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
                if (removed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("Could not save the converted pet selector prefab: " + path);
                Debug.Log("[Character assets] converted " + path + "; removed " + removed
                    + " unavailable official helper components.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
