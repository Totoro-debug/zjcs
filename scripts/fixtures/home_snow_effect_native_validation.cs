using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeWeather;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeSnowEffectNativeValidation
{
    const string Active = "Zjcs.HomeSnowEffectNativeValidation.Active";
    static HomeSnowEffectNativeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            SessionState.SetBool(Active, false);
            new GameObject("Original snow effect acceptance").AddComponent<HomeSnowEffectNativeRunner>();
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class HomeSnowEffectNativeRunner : MonoBehaviour
{
    sealed class Clock : IHomeEnvironmentManager
    {
        public HomeEnvironmentConfigValue Config { get; } = new HomeEnvironmentConfigValue(
            new HomeEnvironmentConfig { HomeTimeScaleRuntime = 1f });
        public HomeDateTime HomeDateTime => null;
        public HomeDayTime HomeDayTime => default;
        public float NextDelta;
        public float DeltaTime { get { float value = NextDelta; NextDelta = 0; return value; } }
    }
    sealed class Modes : IHomeFurnitureViewModes
    {
        public HomeViewMode HomeViewMode => HomeViewMode.Owner;
        public IDisposable Subscribe(Action<HomeViewMode> action) => throw new NotSupportedException();
    }
    static object Field(object value, string name) => value.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); Debug.Log("PASS native snow: " + message); }
    IEnumerator Start()
    {
        var checks = Checks();
        while (true)
        {
            bool next; object current;
            try { next = checks.MoveNext(); current = next ? checks.Current : null; }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); yield break; }
            if (!next) break;
            yield return current;
        }
        Debug.Log("HOME_SNOW_EFFECT_NATIVE_PASSED");
        EditorApplication.Exit(0);
    }
    IEnumerator Checks()
    {
        var camera = new GameObject("Official home camera framing").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 7.111f;
        camera.transform.position = new Vector3(.49f, -1.92f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .08f, .12f, 1f);
        camera.nearClipPlane = -100f; camera.farClipPlane = 100f;
        var surface = new RenderTexture(600, 1068, 24); surface.Create(); camera.targetTexture = surface;
        string output = Environment.GetEnvironmentVariable("ZJCS_SNOW_EFFECT_VALIDATION");
        Directory.CreateDirectory(output);
        var clock = new Clock();
        using var factory = new HomeNativePrefabFactory(root =>
        {
            var controller = root.GetComponent<HomeWeatherSnow>();
            controller.HomeEnvironmentManager = clock; controller.HomeData = new Modes();
        });
        foreach (string name in new[] { "GroundSnow", "GroundHalloweenSnow", "GroundChineseOdysseySnow" })
        {
            var prefab = Resources.Load<GameObject>("Home/Weather/SnowEffects/" + name);
            Require(prefab && prefab.name == name, name + " imports by its local effect resource address");
            var root = factory.Instantiate(prefab, null, false);
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0,
                name + " root has no missing controller");
            var controller = root.GetComponent<HomeWeatherSnow>();
            var systems = (ParticleSystem[])Field(controller, "mSnowParticleSystems");
            Require(root.transform.childCount == 1 && root.transform.GetChild(0).name == "Eff_Home_Snow"
                && systems.Length == 3 && systems[0].name == "snow (5)" && systems[1].name == "snow (4)"
                && systems[2].name == "snow (3)", name + " preserves the original effect hierarchy and ordered controller references");
            Require((float)Field(controller, "mAppearEffectDuration") == 2f
                && systems[0].main.maxParticles == 50 && systems[1].main.maxParticles == 1000
                && systems[2].main.maxParticles == 100, name + " deserializes its authored duration and particle capacities");
            var curve = (AnimationCurve)Field(controller, "mParticleAppearCurve");
            Require(Mathf.Abs(curve.Evaluate(.5f) - (name == "GroundHalloweenSnow" ? .25f : .2f)) < .0001f
                && Mathf.Abs(curve.Evaluate(1f) - 1f) < .0001f,
                name + " imports the original theme-specific particle curve and its infinity modes");
            foreach (var system in systems)
            {
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                Require(renderer && renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported
                    && renderer.sharedMaterial.mainTexture,
                    name + "/" + system.name + " resolves its original material, texture and executable shader");
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.useAutoRandomSeed = false; system.randomSeed = 171;
            }
            clock.NextDelta = 0; controller.OnChangeCreate();
            Simulate(systems);
            int zero = Capture(camera, surface, Path.Combine(output, name + "-start.png"));
            Require(Mathf.Abs(Shader.GetGlobalFloat("_SnowTime")) < .0001f
                && Array.TrueForAll(systems, system => system.particleCount == 0),
                name + " starts with zero shader progress and no snow particles");
            clock.NextDelta = 1f;
            yield return null; yield return null; yield return null;
            Simulate(systems);
            int half = Capture(camera, surface, Path.Combine(output, name + "-half.png"));
            Require(Mathf.Abs(Shader.GetGlobalFloat("_SnowTime") - .5f) < .0001f
                && systems[0].main.maxParticles == (name == "GroundHalloweenSnow" ? 12 : 10),
                name + " runs the original half-progress transition on real serialized particle systems");
            clock.NextDelta = 1f;
            yield return null; yield return null; yield return null;
            Simulate(systems);
            int full = Capture(camera, surface, Path.Combine(output, name + "-full.png"));
            Require(half > zero + 100 && full >= half && systems[0].particleCount > 0
                && systems[1].particleCount > 0 && systems[2].particleCount > 0,
                name + " renders visible original snowflakes through the bound shader at half and full progress"
                    + " pixels=" + zero + "/" + half + "/" + full);
            root.SetActive(false); Object.Destroy(root);
            yield return null;
        }
        camera.targetTexture = null; surface.Release(); Object.Destroy(surface); Object.Destroy(camera.gameObject);
    }
    static void Simulate(ParticleSystem[] systems)
    {
        foreach (var system in systems)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Simulate(3f, true, true);
        }
    }
    static int Capture(Camera camera, RenderTexture surface, string path)
    {
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = surface;
        var image = new Texture2D(surface.width, surface.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, surface.width, surface.height), 0, 0); image.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(path, image.EncodeToPNG());
        int count = 0;
        foreach (Color32 pixel in image.GetPixels32())
            if (pixel.r > 45 && pixel.g > 45 && pixel.b > 45) count++;
        Object.Destroy(image);
        return count;
    }
}
