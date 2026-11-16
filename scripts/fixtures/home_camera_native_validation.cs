using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.HomeCamera;
using Zjcs.Offline.State;
using Zjcs.Official.HomeCamera;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeCameraNativeValidation
{
    private const string Active = "Zjcs.HomeCameraNativeValidation.Active";
    static HomeCameraNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeCameraNativeValidation").AddComponent<HomeCameraNativeRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup >
                SessionState.GetFloat(Active + ".Deadline", float.MaxValue)) Finish(new TimeoutException("Home camera validation timed out."));
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
        if (error == null) Debug.Log("HOME_CAMERA_NATIVE_PASSED: selected camera hierarchy, settings, input, story transitions and bloom registration.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeCameraNativeRunner : MonoBehaviour
{
    private int errors;
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += (message, stack, type) => {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { HomeCameraNativeValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeCameraNativeValidation.Finish(errors == 0 ? null : new Exception("Unexpected camera errors: " + errors));
    }
    private IEnumerator Check()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__PREFAB_GUID__"));
        var settings = AssetDatabase.LoadAssetAtPath<CameraControlSettings>(AssetDatabase.GUIDToAssetPath("__SETTINGS_GUID__"));
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath("__PROFILE_GUID__"));
        var bloom = AssetDatabase.LoadAssetAtPath<Bloom>(AssetDatabase.GUIDToAssetPath("__BLOOM_GUID__"));
        Require(prefab && settings && profile && bloom, "Native camera and external assets imported");
        var catalog = HomeCameraAssets.LoadDefault();
        Require(catalog.FreeCamera == prefab && catalog.Settings == settings
            && catalog.MainCamera == AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__MAIN_PREFAB_GUID__"))
            && catalog.UICamera == AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__UI_PREFAB_GUID__"))
            && catalog.UIParentPosition == new Vector3(__UI_PARENT_X__f, __UI_PARENT_Y__f, __UI_PARENT_Z__f),
            "Packaged camera catalog resolves every selected native asset and UI parent offset");
        Require(profile.components.Count == 1 && profile.components[0] == bloom && bloom.intensity.overrideState
            && Mathf.Approximately(bloom.intensity.value, 1f) && Mathf.Approximately(bloom.threshold.value, 1.3f)
            && bloom.maxIterations.value == 5, "Original Bloom settings and profile reference");
        Require(settings.StorySettings.StorySoftViewSizeMax == 30 && settings.HomeCameraControlSetting.BaseSetting.ZoomFactor == 0f,
            "Original camera settings imported");
        var main = new GameObject("Main Camera", typeof(Camera), typeof(CinemachineBrain));
        main.tag = "MainCamera";
        main.GetComponent<Camera>().orthographic = true;
        var inactive = new GameObject("Camera injection scope"); inactive.SetActive(false);
        var instance = Instantiate(prefab, inactive.transform, false);
        var brain = instance.GetComponentInChildren<CameraControlBrain>(true);
        var drag = instance.GetComponentInChildren<IgnoreUIDragController>(true);
        var init = instance.GetComponentInChildren<HomeCameraIniter>(true);
        var volume = instance.GetComponentInChildren<Volume>(true);
        var trigger = instance.GetComponentInChildren<BloomIntensityVolumeRefreshTrigger>(true);
        Require(brain && drag && init && volume && trigger && volume.sharedProfile == profile,
            "Original camera components and profile binding");
        var services = new CameraServices(settings);
        services.Inject(brain); services.Inject(drag); trigger.BloomIntensityService = services;
        inactive.SetActive(true);
        yield return null;
        Require(services.ControlBrain == brain && brain.CameraController is HomeCameraController,
            "Original home initializer selected its camera controller");
        Require(trigger.IsInitialized && services.Registered == trigger && Mathf.Approximately(trigger.BaseIntensity, 1),
            "Original volume registered after Awake");
        Require(drag.Data.Horizontal && !drag.Data.Vertical && Mathf.Approximately(drag.Data.Sensitive, 1.3f)
            && Mathf.Approximately(drag.Data.DecelerationRate, .04f) && drag.Data.Target,
            "Original drag parameters and target");
        Vector3 before = drag.Data.Target.position;
        services.Pointer = new Vector3(Screen.width * .6f, Screen.height * .5f);
        services.Dispatch(new OnIgnoreUIDragBeginResult(services.Pointer));
        services.Pointer += new Vector3(-Screen.width * .15f, Screen.height * .1f);
        services.Dispatch(new OnGlobalDragResult(Vector2.zero, services.Pointer, true));
        yield return null;
        Require(Mathf.Abs(drag.Data.Target.position.x - before.x) > .1f
            && Mathf.Approximately(drag.Data.Target.position.y, before.y), "Simulated horizontal camera drag");
        services.Dispatch(new OnGlobalDragEndResult(Vector2.zero, services.Pointer));
        var home = (HomeCameraController)brain.CameraController;
        home.SetFocusPositionXForce(10000, true);
        float max = drag.Data.BorderBounds.max.x - brain.MonoProxy.VirCamera.m_Lens.OrthographicSize * brain.MonoProxy.VirCamera.m_Lens.Aspect;
        Require(Mathf.Abs(drag.Data.Target.position.x - max) < .01f, "Official viewport clamp");
        brain.EnterStoryController();
        Require(brain.CameraController is CameraStoryController && !drag.enabled, "Story mode disables drag");
        brain.ExitStoryController();
        Require(brain.CameraController is HomeCameraController && drag.enabled, "Home mode restores drag");
        Destroy(inactive); yield return null;
        Require(!services.Registered, "Original volume unregisters on destroy");

        var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__UI_PREFAB_GUID__"));
        var uiProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath("__UI_PROFILE_GUID__"));
        var uiBloom = AssetDatabase.LoadAssetAtPath<Bloom>(AssetDatabase.GUIDToAssetPath("__UI_BLOOM_GUID__"));
        Require(uiPrefab && uiProfile && uiBloom && uiProfile.components.Count == 1 && uiProfile.components[0] == uiBloom
            && Mathf.Approximately(uiBloom.clamp.value, 65472f), "Distinct original UI volume profile");
        var uiScope = new GameObject("UI camera injection scope"); uiScope.SetActive(false);
        var ui = Instantiate(uiPrefab, uiScope.transform, false);
        var uiCamera = ui.GetComponentInChildren<Camera>(true);
        var uiSwitch = ui.GetComponentInChildren<UIBloomSwitch>(true);
        var uiVolume = ui.GetComponentInChildren<Volume>(true);
        var uiTrigger = ui.GetComponentInChildren<BloomIntensityVolumeRefreshTrigger>(true);
        Require(uiCamera && uiSwitch && uiVolume && uiTrigger && uiVolume.sharedProfile == uiProfile,
            "Original UI camera, volume and script bindings");
        var saved = new CameraMemoryState<BloomIntensitySettingState>(new BloomIntensitySettingState());
        var legacy = new CameraMemoryState<PerformanceSettingState>(new PerformanceSettingState { BloomEnabled = true });
        var existing = new CameraMemoryState<BloomIntensitySettingState>(new BloomIntensitySettingState { BloomIntensityLevel = EBloomIntensityLevel.Half });
        var existingService = new BloomIntensityService(existing, legacy, new CameraDeviceProbe());
        Require(existingService.BloomIntensityLevel == EBloomIntensityLevel.Half && existing.Writes == 0,
            "Original valid saved intensity takes precedence over legacy state");
        var defaultLow = new CameraMemoryState<BloomIntensitySettingState>(new BloomIntensitySettingState());
        var noLegacy = new CameraMemoryState<PerformanceSettingState>(new PerformanceSettingState());
        Require(new BloomIntensityService(defaultLow, noLegacy, new CameraDeviceProbe(PerformanceLevel.Low)).BloomIntensityLevel == EBloomIntensityLevel.Zero
            && defaultLow.Writes == 0, "Original low-performance default");
        var defaultMedium = new CameraMemoryState<BloomIntensitySettingState>(new BloomIntensitySettingState());
        Require(new BloomIntensityService(defaultMedium, noLegacy, new CameraDeviceProbe()).BloomIntensityLevel == EBloomIntensityLevel.Full
            && defaultMedium.Writes == 0, "Original medium-performance default");
        string settingsRoot = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "camera-settings-validation", Guid.NewGuid().ToString("N"));
        string settingsPath = Path.Combine(settingsRoot, "camera-settings.json");
        var diskStore = new HomeCameraSettingsStore(settingsPath);
        Require(diskStore.LoadStatus == SaveReadStatus.Missing && new HomeCameraDesktopDevice().GetPerformanceLevel() == PerformanceLevel.Ultra,
            "Missing local camera settings and original desktop performance level");
        var diskService = new BloomIntensityService(diskStore.Current, diskStore.Legacy, new HomeCameraDesktopDevice());
        Require(diskService.BloomIntensityLevel == EBloomIntensityLevel.Full && !File.Exists(settingsPath),
            "Original desktop default does not create an unnecessary save");
        diskService.ChangeBloomIntensityLevel(EBloomIntensityLevel.Half);
        var reopened = new HomeCameraSettingsStore(settingsPath);
        Require(reopened.LoadStatus == SaveReadStatus.Primary && reopened.Current.State.BloomIntensityLevel == EBloomIntensityLevel.Half,
            "Original intensity persists and reopens from the isolated local file");
        var reopenedService = new BloomIntensityService(reopened.Current, reopened.Legacy, new HomeCameraDesktopDevice());
        reopenedService.ChangeBloomIntensityLevel(EBloomIntensityLevel.Quarter);
        File.WriteAllText(settingsPath, "invalid isolated settings");
        var recovered = new HomeCameraSettingsStore(settingsPath);
        Require(recovered.LoadStatus == SaveReadStatus.Recovered && recovered.Current.State.BloomIntensityLevel == EBloomIntensityLevel.Half,
            "Camera settings recover the previous valid local backup");
        var uiServices = new BloomIntensityService(saved, legacy, new CameraDeviceProbe());
        Require(uiServices.BloomIntensityLevel == EBloomIntensityLevel.Full && saved.Writes == 1,
            "Original legacy Bloom migration writes the selected intensity");
        uiServices.Initialize();
        var switchServices = new UIBloomProbe();
        uiTrigger.BloomIntensityService = uiServices; uiSwitch.UIBloomManager = switchServices;
        uiScope.SetActive(true); yield return null;
        var uiData = uiCamera.GetUniversalAdditionalCameraData();
        Require(switchServices.ListenerCount == 1 && !uiData.renderPostProcessing && !uiVolume.gameObject.activeSelf,
            "Original UI Bloom initial deactivation and listener");
        switchServices.SetActive(true); yield return null;
        Require(uiData.renderPostProcessing && uiVolume.gameObject.activeSelf && uiTrigger.IsInitialized,
            "Original UI Bloom activation and volume registration");
        int bloomChanges = 0;
        uiServices.OnBloomChange += () => bloomChanges++;
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Quarter);
        Require(Mathf.Approximately(uiTrigger.Bloom.intensity.value, .25f) && saved.Writes == 2 && bloomChanges == 0,
            "Original quarter intensity persists without changing the enable state");
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Quarter);
        Require(saved.Writes == 2 && bloomChanges == 0, "Original unchanged setting does not write again");
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Zero);
        Require(Mathf.Approximately(uiTrigger.Bloom.intensity.value, 0f) && saved.Writes == 3 && bloomChanges == 1,
            "Original zero intensity persists and raises enable-state change");
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Full);
        Require(Mathf.Approximately(uiTrigger.Bloom.intensity.value, 1f) && saved.Writes == 4 && bloomChanges == 2,
            "Original full intensity and event restored");
        Require(uiServices.ModifyBloomIntensity(uiTrigger.Bloom, 2f, out float oldIntensity)
            && Mathf.Approximately(oldIntensity, 1f) && Mathf.Approximately(uiTrigger.Bloom.intensity.value, 2f),
            "Original registered volume accepts temporary intensity change");
        switchServices.SetActive(false); yield return null;
        Require(!uiData.renderPostProcessing && !uiVolume.gameObject.activeSelf,
            "Original UI Bloom deactivation");
        Destroy(uiScope); yield return null;
        Require(switchServices.ListenerCount == 0, "UI camera releases the switch listener");

        var mainScope = new GameObject("Main camera performance probe"); mainScope.SetActive(false);
        mainScope.tag = "MainCamera";
        var camera = mainScope.AddComponent<Camera>();
        var cameraData = mainScope.AddComponent<UniversalAdditionalCameraData>();
        var setter = mainScope.AddComponent<CameraPerformanceSetter>();
        var culler = mainScope.AddComponent<CameraSceneLayerCuller>();
        var cameraEvents = new CameraEventProbe(uiServices);
        culler.WindowEvents = cameraEvents; culler.SceneEvents = cameraEvents; culler.PerformanceSetting = cameraEvents;
        typeof(CameraSceneLayerCuller).GetField("mCamera", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culler, camera);
        typeof(CameraSceneLayerCuller).GetField("mFullScreenUICullingMask", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culler, (LayerMask)0);
        setter.BloomIntensityService = uiServices;
        mainScope.SetActive(true); yield return null;
        Require(cameraData.renderPostProcessing, "Original main camera initializes Bloom from the intensity service");
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Zero);
        Require(!cameraData.renderPostProcessing, "Original main camera disables post processing when Bloom is disabled");
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Full);
        Require(cameraData.renderPostProcessing, "Original main camera restores post processing when Bloom is enabled");
        int originalMask = camera.cullingMask;
        var firstWindow = new GameObject("First full screen window");
        var secondWindow = new GameObject("Second full screen window");
        cameraEvents.Open(firstWindow); cameraEvents.Open(secondWindow);
        Require(camera.cullingMask == 0 && !cameraData.renderPostProcessing, "First full-screen window pauses camera rendering");
        cameraEvents.Close(firstWindow);
        Require(camera.cullingMask == 0, "Nested full-screen window keeps camera paused");
        cameraEvents.Close(secondWindow);
        Require(camera.cullingMask == originalMask && cameraData.renderPostProcessing, "Last full-screen window restores camera rendering");
        var owner = culler.TakeOverCameraAttrs("probe");
        cameraEvents.Open(firstWindow);
        Require(camera.cullingMask == originalMask, "Camera owner defers culling changes");
        owner.Dispose();
        Require(camera.cullingMask == 0 && !cameraData.renderPostProcessing, "Releasing camera ownership applies the pending state");
        cameraEvents.Close(firstWindow);
        Require(camera.cullingMask == originalMask && cameraData.renderPostProcessing, "Closing deferred full-screen window restores camera");
        cameraEvents.Open(firstWindow); cameraEvents.Scene(SceneType.Fight);
        Require(camera.cullingMask == originalMask && cameraData.renderPostProcessing, "Fight transition clears full-screen camera pause");
        Destroy(mainScope); yield return null;
        Require(cameraEvents.WindowListeners == 0 && cameraEvents.SceneListeners == 0, "Main camera unsubscribes window and scene events");
        Destroy(firstWindow); Destroy(secondWindow);
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Zero);

        Destroy(main); yield return null;
        var originalMain = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__MAIN_PREFAB_GUID__"));
        Require(originalMain, "Original main camera hierarchy imported");
        var wwiseSettings = Resources.Load<AkWwiseInitializationSettings>("Combat/Wwise/AkWwiseInitializationSettings");
        Require(wwiseSettings && !AkSoundEngine.IsInitialized(), "Original Wwise initialization settings and unowned engine");
        using (var native = AkWwiseInitializationSettings.ActivePlatformSettings.AkInitializationSettings)
            Require(AkSoundEngine.Init(native) == AKRESULT.AK_Success, "Initialize isolated original Wwise listener engine");
        Require(AkSoundEngine.SetOutputVolume(0, 0) == AKRESULT.AK_Success, "Mute isolated Wwise output");
        var originalScope = new GameObject("Original camera stack injection scope"); originalScope.SetActive(false);
        var mainInstance = Instantiate(originalMain, originalScope.transform, false);
        var uiParent = new GameObject("Original UI parent");
        uiParent.transform.SetParent(originalScope.transform, false);
        uiParent.transform.localPosition = new Vector3(__UI_PARENT_X__f, __UI_PARENT_Y__f, __UI_PARENT_Z__f);
        var uiInstance = Instantiate(uiPrefab, uiParent.transform, false);
        var actualCamera = mainInstance.GetComponent<Camera>();
        var actualData = mainInstance.GetComponent<UniversalAdditionalCameraData>();
        var actualCuller = mainInstance.GetComponent<CameraSceneLayerCuller>();
        var actualSetter = mainInstance.GetComponent<CameraPerformanceSetter>();
        var actualListener = mainInstance.GetComponentInChildren<AkAudioListener>(true);
        var actualUiCamera = uiInstance.GetComponent<Camera>();
        var actualUiSwitch = uiInstance.GetComponent<UIBloomSwitch>();
        var actualUiTrigger = uiInstance.GetComponentInChildren<BloomIntensityVolumeRefreshTrigger>(true);
        Require(actualCamera && actualData && actualCuller && actualSetter && actualListener && actualUiCamera && actualUiSwitch && actualUiTrigger,
            "All original main and UI camera components imported");
        Require(mainInstance.GetComponentsInChildren<MonoBehaviour>(true).Length == 7
            && Array.TrueForAll(mainInstance.GetComponentsInChildren<MonoBehaviour>(true), component => component),
            "Original main camera hierarchy has no missing scripts");
        var actualEvents = new CameraEventProbe(uiServices);
        actualCuller.WindowEvents = actualEvents; actualCuller.SceneEvents = actualEvents; actualCuller.PerformanceSetting = actualEvents;
        actualSetter.BloomIntensityService = uiServices;
        actualUiSwitch.UIBloomManager = switchServices; actualUiTrigger.BloomIntensityService = uiServices;
        actualData.cameraStack.Add(actualUiCamera);
        uiServices.ChangeBloomIntensityLevel(EBloomIntensityLevel.Full);
        originalScope.SetActive(true); yield return null;
        Require(actualData.cameraStack.Count == 1 && actualData.cameraStack[0] == actualUiCamera,
            "Original main camera points to the instantiated UI camera");
        Require(actualListener.isActiveAndEnabled && AkAudioListener.DefaultListeners.ListenerList.Contains(actualListener),
            "Original Wwise default listener registered");
        Require(actualCuller.isActiveAndEnabled && actualSetter.isActiveAndEnabled && actualData.renderPostProcessing,
            "Original main camera services initialized");
        Destroy(originalScope); yield return null;
        Require(!AkAudioListener.DefaultListeners.ListenerList.Contains(actualListener)
            && actualEvents.WindowListeners == 0 && actualEvents.SceneListeners == 0,
            "Original main camera releases listener and event registrations");
        AkSoundEngine.Term();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class UIBloomProbe : IUIBloomManager
{
    private event Action<bool> changed;
    public bool BloomActive { get; private set; }
    public int ListenerCount { get; private set; }
    public void RegisterActiveListener(Action<bool> callback) { changed += callback; ListenerCount++; }
    public void UnregisterActiveListener(Action<bool> callback) { changed -= callback; ListenerCount--; }
    public void SetActive(bool value) { changed?.Invoke(value); BloomActive = value; }
}

