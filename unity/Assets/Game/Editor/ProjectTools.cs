using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Zjcs.Combat.Official.EditorTools;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    // Project validation and Windows release tools.
    public static class ProjectTools
    {
        private const string EditorVersion = "2022.3.57f1c2";
        private const string BootstrapScene = "Assets/Game/Scenes/Bootstrap.unity";
        private const string CombatCatalogAsset = "Assets/Game/Resources/Combat/OfficialCombatAssetCatalog.asset";
        private static readonly string[] CombatCatalogPayloads =
        {
            "Assets/Official/Combat/skill-rules.json",
            "Assets/Official/Combat/skill-rules-source-manifest.json",
            "Assets/Official/Combat/battle-rules.json",
            "Assets/Official/Combat/battle-rules-source-manifest.json",
            "Assets/Official/Combat/rules.json",
            "Assets/Official/Combat/rules-source-manifest.json",
            "Assets/Official/Combat/monster-hp-plies.json",
            "Assets/Official/Combat/monster-hp-plies-source-manifest.json"
        };
        private static readonly string[] CombatCatalogFields =
        {
            "skillRules", "skillSourceManifest", "battleRules", "battleSourceManifest", "rules", "rulesSourceManifest",
            "monsterHpPlies", "monsterHpPliesSourceManifest"
        };

        [MenuItem("Zjcs/Project/Configure Framework")]
        public static void Configure()
        {
            RequireEditorVersion();
            PlayerSettings.companyName = "LocalOffline";
            PlayerSettings.productName = "杖剑传说";
            PlayerSettings.defaultScreenWidth = 450;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = false;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.externalVersionControl = "Visible Meta Files";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScene, true) };
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Zjcs/Project/Validate Framework")]
        public static void Validate()
        {
            RequireEditorVersion();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScene) == null)
                throw new BuildFailedException("The framework bootstrap scene could not be imported.");

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length != 1 || !scenes[0].enabled || scenes[0].path != BootstrapScene)
                throw new BuildFailedException("The framework must build only its empty bootstrap scene.");

            if (PlayerSettings.defaultScreenWidth != 450 || PlayerSettings.defaultScreenHeight != 800
                || PlayerSettings.fullScreenMode != FullScreenMode.Windowed
                || !PlayerSettings.resizableWindow || PlayerSettings.allowFullscreenSwitch)
                throw new BuildFailedException("Run Zjcs/Project/Configure Framework to apply desktop settings.");

            BindCombatCatalog();
            FightCombatValidation.RunSmokeTest();
            Zjcs.Editor.Combat.OfficialBattleHudAssetTools.ValidateRenderer();
            try
            {
                OfficialMap10Tools.Validate();
            }
            catch (Exception error)
            {
                throw new BuildFailedException("Unity Editor was found and the project compiled, but Map10 official asset validation failed. "
                    + "Check the source AssetBundle Unity version, build target, and external CAB dependencies. Details: " + error);
            }
            Debug.Log("Unity framework, official combat configuration, and Map10 assets validated.");
        }

        private static void BindCombatCatalog()
        {
            var payloads = new TextAsset[CombatCatalogPayloads.Length];
            for (int i = 0; i < payloads.Length; i++)
            {
                payloads[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(CombatCatalogPayloads[i]);
                if (payloads[i] == null)
                    throw new BuildFailedException("Official combat TextAsset was not imported: " + CombatCatalogPayloads[i]);
            }

            const string resourcesFolder = "Assets/Game/Resources";
            const string combatFolder = resourcesFolder + "/Combat";
            if (!AssetDatabase.IsValidFolder(resourcesFolder)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            if (!AssetDatabase.IsValidFolder(combatFolder)) AssetDatabase.CreateFolder(resourcesFolder, "Combat");

            OfficialCombatAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<OfficialCombatAssetCatalog>(CombatCatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficialCombatAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CombatCatalogAsset);
            }

            var serialized = new SerializedObject(catalog);
            for (int i = 0; i < payloads.Length; i++)
            {
                SerializedProperty field = serialized.FindProperty(CombatCatalogFields[i]);
                if (field == null)
                    throw new BuildFailedException("Official combat catalog field is missing: " + CombatCatalogFields[i]);
                field.objectReferenceValue = payloads[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (OfficialCombatAssetCatalog.LoadDefault() != catalog)
                throw new BuildFailedException("Official combat Resources catalog binding did not resolve.");
        }

        [MenuItem("Zjcs/Build/Windows Framework Only")]
        public static void BuildFrameworkWindows()
        {
            Configure();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Install Windows Build Support for the pinned Unity Editor.");

            // Keep this empty framework separate from the existing released game.
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string destination = Path.Combine(repository, "output", "unity-framework", "杖剑传说.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScene },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Unity Windows framework build failed; inspect the Editor log.");
            Debug.Log("Empty framework built to " + destination + ". This is not a game release.");
        }

        [MenuItem("Zjcs/Build/Windows Game")]
        public static void BuildWindowsGame()
        {
            RequireEditorVersion();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Install Windows Build Support for the pinned Unity Editor.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || !scenes.Contains(BootstrapScene))
                throw new BuildFailedException("The game build must include the runtime Bootstrap scene.");

            PlayerSettings.companyName = "LocalOffline";
            PlayerSettings.productName = "杖剑传说";
            PlayerSettings.defaultScreenWidth = 450;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = false;

            const string destination = "D:/Desktop/杖剑传说/杖剑传说.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Unity Windows game build failed; inspect the Editor log.");
            Debug.Log("Windows game built to " + destination + " (" + report.summary.totalSize + " bytes).");
        }

        private static void RequireEditorVersion()
        {
            if (!string.Equals(Application.unityVersion, EditorVersion, StringComparison.Ordinal))
                throw new BuildFailedException("Use Unity " + EditorVersion + "; found " + Application.unityVersion);
        }
    }
}
