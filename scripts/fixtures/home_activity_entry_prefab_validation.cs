using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeHudUI;
using Category = Zjcs.Official.HomeFunctionMenuUI.HomeMenuConfigurationRules.ActivityCategoryType;

[InitializeOnLoad]
public static class HomeActivityEntryPrefabValidation
{
    private const string Active = "Zjcs.HomeActivityEntryPrefabValidation.Active";
    static HomeActivityEntryPrefabValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeActivityEntryPrefabValidation").AddComponent<HomeActivityEntryPrefabRunner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("An owned validation project is required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigureNativeFrame();
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
    private static void ConfigureNativeFrame()
    {
        if (Application.isBatchMode) throw new InvalidOperationException("A graphical editor frame is required.");
        var editor = typeof(EditorWindow).Assembly;
        var sizesType = editor.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
            new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
        var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), 608, 1080, "Home activity fixture" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        var viewType = editor.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType, false, "Game", false);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        view.Repaint();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_ACTIVITY_ENTRY_PREFAB_PASSED: packaged original hierarchy, serialized controller, category updates and simulated pointer.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeActivityEntryPrefabRunner : MonoBehaviour
{
    private int runtimeErrors;
    private sealed class Services : IHomeActivityEntryButtonServices
    {
        private Action changed;
        public event Action ActivitiesUpdated { add => changed += value; remove => changed -= value; }
        public readonly List<Category> Categories = new List<Category>();
        public IEnumerable<Category> ActivityCategories => Categories;
        public int Subscribers => changed?.GetInvocationList().Length ?? 0;
        public int Opened;
        public void OpenActivityEntryPanel() => Opened++;
        public void Publish() => changed?.Invoke();
    }
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { HomeActivityEntryPrefabValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeActivityEntryPrefabValidation.Finish(runtimeErrors == 0 ? null
            : new InvalidOperationException("Unexpected runtime errors: " + runtimeErrors));
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++;
    }
    private IEnumerator Check()
    {
        var events = new GameObject("Events", typeof(EventSystem)).GetComponent<EventSystem>();
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        // Selected 884 HomeUI Right/EventEntry parent geometry and layout.
        var right = new GameObject("Right", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        right.transform.SetParent(canvas.transform, false);
        var rightRect = (RectTransform)right.transform;
        rightRect.anchorMin = rightRect.anchorMax = Vector2.one;
        rightRect.pivot = new Vector2(1, 1.0000001f);
        rightRect.anchoredPosition = new Vector2(-9, -153);
        rightRect.sizeDelta = new Vector2(176, 0);
        var rightLayout = right.GetComponent<VerticalLayoutGroup>();
        rightLayout.childAlignment = TextAnchor.UpperLeft;
        rightLayout.spacing = 20;
        rightLayout.childControlWidth = rightLayout.childControlHeight = true;
        rightLayout.childForceExpandWidth = rightLayout.childForceExpandHeight = false;
        right.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var slot = new GameObject("EventEntry", typeof(RectTransform), typeof(VerticalLayoutGroup));
        slot.transform.SetParent(right.transform, false);
        var slotRect = (RectTransform)slot.transform;
        slotRect.anchorMin = slotRect.anchorMax = Vector2.zero;
        slotRect.pivot = new Vector2(.5f, 1);
        slotRect.sizeDelta = Vector2.zero;
        var slotLayout = slot.GetComponent<VerticalLayoutGroup>();
        slotLayout.childAlignment = TextAnchor.UpperCenter;
        slotLayout.spacing = -20;
        slotLayout.childControlWidth = slotLayout.childControlHeight = true;
        slotLayout.childForceExpandWidth = true;
        slotLayout.childForceExpandHeight = false;
        var staging = new GameObject("InactiveBindings", typeof(RectTransform));
        staging.SetActive(false);
        staging.transform.SetParent(canvas.transform, false);
        var assets = OfflineHomeActivityEntryAssets.LoadDefault();
        var root = Instantiate(assets.EntryPrefab, staging.transform, false);
        Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length > 10, "Original component hierarchy loaded");
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            Require(component, "Original component script is resolved");
        var entry = root.GetComponent<ActivityEntryBtn>();
        var services = new Services();
        entry.Services = services;
        var texts = JsonUtility.FromJson<TextPayload>(assets.Texts.text);
        var localized = new Dictionary<string, string>();
        foreach (var row in texts.texts) localized.Add(row.key, row.text);
        foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
            text.Localizer = key => localized.TryGetValue(key, out var value) ? value : key;
        int soundCount = 0;
        var audio = gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true))
            sound.Post = (clip, emitter) => {
                Require(clip, "Original button sound is bound");
                audio.clip = clip;
                audio.Play();
                Require(audio.isPlaying, "Original button sound starts while muted");
                soundCount++;
            };
        root.SetActive(false);
        root.transform.SetParent(slot.transform, false);
        Destroy(staging);
        root.SetActive(true);
        Require(!root.activeSelf && services.Subscribers == 1, "Empty actual records hide original prefab");
        var category = (Category)typeof(ActivityEntryBtn).GetField("mActivityCategoryType", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry);
        services.Categories.Add(category);
        services.Publish();
        Require(root.activeSelf, "Matching actual category reactivates original prefab");
        yield return null;
        Canvas.ForceUpdateCanvases();
        var button = (Button)typeof(ActivityEntryBtn).GetField("mEntryBtn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry);
        Require(button && button.isActiveAndEnabled, "Original serialized button is active");
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>();
        events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Original button is the first UI hit at " + pointer.position);
        yield return new WaitForEndOfFrame();
        var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "activity-entry-prefab.png"), screenshot.EncodeToPNG());
        Destroy(screenshot);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Require(services.Opened == 1 && soundCount == 1, "Original pointer dispatches panel and bound sound once");
        services.Categories.Clear();
        services.Publish();
        Require(!root.activeSelf, "Removing record hides original prefab");
        Destroy(root);
        yield return null;
        Require(services.Subscribers == 0, "Destroy releases original activity subscription");
    }
    [Serializable] private sealed class TextPayload { public TextRow[] texts; }
    [Serializable] private sealed class TextRow { public string key, text; }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
