using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zjcs.Editor
{
    // Dynamically loaded official assemblies are invisible to the static BCL
    // dependency scan. Ship the pinned Mono facade without rewriting their IL.
    public sealed class MainlineWindowsCompatibilityBuild : IPostprocessBuildWithReport
    {
        [Serializable] private sealed class Source
        {
            public string editor_version, relative_source, assembly_identity, sha256;
        }
        public int callbackOrder => 0;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            string managed = Path.Combine(Path.GetDirectoryName(report.summary.outputPath),
                Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data", "Managed");
            if (!Directory.Exists(managed)) return;
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/official-map/mainline-windows-compatibility-source-manifest.json")));
            if (source == null || Application.unityVersion != source.editor_version)
                throw new BuildFailedException("The pinned Windows Mono compatibility source is unbound.");
            string input = Path.Combine(EditorApplication.applicationContentsPath, source.relative_source);
            if (AssemblyName.GetAssemblyName(input).FullName != source.assembly_identity)
                throw new BuildFailedException("The Windows Mono compatibility assembly identity differs.");
            using (var hash = SHA256.Create())
            {
                string actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(input)))
                    .Replace("-", "").ToLowerInvariant();
                if (actual != source.sha256)
                    throw new BuildFailedException("The pinned Windows Mono compatibility hash differs.");
            }
            File.Copy(input, Path.Combine(managed, "System.Memory.dll"), true);
            Debug.Log("Windows original client BCL facade verified and packaged: System.Memory.");
        }
    }
}
