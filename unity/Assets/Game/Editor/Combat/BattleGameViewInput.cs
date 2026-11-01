using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Zjcs.Editor.Combat
{
    /// <summary>Delivers raycasted simulated pointer input to one Game view.</summary>
    public static class BattleGameViewInput
    {
        private static EditorWindow gameView;

        private static Rect ValidationRect(float width, float height)
        {
            // unity_internal_display.ps1 positions the editor by the built-in panel's hardware
            // identity. Keep this auxiliary window beside that editor, in Unity desktop points.
            Rect editor = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(editor.x + 40f, editor.y + 40f, width, height);
        }

        public static void Focus()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
            foreach (EditorWindow old in Resources.FindObjectsOfTypeAll(type))
                if (old.titleContent.text == "Battle input validation"
                    || Application.isBatchMode
                    || Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION") == "1") old.Close();
            gameView = ScriptableObject.CreateInstance(type) as EditorWindow;
            gameView.titleContent = new GUIContent("Battle input validation");
            gameView.position = ValidationRect(450, 821);
            gameView.Show();
            gameView.position = ValidationRect(450, 821);
            type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance)?.SetValue(gameView, 0);
            gameView.Repaint();

        }

        public static IEnumerator ResizePortrait(float width)
        {
            if (gameView == null) throw new InvalidOperationException("Focus the Game view first.");
            gameView.position = ValidationRect(width, width * 16f / 9f + 21f);
            gameView.Repaint();
            yield return new WaitForSecondsRealtime(0.5f);
            Canvas.ForceUpdateCanvases();
        }

        public static IEnumerator Click(Button button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
                throw new InvalidOperationException("The battle input target is not interactable.");
            if (gameView == null) throw new InvalidOperationException("Focus the Game view first.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            string label = button.name;
            yield return ClickScreenPoint(point, label);
        }

        /// <summary>
        /// Converts a point returned by an editor overlay canvas into the logical
        /// Game View surface used by EventSystem raycasts.
        /// </summary>
        public static Vector2 NormalizeScreenPoint(Vector2 point)
        {
            if (point.x >= 0f && point.y >= 0f && point.x <= Screen.width && point.y <= Screen.height)
                return point;
            if (!TryGetGameViewContent(out Rect window, out Rect target, out float pixelsPerPoint))
                return point;

            float desktopWidth = Display.main == null ? Screen.width : Display.main.systemWidth;
            float desktopHeight = Display.main == null ? Screen.height : Display.main.systemHeight;
            float scale = target.width / Mathf.Max(1f, Screen.width);
            float x = point.x / pixelsPerPoint - window.x - target.x;
            float yFromTop = desktopHeight / pixelsPerPoint - point.y / pixelsPerPoint
                - window.y - target.y;
            return new Vector2(x / scale, (target.height - yFromTop) / scale);
        }

        /// <summary>
        /// Captures only the rendered Game View content. Editor screenshots otherwise
        /// include the entire desktop when the Game View is embedded or scaled.
        /// </summary>
        public static IEnumerator CaptureGameViewScreenshot(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A screenshot path is required.", nameof(path));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string fullPath = path + ".full.png";
            if (File.Exists(fullPath)) File.Delete(fullPath);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(fullPath);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!File.Exists(fullPath) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!File.Exists(fullPath))
                throw new InvalidOperationException("The Game View screenshot was not written: " + fullPath);

            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(bytes, false))
                    throw new InvalidOperationException("The Game View screenshot is not a valid PNG: " + fullPath);
                if (!TryGetGameViewContent(out Rect window, out Rect target, out float pixelsPerPoint))
                {
                    File.Copy(fullPath, path, true);
                    yield break;
                }

                int left = Mathf.Clamp(Mathf.RoundToInt((window.x + target.x) * pixelsPerPoint), 0, source.width - 1);
                int top = Mathf.Clamp(Mathf.RoundToInt((window.y + target.y) * pixelsPerPoint), 0, source.height - 1);
                int width = Mathf.Clamp(Mathf.RoundToInt(target.width * pixelsPerPoint), 1, source.width - left);
                int height = Mathf.Clamp(Mathf.RoundToInt(target.height * pixelsPerPoint), 1, source.height - top);
                int bottom = source.height - top - height;
                bottom = Mathf.Clamp(bottom, 0, source.height - height);
                Texture2D cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    cropped.SetPixels(source.GetPixels(left, bottom, width, height));
                    cropped.Apply(false, false);
                    File.WriteAllBytes(path, cropped.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(cropped); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
        }

        public static IEnumerator ClickScreenPoint(Vector2 point, string label = "battlefield")
        {
            if (gameView == null) throw new InvalidOperationException("Focus the Game view first.");
            if (point.x <= 2f || point.y <= 2f || point.x >= Screen.width - 2f || point.y >= Screen.height - 2f)
                throw new InvalidOperationException("The simulated input target is outside the interior of the Game view: " + point);
            yield return null;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Debug.Log("Battle input raycast: " + string.Join(", ", hits.ConvertAll(hit => hit.gameObject.name)));
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

        private static bool TryGetGameViewContent(out Rect window, out Rect target, out float pixelsPerPoint)
        {
            window = default;
            target = default;
            pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type == null) return false;
            EditorWindow view = gameView;
            if (view == null)
                view = Resources.FindObjectsOfTypeAll(type).OfType<EditorWindow>().FirstOrDefault();
            if (view == null) return false;
            PropertyInfo targetProperty = type.GetProperty("targetInView",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (targetProperty == null) return false;
            object value = targetProperty.GetValue(view, null);
            if (!(value is Rect content) || content.width <= 1f || content.height <= 1f) return false;
            window = view.position;
            target = content;
            return true;
        }
    }
}
