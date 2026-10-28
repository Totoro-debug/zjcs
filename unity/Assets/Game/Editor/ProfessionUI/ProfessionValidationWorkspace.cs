using System;
using System.IO;
using UnityEngine;

namespace Zjcs.Editor.ProfessionUI
{
    // Validation output never shares the player's persistent data or the source project.
    internal static class ProfessionValidationWorkspace
    {
        [Serializable] private sealed class Marker { public int format; public string repository; }

        internal static string Repository => RequiredPath("ZJCS_REPOSITORY_ROOT");
        internal static string DirectoryPath => RequiredPath("ZJCS_VALIDATION_DIR");

        internal static void AssertIsolatedProject()
        {
            string repository = Repository;
            string workspace = DirectoryPath;
            string root = Path.Combine(repository, "output", "validation");
            if (!Same(Directory.GetParent(workspace)?.FullName, root) ||
                !Same(Application.dataPath, Path.Combine(workspace, "unity", "Assets")))
                throw new InvalidOperationException("Profession validation requires its own output/validation workspace project.");
            var marker = JsonUtility.FromJson<Marker>(File.ReadAllText(Path.Combine(workspace, ".zjcs-validation.json")));
            if (marker == null || marker.format != 1 || !Same(marker.repository, repository))
                throw new InvalidOperationException("Profession validation workspace marker does not match the repository.");
        }

        internal static string Output(string name)
        {
            AssertIsolatedProject();
            if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name)
                throw new ArgumentException("Validation outputs must be direct workspace children.", nameof(name));
            return Path.Combine(DirectoryPath, name);
        }

        internal static string Fixture(string name)
        {
            AssertIsolatedProject();
            if (Path.GetFileName(name) != name) throw new ArgumentException("Expected fixture filename.", nameof(name));
            return Path.Combine(Repository, ".tools", "source-packages", "current", "871_156598", "profession-ui", "typed", name);
        }

        private static string RequiredPath(string variable)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
                throw new InvalidOperationException("Missing absolute validation environment variable: " + variable);
            return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool Same(string left, string right) => left != null && right != null &&
            string.Equals(Path.GetFullPath(left).TrimEnd('\\', '/'), Path.GetFullPath(right).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
