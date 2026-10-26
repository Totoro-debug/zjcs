using UnityEditor;
using UnityEditor.Build;

namespace Zjcs.Editor.Startup
{
    public static class SaveSelectionWindowsBuild
    {
        public static void Run()
        {
            ScriptingImplementation previous = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            try
            {
                // Reuse the installed Windows Mono support and the existing desktop build cache.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                ProjectTools.BuildWindowsGame();
            }
            finally { PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previous); }
        }
    }
}
