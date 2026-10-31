// Controller-only test fixture. These controls are not shipped game UI.
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
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using Prop = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

[InitializeOnLoad]
public static class PartnerGiftUiValidation
{
    static PartnerGiftUiValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerGiftUi", false))
                new GameObject("GiftControllerFixture").AddComponent<PartnerGiftUiRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerGiftUi", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerGiftUi.Deadline", float.MaxValue))
                Finish(new TimeoutException("Gift controller validation timeout"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerGiftUi", true);
        SessionState.SetFloat("PartnerGiftUi.Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerGiftUi", false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_GIFT_UI_PASSED: actual controllers, simulated pointer, lifecycle and property branches; synthetic fixture only.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerGiftUiRunner : MonoBehaviour
{
    private sealed class Bindings : IProfessionPropertyBindings
    {
        public LanguageType Language => LanguageType.zh_CN;
        public IProfessionImageResourceLoader Resources => null;
        public ProfessionPropertyDefinition GetProperty(Prop type) => new ProfessionPropertyDefinition();
        public string GetEntryIcon(int id) => throw new InvalidOperationException("No image loader in controller fixture");
        public string Text(string key, params object[] args) => Format(key, args);
        public string TextFormatted(string key, params object[] args) => Format(key, args);
        public object DecimalNumber(long value, int places) => value.ToString();
        public void PopupEntryDetail(IWindowOperator owner, string path, ProfessionItemText name, ProfessionItemText desc, bool controlXPos) => throw new InvalidOperationException();
        public void PopupBless(IWindowOperator owner, int rank, string desc, bool effect, string effectKey, string seasonKey) => throw new InvalidOperationException();
    }
    private sealed class Texts : ILocalizationTextService
    {
        public LanguageType CurrentLanguage => LanguageType.zh_CN;
        public void ReloadData() { }
        public bool ContainsKey(string key) => true;
        public bool TryGetOriginalText(string key, LanguageType language, out string result) { result = Format(key); return true; }
        public string GetText(string key, LanguageType language, params object[] args) => Format(key, args);
        public string GetTextFormated(string key, LanguageType language, params object[] args) => Format(key, args);
        public void SetReplacer(ITextReplacer replacer) { }
        public bool TryGetText(string key, LanguageType language, out string result) => TryGetOriginalText(key, language, out result);
        public bool TryGetText(string key, out string text) => TryGetOriginalText(key, CurrentLanguage, out text);
        public void AddDataProvider(ILanguageTextDataProvider provider) { }
    }
    private sealed class Services : IPartnerFriendshipUiServices
    {
        public IProfessionPropertyBindings Properties { get; } = new Bindings();
        public List<Prop> TravelNpcPropTypeList { get; } = new List<Prop> { Prop.HangUpExpAdd };
        public bool Limited;
        public int GateReads, BaselineReads, DiffReads;
        public Dictionary<Prop, long> Values = new Dictionary<Prop, long>();
        public bool IsTravelFunctionLimited { get { GateReads++; return Limited; } }
        public IReadOnlyDictionary<Prop, long> GetFriendshipLevelPropDiff(PartnerFriendshipNpcInfo npc, int from, int to)
        { Require(npc.Id == 8401 && from == 1 && to == 2, "difference request retains original levels"); DiffReads++; return Values; }
        public IReadOnlyDictionary<Prop, long> GetLevelProps(int id, int level)
        { Require(id == 77 && level == 1, "baseline request uses original property table id"); BaselineReads++; return Values; }
    }
    private Transform canvas;
    private Camera cameraUi;
    private RenderTexture target;
    private EventSystem events;
    private TMP_FontAsset font;
    private int errors, expectedErrors;
    private static string Format(string key, params object[] args)
    {
        if (key == "ui_common_prop_plus") key = "+{0}";
        return args.Length == 0 ? key : string.Format(System.Globalization.CultureInfo.InvariantCulture, key.Contains("{") ? key : key + " {0}", args);
    }
    private IEnumerator Start()
    {
        var check = Check();
        while (true)
        {
            object current;
            try { if (!check.MoveNext()) break; current = check.Current; }
            catch (Exception error) { PartnerGiftUiValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerGiftUiValidation.Finish(errors == 0 && expectedErrors == 0 ? null : new Exception("Unexpected errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (expectedErrors > 0 && message.StartsWith("level_prop_npc_friendship.csv: at most two")) expectedErrors--;
            else errors++;
        };
        var services = new Services(); ProfessionPropertyBindings.Current = services.Properties;
        LanguageManager.Initialize(new Texts());
        font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        WorldMultiLanguageText.Localize = key => key == "ui_common_prop_plus" ? "+{0}" : key;
        WorldMultiLanguageText.LanguageFont = () => font;
        cameraUi = new GameObject("ControllerFixtureCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = Color.black;
        target = new RenderTexture(768, 1024, 24); cameraUi.targetTexture = target;
        var c = new GameObject("SyntheticFixtureOnly", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cameraUi; c.planeDistance = 1; canvas = c.transform;
        events = new GameObject("SimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var go = Node("GiftController", canvas, new Vector2(-140, 300));
        go.AddComponent<Button>(); go.AddComponent<UIClickSound>();
        var gift = go.AddComponent<ItemIconNpcGift>();
        var proxy = new ImageProxy(); Set(proxy, "mImageLoaderProxy", new ImageLoaderProxy()); Set(gift, "mIconProxy", proxy);
        var preference = Node("Preference", go.transform); var selected = Node("Selection", go.transform);
        Set(gift, "mPreferenceGo", preference); Set(gift, "mSelectedGo", selected);
        var attachment = go.AddComponent<ItemIconCustomAttachment>();
        var reduce = Node("Reduce", canvas, new Vector2(140, 300));
        var image = reduce.AddComponent<Image>(); image.color = Color.cyan;
        var button = reduce.AddComponent<Button>(); button.targetGraphic = image;
        var cell = reduce.AddComponent<ItemIconAttachmentCell>();
        Set(attachment, "AttachmentConfig", new List<ItemIconCustomAttachment.ItemIconAttachmentConfig> {
            new ItemIconCustomAttachment.ItemIconAttachmentConfig { ItemIconAttachmentType = ItemIconAttachmentType.ReduceTip, AttachmentCell = cell } });
        Set(gift, "ItemIconCustomAttachment", attachment);
        int calls = 0; gift.SetReduceBtnClickListener(icon => { Require(icon == gift, "reduce callback keeps original icon identity"); calls++; });
        gift.SetPreferenceGoActive(true); gift.SetSelectedGoActive(true);
        for (int i = 0; i < 5; i++) gift.SetAttachment(ItemIconAttachmentType.ReduceTip, true);
        yield return null;
        Click(reduce); Require(calls == 1, "repeated attachment bind has one reduction listener");
        gift.Clear(); Require(!preference.activeSelf && selected.activeSelf && !reduce.activeSelf, "original Clear preference/selection/attachment states");
        reduce.SetActive(true); Click(reduce); Require(calls == 1, "Clear detaches listener from recycled reduce button");
        gift.SetAttachment(ItemIconAttachmentType.ReduceTip, false);
        Require(!reduce.activeSelf, "hidden attachment keeps hidden state");
        gift.SetAttachment(ItemIconAttachmentType.ReduceTip, true); Click(reduce);
        Require(calls == 2, "reuse reattaches once and retains original callback");
        gift.SetReduceBtnClickListener(null); Click(reduce); Require(calls == 2, "null reduce callback is safe");
        var row = Row("PropertyRow", new Vector2(0, 100)); row.Configure(services);
        row.Init(Prop.Attack, 125, true, 25);
        Require(Get<WorldMultiLanguageText>(Get<ProfessionPlayerPropItem>(row, "mPropItem"), "myPropValueText").Text.Contains("125"), "ordinary value reaches shared PlayerPropItem text");
        Require(Get<WorldMultiLanguageText>(row, "mTxtPropValueAdd").Text == "+25", "positive addition uses shared original formatter");
        Require(!Get<WorldStateSwitcher<bool>>(row, "mTravelStateSwitcher").State, "ordinary property branch");
        row.Init(Prop.Defence, 20, false, 0);
        Require(!Get<WorldMultiLanguageText>(row, "mTxtPropValueAdd").gameObject.activeSelf, "zero addition marker hidden");
        row.Init(Prop.Defence, 20, true, -4);
        Require(!Get<WorldMultiLanguageText>(row, "mTxtPropValueAdd").gameObject.activeSelf, "negative addition marker hidden");
        row.Init(Prop.HangUpExpAdd, 6, true, 0);
        Require(Get<WorldStateSwitcher<bool>>(row, "mTravelStateSwitcher").State, "travel property branch");
        Require(Get<WorldMultiLanguageText>(row, "mTravelPropText").Text.EndsWith("+6"), "travel uses original CultureTextArg plus-value composition");
        var second = Row("UnusedRow", new Vector2(0, 0));
        var group = Node("PropertyGroup", canvas).AddComponent<NpcFriendshipPropItemGroup>();
        Set(group, "propItems", new List<NpcFriendshipPropItem> { row, second }); group.Configure(services);
        int gate = services.GateReads;
        group.Init(new List<(Prop, long)> { (Prop.Attack, 30) }, false, new List<(Prop, long)> { (Prop.Defence, 7) });
        Require(services.GateReads == gate + 1, "original group evaluates function gate");
        Require(Get<WorldMultiLanguageText>(row, "mTxtPropValueAdd").Text == "+7", "group uses addition row index rather than matching property type");
        Require(row.GetComponent<CanvasGroup>().alpha == 1 && row.GetComponent<CanvasGroup>().blocksRaycasts, "populated row visible and interactive");
        Require(second.GetComponent<CanvasGroup>().alpha == 0 && !second.GetComponent<CanvasGroup>().interactable && !second.GetComponent<CanvasGroup>().blocksRaycasts, "unused slot hidden without intercepting input");
        var previewGo = Node("Preview", canvas, new Vector2(0, -200)); previewGo.SetActive(false);
        var preview = previewGo.AddComponent<NpcFriendshipPreviewPropItem>();
        var prop = PlayerProp(previewGo.transform);
        Set(preview, "mPropItem", prop); Set(preview, "mLevelText", Label("Level {0}", previewGo.transform));
        Set(preview, "mTravelPropText", Label("Travel", previewGo.transform));
        Set(preview, "mTravelPropStateSwitcher", Empty(previewGo)); Set(preview, "mTravelItemStateSwitcher", Empty(previewGo));
        preview.Configure(services); var npc = new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 };
        services.Values = new Dictionary<Prop, long> { [Prop.Attack] = 10, [Prop.Defence] = -3 };
        preview.Init(npc, 1); previewGo.SetActive(true); yield return null;
        Require(services.BaselineReads == 1 && services.DiffReads == 0, "level one reads full baseline, skips negative value");
        Require(Get<Prop>(prop, "mPropType") == Prop.Attack, "negative baseline does not replace displayed positive property");
        Require(Get<WorldMultiLanguageText>(preview, "mLevelText").Text == "Level 1", "original level label formatting");
        services.Values = new Dictionary<Prop, long> { [Prop.HangUpExpAdd] = 2, [Prop.Attack] = 4 }; services.Limited = true;
        preview.Init(npc, 2);
        Require(services.DiffReads == 1 && Get<WorldStateSwitcher<bool>>(preview, "mTravelPropStateSwitcher").State && !Get<WorldStateSwitcher<bool>>(preview, "mTravelItemStateSwitcher").State, "preview difference and travel function gate");
        gate = services.GateReads; expectedErrors = 1;
        services.Values = new Dictionary<Prop, long> { [Prop.Attack] = 1, [Prop.Defence] = 2, [Prop.MaxHp] = 3 };
        preview.Init(npc, 2);
        Require(expectedErrors == 0 && services.GateReads == gate, "over-two-property diagnostic returns before label/gate updates");
        preview.SetBgVisible(false); Require(!Get<Image>(prop, "myBg").enabled, "preview delegates original background visibility");
        yield return null;
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var shot = new Texture2D(768, 1024, TextureFormat.RGB24, false); shot.ReadPixels(new Rect(0, 0, 768, 1024), 0, 0); shot.Apply(); RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "gift-controller-fixture.png"), shot.EncodeToPNG()); Object.Destroy(shot);
        gift.Clear(); Object.Destroy(gift.gameObject); yield return null;
        Require(errors == 0, "no unexpected lifecycle/render errors");
    }
    private GameObject Node(string name, Transform parent, Vector2 position = default)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(240, 60); rect.anchoredPosition = position; return go; }
    private WorldMultiLanguageText Label(string key, Transform parent)
    { var go = Node(key, parent, new Vector2(0, key == "Value" ? 35 : key == "Addition" ? -35 : key.StartsWith("Level") ? 70 : 0)); ((RectTransform)go.transform).sizeDelta = new Vector2(480, 32); var tmp = go.AddComponent<TextMeshProUGUI>(); tmp.font = font; tmp.fontSize = 18; tmp.raycastTarget = false; var label = go.AddComponent<WorldMultiLanguageText>(); label.SetKey(key); return label; }
    private ProfessionPlayerPropItem PlayerProp(Transform parent)
    { var go = Node("SharedPlayerProp", parent); var item = go.AddComponent<ProfessionPlayerPropItem>(); Set(item, "mActiveStateSwitcher", Empty(go)); Set(item, "myBg", go.AddComponent<Image>()); Set(item, "myPropValueText", Label("Value", go.transform)); return item; }
    private NpcFriendshipPropItem Row(string name, Vector2 position)
    { var go = Node(name, canvas, position); go.AddComponent<CanvasGroup>(); var row = go.AddComponent<NpcFriendshipPropItem>(); Set(row, "mPropItem", PlayerProp(go.transform)); Set(row, "mTravelPropText", Label("Travel", go.transform)); Set(row, "mTxtPropValueAdd", Label("Addition", go.transform)); Set(row, "mTravelStateSwitcher", Empty(go)); return row; }
    private static WorldStateSwitcher<bool> Empty(GameObject owner) => WorldStateSwitcher<bool>.FromBindings(owner, Array.Empty<IWorldStateConfig>());
    private void Click(GameObject target)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)target.transform;
        var point = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == target, "simulated pointer reaches reduce button without occlusion");
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }
    private static FieldInfo Field(object value, string name)
    { for (var t = value.GetType(); t != null; t = t.BaseType) { var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; } throw new MissingFieldException(value.GetType().Name, name); }
    private static void Set(object value, string name, object field) => Field(value, name).SetValue(value, field);
    private static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Gift controller: " + message); }
}
