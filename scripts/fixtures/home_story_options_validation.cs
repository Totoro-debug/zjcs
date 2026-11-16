using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeStoryUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class HomeStoryOptionsValidation
{
    private const string Active = "Zjcs.HomeStoryOptionsValidation.Active";
    static HomeStoryOptionsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeStoryOptionsValidation").AddComponent<HomeStoryOptionsRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup >
                SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original story options validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigureNativeFrame();
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_STORY_OPTIONS_PASSED: original prefab, local options, pointer clicks, sounds and release.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeStoryOptionsRunner : MonoBehaviour
{
    [Serializable] private sealed class Payload { public Row[] options; }
    [Serializable] private sealed class Row
    {
        public string storyName, icon, blackIcon, conditionParams, text;
        public int id, conditionType;
    }
    private sealed class Lease : IStoryOptionSpriteLease
    {
        private readonly Action onDispose;
        public Sprite Result { get; }
        public Lease(Sprite sprite, Action onDispose) { Result = sprite; this.onDispose = onDispose; }
        public void Dispose() => onDispose();
    }
    private sealed class Services : IStoryOptionServices
    {
        public Row[] Options;
        public Sprite Icon;
        public TMP_FontAsset OriginalFont;
        public int Acquired, Released;
        public StoryOptionInfo GetStoryOptionInfo(string storyName, int id)
        {
            Row row = Options.Single(value => value.storyName == storyName && value.id == id);
            return new StoryOptionInfo { StoryName = row.storyName, Id = row.id, Icon = row.icon,
                BlackIcon = row.blackIcon, ConditionType = row.conditionType, ConditionParams = row.conditionParams };
        }
        public bool IsConditionMet(int type, string parameters) => throw new InvalidOperationException("Unexpected condition");
        public bool ContainsText(string key) => Options.Any(value => key == StoryOptionLanguage.GetStoryOptionTextKey(value.storyName, value.id));
        public DateTime Now() => DateTime.Now;
        public TMP_FontAsset Font() => OriginalFont;
        public string Localize(string key)
        {
            Row row = Options.FirstOrDefault(value => key == StoryOptionLanguage.GetStoryOptionTextKey(value.storyName, value.id));
            return row == null ? key : row.text;
        }
        public IStoryOptionSpriteLease LoadSprite(string path)
        {
            if (path != "UI/Dialogue/Iconarrow_1") throw new InvalidOperationException("Unexpected original icon: " + path);
            Acquired++;
            return new Lease(Icon, () => Released++);
        }
    }

    private int errors, clicked = -1, sounds;
    private bool expectCapacityError;
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += (message, stack, type) => {
            if (expectCapacityError && type == LogType.Error && message.Contains("OptionSlot: original error branch.")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
        var steps = Check();
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { HomeStoryOptionsValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeStoryOptionsValidation.Finish(errors == 0 ? null : new Exception("Unexpected runtime errors: " + errors));
    }

    private IEnumerator Check()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__PREFAB_GUID__"));
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("__ICON_GUID__"));
        var bindings = HomeStoryOptionAssets.LoadDefault();
        Require(bindings.OptionSlots == prefab && bindings.IconArrow1 == icon, "Packaged original option bindings");
        var payload = JsonUtility.FromJson<Payload>(File.ReadAllText(Path.Combine(
            Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "option-test-data.json")));
        Require(prefab && icon && payload?.options?.Length == 3, "Original option subtree, icon and data");
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasObject = new GameObject("OriginalDialogueCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        var parent = new GameObject("OptionTalkDialog", typeof(RectTransform));
        parent.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)parent.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        var root = Object.Instantiate(prefab, parent.transform, false);
        root.SetActive(true);
        Require(root.GetComponentsInChildren<Component>(true).All(value => value != null), "All original components are bound");
        var slot = root.GetComponent<OptionSlot>();
        Require(slot && slot.OptionItems.Count == 5, "Original five option items");
        var fontAssets = OfflineHomeHudAssets.LoadDefault();
        Require(fontAssets && fontAssets.defaultFont, "Original configured language font");
        var fonts = fontAssets.CreateFontService();
        fonts.Initialize();
        var service = new Services { Options = payload.options, Icon = icon, OriginalFont = fontAssets.defaultFont };
        foreach (var item in slot.OptionItems.Cast<OptionTalkDialogItem>()) item.Services = service;
        foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true))
            sound.Post = (clip, owner) => { Require(clip, "Original click sound"); sounds++; };
        slot.Reset();
        Require(slot.OptionItems.All(value => !value.gameObject.activeSelf), "Original reset hides five items");
        var options = new List<DialogOptionDataBase> {
            new DialogOptionData(service, "SenSenHomeStore", 910),
            new DialogOptionData(service, "SenSenHomeStore", 911)
        };
        slot.ShowOptions(options, value => clicked = value);
        yield return null;
        Require(slot.OptionItems.Take(2).All(value => value.gameObject.activeSelf)
            && slot.OptionItems.Skip(2).All(value => !value.gameObject.activeSelf), "Original two-option state");
        Require(service.Acquired == 2 && service.Released == 0, "Original icon leases acquired");
        foreach (var item in slot.OptionItems.Take(2).Cast<OptionTalkDialogItem>())
            Require(item.TypeImage.sprite == icon && item.TypeImage.color == item.mDarkColor,
                "Original option icon and dark color");
        foreach (var row in payload.options.Take(2))
            Require(service.OriginalFont.HasCharacters(row.text, out uint[] missing, true, true),
                "Original font provides localized option glyphs: " + row.text);
        yield return new WaitForSeconds(.25f);
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        Require(image && image.width == 608 && image.height == 1080, "Actual portrait Game View");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "story-options.png"), image.EncodeToPNG());
        Object.Destroy(image);
        Click(slot.OptionItems[1].GetComponent<OptionTalkDialogItem>().Btn);
        Require(clicked == 1 && sounds == 1, "Pointer chooses second option and plays original click event");
        slot.ShowOptions(new List<DialogOptionDataBase> { new DialogOptionData(service, "SenSenHomeStore", 912) },
            value => clicked = value);
        yield return null;
        Require(slot.OptionItems[0].gameObject.activeSelf && slot.OptionItems.Skip(1).All(value => !value.gameObject.activeSelf),
            "Original one-option state");
        Require(service.Acquired == 3 && service.Released == 2, "Original hidden options release sprite leases");
        clicked = -1;
        Click(slot.OptionItems[0].GetComponent<OptionTalkDialogItem>().Btn);
        Require(clicked == 0 && sounds == 2, "Pointer chooses first option after refresh");
        expectCapacityError = true;
        slot.ShowOptions(Enumerable.Repeat(options[0], 6).ToList(), value => clicked = value);
        expectCapacityError = false;
        Require(slot.OptionItems[0].gameObject.activeSelf && slot.OptionItems.Skip(1).All(value => !value.gameObject.activeSelf),
            "Original over-capacity branch leaves option items unchanged");
        slot.Reset();
        Require(service.Released == 3 && slot.OptionItems.All(value => !value.gameObject.activeSelf),
            "Original reset releases remaining icon and hides options");
        fonts.Dispose();
    }

    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Option button is the top UI hit: " + string.Join(",", hits.Select(value => value.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
