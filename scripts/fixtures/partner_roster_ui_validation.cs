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
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerRosterUiValidation
{
    public static bool NativeCard => SessionState.GetBool("PartnerRosterUi.NativeCard", false);
    static PartnerRosterUiValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerRosterUi", false))
                new GameObject("PartnerRosterUiValidation").AddComponent<PartnerRosterUiRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerRosterUi", false) &&
                EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerRosterUi.Deadline", float.MaxValue))
                Finish(new TimeoutException("Partner roster validation timed out"));
        };
    }
    public static void Run()
    {
        SessionState.SetBool("PartnerRosterUi.NativeCard", false);
        Begin();
    }
    public static void RunNativeCard()
    {
        SessionState.SetBool("PartnerRosterUi.NativeCard", true);
        Begin();
    }
    private static void Begin()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        if (string.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(work) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use an owned isolated Unity project");
        SessionState.SetBool("PartnerRosterUi", true);
        SessionState.SetFloat("PartnerRosterUi.Deadline", (float)EditorApplication.timeSinceStartup + 120);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerRosterUi", false);
        if (error != null) Debug.LogException(error);
        else Debug.Log((NativeCard ? "PARTNER_NATIVE_CARD_PASSED" : "PARTNER_ROSTER_UI_PASSED") + ": actual save, simulated card input, 700ms unlock, progress/preview, routes, recycling and resource lifetime");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerRosterUiRunner : MonoBehaviour, IOfflinePartnerPresentation, IProfessionImageResourceLoader
{
    private EventSystem events;
    private Camera cameraUi;
    private RenderTexture target;
    private Transform canvas;
    private TMP_FontAsset font;
    private Sprite fixtureSprite;
    private OfflineMainlineProgressSession session;
    private OfflinePartnerRosterUiServices services;
    private NpcPanelNpcItem card;
    private NpcFriendshipLevelShow friendship;
    private Button meet, detail;
    private GameObject content;
    private Image fill;
    private int errors, acquired, released, levelUps, gifts, details, failures, sounds, dotId;
    private readonly List<string> resourcePaths = new List<string>();
    private readonly List<string> notifications = new List<string>();
    private readonly Dictionary<string, Sprite> nativeSprites = new Dictionary<string, Sprite>();
    [Serializable] private sealed class Pointer { public string guid; public long fileID; }
    [Serializable] private sealed class SpriteConsumer { public int npc_id; public string field; }
    [Serializable] private sealed class SpriteBinding { public string address; public Pointer pointer; public SpriteConsumer[] consumers; }
    [Serializable] private sealed class TextBinding { public string key, text; }
    [Serializable] private sealed class FontBinding { public string region, language, name; public Pointer font_asset, source_font; }
    [Serializable] private sealed class NativeCatalog { public string manifest; public SpriteBinding[] sprites; public TextBinding[] texts; public FontBinding font; }
    [Serializable] private sealed class NativeNpc { public int Id, NpcType, NpcRoleType; public string NameKey, FigureImage, SmallBgImage; }
    [Serializable] private sealed class NativeTables { public NativeNpc[] npc; }
    [Serializable] private sealed class NativeRules { public string manifest; public NativeTables tables; }
    // Explicit test endpoint only; full local graph manager/provider is outside this check.
    private sealed class TipEndpoint : IProfessionTipDotSystem, IProfessionTipDotSystemController
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

    private IEnumerator Start()
    {
        IEnumerator run = Check();
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception error) { PartnerRosterUiValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerRosterUiValidation.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }

    private IEnumerator Check()
    {
        Application.logMessageReceived += Log;
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var tasks = OfficialMainlineTaskCatalog.Load(File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")),
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks-source-manifest.json")));
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        session = OfflineMainlineProgressSession.Open(slots, slots.CreateSlot(), tasks);
        var progression = new OfflineCharacterProgressionService(session, catalog);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "伙伴卡片隔离验证";
            player.UnlockedMapClassIds.Add(11); player.UnlockedMapClassIds.Add(12);
            player.Inventory.Add(new OfflineItemStack { ItemId = 1300, Count = 2 });
            player.Inventory.Add(new OfflineItemStack { ItemId = 1313, Count = 3 });
        }).IsApplied, "Seed actual local profile");
        services = new OfflinePartnerRosterUiServices(session, catalog, progression, this, this);
        services.NpcAdded += (id, type) => { Require(session.Save.Player.CharacterProperties.NpcFriendships.Any(value => value.NpcClassId == id), "Add callback follows saved transaction"); notifications.Add("add:" + id); };
        services.NpcFriendshipUpdated += id => notifications.Add("friendship:" + id);
        services.NpcCollectedChanged += (id, on) => notifications.Add("pin:" + id + ":" + on);
        services.NpcRemoved += id => notifications.Add("remove:" + id);
        font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        Require(font != null, "Official font loads in fixture");
        WorldMultiLanguageText.Localize = key => key;
        WorldMultiLanguageText.LanguageFont = () => font;
        if (PartnerRosterUiValidation.NativeCard)
        {
            var resources = JsonUtility.FromJson<NativeCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PartnerCard/partner-card-images.json").text);
            var currentRules = JsonUtility.FromJson<NativeRules>(read("Partners/partner-rules.json"));
            Require(resources.manifest == "884_156692" && currentRules.manifest == resources.manifest, "Selected original card configuration");
            var rosterNpcs = currentRules.tables.npc.Where(value => value.NpcType == 4 && value.NpcRoleType != 2
                && (value.Id < 11015 || value.Id > 11022)).ToDictionary(value => value.Id);
            var expectedAddresses = new HashSet<string>(rosterNpcs.Values.SelectMany(value => new[] { value.FigureImage, value.SmallBgImage }));
            Require(expectedAddresses.SetEquals(resources.sprites.Select(value => value.address)), "All current roster figure/background addresses");
            var expectedConsumers = new HashSet<string>(rosterNpcs.Keys.SelectMany(id => new[] { id + ":FigureImage", id + ":SmallBgImage" }));
            var actualConsumers = new List<string>();
            foreach (var binding in resources.sprites)
            {
                foreach (var consumer in binding.consumers)
                {
                    Require(rosterNpcs.TryGetValue(consumer.npc_id, out var definition), "Current roster image consumer");
                    string address = consumer.field == "FigureImage" ? definition.FigureImage
                        : consumer.field == "SmallBgImage" ? definition.SmallBgImage : null;
                    Require(address == binding.address, "Exact original image field: " + consumer.npc_id + "/" + consumer.field);
                    actualConsumers.Add(consumer.npc_id + ":" + consumer.field);
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(binding.pointer.guid));
                Require(sprite != null && sprite.texture != null, "Original dynamic sprite: " + binding.address);
                Require(sprite.vertices.Length == sprite.uv.Length && sprite.triangles.Length % 3 == 0,
                    "Dynamic sprite geometry: " + binding.address + " vertices=" + sprite.vertices.Length + " uv=" + sprite.uv.Length + " indices=" + sprite.triangles.Length);
                nativeSprites.Add(binding.address, sprite);
            }
            Require(expectedConsumers.SetEquals(actualConsumers) && expectedConsumers.Count == actualConsumers.Count, "Every roster image field bound exactly once");
            var texts = resources.texts.ToDictionary(value => value.key, value => WorldBattleReportTextService.ProcessText(value.text));
            Require(new HashSet<string>(rosterNpcs.Values.Select(value => value.NameKey).Concat(new[] { "ui_new_mark_text" })).SetEquals(texts.Keys), "All original roster names and card mark text");
            WorldMultiLanguageText.Localize = key => texts.TryGetValue(key, out string value) ? value : key;
            Require(resources.font.region == "CN" && resources.font.language == "zh_CN", "Original Chinese region/language selection");
            var regionalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(resources.font.font_asset.guid));
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(resources.font.source_font.guid));
            Require(regionalFont != null && sourceFont != null && regionalFont.name == resources.font.name, "Original CN font and source TTF");
            if (regionalFont.sourceFontFile == null) Set(regionalFont, "m_SourceFontFile", sourceFont); // Original FontAssetFontService.SetupFont.
            regionalFont.fallbackFontAssetTable.Clear(); // Original CN region has no fallbacks.
            foreach (char character in string.Concat(texts.Values))
                Require(regionalFont.HasCharacter(character, true, true), "Original font includes card text U+" + ((int)character).ToString("X4"));
            WorldMultiLanguageText.LanguageFont = () => regionalFont;
        }
        fixtureSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        events = new GameObject("SimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        cameraUi = new GameObject("RenderCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor;
        cameraUi.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        target = new RenderTexture(768, 1024, 24); cameraUi.targetTexture = target;
        var c = new GameObject("ControllerFixtureOnly", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cameraUi; c.planeDistance = 1; canvas = c.transform;
        MakeCard();
        var roster = services.NpcRoster(true).ToList();
        int index = roster.FindIndex(value => value.NpcClassId == 8401);
        Require(index >= 0 && roster[index].MeetStatus == NpcMeetStatus.UnMeet, "Partial signal item card exists");
        card.gameObject.SetActive(true); card.Init(roster, index); card.Show();
        yield return null;
        Require(dotId == 8401 && services.MeetProgress(8401).Value.Current == 2 && services.MeetProgress(8401).Value.Target == 3, "Original signal item progress");
        Require(resourcePaths.SequenceEqual(new[] { services.GetDefinition(8401).FigureImage, services.GetDefinition(8401).SmallBgImage }), "Card loads exact official figure/background addresses");
        Click(detail.gameObject); yield return null;
        Require(gifts == 1 && details == 0, "Unmet card routes to exclusive gift");
        if (PartnerRosterUiValidation.NativeCard) Require(!meet.gameObject.activeInHierarchy, "Original unmet card hides meeting button");
        else Click(meet.gameObject);
        yield return null;
        Require(services.GetNpc(8401) == null && session.InventoryCount(1300) == 2, "Unmet card cannot start meeting");
        if (PartnerRosterUiValidation.NativeCard) { yield return new WaitForSeconds(0.4f); Capture(Path.Combine(work, "native-card-unmet.png")); }

        session.ApplyCharacterMutation(player => player.Inventory.First(value => value.ItemId == 1300).Count = 3);
        services.Reload(); roster = services.NpcRoster(true).ToList(); index = roster.FindIndex(value => value.NpcClassId == 8401);
        card.Init(roster, index); card.Show(); yield return null;
        Require(Get<WorldStateSwitcher<NpcPanelNpcItem.LockStatus>>(card, "mLockEffectSwitcher").State == NpcPanelNpcItem.LockStatus.WaitLock, "Ready card waits for unlocking");
        if (PartnerRosterUiValidation.NativeCard) { yield return new WaitForSeconds(0.4f); Capture(Path.Combine(work, "native-card-ready.png")); }
        Click(meet.gameObject); Click(meet.gameObject);
        yield return null; yield return null;
        Require(services.GetNpc(8401) != null && session.InventoryCount(1300) == 0 && notifications.Count(value => value == "add:8401") == 1, "Rapid pointer clicks commit once");
        Require(Get<WorldStateSwitcher<NpcPanelNpcItem.LockStatus>>(card, "mLockEffectSwitcher").State == NpcPanelNpcItem.LockStatus.Locking && levelUps == 0 && sounds == 1, "Saved meet begins original lock effect/audio");
        if (PartnerRosterUiValidation.NativeCard) Capture(Path.Combine(work, "native-card-unlocking.png"));
        yield return new WaitForSeconds(0.35f);
        Require(levelUps == 0, "Unlock dialog waits for original 700ms delay");
        yield return new WaitForSeconds(0.5f);
        Require(levelUps == 1 && Get<WorldStateSwitcher<NpcPanelNpcItem.LockStatus>>(card, "mLockEffectSwitcher").State == NpcPanelNpcItem.LockStatus.Unlock, "Unlock finishes and shows level dialog once");
        if (PartnerRosterUiValidation.NativeCard) Capture(Path.Combine(work, "native-card-met.png"));
        Click(detail.gameObject); yield return null;
        Require(details == 1 && gifts == 1, "Met card opens filtered met roster at same selected NPC");
        Require(services.SetCollected(8401, true).IsApplied && services.GetNpc(8401).IsCollected && notifications.Contains("pin:8401:True"), "Pin callback follows actual persisted state");
        session.Reload(); Require(services.GetNpc(8401).IsCollected, "Pin restores from disk");

        var levels = catalog.Rows("npc_friendship_level").Where(value => Convert.ToInt32(value["Id"]) == 8401).OrderBy(value => Convert.ToInt32(value["Level"])).ToArray();
        long begin = Convert.ToInt64(levels[0]["FriendshipExp"]), next = Convert.ToInt64(levels[1]["FriendshipExp"]);
        long gain = Math.Max(1, (next - begin) / 4);
        friendship.Init(8401, begin + gain, 1, gain);
        Require(Math.Abs(fill.fillAmount - (float)gain / (next - begin)) < 0.001f, "Original fraction within current friendship level");
        var preview = Get<List<Image>>(friendship, "mPreviewImages")[0];
        Require(Math.Abs(preview.fillAmount - (float)(gain * 2) / (next - begin)) < 0.001f, "Original additive preview fraction");
        int max = services.FriendshipMaxLevel(8401);
        friendship.Init(8401, services.FriendshipExp(8401, max), max, 1);
        Require(fill.fillAmount == 1 && Get<WorldStateSwitcher<NpcFriendshipLevelShow.PreviewState>>(friendship, "mPreviewStateSwitcher").State == NpcFriendshipLevelShow.PreviewState.MaxLevel, "Global maximum uses max preview state");
        friendship.Init(8401);

        roster = services.NpcRoster(true).ToList(); index = roster.FindIndex(value => value.NpcClassId == 8424);
        card.Init(roster, index); card.Show(); yield return null;
        // Make the displayed condition stale before the actual saved request.
        session.ApplyCharacterMutation(player => player.Inventory.First(value => value.ItemId == 1313).Count = 0);
        Click(meet.gameObject); yield return null; yield return null;
        Require(services.GetNpc(8424) == null && failures == 1 && !Get<bool>(card, "mIsMeeting"), "Rejected local meet restores click availability");
        session.ApplyCharacterMutation(player => player.Inventory.First(value => value.ItemId == 1313).Count = 3);
        roster = services.NpcRoster(true).ToList(); index = roster.FindIndex(value => value.NpcClassId == 8424);
        card.Init(roster, index); card.Show(); yield return null;
        Click(meet.gameObject); yield return null; yield return null;
        Require(services.GetNpc(8424) != null, "Second actual meeting saves");
        card.gameObject.SetActive(false);
        Require(acquired == released, "All card sprite leases release when disabled");
        yield return new WaitForSeconds(0.8f);
        Require(levelUps == 1, "Recycled card cancels stale delayed popup");
        roster = services.NpcRoster(true).ToList(); index = roster.FindIndex(value => value.NpcClassId == 8401);
        card.gameObject.SetActive(true); card.Init(roster, index); card.Show(); yield return null;
        int before = details; Click(detail.gameObject); yield return null;
        Require(details == before + 1, "Re-enabled pooled card has one click subscription");
        session.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.First(value => value.NpcClassId == 8401).FriendshipExp = begin + gain);
        services.Reload(); yield return null;
        Require(notifications.Count(value => value == "friendship:8401") == 2 && Math.Abs(fill.fillAmount - (float)gain / (next - begin)) < 0.001f, "External saved friendship change refreshes live bar");
        if (PartnerRosterUiValidation.NativeCard) yield return new WaitForSeconds(0.4f);
        Capture(Path.Combine(work, PartnerRosterUiValidation.NativeCard ? "native-card-progress.png" : "roster-controller-fixture.png"));
        card.gameObject.SetActive(false); Require(acquired == released, "All resources released after final disable");
        Application.logMessageReceived -= Log;
    }

    private void MakeCard()
    {
        if (PartnerRosterUiValidation.NativeCard) { MakeNativeCard(); return; }
        var root = Node("OfficialCardControllerFixture", canvas, 0, 0); root.SetActive(false);
        card = root.AddComponent<NpcPanelNpcItem>();
        content = Node("Content", root.transform, 0, 0); Set(card, "mContentRoot", content);
        Set(card, "mCommonName", Label("CommonName", content.transform, -120, 200));
        Set(card, "mSpecialName", Label("SpecialName", content.transform, 120, 200));
        foreach (string field in new[] { "mIcon", "mIconDark", "mBgImage", "mBgImageDark", "mCollectionImage" })
        {
            var image = Node(field, content.transform, 0, 0).AddComponent<Image>(); image.raycastTarget = false; Set(card, field, image);
            image.gameObject.SetActive(false); // Resource lifetime assertions do not claim official artwork rendering.
        }
        var progress = Node("MeetProgress", content.transform, 0, 100).AddComponent<NpcMeetProgressItem>();
        progress.CanMeetStates = Empty<bool>(progress.gameObject); progress.ShowShadowStates = Empty<bool>(progress.gameObject);
        progress.WaitMeetProgressText = Label("Waiting", progress.transform, -100, 0);
        progress.CanMeetProgressText = Label("Ready", progress.transform, 100, 0); Set(card, "mNpcMeetProgressItem", progress);
        var friendshipGo = Node("Friendship", content.transform, 0, -100); friendship = friendshipGo.AddComponent<NpcFriendshipLevelShow>();
        fill = Node("Fill", friendshipGo.transform, 0, 0).AddComponent<Image>(); fill.type = Image.Type.Filled; fill.raycastTarget = false;
        var preview = Node("Preview", friendshipGo.transform, 0, -40).AddComponent<Image>(); preview.type = Image.Type.Filled; preview.raycastTarget = false;
        Set(friendship, "mFilledImages", new List<Image> { fill }); Set(friendship, "mPreviewImages", new List<Image> { preview });
        Set(friendship, "mLevelText", Label("Level", friendshipGo.transform, -100, 0));
        Set(friendship, "mPreviewExpText", Label("PreviewText", friendshipGo.transform, 100, 0));
        Set(friendship, "mMaxLevelStateSwitcher", Empty<bool>(friendshipGo));
        Set(friendship, "mPreviewStateSwitcher", Empty<NpcFriendshipLevelShow.PreviewState>(friendshipGo));
        Set(card, "mFriendshipLevel", friendship);
        detail = Button("Detail", content.transform, -140, -240); meet = Button("Meet", content.transform, 140, -240);
        Set(card, "mDetailBtn", detail); Set(card, "mMeetButton", meet);
        Set(card, "mNpcMeetStateSwitcher", Empty<NpcMeetStatus>(root));
        Set(card, "mNpcRoleStateSwitcher", Empty<NpcRoleType>(root));
        Set(card, "mLockEffectSwitcher", Empty<NpcPanelNpcItem.LockStatus>(root));
        card.MeetSound = AudioClip.Create("OriginalEventBindingProbe", 441, 1, 44100, false);
        card.Configure(services);
    }

    private void MakeNativeCard()
    {
        var owner = Node("OriginalCardPrefabHost", canvas, 0, 0); owner.SetActive(false);
        ((RectTransform)owner.transform).sizeDelta = Vector2.zero;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PartnerCard/NpcPanelNpcItem.prefab");
        var root = Object.Instantiate(source, owner.transform, false);
        card = root.GetComponent<NpcPanelNpcItem>();
        Require(root.GetComponentsInChildren<Transform>(true).Length == 72, "Original complete card hierarchy");
        foreach (var node in root.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "No missing native script: " + node.name);
        foreach (var image in root.GetComponentsInChildren<Image>(true).Where(value => value.sprite != null))
            Require(image.sprite.vertices.Length == image.sprite.uv.Length && image.sprite.triangles.Length % 3 == 0,
                "Static sprite geometry: " + image.name + "/" + image.sprite.name + " vertices=" + image.sprite.vertices.Length + " uv=" + image.sprite.uv.Length + " indices=" + image.sprite.triangles.Length);
        foreach (var label in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
        {
            var effect = label.GetComponent<TMPEffect>();
            if (effect != null) label.OnFontChanged += () => effect.SetupEffect(true); // Original MultiLanguageText.RefreshFont effect order.
        }
        var tips = new TipEndpoint();
        foreach (var linker in root.GetComponentsInChildren<TipDotNodeLinker>(true)) { linker.Controller = tips; linker.TipDotSystem = tips; }
        content = Get<GameObject>(card, "mContentRoot");
        friendship = Get<NpcFriendshipLevelShow>(card, "mFriendshipLevel");
        fill = Get<List<Image>>(friendship, "mFilledImages")[0];
        detail = Get<Button>(card, "mDetailBtn"); meet = Get<Button>(card, "mMeetButton");
        card.Configure(services);
        Require(card.MeetSound != null && card.MeetSound.frequency == 48000, "Original meet Wwise audio is bound");
        owner.SetActive(true);
    }

    private WorldMultiLanguageText Label(string name, Transform parent, float x, float y)
    {
        var node = Node(name, parent, x, y); var tmp = node.AddComponent<TextMeshProUGUI>(); tmp.font = font; tmp.fontSize = 18; tmp.raycastTarget = false;
        return node.AddComponent<WorldMultiLanguageText>();
    }
    private static GameObject Node(string name, Transform parent, float x, float y)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(240, 45); return go;
    }
    private static Button Button(string name, Transform parent, float x, float y)
    { var go = Node(name, parent, x, y); go.AddComponent<Image>(); return go.AddComponent<Button>(); }
    private static WorldStateSwitcher<T> Empty<T>(GameObject owner) where T : struct, IConvertible
        => WorldStateSwitcher<T>.FromBindings(owner, new IWorldStateConfig[0]);
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
    private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
    private void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases(); var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(cameraUi, go.transform.position) };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == go || hits[0].gameObject.transform.IsChildOf(go.transform)), "Pointer raycast reaches " + go.name);
        pointer.pointerCurrentRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string path)
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; Object.Destroy(image);
    }
    private void Log(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; }
    public void BindNpcTipDot(TipDotNodeLinker linker, int npcClassId)
    { dotId = npcClassId; if (PartnerRosterUiValidation.NativeCard) linker.SetNodeInstanceInfo(new NpcNodeInstanceInfo(npcClassId)); }
    public void PostSound(AudioClip sound, GameObject owner) { sounds++; }
    public void ShowFriendshipLevelUp(NpcRosterItem npc, long oldExp, int oldLevel) { Require(npc != null && oldExp == 0 && oldLevel == 1, "Original initial-level popup arguments"); levelUps++; }
    public void ShowExclusiveGift(int itemId, Component owner) { Require(itemId == 1300 && owner == card, "Exclusive gift route preserves identity"); gifts++; }
    public void ShowNpcDetails(List<NpcRosterItem> npcs, int index) { Require(npcs.All(value => value.MeetStatus == NpcMeetStatus.HadMeet) && npcs[index].NpcClassId == 8401, "Detail route preserves met roster and selection"); details++; }
    public void ShowError(int code) { throw new Exception("Unexpected error route " + code); }
    public void ShowProgressionResult(ProgressionResult result) { Require(!result.IsApplied, "Only rejected operations route to error display"); failures++; }
    public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : Object
    { acquired++; resourcePaths.Add(path); return new Lease<T>((PartnerRosterUiValidation.NativeCard ? nativeSprites[path] : fixtureSprite) as T, () => released++); }
    public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : Object => LoadAsset<T>(path);
    public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : Object => throw new NotSupportedException();
    public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => throw new NotSupportedException();
    private sealed class Lease<T> : IProfessionAssetOperation<T> where T : Object
    {
        public bool IsValid { get; private set; } = true; public int Status => 2; public T Result { get; private set; }
        private readonly Action release; public Lease(T value, Action release) { Result = value; this.release = release; }
        public event Action<IProfessionAssetOperation<T>> Completed { add { value(this); } remove { } }
        public void Dispose() { Require(IsValid, "A sprite lease cannot be disposed twice"); IsValid = false; Result = null; release(); }
    }
}
