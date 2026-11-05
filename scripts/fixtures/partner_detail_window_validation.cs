using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
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
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerDetailWindowValidation
{
    const string Key = "PartnerDetailWindowValidation.Active";
    static PartnerDetailWindowValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Complete original partner detail validation").AddComponent<PartnerDetailWindowRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Complete detail validation timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        typeof(PartnerPanelHostValidation).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 450, 800 });
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) Debug.LogException(error); else Debug.Log("PARTNER_COMPLETE_DETAIL_WINDOW_OK");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

// All four pages share production data services. Unexercised secondary popup
// endpoints fail explicitly; this probe does not certify those popup flows.
public sealed class PartnerDetailWindowRunner : MonoBehaviour, IOfflinePartnerPresentation,
    IOfflinePartnerGiftPresentation, IOfflinePartnerDetailPresentation, IPartnerTreasureIconServices
{
    Camera cameraUi;
    RenderTexture target;
    EventSystem events;
    PartnerDetailWindowView view;
    OfflineMainlineProgressSession session;
    OfflinePartnerGiftPlayer gifts;
    OfflinePartnerGiftRuntimeBindings icons;
    OfflinePartnerTipDotRuntime tips;
    Func<string, string> localize;
    int errors, clicks, commits;
    IEnumerator Start()
    {
        Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var run = Check();
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception error) { PartnerDetailWindowValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerDetailWindowValidation.Finish(errors == 0 ? null : new Exception("Unexpected complete window errors: " + errors));
    }
    IEnumerator Check()
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while ((Screen.width != 450 || Screen.height != 800) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(Screen.width == 450 && Screen.height == 800, "Actual portrait viewport");
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("Complete detail camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.backgroundColor = Color.gray;
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("Complete detail viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("Simulated complete detail pointer", typeof(EventSystem)).GetComponent<EventSystem>();
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string Read(string name) => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", name));
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var giftConfig = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var character = OfficialCharacterCatalog.Parse(Read("Character/character-progression.json"), Read("Character/item-time-limits.json"),
            Read("CharacterCreation/wardrobe.json"), Read("CharacterCreation/wardrobe-rules.json"), Read("Character/character-overview.json"), catalog);
        var slots = new ProfileSlotCatalog(Path.Combine(work, "complete-detail-profiles"));
        string slot = slots.CreateSlot(); session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "完整详情验证"; player.Level = 1;
            player.SubRank = catalog.Rows("npc_friendship_level").Where(row => Convert.ToInt32(row["Id"]) == 8402).Max(row => Convert.ToInt32(row["SubRankLimit"]));
            player.CharacterProperties.NpcFriendships.Clear(); player.Inventory.Clear();
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 20, FriendshipExp = giftConfig.Get(8402, 20).FriendshipExp });
            OfflineInventoryOperations.Grant(player, 1130, 8, 10, 0, true);
        }).IsApplied, "Isolated real save");
        var progression = new OfflineCharacterProgressionService(session, character);
        var giftAssets = OfficialPartnerGiftPageAssetCatalog.LoadDefault();
        var friendshipAssets = OfficialPartnerFriendshipAssetCatalog.LoadDefault();
        var propertyAssets = OfficialPartnerDetailPropertyAssetCatalog.LoadDefault();
        var profileAssets = OfficialPartnerProfileAssetCatalog.Load();
        var achievementAssets = OfficialPartnerAchievementAssetCatalog.Load();
        var rosterAssets = OfficialPartnerRosterAssetCatalog.LoadDefault();
        var detailAssets = OfficialPartnerDetailWindowAssetCatalog.Load();
        var words = new Dictionary<string, string>();
        var language = new OfflinePartnerLocalizationService(propertyAssets.Texts, giftAssets.GiftTexts, giftAssets.RelationshipTexts,
            friendshipAssets.PropertyTexts, friendshipAssets.DialogTexts);
        foreach (var row in profileAssets.Texts) words[row.Key] = row.Value;
        foreach (var row in achievementAssets.Texts) words[row.Key] = row.Value;
        foreach (var row in detailAssets.Texts) words[row.key] = row.text;
        localize = key => words.TryGetValue(key, out var word) ? word : language.GetText(key, LanguageType.zh_CN);
        var wordDocument = new TextAsset(new JObject { ["texts"] = new JArray(words.Select(row => new JObject { ["key"] = row.Key, ["text"] = row.Value })) }.ToString());
        LanguageManager.Initialize(new OfflinePartnerLocalizationService(propertyAssets.Texts, giftAssets.GiftTexts, giftAssets.RelationshipTexts,
            friendshipAssets.PropertyTexts, friendshipAssets.DialogTexts, wordDocument));
        var images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        images.Entries = new[] { giftAssets.Images, propertyAssets.Common.Images, profileAssets.Images, achievementAssets.Images, rosterAssets.Images }
            .SelectMany(value => value.Entries).GroupBy(row => row.Path).Select(group => group.First()).ToArray();
        var loader = new OfflineProfessionImageResourceLoader(images);
        var properties = new OfflineProfessionPropertyBindings(friendshipAssets.PropertyConfiguration, loader, language,
            (_, __, ___, ____, _____) => throw new InvalidOperationException("Property explanation outside this probe"),
            (_, __, ___, ____, _____, ______) => throw new InvalidOperationException("Rank explanation outside this probe"));
        ProfessionPropertyBindings.Current = properties;
        var friendship = new OfflinePartnerFriendshipProperties(catalog, properties, () => false);
        var roster = new OfflinePartnerRosterUiServices(session, character, progression, loader, this);
        gifts = new OfflinePartnerGiftPlayer(session, catalog, giftConfig, id => new OfflinePartnerItemInfo(catalog, id), () => false);
        tips = new OfflinePartnerTipDotRuntime(session, catalog, giftConfig, progression); tips.Refresh();
        icons = new OfflinePartnerGiftRuntimeBindings(session, catalog, giftAssets, id => new OfflinePartnerItemInfo(catalog, id), localize, loader);
        var giftUi = new OfflinePartnerGiftUiServices(session, gifts, giftConfig, friendship, icons, loader, this);
        var rewardPresentation = new OfflinePartnerRewardUiPresentation(icons, profileAssets, achievementAssets,
            localize, key => words.ContainsKey(key) || language.ContainsKey(key), linker => tips.BindNativeTree(linker.gameObject),
            () => { commits++; gifts.Reload(); tips.Refresh(); }, ShowProgressionResult, SystemMessage);
        var profile = new OfflinePartnerProfileService(session, new PartnerProfileRules(catalog.Rows));
        var profileUi = new OfflinePartnerProfileUiServices(profile, rewardPresentation.Profile);
        var achievements = new OfflinePartnerAchievementService(session, new PartnerFriendshipAchievementRules(catalog.Rows));
        Require(achievements.RefreshFromSave().IsApplied || achievements.ReadCurrentPlayer() != null, "Original achievement progress");
        var achievementUi = new OfflinePartnerAchievementUiServices(achievements, catalog, () => false, rewardPresentation.Achievement);
        var actors = Resources.Load<PartnerNativeDetailActorAssets>("Partners/PartnerNativeDetailActorAssets");
        var presentationAssets = Resources.Load<PartnerNativeDetailPresentationAssets>("Partners/PartnerNativeDetailPresentationAssets");
        var config = new OfflinePartnerDetailConfiguration(catalog, giftConfig, presentationAssets.Regions.text);
        foreach (var row in catalog.Rows("npc")) Require(config.GetNpcInfo(Convert.ToInt32(row["Id"])) != null, "Every original NPC projection");
        Require(config.GetFullBodyImagePath(8402, out var livePath) == PartnerFullBodyImageType.Live && !string.IsNullOrEmpty(livePath), "Original Live before Static selection");
        var managerConfig = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
        using var manager = new AppearanceAssetManager(managerConfig, action => {
            Application.LowMemoryCallback callback = () => action(); Application.lowMemory += callback;
            return new Subscription(() => Application.lowMemory -= callback);
        });
        var textures = new OfflinePartnerUiTextureResources(actors, manager);
        var previousTextures = ProfessionAppearanceTextureBindings.Current; ProfessionAppearanceTextureBindings.Current = textures;
        var live = new OfflinePartnerDetailLiveServices(actors, config.GetNpcInfo,
            id => config.TryGetFullBodyImagePath(id, PartnerFullBodyImageType.Live, out var path) ? path : null);
        var adventure = new OfflinePartnerAdventureTipServices(presentationAssets, cameraUi, root => Destroy(root));
        var presentation = new OfflinePartnerDetailPresentationServices(config, actors, presentationAssets, textures, live, adventure,
            (_, __, ___, ____) => throw new InvalidOperationException("Adventure tip interaction outside this probe"));
        view = canvas.gameObject.AddComponent<PartnerDetailWindowView>();
        var npcUi = new OfflinePartnerDetailWindowServices(session, gifts, profileUi, config, Close);
        var detailProperties = new OfflinePartnerDetailPropertyServices(catalog, friendship,
            new OfflinePartnerDetailPropertySource(session, catalog, friendship), roster, _ => false, this);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.FindAssets("FZY4K SDF - dynamic t:TMP_FontAsset")
            .Select(AssetDatabase.GUIDToAssetPath).First(path => path.Contains("/Official/Partners/")));
        Set(font, "m_SourceFontFile", AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf")); font.fallbackFontAssetTable.Clear();
        var context = new PartnerDetailWindowContext { Npcs = npcUi, Properties = detailProperties, Gifts = giftUi,
            Profiles = profileUi, Achievements = achievementUi, Treasures = this, Presentation = presentation,
            Windows = new OfflinePartnerWindowService(Close, () => Close(view.Content)), Images = loader, Font = font,
            Localize = localize, BindNativeTree = tips.BindNativeTree, PostSound = PostSound };
        try
        {
            view.Open(detailAssets, context, new List<IPartnerDetailWindowNpcData> { npcUi.GetNpcByClassId(8402) }, 0);
            yield return null; yield return new WaitForSeconds(.8f);
            Require(view.Window.State == WorldPopupWindow.EState.Show && view.Panel.NpcData.NpcClassId == 8402, "Complete official window initialized");
            Require(view.Content.GetComponentsInChildren<MonoBehaviour>(true).All(component => component), "No missing original scripts");
            Require(view.Content.GetComponentsInChildren<PartnerNativeUiActor>().Length >= 1 && textures.LoadedResourceTextureCount > 0, "Complete page owns actual original actors and High textures");
            Capture("complete-detail-default.png");
            var tabs = Get<List<NpcDetailPanel.TabData>>(view.Panel, "mTabDatas");
            foreach (var type in new[] { NpcDetailPanel.TabType.Gift, NpcDetailPanel.TabType.Profile, NpcDetailPanel.TabType.Achievement, NpcDetailPanel.TabType.Detail })
            {
                Click(tabs.Single(row => row.TabType == type).TabItem.gameObject);
                Require(Object.FindObjectsOfType<AudioSource>().Any(source => source.gameObject.name == "PartnerSound:680474747" && source.isPlaying && source.clip == OfficialPartnerPanelAssetCatalog.Load().GetSound(680474747)), "Original tab event plays actual PCM");
                yield return null; yield return new WaitForSeconds(.35f);
                Require(Get<WorldStateSwitcher<NpcDetailPanel.TabType>>(view.Panel, "mStateSwitcher").State == type, "Pointer changes original tab " + type);
                Capture("complete-detail-" + type + ".png");
                if (type == NpcDetailPanel.TabType.Gift)
                {
                    var page = view.Content.GetComponentInChildren<NpcDetailPanelGiftContent>();
                    var giftIcons = Get<Dictionary<int, ItemIcon>>(page, "mGiftItemIconDict");
                    Require(giftIcons.ContainsKey(1130), "Full window uses actual available gift inventory");
                    long beforeExp = gifts.GetNpcByClassId(8402).FriendShip;
                    int increment = giftConfig.GetAllExpByDict(new Dictionary<int, long> { [1130] = 1 }, giftConfig.GetNpcInfo(8402).GiftPreferenceDict);
                    Click(giftIcons[1130].gameObject);
                    Require(Get<Dictionary<int, long>>(page, "mSelectedGiftDict")[1130] == 1, "Original gift icon pointer selects one item");
                    Click(Get<Button>(page, "mGiveGiftBtn").gameObject);
                    float saveDeadline = Time.realtimeSinceStartup + 5;
                    while (gifts.GetItemAmount(1130) == 8 && Time.realtimeSinceStartup < saveDeadline) yield return null;
                    Require(gifts.GetItemAmount(1130) == 7 && gifts.GetNpcByClassId(8402).FriendShip == beforeExp + increment, "Complete-window gift commits original preferred exp");
                    var reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                    Require(reloaded.InventoryCount(1130) == 7 && reloaded.Save.Player.CharacterProperties.NpcFriendships.Single().FriendshipExp == beforeExp + increment, "Gift state is durable before UI success");
                    Capture("complete-detail-gift-committed.png");
                }
                if (type == NpcDetailPanel.TabType.Profile)
                {
                    var page = view.Content.GetComponentInChildren<NpcDetailPanelProfileContent>();
                    Click(Get<Button>(page, "mUnlockBtn").gameObject); yield return null;
                    Require(commits == 1 && profile.ReceivedProfileRewards(8402).SequenceEqual(new[] { 1 }) && session.InventoryCount(107) == 3, "Original complete-window profile pointer saves reward and chapter together");
                    Require(Get<WorldMultiLanguageText>(page, "mContentText").Text == profileAssets.Texts["npc_profile_Content_8402_1"], "Original unlocked profile text");
                    Capture("complete-detail-profile-committed.png");
                }
                if (type == NpcDetailPanel.TabType.Achievement)
                {
                    var rows = view.Content.GetComponentsInChildren<PartnerAchievementPanelItem>().Where(row => row.gameObject.activeInHierarchy).ToArray();
                    Require(rows.Length == 5 && rows[0].StateSwitcher.State == PartnerFriendshipAchievementRules.AchievementState.Completed, "Original five rows and eligible reward");
                    Click(rows[0].GetRewardBtn.gameObject); yield return null;
                    Require(commits == 2 && view.Content.GetComponentsInChildren<PartnerAchievementPanelItem>().Where(row => row.gameObject.activeInHierarchy).All(row => row.StateSwitcher.State == PartnerFriendshipAchievementRules.AchievementState.Processing), "Original achievement request range removes all received eligible rows");
                    Require(OfflineMainlineProgressSession.OpenCharacter(slots, slot).InventoryCount(100) == session.InventoryCount(100) && session.InventoryCount(100) > 0, "Achievement inventory saved before original callback");
                    Capture("complete-detail-achievement-committed.png");
                }
            }
            Click(Get<Button>(view.Panel, "mFullViewBtn").gameObject); yield return null;
            Require(Get<WorldStateSwitcher<bool>>(view.Panel, "mFullViewStates").State, "Original full view pointer");
            Capture("complete-detail-fullview.png");
            Click(Get<Button>(view.Panel, "mExitFullViewBtn").gameObject); yield return null;
            Require(!Get<WorldStateSwitcher<bool>>(view.Panel, "mFullViewStates").State, "Original exit full view pointer");
            view.Close(); yield return null; manager.Update();
            Require(textures.LoadedResourceTextureCount == 0, "Closing complete window releases all original High textures");
            view.Open(detailAssets, context, new List<IPartnerDetailWindowNpcData> { npcUi.GetNpcByClassId(8402) }, 0);
            yield return new WaitForSeconds(.8f);
            Require(textures.LoadedResourceTextureCount > 0, "Complete window reopens with fresh textures");
            view.Close(); yield return null;
            Require(textures.LoadedResourceTextureCount == 0, "Repeated close releases texture references");
            Debug.Log("Complete native partner detail: four actual pointer tabs, full-view, real save providers, original actors, reopen and cleanup; clicks=" + clicks + ", commits=" + commits);
        }
        finally { view.Close(); ProfessionAppearanceTextureBindings.Current = previousTextures; tips.Dispose(); icons.Dispose(); }
    }
    public void BindNpcTipDot(TipDotNodeLinker linker, int id) => tips.Bind(linker, id);
    public void ConfigureGiftIcon(ItemIconNpcGift icon) => icons.BindGiftIcon(icon);
    public void PostSound(AudioClip clip, GameObject owner)
    { Require(clip && clip.samples > 0, "Actual original PCM"); var source = owner.GetComponent<AudioSource>(); if (!source) source = owner.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = clip; source.Play(); }
    public string GetText(string key) => localize(key);
    public void SystemMessage(string text) => Debug.Log("Original partner message: " + text);
    public object Compact(long value) => CultureNumberArg.Compact(value);
    public void Close(GameObject window) { Require(window == view.Content, "Exact original detail close"); view.Close(); }
    public void ShowProgressionResult(ProgressionResult result) => throw new Exception("Unexpected local transaction failure: " + result.Message);
    public void ShowError(int code) => throw new Exception("Unexpected roster error " + code);
    public void ShowError(int code, object args, bool flag) => throw new Exception("Unexpected gift error " + code);
    public void ShowFriendshipLevelUp(NpcRosterItem npc, long value, int level) => throw new InvalidOperationException("Level popup outside probe");
    public void ShowExclusiveGift(int id, Component owner) => throw new InvalidOperationException("Exclusive gift outside probe");
    public void ShowNpcDetails(List<NpcRosterItem> npcs, int index) => throw new InvalidOperationException("Roster entry outside probe");
    public void PopupInput(Action<IPartnerGiftInputDialog> initialize) => throw new InvalidOperationException("Input popup outside probe");
    public void PopupConfirm(Action<IPartnerGiftConfirmDialog> initialize) => throw new InvalidOperationException("Confirm popup outside probe");
    public void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize) => throw new InvalidOperationException("Friendship popup outside probe");
    public bool CanNavigateNpc(int id) => false;
    public PartnerDetailNpcTarget CreateNpcTarget(int id) => new PartnerDetailNpcTarget { NpcClassId = id };
    public void GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback) => throw new InvalidOperationException("Brief popup outside probe");
    public void PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target) => throw new InvalidOperationException("Brief popup outside probe");
    public void NavigateNpcPosition(int id, Action<PartnerDetailNavigateResult> complete) => throw new InvalidOperationException("Navigation outside probe");
    public bool IsNavigateTipValid(string tip) => !string.IsNullOrEmpty(tip);
    public void BroadcastNavigateTip(string tip) => SystemMessage(tip);
    public IProfessionItemInfo GetItemInfo(int id) => throw new InvalidOperationException("8402 has no NPC treasure");
    public bool IsTreasureActive(int id) => throw new InvalidOperationException("8402 has no NPC treasure");
    public void SetUIGray(GameObject target, bool gray) => throw new InvalidOperationException("8402 has no NPC treasure");
    public void PopupTreasureDetail(int id) => throw new InvalidOperationException("8402 has no NPC treasure");
    void Click(GameObject button)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
        foreach (float y in new[] { .5f, .25f, .75f }) foreach (float x in new[] { .5f, .25f, .75f })
        {
            var point = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y));
            var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(point)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
            if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button) continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler); clicks++; return;
        }
        throw new Exception("Original complete detail target occluded: " + button.name);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false); frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); frame.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), frame.EncodeToPNG());
        RenderTexture.active = previous; Destroy(frame);
    }
    static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    static void Set(object instance, string field, object value) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    sealed class Subscription : IDisposable { Action release; public Subscription(Action value) { release = value; } public void Dispose() { var action = release; release = null; action?.Invoke(); } }
}
