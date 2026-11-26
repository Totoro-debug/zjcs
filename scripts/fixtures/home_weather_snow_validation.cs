using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeWeather;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeWeatherSnowValidation
{
    private const string Active = "Zjcs.HomeWeatherSnowValidation.Active";
    static HomeWeatherSnowValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            SessionState.SetBool(Active, false);
            new GameObject("Original snow lifecycle acceptance").AddComponent<HomeWeatherSnowValidationRunner>();
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
}

public sealed class HomeWeatherSnowValidationRunner : MonoBehaviour
{
    private sealed class Clock : IHomeEnvironmentManager
    {
        public readonly HomeEnvironmentConfig Value = new HomeEnvironmentConfig { HomeTimeScaleRuntime = 2f };
        public HomeEnvironmentConfigValue Config => new HomeEnvironmentConfigValue(Value);
        public HomeDateTime HomeDateTime => null;
        public HomeDayTime HomeDayTime => default;
        public float NextDelta;
        public float DeltaTime { get { float value = NextDelta; NextDelta = 0f; return value; } }
    }
    private sealed class Modes : IHomeFurnitureViewModes
    {
        public HomeViewMode Value;
        public HomeViewMode HomeViewMode => Value;
        public IDisposable Subscribe(Action<HomeViewMode> action) => throw new NotSupportedException();
    }
    private static void Set(object owner, string name, object value)
        => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); Debug.Log("PASS snow: " + message); }
    private static bool Snow(float value) => Mathf.Abs(Shader.GetGlobalFloat("_SnowTime") - value) < .00001f;
    private IEnumerator Start()
    {
        IEnumerator checks = Checks();
        while (true)
        {
            bool next;
            object current;
            try { next = checks.MoveNext(); current = next ? checks.Current : null; }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); yield break; }
            if (!next) break;
            yield return current;
        }
        Debug.Log("HOME_WEATHER_SNOW_PASSED");
        EditorApplication.Exit(0);
    }
    private IEnumerator Checks()
    {
        var root = new GameObject("Official snow controller"); root.SetActive(false);
        var first = new GameObject("Original capacity 64").AddComponent<ParticleSystem>();
        var second = new GameObject("Original capacity 17").AddComponent<ParticleSystem>();
        first.transform.SetParent(root.transform, false); second.transform.SetParent(root.transform, false);
        var firstMain = first.main; firstMain.maxParticles = 64;
        var secondMain = second.main; secondMain.maxParticles = 17;
        var clock = new Clock(); var modes = new Modes { Value = HomeViewMode.TakePhoto };
        var snow = root.AddComponent<HomeWeatherSnow>();
        snow.HomeEnvironmentManager = clock; snow.HomeData = modes;
        Set(snow, "mSnowParticleSystems", new[] { first, second });
        Set(snow, "mAppearEffectDuration", 2f);
        Set(snow, "mParticleAppearCurve", AnimationCurve.Linear(0f, 0f, 1f, .5f));
        root.SetActive(true);
        Require(Snow(1f) && first.main.maxParticles == 32 && second.main.maxParticles == 8,
            "enable sets full shader progress and evaluates the curve against each captured particle capacity");
        snow.OnChangeCreate(); yield return null; yield return null;
        Require(Snow(1f), "photo mode skips the appearance transition");
        modes.Value = HomeViewMode.Owner;
        snow.OnChangeCreate();
        Require(Snow(0f) && first.main.maxParticles == 0 && second.main.maxParticles == 0,
            "zero environment delta starts the transition at zero");
        clock.NextDelta = 1f;
        yield return null; yield return null; yield return null;
        Require(Snow(.25f) && first.main.maxParticles == 8 && second.main.maxParticles == 2,
            "duration multiplies the original appearance duration by runtime time scale before particle curve evaluation");
        clock.NextDelta = 3f;
        yield return null; yield return null; yield return null;
        Require(Snow(1f) && first.main.maxParticles == 32 && second.main.maxParticles == 8,
            "the completed transition clamps shader progress and truncates each original particle limit");
        snow.OnChangeCreate(); clock.NextDelta = .5f;
        yield return null; yield return null; yield return null;
        Require(Snow(.125f), "a subsequent transition starts from its own elapsed time");
        root.SetActive(false); clock.NextDelta = 100f;
        yield return null; yield return null; yield return null;
        Require(Snow(.125f), "disable cancels pending work without resetting the global shader value");
        clock.NextDelta = 0f; root.SetActive(true);
        Require(Snow(1f) && first.main.maxParticles == 32 && second.main.maxParticles == 8,
            "re-enable restores full progress using the capacities captured once in Awake");
        clock.NextDelta = 1f; snow.OnChangeCreate();
        Require(Snow(.25f), "new creation consumes the first environment delta immediately");
        clock.NextDelta = 2f; snow.OnChangeCreate();
        yield return null; yield return null; yield return null;
        Require(Snow(.5f), "replacing an in-flight transition cancels the previous task");
        modes.Value = HomeViewMode.TakePhoto; snow.OnChangeCreate(); clock.NextDelta = 1f;
        yield return null; yield return null; yield return null;
        Require(Snow(.75f), "photo-mode creation leaves an already running transition intact");
        root.SetActive(false); root.SetActive(true);
        Set(snow, "mAppearEffectDuration", 0f); modes.Value = HomeViewMode.Owner;
        snow.OnChangeCreate(); yield return null; yield return null;
        Require(Snow(1f), "zero duration preserves the enabled state without entering the original loop");
        Object.Destroy(root);
    }
}
