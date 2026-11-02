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
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerDialoguePlaybackValidation
{
    const string Key = "PartnerDialoguePlaybackValidation.Active";
    static PartnerDialoguePlaybackValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original conversation option validation").AddComponent<PartnerDialoguePlaybackRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original conversation option input timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var editor = typeof(EditorWindow).Assembly;
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(editor.GetType("UnityEditor.GameViewSizes")).GetProperty("instance").GetValue(null);
        var group = sizes.GetType().GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
        var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), 450, 800, "Partner dialogue input" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var game = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"), false, "Game", false);
        game.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(game,
            (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1);
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "playback-option-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() ?? "" }, true));
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_PLAYBACK_OPTIONS_PASSED: exact native prefab/row import, actual original branch labels, simulated pointer acknowledgment and disk state, stable player/history projection, stale option rejection, original option hiding and PCM. Full transcript/avatar/award/navigation presentation is not exercised.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] private sealed class Result { public bool success; public string error; }
}

public sealed class PartnerDialoguePlaybackRunner : MonoBehaviour, IOfflinePartnerDialoguePresentation,
    IOfflinePartnerDialoguePlaybackPresentation, IPartnerDialogueAwardServices,
    IProfessionSoundHost, IProfessionSoundObjectHost
{
    private sealed class Mouse : BaseInput
    {
        internal Vector2 Point; internal bool Down, Held, Up;
        public override bool mousePresent => true;
        public override Vector2 mousePosition => Point;
        public override Vector2 mouseScrollDelta => Vector2.zero;
        public override bool GetMouseButtonDown(int button) => button == 0 && Down;
        public override bool GetMouseButtonUp(int button) => button == 0 && Up;
        public override bool GetMouseButton(int button) => button == 0 && Held;
        public override float GetAxisRaw(string name) => 0;
        public override bool GetButtonDown(string name) => false;
    }
    private int errors, failures, sounds, updates;
    private string work;
    private Camera cameraUi;
    private RenderTexture target;
    private EventSystem events;
    private Mouse mouse;
    private OfflineMainlineProgressSession session;
    private OfflinePartnerDialogueUiServices views;
    private OfflinePartnerDialoguePlaybackServices services;
    private readonly HashSet<Func<object, bool>> awardHandlers = new HashSet<Func<object, bool>>();
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var check = Check();
        while (true)
        {
            object value;
            try { if (!check.MoveNext()) break; value = check.Current; }
            catch (Exception error) { PartnerDialoguePlaybackValidation.Finish(error); yield break; }
            yield return value;
        }
        PartnerDialoguePlaybackValidation.Finish(errors == 0 ? null : new Exception("Runtime errors: " + errors));
    }
    void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    IEnumerator Check()
    {
        work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var partner = OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json"));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), partner);
        var conditions = new PartnerConversationConditionRules(partner.Conditions, partner.Rows("conditions"));
        var config = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(conditions.Bind);
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles")); string slot = slots.CreateSlot();
        session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var player = new OfflinePartnerConversationPlayer(session, character, config,
            new OfflinePartnerConversationWorld(session, OfficialPartnerConversationWorldAssetCatalog.LoadDefault()),
            () => new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), _ => {});
        long identity = 8000;
        var transactions = new OfflinePartnerConversationService(session, character, player, 7001, () => ++identity, () => false, 8, () => {});
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "原通话按钮验证"; value.Level = 1;
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 40 });
        }).IsApplied && transactions.RefreshOnPanelOpen().IsApplied, "Create actual isolated conversation");
        long dialogueId = session.Save.Player.CharacterProperties.NpcConversations.Records.Single().DialogueId;
        // Explicit setup at the first real branch. Auto-playback is outside this check.
        Require(transactions.Acknowledge(dialogueId, 2).IsApplied, "Prepare actual original first option node");
        views = new OfflinePartnerDialogueUiServices(session, player, this);
        services = new OfflinePartnerDialoguePlaybackServices(views, transactions, config, this);
        views.DialogueUpdated += _ => updates++;
        var stable = views.Dialogues[dialogueId]; var history = stable.DialogueNodes;
        Require(stable.PlayerId == 7001 && history.SequenceEqual(new[] { 1, 2 }), "Original player identity/history projection");
        var assets = OfficialPartnerDialoguePlaybackAssetCatalog.LoadDefault();
        Require(assets.PanelPrefab.GetComponentsInChildren<Transform>(true).All(value => value.GetComponents<Component>().All(component => component)), "All original main-window components resolve");
        var loop = assets.PanelPrefab.GetComponentInChildren<LoopVerticalScrollRect>(true);
        var row = loop.DefaultPrefabSource.Prefab;
        Require(row && row.GetComponent<NpcDialoguePanelItem>() && row.GetComponentsInChildren<Transform>(true)
            .All(value => value.GetComponents<Component>().All(component => component)), "Original external transcript row closure resolves");
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.FindAssets("FZY4K SDF - dynamic t:TMP_FontAsset")
            .Select(AssetDatabase.GUIDToAssetPath).First(path => path.Contains("/Official/Partners/")));
        font.fallbackFontAssetTable.Clear(); font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        typeof(TMP_FontAsset).GetField("m_SourceFontFile", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(font, AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        Require(font.sourceFontFile && font.TryAddCharacters("通讯录知道了我明白了", out string missingFontCharacters),
            "Original language font can populate actual dialogue glyphs: " + font.sourceFontFile);
        WorldMultiLanguageText.LanguageFont = () => font;
        WorldMultiLanguageText.Localize = assets.CreateLocalizer(OfficialPartnerRosterAssetCatalog.LoadDefault().CreateLocalizer());
        OfficialWwiseEvent.Host = this;
        cameraUi = new GameObject("Original conversation camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = Color.black;
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("Original conversation viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var gate = new GameObject("Inactive original prefab binding", typeof(RectTransform)); gate.transform.SetParent(canvas.transform, false); gate.SetActive(false);
        var rect = (RectTransform)gate.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        var native = Object.Instantiate(assets.PanelPrefab, gate.transform, false);
        // The complete transcript controller remains uninited; the exact native
        // option group is initialized directly for this bounded input check.
        // Match NpcDialoguePanel.Init / AutoMoveToNext before showing choices.
        ((Button)typeof(NpcDialoguePanel).GetField("mFastEndTypeWriterBtn", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(native.GetComponent<NpcDialoguePanel>())).gameObject.SetActive(false);
        foreach (var handler in native.GetComponentsInChildren<NpcDialogueAwardHandler>(true)) handler.Configure(this);
        int optionSounds = 0;
        foreach (var sound in native.GetComponentsInChildren<WorldButtonSound>(true))
            sound.Post = (clip, emitter) => { PlayClip(clip, emitter); optionSounds++; };
        var option = native.GetComponentInChildren<NpcDialoguePanelOption>(true);
        option.Configure(services);
        foreach (var item in native.GetComponentsInChildren<NpcDialoguePanelOptionItem>(true)) item.Configure(services);
        option.gameObject.SetActive(false); option.Init(dialogueId, config.Get(2021, 2)); option.gameObject.SetActive(true);
        native.SetActive(true); gate.SetActive(true);
        native.GetComponent<WorldPopupWindow>().Show();
        events = new GameObject("Simulated conversation pointer", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
        mouse = events.gameObject.AddComponent<Mouse>(); var module = events.GetComponent<StandaloneInputModule>(); module.inputOverride = mouse; module.forceModuleActive = true;
        yield return new WaitForSeconds(.8f);
        Debug.Log("Native sound lifecycle: " + string.Join(",", native.GetComponentsInChildren<WorldButtonSound>(true).Select(sound => sound.name
            + ":enabled=" + sound.enabled + ":active=" + sound.gameObject.activeInHierarchy + ":post=" + (sound.Post != null)
            + ":started=" + (typeof(WorldButtonSound).GetField("mBtn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sound) != null))));
        Require(awardHandlers.Count == 1, "Original award router subscribes on enable");
        var options = option.GetComponentsInChildren<NpcDialoguePanelOptionItem>(false);
        Require(options.Length == 2 && options[0].GetComponentInChildren<WorldMultiLanguageText>().myKey == "npc_dialogue_2021_3_option_text"
            && options[1].GetComponentInChildren<WorldMultiLanguageText>().myKey == "npc_dialogue_2021_4_option_text", "Original branch order and language keys");
        Capture("native-options-before.png");
        string progressPath = slots.GetProgressPath(slot), temporary = progressPath + ".tmp";
        byte[] original = File.ReadAllBytes(progressPath);
        Require(!File.Exists(temporary) && !Directory.Exists(temporary), "Own transaction temporary path is unused");
        Directory.CreateDirectory(temporary);
        var click = Click(options[0].GetComponentInChildren<Button>());
        try { while (click.MoveNext()) yield return click.Current; }
        finally { Directory.Delete(temporary); }
        Require(failures == 1 && updates == 0 && history.SequenceEqual(new[] { 1, 2 }) && !option.gameObject.activeSelf
            && File.ReadAllBytes(progressPath).SequenceEqual(original), "Native option disk failure returns original error completion without publishing mutation");
        option.Init(dialogueId, config.Get(2021, 2)); option.gameObject.SetActive(true); yield return null;
        click = Click(option.GetComponentsInChildren<NpcDialoguePanelOptionItem>(false)[0].GetComponentInChildren<Button>());
        while (click.MoveNext()) yield return click.Current;
        Require(stable.CurrentNode == 3 && ReferenceEquals(stable, views.Dialogues[dialogueId]) && ReferenceEquals(history, stable.DialogueNodes)
            && history.SequenceEqual(new[] { 1, 2, 3 }) && !option.gameObject.activeSelf && updates == 1 && failures == 1,
            "Native simulated input commits legal branch, updates stable history and hides original options");
        session.Reload(); Require(session.Save.Player.CharacterProperties.NpcConversations.Records.Single().CurrentNode == 3, "Native option survives disk reload");
        byte[] before = File.ReadAllBytes(slots.GetProgressPath(slot));
        option.Init(dialogueId, config.Get(2021, 2)); option.gameObject.SetActive(true);
        yield return null;
        click = Click(option.GetComponentsInChildren<NpcDialoguePanelOptionItem>(false)[1].GetComponentInChildren<Button>());
        while (click.MoveNext()) yield return click.Current;
        Require(failures == 2 && updates == 1 && stable.CurrentNode == 3 && !option.gameObject.activeSelf
            && File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(before), "Stale original option rejects without saved history/reward changes");
        views.Reload(); Require(ReferenceEquals(history, stable.DialogueNodes) && history.SequenceEqual(new[] { 1, 2, 3 }), "Reload retains original view/list identity");
        Capture("native-options-after.png");
        gate.SetActive(false); yield return null;
        Require(awardHandlers.Count == 0 && optionSounds == 3, "Original award routing releases and actual click PCM plays: handlers="
            + awardHandlers.Count + " clicks=" + optionSounds + " bindings=" + string.Join(",", native.GetComponentsInChildren<WorldButtonSound>(true).Select(sound => sound.name + ":" + (sound.ClickEvent ? sound.ClickEvent.name : "none"))));
    }
    IEnumerator Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        mouse.Point = RectTransformUtility.WorldToScreenPoint(cameraUi, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
        Debug.Log("Original option pointer=" + mouse.Point + " screen=" + Screen.width + "x" + Screen.height + " target=" + button.name
            + " hits=" + string.Join(",", hits.Take(10).Select(hit => hit.gameObject.name + ":" + (hit.gameObject.GetComponentInParent<Button>()?.name ?? "none"))));
        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Original option is unobscured at simulated pointer");
        mouse.Down = true; mouse.Held = true; yield return null;
        mouse.Down = false; mouse.Held = false; mouse.Up = true; yield return null;
        mouse.Up = false; yield return new WaitForSeconds(.15f);
    }
    void Capture(string name)
    {
        cameraUi.Render(); var old = RenderTexture.active; RenderTexture.active = target;
        var frame = new Texture2D(450, 800, TextureFormat.RGB24, false); frame.ReadPixels(new Rect(0, 0, 450, 800), 0, 0); frame.Apply();
        File.WriteAllBytes(Path.Combine(work, name), frame.EncodeToPNG()); Object.Destroy(frame); RenderTexture.active = old;
    }
    public string GetNpcNameKey(int id) => throw new NotSupportedException("Transcript title is outside this option check");
    public void InitializeActorIcon(Component icon, long id, PartnerDialogueSpokesMan speaker) => throw new NotSupportedException("Actor presentation is outside this option check");
    public ItemIcon GetRewardIcon(int id, Transform parent) => throw new NotSupportedException("Reward presentation is outside this option check");
    public void RecycleRewardIcon(ItemIcon icon) => throw new NotSupportedException();
    public void ShowAcknowledgeFailure(ProgressionResult result, bool byPlayer) { Require(!result.IsApplied && byPlayer, "Original manual option rejection"); failures++; }
    public GameObject Instantiate(GameObject prefab, Transform parent, bool reset) => throw new NotSupportedException();
    public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => throw new NotSupportedException();
    public void ShowError(int code) => throw new NotSupportedException();
    public void ShowDialogue(PartnerDialogueData data) => throw new NotSupportedException();
    public void RegisterHandler(Func<object, bool> handler) { Require(awardHandlers.Add(handler), "Single original award subscription"); }
    public void RemoveHandler(Func<object, bool> handler) { Require(awardHandlers.Remove(handler), "Exact original award unsubscription"); }
    public bool IsConversationAward(object message) => throw new NotSupportedException();
    public void PopupAward(object message) => throw new NotSupportedException();
    public uint PostEvent(uint id, GameObject emitter)
    {
        var clip = OfficialPartnerDialoguePlaybackAssetCatalog.LoadDefault().GetSound(id);
        PlayClip(clip, emitter); return (uint)sounds;
    }
    void PlayClip(AudioClip clip, GameObject emitter)
    {
        Require(clip && clip.samples > 0 && emitter, "Original option event PCM");
        // OnSelect hides the original option before its later sound listener.
        // Keep this local audio emitter alive for the complete official PCM.
        var source = cameraUi.GetComponent<AudioSource>();
        if (!source) source = cameraUi.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.PlayOneShot(clip); sounds++;
    }
    public bool IsInitialized => true; public float DefaultScalingFactor => 1;
    public int RegisterGameObject(GameObject emitter, string name) => 1;
    public void SetObjectPosition(GameObject emitter, Vector3 p, Vector3 f, Vector3 u) { }
    public void InitializeEmptyEnvironment(GameObject emitter, Collider source) { }
    public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position) { }
    public void SetDefaultListeners(GameObject emitter) { }
    public void SetScalingFactor(GameObject emitter, float factor) { }
    public void UnregisterGameObject(GameObject emitter) { }
}
