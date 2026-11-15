using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Zjcs.Editor
{
    // Applies to menu, scripted and validation builds before Burst reads productName.
    public sealed class WindowsBuildNamingPolicy : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        internal static void RequireAsciiBuildPath(string path)
        {
            if (Path.GetFullPath(path).Any(character => character > 127))
                throw new BuildFailedException("Windows build paths must use ASCII: " + path);
        }

        internal static void ValidateWindowsBuildPaths(string destination)
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(
                Path.GetDirectoryName(destination), "*", SearchOption.AllDirectories))
                RequireAsciiBuildPath(path);
        }


        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64
                && report.summary.platform != BuildTarget.StandaloneWindows) return;
            WindowsBuildNamingPolicy.RequireAsciiBuildPath(report.summary.outputPath);
            string directory = Path.GetDirectoryName(report.summary.outputPath);
            if (Directory.Exists(directory))
                WindowsBuildNamingPolicy.ValidateWindowsBuildPaths(report.summary.outputPath);
            PlayerSettings.productName = "Zjcs";
        }
    }

    public sealed class WindowsBuildPathValidation : IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneWindows64
                || report.summary.platform == BuildTarget.StandaloneWindows)
                WindowsBuildNamingPolicy.ValidateWindowsBuildPaths(report.summary.outputPath);
        }
    }
}
