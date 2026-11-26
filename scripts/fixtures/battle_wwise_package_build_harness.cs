using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnityEngine
{
    public static class Application { public static string dataPath; }
    public class TextAsset { public string text; }
    public static class Resources
    {
        public static string Catalog;
        public static T Load<T>(string path) where T : class
            => new TextAsset { text = Catalog } as T;
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string text) => JsonSerializer.Deserialize<T>(text,
            new JsonSerializerOptions { IncludeFields = true });
    }
    public static class Debug { public static void Log(object text) { } }
}
namespace UnityEditor
{
    public enum BuildTarget { StandaloneWindows64 }
}
namespace UnityEditor.Build.Reporting
{
    public class Summary { public UnityEditor.BuildTarget platform; public string outputPath; }
    public class BuildReport { public Summary summary; }
}
namespace UnityEditor.Build
{
    public interface IPreprocessBuildWithReport
    { int callbackOrder { get; } void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public interface IPostprocessBuildWithReport
    { int callbackOrder { get; } void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public class BuildFailedException : Exception { public BuildFailedException(string text) : base(text) { } }
}
namespace Zjcs.Offline.BattleNativePlayables
{
    public static class OfflineBattleWwisePackages
    {
        public const string AssetDirectory = "Assets/Official/Combat/NativeSound/Packages";
        public const string StreamingDirectory = "Combat/Wwise";
        public static string DirectoryPath => Path.Combine(UnityEngine.Application.dataPath,
            "Official/Combat/NativeSound/Packages");
    }
}
public static class BattleWwisePackageBuildHarness
{
    static string root, baselinePath, routingPath, packagePath;
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static object Identity(string relative, byte[] bytes)
        => new { path = relative, sha256 = Hash(bytes), bytes = bytes.Length };
    static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value));
    static void Reject(Action action, string reason)
    {
        try { action(); }
        catch (UnityEditor.Build.BuildFailedException) { return; }
        throw new Exception("Invalid sound package accepted: " + reason);
    }
    public static string Run(string workspace)
    {
        root = Path.Combine(workspace, "wwise-package-build-" + Guid.NewGuid().ToString("N"));
        UnityEngine.Application.dataPath = Path.Combine(root, "unity/Assets");
        packagePath = Path.Combine(root, "unity/" + Zjcs.Offline.BattleNativePlayables.OfflineBattleWwisePackages.AssetDirectory,
            "Minimum.pck");
        baselinePath = Path.Combine(root, "assets/reference/battle-animation/windows-sound-runtime-source-manifest.json");
        routingPath = Path.Combine(root, "assets/reference/battle-animation/windows-sound-routing-source-manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath));
        Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
        byte[] oldBytes = Encoding.UTF8.GetBytes("original official routing");
        byte[] newBytes = Encoding.UTF8.GetBytes("current official routing with changed equalizer");
        string relative = "unity/" + Zjcs.Offline.BattleNativePlayables.OfflineBattleWwisePackages.AssetDirectory + "/Minimum.pck";
        var original = Identity(relative, oldBytes);
        Write(baselinePath, new { manifest = "original-source", outputs = new[] { original } });
        UnityEngine.Resources.Catalog = JsonSerializer.Serialize(new {
            manifest = "original-source", sdk_version = "2022.1.18.8567",
            packages = new[] { new { file = "Minimum.pck", bank = "Minimum" } } });
        File.WriteAllBytes(packagePath, oldBytes);
        Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate();
        File.WriteAllBytes(packagePath, newBytes);
        Reject(Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate, "changed bytes without source receipt");
        var replacement = new { previous_output = original, output = Identity(relative, newBytes),
            source_package = "original-minimum-source" };
        var source = Identity("assets/reference/battle-animation/windows-sound-runtime-source-manifest.json",
            File.ReadAllBytes(baselinePath));
        var routing = new { manifest = "current-source", base_source = source, overrides = new[] { replacement } };
        Write(routingPath, routing);
        Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate();
        File.AppendAllText(baselinePath, " ");
        Reject(Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate, "changed base receipt");
        Write(baselinePath, new { manifest = "original-source", outputs = new[] { original } });
        Write(routingPath, new { manifest = "current-source", base_source = source,
            overrides = new[] { replacement, replacement } });
        Reject(Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate, "duplicate replacement");
        Write(routingPath, new { manifest = "current-source", base_source = source, overrides = new[] {
            new { previous_output = Identity(relative, newBytes), output = Identity(relative, newBytes),
                source_package = "original-minimum-source" } } });
        Reject(Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate, "replacement against a different original");
        Write(routingPath, routing);
        File.WriteAllBytes(packagePath, new byte[] { 1, 2 });
        Reject(Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate, "truncated current package");
        var corrupted = (byte[])newBytes.Clone(); corrupted[0] ^= 1;
        File.WriteAllBytes(packagePath, corrupted);
        Reject(Zjcs.Editor.OfficialBattleWwisePackageBuild.Validate, "same-size corrupt current package");
        File.WriteAllBytes(packagePath, newBytes);
        string executable = Path.Combine(root, "Player/Zjcs.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)); File.WriteAllBytes(executable, new byte[] { 0 });
        string previousEnvironment = Environment.GetEnvironmentVariable("ZJCS_WINDOWS_RELEASE_EXE");
        try
        {
            Environment.SetEnvironmentVariable("ZJCS_WINDOWS_RELEASE_EXE", executable);
            Zjcs.Editor.OfficialBattleWwisePackageBuild.DeliverConfiguredPlayer();
            string delivered = Path.Combine(root, "Player/Zjcs_Data/StreamingAssets/Combat/Wwise/Minimum.pck");
            if (Hash(File.ReadAllBytes(delivered)) != Hash(newBytes))
                throw new Exception("Current verified routing was not delivered to the isolated player");
            File.WriteAllBytes(delivered, oldBytes);
            Zjcs.Editor.OfficialBattleWwisePackageBuild.DeliverConfiguredPlayer();
            if (Hash(File.ReadAllBytes(delivered)) != Hash(newBytes))
                throw new Exception("Changed player package was not replaced atomically");
        }
        finally { Environment.SetEnvironmentVariable("ZJCS_WINDOWS_RELEASE_EXE", previousEnvironment); }
        return "Windows Wwise package sources passed: historical/current bindings, six corrupt source/package rejections, verified delivery and replacement.";
    }
}
