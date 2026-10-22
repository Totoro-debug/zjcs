using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Opening;

namespace Zjcs.Editor.CharacterCreation
{
    /// <summary>
    /// Real-input acceptance: drives the opening story with an OS mouse click delivered to
    /// the Game view window instead of invoking the button's onClick or pointer handlers.
    /// Run with a visible Game view; the window is focused before the click.
    /// </summary>
    public static class CharacterOpeningRealInputPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.OpeningInput.Active";
        private const string ResultKey = "Zjcs.OpeningInput.Result";
        private const string DeadlineKey = "Zjcs.OpeningInput.Deadline";
        private const string SlotKey = "Zjcs.OpeningInput.Slot";
        private static bool started;

        [MenuItem("Zjcs/Validate/Opening Real Input Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the real-input acceptance.");
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetString(SlotKey, "input-" + Guid.NewGuid().ToString("N"));
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 300f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            CharacterOpeningFlow.SkipOpeningForAcceptance = false;
            CharacterOpeningFlow.ForceSkipButtonForAcceptance = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Locate;
            EditorApplication.update += Locate;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.update -= Locate;
            EditorApplication.update += Locate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Locate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode
                || Environment.GetEnvironmentVariable("ZJCS_OPENING_INPUT_AUTOMATION") == "1")
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Locate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Opening real-input acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            started = true;
            var host = new GameObject("OpeningInputHost");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<InputRunner>();
        }

        internal static void Finish(bool passed, Exception error)
        {
            if (SessionState.GetInt(ResultKey, 1) != 2) return;
            if (error != null)
            {
                Debug.LogException(error);
                Debug.LogError("Opening real-input acceptance failed: " + error.Message);
            }
            else
            {
                Debug.Log("Opening real-input acceptance passed: an OS mouse click on the official NextBtn "
                          + "advanced the BeforeFight dialogue.");
            }
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private sealed class InputRunner : MonoBehaviour
        {
            private void Start()
            {
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                var host = new GameObject("OpeningInputFlow");
                CharacterOpeningFlow flow = host.AddComponent<CharacterOpeningFlow>();
                flow.Begin(SessionState.GetString(SlotKey, "input"), () => { }, _ => { });

                // Wait for the official BeforeFight line to be presented.
                TMP_Text target = null;
                float deadline = Time.realtimeSinceStartup + 90f;
                while (target == null && Time.realtimeSinceStartup < deadline)
                {
                    target = FindText("又到最后的时刻了，我们上吧！");
                    yield return null;
                }
                if (target == null) throw new InvalidOperationException("The BeforeFight dialogue never appeared.");
                string before = target.text;

                Button next = FindButton("NextBtn");
                if (next == null) throw new InvalidOperationException("The official NextBtn was not found.");
                FocusGameView();
                yield return null;
                if (!ClickAt(next))
                    throw new InvalidOperationException("The NextBtn screen position could not be mapped to the Game view.");

                // The real click must be consumed by the EventSystem and advance the line.
                float wait = Time.realtimeSinceStartup + 10f;
                bool advanced = false;
                while (Time.realtimeSinceStartup < wait)
                {
                    TMP_Text current = FindText("又到最后的时刻了，我们上吧！");
                    if (current == null || current.text != before) { advanced = true; break; }
                    yield return null;
                }
                if (!advanced)
                    throw new InvalidOperationException("The OS click did not advance the official dialogue.");
                UnityEngine.Object.Destroy(host);
                Finish(true, null);
            }
        }

        private static TMP_Text FindText(string value)
        {
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true))
                if (text.isActiveAndEnabled && text.text != null && text.text.Contains(value)) return text;
            return null;
        }

        private static Button FindButton(string suffix)
        {
            foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
                if (button.gameObject.activeInHierarchy && button.interactable
                    && button.name.EndsWith(suffix, StringComparison.Ordinal)) return button;
            return null;
        }

        private static void FocusGameView()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return;
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type))
            {
                window.Focus();
                window.Repaint();
                return;
            }
        }

        /// <summary>Map a screen-space UI point to the Game view window and click it with the OS cursor.</summary>
        private static bool ClickAt(Button button)
        {
            var rect = (RectTransform)button.transform;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            Rect window = GameViewContent();
            if (window.width <= 1f || window.height <= 1f) return false;
            float x = window.x + screenPoint.x * window.width / Mathf.Max(1, Screen.width);
            float y = window.y + (Screen.height - screenPoint.y) * window.height / Mathf.Max(1, Screen.height);
            if (x < 0 || y < 0 || x >= UnityEngine.Screen.currentResolution.width
                || y >= UnityEngine.Screen.currentResolution.height)
                return false;
            SetCursorPos((int)Mathf.Round(x), (int)Mathf.Round(y));
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            Debug.Log("[Opening input] OS click at " + x.ToString("F0") + "," + y.ToString("F0")
                      + " (screen " + screenPoint.x.ToString("F0") + "," + screenPoint.y.ToString("F0") + ")");
            return true;
        }

        /// <summary>Game view content rect in desktop coordinates, excluding the toolbar.</summary>
        private static Rect GameViewContent()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return new Rect(0, 0, 0, 0);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type))
            {
                Rect position = window.position;
                const float toolbar = 21f;
                return new Rect(position.x + 1f, position.y + toolbar, position.width - 2f,
                    Mathf.Max(1f, position.height - toolbar - 1f));
            }
            return new Rect(0, 0, 0, 0);
        }

        private const int MouseEventLeftDown = 0x0002;
        private const int MouseEventLeftUp = 0x0004;

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int flags, int dx, int dy, int data, UIntPtr extraInfo);
    }
}
