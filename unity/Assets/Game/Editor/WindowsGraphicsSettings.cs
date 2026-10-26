using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor
{
    /// <summary>Explicit Windows rendering configuration shared by Editor checks and builds.</summary>
    public sealed class WindowsGraphicsSettings : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("Zjcs/Project/Configure Windows DX12")]
        public static void Configure()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12 });
            AssetDatabase.SaveAssets();
            Validate();
            ProjectTools.Configure();
        }

        [MenuItem("Zjcs/Project/Validate Windows DX12")]
        public static void Validate()
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64)
                || apis.Length != 1 || apis[0] != GraphicsDeviceType.Direct3D12)
                throw new BuildFailedException("Windows must explicitly use Direct3D12. Run Zjcs/Project/Configure Windows DX12.");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                throw new BuildFailedException("Restart Unity with -force-d3d12; found " + SystemInfo.graphicsDeviceType);
            Debug.Log("Windows graphics validated: Direct3D12; " + SystemInfo.graphicsDeviceName + "; "
                + SystemInfo.graphicsDeviceVersion);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneWindows64
                || report.summary.platform == BuildTarget.StandaloneWindows)
                Validate();
        }
    }
}
