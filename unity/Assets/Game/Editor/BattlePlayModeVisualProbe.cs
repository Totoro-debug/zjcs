using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static class BattlePlayModeVisualProbe
    {
        private const string ActiveKey = "Zjcs.Battle.VisualProbe.Active";
        private const string ScreenshotKey = "Zjcs.Battle.VisualProbe.Screenshot";
        private static bool captured;
        private static double captureAt = -1d;

        [InitializeOnLoadMethod]
        private static void RestoreHooks()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Hook();
        }

        [InitializeOnLoadMethod]
        private static void RunFromCommandLine()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--battle-playmode-visual-probe") >= 0)
                EditorApplication.delayCall += Run;
        }

        [MenuItem("Zjcs/Validate/Map10 Battle Visual")]
        public static void Run()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(ScreenshotKey,
                Path.Combine(Path.GetTempPath(), "zjcs-map10-battle-playmode.png"));
            captured = false;
            captureAt = -1d;
            Hook();
            OfficialMap10BattlePlayModeAcceptance.Run();
        }

        private static void Hook()
        {
            EditorApplication.update -= CaptureWhenBattleIsMounted;
            EditorApplication.update += CaptureWhenBattleIsMounted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void CaptureWhenBattleIsMounted()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying || captured) return;
            OfficialBattleRuntimeController controller = UnityEngine.Object.FindObjectOfType<OfficialBattleRuntimeController>();
            if (controller == null || controller.Checkpoint == null) return;
            if (captureAt < 0d)
            {
                captureAt = EditorApplication.timeSinceStartup + 0.2d;
                return;
            }
            if (EditorApplication.timeSinceStartup < captureAt) return;
            ScreenCapture.CaptureScreenshot(SessionState.GetString(ScreenshotKey, string.Empty));
            captured = true;
            Debug.Log("Battle Play Mode screenshot requested: " + SessionState.GetString(ScreenshotKey, string.Empty));
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= CaptureWhenBattleIsMounted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SessionState.SetBool(ActiveKey, false);
        }
    }
}
