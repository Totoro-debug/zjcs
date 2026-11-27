using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.Startup
{
    public static class SaveSelectionPlayModeValidation
    {
        private const string Key = "Zjcs.SaveSelection.Validation";
        public static void Run()
        {
            OfficialStartupTools.RestoreSaveSelection();
            RunInstalled();
        }
        public static void RunInstalled()
        {
            ProjectTools.Validate();
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".playing", false);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
            typeof(StartupPlayModeValidation).GetMethod("ConfigureView", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 800 });
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            Hook();
            EditorApplication.isPlaying = true;
        }
        public static void RunBodyIcons()
        {
            ProjectTools.Validate();
            SessionState.SetBool(Key + ".buildBodyIcons", true);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".playing", false);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
            typeof(StartupPlayModeValidation).GetMethod("ConfigureView", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 800 });
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            Hook();
            EditorApplication.isPlaying = true;
        }
        public static void BuildBodyIconsPlayer()
        {
            ProjectTools.Validate();
            string root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Set ZJCS_VALIDATION_DIR.");
            string destination = System.IO.Path.Combine(root, "Player", "Zjcs.exe");
            var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/Bootstrap.unity" },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Body icon validation Player build failed.");
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
            var events = EventSystem.current;
            if (events != null)
            {
                // Only the validation process receives UI focus. Its module reads
                // the quiet input override, so desktop input and IME stay untouched.
                events.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
                var module = events.GetComponent<StandaloneInputModule>();
                if (module != null)
                {
                    var input = events.GetComponent<QuietInput>() ?? events.gameObject.AddComponent<QuietInput>();
                    module.inputOverride = input;
#pragma warning disable 618
                    module.forceModuleActive = true;
#pragma warning restore 618
                }
            }
            if (!SessionState.GetBool(Key + ".playing", false))
            {
                SessionState.SetBool(Key + ".playing", true);
                SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
            }
            if (!SaveSelectionProbe.Finished && EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + ".deadline", 0)) return;
            SessionState.SetBool(Key + ".passed", SaveSelectionProbe.Passed);
            EditorApplication.isPlaying = false;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".passed", false) && SessionState.GetBool(Key + ".buildBodyIcons", false))
            {
                SessionState.SetBool(Key + ".buildBodyIcons", false);
                BuildBodyIconsPlayer();
            }
            EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
        }
        private sealed class QuietInput : BaseInput
        {
            public override string compositionString => "";
            public override IMECompositionMode imeCompositionMode { get => IMECompositionMode.Off; set { } }
            public override Vector2 compositionCursorPos { get => Vector2.zero; set { } }
            public override bool mousePresent => false;
            public override Vector2 mousePosition => Vector2.zero;
            public override Vector2 mouseScrollDelta => Vector2.zero;
            public override bool touchSupported => false;
            public override int touchCount => 0;
            public override Touch GetTouch(int index) => default;
            public override bool GetMouseButtonDown(int button) => false;
            public override bool GetMouseButtonUp(int button) => false;
            public override bool GetMouseButton(int button) => false;
            public override float GetAxisRaw(string axisName) => 0;
            public override bool GetButtonDown(string buttonName) => false;
        }
    }
}
