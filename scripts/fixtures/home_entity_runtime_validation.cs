using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;
using RuntimeManager = Zjcs.Official.HomeEntityRuntime.HomeEntityManager;
using RuntimeManagerInterface = Zjcs.Official.HomeEntityRuntime.IHomeEntityManager;
using static UnityEngine.Object;

[InitializeOnLoad]
public static class HomeEntityRuntimeValidation
{
    const string Active = "Zjcs.HomeEntityRuntimeValidation.Active";
    static IEnumerator checks;
    static int lastFrame = -1;
    static HomeEntityRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                checks = new HomeEntityRuntimeChecks().Run();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Home entity registration validation timed out"));
            if (checks != null && EditorApplication.isPlaying && Time.frameCount != lastFrame) {
                lastFrame = Time.frameCount;
                if (!checks.MoveNext()) checks = null;
            }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_ENTITY_RUNTIME_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_ENTITY_RUNTIME_PASSED: source scene/house slots, ordered data/clip registration, replacement/restoration, preview/weather branches, native playable graph and teardown. Component scope only.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed partial class HomeEntityRuntimeChecks
{
    readonly List<GameObject> roots = new List<GameObject>();
    readonly List<Object> assets = new List<Object>();
    Services services;
    int errors, expectedErrors;
    public IEnumerator Run()
    {
        AudioListener.volume = 0; Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Check()); Exception failure = null;
        while (stack.Count != 0)
        {
            object value;
            try {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                value = stack.Peek().Current;
                if (value is IEnumerator child) { stack.Push(child); continue; }
            } catch (Exception error) { failure = error; break; }
            yield return value;
        }
        foreach (var root in roots) if (root) Destroy(root);
        yield return null;
        foreach (var asset in assets) if (asset) Destroy(asset);
        services?.ReleaseFixtureHandles();
        Application.logMessageReceived -= OnLog;
        HomeEntityRuntimeValidation.Finish(failure ?? (errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)));
    }
    void OnLog(string text, string stack, LogType type)
    {
        if (expectedErrors > 0 && type == LogType.Error && text.StartsWith("Official HomeEntityManager.", StringComparison.Ordinal)) { expectedErrors--; return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    GameObject New(string name, Transform parent = null)
    { var go = new GameObject(name); go.SetActive(false); if (parent) go.transform.SetParent(parent, false); else roots.Add(go); return go; }
    T Keep<T>(T value) where T : Object { assets.Add(value); return value; }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(owner);
    static void References(UnityEngine.Object owner, string fieldName, UnityEngine.Object[] values)
    {
        var field = owner.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var list = (IList)Activator.CreateInstance(field.FieldType);
        foreach (var value in values) list.Add(value);
        field.SetValue(owner, list);
        Require(((IList)field.GetValue(owner)).Count == values.Length, "Fixture assigns original provider references before source creation");
    }
    static void Dictionary(UnityEngine.Object owner, string fieldName, IEnumerable<(int key, Transform value)> values)
    {
        var rows = values.ToArray(); var serialized = new SerializedObject(owner);
        var field = serialized.FindProperty(fieldName).FindPropertyRelative("mSerializeBuffer"); field.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++) {
            field.GetArrayElementAtIndex(i).FindPropertyRelative("Key").intValue = rows[i].key;
            field.GetArrayElementAtIndex(i).FindPropertyRelative("Value").objectReferenceValue = rows[i].value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        DeserializeDictionary(owner, fieldName);
    }
    static void DeserializeDictionary(UnityEngine.Object owner, string fieldName)
    {
        var field = owner.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        ((ISerializationCallbackReceiver)field.GetValue(owner)).OnAfterDeserialize();
    }
    [Serializable] public sealed class Inputs { public Scene scene; public House house; public Node[] nodes; }
    [Serializable] public sealed class Scene { public string root; public Slot[] furniture, themes; public Mapping[] mapping; }
    [Serializable] public sealed class House { public string root; public Slot[] furniture; public string[] data_types, authoring_json; }
    [Serializable] public sealed class Slot { public int key; public string node; }
    [Serializable] public sealed class Mapping { public int key, value; }
    [Serializable] public sealed class Node { public string key, name, parent; public bool active; public Vector3 position, scale; public Quaternion rotation; }

    IEnumerator Check()
    {
        var input = JsonUtility.FromJson<Inputs>(File.ReadAllText(Environment.GetEnvironmentVariable("ZJCS_ENTITY_RUNTIME_INPUT")));
        Require(input.scene.furniture.Length == 8 && input.house.furniture.Length == 5 && input.house.authoring_json.Length == 2,
            "Current original scene and default-house registration inputs are complete");
        Require(!input.scene.furniture.Any(row => row.key == (int)EFurniture.StorageBox) && input.house.furniture.Any(row => row.key == (int)EFurniture.StorageBox),
            "Storage slot belongs to the source house provider, not the baseline scene dictionary");
        var sceneRig = New("SourceSceneTransformFixture"); var prefabRig = New("ControlledPrefabFixtures");
        var nodes = new Dictionary<string, Transform>(); var definitions = input.nodes.ToDictionary(row => row.key);
        Transform Build(string key)
        {
            if (nodes.TryGetValue(key, out var existing)) return existing;
            Node source = definitions[key];
            bool houseNode = key == input.house.root || !string.IsNullOrEmpty(source.parent) && IsHouse(source.parent);
            var go = New(source.name, string.IsNullOrEmpty(source.parent) ? (houseNode ? prefabRig.transform : sceneRig.transform) : Build(source.parent));
            go.transform.localPosition = source.position; go.transform.localRotation = source.rotation; go.transform.localScale = source.scale;
            go.SetActive(true); nodes.Add(key, go.transform); return go.transform;
        }
        bool IsHouse(string key) => key == input.house.root || !string.IsNullOrEmpty(definitions[key].parent) && IsHouse(definitions[key].parent);
        foreach (var row in input.nodes) Build(row.key);
        var house = nodes[input.house.root].gameObject;
        var houseData = new List<HomeEntityData>(); int authoringIndex = 0;
        foreach (string type in input.house.data_types) {
            if (type == "HomeBindingAuthoring") {
                var binding = house.AddComponent<HomeBindingAuthoring>(); JsonUtility.FromJsonOverwrite(input.house.authoring_json[authoringIndex++], binding); houseData.Add(binding);
            } else if (type == "HomeSlotEntityData") {
                var slots = house.AddComponent<HomeSlotEntityData>(); Dictionary(slots, "mFurnitureDict", input.house.furniture.Select(row => (row.key, nodes[row.node]))); houseData.Add(slots);
            } else throw new NotSupportedException("Migrate actual house registration data: " + type);
        }
        var houseProvider = house.GetComponent<HomeEntityDataProvider>() ?? house.AddComponent<HomeEntityDataProvider>();
        References(houseProvider, "mDataList", houseData.Cast<Object>().ToArray());
        Require(houseProvider.DataDic.Values.Sum(rows => rows.Count) == input.house.data_types.Length,
            "Source house fixture exposes every original entity-data reference");
        var storage = New("FixtureStorage", prefabRig.transform); storage.SetActive(true);
        var rack = New("WeaponRack", storage.transform); rack.SetActive(true);
        var register = storage.AddComponent<HomeEntityNodeRegister>();
        var registerSerialized = new SerializedObject(register); var buffer = registerSerialized.FindProperty("GameObjectDict").FindPropertyRelative("mSerializeBuffer"); buffer.arraySize = 1;
        buffer.GetArrayElementAtIndex(0).FindPropertyRelative("Key").stringValue = "WeaponRack";
        buffer.GetArrayElementAtIndex(0).FindPropertyRelative("Value").objectReferenceValue = rack; registerSerialized.ApplyModifiedPropertiesWithoutUndo();
        DeserializeDictionary(register, "GameObjectDict");
        var probe = rack.AddComponent<HomeEntityRuntimeProbe>(); storage.AddComponent<HomeEntityRuntimeProxy>().Value = probe;
        var nativeStorage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/StorageBox/Native/StorageBox.prefab");
        var storageBinding = storage.AddComponent<HomeBindingAuthoring>(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(nativeStorage.GetComponent<HomeBindingAuthoring>()), storageBinding);
        var storageProvider = storage.GetComponent<HomeEntityDataProvider>() ?? storage.AddComponent<HomeEntityDataProvider>();
        References(storageProvider, "mDataList", new Object[] { storageBinding });
        Require(house.GetComponents<HomeEntityDataProvider>().Length == 1 && storage.GetComponents<HomeEntityDataProvider>().Length == 1,
            "Fixture respects the original binding's required data-provider component");
        var animated = New("ClockPart", storage.transform); animated.SetActive(true);
        var animator = storage.AddComponent<Animator>();
        var clip = Keep(new AnimationClip { name = "OriginalRegistrationClipFixture" }); clip.SetCurve("ClockPart", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 2, 2));
        var clips = storage.AddComponent<HomePlayableClipBind>(); var clipSerialized = new SerializedObject(clips); var clipRows = clipSerialized.FindProperty("ClipDatas"); clipRows.arraySize = 1;
        clipRows.GetArrayElementAtIndex(0).FindPropertyRelative("Clip").objectReferenceValue = clip;
        clipRows.GetArrayElementAtIndex(0).FindPropertyRelative("Animator").objectReferenceValue = animator; clipSerialized.ApplyModifiedPropertiesWithoutUndo();

        var tableAsset = Resources.Load<TextAsset>("Home/EntityCreation/catalog"); var catalogData = JsonUtility.FromJson<HomeEntityCreationCatalog.Payload>(tableAsset.text);
        var tables = new Tables(new HomeEntityCreationCatalog(catalogData));
        services = new Services(tables);
        foreach (var row in catalogData.FurnitureSkins.Where(row => row.IsDefault)) services.Furniture[row.FurnitureType] = row.SkinItemId;
        foreach (var row in catalogData.ThemeSkins.Where(row => row.IsDefault)) services.Themes[row.ThemeType] = row.SkinItemId;
        foreach (var row in catalogData.FurnitureSkins.Where(row => row.IsDefault)) {
            var prefab = row.FurnitureType == EFurniture.StorageBox ? storage : New("Fixture_" + row.FurnitureType, prefabRig.transform); prefab.SetActive(true);
            services.Prefabs[services.PrefabPaths.GetFurniturePrefabPath(row.SkinItemId)] = prefab;
        }
        foreach (var row in catalogData.ThemeSkins.Where(row => row.IsDefault)) {
            var prefab = row.ThemeType == HomeThemeType.House ? house : New("Fixture_" + row.ThemeType, prefabRig.transform); prefab.SetActive(true);
            services.Prefabs[services.PrefabPaths.GetThemePrefabPath(row.SkinItemId)] = prefab;
        }
        var root = nodes[input.scene.root].gameObject;
        var timeline = root.AddComponent<HomeTimelinePlayable>(); timeline.PlayableBinding = root.AddComponent<HomePlayableBinding>();
        timeline.DefaultTimelineAsset = Keep(ScriptableObject.CreateInstance<TimelineAsset>()); timeline.DefaultTimelineAsset.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.DefaultTimelineAsset.fixedDuration = 2;
        var mono = root.AddComponent<HomeEntityManagerMono>(); mono.HomeTimelinePlayable = timeline; mono.Services = services;
        Dictionary(mono, "FurnitureSlotDict", input.scene.furniture.Select(row => (row.key, nodes[row.node])));
        Dictionary(mono, "HomeThemeSlotDict", input.scene.themes.Select(row => (row.key, nodes[row.node])));
        var monoSerialized = new SerializedObject(mono); var map = monoSerialized.FindProperty("FurnitureCreateMapping").FindPropertyRelative("mSerializeBuffer"); map.arraySize = input.scene.mapping.Length;
        for (int i = 0; i < input.scene.mapping.Length; i++) { map.GetArrayElementAtIndex(i).FindPropertyRelative("Key").intValue = input.scene.mapping[i].key; map.GetArrayElementAtIndex(i).FindPropertyRelative("Value").intValue = input.scene.mapping[i].value; } monoSerialized.ApplyModifiedPropertiesWithoutUndo();
        DeserializeDictionary(mono, "FurnitureCreateMapping");
        DeserializeDictionary(mono, "FurnitureSlotDict"); DeserializeDictionary(mono, "HomeThemeSlotDict");
        Require(mono.FurnitureSlotDict.Count == input.scene.furniture.Length && mono.HomeThemeSlotDict.Count == input.scene.themes.Length,
            "Fixture deserializes all source slot dictionaries after the final owner serialization");
        services.Cache = new Cache(services.Manager, services.Trace);
        var dataAdds = new List<HomeEntityData>(); var dataRemoves = new List<HomeEntityData>();
        var add = mono.OnEntityDataAdd.Subscribe(value => {
            dataAdds.Add(value);
            if (value.gameObject.name.StartsWith("FixtureStorage", StringComparison.Ordinal))
                Require(Field<Dictionary<AnimationClip, UnityEngine.Animations.AnimationMixerPlayable>>(timeline, "mClipMixPlayableDict").ContainsKey(clip), "Entity clip binds before storage data-added notification");
        });
        var remove = mono.OnEntityDataRemove.Subscribe(value => {
            dataRemoves.Add(value);
            if (value.gameObject.name.StartsWith("FixtureStorage", StringComparison.Ordinal))
                Require(!Field<Dictionary<AnimationClip, UnityEngine.Animations.AnimationMixerPlayable>>(timeline, "mClipMixPlayableDict").ContainsKey(clip), "Entity clip unbinds before storage data-removed notification");
        });
        sceneRig.SetActive(true); yield return null;
        Require(services.Manager.Mono == mono && services.SubscriptionCount == 5, "Original Awake binds the manager and five owner subscriptions");
        var liveHouse = services.Manager.GetThemeGameObject(HomeThemeType.House);
        var liveStorage = services.Manager.GetFurnitureGameObject(EFurniture.StorageBox);
        var sourceStorageSlot = input.house.furniture.Single(row => row.key == (int)EFurniture.StorageBox);
        string Relative(string key, string stop) => key == stop ? "" : string.IsNullOrEmpty(Relative(definitions[key].parent, stop)) ? definitions[key].name : Relative(definitions[key].parent, stop) + "/" + definitions[key].name;
        Require(liveStorage && liveStorage.transform.parent == liveHouse.transform.Find(Relative(sourceStorageSlot.node, input.house.root)),
            "Original house data dynamically supplies the actual interior storage slot");
        Require(liveStorage.transform.parent.localPosition == definitions[sourceStorageSlot.node].position,
            "Registered storage slot retains its original local pose");
        Require(dataAdds.Count == 4 && dataAdds.Take(3).Select(value => value.GetType().Name).SequenceEqual(input.house.data_types), "Original provider grouping and ordered data-added events");
        var bindings = new List<HomeBindingAuthoring>(); mono.GetDatas(bindings); Require(bindings.Count == 3, "Type-grouped entity agent includes both house profiles and storage binding");
        var snapshot = services.Cache.GetSnapshot(EHomeActorBindingProfile.Ground); Require(snapshot != null, "Actual binding cache consumes the new manager's registered data agent");
        Require(services.Trace.TakeLast(3).SequenceEqual(new[] { "dirty", "tips:True", "ui:True" }), "Original post-create dirty, tip and UI notification order");
        var named = services.Manager.GetFurnitureGameObject(new FurnitureBindInfo { FurnitureType = EFurniture.StorageBox, Key = "WeaponRack" });
        Require(named && named.name == "WeaponRack" && !services.Manager.GetFurnitureGameObject(new FurnitureBindInfo { FurnitureType = EFurniture.StorageBox, Key = "absent" }), "Furniture facade resolves only original named registry entries");
        Require(services.Manager.GetFurnitureComponent<HomeEntityRuntimeProbe>(EFurniture.StorageBox) == named.GetComponent<HomeEntityRuntimeProbe>(), "Furniture component facade uses the original typed proxy fallback");
        Require(input.scene.mapping.All(row => services.Manager.GetFurnitureGameObject((EFurniture)row.key) ==
            services.Manager.GetFurnitureGameObject((EFurniture)row.value)), "Source furniture aliases resolve to the same creator-owned entity");
        timeline.DoUpdate(.5f); Require(Mathf.Abs(liveStorage.transform.Find("ClockPart").localPosition.x - 1) < .02f, "Registered clip evaluates on the real native playable graph");

        int oldHandles = services.Handles.Count; services.Trace.Clear(); services.HomeChanged(); yield return null;
        Require(services.Handles.Count == oldHandles && services.Manager.GetFurnitureGameObject(EFurniture.StorageBox) == liveStorage,
            "Unchanged home notifications preserve all creator-owned instances");
        int themeId = services.Themes[HomeThemeType.House]; const int variantId = 990001;
        tables.Theme[variantId] = new HomeThemeSkinInfo { SkinItemId = variantId, ThemeType = HomeThemeType.House, PrefabName = "ControlledHouseVariant" };
        services.Prefabs[services.PrefabPaths.GetThemePrefabPath(variantId)] = house;
        services.ThemeChanged(HomeThemeType.House, variantId); yield return null;
        var nextStorage = services.Manager.GetFurnitureGameObject(EFurniture.StorageBox);
        Require(!liveStorage && nextStorage && nextStorage != liveStorage && dataRemoves.Count == 4 && dataAdds.Count == 8,
            "House replacement unregisters old data/clips and recreates furniture under new provider slots");
        mono.GetDatas(bindings); Require(bindings.Count == 3 && bindings.All(value => value), "Type sets contain only live replacement components");

        var overlay = New("ControlledSlotOverride", prefabRig.transform); overlay.SetActive(true);
        var overlayData = overlay.AddComponent<HomeSlotEntityData>();
        var outdoor = input.scene.furniture.First(); var baseline = nodes[outdoor.node];
        Dictionary(overlayData, "mFurnitureDict", new[] { (outdoor.key, overlay.transform) });
        var overlayProvider = overlay.AddComponent<HomeEntityDataProvider>(); References(overlayProvider, "mDataList", new Object[] { overlayData });
        mono.OnEntityCreated(overlay);
        Require(Field<Dictionary<EFurniture, Transform>>(mono, "mFurnitureSlotDict")[(EFurniture)outdoor.key] == overlay.transform, "Provider can replace an existing baseline slot by reference");
        mono.OnEntityWillDestroy(overlay); yield return null;
        Require(Field<Dictionary<EFurniture, Transform>>(mono, "mFurnitureSlotDict")[(EFurniture)outdoor.key] == baseline &&
            Field<Dictionary<EFurniture, Transform>>(mono, "mFurnitureSlotDict").ContainsKey(EFurniture.StorageBox), "Slot removal restores baseline entries and preserves other providers' interior slots");
        services.HomeChanged(); yield return null;

        int dirty = services.Cache.DirtyCount; services.Preview(new HomeSkinPreviewData { ItemId = int.MinValue });
        Require(services.Cache.DirtyCount == dirty, "Unknown preview item returns before changing creators or binding caches");
        int storageSkin = services.Furniture[EFurniture.StorageBox]; oldHandles = services.Handles.Count;
        services.Items[storageSkin] = new HomeEntityItemInfo { ItemType = HomeEntitySkinItemType.FurnitureSkin };
        services.Preview(new HomeSkinPreviewData { ItemId = storageSkin });
        Require(services.Cache.DirtyCount == dirty + 1 && services.Handles.Count == oldHandles,
            "Furniture preview routes through the original same-path branch and invalidates bindings");
        services.Items[variantId] = new HomeEntityItemInfo { ItemType = HomeEntitySkinItemType.HomeThemeSkin };
        services.Preview(new HomeSkinPreviewData { ItemId = variantId }); yield return null;
        Require(services.LoadedPaths.Last(path => path.Contains("/Theme/House/")).EndsWith("ControlledHouseVariant", StringComparison.Ordinal),
            "Theme preview selects its configured theme before recreating interior registrations");
        services.HomeChanged(); yield return null;
        const int weatherId = 990002;
        services.Items[weatherId] = new HomeEntityItemInfo { ItemType = HomeEntitySkinItemType.HomeEnvironmentSkin };
        services.EnvironmentItems[weatherId] = new HomeEntityEnvironmentSkinInfo { EnvironmentType = HomeEnvironmentType.Weather };
        tables.Weather[(themeId, weatherId)] = new HomeSkinWeatherOverridePrefabInfo { SkinId = themeId, WeatherSkinId = weatherId, PrefabName = "ControlledWeatherHouse" };
        services.Prefabs[services.PrefabPaths.GetThemePrefabPath(themeId, "ControlledWeatherHouse")] = house;
        services.Preview(new HomeSkinPreviewData { ItemId = weatherId }); yield return null;
        Require(mono.PreviewWeatherId == weatherId && services.LoadedPaths.Last(path => path.Contains("/Theme/House/" )).EndsWith("ControlledWeatherHouse", StringComparison.Ordinal), "Weather preview selects the original preview override before recreation");
        services.Preview(new HomeSkinPreviewData { ItemId = weatherId, IsReset = true }); yield return null;
        Require(mono.PreviewWeatherId == 0, "Weather reset restores player-weather selection");
        services.Preview(new HomeSkinPreviewData { ItemId = weatherId }); yield return null;
        services.EnvironmentChanged(HomeEnvironmentType.Weather, 0); yield return null;
        Require(mono.PreviewWeatherId == 0, "Committed weather change clears the preview before updating entities");
        services.OwnHome = false; services.Trace.Clear(); services.HomeChanged(); yield return null;
        Require(services.Trace.TakeLast(3).SequenceEqual(new[] { "dirty", "tips:False", "ui:False" }), "Visit mode uses original tip and home-UI gates after reconstruction");
        add.Dispose(); remove.Dispose(); Destroy(sceneRig); yield return null;
        Require(!services.Manager.Mono && services.SubscriptionCount == 0, "Root teardown disposes subscriptions and unbinds the facade");
        expectedErrors = 1; Require(!services.Manager.GetFurnitureGameObject(EFurniture.StorageBox) && expectedErrors == 0,
            "Disposed facade keeps the original missing-manager diagnostic and null result");
        yield return CheckTimeline();
        Resources.UnloadAsset(tableAsset);
    }

    sealed class Lease : IDisposable { Action action; public Lease(Action action) { this.action = action; } public void Dispose() { var value = action; action = null; value?.Invoke(); } }
    sealed class Cache : IHomeRuntimeBindingCacheService
    {
        readonly HomeRuntimeBindingCacheService value; readonly List<string> trace; public int DirtyCount;
        public Cache(RuntimeManager manager, List<string> trace) { value = new HomeRuntimeBindingCacheService(manager); this.trace = trace; }
        public void MarkDirty() { DirtyCount++; trace.Add("dirty"); value.MarkDirty(); }
        public HomeRuntimeBindingSnapshot GetSnapshot(EHomeActorBindingProfile profile) => value.GetSnapshot(profile);
    }
    sealed class Tables : IHomeEntityPrefabTables
    {
        readonly IHomeEntityPrefabTables original;
        public readonly Dictionary<int, HomeThemeSkinInfo> Theme = new Dictionary<int, HomeThemeSkinInfo>();
        public readonly Dictionary<(int, int), HomeSkinWeatherOverridePrefabInfo> Weather = new Dictionary<(int, int), HomeSkinWeatherOverridePrefabInfo>();
        public Tables(IHomeEntityPrefabTables original) { this.original = original; }
        public FurnitureSkinInfo GetFurnitureSkin(int id) => original.GetFurnitureSkin(id);
        public HomeThemeSkinInfo GetThemeSkin(int id) => Theme.TryGetValue(id, out var value) ? value : original.GetThemeSkin(id);
        public HomeSkinWeatherOverridePrefabInfo GetWeatherOverride(int skin, int weather) => Weather.TryGetValue((skin, weather), out var value) ? value : original.GetWeatherOverride(skin, weather);
    }
    sealed class Handle : IHomeEntityPrefabHandle { public GameObject Prefab; public int Releases; public GameObject Result => Prefab; public void Release() { Releases++; } }
    sealed class Services : IHomeEntityRuntimeServices, IHomeEntityResourceLoader, IHomeEntitySkinData, IHomeFurnitureChangePublisher
    {
        public readonly RuntimeManager Manager = new RuntimeManager(); public readonly List<string> Trace = new List<string>(), LoadedPaths = new List<string>();
        public readonly Dictionary<EFurniture, int> Furniture = new Dictionary<EFurniture, int>(); public readonly Dictionary<HomeThemeType, int> Themes = new Dictionary<HomeThemeType, int>();
        public readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(); public readonly List<Handle> Handles = new List<Handle>();
        public readonly Dictionary<int, HomeEntityItemInfo> Items = new Dictionary<int, HomeEntityItemInfo>(); public readonly Dictionary<int, HomeEntityEnvironmentSkinInfo> EnvironmentItems = new Dictionary<int, HomeEntityEnvironmentSkinInfo>();
        public Cache Cache; public bool OwnHome = true;
        Action<HomeSkinPreviewData> preview; Action home; Action<EFurniture, int> furniture; Action<HomeThemeType, int> theme; Action<HomeEnvironmentType, int> environment;
        public IHomeEntityResourceLoader ResourceLoader => this; public IHomeEntitySkinData PlayerData => this; public IHomeEntityPrefabTables Tables { get; }
        public HomeEntityPrefabPaths PrefabPaths { get; } public IHomeFurnitureChangePublisher FurnitureChangePublisher => this;
        public RuntimeManagerInterface EntityManager => Manager; public IHomeRuntimeBindingCacheService RuntimeBindingCacheService => Cache; public bool InMyHome => OwnHome;
        public Services(IHomeEntityPrefabTables tables) { Tables = tables; PrefabPaths = new HomeEntityPrefabPaths(tables); }
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays) => Object.Instantiate(prefab, parent, worldPositionStays);
        public IHomeEntityPrefabHandle LoadPrefab(string path) { LoadedPaths.Add(path); if (!Prefabs.TryGetValue(path, out var value)) throw new Exception("Fixture explicitly supplies this original route: " + path); var handle = new Handle { Prefab = value }; Handles.Add(handle); return handle; }
        public int GetFurnitureSkinId(EFurniture type) => Furniture[type]; public int GetThemeSkinId(HomeThemeType type) => Themes[type]; public int GetEnvironmentSkinId(HomeEnvironmentType type) => 0;
        public void Publish(HomeFurnitureChangeData data) => Trace.Add("furniture:" + data.Furniture);
        public IDisposable SubscribeSkinPreview(Action<HomeSkinPreviewData> action) { preview += action; return new Lease(() => preview -= action); }
        public IDisposable SubscribeHomeChange(Action action) { home += action; return new Lease(() => home -= action); }
        public IDisposable SubscribeFurnitureSkinChanged(Action<EFurniture, int> action) { furniture += action; return new Lease(() => furniture -= action); }
        public IDisposable SubscribeThemeSkinChanged(Action<HomeThemeType, int> action) { theme += action; return new Lease(() => theme -= action); }
        public IDisposable SubscribeEnvironmentSkinChanged(Action<HomeEnvironmentType, int> action) { environment += action; return new Lease(() => environment -= action); }
        public void Preview(HomeSkinPreviewData data) => preview?.Invoke(data); public void HomeChanged() => home?.Invoke();
        public void ThemeChanged(HomeThemeType type, int id) => theme?.Invoke(type, id); public void EnvironmentChanged(HomeEnvironmentType type, int id) => environment?.Invoke(type, id);
        public HomeEntityItemInfo GetItemInfo(int id) => Items.TryGetValue(id, out var value) ? value : null;
        public FurnitureSkinInfo GetFurnitureSkin(int id) => Tables.GetFurnitureSkin(id); public HomeThemeSkinInfo GetThemeSkin(int id) => Tables.GetThemeSkin(id);
        public HomeEntityEnvironmentSkinInfo GetEnvironmentSkin(int id) => EnvironmentItems[id];
        public void SetShowTipDot(bool value) => Trace.Add("tips:" + value); public void SetHomeSceneUIActive(bool value) => Trace.Add("ui:" + value);
        static int Count(Delegate value) => value?.GetInvocationList().Length ?? 0;
        public int SubscriptionCount => Count(preview) + Count(home) + Count(furniture) + Count(theme) + Count(environment);
        public void ReleaseFixtureHandles() { foreach (var handle in Handles) if (handle.Releases == 0) handle.Release(); }
    }
}
