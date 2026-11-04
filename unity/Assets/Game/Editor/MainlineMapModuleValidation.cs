using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            if (!whaleIsland.RequiredAccessConditions.SequenceEqual(new[] { 116 })
                || Resources.Load<TextAsset>(whaleIsland.GridVisibilityResource) == null
                || Resources.Load<TextAsset>(whaleIsland.GridBodiesResource) == null
                || Resources.Load<GameObject>(whaleIsland.GridFogResource) == null
                || Resources.Load<TextAsset>(whaleIsland.InteractionResource) == null
                || Resources.Load<TextAsset>(whaleIsland.Resource("battle-runtime")) == null
                || Resources.Load<TextAsset>(whaleIsland.Resource("stories")) == null)
                throw new InvalidDataException("Whale Island profile resources or access condition are not loadable.");
        }

        public static void ValidateWhaleIslandStoryCondition()
        {
            const string tag = "金宝箱_鲸岛神龟";
            Type evaluatorType = typeof(OfficialMainlineMapProfile).Assembly.GetType(
                "Zjcs.Presentation.World.Map13OfflinePlayerConditionEvaluator", true);
            object evaluator = Activator.CreateInstance(evaluatorType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Array.Empty<Map13RuntimePlayerCondition>(), (Func<int, long>)(_ => 0),
                    (Func<string, int>)(_ => 0), (Func<string, int>)(_ => 0), (Func<int, int>)(_ => 0),
                    (Func<int, int, int>)((_, _) => 0), (Func<string, bool>)(value => value == tag) }, null);
            MethodInfo evaluate = evaluatorType.GetMethod("Evaluate");
            bool Evaluate(string value, int not) => (bool)evaluate.Invoke(evaluator, new object[] {
                "MapLogicEntityEndByTag", new Map13RuntimePlayerConditionData { Tag = value }, null, not });
            if (!Evaluate(tag, 0) || Evaluate(tag, 1) || Evaluate("another-tag", 0) || !Evaluate("another-tag", 1))
                throw new InvalidDataException("Shared mainline completed-tag story condition changed.");
        }

        public static void RunInteractionCatalogs()
        {
            Type type = typeof(OfficialMainlineMapProfile).Assembly.GetType(
                "Zjcs.Presentation.World.OfficialMainlineInteractionCatalog", true);
            MethodInfo loadMap = type.GetMethod("Load", new[] { typeof(int) });
            for (int map = 10; map <= 15; map++)
            {
                var heroes = (Array)type.GetField("heroes").GetValue(loadMap.Invoke(null, new object[] { map }));
                if (heroes.Length != 4) throw new InvalidDataException("Historical hero selection changed: " + map);
            }
            object catalog = loadMap.Invoke(null, new object[] { 16 });
            var variants = (Array)type.GetField("heroes").GetValue(catalog);
            int[] ids = variants.Cast<object>().Select(hero => (int)hero.GetType().GetField("EntityClassId").GetValue(hero)).ToArray();
            if (!ids.SequenceEqual(new[] { 6570, 6571, 6572, 6573, 6574, 6575 }))
                throw new InvalidDataException("Whale Island profile did not load all six original hero variants.");
            var regions = (Array)type.GetField("regions").GetValue(catalog);
            if (regions.Length != 43 || regions.Cast<object>().Any(region =>
                (int)region.GetType().GetField("MapClassId").GetValue(region) != 16))
                throw new InvalidDataException("Whale Island profile did not load its 43 original interaction regions.");
            MethodInfo load = type.GetMethod("Load", new[] { typeof(string), typeof(string), typeof(int), typeof(int) });
            foreach (object[] invalid in new[] {
                new object[] { "Map16/interactions", "871_156598", 16, 43 },
                new object[] { "Map16/interactions", "884_156692", 13, 43 },
                new object[] { "Map16/interactions", "884_156692", 16, 42 } })
            {
                try { load.Invoke(null, invalid); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidDataException) { continue; }
                throw new InvalidDataException("The shared interaction loader accepted another source identity.");
            }
            Debug.Log("Shared interaction catalog validation passed: historical maps 10-15, six Whale Island heroes and source identity guards.");
        }

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
            typeof(Home.HomeAcceptanceLauncher).GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 540, 960 });
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
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string destination = Path.Combine(repository, ".tools", "mainline-refactor-player", "杖剑传说.exe");
            string[] args = Environment.GetCommandLineArgs();
            int output = Array.IndexOf(args, "-mainline-player-output");
            bool releaseProbe = args.Contains("-mainline-release-probe");
            if (output >= 0)
            {
                if (output + 1 >= args.Length) throw new ArgumentException("Pass a Windows executable path after -mainline-player-output.");
                destination = Path.GetFullPath(args[output + 1]);
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
