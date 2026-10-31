using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
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
    }
}
