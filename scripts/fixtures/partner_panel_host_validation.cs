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
public static class PartnerPanelHostValidation
{
    const string Key = "PartnerPanelHostValidation.TipRuntime.Active";
    static PartnerPanelHostValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original star-god local validation").AddComponent<PartnerPanelHostRunner>();
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
        SetGameViewSize(450, 800);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    static void SetGameViewSize(int width, int height)
    {
        var editor = typeof(EditorWindow).Assembly;
        var sizesType = editor.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,
            new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
        var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, "Original partner panel fixture" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        var viewType = editor.GetType("UnityEditor.GameView");
        // Internal-display launcher owns placement. Never focus the window.
        var view = EditorWindow.GetWindow(viewType, false, "Game", false);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        view.Repaint();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "panel-host-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerPanelHostRunner : MonoBehaviour, IOfflinePartnerPresentation, IProfessionSoundHost, IProfessionSoundObjectHost
{
    EventSystem events;
    Camera cameraUi;
    RenderTexture target;
    OfflineMainlineProgressSession session;
    OfflinePartnerStarGodUiServices services;
    OfflinePartnerRosterUiServices roster;
    OfflinePartnerTipDotRuntime tipDots;
    OfflinePartnerGiftRuntimeBindings icons;
    PartnerPanelView view;
    NpcRosterItem met;
    int errors, additions, notices, details, levels, failures, sounds;
    void Start() => StartCoroutine(Guard(Check()));
    IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception error) { PartnerPanelHostValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerPanelHostValidation.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
    IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors++; };
        Require(Screen.width == 450 && Screen.height == 800, "Original SafeArea and pointer viewport use the same portrait resolution");
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
            player.CharacterCreated = true; player.CharacterName = "名册窗口验证"; player.Level = 1;
            player.CharacterProperties.NpcFriendships.Clear(); player.Inventory.Clear(); player.UnlockedMapClassIds.Clear();
            foreach (int id in new[] { 1357, 1358, 1359 }) player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = 1 });
        }).IsApplied, "Seed isolated real profile with all three original fragments");
        Require(progression.MeetAutomaticNpcs().IsApplied && session.Save.Player.CharacterProperties.NpcFriendships.Any(row => row.NpcClassId == 8402),
            "Create original auto-met partner through confirmed local transaction");
        var assets = OfficialPartnerStarGodAssetCatalog.LoadDefault();
        var images = assets.CreateImageCatalog();
        var rosterAssets = OfficialPartnerRosterAssetCatalog.LoadDefault();
        images.Entries = images.Entries.Concat(rosterAssets.Images.Entries).GroupBy(row => row.Path).Select(group => group.First()).ToArray();
        roster = new OfflinePartnerRosterUiServices(session, character, progression, new OfflineProfessionImageResourceLoader(images), this);
        Require(session.ApplyCharacterMutation(player =>
            player.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8402).Level = roster.FriendshipMaxLevel(8402)).IsApplied,
            "Seed isolated partner at the original configured friendship cap");
        tipDots = new OfflinePartnerTipDotRuntime(session, selected, OfficialPartnerGiftAssetCatalog.LoadDefault(), progression);
        tipDots.Refresh();
        icons = new OfflinePartnerGiftRuntimeBindings(session, selected, OfficialPartnerGiftPageAssetCatalog.LoadDefault(),
            id => new OfflinePartnerItemInfo(selected, id), key => key);
        services = new OfflinePartnerStarGodUiServices(session, selected, roster, assets, this, icons.BindGiftIcon);
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
        var mergedImages = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        mergedImages.Entries = images.Entries.Concat(rosterAssets.Images.Entries).GroupBy(row => row.Path).Select(group => group.First()).ToArray();
        var localizeRoster = rosterAssets.CreateLocalizer();
        var words = assets.Texts.ToDictionary(row => row.key, row => row.text);
        view = canvas.gameObject.AddComponent<PartnerPanelView>();
        var previousSkin = ProfessionAppearanceSkinClipBindings.Current;
        var previousSound = OfficialWwiseEvent.Host;
        var limited = new HashSet<int>();
        var context = new PartnerPanelContext {
            Roster = roster, StarGod = services, Dialogues = new NewProfileDialogueFixture(),
            Windows = new OfflinePartnerWindowService(CloseWindow, () => CloseWindow(view.Content)),
            Images = new OfflineProfessionImageResourceLoader(mergedImages), Font = font,
            Localize = key => words.TryGetValue(key, out var word) ? word : localizeRoster(key),
            IsFunctionLimit = limited.Contains, BindNativeTree = BindNativeTree,
            BindPartnerTipDotDialogues = tipDots.BindDialogues, PostSound = PostSound
        };
        int destroyedWindows = 0, removedObservers = 0;
        Action onDestroyed = () => destroyedWindows++;
        Action removedObserver = () => removedObservers++;
        view.Open(OfficialPartnerPanelAssetCatalog.Load(), context);
        context.Windows.ObserveDestroyed(view.Window, onDestroyed);
        context.Windows.ObserveDestroyed(view.Window, removedObserver);
        context.Windows.StopObservingDestroyed(view.Window, removedObserver);
        yield return null; yield return new WaitForSeconds(0.7f);
        Require(view.Window.State == WorldPopupWindow.EState.Show, "Original show clip completed");
        var panel = view.Content.GetComponent<NpcPanel>();
        var tabs = Get<MonoDictionary<NpcPanelTabType, WorldTabItem>>(panel, "mTabType2TabItemDict");
        Require(tabs[NpcPanelTabType.NpcList].isOn && Get<GameObject>(panel, "mNpcListGo").activeInHierarchy, "Original default roster");
        Require(view.Content.GetComponentsInChildren<NpcPanelNpcItem>().Length > 0, "Original roster pool populated");
        var originalPartner = view.Content.GetComponentsInChildren<NpcPanelNpcItem>()
            .Single(item => Get<NpcRosterItem>(item, "mNpcDataWrap")?.NpcClassId == 8402);
        var partnerLinker = Get<TipDotNodeLinker>(originalPartner, "mTipDotNodeLinker");
        var tipLevelState = Get<WorldStateSwitcher<ETipDotLevelType>>(partnerLinker, "mTipLevelState");
        var stateConfigs = (IList<IWorldStateConfig>)typeof(WorldStateSwitcher<ETipDotLevelType>)
            .GetField("mStateConfigs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tipLevelState);
        var levelGameObjects = stateConfigs.OfType<WorldStateGameObjectConfig>().Single();
        var levelData = Get<WorldStateGameObjectData>(levelGameObjects, "mStateGameObjectData");
        var importantObjects = levelData.GoConfig.Where(entry => (int)entry.State == (int)ETipDotLevelType.Important)
            .SelectMany(entry => entry.GameObjects).ToArray();
        Debug.Log("PARTNER TIP INDICATOR " + string.Join(";", importantObjects.SelectMany(root => root == null
            ? Array.Empty<Graphic>() : root.GetComponentsInChildren<Graphic>(true))
            .Select(graphic => graphic.name + ":" + graphic.enabled + ":" + graphic.gameObject.activeInHierarchy + ":"
                + graphic.color.a + ":" + (graphic is Image image ? image.sprite?.name : string.Empty))));
        Require(partnerLinker.Controller is TipDotSystemController && partnerLinker.TipDotSystem is TipDotSystem
            && partnerLinker.IsShowTip && partnerLinker.StateInfo.TipLevel == ETipDotLevelType.Important
            && tipLevelState.State == ETipDotLevelType.Important && importantObjects.Any(value => value != null && value.activeInHierarchy),
            "Original partner linker and active native Important-level indicator display the claimable friendship tip");
        Capture("panel-host-roster.png");
        Click(tabs[NpcPanelTabType.Dialogue].gameObject);
        VerifyTabSound();
        yield return new WaitForSeconds(0.5f);
        Require(tabs[NpcPanelTabType.Dialogue].isOn && Get<GameObject>(panel, "mNpcDialogueListGo").activeInHierarchy, "Pointer switches original dialogue page");
        Capture("panel-host-dialogue-empty.png");
        Click(tabs[NpcPanelTabType.StarGod].gameObject);
        VerifyTabSound();
        yield return new WaitForSeconds(0.5f);
        Require(tabs[NpcPanelTabType.StarGod].isOn && view.Content.GetComponentInChildren<NpcPanelStarGodContent>(), "Pointer switches original star-god page");
        Require(view.Content.GetComponentsInChildren<StarGodNpcItem>().Any(item => Get<NpcRosterItem>(item, "mNpcData") != null), "Original star-god pool populated");
        Capture("panel-host-star-god.png");
        Click(tabs[NpcPanelTabType.NpcList].gameObject);
        VerifyTabSound();
        yield return new WaitForSeconds(0.4f);
        Require(tabs[NpcPanelTabType.NpcList].isOn, "Pointer returns to original roster");
        var closeButton = view.Content.GetComponentsInChildren<CloseWindowBtn>().Select(row => row.myButton).Distinct().Single();
        Click(closeButton.gameObject);
        Require(view.Window.State == WorldPopupWindow.EState.Hiding, "Original close button starts actual hide clip");
        yield return new WaitForSeconds(0.7f);
        Require(!view.Content && ReferenceEquals(previousSkin, ProfessionAppearanceSkinClipBindings.Current), "Hide completion destroys window and restores scoped bindings");
        Require(destroyedWindows == 1 && removedObservers == 0,
            "Production window service observes actual native destruction and honors unsubscribe");
        Require(ReferenceEquals(previousSound, OfficialWwiseEvent.Host), "Hide completion restores underlying sound host");
        view.Open(OfficialPartnerPanelAssetCatalog.Load(), context, NpcPanelTabType.StarGod);
        context.Windows.ObserveDestroyed(view.Window, onDestroyed);
        yield return new WaitForSeconds(0.7f);
        Require(view.Content.GetComponentInChildren<NpcPanelStarGodContent>(), "Reopen original window on requested page");
        Capture("panel-host-reopened.png");
        view.Close(); yield return null;
        Require(destroyedWindows == 2 && removedObservers == 0,
            "Direct owner cleanup also emits the original window destruction event exactly once");
        limited.UnionWith(new[] { 57, 174 });
        view.Open(OfficialPartnerPanelAssetCatalog.Load(), context, NpcPanelTabType.StarGod);
        yield return new WaitForSeconds(0.7f);
        panel = view.Content.GetComponent<NpcPanel>();
        tabs = Get<MonoDictionary<NpcPanelTabType, WorldTabItem>>(panel, "mTabType2TabItemDict");
        Require(tabs[NpcPanelTabType.NpcList].isOn && !tabs[NpcPanelTabType.StarGod].gameObject.activeSelf &&
            !tabs[NpcPanelTabType.Dialogue].gameObject.activeSelf && !Get<GameObject>(panel, "mTabGroupGo").activeSelf,
            "Original function limits hide locked tabs and fall back from locked default");
        Capture("panel-host-function-limits.png");
        view.Close(); yield return null;
        services.Dispose(); icons.Dispose(); Object.Destroy(images); Object.Destroy(mergedImages);
    }
    // This fixture tests the real empty state of a new profile only. It does
    // not stand in for the production dialogue save or conversation service.
    sealed class NewProfileDialogueFixture : IPartnerDialogueUiServices
    {
        public bool MainPlayerAvailable => true;
        public string PlayerName => "名册窗口验证";
        public int DialogueStartNodeId => 0;
        public IReadOnlyDictionary<long, PartnerDialogueNpcData> Npcs { get; } = new Dictionary<long, PartnerDialogueNpcData>();
        public IReadOnlyDictionary<long, PartnerDialogueData> Dialogues { get; } = new Dictionary<long, PartnerDialogueData>();
        public event Action<PartnerDialogueData> DialogueAdded { add {} remove {} }
        public event Action<PartnerDialogueData> DialogueRemoved { add {} remove {} }
        public event Action<PartnerDialogueData> DialogueUpdated { add {} remove {} }
        public IReadOnlyList<int> GetNextNodes(int a, int b) => throw new InvalidOperationException("Not an empty-profile branch");
        public bool IsNodeUnlocked(int a, int b) => throw new InvalidOperationException("Not an empty-profile branch");
        public GameObject Instantiate(GameObject prefab, Transform parent, bool reset)
        {
            // EnsureNpcListItem intentionally retains one spare original row,
            // including when there are zero recorded conversations.
            Require(reset, "Original dialogue pool reset flag");
            return Object.Instantiate(prefab, parent, false);
        }
        public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => throw new InvalidOperationException("No dialogue NPC in this fixture");
        public void ShowError(int code) => throw new Exception("Unexpected dialogue error " + code);
        public void ShowDialogue(PartnerDialogueData dialogue) => throw new InvalidOperationException("No dialogue in this fixture");
    }
    void CloseWindow(GameObject window)
    {
        Require(window == view.Content, "Close exact original window");
        window.GetComponent<WorldPopupWindow>().Hide(() => view.Close());
    }
    void BindNativeTree(GameObject root) => tipDots.BindNativeTree(root);
    public void BindNpcTipDot(TipDotNodeLinker linker, int id) => tipDots.Bind(linker, id);
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
        Require(id == 3772241779, "Original common click event");
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
    void OnDestroy() => tipDots?.Dispose();
    void VerifyTabSound()
    {
        var clip = OfficialPartnerPanelAssetCatalog.Load().GetSound(680474747);
        Require(clip && clip.samples > 0, "Original Main_Tag PCM is packaged");
        var voice = Object.FindObjectsOfType<AudioSource>().Single(source => source.gameObject.name == "PartnerSound:680474747");
        Require(voice.clip == clip && voice.isPlaying && !voice.loop && voice.spatialBlend == 0,
            "Simulated original tab click plays its actual event PCM through the scoped host");
    }
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
        foreach (var tab in view.Content.GetComponentsInChildren<WorldTabItem>())
        {
            var animator = tab.GetComponent<Animator>();
            if (animator)
            {
                Require(animator.runtimeAnimatorController && animator.layerCount == 1, "Original tab override controller resolves");
                Require(animator.GetCurrentAnimatorClipInfo(0).Single().clip.name == (tab.isOn ? "TabBottomBarCell_On" : "TabBottomBarCell_Off"),
                    "Original selected/unselected tab animation matches state");
            }
        }
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
