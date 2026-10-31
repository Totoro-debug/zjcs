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
using Prop = Zjcs.Official.ProfessionUI.ProfessionPropertyType;

[InitializeOnLoad]
public static class PartnerGiftNativeValidation
{
    static PartnerGiftNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerGiftNative", false))
                new GameObject("OriginalFriendshipFixture").AddComponent<PartnerGiftNativeRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerGiftNative", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerGiftNative.Deadline", float.MaxValue))
                Finish(new TimeoutException("Original friendship prefab validation timeout"));
        };
    }
    public static void Run()
    {
        var work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Owned isolated Unity project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerGiftNative", true);
        SessionState.SetFloat("PartnerGiftNative.Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerGiftNative", false);
        if (error == null) Debug.Log("PARTNER_GIFT_NATIVE_PASSED: original dialog template/property group, raycast exclusion, fonts, state, particles and scoped audio endpoints; complete gift window not covered.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerGiftNativeRunner : MonoBehaviour
{
    [Serializable] private sealed class TextRow { public string key, text; }
    [Serializable] private sealed class TextCatalog { public string manifest; public TextRow[] texts; }
    private sealed class Context : IPartnerFriendshipUiServices, IProfessionPropertyBindings, ILocalizationTextService, IProfessionSoundHost, IProfessionSoundObjectHost
    {
        public Dictionary<string, string> Texts;
        public Dictionary<Prop, long> Values;
        public int Popups, SoundPosts, Registered, Unregistered;
        public bool Limited;
        public IProfessionPropertyBindings Properties => this;
        public List<Prop> TravelNpcPropTypeList { get; } = new List<Prop> { Prop.HangUpExpAdd };
        public bool IsTravelFunctionLimited => Limited;
        public IReadOnlyDictionary<Prop, long> GetFriendshipLevelPropDiff(PartnerFriendshipNpcInfo npc, int from, int to) => Values;
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
            catch (Exception error) { PartnerGiftNativeValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerGiftNativeValidation.Finish(errors == 0 ? null : new Exception("Unexpected native prefab errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var catalog = JsonUtility.FromJson<TextCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PartnerGift/partner-gift-texts.json").text);
        Require(catalog.manifest == "884_156692", "selected original language");
        var context = new Context { Texts = catalog.texts.ToDictionary(row => row.key, row => row.text), Values = new Dictionary<Prop, long> { [Prop.Attack] = 10 } };
        ProfessionPropertyBindings.Current = context; LanguageManager.Initialize(context); OfficialWwiseEvent.Host = context;
        // The original effect shader has UniversalPipeline/Universal2D passes.
        // A fixture-only 2D pipeline exercises that shader without editing it.
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        WorldMultiLanguageText.Localize = key => context.Text(key);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PartnerGift/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset");
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf");
        Require(font != null && source != null, "original regional font and current source TTF");
        Set(font, "m_SourceFontFile", source); font.fallbackFontAssetTable.Clear(); WorldMultiLanguageText.LanguageFont = () => font;
        cameraUi = new GameObject("NativeFriendshipCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(0.15f, 0.18f, 0.2f);
        target = new RenderTexture(768, 1024, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("NativePrefabFixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        canvas.scaleFactor = 0.75f; // Fit the original 890-wide template into the fixture camera.
        events = new GameObject("SimulatedNativePointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var host = new GameObject("InactiveBindingHost", typeof(RectTransform)); host.transform.SetParent(canvas.transform, false); host.SetActive(false);
        ((RectTransform)host.transform).sizeDelta = new Vector2(680, 800);
        var previewGo = Native("NpcFriendshipPreviewPropItem", host.transform, new Vector2(0, 150));
        var preview = previewGo.GetComponent<NpcFriendshipPreviewPropItem>(); preview.Configure(context);
        preview.Init(new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 }, 1);
        var groupGo = Native("NpcFriendshipPropItemGroup", host.transform, new Vector2(0, -40));
        var group = groupGo.GetComponent<NpcFriendshipPropItemGroup>(); group.Configure(context);
        host.SetActive(true); yield return null;
        var rows = Get<List<NpcFriendshipPropItem>>(group, "propItems"); Require(rows.Count == 2, "original two property slots");
        group.Init(new List<(Prop, long)> { (Prop.Attack, 300), (Prop.Defence, 40) }, true, new List<(Prop, long)> { (Prop.Attack, 20), (Prop.Defence, 4) });
        yield return null; Capture("native-friendship-normal.png");
        var playerProp = Get<ProfessionPlayerPropItem>(preview, "mPropItem");
        Require(Get<WorldMultiLanguageText>(playerProp, "myPropValueText").Text.Contains("10"), "original prefab property text");
        Require(Get<Button>(playerProp, "PropDetailBtn") == null, "original row has no property detail binding");
        Require(previewGo.GetComponentsInChildren<Button>(true).Length == 0 && context.Popups == 0, "actual dialog template is display-only");
        context.Values = new Dictionary<Prop, long> { [Prop.HangUpExpAdd] = 2, [Prop.Attack] = 4 };
        preview.Init(new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 }, 2);
        group.Init(new List<(Prop, long)> { (Prop.HangUpExpAdd, 6) }, false);
        yield return null; Capture("native-friendship-travel.png");
        var travelText = Get<WorldMultiLanguageText>(preview, "mTravelPropText");
        Require(travelText.gameObject.activeInHierarchy && travelText.Text.Contains("+2"), "original travel binding renders the level difference");
        Require(Get<WorldMultiLanguageText>(playerProp, "myPropValueText").Text.Contains("4"), "original normal property refreshes alongside travel property");
        var unused = rows[1].GetComponent<CanvasGroup>(); Require(unused.alpha == 0 && !unused.blocksRaycasts, "native unused row does not intercept input");
        RequireNoRaycast(unused.transform);
        context.Limited = true; preview.Init(new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 }, 2);
        yield return null; Capture("native-friendship-travel-limited.png");
        Require(!travelText.gameObject.activeInHierarchy, "original travel-function gate hides the embedded travel item");
        group.Init(new List<(Prop, long)> { (Prop.Attack, 300) }, true, new List<(Prop, long)> { (Prop.Attack, 20) });
        yield return null;
        var effectProp = Get<ProfessionPlayerPropItem>(rows[0], "mPropItem");
        Require(effectProp.myEffect != null, "original property change particle effect");
        StartCoroutine(effectProp.PlayEffect(true)); yield return new WaitForSeconds(0.4f);
        var particles = effectProp.myEffect.GetComponentsInChildren<ParticleSystem>();
        Require(particles.Length > 0 && particles.Sum(particle => particle.particleCount) > 0, "original effect emits live particles");
        Require(particles.All(particle => particle.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported), "original effect shaders supported by Windows renderer");
        Capture("native-friendship-effect.png");
        Require(context.SoundPosts > 0 && context.Registered > 0, "original enabled effect triggers preserved Wwise event once per activation");
        Object.Destroy(host); yield return null;
        Require(context.Registered == context.Unregistered, "original native sound objects unregister on destruction");
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
