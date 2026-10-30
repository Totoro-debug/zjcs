using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14PlayerBuildAcceptance
    {
        public static void BuildToValidationDirectory()
        {
            string validation = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(validation))
                throw new InvalidDataException("Explicit validation directory required.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new BuildFailedException("Select the Windows build target before running this validation.");

            NamedBuildTarget standalone = NamedBuildTarget.Standalone;
            ScriptingImplementation originalBackend = PlayerSettings.GetScriptingBackend(standalone);
            string originalCompanyName = PlayerSettings.companyName;
            string originalProductName = PlayerSettings.productName;
            string destination = Path.Combine(validation, "Player", "杖剑传说.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            try
            {
                PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.companyName = "ZjcsValidation";
                PlayerSettings.productName = "Map14PlayerValidation";
                var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path).ToArray();
                if (scenes.Length == 0) throw new BuildFailedException("No enabled game scenes are configured.");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = destination,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                if (report == null || report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Map14 Windows Player validation build failed.");
                Debug.Log("Map14 Windows Player validation build passed: " + destination);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(standalone, originalBackend);
                PlayerSettings.companyName = originalCompanyName;
                PlayerSettings.productName = originalProductName;
            }
        }
    }
}
