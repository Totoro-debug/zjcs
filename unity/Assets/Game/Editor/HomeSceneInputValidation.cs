using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Offline.HomeSceneInput;
using Object = UnityEngine.Object;

namespace Zjcs.Editor
{
    // No desktop input or user profile is opened. These are native Unity
    // physics/UI and callback checks for the shared, not-yet-bound scene port.
    [InitializeOnLoad]
    public static class HomeSceneInputValidation
    {
        const string Active = "Zjcs.HomeSceneInputValidation.Active";
        static HomeSceneInputValidation()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("OwnedSceneInputRunner").AddComponent<HomeSceneInputValidationRunner>();
            };
            EditorApplication.update += () => {
                if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup >
                    SessionState.GetFloat(Active + ".deadline", float.MaxValue)) Finish(new TimeoutException("Native scene input verification timed out."));
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
        sealed class World : IHomeSceneInputWorld
        {
            public Camera Camera { get; set; }
            public EventSystem Events;
            public bool EventSystemValid => Events;
            public bool TryGetFirstRaycastResult(Vector2 position, out RaycastResult result)
            {
                var hits = new List<RaycastResult>();
                Events.RaycastAll(new PointerEventData(Events) { position = position }, hits);
                result = hits.Count == 0 ? default : hits[0];
                return hits.Count > 0;
            }
        }
        sealed class Context : IHomeSceneClickContext
        {
            public bool InMyHome { get; set; } = true;
            public bool AllNoTipWindowsClosed { get; set; } = true;
        }
        [Serializable] sealed class Ownership { public int format; public string repository; }
        static int checks;
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        static void Unexpected(Exception error) => throw new InvalidOperationException("Unexpected native click callback error", error);
        static void Tick(Pointer pointer, GameObjectClickOperation operation, bool down, bool up, Vector2 position, bool expected)
        {
            pointer.LeftDown = down; pointer.LeftUp = up; pointer.Position = position;
            Physics.SyncTransforms();
            Require(operation.UpdateResult() == expected, "native click result at " + position + "; down=" + down + "; up=" + up);
        }
        static GameObject Target(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var target = new GameObject(name, typeof(BoxCollider));
            target.transform.SetParent(parent, false);
            target.transform.localPosition = position;
            target.transform.localScale = scale;
            return target;
        }

