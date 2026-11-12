using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Zjcs.Editor
{
    public static class OfficialBattlePublicPlayerBuild
    {
        public static void Build()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(repository))
                throw new BuildFailedException("Set the active validation workspace and repository paths.");
            output = Path.GetFullPath(output);
            string root = Path.Combine(Path.GetFullPath(repository), "output", "validation");
            if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(Path.Combine(output, ".zjcs-validation.json")))
                throw new BuildFailedException("Build the public battle diagnostic inside a marked validation workspace.");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (!scenes.Contains("Assets/Game/Scenes/Bootstrap.unity"))
                throw new BuildFailedException("The original runtime Bootstrap scene is required.");
            var standalone = NamedBuildTarget.Standalone;
            var options = BuildOptions.None;
            string destination = Path.Combine(output, "PublicBattleValidation.exe");
            if (Environment.GetEnvironmentVariable("ZJCS_WINDOWS_SCRIPTS_ONLY") == "1")
            {
                if (!File.Exists(destination) || !Directory.Exists(Path.Combine(output, "PublicBattleValidation_Data")))
                    throw new BuildFailedException("A script update requires this session's complete Player and retained build cache.");
                options = BuildOptions.BuildScriptsOnly;
            }
            var backend = PlayerSettings.GetScriptingBackend(standalone);
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes, target = BuildTarget.StandaloneWindows64, options = options,
                    locationPathName = destination,
                    extraScriptingDefines = new[] { "ZJCS_BATTLE_PUBLIC_VALIDATION" }
                });
            }
            finally { PlayerSettings.SetScriptingBackend(standalone, backend); }
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("The public battle Windows Player build failed.");
        }
    }
}
