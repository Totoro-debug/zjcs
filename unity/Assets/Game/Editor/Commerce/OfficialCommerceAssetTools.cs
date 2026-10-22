using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.TextCore.LowLevel;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using Zjcs.Offline.Commerce;
using Zjcs.Presentation.Commerce;

namespace Zjcs.Editor.Commerce
{
    public static class OfficialCommerceAssetTools
    {
        private const string Payload = "Assets/Official/Commerce/";
        private const string ResourcesRoot = "Assets/Game/Resources/Commerce";
        private const string CatalogPath = ResourcesRoot + "/OfficialCommerceAssetCatalog.asset";
        private const string RuntimePath = "Assets/Game/Resources/Commerce/commerce-runtime.json";
        private const string TmpFontPath = ResourcesRoot + "/FZY4K-Dynamic.asset";

        [MenuItem("Zjcs/Commerce/Import and Validate Catalog")]
        public static void Validate()
        {
            TextAsset runtime = AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimePath);
            TextAsset sourceManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Payload + "source-manifest.json");
            if (runtime == null || sourceManifest == null)
                throw new BuildFailedException("Run scripts/prepare_unity_commerce_catalog.py and import the official commerce payload first.");
            var data = JsonUtility.FromJson<OfficialCommerceCatalogData>(runtime.text);
            if (data == null || data.manifest != "854_156383"
                || data.prefabs == null || data.prefabs.Length < 26
                || data.rules == null || data.rules.Length < 3
                || string.IsNullOrEmpty(data.sourceHashes?.officialUiJson))
                throw new BuildFailedException("The commerce runtime catalog is not from manifest 854_156383.");

            const string spritesRoot = Payload + "Art/sprites";
            const string itemIconsRoot = Payload + "Art/items";
            string[] imageGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { spritesRoot, itemIconsRoot });
            foreach (string imageGuid in imageGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(imageGuid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new BuildFailedException("Commerce image has no texture importer: " + path);
                if (importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { spritesRoot, itemIconsRoot });
            if (guids.Length == 0 || data.items == null || data.items.Length != 169
                || data.audio == null || data.audio.Length != 10
                || data.prefabs == null || data.prefabs.Length < 26
                || data.rules == null || data.rules.Length < 3)
                throw new BuildFailedException("The official commerce sprite, item or audio payload is incomplete.");
            var bindings = new CommerceSpriteBinding[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new BuildFailedException("Commerce sprite did not import: " + path);
                bindings[i] = new CommerceSpriteBinding { sourcePath = Path.GetFileName(path), sprite = sprite };
            }

            const string audioRoot = Payload + "Art/audio";
            var audioBindings = new CommerceAudioBinding[data.audio.Length];
            for (int i = 0; i < data.audio.Length; i++)
            {
                string path = audioRoot + "/" + data.audio[i].file;
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) throw new BuildFailedException("Commerce audio did not import: " + path);
                audioBindings[i] = new CommerceAudioBinding { eventName = data.audio[i].eventName, clip = clip };
            }
            Font chineseFont = AssetDatabase.LoadAssetAtPath<Font>(Payload + "Art/fonts/FZY4K.ttf");
            if (chineseFont == null) throw new BuildFailedException("The official FZY4K font did not import.");
            TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (tmpFont == null)
            {
                if (Shader.Find("TextMeshPro/Distance Field") == null)
                    throw new BuildFailedException("Import TMP Essential Resources before generating the commerce font asset.");
                tmpFont = TMP_FontAsset.CreateFontAsset(chineseFont, 40, 4, GlyphRenderMode.SDFAA,
                    2048, 2048, AtlasPopulationMode.Dynamic, true);
                tmpFont.name = "FZY4K SDF - dynamic";
                tmpFont.italicStyle = 15;
                tmpFont.tabSize = 10;
                AssetDatabase.CreateAsset(tmpFont, TmpFontPath);
                if (tmpFont.material != null) AssetDatabase.AddObjectToAsset(tmpFont.material, tmpFont);
                foreach (Texture2D atlas in tmpFont.atlasTextures)
                    if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, tmpFont);
            }

            EnsureFolders();
            OfficialCommerceAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialCommerceAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialCommerceAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("runtimeCatalog").objectReferenceValue = runtime;
            serialized.FindProperty("sourceManifest").objectReferenceValue = sourceManifest;
            serialized.FindProperty("chineseFont").objectReferenceValue = chineseFont;
            serialized.FindProperty("chineseTmpFont").objectReferenceValue = tmpFont;
            SerializedProperty spriteArray = serialized.FindProperty("sprites");
            spriteArray.arraySize = bindings.Length;
            for (int i = 0; i < bindings.Length; i++)
            {
                SerializedProperty entry = spriteArray.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("sourcePath").stringValue = bindings[i].sourcePath;
                entry.FindPropertyRelative("sprite").objectReferenceValue = bindings[i].sprite;
            }
            SerializedProperty audioArray = serialized.FindProperty("audio");
            audioArray.arraySize = audioBindings.Length;
            for (int i = 0; i < audioBindings.Length; i++)
            {
                SerializedProperty entry = audioArray.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("eventName").stringValue = audioBindings[i].eventName;
                entry.FindPropertyRelative("clip").objectReferenceValue = audioBindings[i].clip;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (catalog.Load().weeklyGifts.Length != 595)
                throw new BuildFailedException("The official weekly gift catalog failed validation.");
            Debug.Log("Commerce catalog imported: " + bindings.Length + " official image sprites, " + data.items.Length
                + " reward items, 10 audio events, 6 direct-pay tiers, 12 daily gifts, 595 weekly gifts, 3 first-pay days, and 75 accumulated-pay levels.");
        }

        [MenuItem("Zjcs/Commerce/Import TMP Essential Resources")]
        public static void ImportTmpEssentialResources()
        {
            if (Shader.Find("TextMeshPro/Distance Field") == null)
            {
                UnityEditor.PackageManager.PackageInfo package = null;
                foreach (UnityEditor.PackageManager.PackageInfo candidate
                    in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
                    if (candidate.name == "com.unity.textmeshpro")
                    {
                        package = candidate;
                        break;
                    }
                string packagePath = package == null ? null
                    : Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
                    throw new BuildFailedException("TextMeshPro package resources are not available in the resolved package cache.");
                AssetDatabase.ImportPackage(packagePath, false);
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Resources")) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Commerce");
        }
    }
}
