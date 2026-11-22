using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Offline.HomeSceneInput;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

namespace Zjcs.Editor
{
    [InitializeOnLoad]
    public static class HomeNativeBedValidation
    {
        const string Active = "Zjcs.HomeNativeBedValidation.Active";
        static HomeNativeBedValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("OwnedNativeBedRunner").AddComponent<HomeNativeBedValidationRunner>();
            };
            EditorApplication.update += () => {
                if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup >
                    SessionState.GetFloat(Active + ".deadline", float.MaxValue))
                    Finish(new TimeoutException("Native bed verification timed out."));
            };
        }
        sealed class Pointer : IHomeSceneClickPointer
        {
            public bool LeftDown { get; set; }
            public bool LeftUp { get; set; }
            public Vector2 Position { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }
        sealed class Context : IHomeSceneClickContext
        {
            public bool InMyHome => true;
            public bool AllNoTipWindowsClosed => true;
        }
        sealed class World : IHomeSceneInputWorld
        {
            public Camera Camera { get; set; }
            public EventSystem Events { get; set; }
            public bool EventSystemValid => Events;
            public bool TryGetFirstRaycastResult(Vector2 position, out RaycastResult result)
            { result = default; return false; }
        }
        static int checks;
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks++; }

        public static void Play()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !System.IO.File.Exists(
                System.IO.Path.Combine(workspace, ".zjcs-validation.json")))
                throw new InvalidOperationException("An owned validation workspace is required.");
            ValidationGameView.Portrait();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Active + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorApplication.EnterPlaymode();
        }

        internal static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error == null)
                Debug.Log("HOME_NATIVE_BED_PASSED: " + checks + " checks; 903 prefab closure, repaired/unrepair visuals, native actor and shared click owner.");
            else Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }

        internal static IEnumerator Checks()
        {
            checks = 0;
            var root = new GameObject("OwnedNativeBedChecks");
            RenderTexture render = null;
            try
            {
                GameObject prefab = Resources.Load<GameObject>("Home/NativeHomeBed");
                Require(prefab, "903 native bed prefab is not bound at the Resources path");
                Require(prefab.transform.Find("Repaired") && prefab.transform.Find("Unrepair"),
                    "original repaired and unrepair branches are missing");
                Require(prefab.GetComponentsInChildren<Collider>(true).Length > 0,
                    "original native bed collider closure is missing");
                Require(prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0,
                    "original native bed renderers are missing");
                var cameraObject = new GameObject("OwnedNativeBedCamera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 5;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.02f, .02f, .02f, 1);
                render = new RenderTexture(Screen.width, Screen.height, 24);
                render.Create(); camera.targetTexture = render;
                var events = new GameObject("OwnedNativeBedEventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(root.transform, false);
                var actorRoot = Object.Instantiate(prefab, root.transform, false);
                actorRoot.name = "NativeBedRuntimeInstance";
                actorRoot.SetActive(false);
                actorRoot.transform.localPosition = Vector3.zero;
                actorRoot.transform.Find("Repaired")?.gameObject.SetActive(true);
                actorRoot.transform.Find("Unrepair")?.gameObject.SetActive(false);
                var actor = actorRoot.AddComponent<NativeHomeBedActor>();
                var pointer = new Pointer { Width = Screen.width, Height = Screen.height };
                var world = new World { Camera = camera, Events = events };
                using var owner = new OfflineHomeSceneInput(() => camera, () => events,
                    new Context(), pointer, error => { throw error; });
                owner.Bind(actor);
                int clicks = 0; actor.Clicked += () => clicks++;
                actorRoot.SetActive(true);
                yield return null;
                Require(actor.GetComponentsInChildren<Collider>(true).Length > 0,
                    "instantiated native bed has no collider for ClickableHomeObj");
                Collider collider = actor.GetComponentInChildren<Collider>(true);
                Vector2 screen = camera.WorldToScreenPoint(collider.bounds.center);
                pointer.Position = screen; pointer.LeftDown = true; pointer.LeftUp = false;
                Physics.SyncTransforms(); owner.Tick(); owner.LateTick();
                pointer.LeftDown = false; pointer.LeftUp = true;
                owner.Tick(); owner.LateTick();
                Require(clicks == 1, "native bed actor did not receive the shared scene click");
                yield return null; yield return null;
                var previous = RenderTexture.active; RenderTexture.active = render;
                var frame = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                try
                {
                    frame.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); frame.Apply();
                    Color pixel = frame.GetPixel(Mathf.Clamp((int)screen.x, 0, frame.width - 1),
                        Mathf.Clamp((int)screen.y, 0, frame.height - 1));
                    Require(pixel.r > .03f || pixel.g > .03f || pixel.b > .03f,
                        "native bed rendered no visible pixels");
                    string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                    string capture = System.IO.Path.Combine(workspace, "native-bed-" + Guid.NewGuid().ToString("N") + ".png");
                    System.IO.File.WriteAllBytes(capture, frame.EncodeToPNG());
                    Debug.Log("HOME_NATIVE_BED_FRAME: " + capture);
                }
                finally { RenderTexture.active = previous; Object.Destroy(frame); }
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (render) { render.Release(); Object.Destroy(render); }
            }
        }
    }

    public sealed class HomeNativeBedValidationRunner : MonoBehaviour
    {
        Exception failure;
        void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure ??= new InvalidOperationException(message + "\n" + trace); }
        IEnumerator Start()
        {
            AudioListener.volume = 0; Application.runInBackground = true;
            Application.logMessageReceived += Observe;
            var check = HomeNativeBedValidation.Checks();
            while (true)
            {
                object next;
                try
                {
                    if (failure != null) throw failure;
                    if (!check.MoveNext()) break;
                    next = check.Current;
                }
                catch (Exception error)
                { (check as IDisposable)?.Dispose(); Application.logMessageReceived -= Observe; HomeNativeBedValidation.Finish(error); yield break; }
                yield return next;
            }
            (check as IDisposable)?.Dispose(); Application.logMessageReceived -= Observe;
            HomeNativeBedValidation.Finish(null);
        }
    }
}
