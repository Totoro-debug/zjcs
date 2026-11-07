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
            // Some original buttons place the clickable graphic away from the container's
            // centre. Use an actual raycast into that graphic, preserving its original hit area.
            var hits = new List<RaycastResult>();
            Func<Vector2, bool> targetsButton = candidate =>
            {
                hits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = candidate }, hits);
                return hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject;
            };
            if (!targetsButton(point))
                foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.raycastTarget) continue;
                    Vector2 candidate = RectTransformUtility.WorldToScreenPoint(camera,
                        graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center));
                    if (targetsButton(candidate)) { point = candidate; break; }
                }
            // Full-screen "click blank space to close" buttons deliberately
            // sit behind the model. Find an exposed point in their original
            // graphic without changing raycast targets or invoking callbacks.
            if (!targetsButton(point))
                foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.raycastTarget) continue;
                    bool found = false;
                    foreach (float y in new[] { .1f, .5f, .9f })
                    {
                        foreach (float x in new[] { .1f, .5f, .9f })
                        {
                            Rect area = graphic.rectTransform.rect;
                            var local = new Vector2(Mathf.Lerp(area.xMin, area.xMax, x),
                                Mathf.Lerp(area.yMin, area.yMax, y));
                            Vector2 candidate = RectTransformUtility.WorldToScreenPoint(camera,
                                graphic.rectTransform.TransformPoint(local));
                            if (candidate.x < 0 || candidate.y < 0 || candidate.x >= Screen.width
                                || candidate.y >= Screen.height || !targetsButton(candidate)) continue;
                            point = candidate; found = true; break;
                        }
                        if (found) break;
                    }
                    if (found) break;
                }
            if (!targetsButton(point))
                throw new InvalidOperationException("No exposed native hit area for " + button.name
                    + "; raycast=" + string.Join(", ", hits.ConvertAll(hit => hit.gameObject.name)));
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
            float desktopWidth = Display.main == null ? Screen.width : Display.main.systemWidth;
            float desktopHeight = Display.main == null ? Screen.height : Display.main.systemHeight;
            if (desktopWidth > Screen.width || desktopHeight > Screen.height)
            {
                Vector2 proportional = new Vector2(point.x * Screen.width / Mathf.Max(1f, desktopWidth),
                    point.y * Screen.height / Mathf.Max(1f, desktopHeight));
                if (proportional.x >= 0f && proportional.y >= 0f
                    && proportional.x <= Screen.width && proportional.y <= Screen.height)
                    return proportional;
            }
            if (!TryGetGameViewContent(out Rect window, out Rect target, out float pixelsPerPoint))
                return point;

            float scale = target.width / Mathf.Max(1f, Screen.width);
            float x = point.x / pixelsPerPoint - window.x - target.x;
            float yFromTop = desktopHeight / pixelsPerPoint - point.y / pixelsPerPoint
                - window.y - target.y;
            return new Vector2(x / scale, (target.height - yFromTop) / scale);
        }

        /// <summary>
        /// Saves the full game render surface, independent of the editor window's
        /// desktop position, display scaling, and Game View zoom.
        /// </summary>
        public static IEnumerator CaptureGameViewScreenshot(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A screenshot path is required.", nameof(path));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            if (frame == null) throw new InvalidOperationException("The Game View render surface was not captured.");
            try
            {
                if (frame.width != Screen.width || frame.height != Screen.height)
                    throw new InvalidOperationException("The screenshot does not cover the full game render surface.");
                File.WriteAllBytes(path, frame.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(frame); }
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
            PropertyInfo targetProperty = type.GetProperty("targetInView",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (targetProperty == null) return false;
            EditorWindow bestView = null;
            Rect bestContent = default;
            float bestArea = 0f;
            foreach (EditorWindow candidate in Resources.FindObjectsOfTypeAll(type).OfType<EditorWindow>())
            {
                object value = targetProperty.GetValue(candidate, null);
                if (!(value is Rect content) || content.width <= 1f || content.height <= 1f) continue;
                float area = content.width * content.height;
                if (area <= bestArea) continue;
                bestView = candidate;
                bestContent = content;
                bestArea = area;
            }
            if (bestView == null) return false;
            window = bestView.position;
            target = bestContent;
            return true;
        }
    }
}
