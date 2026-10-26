using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Zjcs.Editor.Combat
{
    /// <summary>Delivers raycasted simulated pointer input or Windows mouse input to one Game view.</summary>
    public static class BattleGameViewInput
    {
        private static EditorWindow gameView;
        private static bool Simulated => Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED") == "1";

        public static void Focus()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            foreach (EditorWindow old in Resources.FindObjectsOfTypeAll(type))
                if (old.titleContent.text == "Battle input validation"
                    || Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION") == "1") old.Close();
            gameView = ScriptableObject.CreateInstance(type) as EditorWindow;
            gameView.titleContent = new GUIContent("Battle input validation");
            gameView.position = new Rect(80, 60, 450, 821);
            gameView.Show();
            gameView.position = new Rect(80, 60, 450, 821);
            type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance)?.SetValue(gameView, 0);
            if (!Simulated) gameView.Focus();
            gameView.Repaint();
            if (Simulated) return;
            uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out uint owner);
                if (owner != pid) return true;
                var title = new StringBuilder(256);
                GetWindowText(handle, title, title.Capacity);
                if (title.ToString() != "Battle input validation") return true;
                ShowWindow(handle, 5);
                SetForegroundWindow(handle);
                return false;
            }, IntPtr.Zero);
        }

        public static IEnumerator ResizePortrait(float width)
        {
            if (gameView == null) throw new InvalidOperationException("Focus the Game view first.");
            gameView.position = new Rect(80, 60, width, width * 16f / 9f + 21f);
            if (!Simulated) gameView.Focus();
            gameView.Repaint();
            yield return new WaitForSecondsRealtime(0.5f);
            Canvas.ForceUpdateCanvases();
        }

        public static IEnumerator Click(Button button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
                throw new InvalidOperationException("The battle input target is not interactable.");
            if (gameView == null) throw new InvalidOperationException("Focus the Game view first.");
            if (!Simulated) gameView.Focus();
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            string label = button.name;
            yield return ClickScreenPoint(point, label);
        }

        public static IEnumerator ClickScreenPoint(Vector2 point, string label = "battlefield")
        {
            if (gameView == null) throw new InvalidOperationException("Focus the Game view first.");
            if (point.x <= 2f || point.y <= 2f || point.x >= Screen.width - 2f || point.y >= Screen.height - 2f)
                throw new InvalidOperationException("The real input target is outside the interior of the Game view: " + point);
            if (!Simulated) gameView.Focus();
            yield return null;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Debug.Log("Battle input raycast: " + string.Join(", ", hits.ConvertAll(hit => hit.gameObject.name)));
            if (Simulated)
            {
                // User-authorized simulated input: raycast the actual hierarchy and dispatch
                // pointer events through Unity's Button handlers, never invoke onClick directly.
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = point, button = PointerEventData.InputButton.Left,
                    pointerId = -1, clickCount = 1, eligibleForClick = true,
                    pressPosition = point, pointerCurrentRaycast = hits.Count > 0 ? hits[0] : default
                };
                GameObject target = hits.Count > 0 ? hits[0].gameObject : null;
                if (target != null)
                {
                    ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerEnterHandler);
                    pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler)
                        ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                    pointer.rawPointerPress = target;
                    pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
                }
                foreach (var panel in UnityEngine.Object.FindObjectsOfType<Zjcs.Official.WorldUI.WorldFunctionEntrysPanelBase>())
                {
                    Canvas canvas = panel.GetComponentInParent<Canvas>();
                    panel.HandleGlobalPointerDown(point,
                        canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                        false); // This fixture has no open Panel/Dialog.
                }
                yield return new WaitForSecondsRealtime(0.12f);
                if (pointer.pointerPress != null)
                {
                    ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                    var releaseHits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, releaseHits);
                    GameObject released = releaseHits.Count > 0
                        ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(releaseHits[0].gameObject) : null;
                    if (pointer.pointerPress == released)
                        ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerClickHandler);
                }
                Debug.Log("Battle simulated pointer click: " + label + " target=" + point
                    + " press=" + pointer.pointerPress?.name);
                yield return new WaitForSecondsRealtime(0.12f);
                yield break;
            }
            Rect area = gameView.position;
            // A free-aspect Game view has no letterboxing. The toolbar is outside the render area.
            const float toolbar = 21f;
            float x = area.x + point.x * area.width / Screen.width;
            float y = area.y + toolbar + (Screen.height - point.y) * (area.height - toolbar) / Screen.height;
            float dpi = EditorGUIUtility.pixelsPerPoint;
            int desktopX = GetSystemMetrics(76), desktopY = GetSystemMetrics(77);
            int desktopWidth = GetSystemMetrics(78), desktopHeight = GetSystemMetrics(79);
            SendMouse(new MouseInput
            {
                dx = Mathf.RoundToInt((x * dpi - desktopX) * 65535f / (desktopWidth - 1)),
                dy = Mathf.RoundToInt((y * dpi - desktopY) * 65535f / (desktopHeight - 1)),
                flags = 0xC001 // MOVE | ABSOLUTE | VIRTUALDESK
            });
            yield return null;
            yield return null;
            // Floating-window decorations differ across editor layouts/DPI settings. Correct
            // the physical cursor using Unity's observed position, without dispatching UI callbacks.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Vector2 actual = UnityEngine.Input.mousePosition;
                if (Vector2.Distance(actual, point) <= 2f) break;
                GetCursorPos(out DesktopPoint before);
                float correctedX = before.x + (point.x - actual.x) * area.width * dpi / Screen.width;
                float correctedY = before.y - (point.y - actual.y) * (area.height - toolbar) * dpi / Screen.height;
                SendMouse(new MouseInput
                {
                    dx = Mathf.RoundToInt((correctedX - desktopX) * 65535f / (desktopWidth - 1)),
                    dy = Mathf.RoundToInt((correctedY - desktopY) * 65535f / (desktopHeight - 1)),
                    flags = 0xC001
                });
                yield return null;
                yield return null;
            }
            GetCursorPos(out DesktopPoint cursor);
            Debug.Log("Battle mouse move: desktop=" + cursor.x + "," + cursor.y
                + " unity=" + UnityEngine.Input.mousePosition + " target=" + point
                + " focused=" + Application.isFocused + " view=" + area + " screen=" + Screen.width + "x" + Screen.height);
            if (Vector2.Distance(UnityEngine.Input.mousePosition, point) > 3f)
                throw new InvalidOperationException("The real cursor did not reach the battle control.");
            SendMouse(new MouseInput { flags = 0x0002 });
            yield return new WaitForSecondsRealtime(0.12f);
            var actualHits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                { position = UnityEngine.Input.mousePosition }, actualHits);
            Debug.Log("Battle physical press: down=" + UnityEngine.Input.GetMouseButton(0)
                + " focusedWindow=" + EditorWindow.focusedWindow?.GetType().Name
                + " hits=" + string.Join(", ", actualHits.ConvertAll(hit => hit.gameObject.name)));
            SendMouse(new MouseInput { flags = 0x0004 });
            Debug.Log("Battle real input: " + label + " at " + x + "," + y);
            yield return new WaitForSecondsRealtime(0.12f);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int dx, dy;
            public uint data, flags, time;
            public UIntPtr extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Input { public uint type; public MouseInput mouse; }
        [StructLayout(LayoutKind.Sequential)]
        private struct DesktopPoint { public int x, y; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out DesktopPoint point);
        private static void SendMouse(MouseInput mouse)
        {
            var input = new Input { mouse = mouse };
            if (SendInput(1, new[] { input }, Marshal.SizeOf(input)) != 1)
                throw new InvalidOperationException("Windows rejected battle mouse input: "
                    + Marshal.GetLastWin32Error());
        }
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        private delegate bool EnumWindow(IntPtr handle, IntPtr argument);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr argument);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint owner);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr handle, StringBuilder title, int count);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);
    }
}
