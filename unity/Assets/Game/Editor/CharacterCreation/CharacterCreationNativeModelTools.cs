using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Spine;
using Spine.Unity;
using Spine.Unity.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.CharacterCreation
{
    public static class CharacterCreationNativeModelTools
    {
        [Serializable]
        private sealed class Manifest
        {
            public string manifest;
            public string operation;
            public FileHash[] inputs;
            public FileHash[] outputs;
            public string[] missing_dye_maps;
            public string[] missing_creation_atlas_pages;
            public SexBake[] sexes;
        }

        [Serializable]
        private sealed class FileHash
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class SexBake
        {
            public string sex;
            public int skin_count;
            public int split_animation_count;
            public int source_animation_count;
            public int preview_animation_count;
            public string[] preview_animations;
            public int atlas_page_count;
            public int creation_atlas_page_count;
            public string idle_animation;
            public string run_animation;
            public string prefab;
        }

        private sealed class BakedSex
        {
            public string Sex;
            public string PrefabPath;
            public int SkinCount;
            public int AnimationCount;
            public int SourceAnimationCount;
            public int AtlasPageCount;
            public int CreationAtlasPageCount;
        }

        [Serializable]
        private sealed class ProfessionAtlasManifest
        {
            public string manifest;
            public ProfessionAtlasOutput[] outputs;
        }

        [Serializable]
        private sealed class ProfessionAtlasOutput
        {
            public string path;
            public long bytes;
            public string sha256;
            public string meta_sha256;
        }

        [Serializable]
        private sealed class SplitAnimationIndex
        {
            public string sex;
            public string[] shared_strings;
            public SplitAnimation[] animations;
            public string[] no_split_animations;
        }

        [Serializable]
        private sealed class SplitAnimation
        {
            public string name;
            public string file;
            public string sha256;
        }

        private const string PayloadRoot = "Assets/Official/CharacterCreation/Recovered/NativeCharacterData";
        private const string ModelRoot = "Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered";
        private const string BakeRoot = PayloadRoot + "/Baked";
        private const string ManifestAssetPath = "unity/" + BakeRoot + "/native-model-bake-manifest.json";
        private const string ReferenceManifestPath = "assets/reference/character-creation/native-character-model-bake-source-manifest.json";
        private const string CharacterCreationCatalogPath =
            "unity/Assets/Game/Resources/CharacterCreation/OfficialCharacterCreationAssetCatalog.asset";
        private const string DyeMapRoot = PayloadRoot + "/DyeMaps";
        private const string EmptySkinName = "emptySkin";
        private const int PrimaryWeaponSkinType = 7;
        private static readonly string[] Fields =
        {
            "hairstyle_id", "eyebrow_id", "eye_id", "mouth_id", "makeup_id", "feature_id"
        };
        private static readonly string[] PreviewAnimationNames =
        {
            "home/idle_01", "map/run_01", "home/pugilism_06", "home/pugilism_07",
            "home/pugilism_lianxu_01", "home/pugilism_lianxu_02",
            "home/pugilism_lianxu_03", "home/pugilism_lianxu_04",
            "100/idle_01", "200/idle_01",
            "100/show_01", "100/show_02", "100/show_idle_01", "100/show_idle_02",
            "100/show_01_idle", "100/show_02_idle", "100/show_01_02", "100/show_02_01",
            "200/show_01", "200/show_02", "200/show_idle_01", "200/show_idle_02",
            "200/show_01_idle", "200/show_02_idle", "200/show_01_02", "200/show_02_01"
        };
        private static readonly HashSet<string> PreviewAnimationNameSet =
            new HashSet<string>(PreviewAnimationNames, StringComparer.Ordinal);
        private static readonly HashSet<string> WardrobeTransitionAnimationNames =
            new HashSet<string>(new[]
            {
                "100/show_idle_01", "100/show_idle_02", "100/show_01_idle", "100/show_02_idle",
                "100/show_01_02", "100/show_02_01",
                "200/show_idle_01", "200/show_idle_02", "200/show_01_idle", "200/show_02_idle",
                "200/show_01_02", "200/show_02_01"
            }, StringComparer.Ordinal);

        [MenuItem("Zjcs/Character Creation/Bake Native Model Previews")]
        public static void Bake()
        {
            CharacterCreationAssetCatalog catalog = CharacterCreationAssetCatalog.LoadDefault();
            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            AssetDatabase.Refresh();
            ValidateProfessionAtlasSourceManifest(projectRoot);
            string bakeRootAbsolute = Path.Combine(Application.dataPath, BakeRoot.Substring("Assets/".Length));
            VerifyPriorOutputs(projectRoot, bakeRootAbsolute);
            Directory.CreateDirectory(bakeRootAbsolute);
            AssetDatabase.Refresh();

            var bakes = new List<BakedSex>
            {
                BakeSex("male", catalog, false),
                BakeSex("female", catalog, false)
            };

            var serialized = new SerializedObject(catalog);
            GameObject malePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bakes[0].PrefabPath);
            GameObject femalePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bakes[1].PrefabPath);
            if (malePrefab == null || femalePrefab == null)
                throw new BuildFailedException("Native character preview prefabs were not imported after baking.");
            serialized.FindProperty("maleNativeModelPrefab").objectReferenceValue = malePrefab;
            serialized.FindProperty("femaleNativeModelPrefab").objectReferenceValue = femalePrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteManifests(projectRoot, bakes, catalog);
            CharacterCreationAssetCatalog.LoadDefault().ValidateBindings();
            Debug.Log("Baked official male and female creation models with native animation controllers.");
        }

        [MenuItem("Zjcs/Character Creation/Finalize Existing Native Model Bake")]
        public static void FinalizeExistingBake()
        {
            CharacterCreationAssetCatalog catalog = CharacterCreationAssetCatalog.LoadDefault();
            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            AssetDatabase.Refresh();
            ValidateProfessionAtlasSourceManifest(projectRoot);
            string bakeRootAbsolute = Path.Combine(Application.dataPath, BakeRoot.Substring("Assets/".Length));
            if (File.Exists(Path.Combine(projectRoot, ManifestAssetPath))
                || File.Exists(Path.Combine(projectRoot, ReferenceManifestPath)))
                VerifyPriorOutputs(projectRoot, bakeRootAbsolute);

            var bakes = new List<BakedSex>
            {
                BakeSex("male", catalog, true),
                BakeSex("female", catalog, true)
            };
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("maleNativeModelPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(bakes[0].PrefabPath);
            serialized.FindProperty("femaleNativeModelPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(bakes[1].PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteManifests(projectRoot, bakes, catalog);
            CharacterCreationAssetCatalog.LoadDefault().ValidateBindings();
            Debug.Log("Validated and recorded the existing official male and female native character bakes.");
        }

        private static void ValidateProfessionAtlasSourceManifest(string projectRoot)
        {
            const string payloadRelative = "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels/profession-atlas-source-manifest.json";
            const string referenceRelative = "assets/reference/character-creation/profession-atlas-source-manifest.json";
            string payloadPath = Path.Combine(projectRoot, payloadRelative.Replace('/', Path.DirectorySeparatorChar));
            string referencePath = Path.Combine(projectRoot, referenceRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(payloadPath) || !File.Exists(referencePath))
                throw new BuildFailedException("Official profession atlas source manifests are missing.");
            byte[] payload = File.ReadAllBytes(payloadPath);
            if (!payload.SequenceEqual(File.ReadAllBytes(referencePath)))
                throw new BuildFailedException("Official profession atlas source manifests disagree.");
            ProfessionAtlasManifest manifest = JsonUtility.FromJson<ProfessionAtlasManifest>(Encoding.UTF8.GetString(payload));
            if (manifest == null || manifest.manifest != "854_156383" || manifest.outputs == null
                || manifest.outputs.Length != 12)
                throw new BuildFailedException("Official profession atlas source manifest is invalid.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfessionAtlasOutput output in manifest.outputs)
            {
                string path = output.path ?? string.Empty;
                bool allowed = path.StartsWith("Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered/Male/AppearanceAssets/Character/Model/Male_1/Atlas/High/", StringComparison.Ordinal)
                    || path.StartsWith("Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered/Female/AppearanceAssets/Character/Model/Female_1/Atlas/High/", StringComparison.Ordinal);
                if (!allowed || !seen.Add(path))
                    throw new BuildFailedException("Profession atlas source manifest contains an unexpected or duplicate output: " + path);
                string absolute = Path.Combine(projectRoot, "unity", path.Replace('/', Path.DirectorySeparatorChar));
                string metaPath = absolute + ".meta";
                if (!File.Exists(absolute) || !File.Exists(metaPath)
                    || new FileInfo(absolute).Length != output.bytes
                    || Hash(File.ReadAllBytes(absolute)) != output.sha256
                    || Hash(File.ReadAllBytes(metaPath)) != output.meta_sha256
                    || AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                    throw new BuildFailedException("Profession atlas output failed its source hash or Unity import check: " + path);
            }
        }

        private static BakedSex BakeSex(string sex, CharacterCreationAssetCatalog catalog, bool reuseExistingOutputs)
        {
            bool female = sex == "female";
            string body = female ? "female" : "male";
            string sexFolder = female ? "Female" : "Male";
            string modelFolder = female ? "Female_1" : "Male_1";
            string modelBase = ModelRoot + "/" + sexFolder + "/AppearanceAssets/Character/Model/" + modelFolder;
            string modelName = "character_" + (female ? "Female_1" : "Male_1");
            string skeletonPath = modelBase + "/" + modelName + ".skel.bytes";
            string atlasPath = modelBase + "/" + modelName + ".atlas.txt";
            string atlasTextureRoot = modelBase + "/Atlas/High";
            string commonModelBase = ModelRoot + "/" + sexFolder + "/AppearanceAssets/Character/Model/Common";
            string commonAtlasTextureRoot = commonModelBase + "/Atlas/High";
            string placeholderTextureRoot = modelBase + "/Atlas/Placeholder";
            string commonPlaceholderTextureRoot = commonModelBase + "/Atlas/Placeholder";
            TextAsset skeletonBytes = AssetDatabase.LoadAssetAtPath<TextAsset>(skeletonPath);
            TextAsset atlasText = AssetDatabase.LoadAssetAtPath<TextAsset>(atlasPath);
            if (skeletonBytes == null || atlasText == null)
                throw new BuildFailedException("Pinned Spine model inputs were not imported: " + sex);

            List<CharacterCreationSkinInfo> skinInfos = CreationSkinInfos(catalog, body, female ? 1 : 0);
            var skinInfoByName = skinInfos.ToDictionary(info => info.Name, StringComparer.Ordinal);
            List<Skin> bakeSkins;
            HashSet<string> creationAtlasPages;
            var metadataAtlas = new Atlas(new StringReader(atlasText.text), string.Empty, new NoOpTextureLoader());
            using (var skeletonStream = new MemoryStream(skeletonBytes.bytes))
            {
                SkeletonData metadata = new SkeletonBinary(new AtlasAttachmentLoader(metadataAtlas))
                    .ReadSkeletonData(skeletonStream);
                if (metadata == null || metadata.DefaultSkin == null)
                    throw new BuildFailedException("The official Spine model has no readable skeleton/default skin: " + sex);
                bakeSkins = new List<Skin> { metadata.DefaultSkin };
                foreach (CharacterCreationSkinInfo info in skinInfos)
                {
                    Skin skin = metadata.FindSkin(info.Name);
                    if (skin == null)
                        throw new BuildFailedException("Official default or creation skin is missing from the pinned Spine model: " + body + "/" + info.Name);
                    if (!bakeSkins.Contains(skin)) bakeSkins.Add(skin);
                }
                creationAtlasPages = RequiredAtlasPages(metadata, bakeSkins);
            }

            string outputRoot = BakeRoot + "/Variants/" + sexFolder;
            string outputAbsolute = Path.Combine(Application.dataPath, outputRoot.Substring("Assets/".Length));
            Directory.CreateDirectory(outputAbsolute);
            string materialRoot = BakeRoot + "/Materials/" + sexFolder;
            string materialAbsolute = Path.Combine(Application.dataPath, materialRoot.Substring("Assets/".Length));
            Directory.CreateDirectory(materialAbsolute);

            List<string> pageNames = ReadAtlasPageNames(atlasText.text);
            if (pageNames.Count == 0)
                throw new BuildFailedException("Official Spine atlas has no texture pages: " + sex);
            var pageSet = new HashSet<string>(pageNames, StringComparer.Ordinal);
            string[] unlistedCreationPages = creationAtlasPages.Except(pageSet)
                .OrderBy(page => page, StringComparer.Ordinal).ToArray();
            if (unlistedCreationPages.Length > 0)
                throw new BuildFailedException("Creation skins reference regions outside the pinned Spine atlas: "
                    + sex + "/" + string.Join(", ", unlistedCreationPages));
            var unresolvedCreationPages = creationAtlasPages
                .Where(page => LoadAtlasPage(atlasTextureRoot, commonAtlasTextureRoot, page) == null)
                .OrderBy(page => page, StringComparer.Ordinal).ToArray();
            if (unresolvedCreationPages.Length > 0)
                throw new BuildFailedException("Official atlas pages required by the default and creation skins are missing: "
                    + sex + "/" + string.Join(", ", unresolvedCreationPages));
            Shader shader = Shader.Find("Zjcs/CharacterCreation/OfficialSpineDye");
            if (shader == null)
                throw new BuildFailedException("The native official character dye shader is unavailable.");
            var materials = new Material[pageNames.Count];
            var materialByTexture = new Dictionary<string, Material>(StringComparer.Ordinal);
            var temporaryObjects = new List<UnityEngine.Object>();
            for (int i = 0; i < pageNames.Count; i++)
            {
                string page = pageNames[i];
                Texture2D texture = LoadAtlasPage(atlasTextureRoot, commonAtlasTextureRoot, page);
                bool creationPage = creationAtlasPages.Contains(page);
                if (texture == null)
                {
                    texture = CreateParsePlaceholderTexture(placeholderTextureRoot, commonPlaceholderTextureRoot,
                        page, temporaryObjects);
                }
                Material material;
                if (creationPage)
                {
                    string materialPath = materialRoot + "/" + page + ".mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(shader) { name = page, mainTexture = texture };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    else if (material.shader != shader || material.mainTexture != texture)
                    {
                        throw new BuildFailedException("A generated native character material was modified: " + materialPath);
                    }
                }
                else
                {
                    material = new Material(shader) { name = page, mainTexture = texture };
                    temporaryObjects.Add(material);
                }
                materials[i] = material;
                materialByTexture.Add(page, material);
            }

            SpineAtlasAsset atlasAsset = null;
            SkeletonDataAsset skeletonAsset = null;
            try
            {
                atlasAsset = SpineAtlasAsset.CreateRuntimeInstance(atlasText, materials, true);
                skeletonAsset = SkeletonDataAsset.CreateRuntimeInstance(skeletonBytes, atlasAsset, false);
                SkeletonData data = skeletonAsset.GetSkeletonData(true);
                if (data == null || data.DefaultSkin == null)
                    throw new BuildFailedException("The official Spine model has no readable skeleton/default skin: " + sex);
                Spine.Animation idle = data.FindAnimation("home/idle_01");
                Spine.Animation run = data.FindAnimation("map/run_01");
                if (idle == null || run == null)
                    throw new BuildFailedException("The official creation idle/run animations are missing from the base skeleton: " + sex);
                int animationCount = LoadSplitAnimations(female ? "female" : "male", sexFolder,
                    atlasAsset, skeletonAsset, data);
                int sourceAnimationCount = RetainPreviewAnimations(data);

                bakeSkins.Clear();
                bakeSkins.Add(data.DefaultSkin);
                foreach (CharacterCreationSkinInfo info in skinInfos)
                {
                    Skin skin = data.FindSkin(info.Name);
                    if (skin == null)
                        throw new BuildFailedException("Official default or creation skin is missing from the pinned Spine model: " + body + "/" + info.Name);
                    if (!bakeSkins.Contains(skin)) bakeSkins.Add(skin);
                }

                var skinList = new ExposedList<Skin>();
                foreach (Skin skin in bakeSkins) skinList.Add(skin);
                string bakedSkinPath = SkinPrefabPath(outputRoot, skeletonBytes.name, data.DefaultSkin.Name);
                string controllerPath = outputRoot + "/" + skeletonBytes.name + " Controller.controller";
                string finalPath = BakeRoot + "/Preview/CharacterCreation_" + (female ? "Female" : "Male") + ".prefab";
                if (reuseExistingOutputs)
                {
                    ValidateExistingBake(sex, outputRoot, skeletonBytes.name, finalPath, bakeSkins, data.DefaultSkin);
                }
                else
                {
                    SkeletonBaker.BakeToPrefab(skeletonAsset, skinList, outputRoot, true, true);
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(bakedSkinPath) == null
                        || AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) == null)
                        throw new BuildFailedException("Spine Baker did not create the default model and animation controller: " + sex);
                    ConfigureWardrobeAnimationLoops(controllerPath);

                    Directory.CreateDirectory(Path.Combine(Application.dataPath,
                        (BakeRoot + "/Preview").Substring("Assets/".Length)));
                    BuildCombinedPrefab(sexFolder, outputRoot, skeletonBytes.name, bakedSkinPath, finalPath, data, bakeSkins,
                        materialByTexture, skinInfoByName);
                }
                AssetDatabase.SaveAssets();
                return new BakedSex
                {
                    Sex = sex, PrefabPath = finalPath, SkinCount = bakeSkins.Count,
                    AnimationCount = animationCount, SourceAnimationCount = sourceAnimationCount,
                    AtlasPageCount = pageNames.Count,
                    CreationAtlasPageCount = creationAtlasPages.Count
                };
            }
            finally
            {
                if (skeletonAsset != null) UnityEngine.Object.DestroyImmediate(skeletonAsset);
                if (atlasAsset != null) UnityEngine.Object.DestroyImmediate(atlasAsset);
                foreach (UnityEngine.Object temporaryObject in temporaryObjects)
                    if (temporaryObject != null) UnityEngine.Object.DestroyImmediate(temporaryObject);
            }
        }

        private static void ValidateExistingBake(string sex, string outputRoot, string skeletonAssetName,
            string finalPath, IReadOnlyList<Skin> skins, Skin defaultSkin)
        {
            string controllerPath = outputRoot + "/" + skeletonAssetName + " Controller.controller";
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
                throw new BuildFailedException("Existing native character bake has no animation controller: " + sex);
            foreach (Skin skin in skins)
            {
                string path = SkinPrefabPath(outputRoot, skeletonAssetName, skin.Name);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new BuildFailedException("Existing native character bake is missing skin prefab: " + path);
                Animator animator = prefab.GetComponent<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new BuildFailedException("Existing native character skin has no Animator: " + path);
            }
            string defaultPath = SkinPrefabPath(outputRoot, skeletonAssetName, defaultSkin.Name);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(defaultPath) == null)
                throw new BuildFailedException("Existing native character bake has no default skin: " + sex);
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(finalPath);
            Animator previewAnimator = preview == null ? null : preview.GetComponent<Animator>();
            if (previewAnimator == null || previewAnimator.runtimeAnimatorController == null)
                throw new BuildFailedException("Existing native character preview prefab is missing or has no Animator: " + sex);
            ConfigureWardrobeAnimationLoops(controllerPath);
            ValidateWardrobeAnimationClips(controllerPath);
        }

        private static void ConfigureWardrobeAnimationLoops(string controllerPath)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(controllerPath)
                .OfType<AnimationClip>().ToArray();
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnimationClip clip in clips)
            {
                if (!PreviewAnimationNameSet.Contains(clip.name)) continue;
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                bool loop = !WardrobeTransitionAnimationNames.Contains(clip.name);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                found.Add(clip.name);
            }
            if (found.Count != PreviewAnimationNames.Length)
                throw new BuildFailedException("The native character controller is missing one or more pinned wardrobe animations: " + controllerPath);
        }

        private static void ValidateWardrobeAnimationClips(string controllerPath)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(controllerPath)
                .OfType<AnimationClip>().Where(clip => PreviewAnimationNameSet.Contains(clip.name)).ToArray();
            if (clips.Length != PreviewAnimationNames.Length)
                throw new BuildFailedException("The existing native character controller does not contain the complete pinned preview animation set: " + controllerPath);
            foreach (AnimationClip clip in clips)
            {
                bool expectedLoop = !WardrobeTransitionAnimationNames.Contains(clip.name);
                if (AnimationUtility.GetAnimationClipSettings(clip).loopTime != expectedLoop)
                    throw new BuildFailedException("The existing native character controller has incorrect wardrobe loop settings: " + clip.name);
            }
        }

        private static int LoadSplitAnimations(string sex, string sexFolder, SpineAtlasAsset atlasAsset,
            SkeletonDataAsset skeletonAsset, SkeletonData skeletonData)
        {
            string indexPath = PayloadRoot + "/Animations/" + sexFolder + ".json";
            TextAsset indexAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(indexPath);
            if (indexAsset == null)
                throw new BuildFailedException("Official split animation index is missing: " + indexPath);
            SplitAnimationIndex index = JsonUtility.FromJson<SplitAnimationIndex>(indexAsset.text);
            if (index == null || index.sex != sex || index.shared_strings == null || index.animations == null
                || index.animations.Length == 0 || index.no_split_animations == null)
                throw new BuildFailedException("Official split animation index is invalid: " + indexPath);

            var binary = new SkeletonBinary(atlasAsset.GetAtlas()) { Scale = skeletonAsset.scale };
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            int loaded = 0;
            foreach (SplitAnimation entry in index.animations)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name) || string.IsNullOrEmpty(entry.file)
                    || string.IsNullOrEmpty(entry.sha256) || !seenNames.Add(entry.name))
                    throw new BuildFailedException("Official split animation entry is invalid or duplicated: " + sex);
                if (!PreviewAnimationNameSet.Contains(entry.name)) continue;
                string expectedPrefix = "Animations/" + sexFolder + "/";
                if (!entry.file.StartsWith(expectedPrefix, StringComparison.Ordinal)
                    || entry.file.Contains("..") || entry.file.Contains("\\"))
                    throw new BuildFailedException("Official split animation path is outside its pinned folder: " + entry.file);

                string assetPath = PayloadRoot + "/" + entry.file;
                if (skeletonData.FindAnimation(entry.name) != null) continue;
                string absolutePath = Path.Combine(Application.dataPath,
                    assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolutePath))
                    throw new BuildFailedException("Official split animation bytes are missing: " + assetPath);
                byte[] bytes = File.ReadAllBytes(absolutePath);
                if (!string.Equals(Hash(bytes), entry.sha256, StringComparison.Ordinal))
                    throw new BuildFailedException("Official split animation hash mismatch: " + assetPath);

                Spine.Animation animation = binary.ReadAnimationFromByteArray(bytes, index.shared_strings, skeletonData);
                if (animation == null || animation.Name != entry.name)
                    throw new BuildFailedException("Official split animation decoded with the wrong name: " + entry.name);
                skeletonData.Animations.Add(animation);
                loaded++;
            }
            return loaded;
        }

        private static int RetainPreviewAnimations(SkeletonData skeletonData)
        {
            int sourceAnimationCount = skeletonData.Animations.Count;
            foreach (string animationName in PreviewAnimationNames)
                if (skeletonData.FindAnimation(animationName) == null)
                    throw new BuildFailedException("An official character preview animation is missing after loading: "
                        + animationName);

            for (int i = skeletonData.Animations.Count - 1; i >= 0; i--)
                if (!PreviewAnimationNameSet.Contains(skeletonData.Animations.Items[i].Name))
                    skeletonData.Animations.RemoveAt(i);

            if (skeletonData.Animations.Count != PreviewAnimationNames.Length)
                throw new BuildFailedException("The native character preview animation set is incomplete.");
            return sourceAnimationCount;
        }

        private static void BuildCombinedPrefab(string sexFolder, string outputRoot, string skeletonAssetName,
            string templatePath, string finalPath,
            SkeletonData skeletonData, IReadOnlyList<Skin> skins, IReadOnlyDictionary<string, Material> materials,
            IReadOnlyDictionary<string, CharacterCreationSkinInfo> skinInfoByName)
        {
            GameObject combined = PrefabUtility.LoadPrefabContents(templatePath);
            try
            {
                Transform[] targetSlots = new Transform[skeletonData.Slots.Count];
                for (int i = 0; i < skeletonData.Slots.Count; i++)
                {
                    string name = skeletonData.Slots.Items[i].Name;
                    Transform target = FindDeep(combined.transform, name);
                    if (target == null) throw new BuildFailedException("Baked skeleton slot is missing: " + name);
                    targetSlots[i] = target;
                    for (int child = target.childCount - 1; child >= 0; child--)
                        UnityEngine.Object.DestroyImmediate(target.GetChild(child).gameObject);
                }

                var boneByName = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (BoneData bone in skeletonData.Bones)
                {
                    Transform target = FindDeep(combined.transform, bone.Name, combined.transform);
                    if (target == null) throw new BuildFailedException("Baked skeleton bone is missing: " + bone.Name);
                    boneByName.Add(bone.Name, target);
                }
                var attachmentRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);

                foreach (Skin skin in skins)
                {
                    if (skin != skeletonData.DefaultSkin)
                    {
                        string sourcePath = SkinPrefabPath(outputRoot, skeletonAssetName, skin.Name);
                        if (!skinInfoByName.TryGetValue(skin.Name, out CharacterCreationSkinInfo skinInfo))
                            throw new BuildFailedException("Official Spine skin has no pinned appearance metadata: " + skin.Name);
                        CopySkinVariants(skin.Name, sourcePath, skin, skinInfo, sexFolder, skeletonData,
                            targetSlots, boneByName, materials, attachmentRoots);
                    }
                    else
                    {
                        string sourcePath = SkinPrefabPath(outputRoot, skeletonAssetName, skin.Name);
                        CopySkinVariants("default", sourcePath, skin, null, sexFolder, skeletonData,
                            targetSlots, boneByName, materials, attachmentRoots);
                    }
                }

                foreach (KeyValuePair<string, Transform> pair in attachmentRoots)
                {
                    string slotName = pair.Key.Substring(0, pair.Key.IndexOf('\n'));
                    string attachmentName = pair.Key.Substring(pair.Key.IndexOf('\n') + 1);
                    SlotData slot = skeletonData.FindSlot(slotName);
                    pair.Value.gameObject.SetActive(slot != null && slot.AttachmentName == attachmentName);
                }

                PrefabUtility.SaveAsPrefabAsset(combined, finalPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(combined);
            }
        }

        private static void CopySkinVariants(string variantName, string prefabPath, Skin skin,
            CharacterCreationSkinInfo skinInfo, string sexFolder, SkeletonData skeletonData, Transform[] targetSlots,
            IReadOnlyDictionary<string, Transform> boneByName, IReadOnlyDictionary<string, Material> materials,
            IDictionary<string, Transform> attachmentRoots)
        {
            GameObject sourceRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var sourceSlots = new Transform[skeletonData.Slots.Count];
                for (int i = 0; i < skeletonData.Slots.Count; i++)
                {
                    sourceSlots[i] = FindDeep(sourceRoot.transform, skeletonData.Slots.Items[i].Name);
                    if (sourceSlots[i] == null)
                        throw new BuildFailedException("Skin prefab slot is missing: " + skeletonData.Slots.Items[i].Name);
                }

                var entries = new List<Skin.SkinEntry>();
                for (int slotIndex = 0; slotIndex < skeletonData.Slots.Count; slotIndex++)
                {
                    entries.Clear();
                    skin.GetAttachments(slotIndex, entries);
                    foreach (Skin.SkinEntry entry in entries)
                    {
                        if (!(entry.Attachment is RegionAttachment) && !(entry.Attachment is MeshAttachment)) continue;
                        Transform sourceAttachment = FindDirectChild(sourceSlots[slotIndex], entry.Name);
                        if (sourceAttachment == null)
                            throw new BuildFailedException("Skin prefab attachment is missing: " + skin.Name + "/" + entry.Name);
                        string slotName = skeletonData.Slots.Items[slotIndex].Name;
                        string key = slotName + "\n" + entry.Name;
                        if (!attachmentRoots.TryGetValue(key, out Transform attachmentRoot))
                        {
                            var root = new GameObject(entry.Name);
                            root.transform.SetParent(targetSlots[slotIndex], false);
                            root.layer = targetSlots[slotIndex].gameObject.layer;
                            attachmentRoot = root.transform;
                            attachmentRoots.Add(key, attachmentRoot);
                        }
                        CopyAttachmentVariant(variantName, sourceAttachment, attachmentRoot, boneByName, materials,
                            skinInfo, sexFolder);
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        private static void CopyAttachmentVariant(string variantName, Transform source, Transform parent,
            IReadOnlyDictionary<string, Transform> boneByName, IReadOnlyDictionary<string, Material> materials,
            CharacterCreationSkinInfo skinInfo, string sexFolder)
        {
            var variant = new GameObject("__skin__" + variantName);
            variant.transform.SetParent(parent, false);
            variant.transform.localPosition = source.localPosition;
            variant.transform.localRotation = source.localRotation;
            variant.transform.localScale = source.localScale;
            variant.layer = parent.gameObject.layer;
            variant.SetActive(variantName == "default");

            if (skinInfo != null && skinInfo.HasDye)
            {
                string dyeMapPath = DyeMapPath(sexFolder, skinInfo);
                string absoluteDyeMapPath = Path.Combine(Application.dataPath,
                    dyeMapPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(absoluteDyeMapPath))
                {
                    Texture2D dyeMask = LoadDyeMask(dyeMapPath);
                    if (dyeMask == null)
                        throw new BuildFailedException("Official dye map could not be imported: " + dyeMapPath);
                    variant.AddComponent<CharacterCreationDyeMapBinding>().Bind(skinInfo.Type, dyeMask);
                }
            }

            Renderer sourceRenderer = source.GetComponent<Renderer>();
            if (sourceRenderer == null || sourceRenderer.sharedMaterial == null)
                throw new BuildFailedException("Baked Spine attachment has no renderer/material: " + source.name);
            Texture texture = sourceRenderer.sharedMaterial.mainTexture;
            if (texture == null || !materials.TryGetValue(texture.name, out Material material))
                throw new BuildFailedException("Baked Spine attachment uses an unbound atlas material: " + source.name);

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter != null)
            {
                MeshFilter filter = variant.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = variant.AddComponent<MeshRenderer>();
                CopyRendererSettings(sourceRenderer, renderer);
                renderer.sharedMaterial = material;
                return;
            }

            SkinnedMeshRenderer sourceSkinned = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinned == null || sourceSkinned.sharedMesh == null)
                throw new BuildFailedException("Baked Spine attachment has an unsupported mesh renderer: " + source.name);
            var skinned = variant.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = sourceSkinned.sharedMesh;
            skinned.rootBone = MapBone(sourceSkinned.rootBone, boneByName);
            skinned.bones = sourceSkinned.bones.Select(bone => MapBone(bone, boneByName)).ToArray();
            skinned.updateWhenOffscreen = true;
            CopyRendererSettings(sourceRenderer, skinned);
            skinned.sharedMaterial = material;
        }

        private static Transform MapBone(Transform source, IReadOnlyDictionary<string, Transform> boneByName)
        {
            if (source == null || !boneByName.TryGetValue(source.name, out Transform target))
                throw new BuildFailedException("Baked Spine mesh references an unmapped bone: " + (source == null ? "null" : source.name));
            return target;
        }

        private static void CopyRendererSettings(Renderer source, Renderer target)
        {
            target.enabled = source.enabled;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.receiveShadows = source.receiveShadows;
            target.shadowCastingMode = source.shadowCastingMode;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
        }

        private static List<CharacterCreationSkinInfo> CreationSkinInfos(CharacterCreationAssetCatalog catalog,
            string body, int sex)
        {
            var result = new Dictionary<string, CharacterCreationSkinInfo>(StringComparer.Ordinal);
            foreach (CharacterCreationSkinInfo info in catalog.GetDefaultSkinInfos(sex,
                CharacterCreationOfflineService.DefaultProfession))
                AddSkinInfo(result, info);
            foreach (string field in Fields)
                foreach (CharacterCreationOptionInfo option in catalog.GetAppearanceOptions(body, field))
                {
                    if (!catalog.TryGetSkinInfo(option.SkinId, out CharacterCreationSkinInfo info)
                        || info.Name != option.SkinName || info.Type != option.SkinType)
                        throw new BuildFailedException("Official appearance option metadata disagrees with the skin table: " + option.Id);
                    AddSkinInfo(result, info);
                }
            return result.Values.OrderBy(info => info.Type).ThenBy(info => info.Name, StringComparer.Ordinal).ToList();
        }

        private static HashSet<string> RequiredAtlasPages(SkeletonData data, IReadOnlyList<Skin> skins)
        {
            var pages = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<Skin.SkinEntry>();
            foreach (Skin skin in skins)
            {
                for (int slot = 0; slot < data.Slots.Count; slot++)
                {
                    entries.Clear();
                    skin.GetAttachments(slot, entries);
                    foreach (Skin.SkinEntry entry in entries)
                    {
                        if (entry.Attachment is IHasTextureRegion regionAttachment)
                            AddAtlasPage(regionAttachment.Region, pages);
                        Sequence sequence = entry.Attachment is RegionAttachment region ? region.Sequence
                            : entry.Attachment is MeshAttachment mesh ? mesh.Sequence : null;
                        if (sequence == null) continue;
                        foreach (TextureRegion sequenceRegion in sequence.Regions)
                            AddAtlasPage(sequenceRegion, pages);
                    }
                }
            }
            return pages;
        }

        private static void AddAtlasPage(TextureRegion region, ISet<string> pages)
        {
            if (region is AtlasRegion atlasRegion && atlasRegion.page != null
                && !string.IsNullOrEmpty(atlasRegion.page.name))
            {
                string page = atlasRegion.page.name;
                if (page.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    page = page.Substring(0, page.Length - 4);
                pages.Add(page);
            }
        }

        private static Texture2D LoadAtlasPage(string modelAtlasRoot, string commonAtlasRoot, string page)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(modelAtlasRoot + "/" + page + ".png")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(commonAtlasRoot + "/" + page + ".png");
        }

        private static Texture2D CreateParsePlaceholderTexture(string placeholderRoot,
            string commonPlaceholderRoot, string page, ICollection<UnityEngine.Object> temporaryObjects)
        {
            Texture2D placeholder = AssetDatabase.LoadAssetAtPath<Texture2D>(
                placeholderRoot + "/" + page + "_placeholder.png")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(commonPlaceholderRoot + "/" + page + "_placeholder.png");
            int width = placeholder != null ? placeholder.width : 1;
            int height = placeholder != null ? placeholder.height : 1;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.Apply(false, true);
            texture.name = page;
            texture.hideFlags = HideFlags.HideAndDontSave;
            temporaryObjects.Add(texture);
            return texture;
        }

        private static void AddSkinInfo(IDictionary<string, CharacterCreationSkinInfo> result,
            CharacterCreationSkinInfo info)
        {
            if (info.Name == EmptySkinName || info.Type == PrimaryWeaponSkinType) return;
            if (!result.TryGetValue(info.Name, out CharacterCreationSkinInfo existing))
            {
                result.Add(info.Name, info);
                return;
            }
            if (existing.Type != info.Type || existing.HasDye != info.HasDye)
                throw new BuildFailedException("Official skin name has conflicting dye metadata: " + info.Name);
        }

        private static string DyeMapPath(string sexFolder, CharacterCreationSkinInfo info)
        {
            string directory = info.Type == 4 ? "Common" : sexFolder;
            string fileName = info.Type == 0 ? "face"
                : info.Type == 4 ? info.Name.Replace("_G_", "_")
                : info.Name;
            if (info.Type != 0 && info.Type != 3 && info.Type != 4 && info.Type != 5)
                throw new BuildFailedException("Official dye-enabled skin type has no pinned map rule: " + info.Type);
            return DyeMapRoot + "/" + directory + "/High/" + fileName + ".png";
        }

        private static Texture2D LoadDyeMask(string assetPath)
        {
            string absolutePath = Path.Combine(Application.dataPath,
                assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath)) return null;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return null;
            if (importer.sRGBTexture || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static List<string> ReadAtlasPageNames(string atlas)
        {
            var result = new List<string>();
            foreach (string sourceLine in atlas.Replace("\r", string.Empty).Split('\n'))
            {
                string line = sourceLine.Trim();
                if (!line.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                string name = line.Substring(0, line.Length - 4);
                if (!result.Contains(name, StringComparer.OrdinalIgnoreCase)) result.Add(name);
            }
            return result;
        }

        private static string SkinPrefabPath(string outputRoot, string skeletonName, string skinName)
        {
            return outputRoot + "/" + skeletonName + " (" + skinName + ").prefab";
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        private static Transform FindDeep(Transform root, string name, Transform exclude = null)
        {
            if (root != exclude && root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name, exclude);
                if (found != null) return found;
            }
            return null;
        }

        private static void VerifyPriorOutputs(string projectRoot, string outputRoot)
        {
            string referencePath = Path.Combine(projectRoot, ReferenceManifestPath);
            string payloadPath = Path.Combine(projectRoot, ManifestAssetPath);
            bool hasReference = File.Exists(referencePath);
            bool hasPayload = File.Exists(payloadPath);
            if (hasReference != hasPayload)
                throw new BuildFailedException("Native character bake manifests are incomplete; refusing to overwrite generated assets.");
            if (!hasReference)
            {
                bool hasUntrackedOutputs = Directory.Exists(outputRoot)
                    && Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).Any(path =>
                        !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                        && !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
                if (hasUntrackedOutputs)
                    throw new BuildFailedException("Native character bake output already exists without a source manifest.");
                return;
            }
            byte[] referenceBytes = File.ReadAllBytes(referencePath);
            if (!referenceBytes.SequenceEqual(File.ReadAllBytes(payloadPath)))
                throw new BuildFailedException("Native character bake manifests disagree.");
            Manifest previous = JsonUtility.FromJson<Manifest>(Encoding.UTF8.GetString(referenceBytes));
            if (previous == null || previous.manifest != "854_156383"
                || previous.inputs == null || previous.outputs == null)
                throw new BuildFailedException("Native character bake manifest is not pinned to 854_156383.");
            bool inputsChanged = false;
            foreach (FileHash input in previous.inputs)
            {
                if (input == null || string.IsNullOrEmpty(input.path) || string.IsNullOrEmpty(input.sha256))
                    throw new BuildFailedException("Native character bake manifest contains an invalid input record.");
                string path = Path.Combine(projectRoot, input.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    throw new BuildFailedException("A previous native character bake input is missing: " + input.path);
                if (HashFile(path) != input.sha256)
                    inputsChanged = true;
            }
            foreach (FileHash output in previous.outputs)
            {
                if (string.Equals(output.path, CharacterCreationCatalogPath, StringComparison.Ordinal))
                    continue;
                string path = Path.Combine(projectRoot, output.path.Replace('/', Path.DirectorySeparatorChar));
                if ((!File.Exists(path) || HashFile(path) != output.sha256) && !inputsChanged)
                    throw new BuildFailedException("Generated native character output changed; refusing to overwrite: " + output.path);
            }
        }

        private static void WriteManifests(string projectRoot, IReadOnlyList<BakedSex> bakes,
            CharacterCreationAssetCatalog catalog)
        {
            var manifest = new Manifest
            {
                manifest = "854_156383",
                    operation = "Bake official default and creation skins to native Unity Animator meshes with home, map, Pet Gacha, and wardrobe transition animations; wardrobe transitions play once and target poses loop; bind pinned dye maps and leave officially missing maps untinted.",
                inputs = GetInputs(projectRoot, bakes, catalog),
                outputs = GetOutputs(projectRoot, catalog),
                missing_dye_maps = GetMissingDyeMaps(projectRoot, catalog),
                missing_creation_atlas_pages = Array.Empty<string>(),
                sexes = bakes.Select(bake => new SexBake
                {
                    sex = bake.Sex,
                    skin_count = bake.SkinCount,
                    split_animation_count = bake.AnimationCount,
                    source_animation_count = bake.SourceAnimationCount,
                    preview_animation_count = PreviewAnimationNames.Length,
                    preview_animations = PreviewAnimationNames,
                    atlas_page_count = bake.AtlasPageCount,
                    creation_atlas_page_count = bake.CreationAtlasPageCount,
                    idle_animation = "home/idle_01",
                    run_animation = "map/run_01",
                    prefab = bake.PrefabPath
                }).ToArray()
            };
            string encoded = JsonUtility.ToJson(manifest, true) + "\n";
            string payloadPath = Path.Combine(projectRoot, ManifestAssetPath);
            string referencePath = Path.Combine(projectRoot, ReferenceManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(payloadPath));
            Directory.CreateDirectory(Path.GetDirectoryName(referencePath));
            WriteAtomic(payloadPath, encoded);
            WriteAtomic(referencePath, encoded);
        }

        private static FileHash[] GetInputs(string projectRoot, IReadOnlyList<BakedSex> bakes,
            CharacterCreationAssetCatalog catalog)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                "unity/Assets/Official/CharacterCreation/appearance-options.json",
                "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels/profession-atlas-source-manifest.json",
                "unity/Assets/Official/CharacterCreation/wardrobe-rules.json",
                "unity/Assets/Spine/Runtime/spine-csharp/SkeletonBinary.cs",
                "unity/Assets/Spine/Editor/spine-unity/Editor/Windows/SkeletonBaker.cs",
                "unity/Assets/Game/Runtime/Presentation/CharacterCreation/Spine-SkeletonDye.shader",
                "unity/Assets/Game/Runtime/Presentation/Character/CharacterCreationDyeMapBinding.cs",
                "unity/Assets/Game/Editor/CharacterCreation/CharacterCreationNativeModelTools.cs",
                "assets/characters/wardrobe/ui.json"
            };
            foreach (BakedSex bake in bakes)
            {
                bool female = bake.Sex == "female";
                string sexFolder = female ? "Female" : "Male";
                string modelFolder = female ? "Female_1" : "Male_1";
                string modelRoot = "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered/"
                    + sexFolder + "/AppearanceAssets/Character/Model/" + modelFolder + "/";
                string modelName = "character_" + modelFolder;
                paths.Add(modelRoot + modelName + ".skel.bytes");
                paths.Add(modelRoot + modelName + ".atlas.txt");
                string animationRoot = "unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData/Animations/";
                string animationIndexPath = animationRoot + sexFolder + ".json";
                paths.Add(animationIndexPath);
                TextAsset animationIndexAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Official/CharacterCreation/Recovered/NativeCharacterData/Animations/" + sexFolder + ".json");
                SplitAnimationIndex animationIndex = animationIndexAsset == null ? null
                    : JsonUtility.FromJson<SplitAnimationIndex>(animationIndexAsset.text);
                if (animationIndex == null || animationIndex.animations == null)
                    throw new BuildFailedException("Official character preview animation index is missing: " + animationIndexPath);
                foreach (SplitAnimation entry in animationIndex.animations)
                {
                    if (entry == null || !PreviewAnimationNameSet.Contains(entry.name)) continue;
                    string assetPath = "Assets/Official/CharacterCreation/Recovered/NativeCharacterData/" + entry.file;
                    if (File.Exists(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length))))
                        paths.Add("unity/" + assetPath);
                }
                string commonRoot = "unity/Assets/Official/CharacterCreation/Recovered/CharacterModels/Recovered/"
                    + sexFolder + "/AppearanceAssets/Character/Model/Common/";
                foreach (string atlasRoot in new[]
                {
                    modelRoot + "Atlas/High/", commonRoot + "Atlas/High/",
                    modelRoot + "Atlas/Placeholder/", commonRoot + "Atlas/Placeholder/"
                })
                {
                    string absoluteRoot = Path.Combine(Application.dataPath,
                        atlasRoot.Substring("unity/Assets/".Length));
                    if (!Directory.Exists(absoluteRoot)) continue;
                    foreach (string file in Directory.GetFiles(absoluteRoot, "*.png", SearchOption.TopDirectoryOnly))
                        paths.Add("unity/Assets/" + file.Substring(Application.dataPath.Length + 1).Replace('\\', '/'));
                }
                foreach (CharacterCreationSkinInfo info in CreationSkinInfos(catalog, female ? "female" : "male", female ? 1 : 0))
                {
                    if (!info.HasDye) continue;
                    string dyeMapPath = "unity/" + DyeMapPath(sexFolder, info);
                    if (File.Exists(Path.Combine(projectRoot, dyeMapPath.Replace('/', Path.DirectorySeparatorChar))))
                        paths.Add(dyeMapPath);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => ReadFileHash(projectRoot, path)).ToArray();
        }

        private static string[] GetMissingDyeMaps(string projectRoot, CharacterCreationAssetCatalog catalog)
        {
            var missing = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string sex in new[] { "male", "female" })
            {
                bool female = sex == "female";
                string sexFolder = female ? "Female" : "Male";
                foreach (CharacterCreationSkinInfo info in CreationSkinInfos(catalog,
                    female ? "female" : "male", female ? 1 : 0))
                {
                    if (!info.HasDye) continue;
                    string dyeMapPath = "unity/" + DyeMapPath(sexFolder, info);
                    if (!File.Exists(Path.Combine(projectRoot, dyeMapPath.Replace('/', Path.DirectorySeparatorChar))))
                        missing.Add(dyeMapPath);
                }
            }
            return missing.ToArray();
        }

        private static FileHash[] GetOutputs(string projectRoot, CharacterCreationAssetCatalog catalog)
        {
            string absoluteRoot = Path.Combine(projectRoot, "unity", BakeRoot.Replace('/', Path.DirectorySeparatorChar));
            var paths = Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("native-model-bake-manifest.json", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .Select(path => "unity/Assets/" + path.Substring(Application.dataPath.Length + 1).Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal);
            string catalogPath = "unity/" + AssetDatabase.GetAssetPath(catalog);
            paths.Add(catalogPath);
            if (File.Exists(Path.Combine(projectRoot, catalogPath.Replace('/', Path.DirectorySeparatorChar) + ".meta")))
                paths.Add(catalogPath + ".meta");
            return paths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => ReadFileHash(projectRoot, path)).ToArray();
        }

        private static FileHash ReadFileHash(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return new FileHash { path = relativePath, bytes = stream.Length, sha256 = Hash(stream) };
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Hash(Stream stream)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string HashFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return Hash(stream);
        }

        private static void WriteAtomic(string path, string text)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
