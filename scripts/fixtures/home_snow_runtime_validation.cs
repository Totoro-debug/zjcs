using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeWeather;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeSnowRuntimeValidation
{
    const string Active = "Zjcs.HomeSnowRuntimeValidation.Active";
    static HomeSnowRuntimeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            SessionState.SetBool(Active, false);
            AudioListener.volume = 0f;
            new GameObject("Home snow runtime acceptance").AddComponent<HomeSnowRuntimeRunner>();
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class HomeSnowRuntimeRunner : MonoBehaviour
{
    sealed class InertPointer : IHomeSceneClickPointer
    {
        public bool LeftDown => false;
        public bool LeftUp => false;
        public Vector2 Position => Vector2.zero;
        public int Width => Screen.width;
        public int Height => Screen.height;
    }
    HomeFeatureRuntime home;
    int checks, errors;
    static string Work => Environment.GetEnvironmentVariable("ZJCS_SNOW_RUNTIME_VALIDATION");
    static object Field(object owner, string name) => owner.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static GameObject Weather(HomeFeatureRuntime runtime)
    {
        var owner = Field(Field(runtime, "storage"), "snowEffects");
        return (GameObject)owner.GetType().GetProperty("Instance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
    void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); checks++; Debug.Log("PASS Home snow: " + message); }
    void OnLog(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Checks());
        Exception failure = null;
        while (stack.Count > 0)
        {
            object current;
            try
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                current = stack.Peek().Current;
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception error) { failure = error; break; }
            yield return current;
        }
        if (failure == null && errors > 0) failure = new InvalidOperationException("Unexpected runtime errors: " + errors);
        Application.logMessageReceived -= OnLog;
        if (home) home.Close();
        if (failure != null) { Debug.LogException(failure); EditorApplication.Exit(1); }
        else { Debug.Log("HOME_SNOW_RUNTIME_PASSED: " + checks); EditorApplication.Exit(0); }
    }
    IEnumerator Checks()
    {
        string saves = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(saves);
        string slot = creation.CreateSlot(); var draft = creation.OpenDraft(slot); draft.name = "主城雪景验证";
        Require(creation.TryCreateCharacter(slot, draft, out string error), "create isolated character: " + error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error),
            "set isolated initial weapon: " + error);
        home = HomeFeatureRuntime.BeginWithNativePointer(slot, saves, null, new InertPointer());
        home.TutorialAutoStartEnabled = false;
        for (int i = 0; i < 12; i++) yield return null;
        Require(home.Environment is IHomeEnvironmentManager, "Home exposes its original environment clock to weather controllers");
        Require(Weather(home).GetComponent<HomeWeatherSnow>() == null, "normal weather creates the original ground's empty snow projection");
        foreach (var pair in new[] { (4500, "GroundSnow"), (4501, "GroundChineseOdysseySnow"), (4502, "GroundHalloweenSnow") })
        {
            Equip(pair.Item1, 4900);
            for (int i = 0; i < 8; i++) yield return null;
            var normal = Weather(home);
            Require(normal.GetComponent<HomeWeatherSnow>() == null, pair.Item2 + " normal weather has no snow controller");
            Equip(pair.Item1, 4901);
            var snowRoot = Weather(home); var controller = snowRoot.GetComponent<HomeWeatherSnow>();
            Require(controller && snowRoot.name == pair.Item2 + "(Clone)", "official creator selects " + pair.Item2);
            Require(!normal.activeSelf && ReferenceEquals(controller.HomeEnvironmentManager, home.Environment),
                pair.Item2 + " deactivates the old root and binds the live Home clock before activation");
            var chain = new List<string>();
            for (var node = snowRoot.transform.parent; node && node != home.Scene.Root.transform; node = node.parent) chain.Add(node.name);
            Require(chain.Take(4).SequenceEqual(new[] { "GroundSlot", "Theme", "Home", "HomeRoot" }),
                pair.Item2 + " uses the extracted original scene slot ancestry");
            var clock = (IHomeEnvironmentManager)home.Environment;
            float firstProgress = Mathf.Clamp01(clock.DeltaTime / (clock.Config.Value.HomeTimeScaleRuntime * 2f));
            Require(Mathf.Abs(Shader.GetGlobalFloat("_SnowTime") - firstProgress) < .0001f,
                pair.Item2 + " replacement consumes the exact live environment delta for its appearance transition");
            var before = Weather(home); home.RefreshNativeFurnitureSkins();
            Require(Weather(home) == before, pair.Item2 + " unchanged ground path reuses the weather instance");
            var tree = home.NativeFurnitureInstance(EFurniture.FruitTree);
            var position = home.Scene.Camera.transform.position; position.x = tree.transform.position.x; position.y = tree.transform.position.y + 1.5f;
            home.Scene.Camera.transform.position = home.Scene.Clamp(position);
            Capture(pair.Item2 + "-start.png");
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Shader.GetGlobalFloat("_SnowTime") < .999f && Time.realtimeSinceStartup < deadline) yield return null;
            Require(Shader.GetGlobalFloat("_SnowTime") >= .999f, pair.Item2 + " reaches full snow with the real Home time scale");
            yield return new WaitForSecondsRealtime(1f);
            Require(snowRoot.GetComponentsInChildren<ParticleSystem>().Any(system => system.particleCount > 0),
                pair.Item2 + " emits its original particle systems in the live city");
            Capture(pair.Item2 + "-full.png");
        }
        home.SetHomeViewMode(HomeViewMode.TakePhoto);
        Equip(4500, 4900); Equip(4500, 4901);
        Require(Mathf.Abs(Shader.GetGlobalFloat("_SnowTime") - 1f) < .0001f, "photo mode preserves the official immediate full-snow state");
        home.SetHomeViewMode(HomeViewMode.Owner);
        Equip(4501, 4901);
        Require(Shader.GetGlobalFloat("_SnowTime") < .25f, "return to owner mode restarts appearance for a changed ground");
        var closing = Weather(home); home.Close(); home = null;
        yield return null; yield return null;
        Require(!closing && Object.FindObjectsOfType<HomeWeatherSnow>().Length == 0,
            "closing Home during appearance releases all native weather controllers");
        float stopped = Shader.GetGlobalFloat("_SnowTime");
        yield return new WaitForSecondsRealtime(.2f);
        Require(Mathf.Abs(stopped - Shader.GetGlobalFloat("_SnowTime")) < .0001f,
            "closed weather tasks stop advancing global snow state");
        home = HomeFeatureRuntime.BeginWithNativePointer(slot, saves, null, new InertPointer()); home.TutorialAutoStartEnabled = false;
        Require(Weather(home).name == "GroundChineseOdysseySnow(Clone)" && Shader.GetGlobalFloat("_SnowTime") == 1f,
            "saved ground/weather reopen through the initial-creation path at full snow");
        yield return null; yield return null;
        Capture("reopened-snow.png");
        Equip(4500, 4900); yield return null;
        Require(Object.FindObjectsOfType<HomeWeatherSnow>().Length == 0, "returning to normal weather removes the snow effect");
        home.Close(); home = null; yield return null;
    }
    void Equip(int ground, int weather)
    {
        Require(home.HomeProgression.EquipSkin(home.Session, 1, (int)HomeThemeType.Ground, ground, _ => 1,
            "snow-ground:" + Guid.NewGuid().ToString("N")), "equip original ground " + ground);
        Require(home.HomeProgression.EquipSkin(home.Session, 2, (int)HomeEnvironmentType.Weather, weather, _ => 1,
            "snow-weather:" + Guid.NewGuid().ToString("N")), "equip original weather " + weather);
        home.Scene.ApplyHomeSkins(home.Catalog.HomeSkins, home.Session.Skins, home.Rules);
        home.RefreshNativeFurnitureSkins();
    }
    void Capture(string name)
    {
        var camera = home.Scene.Camera; var previous = camera.targetTexture;
        var surface = new RenderTexture(600, 1068, 24); surface.Create(); camera.targetTexture = surface;
        camera.Render(); var active = RenderTexture.active; RenderTexture.active = surface;
        var image = new Texture2D(surface.width, surface.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, surface.width, surface.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Work, name), image.EncodeToPNG());
        RenderTexture.active = active; camera.targetTexture = previous; surface.Release();
        Object.Destroy(image); Object.Destroy(surface);
    }
}
