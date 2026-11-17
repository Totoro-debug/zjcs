using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;

namespace Zjcs.Editor.CharacterCreation
{
    public static class CharacterCreationAssetTools
    {
        [Serializable] private sealed class MeshReceipt { public string package_sha256; public long package_bytes; }
        [Serializable]
        private sealed class SourceManifest
        {
            public SourceOutput[] outputs;
        }

        [Serializable]
        private sealed class SourceOutput
        {
            public string name;
            public string sha256;
        }

        [Serializable]
        private sealed class RecoveredUiManifest
        {
            public RecoveredUiOutput[] outputs;
        }

        [Serializable]
        private sealed class RecoveredUiOutput
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        private sealed class SkinIconSourceManifest
        {
            public string apk_sha256;
            public SkinIconSourceBundle source_bundle;
            public int required_option_icon_count;
            public SkinIconSourceOutput[] sprites;
        }

        [Serializable]
        private sealed class SkinIconSourceBundle
        {
            public string name;
            public string origin;
            public string apk_entry;
            public string url;
            public string bundle_md5;
            public string bundle_sha256;
            public long bytes;
        }

        [Serializable]
        private sealed class SkinIconSourceOutput
        {
            public string name;
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class AppearanceGroupIconSourceManifest
        {
            public string apk_sha256;
            public AppearanceGroupIconLocalizationSource source_localization;
            public AppearanceGroupIconSourceBundle[] source_bundles;
            public AppearanceGroupIconSourceOutput[] groups;
        }

        [Serializable]
        private sealed class AppearanceGroupIconLocalizationSource
        {
            public string name;
            public string origin;
            public string apk_sha256;
            public string apk_entry;
            public string url;
            public string bundle_md5;
            public string bundle_sha256;
            public long bytes;
        }

        [Serializable]
        private sealed class AppearanceGroupIconSourceBundle
        {
            public string name;
            public string origin;
            public string bundle_md5;
            public string bundle_sha256;
            public long bytes;
            public string apk_sha256;
            public string apk_entry;
            public string url;
        }

        [Serializable]
        private sealed class AppearanceGroupIconSourceOutput
        {
            public int group_id;
            public string sex;
            public int item_id;
            public string localized_label;
            public string path;
            public long bytes;
            public string sha256;
        }

        private const string PayloadRoot = "Assets/Official/CharacterCreation/";
        private const string RecoveredRoot = PayloadRoot + "Recovered/";
        private const string ResourcesRoot = "Assets/Game/Resources/CharacterCreation";
        private const string CatalogPath = ResourcesRoot + "/OfficialCharacterCreationAssetCatalog.asset";
        private const string OfficialFontPath = "Assets/Game/Resources/Combat/FZY4K-Dynamic.asset";
        private const string AudioRoot = RecoveredRoot + "Audio";
        private const string UiManifestPath = PayloadRoot + "create-player-ui-source-manifest.json";
        private const string ConfirmDialogManifestPath = RecoveredRoot + "ConfirmDialog/source-manifest.json";
        private const string SkinIconManifestPath = RecoveredRoot + "PlayerSkinIcons/source-manifest.json";
        private const string AppearanceGroupIconManifestPath = RecoveredRoot + "AppearanceGroupIcons/source-manifest.json";
        private const string PanelPrefabPath = CharacterCreationUiTools.Root + "/CreatePlayerPanel.prefab";
        private const string DiyPrefabPath = CharacterCreationUiTools.Root + "/CreatePlayerPanelDIYContent.prefab";
        private const string ConfirmDialogPrefabPath = CharacterCreationUiTools.Root + "/ConfirmDialog.prefab";
        private const string SkinIconBundleName = "defaultpackage_assets_image_ui_playerskin.bundle";
        private static readonly string[] Names =
        {
            "creation-performance.json", "appearance-ui.json", "appearance-options.json",
            "wardrobe-rules.json", "wardrobe-ui.json", "wardrobe.json", "source-manifest.json",
            "create-player-ui-source-manifest.json", "appearance-config-rules.json"
        };
        private static readonly string[] Fields =
        {
            "creationPerformance", "appearanceUi", "appearanceOptions", "wardrobeRules",
            "wardrobeUi", "wardrobeItems", "sourceManifest", "createPlayerUiSourceManifest",
            "appearanceConfigRules"
        };
        private static readonly string[] AudioEvents =
        {
            "Main_Page", "Main_Cursor_Normal", "Main_Tag", "Main_Cursor_Back"
        };
        private static readonly string[] AudioFiles =
        {
            "a10d8c4c1a304e6d463c4b98.ogg", "01c46b72b7e3ea6702388df2.ogg",
            "fb1c3dd08471f3c39542a7ca.ogg", "a5a83c8c0d8e9676c907219d.ogg"
        };
        private static readonly string[] AudioHashes =
        {
            "ba4d6d87f162004699a8a897e200a0f016a924c01c18727634d5c5fdd6d2f452",
            "eed64e3606c5095cd89abbfdd87b4bd7afdf22bde70185dd8986cb405e1d920f",
            "6f30e72f1c0a03fc2db06f3c01f3c953582fadbe1fccebee41247759201624de",
            "5fa61bec2bc651dbab59c65b9e04a1bfacc569acce336b7b136a79514b4a1650"
        };

