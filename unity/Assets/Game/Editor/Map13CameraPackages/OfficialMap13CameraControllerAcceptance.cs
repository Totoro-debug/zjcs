using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.World.NativeCamera;
using Zjcs.Offline.World.NativeCamera;

namespace Zjcs.Editor.Map13CameraPackages
{
    public static class OfficialMap13CameraControllerAcceptance
    {
        private const string Key = "Zjcs.Map13.CameraControllers";
        private static bool started, runtimeError;
        [Serializable] private sealed class Bindings { public string manifest; public Record[] records; }
        [Serializable] private sealed class Record { public string path, type, fieldsJson; }
        private sealed class Runner : MonoBehaviour { }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = runtimeError = false;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = true; }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map13 camera controller probe timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Map13 original camera controller probe").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            Require(!runtimeError, "Camera controller runtime reported an error.");
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 original camera controllers passed: actual Cinemachine follow, four mode transitions, directional look-ahead, original zoom bounds/recovery, drag bounds/inertia, complex-mode containment, story restoration and awaited partition preparation. Diagnostic component probe; original camera prefab, Windows input routing and Map13 gameplay remain pending.");
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            foreach (int width in new[] { 450, 720 })
            {
                int height = width == 450 ? 800 : 1280;
                SetViewport(width, height);
                yield return null; yield return new WaitForEndOfFrame();
                Require(Screen.width == width && Screen.height == height, "Camera probe resolution did not apply.");
                var steps = StepsAtSize();
                while (steps.MoveNext()) yield return steps.Current;
                Debug.Log("Original camera controller resolution passed: " + width + "x" + height);
            }
        }
        private static IEnumerator StepsAtSize()
        {
            foreach (var startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(v => v.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (var existing in UnityEngine.Object.FindObjectsOfType<Camera>()) existing.enabled = false;
            Time.timeScale = 1;
            Require(!CameraControlUtils.IsMapMoveImmediately(new Vector3(14, 0, 100), Vector3.zero) && CameraControlUtils.IsMapMoveImmediately(new Vector3(14.001f, 0, 0), Vector3.zero), "Original 14-unit discontinuity boundary differs.");
            var data = JsonUtility.FromJson<Bindings>(Resources.Load<TextAsset>("Map13/Native/camera-controller-bindings").text);
            Require(data.manifest == "871_156598" && data.records.Length == 9, "Unexpected original camera binding set.");
            var settings = ScriptableObject.CreateInstance<CameraControlSettings>();
            JsonUtility.FromJsonOverwrite(Resources.Load<TextAsset>("Map13/Native/camera-settings").text, settings);
            var actual = settings.WorldCameraControlSetting;
            Require(actual.BaseSetting.ViewLeaveLimitFactor == 1 && actual.BaseSetting.ViewOverLeaveLimitResetSpeed == 2 &&
                actual.ComplexSetting.ViewMoveRangePadding == Vector2.zero && !actual.ComplexSetting.AllowViewMissTarget, "Original asset overrides were replaced by class defaults.");
            var host = new GameObject("Diagnostic controller host"); host.SetActive(false);
            var target = new GameObject("Diagnostic target").transform; target.SetParent(host.transform, false); target.position = new Vector3(40, 40, 0);
            var follow = new GameObject("freeCamFocus").transform; follow.SetParent(host.transform, false);
            var cameraGo = new GameObject("Diagnostic Main Camera"); cameraGo.transform.SetParent(host.transform, false); cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.backgroundColor = new Color(.07f, .12f, .18f);
            var engineBrain = cameraGo.AddComponent<CinemachineBrain>();
            Apply(Find(data, "/WorldCamera/Main Camera", "Cinemachine.CinemachineBrain"), engineBrain);
            var vcamGo = new GameObject("freeVCam"); vcamGo.transform.SetParent(host.transform, false);
            var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
            Apply(Find(data, "/WorldCamera/controller/freeVCam", "Cinemachine.CinemachineVirtualCamera"), vcam);
            var transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
            Apply(Find(data, "/WorldCamera/controller/freeVCam/cm", "Cinemachine.CinemachineTransposer"), transposer);
            var adaptor = host.AddComponent<OrthographicCameraResolutionAdaptor>();
            Apply(Find(data, "/WorldCamera/controller", "OrthographicCameraResolutionAdaptor"), adaptor);
            var adaptorProperties = new SerializedObject(adaptor);
            adaptorProperties.FindProperty("mCamera").objectReferenceValue = vcam; adaptorProperties.ApplyModifiedPropertiesWithoutUndo();
            Require(adaptorProperties.FindProperty("mySafeArea").boundsValue.extents == new Vector3(3.5f, 6.2220001220703125f, 0), "Original camera safe area failed to deserialize.");
            var drag = vcamGo.AddComponent<IgnoreUIDragController>();
            Apply(Find(data, "/WorldCamera/controller/freeVCam", "IgnoreUIDragController"), drag);
            drag.Data.Target = follow;
            Require(drag.Data.Horizontal && drag.Data.Vertical && drag.Data.Inertia && Mathf.Abs(drag.Data.DecelerationRate - .04f) < 1e-7f && drag.Data.TargetBounds.extents == new Vector3(5.72f, 6.22f, 0), "Original drag configuration failed to deserialize.");
            TaskCompletionSource<bool> gate = null; int prepared = 0;
            var services = new OfflineMap13CameraServices(settings, host, Vector3.zero, new Vector2Int(160, 160), position =>
            { prepared++; return gate == null ? Task.CompletedTask : gate.Task; });
            Vector3 pointer = new Vector3(300, 300, 0); services.PointerPosition = () => pointer; services.TouchCount = () => 0;
            services.Inject(drag);
            var brain = host.AddComponent<CameraControlBrain>(); services.Inject(brain);
            brain.MonoProxy = new CameraMonoProxy { VirCamera = vcam, CameraFollow = follow, DragController = drag, ResolutionAdaptor = adaptor, CameraControlEnv = CameraControlEnv.Map, CinemachineBrain = engineBrain };
            host.SetActive(true); brain.Init(target); brain.SetController<WorldCameraFollowController>(true);
            yield return null; yield return new WaitForEndOfFrame();
            Require(!drag.enabled && services.ControlBrain == brain, "Follow mode did not enter the actual controller lifecycle.");
            target.position += new Vector3(2, 1, 0); yield return null; yield return new WaitForEndOfFrame();
            Near(follow.position.x, target.position.x, "Original follow did not copy player pose.");
            yield return new WaitForSeconds(.8f);
            Near(camera.transform.position.x, target.position.x, "Actual Cinemachine did not converge to the follow target.", .03f);
            Near(camera.transform.position.z, -250, "Current camera offset differs from the original transposer.", .03f);
            var followMode = brain.GetController<WorldCameraFollowController>(); float baseSize = followMode.DefaultViewSize;
            Near(baseSize, adaptor.CalcAdaptOrthographicSize(7f / 2 / Screen.width * Screen.height), "Original default view formula differs.");
            services.Input.Dispatch(new OnGameObjectZoomBeginResult(null, Vector2.zero, -10));
            services.Input.Dispatch(new OnGameObjectZoomResult(null, Vector2.zero, -10));
            Near(followMode.GetViewSize(), baseSize / 3, "Zoom close hard limit differs.");
            services.Input.Dispatch(new OnGameObjectZoomEndResult(null, Vector2.zero, 0));
            yield return new WaitForSeconds(.5f);
            Require(followMode.GetViewSize() >= followMode.SoftViewSizeMin, "Zoom close limit did not recover with the original reset speed.");
            services.Input.Dispatch(new OnGameObjectZoomBeginResult(null, Vector2.zero, 10));
            services.Input.Dispatch(new OnGameObjectZoomResult(null, Vector2.zero, 10));
            Near(followMode.GetViewSize(), baseSize / actual.BaseSetting.ViewZoomLeaveLimitFactor, "Zoom far hard limit differs.");
            services.Input.Dispatch(new OnGameObjectZoomEndResult(null, Vector2.zero, 0));
            yield return new WaitForSeconds(.5f);
            Require(followMode.GetViewSize() <= followMode.SoftViewSizeMax, "Zoom far limit did not recover with the original reset speed.");
            brain.SetController<WorldCameraLookAheadController>(true); Require(!drag.enabled, "Ahead mode enabled drag.");
            target.position += Vector3.right; yield return null; yield return new WaitForEndOfFrame();
            Near(follow.position.x, target.position.x + 1.5f, "Rightward look-ahead differs.");
            target.position += Vector3.left * 2; yield return null; yield return new WaitForEndOfFrame();
            Near(follow.position.x, target.position.x - 1.5f, "Leftward look-ahead differs.");
            target.position += Vector3.up; yield return null; yield return new WaitForEndOfFrame();
            Near(follow.position.x, target.position.x - 1.5f, "Vertical movement lost the last horizontal direction.");
            brain.SetController<WorldCameraDragController>(true); yield return null;
            Require(drag.enabled && drag.Data.BorderBounds.center == new Vector3(80, 80, 0) && drag.Data.BorderBounds.size == new Vector3(160, 160, 0), "Free mode did not bind the map grid boundary.");
            var freeMode = brain.GetController<WorldCameraDragController>();
            brain.SetViewPos(new Vector3(-100, -100, 9), true); yield return null;
            Require(follow.position.x >= drag.Data.TargetBounds.extents.x && follow.position.y >= drag.Data.TargetBounds.extents.y && follow.position.z == 0, "Original free camera did not clamp to the grid.");
            brain.SetViewPos(target.position, true); yield return null;
            Vector3 dragStart = follow.position;
            services.Input.Dispatch(new OnGameObjectDragBeginResult()); services.Input.Dispatch(new OnIgnoreUIDragBeginResult(pointer));
            pointer += new Vector3(35, 20, 0); services.Input.Dispatch(new OnGlobalDragResult(Vector2.zero, pointer, true));
            yield return null;
            Require(follow.position.x < dragStart.x && follow.position.y < dragStart.y, "Original drag did not move opposite to screen displacement.");
            services.Input.Dispatch(new OnGlobalDragEndResult(Vector2.zero, pointer)); services.Input.Dispatch(new OnGameObjectDragEndResult());
            yield return new WaitForSeconds(.2f);
            Require(follow.position != dragStart, "Original drag/inertia discarded movement.");
            brain.SetController<WorldCameraComplexController>(true); yield return null;
            Require(drag.Data.UpdateMode == DragControllerData.UpdateType.ManualUpdate && drag.Data.DragUpdateMode == DragControllerData.DragUpdateType.ManualUpdate, "Complex mode did not own manual drag updates.");
            target.position += Vector3.right * 2; yield return null; yield return new WaitForEndOfFrame();
            Require(follow.position.x >= drag.Data.ContentPosLimitX.x && follow.position.x <= drag.Data.ContentPosLimitX.y, "Complex camera lost its custom player containment limit.");
            brain.EnterStoryController(); Require(!drag.enabled, "Story mode did not disable drag.");
            Require(drag.Data.UpdateMode == DragControllerData.UpdateType.LateUpdate, "Leaving complex mode did not restore drag update mode.");
            var story = brain.GetController<CameraStoryController>();
            Near(story.SoftViewSizeMax, 30, "Story camera soft maximum differs.");
            var before = follow.position; gate = new TaskCompletionSource<bool>(); int previousPrepared = prepared;
            brain.SetViewPos(before + new Vector3(20, 0, 0), false);
            Require(prepared == previousPrepared + 1 && follow.position == before, "Abrupt story move did not wait for partition preparation.");
            gate.SetResult(true); yield return null;
            Near(follow.position.x, before.x + 20, "Prepared story move did not resume."); gate = null;
            brain.ExitStoryController(); yield return null;
            Require(brain.CameraController is WorldCameraComplexController && drag.enabled, "Story mode did not restore the previous controller.");
            brain.SetController<WorldCameraFollowController>(true); yield return null;
            before = follow.position; gate = new TaskCompletionSource<bool>(); previousPrepared = prepared;
            brain.SetViewPos(before + new Vector3(20, 0, 3), false);
            Require(prepared == previousPrepared + 1 && follow.position == before, "Abrupt map move did not wait for partition preparation.");
            gate.SetResult(true); yield return null; Near(follow.position.z, 0, "Map camera failed to clamp Z after preparation."); gate = null;
            var order = new System.Collections.Generic.List<int>(); Action<OnGlobalDragEndResult> first = v => order.Add(1), second = v => order.Add(2);
            services.Input.HookOnInput(first); services.Input.HookOnInput(second); services.Input.Dispatch(default(OnGlobalDragEndResult));
            Require(order.SequenceEqual(new[] { 2, 1 }), "Input callback order differs from the original reverse hook order.");
            services.Input.UnhookOnInput(second); order.Clear(); services.Input.Dispatch(default(OnGlobalDragEndResult));
            Require(order.SequenceEqual(new[] { 1 }), "Removed input callback was executed."); services.Input.UnhookOnInput(first);
            string output = Path.GetFullPath(".tools/map13-camera-controllers/controller-playmode-" + Screen.width + "x" + Screen.height + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(output));
            ScreenCapture.CaptureScreenshot(output); yield return new WaitForEndOfFrame(); yield return null;
            Require(File.Exists(output), "Camera probe screenshot was not captured.");
            UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(settings); yield return null;
            Require(!runtimeError, "Camera controller lifecycle reported runtime errors.");
        }
        private static void SetViewport(int width, int height)
        {
            var editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            object size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, "Map13 camera controller fixture" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type type = editor.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(type, false, "Game", false);
            type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
            view.Repaint();
        }
        private static void Apply(string json, UnityEngine.Object component) => EditorJsonUtility.FromJsonOverwrite("{\"MonoBehaviour\":" + json + "}", component);
        private static string Find(Bindings data, string path, string type) => data.records.Single(r => r.path == path && r.type == type).fieldsJson;
        private static void Near(float actual, float expected, string message, float tolerance = 1e-4f) => Require(Mathf.Abs(actual - expected) < tolerance, message + " actual=" + actual + " expected=" + expected);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
