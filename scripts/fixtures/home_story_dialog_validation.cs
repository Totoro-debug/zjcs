using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeStoryCharacter;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.HomeStoryUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeStory;
using Zjcs.Offline.HomeStoryDialog;
using Zjcs.Offline.HomeUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class HomeStoryDialogValidation
{
    private const string Active = "Zjcs.HomeStoryDialogValidation.Active";
    static HomeStoryDialogValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeStoryDialogValidation").AddComponent<HomeStoryDialogRunner>();
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
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_STORY_DIALOG_PASSED: original dialogue, voice, pointer progression and option branch.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeStoryDialogRunner : MonoBehaviour
{
    [Serializable] private sealed class Payload
    {
        public StoryDialogInfo[] dialogs;
        public StoryDialogRoleInfo[] roles;
        public Option[] options;
        public TextRow[] texts, voices;
        public int playerRoleId;
    }
    [Serializable] private sealed class Option
    {
        public string StoryName, Icon, BlackIcon, ConditionParams;
        public int Id, ConditionType;
    }
    [Serializable] private sealed class TextRow { public string key, value, path; }
    private sealed class SpriteLease : IStoryOptionSpriteLease
    {
        private readonly Action release;
        private readonly IStoryOptionSpriteLease lease;
        public Sprite Result => lease.Result;
        public SpriteLease(IStoryOptionSpriteLease lease, Action release) { this.lease = lease; this.release = release; }
        public void Dispose() { lease.Dispose(); release(); }
    }
    private sealed class Services : IStoryDialogServices, IHomeStoryDialogEnvironment
    {
        private readonly Payload data;
        private readonly HomeStorySettings settings;
        private readonly HomeStoryDialogServices runtime;
        private readonly AudioSource audio;
        public readonly List<string> VoiceEvents = new List<string>();
        public readonly List<string> Callbacks = new List<string>();
        public int Acquired, Released, PlayingVoices, ReleasedPlayables;
        public Services(Payload data, TMP_FontAsset font, GameObject host)
        {
            this.data = data; settings = HomeStorySettings.LoadDefault();
            audio = host.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
            runtime = new HomeStoryDialogServices(HomeStoryDialogAssets.LoadDefault(), HomeStoryPortraitAssets.LoadDefault(),
                HomeStoryOptionAssets.LoadDefault(), settings, font, this);
        }
        public string PlayerName => "测试角色";
        public int PlayerRoleId => data.playerRoleId;
        public bool HasStorySettings => runtime.HasStorySettings;
        public string DialogVoiceStopEventKey => runtime.DialogVoiceStopEventKey;
        public string TalkBubblePlayablePath => runtime.TalkBubblePlayablePath;
        public RegionCodeEnum Region() => RegionCodeEnum.CN;
        public bool IsVoiceOver(int roleId) => runtime.IsVoiceOver(roleId);
        public IHomeHudAssetHandle<GameObject> LoadPortrait(string path) => runtime.LoadPortrait(path);
        public TMP_FontAsset Font() => runtime.Font();
        public DateTime Now() => DateTime.Now;
        public StoryOptionInfo GetStoryOptionInfo(string storyName, int id) => runtime.GetStoryOptionInfo(storyName, id);
        public bool IsConditionMet(int type, string parameters) => runtime.IsConditionMet(type, parameters);
        bool IHomeStoryDialogEnvironment.IsConditionMet(int type, string parameters) => throw new InvalidOperationException("Unexpected option condition.");
        public bool ContainsText(string key) => runtime.ContainsText(key);
        bool IHomeStoryDialogEnvironment.ContainsText(string key) => false;
        public string Localize(string key) => runtime.Localize(key);
        string IHomeStoryDialogEnvironment.Localize(string key) => key;
        public IStoryOptionSpriteLease LoadSprite(string path)
        {
            Acquired++; return new SpriteLease(runtime.LoadSprite(path), () => Released++);
        }
        public List<StoryDialogInfo> GetDialogInfos(string name, int id) => runtime.GetDialogInfos(name, id);
        public StoryDialogRoleInfo GetDialogRole(int roleId) => runtime.GetDialogRole(roleId);
        public WorldPopupWindow LastWindowIgnoringTips() => runtime.LastWindowIgnoringTips();
        WorldPopupWindow IHomeStoryDialogEnvironment.LastWindowIgnoringTips() => null;
        public bool ActivePanelHasSceneGraphic() => runtime.ActivePanelHasSceneGraphic();
        bool IHomeStoryDialogEnvironment.ActivePanelHasSceneGraphic() => false;
        public void CloseWindow(GameObject window) => runtime.CloseWindow(window);
        void IHomeStoryDialogEnvironment.CloseWindow(GameObject window) { Callbacks.Add("close"); window.GetComponent<WorldPopupWindow>().Hide(null); }
        public void PostVoice(string eventKey, GameObject emitter) => runtime.PostVoice(eventKey, emitter);
        void IHomeStoryDialogEnvironment.PostVoice(string eventKey, AudioClip clip, GameObject emitter)
        {
            if (eventKey == settings.DialogVoiceStopEventKey) { audio.Stop(); return; }
            var row = data.voices.Single(value => value.key == eventKey);
            Require(clip && clip == Resources.Load<AudioClip>(row.path), "Original dialogue voice clip: " + eventKey);
            audio.clip = clip; audio.Play();
            if (audio.isPlaying) PlayingVoices++;
            VoiceEvents.Add(eventKey);
        }
        public MyPlayable PlayWorldNpcPlayable(int roleId, string path) => runtime.PlayWorldNpcPlayable(roleId, path);
        MyPlayable IHomeStoryDialogEnvironment.PlayWorldNpcPlayable(int roleId, string path) => null;
        public void ReleasePlayableResources() => runtime.ReleasePlayableResources();
        void IHomeStoryDialogEnvironment.ReleasePlayableResources() => ReleasedPlayables++;
    }

    private int errors, buttonSounds;
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += (message, stack, type) => {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
        var steps = Check();
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { HomeStoryDialogValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeStoryDialogValidation.Finish(errors == 0 ? null : new Exception("Unexpected dialogue runtime errors: " + errors));
    }
    private IEnumerator Check()
    {
        var payload = JsonUtility.FromJson<Payload>(File.ReadAllText(Path.Combine(
            Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "dialog-test-data.json")));
        Require(payload?.dialogs?.Length == 4 && payload.options.Length == 3, "Original story dialogue and options");
        var camera = new GameObject("DialogueValidationCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var canvasObject = new GameObject("OriginalDialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
        int backgroundClicks = 0;
        var background = new GameObject("UnderlyingHomeInput", typeof(RectTransform), typeof(Image), typeof(Button));
        background.transform.SetParent(canvas.transform, false);
        var backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one; backgroundRect.sizeDelta = Vector2.zero;
        background.GetComponent<Image>().color = new Color(.1f, .15f, .2f, 1);
        background.GetComponent<Button>().onClick.AddListener(() => backgroundClicks++);
        var host = new GameObject("OriginalDialogueScope", typeof(RectTransform), typeof(HomeStoryDialogScope), typeof(HomeHudResourceScope));
        host.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)host.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        var hud = OfflineHomeHudAssets.LoadDefault();
        var fonts = hud.CreateFontService(); fonts.Initialize();
        var service = new Services(payload, hud.defaultFont, host);
        WorldMultiLanguageText.LanguageFont = null;
        WorldMultiLanguageText.Localize = null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__PREFAB_GUID__"));
        Require(prefab, "Original complete dialogue prefab");
        var assets = HomeStoryDialogAssets.LoadDefault();
        HomeStoryGraphValidation.Check(assets.StorySource);
        Require(assets.OptionTalkDialog == prefab, "Packaged original dialogue binding");
        var window = new HomeStoryDialogWindow(canvas, assets, HomeStoryPortraitAssets.LoadDefault(),
            HomeStoryOptionAssets.LoadDefault(), HomeStorySettings.LoadDefault(), hud.defaultFont, service,
            (clip, owner) => { Require(clip, "Original click sound"); buttonSounds++; });
        window.Closed += () => service.Callbacks.Add("close");
        var nodeRunner = new HomeFurnitureStoryDialogNode(this, window);
        nodeRunner.Begin("SenSenHomeStore", HomeStoryGraphValidation.StoreOptionNode(assets.StorySource),
            index => service.Callbacks.Add("node:" + index));
        var root = window.Root;
        var dialog = root.GetComponent<OptionTalkDialogPanel>();
        Require(root && root.GetComponentsInChildren<Component>(true).All(value => value), "All original dialogue components are bound");
        var popup = root.GetComponent<WorldPopupWindow>();
        Require(dialog && popup, "Original dialogue and popup controllers");
        var invisibleMask = Object.FindObjectsOfType<WorldWindowMask>(true).Single(value => value.MaskType == 2);
        Require(invisibleMask.gameObject.activeInHierarchy && invisibleMask.AttachWindow == popup
            && invisibleMask.Canvas.sortingOrder == popup.Canvas.sortingOrder - 1,
            "Original invisible mask is attached below the dialogue");
        yield return new WaitForEndOfFrame();
        ClickAt(new Vector2(10, 10));
        Require(backgroundClicks == 0, "Original dialog and mask block the underlying home input");
        float showDeadline = Time.realtimeSinceStartup + 8;
        while (popup.State != WorldPopupWindow.EState.Show && Time.realtimeSinceStartup < showDeadline) yield return null;
        Require(popup.State == WorldPopupWindow.EState.Show, "Original dialogue show animation completes");
        var next = (Button)typeof(TalkDialog).GetField("mNextBtn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
        var slot = (OptionSlot)typeof(OptionTalkDialogPanel).GetField("OptionSlot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
        var typing = typeof(TalkDialog).GetField("mInTypeWriting", BindingFlags.NonPublic | BindingFlags.Instance);
        var ready = typeof(TalkDialog).GetField("mWaitClickForNext", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < payload.dialogs.Length; i++)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while ((service.VoiceEvents.Count <= i || !next.gameObject.activeInHierarchy) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(service.VoiceEvents.Count > i && next.gameObject.activeInHierarchy, "Original dialogue line and voice " + i);
            Require(service.VoiceEvents[i] == payload.dialogs[i].VoiceEventKey, "Original dialogue voice order");
            if (i == 0) { yield return new WaitForEndOfFrame(); SaveFrame("story-dialog-before-click.png"); }
            if ((bool)typing.GetValue(dialog)) { Click(next); yield return null; }
            deadline = Time.realtimeSinceStartup + 5;
            while (!(bool)ready.GetValue(dialog) && Time.realtimeSinceStartup < deadline) yield return null;
            Require((bool)ready.GetValue(dialog), "Original typewriter completed " + i);
            if (i == 0)
            {
                yield return new WaitForEndOfFrame();
                var content = (WorldMultiLanguageText)typeof(TalkDialog).GetField("mTalkContentTxt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog);
                var text = content.GetComponent<TMP_Text>();
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                Debug.Log("DIALOG_TEXT_DIAGNOSTIC: text=" + text.text + " rect=" + text.rectTransform.rect
                    + " corners=" + corners[0] + "/" + corners[2] + " visible=" + text.isActiveAndEnabled
                    + " cull=" + text.canvasRenderer.cull + " count=" + text.textInfo.characterCount
                    + " font=" + text.font.name + " color=" + text.color + " max=" + text.maxVisibleCharacters
                    + " alpha=" + text.canvasRenderer.GetInheritedAlpha() + " alignment=" + text.alignment
                    + " first=" + text.rectTransform.TransformPoint(text.textInfo.characterInfo[0].bottomLeft)
                    + " vertices=" + text.textInfo.meshInfo[0].vertexCount + " material=" + text.fontSharedMaterial.name
                    + " shader=" + text.fontSharedMaterial.shader.name + " parentGroups="
                    + string.Join(",", text.GetComponentsInParent<CanvasGroup>(true).Select(group => group.name + ":" + group.alpha)));
                Require(text.canvasRenderer.GetInheritedAlpha() > .95f, "Original dialogue text is visible after the show animation");
                SaveFrame("story-dialog-first.png");
            }
            Click(next);
            yield return null;
        }
        float optionDeadline = Time.realtimeSinceStartup + 5;
        while (!slot.OptionItems[1].gameObject.activeInHierarchy && Time.realtimeSinceStartup < optionDeadline) yield return null;
        Require(slot.OptionItems[0].gameObject.activeInHierarchy && slot.OptionItems[1].gameObject.activeInHierarchy,
            "Original dialogue options appear after the final line");
        Require(((OptionTalkDialogItem)slot.OptionItems[0]).TypeImage.sprite == HomeStoryOptionAssets.LoadDefault().IconArrow1
            && service.PlayingVoices == 4, "Original option icons and all voice playback");
        yield return new WaitForEndOfFrame(); SaveFrame("story-dialog-options.png");
        Click(((OptionTalkDialogItem)slot.OptionItems[1]).Btn);
        yield return null;
        yield return null;
        Require(service.Callbacks.SequenceEqual(new[] { "close", "node:1" }), "Original option selection closes and advances its story node");
        Require(!window.Root && (!invisibleMask || !invisibleMask.gameObject.activeInHierarchy) && buttonSounds > 0,
            "Original window and mask close and click sounds play");
        nodeRunner.Dispose(); window.Dispose();
        fonts.Dispose();
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Original dialogue button receives pointer at " + point + " rect " + rect.rect + ": "
            + string.Join(",", hits.Select(value => value.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static void ClickAt(Vector2 point)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0, "The original window receives the simulated pointer");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static void SaveFrame(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        Require(image && image.width == 608 && image.height == 1080, "Actual portrait Game View");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG());
        Object.Destroy(image);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
