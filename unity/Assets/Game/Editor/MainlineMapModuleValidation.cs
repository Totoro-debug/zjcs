using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class MainlineMapModuleValidation
    {
        private const string Key = "Zjcs.SharedMainlineProbe";

        public static void ValidateProfileDataManifests()
        {
            string[] battle = { "854_156383", "854_156383", "854_156383", "854_156383", "871_156598", "854_156383", "884_156692" };
            string[] story = { "854_156383", "854_156383", "854_156383", "871_156598", "871_156598", "871_156598", "884_156692" };
            for (int mapId = 10; mapId <= 16; mapId++)
            {
                OfficialMainlineMapProfile profile = OfficialMainlineMapProfile.Get(mapId);
                int index = mapId - 10;
                if (profile.BattleDataManifest != battle[index] || profile.StoryDataManifest != story[index])
                    throw new InvalidDataException("Mainline map source manifests differ from the pinned map inputs: " + mapId);
            }
            OfficialMainlineMapProfile whaleIsland = OfficialMainlineMapProfile.Get(16);
            if (whaleIsland.IsRuntimeEntryReady || whaleIsland.NativeUiRoot != null || whaleIsland.RegionCount != 43
                || whaleIsland.DefaultEntryPlacementId != 101 || whaleIsland.LocalChannelCount != 1
                || whaleIsland.GridVisibilityResource != "World/Mainline/Visibility/Map16/visibility"
                || whaleIsland.GridBodiesResource != "World/Mainline/Visibility/Map16/bodies"
                || whaleIsland.GridFogResource != "World/Mainline/Fog/Map16"
                || whaleIsland.InteractionResource != "Map16/interactions"
                || whaleIsland.InteractionManifest != "884_156692")
                throw new InvalidDataException("Whale Island data profile or entry readiness differs from its migrated inputs.");
            if (whaleIsland.RequiredAccessConditions.Count != 0
                || Resources.Load<TextAsset>(whaleIsland.GridVisibilityResource) == null
                || Resources.Load<TextAsset>(whaleIsland.GridBodiesResource) == null
                || Resources.Load<GameObject>(whaleIsland.GridFogResource) == null
                || Resources.Load<TextAsset>(whaleIsland.InteractionResource) == null
                || Resources.Load<TextAsset>(whaleIsland.Resource("battle-runtime")) == null
                || Resources.Load<TextAsset>(whaleIsland.Resource("stories")) == null)
                throw new InvalidDataException("Whale Island profile resources or access condition are not loadable.");
            WorldMapAssets map = OfficialWorldMapAssetCatalog.LoadDefault().LoadMap(16);
            var definition = map.Definition;
            if (definition.Navigation.Width != 140 || definition.Navigation.Height != 120
                || definition.Navigation.MaxSteps != 35 || definition.Entities.Length != 1021
                || definition.MaxRegionProgress.Length != whaleIsland.RegionCount)
                throw new InvalidDataException("Whale Island public catalog definition differs from its official map.");
            int entityCount = OfficialMap10EntityView.ValidateCatalog(
                Resources.Load<TextAsset>(whaleIsland.Resource("Entities/catalog")), 16,
                out int modelCount, out int geometryCount, out int spineCount, out int textureCount);
            if (entityCount != definition.Entities.Length || modelCount != 1
                || geometryCount != 0 || spineCount != 0 || textureCount != 0)
                throw new InvalidDataException("Whale Island placement catalog differs or contains unverified visual bindings.");
            Debug.Log("Whale Island shared entity catalog loaded: 1021 original placements; native visual binding remains pending.");
            var regions = new OfficialMainlineRegionPolicy(definition, whaleIsland);
            var points = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var region in definition.MaxRegionProgress)
                if (!regions.IsAccessible(region.Region, points,
                    _ => throw new InvalidDataException("Whale Island has no regional story-access condition.")))
                    throw new InvalidDataException("Whale Island official regional access condition changed.");
        }

        public static void ValidateWhaleIslandStoryCondition() => OfficialMainlineMapModuleProbe.ValidateWhaleIslandStoryCondition();

        public static void RunInteractionCatalogs() => OfficialMainlineMapModuleProbe.RunInteractionCatalogs();

        public static void Run()
        {
            OfficialMap11UiTools.Import();
            ProjectTools.Validate();
            Zjcs.Editor.Progression.OfflineMainlineProgressValidation.Run();
            Zjcs.Editor.Progression.OfflineMapStaminaValidation.Run();
            Zjcs.Editor.Progression.OfflineWorldExplorationValidation.Run();
            StartPlayMode();
        }

        public static void RunPlayMode()
        {
            OfficialMap11UiTools.Import();
            RunLoadedPlayMode();
        }

        public static void RunLoadedPlayMode()
        {
            ProjectTools.Validate();
            Zjcs.Editor.Progression.OfflineMainlineProgressValidation.Run();
            Zjcs.Editor.Progression.OfflineMapStaminaValidation.Run();
            Zjcs.Editor.Progression.OfflineWorldExplorationValidation.Run();
            StartPlayMode();
        }

        private static void StartPlayMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, OfficialMainlineMapModuleProbe.Argument);
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -verify-mainline-modules and an isolated output directory.");
            SessionState.SetString(Key, Path.GetFullPath(args[index + 1]));
            Hook();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Home.HomeAcceptanceLauncher.ConfigurePortraitGameView(540, 960);
            EditorWindow gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.maximized = true;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, ""))) Hook();
        }

        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            string root = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(root)) return;
            SessionState.EraseString(Key);
            EditorApplication.playModeStateChanged -= Changed;
            bool success = File.Exists(Path.Combine(root, "passed.txt")) && !File.Exists(Path.Combine(root, "failed.txt"));
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static void BuildWindowsPlayer()
        {
            string destination = null;
            string[] args = Environment.GetCommandLineArgs();
            int output = Array.IndexOf(args, "-mainline-player-output");
            bool releaseProbe = args.Contains("-mainline-release-probe");
            if (output >= 0)
            {
                if (output + 1 >= args.Length) throw new ArgumentException("Pass a Windows executable path after -mainline-player-output.");
                destination = Path.GetFullPath(args[output + 1]);
            }
            if (destination == null)
            {
                string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (string.IsNullOrWhiteSpace(workspace))
                    throw new ArgumentException("Set ZJCS_VALIDATION_DIR or pass -mainline-player-output.");
                destination = Path.Combine(Path.GetFullPath(workspace), "player", "杖剑传说.exe");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(),
                    locationPathName = destination,
                    target = BuildTarget.StandaloneWindows64,
                    options = releaseProbe ? BuildOptions.None : BuildOptions.Development,
                    extraScriptingDefines = releaseProbe ? new[] { "ZJCS_MAINLINE_MODULE_VALIDATION" } : Array.Empty<string>()
                });
            }
            finally { PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previous); }
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Shared mainline module Windows regression build failed.");
            Debug.Log("Shared mainline module Windows regression Player: " + destination);
        }
    }
}
