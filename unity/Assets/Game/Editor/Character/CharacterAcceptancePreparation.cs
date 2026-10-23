using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Character
{
    public static class CharacterAcceptancePreparation
    {
        private const string NativeRoot = "Assets/Official/CharacterCreation/Recovered/NativeCharacterData";
        private const string CharacterCatalog = "Assets/Game/Resources/CharacterCreation/OfficialCharacterCreationAssetCatalog.asset";
        private const string PetCatalog = "Assets/Game/Resources/PetGacha/official-pet-gacha-assets.asset";
        private const string CharacterTool = "Zjcs.Editor.CharacterCreation.CharacterCreationNativeModelTools";
        private const string PetTool = "Zjcs.Editor.PetGacha.PetGachaNativeModelTools";

        [MenuItem("Zjcs/Validate/Prepare Character Acceptance Assets")]
        public static void EnsureModels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before preparing character acceptance assets.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for Unity import and compilation to finish, then retry preparation.");
            bool force = Environment.GetEnvironmentVariable("ZJCS_CHARACTER_FORCE_REBAKE") == "1"
                || Environment.GetCommandLineArgs().Contains("-forceAcceptanceRebuild");
            PrepareCharacter(force);
            PreparePets(force);
        }

        [MenuItem("Zjcs/Validate/Check Prepared Character Models")]
        public static void ValidatePreparedModels()
        {
            ScriptableObject catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CharacterCatalog);
            if (catalog == null) throw new InvalidOperationException("The official character creation catalog is not imported.");
            ValidateCharacter(catalog);
            ValidatePets();
            Debug.Log("[Acceptance preparation] character and pet model bindings and required animation clips validated.");
        }

        private static void PrepareCharacter(bool force)
        {
            ScriptableObject catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CharacterCatalog);
            if (catalog == null) throw new InvalidOperationException("The official character creation catalog is not imported.");
            // Persistent GUIDs remain stable across Editor restarts; exclude only the two outputs assigned by Bake().
            Func<string> configuration = () => string.Join("\n", File.ReadAllLines(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, CharacterCatalog)).Where(line =>
                    !line.TrimStart().StartsWith("maleNativeModelPrefab:", StringComparison.Ordinal)
                    && !line.TrimStart().StartsWith("femaleNativeModelPrefab:", StringComparison.Ordinal)));

            var inputs = CommonInputs(CharacterTool).Concat(new[]
            {
                NativeRoot + "/Animations", NativeRoot + "/DyeMaps",
                "Assets/Official/CharacterCreation/Recovered/CharacterModels",
                "Assets/Official/CharacterCreation/appearance-options.json",
                "Assets/Official/CharacterCreation/wardrobe-rules.json",
                "Assets/Game/Runtime/Offline/CharacterCreation",
                "Assets/Game/Runtime/Presentation/Character/CharacterCreationDyeMapBinding.cs",
                "Assets/Game/Runtime/Presentation/CharacterCreation/Spine-SkeletonDye.shader",
                "../assets/characters/wardrobe/ui.json",
                "../assets/reference/character-creation/profession-atlas-source-manifest.json",
                CharacterCatalog + ".meta"
            }).Concat(AssetDatabase.GetDependencies(CharacterCatalog, true).Where(path =>
                path != CharacterCatalog && !path.StartsWith(NativeRoot + "/Baked/", StringComparison.Ordinal)));
            Ensure("character-models", inputs,
                new[] { NativeRoot + "/Baked", CharacterCatalog,
                    "../assets/reference/character-creation/native-character-model-bake-source-manifest.json" },
                configuration, CharacterTool, () => ValidateCharacter(catalog), force);
        }

        private static void PreparePets(bool force)
        {
            Ensure("pet-models", CommonInputs(PetTool).Concat(new[]
                {
                    "Assets/Official/PetGacha/NativeSource",
                    "Assets/Game/Resources/PetGacha/pet-gacha-runtime.json",
                    "../assets/reference/pet-gacha/native-model-source-manifest.json",
                    "Assets/Game/Runtime/Offline/Progression/OfficialPetGachaCatalog.cs",
                    "Assets/Game/Runtime/Offline/Progression/OfficialPetGachaAssetCatalog.cs",
                    "Assets/Game/Runtime/Presentation/Home/HomePetMeshGraphic.cs",
                    "Assets/Game/Runtime/Presentation/CharacterCreation/Spine-SkeletonDye.shader"
                }),
                new[] { "Assets/Official/PetGacha/Native", PetCatalog,
                    "../assets/reference/pet-gacha/native-model-bake-manifest.json" },
                () => string.Empty, PetTool, ValidatePets, force);
        }

        private static IEnumerable<string> CommonInputs(string tool)
        {
            // Include imported dependencies and converter source, not file timestamps. A parser or importer change invalidates the receipt.
            yield return "Assets/Game/Editor/Character/AcceptancePreparationCache.cs";
            yield return "Assets/Game/Editor/Character/CharacterAcceptancePreparation.cs";
            yield return "Assets/Game/Editor/" + (tool == CharacterTool
                ? "CharacterCreation/CharacterCreationNativeModelTools.cs" : "PetGacha/PetGachaNativeModelTools.cs");
            yield return "Assets/Spine/Runtime";
            yield return "Assets/Spine/Editor";
            yield return "ProjectSettings/ProjectSettings.asset";
            yield return "Packages/manifest.json";
            yield return "Packages/packages-lock.json";
            yield return "../official-source.json";
        }

        private static void Ensure(string name, IEnumerable<string> inputs, IEnumerable<string> outputs,
            Func<string> configuration, string tool, Action validate, bool force)
        {
            string unityRoot = Directory.GetParent(Application.dataPath).FullName;
            Func<IEnumerable<string>, string[]> resolve = paths => paths.Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFullPath(Path.Combine(unityRoot, path))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string[] inputPaths = resolve(inputs);
            inputPaths = inputPaths.Concat(inputPaths.Select(path => path + ".meta")).ToArray();
            string[] outputPaths = resolve(outputs);
            string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            outputPaths = outputPaths.Concat(outputPaths.Where(path => path.StartsWith(
                assetsRoot, StringComparison.OrdinalIgnoreCase))
                .Select(path => path + ".meta")).ToArray();
            Func<string> context = () => Application.unityVersion + "\n" + EditorUserBuildSettings.activeBuildTarget + "\n" + configuration();
            string receipt = Path.Combine(unityRoot, "Library", "AcceptancePreparation", name + ".receipt");
            AcceptancePreparationCache.Ensure(receipt, inputPaths, outputPaths, context,
                () => Invoke(ToolType(tool), "Bake", null), validate,
                message => Debug.Log("[Acceptance preparation] " + name + ": " + message), force);
        }

        private static void ValidateCharacter(ScriptableObject catalog)
        {
            Invoke(catalog.GetType(), "ValidateBindings", catalog);
            string[] animations = (string[])ToolType(CharacterTool).GetField("PreviewAnimationNames",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var properties = new SerializedObject(catalog);
            foreach (string property in new[] { "maleNativeModelPrefab", "femaleNativeModelPrefab" })
            {
                GameObject prefab = properties.FindProperty(property).objectReferenceValue as GameObject;
                Animator animator = prefab == null ? null : prefab.GetComponent<Animator>();
                RuntimeAnimatorController controller = animator == null ? null : animator.runtimeAnimatorController;
                if (controller == null || animations.Except(controller.animationClips.Select(clip => clip.name)).Any())
                    throw new InvalidOperationException("The prepared character model lacks its required animation set: " + property);
            }
        }

        private static void ValidatePets()
        {
            ScriptableObject catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PetCatalog);
            if (catalog == null) throw new InvalidOperationException("The official native pet catalog is missing.");
            Type rulesType = ToolType("Zjcs.Offline.Progression.OfficialPetGachaAssets");
            object rules = Invoke(rulesType, "LoadDefault", null);
            try { catalog.GetType().GetMethod("Validate").Invoke(catalog, new[] { rules }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static Type ToolType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null)
            ?? throw new InvalidOperationException("Acceptance preparation tool is unavailable: " + name);

        private static object Invoke(Type type, string method, object instance)
        {
            try { return type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Invoke(instance, null); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
