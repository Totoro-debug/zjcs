using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.Wishes;

namespace Zjcs.Editor.Wishes
{
    public static class OfficialWishAssetTools
    {
        private const string RuntimePath = "Assets/Game/Resources/Wishes/wishes-runtime.json";
        private const string OutputPath = "Assets/Game/Resources/Wishes/OfficialWishAssetCatalog.asset";
        private const string SpriteRoot = "Assets/Official/Wish/Art/sprites";
        private const string AudioRoot = "Assets/Official/Wish/Performance/audio";
        private const string CommerceCatalogPath = "Assets/Game/Resources/Commerce/OfficialCommerceAssetCatalog.asset";

        [MenuItem("Tools/Wishes/Import Pinned Official Assets")]
        public static void ImportPinnedOfficialAssets()
        {
            TextAsset runtime = AssetDatabase.LoadAssetAtPath<TextAsset>(RuntimePath);
            if (runtime == null) throw new FileNotFoundException("Wish runtime catalog is missing.", RuntimePath);
            WishUiRuntimeCatalog source = JsonUtility.FromJson<WishUiRuntimeCatalog>(runtime.text);
            if (source == null || source.manifest != "854_156383" || source.spriteBindings == null
                || source.spriteFiles == null || source.spriteBindings.Length != source.spriteFiles.Length
                || source.prefabs == null || source.prefabs.Length != 19
                || source.Prefab("gacha_friend") == null || source.Prefab("gacha_ten_reward") == null
                || source.Prefab("gacha_hundred_row") == null || source.Prefab("gacha_award_icon") == null
                || source.resultAnimations == null || source.resultAnimations.starEvents == null
                || source.resultAnimations.starEvents.Length != 20 || source.resultTiming == null
                || source.audio == null || source.audio.Length == 0)
                throw new InvalidDataException("Wish UI catalog does not match the pinned source.");

            var spriteBindings = new List<WishSpriteAssetBinding>(source.spriteBindings.Length);
            foreach (WishSpriteAssetBinding binding in source.spriteBindings)
            {
                string assetPath = SpriteRoot + "/" + binding.file;
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) throw new FileNotFoundException("Wish sprite is missing.", assetPath);
                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                if (binding.pixelsPerUnit > 0 && Math.Abs(importer.spritePixelsPerUnit - binding.pixelsPerUnit) > 0.01f)
                {
                    importer.spritePixelsPerUnit = binding.pixelsPerUnit;
                    changed = true;
                }
                if (binding.border != null && binding.border.Length >= 4)
                {
                    var border = new Vector4(binding.border[0], binding.border[1], binding.border[2], binding.border[3]);
                    if (importer.spriteBorder != border)
                    {
                        importer.spriteBorder = border;
                        changed = true;
                    }
                }
                if (changed) importer.SaveAndReimport();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null) throw new InvalidDataException("Wish sprite did not import as a Sprite: " + assetPath);
                binding.sprite = sprite;
                spriteBindings.Add(binding);
            }

            var audioBindings = new List<WishAudioAssetBinding>(source.audio.Length);
            foreach (WishAudioAssetBinding binding in source.audio)
            {
                string assetPath = AudioRoot + "/" + Path.GetFileName(binding.file);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null) throw new FileNotFoundException("Wish audio event media is missing.", assetPath);
                binding.clip = clip;
                audioBindings.Add(binding);
            }

            OfficialCommerceAssetCatalog commerce = AssetDatabase.LoadAssetAtPath<OfficialCommerceAssetCatalog>(CommerceCatalogPath);
            if (commerce == null) throw new FileNotFoundException("The shared Chinese font catalog is missing.", CommerceCatalogPath);
            OfficialWishAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialWishAssetCatalog>(OutputPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialWishAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, OutputPath);
            }
            catalog.runtimeCatalog = runtime;
            catalog.sprites = spriteBindings.ToArray();
            catalog.audio = audioBindings.ToArray();
            catalog.chineseFont = commerce.ChineseFont;
            catalog.chineseTmpFont = commerce.ChineseTmpFont;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.Load();
            Debug.Log("Imported " + spriteBindings.Count + " official wish sprites and " + audioBindings.Count + " audio clips.");
        }
    }
}
