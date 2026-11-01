// Exact original roster subtree/card assets. Popup, tip and audio endpoints are explicit fixture ports.
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
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerRosterNativeValidation
{
    static PartnerRosterNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerRosterNative", false))
                new GameObject("OriginalRosterValidation").AddComponent<PartnerRosterNativeRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerRosterNative", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerRosterNative.Deadline", float.MaxValue))
                Finish(new TimeoutException("Original roster validation timeout"));
        };
    }
    public static void Run()
    {
        var work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Owned isolated Unity project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerRosterNative", true);
        SessionState.SetFloat("PartnerRosterNative.Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerRosterNative", false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_ROSTER_NATIVE_PASSED: original roster/card hierarchy, real local roster state, simulated show-all/sort/reverse/close/card clicks, scrolling and pool lifetime; complete NpcPanel/detail/player not covered.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerRosterNativeRunner : MonoBehaviour, IOfflinePartnerPresentation, IProfessionImageResourceLoader, IProfessionSoundHost
{
    [Serializable] private sealed class Pointer { public string guid; public long fileID; }
    [Serializable] private sealed class Row { public string key, text; }
    [Serializable] private sealed class SpriteRow { public string address; public Pointer pointer; }
    [Serializable] private sealed class FontRow { public Pointer font_asset, source_font; }
    [Serializable] private sealed class Catalog { public Row[] texts; public SpriteRow[] sprites; public FontRow font; }
    private sealed class Tips : IProfessionTipDotSystem, IProfessionTipDotSystemController
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
    private sealed class Pool : LoopScrollPrefabSource
    {
        public LoopScrollPrefabSource Original;
        public PartnerRosterNativeRunner Owner;
        public Transform GetPoolRoot() => Original.GetPoolRoot();
        public GameObject GetObject(int index)
        {
            var go = Original.GetObject(index);
            go.GetComponent<NpcPanelNpcItem>().Configure(Owner.services);
            var tips = new Tips();
            foreach (var linker in go.GetComponentsInChildren<TipDotNodeLinker>(true)) { linker.Controller = tips; linker.TipDotSystem = tips; }
            return go;
        }
        public void ReturnObject(Transform value, int index) => Original.ReturnObject(value, index);
    }
    private Camera cameraUi;
    private RenderTexture target;
    private EventSystem events;
    private OfflinePartnerRosterUiServices services;
    private OfflineMainlineProgressSession session;
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private int errors, acquired, released, detailCalls, giftCalls;
    private IEnumerator Start()
    {
        var check = Check();
        while (true)
        {
            object value;
            try { if (!check.MoveNext()) break; value = check.Current; }
            catch (Exception error) { PartnerRosterNativeValidation.Finish(error); yield break; }
            yield return value;
        }
        PartnerRosterNativeValidation.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"),
            OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json")));
        var tasks = OfficialMainlineTaskCatalog.Load(File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")),
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks-source-manifest.json")));
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        session = OfflineMainlineProgressSession.Open(slots, slots.CreateSlot(), tasks);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "原生伙伴名册验证";
            player.UnlockedMapClassIds.Add(11); player.UnlockedMapClassIds.Add(12);
            player.Inventory.Add(new OfflineItemStack { ItemId = 1300, Count = 2 });
            int rank = 0;
            var developable = new HashSet<int>(catalog.PartnerRows("npc_friendship_level").Where(row => Convert.ToInt32(row["Level"]) >= 4).Select(row => Convert.ToInt32(row["Id"])));
            foreach (int id in catalog.PartnerRows("npc").Where(row => Convert.ToInt32(row["NpcType"]) != 2 && Convert.ToInt32(row["NpcRoleType"]) != 2)
                .Select(row => Convert.ToInt32(row["Id"])).Where(id => id != 8401 && (id < 11015 || id > 11022) && developable.Contains(id)).Take(21))
            {
                int level = ++rank % 4 + 1;
                long exp = Convert.ToInt64(catalog.PartnerRows("npc_friendship_level").Single(row => Convert.ToInt32(row["Id"]) == id && Convert.ToInt32(row["Level"]) == level)["FriendshipExp"]);
                player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = id, Level = level, FriendshipExp = exp, ActivateTimeTicks = 10000 - rank,
                    IsCollected = id == 8404 || id == 8411, CollectedTimeTicks = rank });
            }
        }).IsApplied, "Seed isolated actual profile");
        services = new OfflinePartnerRosterUiServices(session, catalog, new OfflineCharacterProgressionService(session, catalog), this, this);
        OfficialWwiseEvent.Host = this;
        var cardCatalog = JsonUtility.FromJson<Catalog>(AssetDatabase.LoadAssetAtPath<TextAsset>(Find("partner-card-images.json")).text);
        var rosterTexts = JsonUtility.FromJson<Catalog>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PartnerGift/partner-roster-texts.json").text);
        var texts = cardCatalog.texts.Concat(rosterTexts.texts).GroupBy(row => row.key).ToDictionary(group => group.Key, group => group.Last().text);
        WorldMultiLanguageText.Localize = key => texts.TryGetValue(key, out string text) ? WorldBattleReportTextService.ProcessText(text) : key;
        foreach (var row in cardCatalog.sprites) sprites.Add(row.address, AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(row.pointer.guid)));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(cardCatalog.font.font_asset.guid));
        Set(font, "m_SourceFontFile", AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(cardCatalog.font.source_font.guid)));
        font.fallbackFontAssetTable.Clear(); WorldMultiLanguageText.LanguageFont = () => font;
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("OriginalRosterCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.backgroundColor = Color.gray;
        target = new RenderTexture(720, 1560, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("NativeRosterFixtureHost", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1; canvas.scaleFactor = 2f / 3f;
        events = new GameObject("SimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var host = new GameObject("InactiveNativeBinding", typeof(RectTransform)); host.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)host.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        host.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PartnerGift/NpcRosterContent.prefab");
        Require(prefab != null, "Original roster root loaded");
        foreach (var node in prefab.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "No missing original component: " + node.name);
        foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true)) Require(!UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(component), "No missing managed reference: " + component.name);
        var root = Object.Instantiate(prefab, host.transform, false);
        root.SetActive(true); // The original NpcPanel tab host activates mNpcListGo.
        var list = root.GetComponent<NpcPanelNpcList>(); list.Configure(services);
        var loop = root.GetComponentInChildren<LoopVerticalScrollRect>(true);
        Require(loop.DefaultPrefabSource.Prefab.GetComponent<NpcPanelNpcItem>() != null, "Original serialized card source");
        Set(loop, "mPrefabSource", new Pool { Original = loop.prefabSource, Owner = this });
        host.SetActive(true); yield return null; yield return new WaitForSeconds(0.8f);
        Require(loop.totalCount == services.NpcRoster(false).Count, "Original initial roster count: " + loop.totalCount + "/" + services.NpcRoster(false).Count + ", active=" + list.isActiveAndEnabled);
        Require(loop.content.GetComponentsInChildren<NpcPanelNpcItem>().Length > 0, "Original list instantiates visible cards");
        Require(services.NpcRoster(false).Any(value => value.NpcClassId == 8404)
            && services.NpcRoster(false).Any(value => value.NpcClassId == 8411), "Original roster shows saved tutor and trader cards");
        var filter = root.GetComponentInChildren<FilterSortPanel>(true);
        Require(!filter.CloseBtn.enabled && !filter.CloseBtn.targetGraphic.enabled, "Original initial animation disables root close mask");
        Capture("native-roster-default.png");
        foreach (int id in new[] { 8404, 8411 })
        {
            var card = loop.content.GetComponentsInChildren<NpcPanelNpcItem>().Single(value => Get<NpcRosterItem>(value, "mNpcDataWrap").NpcClassId == id);
            int before = detailCalls;
            Click(Get<Button>(card, "mDetailBtn").gameObject); yield return null;
            Require(detailCalls == before + 1, "Simulated pointer opens original trader/tutor card " + id);
        }
        var showAll = root.GetComponentInChildren<Zjcs.Official.SkillsUI.UIToggle>(true);
        Click(showAll.gameObject); yield return null; yield return new WaitForSeconds(0.8f);
        Require(showAll.isOn && loop.totalCount == services.NpcRoster(true).Count && loop.totalCount > services.NpcRoster(false).Count, "Original show-all simulated click adds unmet gift owner: on=" + showAll.isOn + ", count=" + loop.totalCount + ", default=" + services.NpcRoster(false).Count + ", all=" + services.NpcRoster(true).Count);
        Capture("native-roster-all.png");
        Click(filter.SwitchBtn.gameObject); yield return new WaitForSeconds(0.4f);
        Require(filter.FilterSortDialog.activeInHierarchy, "Original sort dialog opens");
        Require(filter.CloseBtn.enabled && filter.CloseBtn.targetGraphic.enabled, "Original open animation enables root close mask");
        Capture("native-roster-sort.png");
        var option = filter.FilterSortDialog.GetComponentsInChildren<CollectSortGroupItem>().Single(item => item.SortType == CollectSortType.ActivateTime);
        Click(option.gameObject); yield return null;
        Require(Get<CollectSortType>(list, "mSortType") == CollectSortType.ActivateTime, "Original time sorting selected by pointer");
        Click(option.ReverseBtn.gameObject); yield return null;
        Require(Get<bool>(list, "mIsReverse"), "Original reverse button changes list sorting");
        Click(filter.CloseBtn.gameObject); yield return new WaitForSeconds(0.5f);
        Require(!filter.FilterSortDialog.activeInHierarchy, "Original sort close waits for animation");
        Require(!filter.CloseBtn.enabled && !filter.CloseBtn.targetGraphic.enabled, "Original close animation releases underlying card raycasts");
        Capture("native-roster-closed.png");
        var metCard = loop.content.GetComponentsInChildren<NpcPanelNpcItem>().First(card => Get<Button>(card, "mDetailBtn").gameObject.activeInHierarchy);
        Click(Get<Button>(metCard, "mDetailBtn").gameObject); yield return null;
        Require(detailCalls + giftCalls == 3, "Visible original card dispatches its route");
        var beforeScroll = loop.content.anchoredPosition;
        var pointer = new PointerEventData(events) { scrollDelta = new Vector2(0, -20) };
        ExecuteEvents.Execute(loop.gameObject, pointer, ExecuteEvents.scrollHandler); yield return new WaitForSeconds(0.6f);
        Require((loop.content.anchoredPosition - beforeScroll).sqrMagnitude > 1, "Actual original scroll moves content");
        Capture("native-roster-scroll.png");
        host.SetActive(false); yield return null;
        Require(acquired == released, "Pooled card sprite leases released on disable");
        host.SetActive(true); yield return new WaitForSeconds(0.8f);
        Require(loop.totalCount == services.NpcRoster(false).Count && !showAll.isOn, "Original re-enable resets show-all");
        host.SetActive(false); yield return null;
        Require(acquired == released, "Re-enabled pooled cards release all sprite leases");
    }
    private static string Find(string filename) => AssetDatabase.GetAllAssetPaths().Single(path => path.StartsWith("Assets/") && path.EndsWith("/" + filename));
    private static FieldInfo Field(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new MissingFieldException(owner.GetType().Name, name);
    }
    private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    private static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)go.transform;
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == go || hits[0].gameObject.transform.IsChildOf(go.transform)), "Pointer raycast reaches original " + go.name + "; hit=" + (hits.FirstOrDefault().gameObject?.name ?? "none"));
        pointer.pointerCurrentRaycast = hits[0]; ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false); picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); picture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), picture.EncodeToPNG()); Object.Destroy(picture); RenderTexture.active = previous;
    }
    public void BindNpcTipDot(TipDotNodeLinker linker, int id) => linker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(id));
    public void PostSound(AudioClip sound, GameObject owner) { Require(sound != null, "Original card sound bound"); }
    public uint PostEvent(uint eventId, GameObject owner) { Require(eventId != 0 && owner != null, "Original UI event and emitter bound"); return 1; }
    public void ShowFriendshipLevelUp(NpcRosterItem npc, long exp, int level) { }
    public void ShowExclusiveGift(int id, Component owner) { giftCalls++; }
    public void ShowNpcDetails(List<NpcRosterItem> npcs, int index) { Require(npcs[index].MeetStatus == NpcMeetStatus.HadMeet, "Met route selection"); detailCalls++; }
    public void ShowError(int error) => throw new InvalidOperationException("Unexpected original error " + error);
    public void ShowProgressionResult(ProgressionResult result) => throw new InvalidOperationException("Unexpected original result " + result.Status);
    public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : Object { acquired++; return new Lease<T>(sprites[path] as T, () => released++); }
    public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : Object => LoadAsset<T>(path);
    public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : Object => throw new NotSupportedException();
    public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => throw new NotSupportedException();
    private sealed class Lease<T> : IProfessionAssetOperation<T> where T : Object
    {
        public bool IsValid { get; private set; } = true; public int Status => 2; public T Result { get; private set; }
        private readonly Action release;
        public Lease(T value, Action release) { Result = value; this.release = release; Require(value != null, "Actual original sprite exists"); }
        public event Action<IProfessionAssetOperation<T>> Completed { add { value(this); } remove { } }
        public void Dispose() { Require(IsValid, "Single release"); IsValid = false; Result = null; release(); }
    }
}