        [MenuItem("Zjcs/Validate/Character Creation Assets")]
        public static void Validate()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string repository = Directory.GetParent(project).FullName;
            MeshReceipt meshReceipt = JsonUtility.FromJson<MeshReceipt>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/character-creation/native-mesh-source-manifest.json")));
            string meshPackage = Path.Combine(Application.streamingAssetsPath, "CharacterCreation/creation-meshes.zip");
            if (!File.Exists(meshPackage) || new FileInfo(meshPackage).Length != meshReceipt.package_bytes
                || Hash(File.ReadAllBytes(meshPackage)) != meshReceipt.package_sha256)
                throw new BuildFailedException("Pinned official creation mesh package is missing or changed.");
            CharacterCreationUiTools.Restore();
            string manifestPath = PayloadRoot + "source-manifest.json";
            TextAsset manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(manifestPath);
            if (manifest == null) throw new BuildFailedException("Character source manifest was not imported.");

            string catalogFolder = "Assets/Game/Resources";
            if (!AssetDatabase.IsValidFolder(catalogFolder)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder(catalogFolder, "CharacterCreation");
            CharacterCreationAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCreationAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCreationAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            Shader creationShader = Shader.Find("Zjcs/CharacterCreation/OfficialMeshFrameDye");
            if (creationShader == null || !creationShader.isSupported)
                throw new BuildFailedException("Native character creation dye shader did not compile.");
            serialized.FindProperty("nativeCreationShader").objectReferenceValue = creationShader;
            SerializedProperty titles = serialized.FindProperty("creationPageTitles");
            titles.arraySize = CharacterCreationUiTools.PageTitles.Length;
            for (int i = 0; i < titles.arraySize; i++) titles.GetArrayElementAtIndex(i).stringValue = CharacterCreationUiTools.PageTitles[i];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = PayloadRoot + Names[i];
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) throw new BuildFailedException("Character TextAsset was not imported: " + path);
                SerializedProperty field = serialized.FindProperty(Fields[i]);
                if (field == null) throw new BuildFailedException("Character catalog field is missing: " + Fields[i]);
                field.objectReferenceValue = asset;
            }
            TextAsset uiManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(UiManifestPath);
            TextAsset confirmDialogManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfirmDialogManifestPath);
            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            GameObject diyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiyPrefabPath);
            GameObject confirmDialogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmDialogPrefabPath);
            TextAsset skinIconManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(SkinIconManifestPath);
            TextAsset appearanceGroupIconManifest = AssetDatabase.LoadAssetAtPath<TextAsset>(AppearanceGroupIconManifestPath);
            if (uiManifest == null || confirmDialogManifest == null || panelPrefab == null || diyPrefab == null
                || confirmDialogPrefab == null || skinIconManifest == null || appearanceGroupIconManifest == null)
                throw new BuildFailedException("Recovered official CreatePlayer prefab dependencies were not imported.");
            serialized.FindProperty("createPlayerPanelPrefab").objectReferenceValue = panelPrefab;
            serialized.FindProperty("createPlayerDiyContentPrefab").objectReferenceValue = diyPrefab;
            serialized.FindProperty("confirmDialogPrefab").objectReferenceValue = confirmDialogPrefab;
            serialized.FindProperty("confirmDialogSourceManifest").objectReferenceValue = confirmDialogManifest;
            serialized.FindProperty("skinIconSourceManifest").objectReferenceValue = skinIconManifest;
            TMP_FontAsset officialFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OfficialFontPath);
            if (officialFont == null || officialFont.sourceFontFile == null)
                throw new BuildFailedException("The fixed-version FZY4K TMP font did not import: " + OfficialFontPath);
            serialized.FindProperty("officialFont").objectReferenceValue = officialFont;
            SerializedProperty audioArray = serialized.FindProperty("audioBindings");
            if (audioArray == null) throw new BuildFailedException("Character audio bindings field is missing.");
            audioArray.arraySize = AudioEvents.Length;
            for (int i = 0; i < AudioEvents.Length; i++)
            {
                string path = AudioRoot + "/" + AudioFiles[i];
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    path.Substring("Assets/".Length)));
                if (clip == null || !File.Exists(absolutePath)
                    || !string.Equals(Hash(File.ReadAllBytes(absolutePath)), AudioHashes[i],
                    StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Pinned official character creation audio is missing or changed: " + path);
                SerializedProperty binding = audioArray.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("eventName").stringValue = AudioEvents[i];
                binding.FindPropertyRelative("clip").objectReferenceValue = clip;
            }
            SkinIconSourceManifest skinIcons = ParseSkinIconManifest(skinIconManifest.text);
            SerializedProperty skinIconArray = serialized.FindProperty("skinIcons");
            if (skinIconArray == null) throw new BuildFailedException("Character skin icon bindings field is missing.");
            skinIconArray.arraySize = skinIcons.sprites.Length;
            for (int i = 0; i < skinIcons.sprites.Length; i++)
            {
                SkinIconSourceOutput output = skinIcons.sprites[i];
                Sprite sprite = LoadSkinIcon(output.path);
                SerializedProperty binding = skinIconArray.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("skinName").stringValue = output.name;
                binding.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
            AppearanceGroupIconSourceManifest groupIcons = ParseAppearanceGroupIconManifest(appearanceGroupIconManifest.text);
            serialized.FindProperty("appearanceGroupIconSourceManifest").objectReferenceValue = appearanceGroupIconManifest;
            SerializedProperty groupIconArray = serialized.FindProperty("appearanceGroupIcons");
            if (groupIconArray == null) throw new BuildFailedException("Character appearance group icon bindings field is missing.");
            groupIconArray.arraySize = groupIcons.groups.Length;
            for (int i = 0; i < groupIcons.groups.Length; i++)
            {
                AppearanceGroupIconSourceOutput output = groupIcons.groups[i];
                SerializedProperty binding = groupIconArray.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("groupId").intValue = output.group_id;
                binding.FindPropertyRelative("sex").intValue = output.sex == "male" ? 0 : 1;
                binding.FindPropertyRelative("sprite").objectReferenceValue = LoadAppearanceGroupIcon(output.path);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            CharacterCreationAssetCatalog.LoadDefault().ValidateBindings();
            ValidateSourceHashes(manifest.text);
            ValidateRecoveredUiHashes(uiManifest.text);
            ValidateRecoveredUiHashes(confirmDialogManifest.text);
            ValidateSkinIconHashes(skinIconManifest.text);
            ValidateAppearanceGroupIconHashes(appearanceGroupIconManifest.text);
            CharacterCreationPerformanceTools.BuildAndValidate(catalog.CreationPerformance);
            Debug.Log("Official character creation catalogs, UI audio, skin icons, appearance group icons, and source hashes validated.");
        }

        private static bool ValidHash(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }

        private static bool ValidSource(string origin, string apkHash, string expectedApkHash,
            string apkEntry, string url)
        {
            if (origin == "apk_zip")
                return ValidHash(apkHash, 64) && string.Equals(apkHash, expectedApkHash,
                    StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(apkEntry);
            return origin == "official_cdn" && Uri.TryCreate(url, UriKind.Absolute, out Uri source)
                && source.Scheme == Uri.UriSchemeHttps;
        }

        private static AppearanceGroupIconSourceManifest ParseAppearanceGroupIconManifest(string json)
        {
            AppearanceGroupIconSourceManifest manifest = JsonUtility.FromJson<AppearanceGroupIconSourceManifest>(json);
            if (manifest == null || manifest.source_localization == null
                || manifest.source_localization.name != "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"
                || !ValidSource(manifest.source_localization.origin, manifest.source_localization.apk_sha256,
                    manifest.apk_sha256, manifest.source_localization.apk_entry, manifest.source_localization.url)
                || !ValidHash(manifest.source_localization.bundle_md5, 32)
                || !ValidHash(manifest.source_localization.bundle_sha256, 64)
                || manifest.source_localization.bytes <= 0
                || manifest.source_bundles == null || manifest.source_bundles.Length == 0
                || manifest.groups == null || manifest.groups.Length == 0)
                throw new BuildFailedException("Official appearance group icon source manifest is invalid.");
            foreach (AppearanceGroupIconSourceBundle bundle in manifest.source_bundles)
            {
                if (bundle == null || !ValidSource(bundle.origin, bundle.apk_sha256, manifest.apk_sha256,
                        bundle.apk_entry, bundle.url) || !ValidHash(bundle.bundle_md5, 32)
                    || !ValidHash(bundle.bundle_sha256, 64) || bundle.bytes <= 0)
                    throw new BuildFailedException("Appearance group icon bundle source is invalid.");
            }
            var groups = new System.Collections.Generic.HashSet<int>();
            foreach (AppearanceGroupIconSourceOutput output in manifest.groups)
                if (output == null || output.path == null || output.sha256 == null
                    || string.IsNullOrEmpty(output.localized_label)
                    || output.group_id <= 0 || (output.sex != "male" && output.sex != "female")
                    || !groups.Add(output.group_id))
                    throw new BuildFailedException("An official appearance group icon record is invalid or duplicated.");
            return manifest;
        }

        private static Sprite LoadAppearanceGroupIcon(string path)
        {
            const string root = RecoveredRoot + "AppearanceGroupIcons/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(root, StringComparison.Ordinal)
                || Path.GetFileNameWithoutExtension(path) == string.Empty)
                throw new BuildFailedException("Official appearance group icon path is invalid: " + path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new BuildFailedException("Official appearance group icon was not imported as a texture: " + path);
            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new BuildFailedException("Official appearance group icon did not import as a Sprite: " + path);
            return sprite;
        }

        private static void ValidateAppearanceGroupIconHashes(string json)
        {
            AppearanceGroupIconSourceManifest manifest = ParseAppearanceGroupIconManifest(json);
            var itemIds = new System.Collections.Generic.HashSet<int>();
            foreach (AppearanceGroupIconSourceOutput output in manifest.groups)
            {
                string name = "item_" + output.item_id;
                if (!itemIds.Add(output.item_id) || Path.GetFileNameWithoutExtension(output.path) != name
                    || !output.path.StartsWith(RecoveredRoot + "AppearanceGroupIcons/", StringComparison.Ordinal))
                    throw new BuildFailedException("An official appearance group icon source record has an invalid item binding.");
                string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    output.path.Substring("Assets/".Length)));
                if (!File.Exists(absolutePath) || new FileInfo(absolutePath).Length != output.bytes
                    || !string.Equals(Hash(File.ReadAllBytes(absolutePath)), output.sha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Official appearance group icon SHA-256 differs from its source manifest: " + output.path);
            }
        }

        private static SkinIconSourceManifest ParseSkinIconManifest(string json)
        {
            SkinIconSourceManifest manifest = JsonUtility.FromJson<SkinIconSourceManifest>(json);
            if (manifest == null
                || manifest.source_bundle == null || manifest.source_bundle.name != SkinIconBundleName
                || !ValidSource(manifest.source_bundle.origin, manifest.apk_sha256, manifest.apk_sha256,
                    manifest.source_bundle.apk_entry, manifest.source_bundle.url)
                || !ValidHash(manifest.source_bundle.bundle_md5, 32)
                || !ValidHash(manifest.source_bundle.bundle_sha256, 64) || manifest.source_bundle.bytes <= 0
                || manifest.required_option_icon_count <= 0 || manifest.sprites == null
                || manifest.sprites.Length < manifest.required_option_icon_count)
                throw new BuildFailedException("Official player skin icon source manifest is invalid.");
            return manifest;
        }

        private static Sprite LoadSkinIcon(string path)
        {
            if (string.IsNullOrEmpty(path)
                || !path.StartsWith(RecoveredRoot + "PlayerSkinIcons/", StringComparison.Ordinal)
                || Path.GetFileNameWithoutExtension(path) == string.Empty)
                throw new BuildFailedException("Official player skin icon path is invalid: " + path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new BuildFailedException("Official player skin icon was not imported as a texture: " + path);
            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new BuildFailedException("Official player skin icon did not import as a Sprite: " + path);
            return sprite;
        }

        private static void ValidateSkinIconHashes(string json)
        {
            SkinIconSourceManifest manifest = ParseSkinIconManifest(json);
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SkinIconSourceOutput output in manifest.sprites)
            {
                if (output == null || string.IsNullOrEmpty(output.name) || string.IsNullOrEmpty(output.path)
                    || string.IsNullOrEmpty(output.sha256) || !names.Add(output.name)
                    || Path.GetFileNameWithoutExtension(output.path) != output.name
                    || !output.path.StartsWith(RecoveredRoot + "PlayerSkinIcons/", StringComparison.Ordinal))
                    throw new BuildFailedException("An official player skin icon source record is invalid.");
                string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    output.path.Substring("Assets/".Length)));
                if (!File.Exists(absolutePath) || new FileInfo(absolutePath).Length != output.bytes
                    || !string.Equals(Hash(File.ReadAllBytes(absolutePath)), output.sha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Official player skin icon SHA-256 differs from its source manifest: " + output.path);
            }
        }

        [MenuItem("Zjcs/Character Creation/Build and Validate")]
        public static void BuildAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Validate();
            CharacterCreationNativeModelTools.Bake();
            Validate();
        }

        private static void ValidateRecoveredUiHashes(string json)
        {
            RecoveredUiManifest manifest = JsonUtility.FromJson<RecoveredUiManifest>(json);
            if (manifest == null || manifest.outputs == null
                || manifest.outputs.Length == 0)
                throw new BuildFailedException("Recovered CreatePlayer UI source manifest is invalid.");
            foreach (RecoveredUiOutput output in manifest.outputs)
            {
                if (output == null || string.IsNullOrEmpty(output.path) || string.IsNullOrEmpty(output.sha256)
                    || !output.path.StartsWith("Assets/Official/CharacterCreation/Recovered/", StringComparison.Ordinal))
                    throw new BuildFailedException("Recovered CreatePlayer UI source path is invalid.");
                string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    output.path.Substring("Assets/".Length)));
                if (!File.Exists(absolutePath)
                    || !string.Equals(Hash(File.ReadAllBytes(absolutePath)), output.sha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Recovered CreatePlayer UI payload SHA-256 differs from its source manifest: " + output.path);
            }
        }

        private static void ValidateSourceHashes(string json)
        {
            SourceManifest manifest = JsonUtility.FromJson<SourceManifest>(json);
            if (manifest == null || manifest.outputs == null)
                throw new BuildFailedException("Character source manifest is invalid.");
            foreach (SourceOutput output in manifest.outputs)
            {
                string path = PayloadRoot + output.name;
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)));
                if (asset == null || !File.Exists(absolutePath)
                    || !string.Equals(Hash(File.ReadAllBytes(absolutePath)), output.sha256,
                    StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Character payload SHA-256 differs from its source manifest: " + path);
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }
    }
}
