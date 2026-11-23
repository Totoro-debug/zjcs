using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Offline.NativeSpine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeNativeRackOwnerValidation
{
    const string Active = "Zjcs.HomeNativeRackOwnerValidation.Active";
    static IEnumerator checks;
    static int frame = -1, errors;
    static Camera view;
    static RenderTexture rendered;
    static readonly List<GameObject> roots = new List<GameObject>();
    static readonly List<IDisposable> owners = new List<IDisposable>();
    static HomeNativeRackOwnerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false)) {
                AudioListener.volume = 0; Application.runInBackground = true;
                Application.logMessageReceived += OnLog; checks = Check();
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Native rack owner validation timed out"));
            if (checks == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (!checks.MoveNext()) { checks = null; Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)); } }
            catch (Exception failure) { checks = null; Finish(failure); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_HOME_NATIVE_RACK_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Finish(Exception failure)
    {
        SessionState.SetBool(Active, false);
        foreach (var root in roots) if (root) Object.DestroyImmediate(root);
        if (rendered) { rendered.Release(); Object.DestroyImmediate(rendered); }
        foreach (var owner in owners.AsEnumerable().Reverse()) {
            try { owner.Dispose(); } catch (Exception error) { if (failure == null) failure = error; else Debug.LogException(error); }
        }
        Application.logMessageReceived -= OnLog;
        if (failure == null) Debug.Log("HOME_NATIVE_RACK_OWNER_PASSED: shared inactive native prefab factory, parenting/activation/failure lifetime, original storage rack, local Resources weapons, native Spine/materials, original reload/view/event lifetimes and independent owners. Component composition; full home/Player gameplay remains separate.");
        else Debug.LogException(failure);
        EditorApplication.Exit(failure == null ? 0 : 1);
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static GameObject New(string name, bool active = true)
    { var value = new GameObject(name); value.SetActive(active); roots.Add(value); return value; }
    static T Keep<T>(T value) where T : IDisposable { owners.Add(value); return value; }
    static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;

    static IEnumerator Check()
    {
        var sourceRoot = New("ProbeSource", false);
        sourceRoot.transform.SetPositionAndRotation(new Vector3(3, -2, 1), Quaternion.Euler(0, 0, 23));
        sourceRoot.transform.localScale = new Vector3(2, 1.5f, 1);
        var source = new GameObject("ProbePrefab"); source.transform.SetParent(sourceRoot.transform, false);
        source.transform.localPosition = new Vector3(1, 2, 3); source.transform.localRotation = Quaternion.Euler(0, 0, 17);
        source.transform.localScale = new Vector3(.7f, 1.3f, 1); source.AddComponent<HomeNativeFactoryProbe>();
        var hidden = new GameObject("AuthoredInactiveChild"); hidden.transform.SetParent(source.transform, false); hidden.SetActive(false);
        object dependency = new object();
        var factory = Keep(new HomeNativePrefabFactory(clone => {
            var probe = clone.GetComponent<HomeNativeFactoryProbe>();
            Require(!clone.activeInHierarchy && probe.AwakeCount == 0 && probe.EnableCount == 0, "Injection precedes Awake and OnEnable");
            probe.Dependency = dependency;
        }));
        var target = New("FinalNativeParent");
        target.transform.SetPositionAndRotation(new Vector3(-4, 6, 2), Quaternion.Euler(0, 0, -31));
        target.transform.localScale = new Vector3(1.6f, .8f, 1.2f);
        foreach (bool world in new[] { false, true }) {
            var expected = Object.Instantiate(source, target.transform, world);
            var actual = factory.Instantiate(source, target.transform, world);
            var probe = actual.GetComponent<HomeNativeFactoryProbe>();
            Require(probe.AwakeCount == 1 && probe.EnableCount == 1 && ReferenceEquals(probe.AwakeDependency, dependency) &&
                ReferenceEquals(probe.EnableDependency, dependency) && probe.AwakeParent == target.transform,
                "Native lifecycle sees the final parent and installed dependency, world=" + world);
            Require(Near(actual.transform.localPosition, expected.transform.localPosition) && Near(actual.transform.localScale, expected.transform.localScale) &&
                Quaternion.Angle(actual.transform.localRotation, expected.transform.localRotation) < .001f && Near(actual.transform.position, expected.transform.position),
                "Inactive construction preserves direct Unity parenting with rotated/scaled ancestors, world=" + world);
            Require(!actual.transform.Find("AuthoredInactiveChild").gameObject.activeSelf, "Authored inactive children retain their state");
            Object.Destroy(expected);
        }
        var inactiveParent = New("DeferredNativeParent", false);
        var deferred = factory.Instantiate(source, inactiveParent.transform, false);
        Require(deferred.activeSelf && !deferred.activeInHierarchy && deferred.GetComponent<HomeNativeFactoryProbe>().AwakeCount == 0,
            "Inactive final parent retains deferred lifecycle without changing activeSelf");
        inactiveParent.SetActive(true);
        Require(deferred.GetComponent<HomeNativeFactoryProbe>().AwakeCount == 1, "Later owner activation initializes once");
        source.SetActive(false);
        var inactive = factory.Instantiate(source, target.transform, false);
        Require(!inactive.activeSelf && inactive.GetComponent<HomeNativeFactoryProbe>().AwakeCount == 0, "Authored inactive root remains inactive");
        inactive.SetActive(true); source.SetActive(true);
        var unparented = factory.Instantiate(source, null, true); roots.Add(unparented);
        Require(unparented.transform.parent == null && Near(unparented.transform.position, source.transform.position), "Null-parent construction preserves original world pose");
        GameObject rejectedClone = null;
        var failing = Keep(new HomeNativePrefabFactory(clone => { rejectedClone = clone; throw new InvalidOperationException("expected injection failure"); }));
        bool rejected = false;
        try { failing.Instantiate(source, target.transform, false); } catch (InvalidOperationException error) { rejected = error.Message == "expected injection failure"; }
        Require(rejected && rejectedClone && rejectedClone.GetComponent<HomeNativeFactoryProbe>().AwakeCount == 0, "Failed installation never activates the partial instance");
        yield return null;
        Require(!rejectedClone, "Failed installation destroys only its partial clone");
        factory.Dispose(); factory.Dispose(); yield return null;
        Require(unparented && unparented.activeInHierarchy && target && deferred, "Factory disposal preserves returned instances and their final parents");
        rejected = false; try { factory.Instantiate(source, null, false); } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "Disposed construction owner rejects new instances");
        Object.Destroy(target); Object.Destroy(inactiveParent); Object.Destroy(unparented); Object.Destroy(sourceRoot); yield return null;

        var catalog = AssetDatabase.LoadAssetAtPath<NativeSpineAppearanceCatalog>("Assets/Official/Home/WeaponParts/HomeWeaponAppearanceCatalog.asset");
        Require(catalog && catalog.Config, "Verified native appearance catalog is available");
        var config = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
        var materials = Keep(new AppearanceAssetManager(config, action => new Subscription(() => { })));
        var appearances = Keep(new NativeSpineAppearanceBindings(catalog, materials));
        var animations = new Animations();
        var events = new Events(); var main = new Player(); Player home = new Player();
        var tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
        int primary = Skin(tables, "pWeapon_100_G_007"), secondPrimary = Skin(tables, "pWeapon_100_G_136"), secondary = Skin(tables, "sWeapon_11_015");
        home.Set(primary, secondary);
        var service = Keep(new HomeNativeWeaponRackServices(() => main, () => home, new ProfessionAppearanceResolver(tables), tables,
            events.SubscribeHome, appearances, animations));
        var outer = Keep(new HomeNativePrefabFactory(clone => {
            clone.GetComponent<HomeWeaponRack>().Services = service;
            clone.GetComponent<HomeViewModeControl>().ViewModes = events;
            clone.GetComponent<HomeSwitcherActionEventHandler>().ActionEvents = events;
        }));
        var resources = Keep(new HomeNativePrefabLoader());
        using (var storage = resources.LoadPrefab("Home/Dynamic/Furniture/StorageBox/StorageBox")) {
            var prefab = storage.Result.GetComponentInChildren<HomeWeaponRack>(true).gameObject;
            var rackOwner = New("NativeRackOwner");
            var instance = outer.Instantiate(prefab, rackOwner.transform, false);
            var rack = instance.GetComponent<HomeWeaponRack>();
            PrepareView(instance);
            yield return null; yield return null;
            Require(rack.PSlot.childCount == 1 && rack.SSlot.childCount == 1 && main.Subscribers == 1 && events.Home.Count == 1,
                "Actual storage rack initializes both native weapons through production local owner");
            CheckWeapon(rack.PSlot, "pWeapon_100_G_007"); CheckWeapon(rack.SSlot, "sWeapon_11_015");
            Capture(instance, "native-rack-local-owner.png");
            var retained = rack.PSlot.GetChild(0).gameObject;
            main.Set(0, 0); main.Raise(); yield return null;
            Require(rack.PSlot.GetChild(0).gameObject == retained, "Main-player notifications keep displayed-home appearance authoritative");
            home.Set(secondPrimary, secondary); main.Raise(); yield return null; yield return null;
            Require(!retained && rack.PSlot.childCount == 1, "Native replacement removes the old weapon instance");
            CheckWeapon(rack.PSlot, "pWeapon_100_G_136");
            var display = instance.transform.Find("Slots");
            events.Mode(HomeViewMode.TakePhoto);
            Require(!display.gameObject.activeSelf && instance.transform.Find("Rack").gameObject.activeSelf, "Original photo view hides weapons and retains the rack");
            events.Mode(HomeViewMode.Visit); Require(display.gameObject.activeSelf, "Original visit view restores native weapon display");
            events.Publish("Weapon", false);
            Require(!rack.PSlot.gameObject.activeSelf && rack.SSlot.gameObject.activeSelf, "Original named action hides only the primary weapon");
            events.Publish("Other", true); Require(!rack.PSlot.gameObject.activeSelf, "Other action events do not change the weapon state");
            events.Publish("Weapon", true); Require(rack.PSlot.gameObject.activeSelf, "Original named action restores the primary weapon");
            Capture(instance, "native-rack-local-owner-replaced.png");
            rackOwner.SetActive(false); yield return null;
            Require(main.Subscribers == 0 && events.Modes.Count == 0 && events.Actions.Count == 1, "Disable follows original appearance/view versus action subscription lifetimes");
            var preserved = rack.PSlot.GetChild(0).gameObject;
            rackOwner.SetActive(true); yield return null;
            Require(main.Subscribers == 1 && events.Modes.Count == 1 && rack.PSlot.GetChild(0).gameObject == preserved, "Reenable restores subscriptions without replacing unchanged native weapons");
            home = new Player(); home.Set(0, 0); events.ChangeHome(); yield return null; yield return null;
            Require(!preserved && rack.PSlot.childCount == 0 && rack.SSlot.childCount == 0, "Displayed-home replacement reads the new data owner and clears zero skins without replacement models");
            home.Set(secondPrimary, secondary); events.ChangeHome(); yield return null; yield return null;
            CheckWeapon(rack.PSlot, "pWeapon_100_G_136"); CheckWeapon(rack.SSlot, "sWeapon_11_015");

            var otherHome = new Player(); otherHome.Set(primary, secondary);
            var otherEvents = new Events();
            var otherService = Keep(new HomeNativeWeaponRackServices(() => main, () => otherHome, new ProfessionAppearanceResolver(tables), tables,
                otherEvents.SubscribeHome, appearances, animations));
            var otherFactory = Keep(new HomeNativePrefabFactory(clone => {
                clone.GetComponent<HomeWeaponRack>().Services = otherService;
                clone.GetComponent<HomeViewModeControl>().ViewModes = otherEvents;
                clone.GetComponent<HomeSwitcherActionEventHandler>().ActionEvents = otherEvents;
            }));
            var secondOwner = New("SecondNativeRackOwner"); secondOwner.transform.position = new Vector3(12, 0, 0);
            var second = otherFactory.Instantiate(prefab, secondOwner.transform, false).GetComponent<HomeWeaponRack>();
            PrepareView(second.gameObject);
            yield return null; yield return null;
            Object.Destroy(rackOwner); yield return null; yield return null;
            Require(events.Home.Count == 0 && events.Actions.Count == 0 && events.Modes.Count == 0 && main.Subscribers == 1,
                "Controller teardown removes its own subscriptions and preserves the other native rack");
            service.Dispose(); outer.Dispose(); yield return null;
            CheckWeapon(second.PSlot, "pWeapon_100_G_007"); CheckWeapon(second.SSlot, "sWeapon_11_015");
            Require(appearances.OpenAssetHandles > 0 && appearances.OpenTextureHandles > 0, "Closing one prefab owner retains the other owner's native materials and textures");
            home = new Player(); home.Set(0, 0); events.ChangeHome();
            Object.Destroy(secondOwner); yield return null; yield return null;
            Require(main.Subscribers == 0 && otherEvents.Home.Count == 0 && appearances.OpenAssetHandles == 0, "Last native controller destruction releases original subscriptions and appearance handles");
            otherService.Dispose(); otherFactory.Dispose();
        }
        // Preview components use shared source texture proxies. Close that asset
        // scope after every renderer, not when one rack's prefab owner closes.
        foreach (var entry in catalog.Assets) {
            var loader = (YooAssetTextureLoader)entry.Asset; loader.TryUnloadUnusedTextures(true);
            foreach (var mapping in loader.placeholderMap) {
                mapping.targetTextureReference.AssetReference?.Unload(); mapping.dyeTextureReference.AssetReference?.Unload();
            }
        }
        Require(appearances.OpenTextureHandles == 0, "Complete shared asset-owner closure releases every native texture handle");
        Object.Destroy(config);
        yield return null;
    }
    static int Skin(ProfessionAppearanceTables tables, string name) => tables.SpineSkinInfoParser.GetAll().Single(row => row.SkinName == name).Id;
    static void CheckWeapon(Transform slot, string skin)
    {
        var weapon = slot.GetChild(0); var skeleton = weapon.GetComponentInChildren<SkeletonAnimation>(true);
        var bridge = weapon.GetComponentInChildren<NativeSpineAppearanceAdapter>(true);
        Require(skeleton && skeleton.valid && skeleton.Skeleton.Skin.Name == skin && bridge && bridge.Source == skeleton, "Native rack uses real SDK skeleton and exact skin: " + skin);
        float before = skeleton.AnimationState.GetCurrent(0).TrackTime;
        skeleton.Update(.16f); bridge.UpdateAnimationAndMesh();
        var mesh = skeleton.GetComponent<MeshFilter>().sharedMesh;
        Require(mesh && mesh.vertexCount > 0 && mesh.triangles.Length > 0 && skeleton.AnimationState.GetCurrent(0).TrackTime > before,
            "Native weapon produces an animated mesh: " + skin + "; mode=" + skeleton.UpdateMode + "; enabled=" + skeleton.GetComponent<MeshRenderer>().enabled +
            "; vertices=" + (mesh ? mesh.vertexCount : -1) + "; time=" + before + "->" + skeleton.AnimationState.GetCurrent(0).TrackTime);
        Require(skeleton.GetComponent<MeshRenderer>().sharedMaterials.All(value => value && value.shader.isSupported && value.mainTexture && value.mainTexture.width > 32), "Native weapon uses decoded original textures and supported materials: " + skin);
    }
    static void PrepareView(GameObject rack)
    {
        if (!view) {
            view = New("NativeRackRenderCamera").AddComponent<Camera>();
            var light = New("NativeRackGlobalLight").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
            rendered = new RenderTexture(608, 1080, 24); view.targetTexture = rendered;
        }
        var sprites = rack.GetComponentsInChildren<SpriteRenderer>().Where(value => value.sprite).ToArray();
        Bounds bounds = sprites[0].bounds; foreach (var sprite in sprites.Skip(1)) bounds.Encapsulate(sprite.bounds);
        view.orthographic = true; view.aspect = 608f / 1080; view.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / view.aspect) * 1.3f;
        view.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20); view.nearClipPlane = .01f; view.farClipPlane = 100;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.12f, .15f, .19f);
        view.Render();
    }
    static void Capture(GameObject rack, string name)
    {
        PrepareView(rack);
        var previous = RenderTexture.active;
        var pixels = new Texture2D(608, 1080, TextureFormat.RGBA32, false);
        try {
            RenderTexture.active = rendered;
            pixels.ReadPixels(new Rect(0, 0, 608, 1080), 0, 0); pixels.Apply();
            var colors = pixels.GetPixels32();
            Require(colors.Count(value => value.r > 220 && value.b > 220 && value.g < 30) < colors.Length / 1000, "Rendered native rack has no shader-error color");
            Require(colors.Count(value => value.r > 70 || value.g > 70 || value.b > 80) > 1000, "Rendered native rack contains visible original artwork");
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_HOME_NATIVE_RACK_OUTPUT"), name), pixels.EncodeToPNG());
        } finally {
            RenderTexture.active = previous; Object.Destroy(pixels);
        }
    }
    sealed class Subscription : IDisposable
    {
        Action end; public Subscription(Action end) => this.end = end;
        public void Dispose() { var action = end; end = null; action?.Invoke(); }
    }
    sealed class Player : IProfessionPlayerAppearanceSource
    {
        public readonly AppearanceData Data = new AppearanceData();
        public long Id => 1; public Sex Sex => Sex.Male;
        public int Profession => (int)Zjcs.Official.ProfessionUI.Profession.Zhanshi;
        public object AppearanceData => Data;
        public event Action<AppearanceOperateSource> OnAppearanceUpdate;
        public int Subscribers => OnAppearanceUpdate?.GetInvocationList().Length ?? 0;
        public void Raise() => OnAppearanceUpdate?.Invoke(AppearanceOperateSource.Normal);
        public void Set(int primary, int secondary) {
            Data.SkinMap[SkinType.pWeapon] = new SkinData { Type = SkinType.pWeapon, SkinId = primary };
            Data.SkinMap[SkinType.sWeapon] = new SkinData { Type = SkinType.sWeapon, SkinId = secondary };
            Data.EquipSkinDict[SkinType.sWeapon.ToEquipPos()] = 0;
        }
    }
    sealed class Events : IHomeFurnitureViewModes, IHomeFurnitureActionEvents
    {
        public readonly List<Action> Home = new List<Action>();
        public readonly List<Action<HomeViewMode>> Modes = new List<Action<HomeViewMode>>();
        public readonly List<Action<ActionEventData>> Actions = new List<Action<ActionEventData>>();
        public HomeViewMode HomeViewMode { get; private set; }
        public IDisposable SubscribeHome(Action action) { Home.Add(action); return new Subscription(() => Home.Remove(action)); }
        public IDisposable Subscribe(Action<HomeViewMode> action) { Modes.Add(action); return new Subscription(() => Modes.Remove(action)); }
        public IDisposable Subscribe(Action<ActionEventData> action) { Actions.Add(action); return new Subscription(() => Actions.Remove(action)); }
        public void ChangeHome() { foreach (var action in Home.ToArray()) action(); }
        public void Mode(HomeViewMode value) { HomeViewMode = value; foreach (var action in Modes.ToArray()) action(value); }
        public void Publish(string name, bool state) { foreach (var action in Actions.ToArray()) action(new ActionEventData { EventName = name, BoolParam = state }); }
    }
    sealed class Animations : IHomeWeaponAnimationBindings
    {
        public Spine.Animation GetOrLoadSpineAnimation(ISkeletonComponent component, string name) => component.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(name);
        public void UpdateAnimation(SkeletonRenderer renderer) => renderer.GetComponent<NativeSpineAppearanceAdapter>().UpdateAnimationAndMesh();
        public void UpdateAnimation(SkeletonGraphic graphic) { graphic.ApplyAnimation(); graphic.UpdateMesh(); }
    }
}
