using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.Startup
{
    public static class SaveSelectionPlayModeValidation
    {
        private const string Key = "Zjcs.SaveSelection.Validation";
        public static void Run()
        {
            OfficialStartupTools.RestoreSaveSelection();
            ProjectTools.Validate();
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
            typeof(StartupPlayModeValidation).GetMethod("ConfigureView", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 800 });
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            EditorApplication.update -= Update; EditorApplication.update += Update;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Update()
        {
            SaveSelectionProbe.ResizeView = (width, height) => typeof(StartupPlayModeValidation).GetMethod("ConfigureView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { width, height });
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (!SaveSelectionProbe.Finished && EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + ".deadline", 0)) return;
            SessionState.SetBool(Key + ".passed", SaveSelectionProbe.Passed);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
        }
    }
}
