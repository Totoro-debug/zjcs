using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Offline.Startup
{
    /// <summary>Checks the actual Windows device during the explicit Player startup probe.</summary>
    public static class WindowsGraphicsValidation
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--zjcs-validate-startup") < 0) return;
            Application.runInBackground = true;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
            {
                Debug.LogError("Windows startup validation requires Direct3D12; found " + SystemInfo.graphicsDeviceType);
                Application.Quit(1);
                return;
            }
            Debug.Log("Windows Player graphics validated: Direct3D12; " + SystemInfo.graphicsDeviceName + "; "
                + SystemInfo.graphicsDeviceVersion);
#endif
        }
    }
}
