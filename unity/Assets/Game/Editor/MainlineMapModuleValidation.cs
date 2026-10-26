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
