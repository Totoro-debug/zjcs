using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.Equipment;
using Zjcs.Presentation.Skills;

namespace Zjcs.Editor.Skills
{
    public static class OfficialSkillAssetTools
    {
        public static void BuildWindowsValidation()
        {
            string root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                throw new BuildFailedException("A skills Windows validation requires ZJCS_VALIDATION_DIR.");
            Validate();
            ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                string destination = Path.Combine(root, "Player", "杖剑传说.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                OfficialSkillRuntimeShaderBuild.Prepare();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(),
                    locationPathName = destination, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Skills Windows Player build failed.");
                Debug.Log("[Skills] Windows Player built: " + destination);
            } finally {
                OfficialSkillRuntimeShaderBuild.Restore();
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previous);
            }
        }

        private const string PayloadRoot = "Assets/Official/Skills/";
        private const string ResourcesRoot = "Assets/Game/Resources/Skills";
        private const string CatalogPath = ResourcesRoot + "/OfficialSkillAssetCatalog.asset";
        private static readonly string[] Names =
        {
            "skills.json", "skill-rules.json", "skill-runtime-rules.json",
            "skill-cultivation.json", "skill-ui.json",
            "fidelity-texts.json", "source-manifest.json", "skill-quality-gradient.json"
        };
        private static readonly string[] Fields =
        {
            "skillRecommendations", "skillRules", "runtimeRules", "cultivationRules", "uiLayouts",
            "fidelityTexts", "sourceManifest", "qualityGradient"
        };

        [Serializable]
        private sealed class SourceManifest
        {
            public string manifest;
            public SourceFile[] files;
        }

        [Serializable]
        private sealed class SourceFile
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [MenuItem("Zjcs/Validate/Official Skills Assets")]
        public static void Validate()
        {
            Zjcs.Editor.Home.OfficialHomeAssetTools.BindRuntimeShaders();
            string catalogFolder = "Assets/Game/Resources";
            if (!AssetDatabase.IsValidFolder(catalogFolder)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesRoot)) AssetDatabase.CreateFolder(catalogFolder, "Skills");

            var payloads = new TextAsset[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = PayloadRoot + Names[i];
                payloads[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (payloads[i] == null) throw new BuildFailedException("Skill TextAsset was not imported: " + path);
            }

            string artRoot = PayloadRoot + "Art/";
            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { artRoot.TrimEnd('/') });
            var sprites = new System.Collections.Generic.List<OfficialSkillSpriteBinding>(spriteGuids.Length);
            foreach (string guid in spriteGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                string relative = path.Substring(artRoot.Length).Replace('\\', '/');
                sprites.Add(new OfficialSkillSpriteBinding { sourcePath = relative, sprite = sprite });
            }
            if (sprites.Count == 0) throw new BuildFailedException("No official skill sprites were imported.");
            sprites.Sort((left, right) => string.CompareOrdinal(left.sourcePath, right.sourcePath));

            OfficialSkillAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialSkillAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialSkillAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            for (int i = 0; i < payloads.Length; i++)
            {
                SerializedProperty field = serialized.FindProperty(Fields[i]);
                if (field == null) throw new BuildFailedException("Skill catalog field is missing: " + Fields[i]);
                field.objectReferenceValue = payloads[i];
            }
            SerializedProperty spriteArray = serialized.FindProperty("sprites");
            var nativeFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                "Assets/Official/Skills/NativeText/FZY4K-Dynamic.asset");
            if (nativeFont == null) throw new BuildFailedException("Verified original FZY4K font is missing.");
            serialized.FindProperty("nativeUiFont").objectReferenceValue = nativeFont;
            var nativeCharacterShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Game/Runtime/Official/Character/NativeSkillPreviewDye.shader");
            if (nativeCharacterShader == null || !nativeCharacterShader.isSupported)
                throw new BuildFailedException("Original skill character preview shader did not compile.");
            serialized.FindProperty("nativeCharacterShader").objectReferenceValue = nativeCharacterShader;
            var nativeUrpShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/UniversalRenderPipelineSpineSkeletonDye2D_ffd6b8c6.shader");
            if (nativeUrpShader == null || !nativeUrpShader.isSupported)
                throw new BuildFailedException("Original URP character shader did not compile.");
            serialized.FindProperty("nativeCharacterUrpShader").objectReferenceValue = nativeUrpShader;
            spriteArray.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
            {
                SerializedProperty element = spriteArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("sourcePath").stringValue = sprites[i].sourcePath;
                element.FindPropertyRelative("sprite").objectReferenceValue = sprites[i].sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            string animationPath = "Assets/Official/Dungeons/Prefabs/UI/Skill/SkillContentPanelSkillGroupItem_LevelUp.anim";
            AnimationClip animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationPath);
            if (animation == null)
                throw new BuildFailedException("Official skill slot level-up animation was not imported: " + animationPath);
            OfficialCharacterAssetCatalog characterCatalog =
                Resources.Load<OfficialCharacterAssetCatalog>("Character/OfficialCharacterAssetCatalog");
            if (characterCatalog == null)
                throw new BuildFailedException("Official character asset catalog is missing.");
            var characterSerialized = new SerializedObject(characterCatalog);
            characterSerialized.FindProperty("skillSlotLevelUpAnimation").objectReferenceValue = animation;
            SerializedProperty skillUiLayouts = characterSerialized.FindProperty("skillUiLayouts");
            if (skillUiLayouts == null)
                throw new BuildFailedException("Official character skill UI layout binding is missing.");
            skillUiLayouts.objectReferenceValue = payloads[4];
            string soundPath = "Assets/Official/Skills/Audio/Skill_Upgrade.ogg";
            AudioClip sound = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath);
            if (sound == null)
                throw new BuildFailedException("Official skill slot level-up sound was not imported: " + soundPath);
            characterSerialized.FindProperty("skillSlotLevelUpSound").objectReferenceValue = sound;
            characterSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(characterCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            catalog.ValidateBindings();
            OfficialSkillQualityGradientData qualityGradient = catalog.LoadQualityGradient();
            if (qualityGradient.components.Length != 6)
                throw new BuildFailedException("Official skill quality gradient must bind all six source components.");
            ValidateSourceHashes(payloads[6].text);
            ValidateDerivedSkillPieceMetadata(characterCatalog.Load());
            OfflineSkillRules progressionRules = catalog.LoadProgressionRules();
            if (progressionRules.PlanCount != OfflineSkillProfile.PlanCount)
                throw new BuildFailedException("Official skill plans must match GameSettings.SkillPlanMaxCount.");
            if (progressionRules.Skills.Length != 394)
                throw new BuildFailedException("Official skill progression rules did not load all 394 active and passive skills.");
            var ui = (Dictionary<string, object>)OfficialEquipmentJson.Parse(payloads[4].text);
            var extraIcons = (Dictionary<string, object>)ui["skill_item_icons"];
            var characterData = characterCatalog.Load();
            foreach (var skill in progressionRules.Skills) {
                string uri = characterData.ItemIconUri(skill.ClassId);
                if (string.IsNullOrEmpty(uri) && extraIcons.TryGetValue(skill.ClassId.ToString(), out var extra))
                    uri = Convert.ToString(extra);
                if (characterCatalog.FindSprite(uri) == null && catalog.FindSprite(uri) == null)
                    throw new BuildFailedException("Original skill icon is not bound: " + skill.ClassId);
            }
            if (catalog.FindSprite("res://assets/official/sprites/ImgDot-4ed3349f24.png") == null)
                throw new BuildFailedException("A pinned CharacterPanelSkillPanel sprite binding did not resolve.");
            Debug.Log("Official skill data, quality gradient, 394 progression rules, source hashes and "
                + sprites.Count + " skill UI sprites validated.");
        }

        private static void ValidateDerivedSkillPieceMetadata(OfficialCharacterCatalog characterData)
        {
            int[] missingPieceIds =
            {
                12125, 12126, 12127, 12128, 12129, 12130, 12131, 12132, 12133, 12134,
                12184, 12185, 12186, 12187, 12212, 12213, 12214, 12215
            };
            foreach (int pieceId in missingPieceIds)
            {
                Dictionary<string, object> piece = characterData.FindItem(pieceId);
                Dictionary<string, object> skill = characterData.FindItem(pieceId - 2000);
                if (piece == null || skill == null
                    || Convert.ToString(piece["Name"], System.Globalization.CultureInfo.InvariantCulture)
                        != Convert.ToString(skill["Name"], System.Globalization.CultureInfo.InvariantCulture) + "碎片"
                    || Convert.ToInt32(piece["ItemType"]) != 111
                    || characterData.ItemType(pieceId) != 111
                    || Convert.ToInt32(piece["Rank"]) != -1
                    || Convert.ToInt32(piece["Quality"]) != 1
                    || Convert.ToString(piece["RawIcon"], System.Globalization.CultureInfo.InvariantCulture)
                        != Convert.ToString(skill["RawIcon"], System.Globalization.CultureInfo.InvariantCulture)
                    || characterData.ItemIconUri(pieceId) != characterData.ItemIconUri(pieceId - 2000))
                    throw new BuildFailedException("Official derived skill piece metadata failed validation: " + pieceId);
            }
        }

        private static void ValidateSourceHashes(string json)
        {
            SourceManifest manifest = JsonUtility.FromJson<SourceManifest>(json);
            if (manifest == null || manifest.manifest != "854_156383" || manifest.files == null)
                throw new BuildFailedException("Skill source manifest is not pinned to 854_156383.");
            foreach (SourceFile file in manifest.files)
            {
                string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, file.path.Substring("Assets/".Length)));
                if (!File.Exists(absolute) || new FileInfo(absolute).Length != file.bytes
                    || !string.Equals(Hash(File.ReadAllBytes(absolute)), file.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Skill payload differs from its source manifest: " + file.path);
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
