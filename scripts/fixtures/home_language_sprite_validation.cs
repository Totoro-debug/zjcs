using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.WorldUI;

[InitializeOnLoad]
public static class LanguageSpriteProbe
{
    private const string Key = "LanguageSpriteProbe.Active";
    static LanguageSpriteProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            new GameObject("LanguageSpriteProbe").AddComponent<LanguageSpriteRunner>();
    }
    public static void Finish(Exception error)
    {
        SessionState.EraseBool(Key);
        if (error != null) Debug.LogException(error);
        else Debug.Log("LANGUAGE_SPRITE_LIFECYCLE_PASS");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class LanguageSpriteRunner : MonoBehaviour
{
    private sealed class Handle : IWorldLanguageSpriteHandle
    {
        private readonly List<string> calls;
        private readonly string path;
        public Sprite Result { get; }
        public Handle(List<string> calls, string path, Sprite sprite) { this.calls = calls; this.path = path; Result = sprite; }
        public void Release(bool unload) => calls.Add("release:" + path + ":" + unload);
        public void Dispose() => calls.Add("dispose:" + path);
    }
    private sealed class Services : IWorldLanguageSpriteServices
    {
        public string CurrentLanguage => "zh_CN";
        public readonly List<string> Calls = new List<string>();
        public Sprite First, Second;
        public void Register(WorldMultiLanguageSpriteRenderer renderer) => Calls.Add("register");
        public void Unregister(WorldMultiLanguageSpriteRenderer renderer) => Calls.Add("unregister");
        public IWorldLanguageSpriteHandle LoadSprite(string path)
        {
            Calls.Add("load:" + path);
            return new Handle(Calls, path, path.Contains("zh_CN") ? First : Second);
        }
    }
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        var texture = new Texture2D(2, 2);
        var first = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
        var second = Sprite.Create(texture, new Rect(1, 0, 1, 1), Vector2.zero);
        var services = new Services { First = first, Second = second };
        var root = new GameObject("SpriteFixture"); root.SetActive(false);
        var component = root.AddComponent<WorldMultiLanguageSpriteRenderer>();
        typeof(WorldMultiLanguageSpriteRenderer).GetField("mKey", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(component, "LogoIP/LogoIP_1.png");
        component.Services = services;
        root.SetActive(true);
        yield return null;
        if (component.SpriteRenderer.sprite != first || services.Calls.Count != 2 ||
            services.Calls[0] != "register" || services.Calls[1] != "load:Language/zh_CN/LogoIP/LogoIP_1.png")
        { LanguageSpriteProbe.Finish(new Exception("Initial language registration and load")); yield break; }
        component.Refresh("en_US");
        if (component.SpriteRenderer.sprite != second || services.Calls.Count != 4 ||
            services.Calls[2] != "release:Language/zh_CN/LogoIP/LogoIP_1.png:True" ||
            services.Calls[3] != "load:Language/en_US/LogoIP/LogoIP_1.png")
        { LanguageSpriteProbe.Finish(new Exception("Language switch and previous asset release")); yield break; }
        Destroy(root);
        yield return null;
        if (services.Calls.Count != 6 || services.Calls[4] != "dispose:Language/en_US/LogoIP/LogoIP_1.png" ||
            services.Calls[5] != "unregister")
        { LanguageSpriteProbe.Finish(new Exception("Destroy must dispose before unregister")); yield break; }
        Destroy(first); Destroy(second); Destroy(texture);
        LanguageSpriteProbe.Finish(null);
    }
}
