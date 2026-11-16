using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Category = Zjcs.Official.HomeFunctionMenuUI.HomeMenuConfigurationRules.ActivityCategoryType;

[InitializeOnLoad]
public static class HomeActivityEntryControllerValidation
{
    private const string Active = "Zjcs.HomeActivityEntryControllerValidation.Active";
    static HomeActivityEntryControllerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("ActivityEntryControllerValidation").AddComponent<HomeActivityEntryControllerRunner>();
        };
    }
    public static void Run()
    {
        string work = System.IO.Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!System.IO.Path.GetFullPath(Application.dataPath).StartsWith(work + System.IO.Path.DirectorySeparatorChar))
            throw new InvalidOperationException("An owned validation project is required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_ACTIVITY_ENTRY_CONTROLLER_PASSED: category updates, simulated pointer and subscription lifetime.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeActivityEntryControllerRunner : MonoBehaviour
{
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
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { HomeActivityEntryControllerValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeActivityEntryControllerValidation.Finish(null);
    }
    private IEnumerator Check()
    {
        var events = new GameObject("Events", typeof(EventSystem)).GetComponent<EventSystem>();
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var root = new GameObject("OriginalActivityEntry", typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(canvas.transform, false);
        ((RectTransform)root.transform).sizeDelta = new Vector2(200, 100);
        root.AddComponent<Image>();
        var button = root.AddComponent<Button>();
        var entry = root.AddComponent<ActivityEntryBtn>();
        var services = new Services();
        entry.Services = services;
        typeof(ActivityEntryBtn).GetField("mEntryBtn", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, button);
        typeof(ActivityEntryBtn).GetField("mActivityCategoryType", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, Category.Common);
            root.SetActive(true);
            Require(!root.activeSelf && services.Subscribers == 1, "Empty activities hide but preserve updates");
            services.Categories.Add(Category.Pay);
            services.Publish();
            Require(!root.activeSelf, "A different category remains hidden");
            services.Categories.Add(Category.Common);
            services.Publish();
            Require(root.activeSelf, "Matching activity reactivates the entry");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)root.transform;
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject == root,
                "Entry is first UI raycast hit at " + pointer.position + "; hits=" + hits.Count);
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Require(services.Opened == 1, "Simulated pointer opens the original entry panel once");
            services.Categories.Clear();
            services.Publish();
            Require(!root.activeSelf && services.Subscribers == 1, "Removing matching activity hides the entry");
        Destroy(root);
        yield return null;
        Require(services.Subscribers == 0, "Destroy releases subscription");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
