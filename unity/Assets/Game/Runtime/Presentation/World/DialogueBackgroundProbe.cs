using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    // Explicit diagnostic switch; never reads or writes a player profile.
    public static class DialogueBackgroundProbe
    {
        private static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-dialogueBackgroundValidation") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Prepare()
        {
            if (!Requested) return;
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            AudioListener.volume = 0;
            Application.runInBackground = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunPlayer()
        {
            if (!Requested) return;
            try { Verify(); Application.Quit(0); }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }

        public static void Verify()
        {
                AudioListener.volume = 0;
                var camera = new GameObject("Dialogue camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.45f, 0.65f, 1);
                camera.orthographic = true;
                var canvas = new GameObject("Dialogue canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
                canvas.GetComponent<Canvas>().worldCamera = camera;
                canvas.GetComponent<Canvas>().planeDistance = 10;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                var events = new GameObject("Dialogue events", typeof(EventSystem)).GetComponent<EventSystem>();
                var target = new RenderTexture(720, 1280, 24);
                camera.targetTexture = target;
                foreach (int map in new[] { 11, 12, 13, 14 })
                foreach (string name in new[] { "TalkDialog", "OptionTalkDialog" })
                {
                    var panel = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map" + map + "/UI/" + name), canvas.transform, false);
                    panel.SetActive(true);
                    DialogueBackgroundAppearance.Apply(panel);
                    var image = panel.transform.Find("Content/CharacterAndTalkContent/TalkContent/BgImg").GetComponent<Image>();
                    Require(image.sprite.border == new Vector4(0, 0, 0, 166), "original tiled border");
                    Require(image.sprite.rect.size == new Vector2(1080, 172), "original texture dimensions");
                    // Isolate the real prefab background from animated text, portraits and masks.
                    foreach (var animator in panel.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.enabled = graphic == image;
                    foreach (var group in panel.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
                    var button = panel.transform.Find("NextBtn").GetComponent<Button>();
                    bool clicked = false;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => clicked = true);
                    var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Require(clicked, "native next button accepts simulated pointer events");
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var pixels = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
                    pixels.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "map" + map + "-" + name + ".png"), pixels.EncodeToPNG());
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(panel);
                }
                Debug.Log("Dialogue background: eight native prefabs rendered; sprite geometry and simulated next pointer events passed.");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
