using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zjcs.Editor.Home
{
    // Runs the same explicit diagnostic in a visible Play Mode and Windows Player.
    public static class DailyDungeonPlayerAcceptance
    {
        public static void Build()
        {
            string directory = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Supply the isolated daily dungeon validation directory.");
            ProjectTools.Validate();
            Skills.OfficialSkillAssetTools.BuildWindowsValidation();
        }

        public static void Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--zjcs-validate-daily-dungeon") < 0)
                throw new InvalidOperationException("Supply the isolated daily dungeon validation output directory.");
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            HomeAcceptanceLauncher.ConfigurePortraitGameView();
            Application.runInBackground = true;
            EditorApplication.isPlaying = true;
        }
    }
}
