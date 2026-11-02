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
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerStarGodLocalValidation
{
    const string Key = "PartnerStarGodLocalValidation.Active";
    static PartnerStarGodLocalValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original star-god local validation").AddComponent<PartnerStarGodLocalRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Local star-god validation timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "star-god-local-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerStarGodLocalRunner : MonoBehaviour, IOfflinePartnerPresentation, IProfessionSoundHost, IProfessionSoundObjectHost
{
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
    readonly Tips tips = new Tips();
    EventSystem events;
    Camera cameraUi;
    RenderTexture target;
    OfflineMainlineProgressSession session;
    OfflinePartnerStarGodUiServices services;
    OfflinePartnerRosterUiServices roster;
    OfflinePartnerGiftRuntimeBindings icons;
    PartnerStarGodPanelView view;
    NpcRosterItem met;
    int errors, additions, notices, details, levels, failures, sounds;
    void Start() => StartCoroutine(Guard(Check()));
    IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception error) { PartnerStarGodLocalValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerStarGodLocalValidation.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
    IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors++; };
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        Func<string, string> read = name => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", name));
        var selected = OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json"));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), selected);
        var tasks = OfficialMainlineTaskCatalog.Load(File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")),
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks-source-manifest.json")));
        var slots = new ProfileSlotCatalog(Path.Combine(work, "star-god-profiles"));
        string slot = slots.CreateSlot();
        session = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        var progression = new OfflineCharacterProgressionService(session, character);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "星神隔离验证";
            player.CharacterProperties.NpcFriendships.Clear(); player.Inventory.Clear(); player.UnlockedMapClassIds.Clear();
            foreach (int id in new[] { 1357, 1358, 1359 }) player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = 1 });
        }).IsApplied, "Seed isolated real profile with all three original fragments");
        var assets = OfficialPartnerStarGodAssetCatalog.LoadDefault();
        var images = assets.CreateImageCatalog();
        roster = new OfflinePartnerRosterUiServices(session, character, progression, new OfflineProfessionImageResourceLoader(images), this);
        icons = new OfflinePartnerGiftRuntimeBindings(session, selected, OfficialPartnerGiftPageAssetCatalog.LoadDefault(),
            id => new OfflinePartnerItemInfo(selected, id), key => key);
        services = new OfflinePartnerStarGodUiServices(session, selected, roster, assets, this, icons.BindGiftIcon);
        services.NpcAdded += (id, type) => {
            Require(id == 10608 && services.GetNpc(id) != null && session.InventoryCount(1357) == 0 &&
                OfflineMainlineProgressSession.Open(slots, slot, tasks).Save.Player.CharacterProperties.NpcFriendships.Any(row => row.NpcClassId == id),
                "Addition follows atomic disk save and fragment consumption"); additions++;
        };
        services.NpcFriendshipLevelUpdated += (id, from, to) => {
            Require(id == 10608 && from == 1 && to == 5 && ReferenceEquals(met, services.GetNpc(id)) && met.FriendshipLevel == 5,
                "Saved level changes update retained NPC identity before notification"); levels++;
        };
        Require(services.GetMeetNpcItems(10608).SequenceEqual(new[] { (1357, 1), (1358, 1), (1359, 1) }), "Original direct condition order");
        Require(services.GetMeetNpcItems(10708).SequenceEqual(new[] { (1367, 1), (1368, 1), (1369, 1) }) &&
            services.GetMeetNpcItems(10801).SequenceEqual(new[] { (1376, 1), (1377, 1), (1378, 1) }), "All nine original meet fragments");
        Require(services.GetMeetNpcItems(8402) == null && services.GetMeetNpcItems(-1) == null, "Original non-item and absent-NPC branches");
        Require(services.MeetStatusOf(10608) == NpcMeetStatus.CanMeet && services.OwnedNpcs.Count() == 0, "Preview is not an owned record");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.FindAssets("FZY4K SDF - dynamic t:TMP_FontAsset")
            .Select(AssetDatabase.GUIDToAssetPath).First(path => path.Contains("/Official/Partners/")));
        Set(font, "m_SourceFontFile", AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf")); font.fallbackFontAssetTable.Clear();
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("Original page camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.backgroundColor = Color.gray;
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("Original page viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("Simulated pointer", typeof(EventSystem)).GetComponent<EventSystem>();
        OfficialWwiseEvent.Host = this;
        view = canvas.gameObject.AddComponent<PartnerStarGodPanelView>(); view.Open(assets, services, font, BindNativeTree);
        yield return null; yield return new WaitForSeconds(0.5f);
        var list = view.GetComponentInChildren<NpcPanelStarGodContent>();
        var loop = list.GetComponentInChildren<LoopVerticalScrollRect>();
        var card = loop.content.GetComponentsInChildren<StarGodNpcItem>().Single(value => Get<NpcRosterItem>(value, "mNpcData") != null);
        foreach (var icon in Get<List<ItemIcon>>(card, "mCanMeetItems"))
            Require(icon.gameObject.activeInHierarchy && icon.IconSprite && new[] { 1357, 1358, 1359 }.Contains(icon.myInfo.ClassId),
                "Actual original ItemIcon shows each packaged fragment");
        Capture("star-god-local-fragments.png");
        Click(Get<Button>(card, "mNpcClickBtn").gameObject); Click(Get<Button>(card, "mNpcClickBtn").gameObject);
        yield return null; yield return new WaitForSeconds(Get<AnimationClip>(card, "mUnlockAnimation").length * 0.8f + 0.2f);
        Require(additions == 1 && notices == 1 && failures == 0, "Double pointer input commits and presents one unlock: " + additions + "/" + notices + "/" + failures);
        met = services.GetNpc(10608);
        Require(met != null && ReferenceEquals(met, services.GetNpc(10608)) && met.MeetStatus == NpcMeetStatus.HadMeet &&
            services.OwnedNpcs.Single() == met && session.InventoryCount(1358) == 0 && session.InventoryCount(1359) == 0,
            "All original fragments consumed; retained owned identity replaces preview");
        Capture("star-god-local-met.png");
        Click(Get<Button>(card, "mNpcClickBtn").gameObject); yield return null;
        Require(details == 1, "Simulated pointer routes owned star-god details");
        long threshold = Convert.ToInt64(selected.Rows("npc_friendship_level").Single(row => Convert.ToInt32(row["Id"]) == 10608 && Convert.ToInt32(row["Level"]) == 5)["FriendshipExp"]);
        Require(session.ApplyCharacterMutation(player => {
            var row = player.CharacterProperties.NpcFriendships.Single(value => value.NpcClassId == 10608);
            row.Level = 5; row.FriendshipExp = threshold; row.IsCollected = true; row.CollectedTimeTicks = DateTime.UtcNow.Ticks;
        }).IsApplied, "Save simultaneous external level and collection update");
        services.GetNpc(10608); // A read before Reload must not erase the previous notified level.
        roster.Reload(); yield return null;
        Require(levels == 1 && met.FriendshipLevel == 5 && met.IsCollected, "Combined saved changes retain correct previous level");
        Capture("star-god-local-level.png");
        var reopened = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        Require(reopened.Save.Player.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 10608).Level == 5 &&
            reopened.InventoryCount(1357) == 0, "New session restores actual star-god relationship and consumed materials");
        services.RequestMeetNpc(10708); yield return null; yield return null;
        Require(failures == 1 && additions == 1 && services.GetNpc(10708) == null, "Original missing materials reject meet without state change");
        Require(session.ApplyCharacterMutation(player => {
            foreach (int id in new[] { 1367, 1368, 1369, 1376, 1377, 1378 }) player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = 1 });
        }).IsApplied, "Seed remaining original fragments in isolated profile");
        roster.Reload(); view.Close(); yield return null;
        view.Open(assets, services, font, BindNativeTree); yield return null; yield return new WaitForSeconds(0.5f);
        list = view.GetComponentInChildren<NpcPanelStarGodContent>(); loop = list.GetComponentInChildren<LoopVerticalScrollRect>();
        var remaining = loop.content.GetComponentsInChildren<StarGodNpcItem>().Where(value => {
            var data = Get<NpcRosterItem>(value, "mNpcData"); return data != null && data.NpcClassId != 10608;
        }).ToArray();
        Require(remaining.Length == 2, "Reopening the original page discovers two newly meetable star-gods");
        foreach (var item in remaining)
            foreach (var icon in Get<List<ItemIcon>>(item, "mCanMeetItems"))
                Require(icon.gameObject.activeInHierarchy && icon.IconSprite && new[] { 1367, 1368, 1369, 1376, 1377, 1378 }.Contains(icon.myInfo.ClassId),
                    "Original grouped ItemIcons show all remaining fragment bindings");
        Capture("star-god-local-all-fragments.png");
        view.Close(); yield return null;
        services.Dispose(); services.Dispose(); icons.Dispose();
        roster.Reload();
        Require(levels == 1 && additions == 1 && sounds >= 2, "Close/dispose releases page event subscriptions and retains original PCM playback");
        Object.Destroy(images);
    }
    void BindNativeTree(GameObject root)
    {
        foreach (var linker in root.GetComponentsInChildren<TipDotNodeLinker>(true)) { linker.Controller = tips; linker.TipDotSystem = tips; }
    }
    public void BindNpcTipDot(TipDotNodeLinker linker, int id) => linker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(id));
    public void PostSound(AudioClip clip, GameObject owner)
    {
        Require(clip && clip.samples > 0, "Actual official PCM"); var source = owner.GetComponent<AudioSource>();
        if (!source) source = owner.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = clip; source.Play(); sounds++;
    }
    public void ShowFriendshipLevelUp(NpcRosterItem npc, long exp, int level)
    { Require(ReferenceEquals(npc, services.GetNpc(10608)) && exp == 0 && level == 1, "Original delayed unlock arguments"); notices++; }
    public void ShowExclusiveGift(int id, Component owner) => throw new NotSupportedException();
    public void ShowNpcDetails(List<NpcRosterItem> npcs, int index)
    { Require(npcs.Count == 1 && ReferenceEquals(npcs[index], met), "Owned star-god detail identity"); details++; }
    public void ShowError(int code) => throw new Exception("Unexpected error " + code);
    public void ShowProgressionResult(ProgressionResult result) { Require(!result.IsApplied, "Rejected transaction"); failures++; }
    public uint PostEvent(uint id, GameObject emitter)
    {
        Require(id == 3772241779, "Original click event");
        PostSound(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("a349287c39df984af5f51e2338d53e9f")), emitter); return (uint)sounds;
    }
    public bool IsInitialized => true;
    public float DefaultScalingFactor => 1;
    public int RegisterGameObject(GameObject emitter, string name) => 1;
    public void SetObjectPosition(GameObject emitter, Vector3 p, Vector3 f, Vector3 u) { }
    public void InitializeEmptyEnvironment(GameObject emitter, Collider source) { }
    public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position) { }
    public void SetDefaultListeners(GameObject emitter) { }
    public void SetScalingFactor(GameObject emitter, float factor) { }
    public void UnregisterGameObject(GameObject emitter) { }
    void Click(GameObject button)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
        // Fragment buttons legitimately cover the card center. Locate a real
        // exposed part of the original parent button through pointer raycasts.
        foreach (float y in new[] { 0.75f, 0.5f, 0.25f }) foreach (float x in new[] { 0.5f, 0.25f, 0.75f })
        {
            var point = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y));
            var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(point)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
            if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button) continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            return;
        }
        throw new Exception("Original card button has no exposed pointer target");
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; var frame = new Texture2D(450, 800, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, 450, 800), 0, 0); frame.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), frame.EncodeToPNG());
        RenderTexture.active = null; Object.Destroy(frame);
    }
    static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    static void Set(object value, string name, object data)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) { field.SetValue(value, data); return; } }
        throw new MissingFieldException(name);
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
