using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

public sealed partial class HomeFurnitureControllerChecks
{
    IEnumerator CheckWeaponRack()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/StorageBox/WeaponRack/StorageWeaponRack.prefab");
        Require(prefab, "Original native storage rack prefab is installed");
        var owner = New("NativeRackFixture"); owner.SetActive(false);
        var instance = Instantiate(prefab, owner.transform, false);
        var rack = instance.GetComponent<HomeWeaponRack>();
        var modes = instance.GetComponent<HomeViewModeControl>();
        var actions = instance.GetComponent<HomeSwitcherActionEventHandler>();
        Require(rack && modes && actions && rack.PSlot && rack.SSlot, "Native rack has all original runtime components and slot references");
        Require(actions.EventName == "Weapon", "Native rack keeps original action event name");
        var data = new RackServices(New("PortOnlyWeaponFixtures"));
        rack.Services = data; modes.ViewModes = data; actions.ActionEvents = data;
        int primary = data.Tables.SpineSkinInfoParser.GetAll().First(value => value.SkinType == SkinType.pWeapon && value.SkinName.StartsWith("pWeapon_100", StringComparison.Ordinal)).Id;
        int secondary = data.Tables.SpineSkinInfoParser.GetAll().First(value => value.SkinType == SkinType.sWeapon && value.SkinName.StartsWith("sWeapon_11", StringComparison.Ordinal)).Id;
        data.Home.Data.SkinMap[SkinType.pWeapon] = new SkinData { Type = SkinType.pWeapon, SkinId = primary };
        data.Home.Data.SkinMap[SkinType.sWeapon] = new SkinData { Type = SkinType.sWeapon, SkinId = secondary };
        data.Home.Data.EquipSkinDict[SkinType.sWeapon.ToEquipPos()] = 0;
        owner.SetActive(true); yield return null;
        Require(data.Loaded.Count == 2 && data.Loaded.All(path => path.EndsWith("_Home", StringComparison.Ordinal)), "Rack uses original Home-specific primary and secondary prefab addresses");
        Require(rack.PSlot.childCount == 1 && rack.SSlot.childCount == 1, "Rack populates the original two slots");
        foreach (var follower in instance.GetComponentsInChildren<BaseSkeletonFollower>(true))
            Require(follower.transform.localPosition == Vector3.zero && follower.transform.localRotation == Quaternion.identity && follower.transform.localScale == Vector3.one, "Original follower transforms are reset after instantiation");
        GameObject retained = rack.PSlot.GetChild(0).gameObject;
        data.Main.Raise(); yield return null;
        Require(data.Loaded.Count == 2 && rack.PSlot.GetChild(0).gameObject == retained, "Unchanged resolved skins do not reload weapon prefabs");

        // Main-player events trigger a refresh, while the displayed home owner's
        // appearance supplies the weapons, as in the original IHomeData path.
        data.Main.Data.SkinMap[SkinType.pWeapon] = new SkinData { Type = SkinType.pWeapon, SkinId = 0 };
        data.Main.Raise(); yield return null;
        Require(rack.PSlot.GetChild(0).gameObject == retained, "Rack reads displayed home owner independently of event publisher");
        data.Home.Data.SkinMap.Remove(SkinType.pWeapon);
        data.Main.Raise(); yield return null;
        Require(rack.PSlot.GetChild(0).gameObject == retained, "Unresolved skin keeps the previous slot according to original branch");
        data.Home.Data.SkinMap[SkinType.pWeapon] = new SkinData { Type = SkinType.pWeapon, SkinId = 0 };
        data.Main.Raise(); yield return null;
        Require(rack.PSlot.childCount == 0 && data.Loaded.Count == 2, "Zero skin clears slot without loading a prefab");
        const int emptyId = 2147483000;
        data.Tables.SpineSkinInfoParser.Add(emptyId, new SpineSkinInfo { Id = emptyId, SkinType = SkinType.pWeapon, SkinName = "emptySkin", BlackIdSet = new HashSet<int>(), WhiteIdSet = new HashSet<int>() });
        data.Home.Data.SkinMap[SkinType.pWeapon].SkinId = emptyId;
        data.Main.Raise(); yield return null;
        Require(rack.PSlot.childCount == 0 && data.Loaded.Count == 2, "emptySkin clears slot without a resource request");
        data.Home.Data.SkinMap[SkinType.pWeapon].SkinId = primary;
        data.ChangeHome(); yield return null;
        Require(rack.PSlot.childCount == 1 && data.Loaded.Count == 3, "Original home-change subscription refreshes rack weapons");

