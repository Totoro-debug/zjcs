using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Zjcs.Editor
{
    public static class Map11PlayerValidationBuild
    {
        [MenuItem("Zjcs/Map11/Build Player Validation")]
        public static void Build()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(output))
                throw new BuildFailedException("Set ZJCS_VALIDATION_DIR to the active validation workspace.");

            output = Path.GetFullPath(output);
            string repository = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            string validationRoot = Path.Combine(repository, "output", "validation");
            if (!output.StartsWith(validationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Map11 validation Player must be built under output/validation.");

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            const string bootstrap = "Assets/Game/Scenes/Bootstrap.unity";
            if (scenes.Length == 0 || !scenes.Contains(bootstrap))
                throw new BuildFailedException("The Map11 validation Player requires the runtime Bootstrap scene.");

            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "Map11Validation.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Map11 validation Windows Player build failed.");
            UnityEngine.Debug.Log("Map11 validation Player built to " + report.summary.outputPath);
        }
    }
}