        static string OwnedWorkspace()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace))
                throw new InvalidOperationException("An owned validation workspace is required.");
            workspace = System.IO.Path.GetFullPath(workspace).TrimEnd('/', '\\');
            string marker = System.IO.Path.Combine(workspace, ".zjcs-validation.json");
            var ownership = System.IO.File.Exists(marker)
                ? JsonUtility.FromJson<Ownership>(System.IO.File.ReadAllText(marker)) : null;
            if (ownership == null || ownership.format != 1 || string.IsNullOrEmpty(ownership.repository)
                || !workspace.StartsWith(System.IO.Path.Combine(System.IO.Path.GetFullPath(ownership.repository),
                    "output", "validation") + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFullPath(Application.dataPath).StartsWith(
                    workspace + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use an independent owned Unity project for native scene input checks.");
            return workspace;
        }

        public static void Play()
        {
            OwnedWorkspace();
            ValidationGameView.Portrait();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Active + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorApplication.EnterPlaymode();
        }
        internal static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error == null) Debug.Log("HOME_SCENE_INPUT_PASSED: " + checks + " checks; actual Physics/uGUI, exact drag boundary, saved press position, identity recheck, actor gates and cleanup.");
            else Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
        internal static IEnumerator Checks()
        {
            string workspace = OwnedWorkspace();
            checks = 0;
            var root = new GameObject("OwnedHomeSceneInputChecks");
            RenderTexture render = null;
            try
            {
                var pointer = new Pointer();
                var camera = new GameObject("OwnedSceneCamera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.localPosition = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 5;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                render = new RenderTexture(Screen.width, Screen.height, 24);
                render.Create(); camera.targetTexture = render;
                var world = new World { Camera = camera };
                Vector2 center = camera.WorldToScreenPoint(Vector3.zero);
                pointer.Position = center; pointer.Width = camera.pixelWidth; pointer.Height = camera.pixelHeight;
                var first = Target(root.transform, "PressedTarget", Vector3.zero, new Vector3(.1f, .1f, .2f));
                Vector3 releasePosition = camera.ScreenToWorldPoint(new Vector3(center.x + 19, center.y, 10));
                var release = Target(root.transform, "ReleaseOnlyTarget", releasePosition, new Vector3(.1f, .1f, .2f));

                var operation = new GameObjectClickOperation(pointer, Unexpected, world);
                world.Camera = null;
                Tick(pointer, operation, true, false, center, false);
                world.Camera = camera;
                Tick(pointer, operation, true, false, center, false);
                var pressRay = camera.ScreenPointToRay((Vector3)center);
                Require(operation.Result.Value.Target == first, "press uses the actual nearest native BoxCollider; target="
                    + (operation.Result.Value.Target ? operation.Result.Value.Target.name : "<null>")
                    + "; ray=" + pressRay + "; bounds=" + first.GetComponent<BoxCollider>().bounds
                    + "; cameraRect=" + camera.pixelRect + "; aspect=" + camera.aspect);
                Vector2 pressedWorld = operation.Result.Value.WorldPos;
                Tick(pointer, operation, false, true, center + new Vector2(19, 0), true);
                Require(operation.Result.Value.Target == first && operation.Result.Value.WorldPos == pressedWorld,
                    "release retains the original down-target and press-time world position");
                Require(Physics.Raycast(camera.ScreenPointToRay((Vector3)pointer.Position), out var hit, 1000f)
                    && hit.collider.gameObject == release, "release coordinate really hits a different native collider");
                Tick(pointer, operation, false, true, center, false);
                Tick(pointer, operation, true, false, center, false);
                Tick(pointer, operation, false, true, center + new Vector2(20, 0), false);
                Tick(pointer, operation, false, true, center, false);
                Tick(pointer, operation, true, false, center, false);
                first.SetActive(false);
                Tick(pointer, operation, false, true, center, false);
                first.SetActive(true);
                Tick(pointer, operation, true, false, new Vector2(-1, center.y), false);
                Tick(pointer, operation, false, true, center, false);

                release.SetActive(false);
                var near = Target(root.transform, "OccludingTarget", new Vector3(0, 0, -1), new Vector3(.1f, .1f, .2f));
                Tick(pointer, operation, true, false, center, false);
                Require(operation.Result.Value.Target == near, "original unfiltered Physics raycast selects the nearest collider");
                near.SetActive(false);
                Tick(pointer, operation, false, true, center, false);
                first.transform.localPosition = new Vector3(0, 0, 1000);
                Tick(pointer, operation, true, false, center, false);
                Require(!operation.Result.Value.Target, "original 1000-unit hit limit is retained");
                Tick(pointer, operation, false, true, center, false);
                first.transform.localPosition = Vector3.zero;

                var events = new GameObject("OwnedEventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(root.transform, false); world.Events = events;
                var canvas = new GameObject("OwnedOcclusionCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvas.transform.SetParent(root.transform, false);
                var canvasComponent = canvas.GetComponent<Canvas>();
                canvasComponent.renderMode = RenderMode.ScreenSpaceCamera; canvasComponent.worldCamera = camera; canvasComponent.planeDistance = 1;
                var blocker = new GameObject("OwnedBlockingGraphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                blocker.transform.SetParent(canvas.transform, false);
                var rect = blocker.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                blocker.GetComponent<Image>().color = Color.green;
                Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                Require(world.TryGetFirstRaycastResult(center, out var uiHit) && uiHit.gameObject == blocker,
                    "real uGUI GraphicRaycaster covers the world click coordinate");
                var previous = RenderTexture.active;
                RenderTexture.active = render;
                var frame = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                try
                {
                    frame.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); frame.Apply();
                    var pixel = frame.GetPixel((int)center.x, (int)center.y);
                    Require(pixel.g > .8f && pixel.r < .2f, "native UI blocker really renders over the tested coordinate");
                    string capture = System.IO.Path.Combine(workspace, "scene-input-" + Guid.NewGuid().ToString("N") + ".png");
                    System.IO.File.WriteAllBytes(capture, frame.EncodeToPNG());
                    Debug.Log("HOME_SCENE_INPUT_FRAME: " + capture);
                }
                finally { RenderTexture.active = previous; Object.Destroy(frame); }
                Tick(pointer, operation, true, false, center, false);
                Require(!operation.Result.Value.Target, "UI-covered press cannot select the world collider");
                Tick(pointer, operation, false, true, center, false);
                blocker.SetActive(false); Canvas.ForceUpdateCanvases();
                Tick(pointer, operation, true, false, center, false);
                blocker.SetActive(true); Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                Tick(pointer, operation, false, true, center, false);
                blocker.SetActive(false); Canvas.ForceUpdateCanvases();

                var context = new Context();
                var actorRoot = new GameObject("OwnedClickableActor"); actorRoot.SetActive(false);
                actorRoot.transform.SetParent(root.transform, false);
                first.transform.SetParent(actorRoot.transform, true);
                var dormant = Target(actorRoot.transform, "InitiallyInactiveCollider", new Vector3(2, 0, 0), Vector3.one);
                dormant.SetActive(false);
                var actor = actorRoot.AddComponent<HomeSceneInputValidationActor>();
                typeof(ClickableHomeObj).GetField("mTouchList", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(actor, new List<GameObject>());
                using var owner = new OfflineHomeSceneInput(() => camera, () => events, context, pointer, Unexpected);
                var module = owner.InputModule;
                blocker.SetActive(true); Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                Require(owner.World.TryGetFirstRaycastResult(center, out _), "local owner uses the real original first uGUI raycast result");
                blocker.SetActive(false); Canvas.ForceUpdateCanvases();
                Require(owner.World.TryGetFirstRaycastResult(center, out _), "same-frame queries retain the original position cache");
                owner.LateTick();
                Require(!owner.World.TryGetFirstRaycastResult(center, out _), "original late-frame clearing observes the removed UI blocker");
                owner.LateTick();
                owner.Bind(actor);
                actorRoot.SetActive(true); yield return null;
                Require(((List<GameObject>)typeof(ClickableHomeObj).GetField("mTouchList", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(actor)).Contains(dormant), "original fallback includes inactive child colliders");
                var order = new List<string>(); actor.Order = order; actor.OnClickAction.AddListener(() => order.Add("callback"));
                Action<bool, bool> dispatch = (down, up) => {
                    pointer.Position = center; pointer.LeftDown = down; pointer.LeftUp = up;
                    Physics.SyncTransforms(); owner.Tick(); owner.LateTick(); };
                dispatch(true, false); dispatch(false, true);
                Require(order.SequenceEqual(new[] { "callback", "action" }), "shared input dispatch invokes the original callback before the building action");
                order.Clear(); context.AllNoTipWindowsClosed = false;
                dispatch(true, false); dispatch(false, true);
                Require(order.Count == 0, "owned-home modal/story gate blocks the native actor");
                context.AllNoTipWindowsClosed = true; context.InMyHome = false;
                dispatch(true, false); dispatch(false, true);
                Require(order.Count == 0, "base native furniture rejects a foreign-home context");
                context.InMyHome = true;
                long token = ((IInputOperationModule)module).Block<OnGameObjectClickResult>();
                dispatch(true, false); dispatch(false, true);
                Require(order.Count == 0, "existing per-result block tokens also block native furniture clicks");
                ((IInputOperationModule)module).Unblock(token);
                Object.DestroyImmediate(actor);
                dispatch(true, false); dispatch(false, true);
                Require(order.Count == 0, "actual Unity component destruction unregisters its callback");
                owner.Dispose();
                Require(!module.Enable, "disposed local input owner stops dispatch without touching desktop input");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (render) { render.Release(); Object.Destroy(render); }
            }
        }
    }

    public sealed class HomeSceneInputValidationRunner : MonoBehaviour
    {
        Exception failure;
        void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failure ??= new InvalidOperationException(message + "\n" + trace);
        }
        IEnumerator Start()
        {
            AudioListener.volume = 0; Application.runInBackground = true;
            Application.logMessageReceived += Observe;
            var check = HomeSceneInputValidation.Checks();
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
                {
                    (check as IDisposable)?.Dispose();
                    Application.logMessageReceived -= Observe;
                    HomeSceneInputValidation.Finish(error); yield break;
                }
                yield return next;
            }
            (check as IDisposable)?.Dispose();
            Application.logMessageReceived -= Observe;
            HomeSceneInputValidation.Finish(null);
        }
    }

    public sealed class HomeSceneInputValidationActor : ClickableHomeObj
    {
        public List<string> Order;
        protected override void OnValidClick() => Order.Add("action");
    }
}
