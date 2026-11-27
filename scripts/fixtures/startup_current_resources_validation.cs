using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Startup;
using Zjcs.Official.Startup;
using Object = UnityEngine.Object;

public static class StartupCurrentResourcesValidation
{
    const string Key = "Zjcs.StartupCurrent.Resources";
    static bool started;
    static double deadline;
    static string Output => Environment.GetEnvironmentVariable("ZJCS_STARTUP_VALIDATION_OUTPUT");

    public static void Run()
    {
        Zjcs.Editor.Startup.OfficialStartupTools.Restore();
        var assets = Resources.Load<OfficialStartupAssets>(OfficialStartupAssets.ResourcePath);
        Require(assets != null && assets.sourceVersion == "911_156995", "Selected startup catalog");
        foreach (var prefab in new[] {assets.bootSplash, assets.themeCanvas, assets.launchCanvas, assets.ageTipsDialog})
        {
            Require(prefab != null, "Original startup prefab");
            Require(prefab.GetComponentsInChildren<Transform>(true).All(t =>
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Missing native scripts");
        }
        SessionState.SetBool(Key, true);
        ConfigureView();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Hook();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod] static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
    static void Hook()
    {
        started = false;
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup > deadline) { Finish(new TimeoutException()); return; }
        if (!EditorApplication.isPlaying || started) return;
        started = true;
        new GameObject("Selected startup validation").AddComponent<Runner>();
    }
    static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    static void ConfigureView()
    {
        var editor = typeof(UnityEditor.Editor).Assembly;
        var sizesType = editor.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] {Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1)});
        var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] {Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), 450, 800, "Selected startup"}, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] {size});
        var index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        var viewType = editor.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType, false, null, false);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
    }

    sealed class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            Application.runInBackground = true;
            AudioListener.pause = true;
            var routine = Probe();
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception error) { Finish(error); yield break; }
                yield return next;
            }
            Finish(null);
        }
        IEnumerator Probe()
        {
            var assets = Resources.Load<OfficialStartupAssets>(OfficialStartupAssets.ResourcePath);
            new GameObject("Events", typeof(EventSystem));
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var splash = Instantiate(assets.bootSplash);
            var animation = splash.GetComponent<Animation>();
            Require(animation["BootSplashFadeIn"].length > 0 && animation["BootSplashFadeOut"].length > 0, "Original boot clips");
            animation.Play("BootSplashFadeIn");
            yield return new WaitForSecondsRealtime(animation["BootSplashFadeIn"].length);
            yield return new WaitForEndOfFrame(); Capture("boot");
            Instantiate(assets.themeCanvas);
            var launch = Instantiate(assets.launchCanvas);
            var panel = launch.GetComponent<LaunchUIRoot>().LaunchPanel;
            launch.transform.Find("Content/LaunchPanel/Content/DefaultUniverseSetter").gameObject.SetActive(false);
            launch.transform.Find("Content/LaunchPanel/Content/RightTop").gameObject.SetActive(false);
            var version = launch.transform.Find("Content/LaunchPanel/LaunchBg/Content/Version").GetComponent<TMPro.TMP_Text>();
            version.text = assets.sourceVersion.Replace('_', '.') + " · 离线版";
            panel.SetResourceUpdateState(true);
            panel.OnProgress(assets.resourceStatus, .5f);
            Require(panel.ProgressSlider.value == .5f && panel.StatusText.text == assets.resourceStatus, "Original progress bindings");
            animation.Play("BootSplashFadeOut");
            yield return new WaitForSecondsRealtime(animation["BootSplashFadeOut"].length);
            Destroy(splash);
            yield return null;
            yield return new WaitForEndOfFrame(); Capture("loading");
            GameObject age = null;
            launch.GetComponentInChildren<AgeTipGo>(true).OpenDialog = () =>
            {
                age = Instantiate(assets.ageTipsDialog, launch.transform.Find("Content/Popup"));
                age.GetComponent<Canvas>().overrideSorting = true;
                age.GetComponent<Canvas>().sortingOrder = 10001;
            };
            Click(Find("AgeTipBtn"));
            yield return null;
            Require(age != null, "Original age button route");
            panel.OnProgress(assets.initializationStatus, 1f);
            Require(panel.ProgressSlider.value == 1 && version.text.StartsWith("911.156995"), "Completed progress and selected version");
            yield return new WaitForEndOfFrame(); Capture("age");
            Click(Find("CommonBtn_12"));
            yield return null;
            Require(age == null, "Original dialog button closes its owned instance");
            yield return new WaitForEndOfFrame(); Capture("complete");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "result.json"), "{\"manifest\":\"911_156995\",\"passed\":true,\"scope\":\"native startup assets, clips, progress, age dialog raycast/down/up/click\",\"saveWrites\":false}");
            Debug.Log("STARTUP_CURRENT_RESOURCES_PASSED");
        }
        static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Button>().First(b => b.name == name && b.gameObject.activeInHierarchy).gameObject;
        static void Click(GameObject control)
        {
            Canvas.ForceUpdateCanvases();
            var rect = control.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == control, "Original UI hit: " + control.name);
            ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerClickHandler);
        }
        static void Capture(string name)
        {
            Directory.CreateDirectory(Output);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Require(texture.width > 0 && texture.height > 0, "Rendered startup frame");
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), texture.EncodeToPNG());
            Destroy(texture);
        }
    }
}
