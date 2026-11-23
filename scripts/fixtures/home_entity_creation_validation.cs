using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeEntityCreationValidation
{
    const string Active = "Zjcs.HomeEntityCreationValidation.Active";
    static HomeEntityCreationValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("OriginalEntityCreatorChecks").AddComponent<HomeEntityCreationChecks>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original entity creator checks timed out"));
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_ENTITY_CREATION_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Use the selected independent Unity project");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_ENTITY_CREATION_PASSED: original creator cache/mapping, addresses, weather, offsets, replacement/release order, failure branches and native storage instantiation. Full home-host integration remains separate.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeEntityCreationMarker : MonoBehaviour, IHomeEntityChangeCreateNotify
{
    public static Action<string> Trace;
    public void OnChangeCreate() => Trace?.Invoke("changed:" + name);
}

public sealed class HomeEntityCreationChecks : MonoBehaviour
{
    readonly List<GameObject> roots = new List<GameObject>();
    readonly List<Context> contexts = new List<Context>();
    int errors, expectedErrors;
    IEnumerator Start()
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
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
            } catch (Exception error) { failure = error; break; }
            yield return value;
        }
        foreach (var root in roots) if (root) Destroy(root);
        foreach (var context in contexts) context.ReleaseFixtureHandles();
        HomeEntityCreationMarker.Trace = null;
        yield return null;
        Application.logMessageReceived -= OnLog;
        HomeEntityCreationValidation.Finish(failure ?? (errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)));
    }
    void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error && expectedErrors > 0 && text.StartsWith("Official BaseHomeEntityCreator.", StringComparison.Ordinal)) { expectedErrors--; return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    void ExpectedErrors(int count, Action action) { expectedErrors = count; action(); Require(expectedErrors == 0, "Original invalid-input diagnostics remain present"); }
    GameObject New(string name) { var value = new GameObject(name); roots.Add(value); return value; }
    Context Owner(IHomeEntityPrefabTables tables) { var value = new Context(tables); contexts.Add(value); return value; }
    Transform Slot(string name)
    {
        var value = New(name); value.SetActive(false);
        value.transform.position = new Vector3(4.1f, -2.7f, .5f);
        value.transform.rotation = Quaternion.Euler(0, 0, 17);
        value.transform.localScale = new Vector3(1.2f, .8f, 1);
        return value.transform;
    }
    GameObject Prototype(string name, Vector3 offset)
    {
        var value = New(name); value.SetActive(false);
        value.transform.localPosition = new Vector3(.2f, -.3f, 0);
        value.transform.localScale = new Vector3(.8f, 1.3f, 1);
        var data = value.AddComponent<HomeEntityCreateData>();
        var serialized = new SerializedObject(data); serialized.FindProperty("mLocalPosOffset").vector3Value = offset; serialized.ApplyModifiedPropertiesWithoutUndo();
        value.AddComponent<HomeEntityCreationMarker>(); return value;
    }
    [Serializable] public sealed class Mapping { public MonoDictionary<EFurniture, EFurniture> Value; }
    static MonoDictionary<EFurniture, EFurniture> Alias()
        => JsonUtility.FromJson<Mapping>("{\"Value\":{\"mSerializeBuffer\":[{\"Key\":10,\"Value\":7}]}}").Value;

    IEnumerator Check()
    {
        var tableAsset = Resources.Load<TextAsset>("Home/EntityCreation/catalog");
        Require(tableAsset, "Current original local creator tables are available in Resources");
        var data = JsonUtility.FromJson<HomeEntityCreationCatalog.Payload>(tableAsset.text);
        Require(data.FurnitureSkins.Length == 65 && data.ThemeSkins.Length == 19 && data.WeatherOverrides.Length == 49,
            "Complete selected furniture, theme and weather table coverage");
        var catalog = HomeEntityCreationCatalog.Load();
        Require(tableAsset, "Loading a parsed catalog does not invalidate another owner's shared TextAsset reference");
        var paths = new HomeEntityPrefabPaths(catalog);
        foreach (var row in data.FurnitureSkins)
            Require(paths.GetFurniturePrefabPath(row.SkinItemId) == "Home/Dynamic/Furniture/" + row.FurnitureType + "/" + row.PrefabName,
                "Original configured furniture address: " + row.SkinItemId);
        foreach (var row in data.ThemeSkins)
            Require(paths.GetThemePrefabPath(row.SkinItemId) == "Home/Dynamic/Theme/" + row.ThemeType + "/" + row.PrefabName,
                "Original configured theme address: " + row.SkinItemId);
        Require(paths.GetFurniturePrefabPath(int.MinValue) == "" && paths.GetThemePrefabPath(int.MinValue, "Ignored") == "", "Missing skin rows keep the original empty-address result");
        var original = data.FurnitureSkins.Single(row => row.FurnitureType == EFurniture.StorageBox && row.IsDefault);
        var native = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/StorageBox/Native/StorageBox.prefab");
        Require(native, "Actual bound original storage prefab is installed");
        var real = Owner(catalog); real.Skins[EFurniture.StorageBox] = original.SkinItemId;
        real.Prefabs[paths.GetFurniturePrefabPath(original.SkinItemId)] = native;
        var realSlot = Slot("NativeStorageSlot");
        var nativeCreator = new FurnitureEntityCreator(new Dictionary<EFurniture, Transform> { [EFurniture.StorageBox] = realSlot }, null);
        var model = nativeCreator.GetOrCreate(EFurniture.StorageBox, real);
        Require(model && model.transform.parent == realSlot && model.transform.localPosition == native.transform.localPosition && model.transform.localScale == native.transform.localScale,
            "Original creator instantiates the native storage transform without flattening the hierarchy");
        Require(model.GetComponentsInChildren<MonoBehaviour>(true).Length == 40 && model.GetComponentsInChildren<Component>(true).All(value => value),
            "Native storage keeps all migrated components under the creator-owned root");
        Require(nativeCreator.GetOrCreate(EFurniture.StorageBox, real) == model && real.Handles.Count == 1,
            "Repeated native storage queries reuse the original cached entity and resource handle");
        nativeCreator.ReleaseAll(real); yield return null;
        Require(!model && real.Handles.Single().Releases == 1, "Native storage teardown destroys the instance and releases its handle once");

        var tables = new Tables();
        tables.Furniture[700001] = new FurnitureSkinInfo { SkinItemId = 700001, FurnitureType = EFurniture.StorageBox, PrefabName = "FixtureA" };
        tables.Furniture[700002] = new FurnitureSkinInfo { SkinItemId = 700002, FurnitureType = EFurniture.StorageBox, PrefabName = "FixtureB" };
        tables.Themes[800001] = new HomeThemeSkinInfo { SkinItemId = 800001, ThemeType = HomeThemeType.House, PrefabName = "FixtureHouseA" };
        tables.Themes[800002] = new HomeThemeSkinInfo { SkinItemId = 800002, ThemeType = HomeThemeType.House, PrefabName = "FixtureHouseB" };
        var owner = Owner(tables); owner.Skins[EFurniture.StorageBox] = 700001; owner.Themes[HomeThemeType.House] = 800001;
        var a = Prototype("FixtureA", new Vector3(.13f, -.27f, 0)); var b = Prototype("FixtureB", Vector3.zero);
        var main = Slot("FixtureStorageSlot");
        string addressA = owner.PrefabPaths.GetFurniturePrefabPath(700001), addressB = owner.PrefabPaths.GetFurniturePrefabPath(700002);
        owner.Prefabs[addressA] = a; owner.Prefabs[addressB] = b;
        var creator = new FurnitureEntityCreator(new Dictionary<EFurniture, Transform> { [EFurniture.StorageBox] = main }, Alias());
        var first = creator.GetOrCreate(EFurniture.Cart, owner);
        Require(first && creator.GetOrCreate(EFurniture.StorageBox, owner) == first && owner.Handles.Count == 1,
            "Original create mapping resolves aliases into one cached furniture entity");
        Require(first.transform.localPosition == a.transform.localPosition + new Vector3(.13f, -.27f, 0) &&
            first.transform.localScale == a.transform.localScale && owner.LastCreatedPosition == first.transform.localPosition,
            "Original local position offset is applied before entity-created registration");
        owner.Trace.Clear(); creator.OnSkinChanged(EFurniture.StorageBox, 700001, owner, out bool changed);
        Require(!changed && owner.Trace.Count == 0, "Identical resolved prefab paths do not recreate or publish a change");
        HomeEntityCreationMarker.Trace = owner.Trace.Add;
        creator.OnSkinChanged(EFurniture.StorageBox, 700002, owner, out changed);
        Require(changed && owner.Trace.SequenceEqual(new[] { "will", "release:" + addressA, "load:" + addressB, "instantiate", "created", "publish:StorageBox" }),
            "Furniture replacement preserves notify, deactivate/release, load, instantiate, register and publish order");
        Require(owner.Handles[0].Releases == 1 && owner.Handles[1].Releases == 0, "New furniture retains a separate live handle");
        yield return null; Require(!first, "Replaced original instance is destroyed after the native Unity frame");
        owner.Skins[EFurniture.StorageBox] = 700002; owner.Trace.Clear(); creator.ReCreateAll(owner);
        Require(owner.Trace.Count == 0, "Recreate-all also preserves unchanged entity identity");

        tables.Weather[(700002, 91)] = new HomeSkinWeatherOverridePrefabInfo { SkinId = 700002, WeatherSkinId = 91, PrefabName = "FixtureRain" };
        tables.Weather[(700002, 92)] = new HomeSkinWeatherOverridePrefabInfo { SkinId = 700002, WeatherSkinId = 92, PrefabName = "FixturePreview" };
        string rain = owner.PrefabPaths.GetFurniturePrefabPath(700002, "FixtureRain"), preview = owner.PrefabPaths.GetFurniturePrefabPath(700002, "FixturePreview");
        owner.Prefabs[rain] = a; owner.Prefabs[preview] = b;
        owner.Weather = 91; owner.Trace.Clear(); creator.OnSkinChanged(EFurniture.StorageBox, 700002, owner, out changed);
        Require(changed && owner.Trace.Contains("load:" + rain), "Current weather selects the original configured prefab override");
        owner.Preview = 92; owner.WeatherQueries = 0; owner.Trace.Clear(); creator.OnSkinChanged(EFurniture.StorageBox, 700002, owner, out changed);
        Require(changed && owner.WeatherQueries == 0 && owner.Trace.Contains("load:" + preview), "Positive preview weather has priority and avoids reading player weather");
        owner.Preview = 0; owner.Weather = 0; owner.Trace.Clear(); creator.OnSkinChanged(EFurniture.StorageBox, 700002, owner, out changed);
        Require(changed && owner.Trace.Contains("load:" + addressB), "Absent weather override returns to the configured base prefab");

        var theme = new ThemeEntityCreator(new Dictionary<HomeThemeType, Transform> { [HomeThemeType.House] = Slot("ThemeSlot") });
        string houseA = owner.PrefabPaths.GetThemePrefabPath(800001), houseB = owner.PrefabPaths.GetThemePrefabPath(800002);
        owner.Prefabs[houseA] = a; owner.Prefabs[houseB] = b;
        theme.GetOrCreate(HomeThemeType.House, owner); owner.Trace.Clear(); theme.OnSkinChanged(HomeThemeType.House, 800002, owner, out changed);
        Require(changed && owner.Trace.SequenceEqual(new[] { "will", "release:" + houseA, "load:" + houseB, "instantiate", "created", "changed:FixtureB(Clone)" }),
            "Theme replacement invokes the new entity's original change-create notification after registration");

        var missingSlot = new FurnitureEntityCreator(new Dictionary<EFurniture, Transform>(), null);
        owner.Trace.Clear(); ExpectedErrors(2, () => Require(!missingSlot.GetOrCreate(EFurniture.StorageBox, owner), "Missing slots do not create substitute entities"));
        Require(owner.Trace.Count == 0, "Missing slots never load or instantiate a prefab");
        owner.Trace.Clear(); ExpectedErrors(1, () => creator.OnSkinChanged(EFurniture.StorageBox, int.MinValue, owner, out changed));
        Require(changed && owner.Trace.SequenceEqual(new[] { "will", "release:" + addressB, "load:", "publish:StorageBox" }),
            "Original failed replacement still returns and publishes its original create result without a fallback model");
        owner.Trace.Clear(); Require(!creator.GetOrCreate(EFurniture.StorageBox, owner) && owner.Trace.Count == 0,
            "Original failed replacement leaves the released cached entry instead of silently retrying or substituting");
        owner.AcceptNullDestroy = true;
        creator.ReleaseAll(owner); theme.ReleaseAll(owner); yield return null;
        Require(owner.Trace.Contains("will:null"), "Original release-all forwards a failed replacement's empty instance to its owner");
        Require(owner.Handles.Where(handle => handle.Prefab).All(handle => handle.Releases == 1), "All successfully instantiated creator resources release exactly once");

        var zero = new HomeEntity { Handle = new Handle(null, "empty", owner.Trace) };
        zero.Release(); Require(((Handle)zero.Handle).Releases == 0, "Original entity release guard does not invent ownership for an absent GameObject");
        Resources.UnloadAsset(tableAsset);
    }

    sealed class Tables : IHomeEntityPrefabTables
    {
        public readonly Dictionary<int, FurnitureSkinInfo> Furniture = new Dictionary<int, FurnitureSkinInfo>();
        public readonly Dictionary<int, HomeThemeSkinInfo> Themes = new Dictionary<int, HomeThemeSkinInfo>();
        public readonly Dictionary<(int, int), HomeSkinWeatherOverridePrefabInfo> Weather = new Dictionary<(int, int), HomeSkinWeatherOverridePrefabInfo>();
        public FurnitureSkinInfo GetFurnitureSkin(int id) => Furniture.TryGetValue(id, out var value) ? value : null;
        public HomeThemeSkinInfo GetThemeSkin(int id) => Themes.TryGetValue(id, out var value) ? value : null;
        public HomeSkinWeatherOverridePrefabInfo GetWeatherOverride(int skin, int weather) => Weather.TryGetValue((skin, weather), out var value) ? value : null;
    }
    sealed class Handle : IHomeEntityPrefabHandle
    {
        readonly string path; readonly List<string> trace;
        public GameObject Prefab; public int Releases;
        public GameObject Result => Prefab;
        public Handle(GameObject value, string path, List<string> trace) { Prefab = value; this.path = path; this.trace = trace; }
        public void Release() { Releases++; trace.Add("release:" + path); }
    }
    sealed class Context : IHomeEntityManagerContext, IHomeEntityResourceLoader, IHomeEntitySkinData, IHomeFurnitureChangePublisher
    {
        public readonly List<string> Trace = new List<string>();
        public readonly List<Handle> Handles = new List<Handle>();
        public readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        public readonly Dictionary<EFurniture, int> Skins = new Dictionary<EFurniture, int>();
        public readonly Dictionary<HomeThemeType, int> Themes = new Dictionary<HomeThemeType, int>();
        public int Weather, Preview, WeatherQueries; public Vector3 LastCreatedPosition; public bool AcceptNullDestroy;
        public IHomeEntityResourceLoader ResourceLoader => this;
        public IHomeFurnitureChangePublisher FurnitureChangePublisher => this;
        public IHomeEntitySkinData PlayerData => this;
        public IHomeEntityPrefabTables Tables { get; }
        public HomeEntityPrefabPaths PrefabPaths { get; }
        public int PreviewWeatherId => Preview;
        public Context(IHomeEntityPrefabTables tables) { Tables = tables; PrefabPaths = new HomeEntityPrefabPaths(tables); }
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            Require(!parent.gameObject.activeInHierarchy && !worldPositionStays, "Fixture stages original creation under an inactive slot using local coordinates");
            Trace.Add("instantiate"); return Object.Instantiate(prefab, parent, worldPositionStays);
        }
        public void OnEntityWillDestroy(GameObject instance)
        {
            if (!instance && AcceptNullDestroy) { Trace.Add("will:null"); return; }
            Require(instance, "Destruction notification receives the still-owned instance"); Trace.Add("will");
        }
        public void OnEntityCreated(GameObject instance) { LastCreatedPosition = instance.transform.localPosition; Trace.Add("created"); }
        public IHomeEntityPrefabHandle LoadPrefab(string path) { Trace.Add("load:" + path); Prefabs.TryGetValue(path, out var prefab); var handle = new Handle(prefab, path, Trace); Handles.Add(handle); return handle; }
        public int GetFurnitureSkinId(EFurniture type) => Skins[type];
        public int GetThemeSkinId(HomeThemeType type) => Themes[type];
        public int GetEnvironmentSkinId(HomeEnvironmentType type) { Require(type == HomeEnvironmentType.Weather, "Creator queries the original weather environment key"); WeatherQueries++; return Weather; }
        public void Publish(HomeFurnitureChangeData data) => Trace.Add("publish:" + data.Furniture);
        public void ReleaseFixtureHandles() { foreach (var handle in Handles) if (handle.Releases == 0) handle.Release(); }
    }
}
