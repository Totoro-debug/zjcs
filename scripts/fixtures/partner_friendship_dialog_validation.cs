// Original native friendship prefab check; mock data/window/audio endpoints only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using Cysharp.Threading.Tasks;
using Zjcs.Official.StoryPlayables;
using PopupWindow = Zjcs.Official.World.Map13MiniMapWindows.PopupWindow;
using Prop = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

[InitializeOnLoad]
public static class PartnerFriendshipDialogValidation
{
    static PartnerFriendshipDialogValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerFriendshipDialog", false))
                new GameObject("OriginalFriendshipFixture").AddComponent<PartnerFriendshipDialogRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerFriendshipDialog", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerFriendshipDialog.Deadline", float.MaxValue))
                Finish(new TimeoutException("Original friendship prefab validation timeout"));
        };
    }
    public static void Run()
    {
        var work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Owned isolated Unity project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerFriendshipDialog", true);
        SessionState.SetFloat("PartnerFriendshipDialog.Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerFriendshipDialog", false);
        if (error == null) Debug.Log("PARTNER_FRIENDSHIP_DIALOG_PASSED: complete native dialog animation, assist condition, rank gates, travel, pooled lifecycle and simulated scroll; local production provider and populated gift icons not covered.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerFriendshipDialogRunner : MonoBehaviour
{
    [Serializable] private sealed class TextRow { public string key, text; }
    [Serializable] private sealed class TextCatalog { public string manifest; public TextRow[] texts; }
    private sealed class Context : IPartnerFriendshipDialogUiServices, Zjcs.Official.World.Map13MiniMapWindows.IMap13PopupWindowHost, IProfessionPropertyBindings, ILocalizationTextService, IProfessionSoundHost, IProfessionSoundObjectHost
    {
        public Dictionary<string, string> Texts;
        public Dictionary<Prop, long> Values;
        public int Popups, SoundPosts, Registered, Unregistered;
        public bool Limited;
        public int SubRank => 2;
        public readonly List<int> RequestedLevels = new List<int>();
        public IReadOnlyDictionary<int, PartnerFriendshipDialogLevel> GetFriendshipLevels(int id) =>
            Enumerable.Range(1, 20).ToDictionary(level => level, level => new PartnerFriendshipDialogLevel {
                SubRankLimit = level == 1 ? 0 : level == 2 ? 2 : level == 4 ? 4 : 3 });
        public int CompareRewardItems(int a, int b) => throw new InvalidOperationException("No gift preferences in this scoped native fixture");
        public ItemIcon BorrowGiftIcon(int id, Transform slot) => throw new InvalidOperationException("No gift preferences in this scoped native fixture");
        public void RecycleGiftIcon(ItemIcon icon) => throw new InvalidOperationException("No gift preferences in this scoped native fixture");
        public GAction<GameObject> OnWindowsOpening { get; set; } = new GAction<GameObject>();
        public GAction<GameObject> OnWindowOpened { get; set; } = new GAction<GameObject>();
        public GAction<GameObject> OnWindowClosing { get; set; } = new GAction<GameObject>();
        public GAction<GameObject> OnWindowClosed { get; set; } = new GAction<GameObject>();
        public void Close(GameObject window, bool instant) => window.GetComponent<PopupWindow>().Hide(instant).Forget();
        public IProfessionPropertyBindings Properties => this;
        public List<Prop> TravelNpcPropTypeList { get; } = new List<Prop> { Prop.HangUpExpAdd };
        public bool IsTravelFunctionLimited => Limited;
        public IReadOnlyDictionary<Prop, long> GetFriendshipLevelPropDiff(PartnerFriendshipNpcInfo npc, int from, int to) { RequestedLevels.Add(to); return Values; }
        public IReadOnlyDictionary<Prop, long> GetLevelProps(int id, int level) => Values;
        public LanguageType Language => LanguageType.zh_CN;
        public IProfessionImageResourceLoader Resources => throw new InvalidOperationException("Native friendship rows have no dynamic property icons");
        public ProfessionPropertyDefinition GetProperty(Prop type) => new ProfessionPropertyDefinition { EntryId = 1 };
        public string GetEntryIcon(int id) => "fixture-entry-endpoint";
        public string Text(string key, params object[] args) { var value = Texts.TryGetValue(key, out string translated) ? translated : key; return args.Length == 0 ? value : string.Format(System.Globalization.CultureInfo.InvariantCulture, value, args); }
        public string TextFormatted(string key, params object[] args) => Text(key, args);
        public object DecimalNumber(long value, int places) => value.ToString();
        public void PopupEntryDetail(IWindowOperator owner, string path, ProfessionItemText name, ProfessionItemText desc, bool controlXPos)
        { Require(owner is ProfessionPlayerPropItem && path == "fixture-entry-endpoint" && !controlXPos, "native property detail arguments"); Popups++; }
        public void PopupBless(IWindowOperator owner, int rank, string desc, bool effect, string effectKey, string seasonKey) => throw new InvalidOperationException();
        public LanguageType CurrentLanguage => Language;
        public void ReloadData() { }
        public bool ContainsKey(string key) => Texts.ContainsKey(key);
        public bool TryGetOriginalText(string key, LanguageType language, out string result) => Texts.TryGetValue(key, out result);
        public string GetText(string key, LanguageType language, params object[] args) => Text(key, args);
        public string GetTextFormated(string key, LanguageType language, params object[] args) => Text(key, args);
        public void SetReplacer(ITextReplacer replacer) { }
        public bool TryGetText(string key, LanguageType language, out string result) => Texts.TryGetValue(key, out result);
        public bool TryGetText(string key, out string text) => Texts.TryGetValue(key, out text);
        public void AddDataProvider(ILanguageTextDataProvider provider) { }
        public uint PostEvent(uint id, GameObject emitter) { Require(id != 0 && emitter != null, "original effect event identity"); SoundPosts++; return (uint)SoundPosts; }
        public bool IsInitialized => true;
        public float DefaultScalingFactor => 1;
        public int RegisterGameObject(GameObject emitter, string name) { Registered++; return 1; }
        public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up) { }
        public void InitializeEmptyEnvironment(GameObject emitter, Collider source) { Require(source == null, "original collider-free sound object"); }
        public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position) { }
        public void SetDefaultListeners(GameObject emitter) { }
        public void SetScalingFactor(GameObject emitter, float value) { }
        public void UnregisterGameObject(GameObject emitter) { Unregistered++; }
    }
    private Camera cameraUi;
    private RenderTexture target;
    private EventSystem events;
    private int errors;
    private IEnumerator Start()
    {
        var check = Check();
        while (true)
        {
            object current;
            try { if (!check.MoveNext()) break; current = check.Current; }
            catch (Exception error) { PartnerFriendshipDialogValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerFriendshipDialogValidation.Finish(errors == 0 ? null : new Exception("Unexpected native prefab errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var catalog = JsonUtility.FromJson<TextCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PartnerGift/partner-friendship-dialog-texts.json").text);
        Require(catalog.manifest == "884_156692", "selected original language");
        var context = new Context { Texts = catalog.texts.ToDictionary(row => row.key, row => row.text), Values = new Dictionary<Prop, long> { [Prop.Attack] = 10, [Prop.HangUpExpAdd] = 2 } };
        ProfessionPropertyBindings.Current = context; LanguageManager.Initialize(context); OfficialWwiseEvent.Host = context;
        ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => Object.Instantiate(prefab, parent, keepWorld);
        // The original effect shader has UniversalPipeline/Universal2D passes.
        // A fixture-only 2D pipeline exercises that shader without editing it.
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        WorldMultiLanguageText.Localize = key => context.Text(key);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PartnerGift/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset");
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf");
        Require(font != null && source != null, "original regional font and current source TTF");
        // A Player mutates its loaded dynamic atlas in memory. Clone those
        // same native assets here so Editor glyph generation cannot save them.
        font = Object.Instantiate(font);
        font.atlasTextures = font.atlasTextures.Select(texture => Object.Instantiate(texture)).ToArray();
        font.material = Object.Instantiate(font.material); font.material.mainTexture = font.atlasTexture;
        Set(font, "m_SourceFontFile", source); font.fallbackFontAssetTable.Clear(); WorldMultiLanguageText.LanguageFont = () => font;
        cameraUi = new GameObject("NativeFriendshipCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(0.15f, 0.18f, 0.2f);
        target = new RenderTexture(768, 1024, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("NativePrefabFixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        canvas.scaleFactor = 0.65f; // Fit the original 890-wide template into the fixture camera.
        events = new GameObject("SimulatedNativePointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var host = new GameObject("InactiveBindingHost", typeof(RectTransform)); host.transform.SetParent(canvas.transform, false); host.SetActive(false);
        ((RectTransform)host.transform).sizeDelta = new Vector2(1080, 1500);
        var dialogGo = Native("NpcFriendshipDialog", host.transform, Vector2.zero);
        var dialog = dialogGo.GetComponent<NpcFriendshipDialog>();
        var popup = dialogGo.GetComponent<PopupWindow>();
        // Fixture-only global mask endpoint, normally owned by the window host.
        // The dialog hierarchy and the original OnMaskClick guard stay intact.
        var maskGo = new GameObject("FixtureGlobalWindowMask", typeof(RectTransform), typeof(Image), typeof(Button));
        maskGo.transform.SetParent(canvas.transform, false); maskGo.transform.SetAsFirstSibling();
        var maskRect = (RectTransform)maskGo.transform;
        maskRect.anchorMin = Vector2.zero; maskRect.anchorMax = Vector2.one; maskRect.offsetMin = maskRect.offsetMax = Vector2.zero;
        maskGo.GetComponent<Image>().color = Color.clear;
        maskGo.GetComponent<Button>().onClick.AddListener(popup.OnMaskClick);
        dialogGo.SetActive(false); popup.WindowManager = context;
        dialog.Configure(context);
        var npc = new PartnerFriendshipDialogNpc { Properties = new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 },
            GiftPreferenceDict = new Dictionary<int, double>(), Name = "原生窗口验证伙伴", AssistFriendshipLevel = 3 };
        dialog.Init(npc);
        host.SetActive(true);
        int opening = 0, opened = 0, closing = 0, closed = 0;
        context.OnWindowsOpening += go => opening++;
        context.OnWindowOpened += go => opened++;
        context.OnWindowClosing += go => closing++;
        context.OnWindowClosed += go => closed++;
        popup.Show(false).Forget();
        yield return new WaitForSeconds(0.8f);
        Require(popup.State == PopupWindow.EState.Show && opened == 1 && opening == 1, "original show animation/event order completes");
        var rows = Get<List<NpcFriendshipPreviewPropItem>>(dialog, "mPropItems");
        var visibleLevels = Enumerable.Range(2, 19).Where(level => level != 4).ToArray();
        Require(rows.Count == visibleLevels.Length && context.RequestedLevels.SequenceEqual(visibleLevels), "level-one omission and original SubRank+1 gate");
        var originalRowIds = rows.Select(row => row.GetInstanceID()).ToArray();
        Require(rows.All(row => Get<WorldMultiLanguageText>(row, "mTravelPropText").gameObject.activeInHierarchy), "original unlocked travel column");
        Require(!Get<GameObject>(dialog, "mGiftRuleGo").activeSelf, "original empty preference rule hides");
        Require(Get<GameObject>(dialog, "mAssistTipGo").activeInHierarchy, "original friendship condition is visible");
        Require(Get<WorldMultiLanguageText>(dialog, "mAssistConditionText").Text.Contains("3"), "original assist level formats");
        Capture("friendship-dialog-open.png");
        var scroll = dialogGo.GetComponentInChildren<ScrollRect>();
        Require(scroll != null, "original dialog scroll view");
        Canvas.ForceUpdateCanvases();
        var point = RectTransformUtility.WorldToScreenPoint(cameraUi, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
        var pointer = new PointerEventData(events) { position = point, scrollDelta = new Vector2(0, -8) };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        var hit = hits.FirstOrDefault(row => ExecuteEvents.GetEventHandler<IScrollHandler>(row.gameObject) == scroll.gameObject);
        Require(hit.gameObject != null, "simulated pointer reaches original scroll handler");
        var before = scroll.content.anchoredPosition;
        ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.scrollHandler);
        yield return null;
        Require(scroll.content.anchoredPosition != before, "simulated wheel moves original content");
        Capture("friendship-dialog-scrolled.png");
        var click = new PointerEventData(events) { position = new Vector2(8, 512), button = PointerEventData.InputButton.Left };
        hits.Clear(); events.RaycastAll(click, hits);
        Require(hits.Count > 0 && hits[0].gameObject == maskGo, "simulated mouse reaches host mask outside original window");
        ExecuteEvents.Execute(maskGo, click, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(maskGo, click, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(maskGo, click, ExecuteEvents.pointerClickHandler);
        yield return new WaitForSeconds(0.8f);
        Require(popup.State == PopupWindow.EState.Hide && !dialogGo.activeSelf && closing == 1 && closed == 1, "original hide animation/events");
        Require(rows.Count == 0, "original disable returns all property rows to pool");
        context.Limited = true; context.RequestedLevels.Clear(); npc.AssistFriendshipLevel = null; npc.IsStarGodNpc = true;
        dialog.Init(npc); popup.Show(false).Forget(); yield return new WaitForSeconds(0.8f);
        Require(!Get<GameObject>(dialog, "mAssistTipGo").activeSelf, "non-friendship condition hides assist tip");
        Require(rows.Select(row => row.GetInstanceID()).SequenceEqual(originalRowIds), "original pool reuses the same rows without duplication");
        // Original preview pool-clear is empty: enabling a reused initialized
        // UIBase refreshes it, then the original Init refreshes it again.
        Require(context.RequestedLevels.SequenceEqual(visibleLevels.SelectMany(level => new[] { level, level })), "original reused-row callback order");
        Require(rows.All(row => !Get<WorldMultiLanguageText>(row, "mTravelPropText").gameObject.activeInHierarchy), "original travel gate applies to every pooled row");
        Require(!dialogGo.GetComponentsInChildren<WorldMultiLanguageText>(true).Any(t => t.gameObject.activeInHierarchy && t.Text.StartsWith("ui_")), "current native localized text bindings");
        Capture("friendship-dialog-star-god-limited.png");
        popup.Hide(true).Forget(); yield return null;
        Object.Destroy(host); yield return null;

        Require(errors == 0, "native import/render/lifecycle errors");
    }
    private GameObject Native(string name, Transform parent, Vector2 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PartnerGift/" + name + ".prefab"); Require(prefab != null, "original prefab loads: " + name);
        Require(prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "all original scripts explicitly bound: " + name);
        var result = Object.Instantiate(prefab, parent, false);
        var rect = (RectTransform)result.transform;
        // Position the extracted root in this fixture only. Preserve original
        // anchors, pivot, size and every child transform/layout component.
        rect.localPosition = new Vector3(position.x - rect.rect.center.x, position.y - rect.rect.center.y, 0);
        result.SetActive(true); return result;
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var shot = new Texture2D(768, 1024, TextureFormat.RGB24, false); shot.ReadPixels(new Rect(0, 0, 768, 1024), 0, 0); shot.Apply(); RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), shot.EncodeToPNG()); Object.Destroy(shot);
    }
    private void RequireNoRaycast(Transform excluded)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)excluded; var point = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left }; var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
        Require(!hits.Any(hit => hit.gameObject.transform == excluded || hit.gameObject.transform.IsChildOf(excluded)), "simulated pointer raycast excludes the original unused slot");
    }
    private static FieldInfo Field(object value, string name)
    { for (var t = value.GetType(); t != null; t = t.BaseType) { var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; } throw new MissingFieldException(value.GetType().Name, name); }
    private static void Set(object value, string name, object field) => Field(value, name).SetValue(value, field);
    private static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Native friendship: " + message); }
}
