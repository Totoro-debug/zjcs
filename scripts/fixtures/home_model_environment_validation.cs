using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeModelEnvironment;
using Zjcs.Official.HomeRelations;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;
using EntityManager = Zjcs.Official.HomeEntityRuntime.IHomeEntityManager;

[InitializeOnLoad]
public static class HomeModelEnvironmentValidation
{
    const string Active = "Zjcs.HomeModelEnvironmentValidation.Active";
    static IEnumerator checks; static int frame = -1, errors;
    static readonly List<GameObject> roots = new List<GameObject>();
    static readonly List<Object> assets = new List<Object>();
    static HomeModelEnvironmentValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false)) {
                AudioListener.volume = 0; Application.runInBackground = true;
                Application.logMessageReceived += OnLog; checks = Check();
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Native home model environment validation timed out"));
            if (checks == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (!checks.MoveNext()) { checks = null; Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)); } }
            catch (Exception failure) { checks = null; Finish(failure); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_HOME_MODEL_ENVIRONMENT_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Finish(Exception failure)
    {
        SessionState.SetBool(Active, false);
        foreach (var root in roots) if (root) Object.DestroyImmediate(root);
        foreach (var asset in assets) if (asset) Object.DestroyImmediate(asset);
        Application.logMessageReceived -= OnLog;
        if (failure == null) Debug.Log("HOME_MODEL_ENVIRONMENT_PASSED: exact original light schedules/overrides, full native default-house hierarchy and actual rendered frames, notification gates/queue/intensity/teardown, original Spine day clock sampling. Native house component scope; full city/Player play remains separate.");
        else Debug.LogException(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static GameObject Root(string name) { var go = new GameObject(name); go.SetActive(false); roots.Add(go); return go; }
    static T Keep<T>(T value) where T : Object { assets.Add(value); return value; }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Tick(HomeLightController controller) => controller.gameObject.SendMessage("Update", SendMessageOptions.RequireReceiver);
    static HomeDayTime Moment(int hour, int minute = 0, int second = 0) => new HomeDayTime { Hour = hour, Minute = minute, Second = second };
    static bool Near(float left, float right) => Mathf.Abs(left - right) < .001f;
    static Dictionary<string, GameObject> LightObjects(HomeLightController value)
    {
        var result = new Dictionary<string, GameObject>();
        foreach (object row in Field<IList>(value, "mLightObjs")) {
            Type type = row.GetType(); result.Add((string)type.GetField("Name").GetValue(row), (GameObject)type.GetField("GameObject").GetValue(row));
        }
        return result;
    }
    static Dictionary<string, bool> LightStates(HomeLightController value)
    {
        var result = new Dictionary<string, bool>();
        foreach (object row in Field<IList>(value, "mLightObjs")) {
            Type type = row.GetType(); result.Add((string)type.GetField("Name").GetValue(row), (bool)type.GetField("IsOn").GetValue(row));
        }
        return result;
    }
    static void AssertStates(HomeLightController value, string[] on, string text)
    {
        var states = LightStates(value); var objects = LightObjects(value);
        Require(states.All(pair => pair.Value == on.Contains(pair.Key)) && objects.All(pair => pair.Value && pair.Value.activeSelf == states[pair.Key]),
            text + "; states=" + string.Join(",", states.Select(pair => pair.Key + "=" + pair.Value)) +
            "; active=" + string.Join(",", objects.Select(pair => pair.Key + "=" + (pair.Value ? pair.Value.activeSelf.ToString() : "destroyed"))));
    }
    static void CheckSchedules(HomeModelEnvironmentCatalog.Payload payload)
    {
        var config = payload.Lights;
        Require(config.HomeLightTimeConfigs.Count == 4 && config.GetHomeLightStatus(Moment(17, 30), "Idle") == null,
            "Actual original empty 17:00 interval remains an explicit no-update result");
        Require(config.GetHomeLightStatus(Moment(5, 59, 59), "Read").Single(row => row.Name == "DeskLight").IsOn &&
            config.GetHomeLightStatus(Moment(6), "Read").All(row => !row.IsOn), "Original schedule switches exactly at 06:00");
        Require(config.GetHomeLightStatus(Moment(18), "Eat").Single(row => row.Name == "DiningTableLight").IsOn &&
            config.GetHomeLightStatus(Moment(23), "Sleep").Where(row => row.Name == "LobbyLight" || row.Name == "BedRoomLight" || row.Name == "StudyLight").All(row => !row.IsOn),
            "Current original Eat/Sleep behavior overrides address the configured array indices");
        var day = config.GetHomeLightStatus(Moment(6), "Idle").ToArray();
        Require(day.Count(row => row.Name == "StudyLight") == 2, "Original repeated daytime light entries are retained");
        var synthetic = new HomeLightTimeConfig { DefaultLights = new[] { new HomeLight { Name = "ControlledLamp", IsOn = false } },
            Overrides = new[] { new HomeLightBehaviourOverride { BehaviourType = "None", LightOverrides = new[] { new HomeLightOverride { Index = 0, IsOn = true } } },
                new HomeLightBehaviourOverride { BehaviourType = "Read", LightOverrides = new[] { new HomeLightOverride { Index = 0, IsOn = false } } } } };
        var first = synthetic.GetHomeLights("Read");
        Require(first[0].IsOn, "Original first wildcard override wins before later matching behavior overrides");
        first[0].IsOn = false;
        Require(ReferenceEquals(first, synthetic.GetHomeLights("Idle")) && first[0].IsOn && !synthetic.DefaultLights[0].IsOn,
            "Original light cache is reused and repopulated without mutating defaults");
        synthetic.DefaultLights = new[] { new HomeLight { Name = "A" }, new HomeLight { Name = "B" } };
        Require(synthetic.GetHomeLights("Idle").Length == 2, "Original cached light array resizes after a configuration shape change");
        Require(new HomeModelLightConfiguration { HomeLightTimeConfigs = new List<HomeLightTimeConfig>() }.GetHomeLightStatus(Moment(1), "Idle") == null,
            "Empty schedule retains its original null result");
    }
    static IEnumerator Check()
    {
        var payload = HomeModelEnvironmentCatalog.Load(); CheckSchedules(payload);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/House/Native/House.prefab");
        Require(prefab, "Complete original default-house prefab is installed");
        var stage = Root("NativeDefaultHouseStage"); var house = Object.Instantiate(prefab, stage.transform, false);
        var components = house.GetComponentsInChildren<Component>(true);
        Require(components.All(value => value) && house.GetComponentsInChildren<Transform>(true).Length == 98 &&
            house.GetComponentsInChildren<MonoBehaviour>(true).Length == 32, "All original 98 transforms and 32 script components retain native bindings");
        var lights = house.GetComponentInChildren<HomeLightController>(true); var clock = house.GetComponentInChildren<HomeTimeDriveClock>(true);
        var relations = new HomeRelationManager();
        foreach (var relation in house.GetComponentsInChildren<HomeMutex>(true)) relation.InjectRelationManager = relations;
        var facade = new FurnitureFacade();
        foreach (var type in new[] { EFurniture.Desk, EFurniture.DiningTable }) {
            var lightRoot = Root("ExplicitFurnitureLightFixture_" + type); facade.Lights[type] = lightRoot;
        }
        var services = new Services(payload) { Time = Moment(0), Behaviour = "Idle", Delta = 0 };
        lights.Services = services; lights.HomeEntityManager = facade; clock.Services = services;
        Require(Field<float>(lights, "mLightTranslateDuration") == .5f && Field<List<SkeletonAnimation>>(clock, "mSkeletonAnimationList").Count == 2,
            "Actual original house retains its authored half-second light spacing and two Spine clocks");
        var controlled = Field<Light2D[]>(lights, "mIntensityControlLights"); var originalIntensity = controlled.Select(value => value.intensity).ToArray();
        stage.SetActive(true); yield return null; yield return null;
        Require(services.Subscribers == 1 && facade.Queries.Count == 2, "Original Awake/Start subscribes once and resolves both furniture lights");
        Require(house.GetComponent<HomeEntityDataProvider>().DataDic.Values.Sum(rows => rows.Count) == 3 &&
            house.GetComponent<HomeSlotEntityData>().FurnitureDict.Count == 5, "Actual native house exposes both actor profiles and all original interior furniture slots");
        var skeletons = house.GetComponentsInChildren<SkeletonAnimation>(true);
        Require(skeletons.Length == 6 && skeletons.All(value => value.valid && value.state != null && value.state.GetCurrent(0) != null),
            "All six original Spine models initialize their original binary, atlas, material and configured animation");
        var clocks = Field<List<SkeletonAnimation>>(clock, "mSkeletonAnimationList");
        Require(clocks.All(value => value.UpdateTiming == (UpdateTiming)0), "Original clock Awake switches both models to manual Spine updates");
        AssertStates(lights, new[] { "StudyLight", "LobbyLight", "BedRoomLight" }, "Native house starts with original night lights and furniture lights off");
        Require(controlled.Select((value, i) => Near(value.intensity, originalIntensity[i] * 3f / 5)).All(value => value), "Actual native light intensity follows the original on-count ratio including furniture lights");
        Capture(house, "native-house-night.png");

        services.Behaviour = "Sleep"; services.Publish(BehaviourActTiming.None, true);
        AssertStates(lights, new[] { "StudyLight", "LobbyLight", "BedRoomLight" }, "Unrelated behavior timing leaves the original light state unchanged");
        services.Limited = true; services.Publish(BehaviourActTiming.EnterEnd, true);
        AssertStates(lights, new[] { "StudyLight", "LobbyLight", "BedRoomLight" }, "Natural-time function limit gates behavior notifications");
        services.Limited = false; services.Publish(BehaviourActTiming.EnterEnd, false);
        Require(((ICollection)Field<object>(lights, "mLightTransitions")).Count == 3,
            "Original Sleep notification queues three light changes without applying them immediately");
        Tick(lights); AssertStates(lights, new[] { "StudyLight", "BedRoomLight" }, "First queued change runs immediately at the original schedule's lobby-light entry");
        Tick(lights); AssertStates(lights, new[] { "StudyLight", "BedRoomLight" }, "Zero playable delta keeps the remaining transition timer paused");
        services.Delta = .25f; Tick(lights); AssertStates(lights, new[] { "StudyLight", "BedRoomLight" }, "Half the authored spacing does not dequeue the next light");
        Tick(lights); AssertStates(lights, new[] { "StudyLight" }, "Original half-second boundary applies the schedule's bedroom-light entry");
        services.Delta = .5f; Tick(lights); AssertStates(lights, Array.Empty<string>(), "Remaining original Sleep transition turns the study light off");
        Require(controlled.All(value => Near(value.intensity, 0) && !value.gameObject.activeSelf), "Zero on-count disables the actual intensity-controlled light");
        services.Delta = 0; services.Behaviour = "Read"; services.Publish(BehaviourActTiming.ExitEnd, true);
        AssertStates(lights, new[] { "StudyLight", "LobbyLight", "BedRoomLight", "DeskLight" }, "Original ExitEnd immediate branch turns on reading lights and flushes the queue");
        Require(controlled.Select((value, i) => Near(value.intensity, originalIntensity[i] * 4f / 5)).All(value => value), "Furniture behavior light participates in the original intensity ratio");
        services.Behaviour = "Eat"; services.Publish(BehaviourActTiming.EnterEnd, false);
        Require(((ICollection)Field<object>(lights, "mLightTransitions")).Count == 2, "Eating queues desk-off and dining-on in original configuration order");
        services.Behaviour = "Read"; services.Publish(BehaviourActTiming.EnterEnd, true);
        AssertStates(lights, new[] { "StudyLight", "LobbyLight", "BedRoomLight", "DeskLight" }, "New notification flushes pending transitions before selecting its own behavior changes");
        services.Time = Moment(6); services.Behaviour = "Idle"; services.Publish(BehaviourActTiming.EnterEnd, true);
        AssertStates(lights, Array.Empty<string>(), "Actual native house applies the original daytime lights");
        Capture(house, "native-house-day.png");
        services.Time = Moment(18); services.Behaviour = "Read"; services.Publish(BehaviourActTiming.EnterEnd, true);
        services.Time = Moment(17, 30); services.Behaviour = "Sleep"; services.Publish(BehaviourActTiming.EnterEnd, true);
        AssertStates(lights, new[] { "StudyLight", "LobbyLight", "BedRoomLight", "DeskLight" }, "Original empty dusk interval preserves current light states");

        var oldDesk = facade.Lights[EFurniture.Desk]; Object.Destroy(oldDesk); yield return null;
        var newDesk = Root("ExplicitReplacementDeskLightFixture"); newDesk.SetActive(true); facade.Lights[EFurniture.Desk] = newDesk;
        int queries = facade.Queries.Count; services.Time = Moment(0); services.Behaviour = "Sleep"; services.Publish(BehaviourActTiming.EnterEnd, true);
        Require(!newDesk.activeSelf && facade.Queries.Count > queries && LightStates(lights).Values.All(value => !value),
            "Original light transition re-resolves a destroyed furniture reference from its configured key");

        float[] firstPose = null; bool poseChanged = false;
        foreach (var time in new[] { Moment(0), Moment(6), Moment(12), Moment(18), Moment(23, 59, 59) }) {
            services.Time = time; clock.gameObject.SendMessage("Update", SendMessageOptions.RequireReceiver);
            float progress = time.GetDayProgress(services.TimeConfiguration);
            Require(clocks.All(value => Near(value.state.GetCurrent(0).TrackTime, progress * value.state.GetCurrent(0).Animation.Duration)),
                "Actual original clock track times follow normalized day progress: " + time);
            var pose = clocks.SelectMany(value => value.Skeleton.Bones.Select(bone => bone.Rotation)).ToArray();
            if (firstPose == null) firstPose = pose; else poseChanged |= pose.Where((value, i) => !Near(value, firstPose[i])).Any();
            foreach (var skeleton in clocks) skeleton.LateUpdate();
        }
        Require(poseChanged && clocks.All(value => value.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0),
            "Clock sampling changes actual original bone poses and retains native rendered mesh geometry");
        var clockBody = clocks.Single(value => value.name == "ClockBody");
        services.Time = Moment(6); clock.gameObject.SendMessage("Update", SendMessageOptions.RequireReceiver);
        foreach (var skeleton in clocks) skeleton.LateUpdate(); var morning = Capture(clockBody.gameObject, "native-clock-morning.png");
        services.Time = Moment(18); clock.gameObject.SendMessage("Update", SendMessageOptions.RequireReceiver);
        foreach (var skeleton in clocks) skeleton.LateUpdate(); var evening = Capture(clockBody.gameObject, "native-clock-evening.png");
        Require(!morning.SequenceEqual(evening), "Original clock face produces different actual rendered frames across the day");
        lights.enabled = false; services.Behaviour = "Idle";
        Require(services.Subscribers == 1, "Disabling the original light component retains its owner subscription");
        lights.enabled = true;
        Require(LightStates(lights).Where(pair => pair.Key != "DeskLight").All(pair => pair.Value == new[] { "StudyLight", "LobbyLight", "BedRoomLight" }.Contains(pair.Key)),
            "Original re-enable immediately applies current behavior after Start");
        Object.Destroy(stage); yield return null; yield return null;
        Require(services.Subscribers == 0 && !house, "Native house destruction disposes its original notification lifetime");
    }

    sealed class Lease : IDisposable { Action action; public Lease(Action value) { action = value; } public void Dispose() { var value = action; action = null; value?.Invoke(); } }
    sealed class Services : IHomeModelEnvironmentServices
    {
        readonly HomeModelEnvironmentCatalog.Payload payload; Action<PlayerBehaviourNotifyData> notify;
        public HomeDayTime Time; public string Behaviour; public float Delta; public bool Limited;
        public Services(HomeModelEnvironmentCatalog.Payload value) { payload = value; TimeConfiguration = value.Clock.CreateConfiguration(); }
        public HomeDayTime HomeDayTime => Time; public HomeEnvironmentConfig TimeConfiguration { get; }
        public HomeModelLightConfiguration LightConfiguration => payload.Lights;
        public string CurrentPlayerBehaviour => Behaviour; public float PlayableDeltaTime => Delta; public bool IsHomeTimeLimit => Limited;
        public int Subscribers => notify?.GetInvocationList().Length ?? 0;
        public IDisposable SubscribePlayerBehaviour(Action<PlayerBehaviourNotifyData> callback) { notify += callback; return new Lease(() => notify -= callback); }
        public void Publish(BehaviourActTiming timing, bool immediately) => notify?.Invoke(new PlayerBehaviourNotifyData { BehaviourId = Behaviour, ActTiming = timing, Immediately = immediately });
    }
    sealed class FurnitureFacade : EntityManager
    {
        public readonly Dictionary<EFurniture, GameObject> Lights = new Dictionary<EFurniture, GameObject>();
        public readonly List<FurnitureBindInfo> Queries = new List<FurnitureBindInfo>();
        public HomeEntityManagerMono Mono => null; public IHomeEntityDataAgent EntityDataAgent => null;
        public void BindMono(HomeEntityManagerMono value) { throw new NotSupportedException(); } public void Dispose() { }
        public GameObject GetFurnitureGameObject(FurnitureBindInfo value) { Queries.Add(value); return Lights.TryGetValue(value.FurnitureType, out var result) ? result : null; }
        public T GetFurnitureComponent<T>(FurnitureBindInfo value) where T : Component => GetFurnitureGameObject(value)?.GetComponent<T>();
        public GameObject GetThemeGameObject(ThemeBindInfo value) => null;
    }
    static byte[] Capture(GameObject house, string file)
    {
        var renderers = house.GetComponentsInChildren<Renderer>(true).Where(value => value.enabled && value.gameObject.activeInHierarchy).ToArray();
        Require(renderers.Length > 0 && renderers.All(value => value.sharedMaterials.All(material => material && material.shader && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader))),
            "Original house renderers retain working native materials and shaders");
        Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        var root = Root("NativeHouseRenderCamera"); var camera = root.AddComponent<Camera>();
        camera.orthographic = true; camera.aspect = 720f / 1280f;
        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.08f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 30);
        camera.nearClipPlane = .01f; camera.farClipPlane = 100; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .08f, .11f, 1);
        var target = new RenderTexture(720, 1280, 24); var image = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
        var prior = RenderTexture.active; camera.targetTexture = target; root.SetActive(true);
        try {
            camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0); image.Apply();
            var pixels = image.GetPixels32();
            Require(pixels.Count(pixel => pixel.r > 220 && pixel.b > 220 && pixel.g < 30) < pixels.Length / 1000 &&
                pixels.Count(pixel => pixel.r > 60 || pixel.g > 60 || pixel.b > 70) > 10000, "Actual native house frame contains visible original art without shader error pixels");
            byte[] png = image.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_HOME_MODEL_ENVIRONMENT_OUTPUT"), file), png);
            return png;
        } finally {
            camera.targetTexture = null; RenderTexture.active = prior; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); Object.Destroy(root);
        }
    }
}
