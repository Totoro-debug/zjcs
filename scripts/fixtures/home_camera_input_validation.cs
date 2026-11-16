using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeCamera;

[InitializeOnLoad]
public static class HomeCameraInputValidation
{
    private const string Active = "Zjcs.HomeCameraInputValidation.Active";
    static HomeCameraInputValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeCameraInputValidation").AddComponent<HomeCameraInputRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup >
                SessionState.GetFloat(Active + ".Deadline", float.MaxValue)) Finish(new TimeoutException("Home camera input validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_CAMERA_INPUT_PASSED: original drag thresholds, UI raycasts and zoom operation.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeCameraInputRunner : MonoBehaviour
{
    private bool down, held;
    private Vector3 position;
    private Touch[] touches = Array.Empty<Touch>();
    private int errors;

    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += (message, stack, type) => {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
        CameraPointerInput.MouseButtonDown = button => button == 0 && down;
        CameraPointerInput.MouseButton = button => button == 0 && held;
        CameraPointerInput.Position = () => position;
        CameraPointerInput.Touches = () => touches.Length;
        CameraPointerInput.Touch = index => touches[index];
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { HomeCameraInputValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeCameraInputValidation.Finish(errors == 0 ? null : new Exception("Unexpected camera input errors: " + errors));
    }

    private IEnumerator Check()
    {
        var system = new GameObject("Original input event system", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvas = new GameObject("Raycast coverage", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var cover = new GameObject("Blocking UI", typeof(RectTransform), typeof(Image));
        cover.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)cover.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        yield return null;
        var testRaycaster = canvas.GetComponent<GraphicRaycaster>();
        foreach (var raycaster in FindObjectsOfType<BaseRaycaster>())
            if (raycaster != testRaycaster) raycaster.enabled = false;
        Canvas.ForceUpdateCanvases();
        var events = new CameraEventSystemProbe();
        var ignored = new IgnoreUIDragBeginOperation { EventSystemManager = events };
        var drag = new GlobalDragOperation();
        var end = new GlobalDragEndOperation();
        position = new Vector3(Screen.width * .5f, Screen.height * .5f); down = held = true;
        Require(events.TryGetFirstRaycastResult(position, out var firstHit) && firstHit.gameObject == cover,
            "Actual UI raycast covers the start position");
        Require(!ignored.UpdateResult() && !drag.UpdateResult() && !end.UpdateResult(), "Original pointer down frame");
        down = false;
        Require(!ignored.UpdateResult() && !drag.UpdateResult() && !end.UpdateResult(), "Original hold establishes drag origin");
        position.x += 19;
        Require(!ignored.UpdateResult() && !drag.UpdateResult() && !end.UpdateResult(), "Nineteen pixels do not start dragging");
        position.x += 1;
        Require(!ignored.UpdateResult() && !drag.UpdateResult() && !end.UpdateResult(), "UI raycast blocks original drag begin");
        position.x += 1;
        Require(drag.UpdateResult() && drag.Result.Moved && !end.UpdateResult(), "Original global drag reports movement after begin");
        Require(drag.UpdateResult() && !drag.Result.Moved && !end.UpdateResult(), "Original one-pixel movement threshold stops stationary updates");
        held = false;
        Require(!ignored.UpdateResult() && !drag.UpdateResult() && end.UpdateResult(), "Original drag end after pointer release");
        cover.SetActive(false); Canvas.ForceUpdateCanvases();
        Require(!events.TryGetFirstRaycastResult(position, out _), "No UI hit after the original cover is hidden");
        ignored = new IgnoreUIDragBeginOperation { EventSystemManager = events };
        position = new Vector3(Screen.width * .5f, Screen.height * .5f); down = held = true;
        Require(!ignored.UpdateResult(), "Uncovered pointer down");
        down = false; Require(!ignored.UpdateResult(), "Uncovered hold");
        position.x += 20;
        Require(ignored.UpdateResult(), "Uncovered original drag begins at twenty pixels");
        Require(Mathf.Approximately(ignored.Result.ScreenPos.x, Screen.width * .5f), "Original drag begin retains the press origin");
        var dispatch = new InputOperationModule(new IInputOperation[] { new GlobalDragOperation() });
        var callbacks = new List<string>();
        Action<OnGlobalDragResult> first = value => callbacks.Add("first");
        Action<OnGlobalDragResult> second = value => callbacks.Add("second");
        dispatch.HookOnInput(first); dispatch.HookOnInput(second);
        dispatch.Update();
        Require(callbacks.Count == 0, "Original disabled input module does not dispatch");
        dispatch.Enable = true;
        position = new Vector3(Screen.width * .5f, Screen.height * .5f); down = held = true; dispatch.Update();
        down = false; dispatch.Update();
        position.x += 20; dispatch.Update();
        position.x += 1; dispatch.Update();
        Require(callbacks.Count == 2 && callbacks[0] == "second" && callbacks[1] == "first",
            "Original input callbacks run in reverse registration order");
        long typeBlock = dispatch.Block<OnGlobalDragResult>();
        long layerBlock = dispatch.Block(InputOperationLayer.GlobalInput);
        Require(typeBlock != 0 && layerBlock != 0 && typeBlock != layerBlock, "Original input block signatures");
        position.x += 1; dispatch.Update();
        dispatch.Unblock(typeBlock); position.x += 1; dispatch.Update();
        Require(callbacks.Count == 2, "Original layer block remains active after type unblock");
        dispatch.Unblock(layerBlock); position.x += 1; dispatch.Update();
        Require(callbacks.Count == 4 && callbacks[2] == "second" && callbacks[3] == "first", "Original input unblocks and dispatches");
        dispatch.UnhookOnInput(second); position.x += 1; dispatch.Update();
        Require(callbacks.Count == 5 && callbacks[4] == "first", "Original removed callback is skipped during dispatch");
        var zoom = new GlobalZoomOperation();
        touches = new[] {
            new Touch { position = new Vector2(100, 100), deltaPosition = new Vector2(-10, 0), phase = TouchPhase.Moved },
            new Touch { position = new Vector2(200, 100), deltaPosition = new Vector2(10, 0), phase = TouchPhase.Stationary }
        };
        Require(zoom.UpdateResult() && Mathf.Abs(zoom.Result.ZoomFactor + .25f) < .0001f
            && zoom.Result.ScreenPos == new Vector2(150, 100), "Original two-touch zoom factor and center");
        touches[1].phase = TouchPhase.Began;
        Require(!zoom.UpdateResult(), "Original zoom ignores the second touch begin frame");
        Destroy(system); Destroy(canvas);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

public sealed class CameraEventSystemProbe : IEventSystemManager
{
    public bool EventSystemValid => EventSystem.current;
    public bool TryGetFirstRaycastResult(Vector2 point, out RaycastResult result)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        result = hits.Count == 0 ? default : hits[0];
        return hits.Count != 0;
    }
}