public sealed class CameraMemoryState<T> : IPersistentState<T>
{
    public T State { get; }
    public int Writes { get; private set; }
    public CameraMemoryState(T state) { State = state; }
    public Task WriteStateAsync() { Writes++; return Task.CompletedTask; }
}

public sealed class CameraDeviceProbe : IDevice
{
    private readonly PerformanceLevel level;
    public CameraDeviceProbe(PerformanceLevel value = PerformanceLevel.Medium) { level = value; }
    public PerformanceLevel GetPerformanceLevel() => level;
}

public sealed class CameraEventProbe : ICameraWindowEvents, ICameraSceneEvents, ICameraPerformanceSetting, ICameraPopup
{
    private readonly IBloomIntensityService bloom;
    private event Action<GameObject> opened, closing;
    private event Action<CameraSceneData> scene;
    public CameraEventProbe(IBloomIntensityService value) { bloom = value; }
    public int WindowListeners { get; private set; }
    public int SceneListeners { get; private set; }
    public bool BloomEnabled => bloom.BloomEnabled;
    public bool FullScreenDisplay => true;
    public event Action<GameObject> OnWindowOpened { add { opened += value; WindowListeners++; } remove { opened -= value; WindowListeners--; } }
    public event Action<GameObject> OnWindowClosing { add { closing += value; WindowListeners++; } remove { closing -= value; WindowListeners--; } }
    public event Action<CameraSceneData> OnPreLoadScene { add { scene += value; SceneListeners++; } remove { scene -= value; SceneListeners--; } }
    public bool TryGetPopup(GameObject go, out ICameraPopup popup) { popup = this; return go; }
    public void Open(GameObject go) => opened?.Invoke(go);
    public void Close(GameObject go) => closing?.Invoke(go);
    public void Scene(SceneType type) => scene?.Invoke(new CameraSceneData { SceneType = type });
}

