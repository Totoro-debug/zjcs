using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Official.BattleDamageUI;


namespace Zjcs.Editor
{
    public static class OfficialBattlePublicMovementAcceptance
    {
        private const string Key = "Zjcs.BattlePublicMovement.Acceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            RunIsolated();
        }
        public static void RunIsolated()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            ConfigureBackgroundGameView();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        private static void ConfigureBackgroundGameView()
        {
            // EditorWindow.Show/GetWindow always focus a newly created native window in
            // this editor version. Use the verified ContainerWindow entry with setFocus=false.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var editor = typeof(EditorWindow).Assembly;
            var sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] {
                Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType", true), "Standalone") });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null), selected = -1;
            for (int i = 0; i < count; i++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)size.GetType().GetProperty("width").GetValue(size) == 450
                    && (int)size.GetType().GetProperty("height").GetValue(size) == 975) { selected = i; break; }
            }
            if (selected < 0)
            {
                var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize", true), flags,
                    null, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType", true), 1),
                        (object)450, 975, "Public battle verification" }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = count;
            }
            var viewType = editor.GetType("UnityEditor.GameView", true);
            foreach (EditorWindow old in Resources.FindObjectsOfTypeAll(viewType)) old.Close();
            var view = (EditorWindow)ScriptableObject.CreateInstance(viewType);
            var bounds = EditorGUIUtility.GetMainWindowPosition();
            view.titleContent = new GUIContent("Public battle verification");
            view.position = new Rect(bounds.x + 40, bounds.y + 40, 450, 996);
            var hostType = editor.GetType("UnityEditor.HostView", true);
            var host = ScriptableObject.CreateInstance(hostType);
            hostType.GetProperty("actualView", flags).SetValue(host, view);
            var containerType = editor.GetType("UnityEditor.ContainerWindow", true);
            var container = ScriptableObject.CreateInstance(containerType);
            containerType.GetProperty("position", flags).SetValue(container, view.position);
            containerType.GetProperty("rootView", flags).SetValue(container, host);
            typeof(EditorWindow).GetMethod("MakeParentsSettingsMatchMe", flags).Invoke(view, null);
            containerType.GetMethod("Show", flags).Invoke(container, new[] {
                Enum.Parse(editor.GetType("UnityEditor.ShowMode", true), "NormalWindow"),
                (object)false, true, false });
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, selected);
            view.Repaint();
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Public movement battle timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            AudioListener.volume = 0f;
            Application.logMessageReceived += Observe;
            new GameObject("Public movement verification runner").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>();
            Action<string> capture = name => ScreenCapture.CaptureScreenshot(
                Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"));
            stack.Push(Steps(capture));
            bool passed = false;
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
                passed = errors == 0;
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                SessionState.SetInt(Key + ".result", passed ? 0 : 1);
                EditorApplication.isPlaying = false;
            }
        }
        public static IEnumerator Steps(Action<string> capture) =>
            Zjcs.Validation.Combat.OfficialBattlePublicFlowValidation.Steps(capture);

        private sealed class Runner : MonoBehaviour { }
    }
}