        var display = instance.transform.Find("Slots");
        Require(display && display.gameObject.activeSelf, "Owner mode shows the authored weapon slots");
        data.SetMode(HomeViewMode.TakePhoto);
        Require(!display.gameObject.activeSelf && instance.transform.Find("Rack").gameObject.activeSelf, "Original photo state hides weapon slots while keeping rack frame");
        data.SetMode(HomeViewMode.Visit);
        Require(display.gameObject.activeSelf, "Original visitor state restores weapon slots");
        var switcher = actions.StateSwitcher;
        data.Publish(new ActionEventData { EventName = "Other", BoolParam = true });
        Require(!switcher.State, "Named action handler ignores unrelated events");
        data.Publish(new ActionEventData { EventName = "Weapon", BoolParam = false });
        Require(!switcher.State && !rack.PSlot.gameObject.activeSelf && rack.SSlot.gameObject.activeSelf, "Original Weapon false event hides only primary slot");
        data.Publish(new ActionEventData { EventName = "Weapon", BoolParam = true });
        Require(switcher.State && rack.PSlot.gameObject.activeSelf, "Original Weapon true event restores primary slot");

        yield return null;
        CaptureRack(instance);
        owner.SetActive(false); yield return null;
        Require(data.Main.SubscriberCount == 0 && data.ModeCallbacks.Count == 0 && data.ActionCallbacks.Count == 1, "Disable removes original appearance and view subscriptions while action subscription retains Awake lifetime");
        owner.SetActive(true); yield return null;
        Require(data.Main.SubscriberCount == 1 && data.ModeCallbacks.Count == 1 && data.Loaded.Count == 3, "Reenable restores subscriptions without duplicate weapon loads");
        Destroy(owner); yield return null;
        Require(data.Main.SubscriberCount == 0 && data.ModeCallbacks.Count == 0 && data.ActionCallbacks.Count == 0 && data.HomeCallbacks.Count == 0, "Rack destruction releases all original subscriptions");
        Require(data.Released == 3, "Rack destruction releases every acquired prefab handle");
        data.Dispose();
        Debug.Log("HOME_NATIVE_RACK_SUBTREE_PASSED: authored prefab, slots, state references and controller ports; full weapon renderer/material and home-host integration remain incomplete.");
    }

    void CaptureRack(GameObject instance)
    {
        var sprites = instance.GetComponentsInChildren<SpriteRenderer>(true);
        Require(sprites.Length == 1 && sprites[0].sprite && sprites[0].sharedMaterial && sprites[0].sharedMaterial.shader.isSupported, "Original rack sprite and converted material are renderable");
        var light = New("RackFixtureLight").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
        var render = new RenderTexture(608, 1080, 24);
        viewCamera.targetTexture = render;
        Bounds bounds = sprites[0].bounds;
        viewCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
        viewCamera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x * 1080f / 608f) * .65f;
        viewCamera.backgroundColor = new Color(.17f, .2f, .24f); viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
        var image = new Texture2D(608, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 608, 1080), 0, 0); image.Apply();
        string folder = Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR");
        File.WriteAllBytes(Path.Combine(folder, "native-rack.png"), image.EncodeToPNG());
        RenderTexture.active = previous; viewCamera.targetTexture = null; render.Release(); Destroy(render); Destroy(image);
    }

    sealed class RackAppearance : IProfessionPlayerAppearanceSource
    {
        public readonly AppearanceData Data = new AppearanceData();
        public long Id => 1;
        public Sex Sex => Sex.Male;
        public int Profession => (int)Zjcs.Official.ProfessionUI.Profession.Zhanshi;
        public object AppearanceData => Data;
        public event Action<AppearanceOperateSource> OnAppearanceUpdate;
        public int SubscriberCount => OnAppearanceUpdate?.GetInvocationList().Length ?? 0;
        public void Raise() => OnAppearanceUpdate?.Invoke(AppearanceOperateSource.Normal);
    }

    sealed class RackServices : IHomeWeaponRackServices, IHomeFurnitureViewModes, IHomeFurnitureActionEvents, IDisposable
    {
        public readonly RackAppearance Main = new RackAppearance(), Home = new RackAppearance();
        public readonly List<string> Loaded = new List<string>();
        public readonly List<Action> HomeCallbacks = new List<Action>();
        public readonly List<Action<HomeViewMode>> ModeCallbacks = new List<Action<HomeViewMode>>();
        public readonly List<Action<ActionEventData>> ActionCallbacks = new List<Action<ActionEventData>>();
        readonly GameObject fixtures;
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        readonly Func<string, GameObject> nativePrefab;
        readonly Action<GameObject> configure;
        public int Released;
        public IProfessionPlayerAppearanceSource MainPlayer => Main;
        public IProfessionPlayerAppearanceSource PlayerData => Home;
        public ProfessionAppearanceTables Tables { get; }
        public ProfessionAppearanceResolver Appearance { get; }
        public HomeViewMode HomeViewMode { get; private set; }
        public RackServices(GameObject fixtures, Func<string, GameObject> nativePrefab = null, Action<GameObject> configure = null)
        {
            this.fixtures = fixtures; fixtures.SetActive(false);
            this.nativePrefab = nativePrefab; this.configure = configure;
            Tables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
            Appearance = new ProfessionAppearanceResolver(Tables);
        }
        public IDisposable SubscribeHomeChange(Action action) { HomeCallbacks.Add(action); return new Subscription(() => HomeCallbacks.Remove(action)); }
        public void ChangeHome() { foreach (Action action in HomeCallbacks.ToArray()) action(); }
        public IDisposable Subscribe(Action<HomeViewMode> action) { ModeCallbacks.Add(action); return new Subscription(() => ModeCallbacks.Remove(action)); }
        public IDisposable Subscribe(Action<ActionEventData> action) { ActionCallbacks.Add(action); return new Subscription(() => ActionCallbacks.Remove(action)); }
        public void SetMode(HomeViewMode value) { HomeViewMode = value; foreach (var action in ModeCallbacks.ToArray()) action(value); }
        public void Publish(ActionEventData value) { foreach (var action in ActionCallbacks.ToArray()) action(value); }
        sealed class Handle : IHomeFurniturePrefabHandle
        {
            readonly RackServices owner;
            public GameObject Result { get; }
            public Handle(RackServices owner, GameObject prefab) { this.owner = owner; Result = prefab; }
            public void Dispose() => owner.Released++;
        }
        public IHomeFurniturePrefabHandle LoadPrefab(string originalLocation)
        {
            Loaded.Add(originalLocation);
            if (nativePrefab != null)
            {
                var original = nativePrefab(originalLocation);
                if (!original) throw new InvalidOperationException("Unbound original native Home weapon: " + originalLocation);
                return new Handle(this, original);
            }
            if (!prefabs.TryGetValue(originalLocation, out var prefab))
            {
                prefab = new GameObject("ControllerPortFixture"); prefab.transform.SetParent(fixtures.transform, false);
                var follower = new GameObject("Follower"); follower.transform.SetParent(prefab.transform, false);
                follower.transform.localPosition = new Vector3(1, 2, 3); follower.transform.localScale = Vector3.one * 3;
                follower.AddComponent<SkeletonRendererFollower>().InitializeOnAwake = false;
                prefabs.Add(originalLocation, prefab);
            }
            return new Handle(this, prefab);
        }
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if (configure == null) return Object.Instantiate(prefab, parent, worldPositionStays);
            var instance = Object.Instantiate(prefab, fixtures.transform, false);
            configure(instance);
            instance.transform.SetParent(parent, worldPositionStays);
            return instance;
        }
        public void Dispose() { if (fixtures) Object.Destroy(fixtures); }
    }
}
