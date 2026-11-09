using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static class OfficialEmbeddedMecanimValidation
    {
        private const string Key = "Zjcs.EmbeddedMecanim.Validation";

        public static void Run()
        {
            ProjectTools.Validate();
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            typeof(Skills.SkillPlayModeAcceptance).GetMethod("ConfigurePortraitGameView",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { 600, 800 });
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".exit", 1);
            SessionState.SetBool(Key + ".async", EditorSettings.asyncShaderCompilation);
            EditorSettings.asyncShaderCompilation = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            Debug.Log("[Embedded Mecanim] Editor mode: " + mode);
            if (mode == PlayModeStateChange.EnteredPlayMode)
                OfficialEmbeddedMecanimProbe.StartDiagnostic(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                    (passed, error) => {
                        SessionState.SetInt(Key + ".exit", passed ? 0 : 1);
                        EditorApplication.isPlaying = false;
                    });
            if (mode == PlayModeStateChange.EnteredEditMode)
            {
                EditorSettings.asyncShaderCompilation = SessionState.GetBool(Key + ".async", true);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
            }
        }

    }
}
