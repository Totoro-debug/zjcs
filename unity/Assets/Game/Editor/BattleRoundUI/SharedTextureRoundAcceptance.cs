using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Editor.BattleRound;

namespace Zjcs.Editor.BattleRoundUI
{
    // Run the original native-round interaction fixture with a background Game
    // view. Do not call the legacy launcher that opens a focused floating view.
    public static class SharedTextureRoundAcceptance
    {
        private const string Key = "Zjcs.SharedTextureRound";
        private static bool started;
        private static int errors;
        private static double deadline;

        [Serializable] private sealed class TextureRecord { public string path, guid; }
        [Serializable] private sealed class Alias { public TextureRecord source, canonical; }
        [Serializable] private sealed class Receipt { public Alias[] textures; }

        public static void Run()
        {
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/shared-textures-source-manifest.json")));
            var seen = new HashSet<string>();
            foreach (var row in receipt.textures)
            {
                string path = row.canonical.path.Substring("unity/".Length);
                if (!seen.Add(path)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Require(texture && !texture.isReadable, "Shared original texture failed to import: " + path);
                Require(AssetDatabase.AssetPathToGUID(path) == row.canonical.guid,
                    "Shared original texture GUID changed: " + path);
            }
            Debug.Log("SHARED_TEXTURE_IMPORT_PASS " + seen.Count);
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            ConfigureBackgroundView();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            Hook();
            EditorApplication.isPlaying = true;
        }

        private static void ConfigureBackgroundView()
        {
            var editor = typeof(EditorWindow).Assembly;
            var sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                .GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), flags,
                null, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1),
                    450, 975, "Shared texture background validation" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var viewType = editor.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType, false, "Game", false);
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(view,
                (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
            view.Repaint();
        }

        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 360;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Debug.LogError("Shared texture round validation timed out.");
                Finish(false);
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            AudioListener.volume = 0;
            Application.logMessageReceived += Observe;
            new GameObject("Shared original texture validation").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static void Observe(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private static IEnumerator Verify()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Action<string> capture = name => ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            var steps = (IEnumerator)typeof(OfficialBattleRoundSceneAcceptance).GetMethod("Steps",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { capture });
            var stack = new Stack<IEnumerator>();
            stack.Push(steps);
            bool passed = false;
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    bool next = false;
                    bool failed = false;
                    try { next = current.MoveNext(); }
                    catch (Exception error) { Debug.LogException(error); failed = true; }
                    if (failed) yield break;
                    if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
                passed = errors == 0;
                if (passed) Debug.Log("SHARED_TEXTURE_ROUND_PLAY_PASS");
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                Finish(passed);
            }
        }

        private static void Finish(bool passed)
        {
            SessionState.SetInt(Key + ".result", passed ? 0 : 1);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Observe;
            EditorApplication.isPlaying = false;
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.EraseBool(Key);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Require(bool valid, string reason)
        {
            if (!valid) throw new InvalidOperationException(reason);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
