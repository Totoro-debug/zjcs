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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerStarGodValidation
{
    const string Key = "PartnerStarGodValidation.Active";
    static PartnerStarGodValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original star-god fixture").AddComponent<PartnerStarGodRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Star-god fixture did not finish"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "star-god-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerStarGodRunner : MonoBehaviour, IPartnerStarGodUiServices, IProfessionSoundHost, IProfessionSoundObjectHost
{
    [Serializable] sealed class Pointer { public string guid; public long fileID; }
    [Serializable] sealed class ImageRow { public string address; public Pointer pointer; }
    [Serializable] sealed class TextRow { public string key, text; }
    [Serializable] sealed class Fixture { public Pointer root; public ImageRow[] sprites; public TextRow[] texts; public PartnerStarGodNpcDefinition[] definitions; }
    sealed class Pool : LoopScrollPrefabSource
    {
        public PartnerStarGodRunner Owner;
        public LoopScrollPrefabSource Original;
        public Transform GetPoolRoot() => Original.GetPoolRoot();
        public GameObject GetObject(int index)
        {
            var go = Original.GetObject(index);
            foreach (var text in go.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Refresh();
            var tips = new Tips();
            foreach (var linker in go.GetComponentsInChildren<TipDotNodeLinker>(true)) { linker.Controller = tips; linker.TipDotSystem = tips; }
            foreach (var card in go.GetComponentsInChildren<StarGodNpcItem>(true)) card.Configure(Owner);
            Owner.BindSounds(go);
            return go;
        }
        public void ReturnObject(Transform value, int index) => Original.ReturnObject(value, index);
    }
    // Page fixture reports no pending red dots; graph evaluation has its own tests.
    sealed class Tips : IProfessionTipDotSystem, IProfessionTipDotSystemController
    {
        public bool IsShowTipDot => true;
        public bool TempLimitShowTip => false;
        public Action OnTipDotShowChanged { get; set; }
        public SafeAction OnTipDotSystemInited { get; set; } = new SafeAction();
        public SafeAction OnTipDotSystemPreClear { get; set; } = new SafeAction();
        public TipDotGraphType? GetGraphType(string name) => TipDotGraphType.Dynamic;
        public bool LinkNodeStateChange(TipDotNodeKey key, GameObject owner, Action<TipDotStateInfo> callback) { callback(default); return true; }
        public bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject owner, Action<TipDotStateInfo> callback) => true;
    }
    sealed class Lease : IProfessionAssetOperation<Sprite>
    {
        readonly Action release;
        public Lease(Sprite value, Action done) { Result = value; release = done; Require(value != null, "Original star-god sprite loaded"); }
        public bool IsValid { get; private set; } = true;
        public int Status => 2;
        public Sprite Result { get; private set; }
        public event Action<IProfessionAssetOperation<Sprite>> Completed { add { value(this); } remove { } }
        public void Dispose() { Require(IsValid, "Sprite lease released once"); IsValid = false; Result = null; release(); }
    }
    Fixture fixture;
    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    readonly Dictionary<int, NpcMeetStatus> statuses = new Dictionary<int, NpcMeetStatus>();
    readonly Dictionary<int, NpcRosterItem> owned = new Dictionary<int, NpcRosterItem>();
    Camera cameraUi;
    RenderTexture target;
    EventSystem events;
    bool disabled, pin = true;
    int acquired, released, errors, popups, sounds, requests, notices, lastError, collectedListeners, addedListeners, levelListeners;
    Action<int, bool> collected;
    Action<int, NpcMeetType> added;
    Action<int, int, int> level;
    public bool MainPlayerAvailable => true;
    public bool CanPinNpc => pin;
    public bool IsStarGodNpcDisabled => disabled;
    public IEnumerable<PartnerStarGodNpcDefinition> Definitions => fixture.definitions.Concat(new[] {
        new PartnerStarGodNpcDefinition { Id = -1, IsStarGodNpc = true, IsMapExploreBoss = true },
        new PartnerStarGodNpcDefinition { Id = -2, IsStarGodNpc = false } });
    public IEnumerable<NpcRosterItem> OwnedNpcs => owned.Values;
    public event Action<int, bool> NpcCollectedChanged { add { collected += value; collectedListeners++; } remove { collected -= value; collectedListeners--; } }
    public event Action<int, NpcMeetType> NpcAdded { add { added += value; addedListeners++; } remove { added -= value; addedListeners--; } }
    public event Action<int, int, int> NpcFriendshipLevelUpdated { add { level += value; levelListeners++; } remove { level -= value; levelListeners--; } }
    public PartnerStarGodNpcDefinition GetDefinition(int id) => fixture.definitions.FirstOrDefault(row => row.Id == id);
    public NpcRosterItem GetNpc(int id) => owned.TryGetValue(id, out var value) ? value : null;
    public NpcMeetStatus MeetStatusOf(int id) => statuses.TryGetValue(id, out var value) ? value : NpcMeetStatus.NotMeet;
    public NpcRosterItem CreatePreviewNpc(PartnerStarGodNpcDefinition definition, int initialLevel)
    { Require(initialLevel == 1, "Original preview starts at level one"); return new NpcRosterItem { NpcClassId = definition.Id, NpcRoleType = 2, FriendshipLevel = initialLevel }; }
    public void BindNpcTipDot(TipDotNodeLinker linker, int id) => linker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(id));
    public IProfessionAssetOperation<Sprite> LoadSprite(string path) { acquired++; return new Lease(sprites[path], () => released++); }
    public void PostSound(AudioClip clip, GameObject owner)
    { Require(clip != null && clip.samples > 0, "Original sound has PCM payload"); var source = owner.GetComponent<AudioSource>(); if (!source) source = owner.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = clip; source.Play(); sounds++; }
    public bool IsInitialized => true;
    public float DefaultScalingFactor => 1;
    public int RegisterGameObject(GameObject emitter, string name) => 1;
    public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up) { }
    public void InitializeEmptyEnvironment(GameObject emitter, Collider source) { Require(source == null, "Original collider-free sound object"); }
    public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position) { }
    public void SetDefaultListeners(GameObject emitter) { }
    public void SetScalingFactor(GameObject emitter, float factor) { }
    public void UnregisterGameObject(GameObject emitter) { }
    public uint PostEvent(uint eventId, GameObject emitter)
    {
        Require(eventId == 3772241779, "Original Main_Cursor_Normal event identity");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("a349287c39df984af5f51e2338d53e9f"));
        PostSound(clip, emitter); return (uint)sounds;
    }
    void BindSounds(GameObject root)
    {
        foreach (var value in root.GetComponentsInChildren<WorldButtonSound>(true)) value.Post = PostSound;
        foreach (var value in root.GetComponentsInChildren<WorldAnimationSoundPlayer>(true)) value.Post = PostSound;
    }
    public void RequestMeetNpc(int id)
    {
        requests++;
        var value = CreatePreviewNpc(GetDefinition(id), 1); value.MeetStatus = NpcMeetStatus.HadMeet;
        owned[id] = value; statuses[id] = NpcMeetStatus.HadMeet;
        added?.Invoke(id, default);
    }
    public void ShowFriendshipLevelUp(NpcRosterItem npc, long exp, int oldLevel)
    { Require(ReferenceEquals(npc, GetNpc(npc.NpcClassId)) && exp == 0 && oldLevel == 1, "Original delayed level-up uses owned NPC identity and arguments"); notices++; }
    public void ShowStarGodDetails(List<NpcRosterItem> npcs, int index)
    { Require(index >= 0 && npcs.All(row => GetDefinition(row.NpcClassId).IsStarGodNpc), "Original detail route contains met star-gods"); popups++; }
    public void ShowError(int code) { lastError = code; Require(code == 53897, "Original star-god disabled error"); }
    // Deliberately test the original null-item branch. Inventory/icon bindings
    // are outside this fixture; no claim of fragment rendering or local saving.
    public List<(int, int)> GetMeetNpcItems(int id) => null;
    public long GetItemAmount(int id) => throw new NotSupportedException("Inventory is outside the page fixture");
    public void PrepareItemIcon(ItemIcon icon) => throw new NotSupportedException("Fragment icons are outside the page fixture");
    void Start() { StartCoroutine(Guard(Check())); }
    IEnumerator Guard(IEnumerator steps)
    {
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { PartnerStarGodValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerStarGodValidation.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
    IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        OfficialWwiseEvent.Host = this;
        fixture = JsonUtility.FromJson<Fixture>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Editor/star-god-fixture.json").text);
        var catalog = OfficialPartnerStarGodAssetCatalog.LoadDefault();
        Require(catalog.Definitions.Length == 3 && catalog.Images.Length == 12, "Installed Resources catalog binds selected star-gods and nine meet fragments");
        fixture.definitions = catalog.Definitions;
        Require(fixture.definitions.All(row => row.IsStarGodNpc && !row.IsMapExploreBoss && row.Id > 0), "Installed catalog retains original star-god role and NPC type");
        foreach (var row in catalog.Images) sprites.Add(row.path, row.sprite);
        var texts = catalog.Texts.ToDictionary(row => row.key, row => row.text);
        WorldMultiLanguageText.Localize = key => texts.TryGetValue(key, out var text) ? text : key;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.FindAssets("FZY4K SDF - dynamic t:TMP_FontAsset")
            .Select(AssetDatabase.GUIDToAssetPath).First(path => path.Contains("/Official/Partners/")));
        Set(font, "m_SourceFontFile", AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        font.fallbackFontAssetTable.Clear();
        WorldMultiLanguageText.LanguageFont = () => font;
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("Star-god page camera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.gameObject.AddComponent<AudioListener>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.backgroundColor = Color.gray;
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("Isolated original page viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("Simulated pointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var parent = new GameObject("Inactive binding", typeof(RectTransform)); parent.transform.SetParent(canvas.transform, false);
        var viewport = (RectTransform)parent.transform; viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.sizeDelta = Vector2.zero; parent.SetActive(false);
        var prefab = catalog.PagePrefab;
        Require(prefab == Load<GameObject>(fixture.root), "Resources catalog retains exact original page prefab");
        var root = Object.Instantiate(prefab, parent.transform, false); root.SetActive(true);
        foreach (var node in root.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "No missing page script: " + node.name);
        var list = root.GetComponent<NpcPanelStarGodContent>();
        var loop = Get<LoopVerticalScrollRect>(list, "mLoop");
        Require(loop.DefaultPrefabSource.Prefab.GetComponent<StarGodNpcItemGroupItem>() != null, "Original loop points to external grouped prefab");
        var template = Get<StarGodNpcItemGroupItem>(list, "mTemplateGroupItem");
        Require(template.gameObject == loop.DefaultPrefabSource.Prefab, "Original group component retains external prefab identity");
        foreach (var node in template.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "No missing group script: " + node.name);
        foreach (var ui in template.GetComponentsInChildren<WorldUiMesh>(true))
        {
            Require(ui.enabled && ui.Mesh != null && ui.Mesh.vertexCount == 4 && ui.Mesh.vertices.Length == 4 &&
                ui.Mesh.triangles.SequenceEqual(new[] { 0, 1, 2, 0, 2, 3 }), "Original UI mesh imports full vertex/index buffers");
            Require(ui.Mesh.vertices.SequenceEqual(new[] { new Vector3(1.27f,-1.27f,0), new Vector3(1.27f,1.27f,0),
                new Vector3(-1.27f,1.27f,0), new Vector3(-1.27f,-1.27f,0) }), "Original UI mesh positions remain unchanged");
        }
        Set(loop, "mPrefabSource", new Pool { Owner = this, Original = loop.prefabSource }); BindSounds(root);
        int firstId = fixture.definitions[0].Id;
        statuses[firstId] = NpcMeetStatus.HadMeet;
        owned[firstId] = CreatePreviewNpc(fixture.definitions[0], 1); owned[firstId].IsCollected = true;
        list.Configure(this); parent.SetActive(true);
        yield return null; yield return new WaitForSeconds(0.5f);
        Require(loop.totalCount == 1 && Get<List<NpcRosterItem>>(list, "mStarGodNpcs").Count == 1, "Original list excludes invisible/normal/boss NPCs; count=" + loop.totalCount + "/" + Get<List<NpcRosterItem>>(list, "mStarGodNpcs").Count + "; active=" + list.isActiveAndEnabled);
        var content = Get<RectTransform>(list, "mContent");
        Require(content.pivot == new Vector2(0.5f, 0.5f), "Single star-god page uses original centered anchors");
        var first = loop.content.GetComponentsInChildren<StarGodNpcItem>().Single(card => Get<NpcRosterItem>(card, "mNpcData") != null);
        Require(Get<Image>(first, "mNpcImage").sprite == sprites[fixture.definitions[0].StaticFullBodyImagePath], "Original static full-body binding");
        var locator = Get<ImageLocator>(first, "mNpcImageLocator");
        Require(locator.Image.rectTransform.anchoredPosition == locator.FixedPos, "Original sprite pivot offset restored");
        Capture("star-god-single.png");
        Click(Get<Button>(first, "mNpcClickBtn").gameObject); yield return null;
        Require(popups == 1, "Simulated pointer opens original star-god detail route once");
        disabled = true; Click(Get<Button>(first, "mNpcClickBtn").gameObject); yield return null;
        Require(lastError == 53897 && popups == 1, "Disabled click keeps original error branch"); disabled = false;
        foreach (var definition in fixture.definitions) statuses[definition.Id] = NpcMeetStatus.HadMeet;
        collected?.Invoke(firstId, true); yield return null; yield return new WaitForSeconds(0.5f);
        Require(loop.totalCount == (int)Math.Ceiling((double)fixture.definitions.Length / template.GroupMaxCount), "Original grouped ceiling count");
        Require(content.pivot == new Vector2(0.5f, 1), "Multiple star-gods use top anchors");
        Require(Get<List<NpcRosterItem>>(list, "mStarGodNpcs")[0].NpcClassId == firstId, "Original collected priority precedes friendship");
        Capture("star-god-grouped.png");
        statuses[firstId] = NpcMeetStatus.CanMeet; owned.Remove(firstId);
        collected?.Invoke(firstId, false); yield return null; yield return new WaitForSeconds(0.4f);
        first = loop.content.GetComponentsInChildren<StarGodNpcItem>().Single(card => Get<NpcRosterItem>(card, "mNpcData")?.NpcClassId == firstId);
        var unlock = Get<AnimationClip>(first, "mUnlockAnimation"); float delay = unlock.length * Get<float>(first, "mPopMeetDelayFactor");
        Require(unlock != null && delay > 0, "Original unlock clip and 0.8 delay factor loaded");
        Click(Get<Button>(first, "mNpcClickBtn").gameObject); yield return null;
        Require(requests == 1 && notices == 0 && sounds > 0, "Pointer requests meet and starts original PCM/animation before notice");
        yield return new WaitForSeconds(delay + 0.2f);
        Require(notices == 1 && ReferenceEquals(Get<NpcRosterItem>(first, "mNpcData"), GetNpc(firstId)), "Original unlock delay resolves owned NPC and opens one notice");
        owned[firstId].FriendshipLevel = 5; level?.Invoke(firstId, 1, 5);
        Require(Get<WorldMultiLanguageText>(first, "mNpcLinklLevelText").Text == "5", "Original level event updates link label");
        Capture("star-god-unlocked.png");
        added?.Invoke(firstId, default); yield return null; int before = notices;
        parent.SetActive(false); yield return new WaitForSeconds(delay + 0.2f);
        Require(notices == before && acquired == released && collectedListeners == 0 && addedListeners == 0 && levelListeners == 0,
            "Pool disable cancels old notice and releases sprite/event leases");
        parent.SetActive(true); yield return null; yield return new WaitForSeconds(0.4f);
        Require(collectedListeners == 1, "Page re-enable has exactly one collection subscription");
        parent.SetActive(false); yield return null;
        Require(acquired == released && addedListeners == 0 && levelListeners == 0, "Re-enabled cards release all leases");
        var x = new NpcRosterItem { FriendshipExp = long.MaxValue };
        var y = new NpcRosterItem { FriendshipExp = -1 };
        Require(PartnerStarGodListRules.Compare(x, y, false) < 0, "Original comparer retains signed subtraction overflow semantics");
    }
    static T Load<T>(Pointer pointer) where T : Object
    { var value = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(pointer.guid)); Require(value != null, "Installed original asset " + pointer.guid); return value; }
    static FieldInfo Field(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType) { var result = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (result != null) return result; }
        throw new MissingFieldException(owner.GetType().Name, name);
    }
    static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Debug.Log("STAR_GOD_PASS " + message); }
    void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)go.transform;
        var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == go,
            "Pointer raycast reaches original " + go.name + "; hit=" + hits.FirstOrDefault().gameObject?.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); picture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), picture.EncodeToPNG());
        Object.Destroy(picture); RenderTexture.active = previous;
    }
}