// This is an isolated dependency probe. The production bloom persistence and
// pointer dispatch still need their real host bindings.
public sealed class CameraServices : ICameraControlManager, IMapRootManagerProxy, IInputOperationModule, IBloomIntensityService
{
    private readonly CameraSettingsValue settings;
    private readonly GmCameraControlData gm = new GmCameraControlData();
    private readonly DragControllerManager drag = new DragControllerManager();
    private readonly Dictionary<Type, List<Delegate>> callbacks = new Dictionary<Type, List<Delegate>>();
    public Vector3 Pointer;
    public CameraControlBrain ControlBrain { get; private set; }
    public SafeAction<Vector3> ViewPosUpdated { get; } = new SafeAction<Vector3>();
    public SafeAction<float> ViewSizeUpdated { get; } = new SafeAction<float>();
    public GameObject MapRoot => null;
    public CameraGridGround GridGround => null;
    public Task PrepareInstantly(Vector3 position) => Task.CompletedTask;
    public BloomIntensityVolumeRefreshTrigger Registered { get; private set; }
    public EBloomIntensityLevel BloomIntensityLevel => EBloomIntensityLevel.Full;
    public bool BloomEnabled => true;
    public event Action OnBloomChange { add {} remove {} }
    public CameraServices(CameraControlSettings value) { settings = new CameraSettingsValue(value); }
    public bool Enable { get; set; } = true;
    public long Block<T>() where T : IInputOperationResult { throw new NotSupportedException(); }
    public long Block(InputOperationLayer layer) { throw new NotSupportedException(); }
    public void Unblock(long signature) { throw new NotSupportedException(); }
    public void Init(CameraControlBrain value) { ControlBrain = value; }
    public void Inject(object value)
    {
        if (value is CameraControlBrain brain) { brain.CameraControlManager = this; brain.InjectDependencies = Inject; }
        if (value is CameraBaseController controller) { controller.InputOperationModule = this; controller.mCameraControlSettings = settings; controller.GmCameraControlData = gm; }
        if (value is CameraStoryController story) story.MapRootManagerProxy = this;
        if (value is DragControllerBase d) { d.DragControllerManager = drag; d.PointerPosition = () => Pointer; d.TouchCount = () => 0; }
        if (value is IgnoreUIDragController input) input.InputOperationModule = this;
    }
    public void HookOnInput<T>(Action<T> callback) where T : IInputOperationResult
    { if (!callbacks.TryGetValue(typeof(T), out var list)) callbacks.Add(typeof(T), list = new List<Delegate>()); list.Add(callback); }
    public void UnhookOnInput<T>(Action<T> callback) where T : IInputOperationResult
    { if (callbacks.TryGetValue(typeof(T), out var list)) list.Remove(callback); }
    public void Dispatch<T>(T result) where T : IInputOperationResult
    { if (callbacks.TryGetValue(typeof(T), out var list)) foreach (var callback in list.ToArray()) ((Action<T>)callback)(result); }
    public void ChangeBloomIntensityLevel(EBloomIntensityLevel level) { throw new NotSupportedException(); }
    public bool ModifyBloomIntensity(Bloom value, float next, out float old) { old = -1; throw new NotSupportedException(); }
    public void RegisterVolume(BloomIntensityVolumeRefreshTrigger value) { Registered = value; }
    public void UnregisterVolume(BloomIntensityVolumeRefreshTrigger value) { if (Registered == value) Registered = null; }
}
