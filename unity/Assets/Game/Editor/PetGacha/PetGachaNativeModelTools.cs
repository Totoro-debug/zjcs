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
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor.PetGacha
{
    public static class PetGachaNativeModelTools
    {
        [Serializable]
        private sealed class SourceManifest
        {
            public string manifest;
            public ModelSource[] models;
        }

        [Serializable]
        private sealed class ModelSource
        {
            public int pet_class_id;
            public int ui_model;
            public int[] support_skill_ids;
            public SourceFile[] files;
        }

        [Serializable]
        private sealed class SourceFile
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class BakeManifest
        {
            public string manifest;
            public string generator;
            public SourceFile[] inputs;
            public SourceFile[] outputs;
        }

        private const string ManifestPath = "assets/reference/pet-gacha/native-model-source-manifest.json";
        private const string SourceRoot = "Assets/Official/PetGacha/NativeSource";
        private const string BakeRoot = "Assets/Official/PetGacha/Native";
        private const string CatalogPath = "Assets/Game/Resources/PetGacha/official-pet-gacha-assets.asset";
        private static readonly int[] PetIds = { 1600, 1601, 1602, 1603, 1604, 1605, 1607, 1608 };

        [MenuItem("Zjcs/Pet Gacha/Bake Native Pet Models")]
        public static void Bake()
        {
            AssetDatabase.Refresh();
            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            PetGachaRuntimeCatalog rules = OfficialPetGachaAssets.LoadDefault();
            SourceManifest manifest = LoadAndValidateSourceManifest(projectRoot, rules);
            EnsureFolder("Assets/Official/PetGacha");
            EnsureFolder(BakeRoot);
            var prefabByPetId = new Dictionary<int, GameObject>();

            foreach (PetGachaPetProfile profile in rules.PetProfiles.OrderBy(value => value.ClassId))
            {
                ModelSource source = manifest.models.Single(value => value.pet_class_id == profile.ClassId);
                if (source.ui_model != profile.UIModel || source.support_skill_ids == null
                    || !source.support_skill_ids.SequenceEqual(profile.SupportSkillIds))
                    throw new BuildFailedException("The pet model source mapping differs from the official runtime catalog: "
                        + profile.ClassId);
                prefabByPetId.Add(profile.ClassId, BakeModel(profile));
            }

            OfficialPetGachaAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialPetGachaAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialPetGachaAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("Manifest").stringValue = rules.Manifest;
            SerializedProperty models = serialized.FindProperty("NativeModels");
            models.arraySize = PetIds.Length;
            for (int index = 0; index < PetIds.Length; index++)
            {
                int petId = PetIds[index];
                PetGachaPetProfile profile = rules.PetProfile(petId);
                SerializedProperty row = models.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("PetClassId").intValue = petId;
                row.FindPropertyRelative("UIModel").intValue = profile.UIModel;
                row.FindPropertyRelative("Prefab").objectReferenceValue = prefabByPetId[petId];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            catalog.Validate(rules);
            WriteBakeManifest(projectRoot, rules.Manifest);
            Debug.Log("Baked eight official pet gacha models as Unity native Animator prefabs.");
        }

        private static void WriteBakeManifest(string projectRoot, string manifestVersion)
        {
            string runtimePath = "unity/Assets/Game/Resources/PetGacha/pet-gacha-runtime.json";
            var inputs = new[]
            {
                HashFile(projectRoot, ManifestPath),
                HashFile(projectRoot, runtimePath)
            };
            string unityRoot = Path.Combine(projectRoot, "unity");
            string nativeRoot = Path.Combine(unityRoot,
                BakeRoot.Replace('/', Path.DirectorySeparatorChar));
            string catalogPath = Path.Combine(unityRoot,
                CatalogPath.Replace('/', Path.DirectorySeparatorChar));
            var outputPaths = Directory.GetFiles(nativeRoot, "*", SearchOption.AllDirectories)
                .Concat(new[] { nativeRoot + ".meta", catalogPath, catalogPath + ".meta" })
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            SourceFile[] outputs = outputPaths.Select(path =>
            {
                string relative = path.Substring(projectRoot.Length + 1).Replace('\\', '/');
                return HashFile(projectRoot, relative);
            }).ToArray();
            var bake = new BakeManifest
            {
                manifest = manifestVersion,
                generator = "PetGachaNativeModelTools.Bake; Unity Spine SkeletonBaker",
                inputs = inputs,
                outputs = outputs
            };
            string outputPath = Path.Combine(projectRoot,
                "assets/reference/pet-gacha/native-model-bake-manifest.json".Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(bake, true), new UTF8Encoding(false));
        }

        private static SourceFile HashFile(string projectRoot, string relative)
        {
            string path = Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(path);
            return new SourceFile { path = relative, bytes = bytes.LongLength, sha256 = Hash(bytes) };
        }

        private static GameObject BakeModel(PetGachaPetProfile profile)
        {
            string stem = "pet_" + profile.UIModel.ToString();
            string input = SourceRoot + "/" + profile.UIModel.ToString() + "/";
            TextAsset skeletonBytes = AssetDatabase.LoadAssetAtPath<TextAsset>(input + stem + ".skel.bytes");
            TextAsset atlasText = AssetDatabase.LoadAssetAtPath<TextAsset>(input + stem + ".atlas.txt");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(input + stem + ".png");
            if (skeletonBytes == null || atlasText == null || texture == null)
                throw new BuildFailedException("Pinned official pet Spine inputs were not imported: " + stem);

            string[] atlasPages = ReadAtlasPageNames(atlasText.text);
            if (atlasPages.Length != 1 || Path.GetFileNameWithoutExtension(atlasPages[0]) != texture.name)
                throw new BuildFailedException("The official pet atlas page does not match its texture: " + stem);
            Shader shader = Shader.Find("Zjcs/CharacterCreation/OfficialSpineDye");
            if (shader == null) throw new BuildFailedException("The native official Spine shader is unavailable.");

            string outputRoot = BakeRoot + "/" + profile.UIModel.ToString();
            EnsureFolder(outputRoot);
            string materialPath = outputRoot + "/" + stem + "_Native.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = stem + "_Native", mainTexture = texture };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader || material.mainTexture != texture)
                throw new BuildFailedException("A native pet model material was modified: " + materialPath);

            SpineAtlasAsset atlas = null;
            SkeletonDataAsset skeleton = null;
            try
            {
                atlas = SpineAtlasAsset.CreateRuntimeInstance(atlasText, new[] { material }, true);
                skeleton = SkeletonDataAsset.CreateRuntimeInstance(skeletonBytes, atlas, false, 0.01f);
                SkeletonData data = skeleton.GetSkeletonData(true);
                if (data == null || data.DefaultSkin == null || data.FindAnimation("idle_01") == null)
                    throw new BuildFailedException("The official pet skeleton lacks its default skin or idle animation: " + stem);

                var skins = new ExposedList<Skin>();
                skins.Add(data.DefaultSkin);
                SkeletonBaker.BakeToPrefab(skeleton, skins, outputRoot, true, true);
                string prefabPath = outputRoot + "/" + skeleton.skeletonJSON.name
                    + " (" + data.DefaultSkin.Name + ").prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    outputRoot + "/" + skeleton.skeletonJSON.name + " Controller.controller");
                if (prefab == null || controller == null)
                    throw new BuildFailedException("Spine Baker did not create the pet prefab and Animator: " + stem);
                PrepareNativePrefab(prefabPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                if (skeleton != null) UnityEngine.Object.DestroyImmediate(skeleton);
                if (atlas != null) UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        private static void PrepareNativePrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) throw new BuildFailedException("The baked pet root has no Animator: " + prefabPath);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller == null || controller.layers.Length == 0)
                    throw new BuildFailedException("The baked pet Animator has no editable controller: " + prefabPath);
                AnimatorState idle = controller.layers[0].stateMachine.states
                    .Select(value => value.state).FirstOrDefault(value => value.name == "idle_01");
                if (idle == null) throw new BuildFailedException("The baked pet controller has no idle state: " + prefabPath);
                controller.layers[0].stateMachine.defaultState = idle;
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
                    .OfType<AnimationClip>())
                {
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                    if (clip.name == "idle_01") settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    EditorUtility.SetDirty(clip);
                }
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null)
                        throw new BuildFailedException("A baked pet slot is missing its mesh or material: " + renderer.name);
                    Transform existing = renderer.transform.Find("CanvasMesh");
                    GameObject graphicObject = existing == null
                        ? new GameObject("CanvasMesh", typeof(RectTransform)) : existing.gameObject;
                    RectTransform rect = (RectTransform)graphicObject.transform;
                    rect.SetParent(renderer.transform, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition3D = Vector3.zero;
                    rect.sizeDelta = Vector2.zero;
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                    HomePetMeshGraphic graphic = graphicObject.GetComponent<HomePetMeshGraphic>();
                    if (graphic == null) graphic = graphicObject.AddComponent<HomePetMeshGraphic>();
                    graphic.Configure(filter.sharedMesh, renderer);
                    renderer.enabled = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                EditorUtility.SetDirty(controller);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SourceManifest LoadAndValidateSourceManifest(string projectRoot,
            PetGachaRuntimeCatalog rules)
        {
            string path = Path.Combine(projectRoot, ManifestPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new BuildFailedException("The pet model source manifest is missing.");
            SourceManifest manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(path, Encoding.UTF8));
            if (manifest == null || manifest.manifest != rules.Manifest || manifest.models == null
                || manifest.models.Length != PetIds.Length)
                throw new BuildFailedException("The pet model source manifest is incomplete or unsupported.");
            foreach (PetGachaPetProfile profile in rules.PetProfiles)
            {
                ModelSource row = manifest.models.SingleOrDefault(value => value.pet_class_id == profile.ClassId);
                if (row == null || row.ui_model != profile.UIModel || row.files == null || row.files.Length != 3)
                    throw new BuildFailedException("The source manifest does not cover pet " + profile.ClassId);
                foreach (SourceFile file in row.files)
                {
                    string relative = file.path.Replace('/', Path.DirectorySeparatorChar);
                    string absolute = Path.Combine(projectRoot, relative);
                    if (!File.Exists(absolute) || new FileInfo(absolute).Length != file.bytes
                        || Hash(File.ReadAllBytes(absolute)) != file.sha256)
                        throw new BuildFailedException("The official pet model input hash is invalid: " + file.path);
                }
            }
            return manifest;
        }

        private static string[] ReadAtlasPageNames(string atlas)
        {
            var pages = new List<string>();
            foreach (string line in atlas.Replace("\r", string.Empty).Split('\n'))
            {
                string value = line.Trim();
                if (value.Length == 0) continue;
                if (!value.Contains(":")) pages.Add(value);
                if (value.StartsWith("size:", StringComparison.Ordinal)) break;
            }
            return pages.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
                throw new BuildFailedException("Pet gacha output folder parent is missing: " + path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string Hash(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
