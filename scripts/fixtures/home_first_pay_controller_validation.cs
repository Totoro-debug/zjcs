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

[InitializeOnLoad]
public static class HomeFirstPayControllerValidation
{
    private const string Active = "Zjcs.HomeFirstPayControllerValidation.Active";
    static HomeFirstPayControllerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("FirstPayControllerValidation").AddComponent<HomeFirstPayControllerRunner>();
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
        if (error == null) Debug.Log("HOME_FIRST_PAY_CONTROLLER_PASSED: official condition, reward visibility, simulated pointer and listener lifetime.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeFirstPayControllerRunner : MonoBehaviour
{
    private sealed class Services : IHomeFirstPayIconServices, IHomeFirstPayCondition
    {
        private Action condition;
        private Action<int> reward;
        public bool HasPlayer => true;
        public bool HasConditionHandler => true;
        public bool Limited = true, Met, Active, AllClaimed;
        public int Opened, Registers, Deregisters;
        public int RewardSubscribers => reward?.GetInvocationList().Length ?? 0;
        public bool IsPayFunctionLimited() => Limited;
        public bool IsMet() => Met;
        public IHomeFirstPayCondition GetPayFunctionCondition() => this;
        public void RegisterCondition(IHomeFirstPayCondition item, Action callback)
        { Require(item == this && condition == null, "Original condition registered once"); condition = callback; Registers++; }
        public void DeregisterCondition(IHomeFirstPayCondition item, Action callback)
        { Require(item == this && condition == callback, "Same condition deregistered"); condition = null; Deregisters++; }
        public event Action<int> FirstPayAwardUpdated { add => reward += value; remove => reward -= value; }
        public bool FirstPayIsActive() => Active;
        public bool FirstPayIsTakenAllAward() => AllClaimed;
        public void OpenFirstPayDialogManual() => Opened++;
        public void NotifyCondition() => condition?.Invoke();
        public void NotifyReward() => reward?.Invoke(1);
    }
    private int runtimeErrors;
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
            catch (Exception error) { HomeFirstPayControllerValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeFirstPayControllerValidation.Finish(runtimeErrors == 0 ? null
            : new InvalidOperationException("Unexpected runtime errors: " + runtimeErrors));
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++;
    }
    private IEnumerator Check()
    {
        var events = new GameObject("Events", typeof(EventSystem)).GetComponent<EventSystem>();
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var root = new GameObject("FirstPay", typeof(RectTransform), typeof(LayoutElement));
        root.SetActive(false);
        root.transform.SetParent(canvas.transform, false);
        ((RectTransform)root.transform).sizeDelta = new Vector2(200, 100);
        var display = new GameObject("Display", typeof(RectTransform), typeof(Image), typeof(Button));
        display.transform.SetParent(root.transform, false);
        ((RectTransform)display.transform).sizeDelta = new Vector2(200, 100);
        var button = display.GetComponent<Button>();
        var entry = root.AddComponent<FirstPayIconEntry>();
        var services = new Services();
        entry.Services = services;
        Set(entry, "mLayoutElement", root.GetComponent<LayoutElement>());
        Set(entry, "mDisplay", display);
        Set(entry, "mEntryBtn", button);
        root.SetActive(true);
        Require(!display.activeSelf && root.GetComponent<LayoutElement>().ignoreLayout && services.Registers == 1
            && services.RewardSubscribers == 0, "Locked pay registers condition and hides without subscribing rewards");
        services.NotifyCondition();
        Require(services.Deregisters == 0 && !display.activeSelf, "Unmet condition leaves entry locked");
        services.Met = true; services.Limited = false; services.NotifyCondition();
        Require(display.activeSelf && !root.GetComponent<LayoutElement>().ignoreLayout
            && services.Deregisters == 1 && services.RewardSubscribers == 1, "Unlock switches from condition to reward listener");
        yield return null;
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>();
        events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject == display, "First-pay button is first UI raycast hit");
        ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Require(services.Opened == 1, "Simulated pointer opens first-pay dialog once");
        services.AllClaimed = true; services.NotifyReward();
        Require(display.activeSelf, "Unactivated first pay remains visible even if all awards are marked claimed");
        services.Active = true; services.AllClaimed = true; services.NotifyReward();
        Require(!display.activeSelf && root.GetComponent<LayoutElement>().ignoreLayout, "All claimed hides first-pay entry");
        services.AllClaimed = false; services.NotifyReward();
        Require(display.activeSelf, "Remaining awards show first-pay entry");
        root.SetActive(false);
        Require(services.RewardSubscribers == 0, "Disable removes reward listener");
        root.SetActive(true);
        Require(services.RewardSubscribers == 1 && services.Registers == 1, "Unlocked enable subscribes directly to rewards");
        root.SetActive(false);
        Require(services.RewardSubscribers == 0, "Repeated disable releases reward listener");
        services.Limited = true; services.Met = false;
        root.SetActive(true);
        Require(!display.activeSelf && services.Registers == 2, "Reenable while locked registers condition again");
        root.SetActive(false);
        Require(services.Deregisters == 2 && services.RewardSubscribers == 0, "Disable removes pending condition listener");
        Destroy(root);
        yield return null;
    }
    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
