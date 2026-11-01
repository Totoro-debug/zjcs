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
using Prop = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using LanguageManager = Zjcs.Official.World.NativeLabelUI.Framework.LanguageManager;

[InitializeOnLoad]
public static class PartnerDetailPropertiesValidation
{
    static PartnerDetailPropertiesValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerDetailProperties", false))
                new GameObject("NativePartnerDetailPropertyValidation").AddComponent<PartnerDetailPropertiesRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerDetailProperties", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerDetailProperties.Deadline", float.MaxValue))
                Finish(new TimeoutException("Native detail property timeout"));
        };
    }
    public static void Run()
    {
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerDetailProperties", true);
        SessionState.SetFloat("PartnerDetailProperties.Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception exception)
    {
        SessionState.SetBool("PartnerDetailProperties", false);
        if (exception != null) Debug.LogException(exception);
        else Debug.Log("PARTNER_DETAIL_PROPERTIES_PASSED: current friendship/talent property merge, native property rows and treasure additions, pointer pin/unpin with actual disk restore, friendship/brief/navigation ports and callback disposal. Popup contents and complete detail window/Player remain pending.");
        EditorApplication.Exit(exception == null ? 0 : 1);
    }
}

public sealed class PartnerDetailPropertiesRunner : MonoBehaviour
{
    [Serializable] private sealed class TextRow { public string key, text; }
    [Serializable] private sealed class TextCatalog { public TextRow[] texts; }
    private sealed class Presentation : PartnerFriendshipDialogRunner.Context, IOfflinePartnerDetailPresentation, IOfflinePartnerPresentation
    {
        public List<string> Messages = new List<string>();
        public List<ProgressionResult> Failures = new List<ProgressionResult>();
        public PartnerDetailLiteInfoCallback LastCallback;
        public int BriefCalls, TipCalls, NavigateCalls;
        public bool NavigationAvailable = true;
        public bool CanNavigateNpc(int id) => NavigationAvailable;
        public PartnerDetailNpcTarget CreateNpcTarget(int id) => new PartnerDetailNpcTarget { NpcClassId = id };
        public void GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback)
        {
            LastCallback = callback;
            // Controlled external lite-info endpoint; no invented production stats.
            callback.Invoke(new PartnerDetailLiteInfo { Level = 40, SubRank = 2,
                TargetData = new PartnerDetailNpcTarget { NpcClassId = target.NpcClassId, CreatePlanesRank = 1 } });
        }
        public void PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target)
        { Require(target.NpcLevel == 40 && target.NpcSubRank == 2 && target.CreatePlanesRank == 1, "original lite-info fields before popup"); BriefCalls++; }
        public void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
        { Require(!starGod, "ordinary original friendship route"); TipCalls++; initialize(null); }
        public void NavigateNpcPosition(int id, Action<PartnerDetailNavigateResult> complete)
        { NavigateCalls++; complete(new PartnerDetailNavigateResult { IsSuccess = false, TipMsg = "fixture-navigation-tip" }); }
        public bool IsNavigateTipValid(string tip) => tip == "fixture-navigation-tip";
        public void BroadcastNavigateTip(string tip) { Messages.Add(tip); }
        public string GetText(string key) => Text(key);
        public void SystemMessage(string text) { Messages.Add(text); }
        public void ShowProgressionResult(ProgressionResult result) { Failures.Add(result); }
        public void BindNpcTipDot(TipDotNodeLinker linker, int id) { }
        public void PostSound(AudioClip sound, GameObject owner) { }
        public void ShowFriendshipLevelUp(NpcRosterItem npc, long oldExp, int oldLevel) => throw new InvalidOperationException();
        public void ShowExclusiveGift(int id, Component owner) => throw new InvalidOperationException();
        public void ShowNpcDetails(List<NpcRosterItem> npcs, int index) => throw new InvalidOperationException();
        public void ShowError(int code) => throw new InvalidOperationException("Unexpected partner error " + code);
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
            catch (Exception exception) { PartnerDetailPropertiesValidation.Finish(exception); yield break; }
            yield return current;
        }
        PartnerDetailPropertiesValidation.Finish(errors == 0 ? null : new Exception("Unexpected native detail errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var assets = OfficialPartnerDetailPropertyAssetCatalog.LoadDefault();
        var friendshipAssets = OfficialPartnerFriendshipAssetCatalog.LoadDefault();
        var presentation = new Presentation { Texts = new Dictionary<string, string>() };
        foreach (var payload in new[] { assets.Texts, assets.Common.GiftTexts, friendshipAssets.PropertyTexts })
            foreach (var row in JsonUtility.FromJson<TextCatalog>(payload.text).texts) presentation.Texts[row.key] = row.text;
        var images = new OfflineProfessionImageResourceLoader(assets.Common.Images);
        var propertyBindings = new OfflineProfessionPropertyBindings(friendshipAssets.PropertyConfiguration, images, presentation,
            (owner, path, name, desc, control) => { presentation.Popups++; }, (owner, rank, desc, effect, first, second) => { throw new InvalidOperationException(); });
        var selected = OfficialPartnerAssetCatalog.LoadDefault();
        var properties = new OfflinePartnerFriendshipProperties(selected, propertyBindings, () => presentation.Limited);
        var slots = new ProfileSlotCatalog(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "detail-property-profiles"));
        string slot = slots.CreateSlot(); var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var config = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var giftPlayer = new OfflinePartnerGiftPlayer(session, selected, config, id => { throw new InvalidOperationException(); }, () => false);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "原生属性验证"; player.Level = 40; player.SubRank = 2;
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 7,
                FriendshipExp = config.Get(8401, 7).FriendshipExp });
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 10110, Level = 7,
                FriendshipExp = config.Get(10110, 7).FriendshipExp });
            foreach (int id in new[] { 10608, 10708, 10801 })
            {
                player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = id, Level = 2 });
                player.CharacterProperties.NpcTalentTrees.Add(new OfflineNpcTalentTree { NpcClassId = id,
                    ActiveNodeIds = selected.Rows("npc_talent_tree_npc_map_prop").Where(row => Convert.ToInt32(row["NpcClassId"]) == id)
                        .Select(row => Convert.ToInt32(row["NodeId"])).ToList() });
            }
        }).IsApplied, "actual isolated property state");
        giftPlayer.Reload();
        var source = new OfflinePartnerDetailPropertySource(session, selected, properties);
        Require(source.CalcPlayerNpcProps(-1).Count == 0, "unowned NPC contributes no property");
        foreach (int id in new[] { 10608, 10708, 10801 })
        {
            int propId = Convert.ToInt32(selected.Rows("npc").Single(row => Convert.ToInt32(row["Id"]) == id)["LevelPropId"]);
            var expected = properties.GetLevelProps(propId, 2).ToDictionary(row => row.Key, row => row.Value);
            foreach (var row in selected.Rows("npc_talent_tree_npc_map_prop").Where(row => Convert.ToInt32(row["NpcClassId"]) == id))
                foreach (var prop in source.GetProps(Convert.ToInt32(row["PropId"]), Convert.ToInt32(row["PropLevel"])))
                    if (((List<object>)row["PropTypes"]).Any(value => Convert.ToInt32(value) == (int)prop.Key))
                        expected[prop.Key] = (expected.TryGetValue(prop.Key, out long count) ? count : 0) + prop.Value;
            var actual = source.CalcPlayerNpcProps(id);
            Require(actual.Count == expected.Count && expected.All(row => actual[row.Key] == row.Value), "all selected activated talent rows merge after friendship: " + id);
        }
        string rootPath = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), "unity/Assets/Official");
        Func<string, string> read = name => File.ReadAllText(Path.Combine(rootPath, name));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), selected);
        var progression = new OfflineCharacterProgressionService(session, character, refreshDerivedProgress: false);
        var roster = new OfflinePartnerRosterUiServices(session, character, progression, images, presentation);
        var services = new OfflinePartnerDetailPropertyServices(selected, properties, source, roster,
            id => session.Save.Player.CharacterProperties.Treasures.Any(value => value.ClassId == id && value.Active), presentation);
        var npc = new OfflinePartnerDetailNpcData(session, giftPlayer.GetNpcByClassId(8401));
        var info = new PartnerDetailNpcInfo { Id = 8401,
            TreasureClassId = Convert.ToInt32(selected.Rows("npc").Single(row => Convert.ToInt32(row["Id"]) == 8401)["TreasureClassId"]), Friendship = config.GetNpcInfo(8401) };
        ProfessionPropertyBindings.Current = propertyBindings; LanguageManager.Initialize(presentation); OfficialWwiseEvent.Host = presentation;
        ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => Object.Instantiate(prefab, parent, keepWorld);
        WorldMultiLanguageText.Localize = key => presentation.Text(key);
        var font = Object.Instantiate((TMP_FontAsset)assets.Common.RegionFont);
        font.atlasTextures = font.atlasTextures.Select(texture => Object.Instantiate(texture)).ToArray();
        font.material = Object.Instantiate(font.material); font.material.mainTexture = font.atlasTexture;
        Set(font, "m_SourceFontFile", assets.Common.SourceFont); font.fallbackFontAssetTable.Clear(); WorldMultiLanguageText.LanguageFont = () => font;
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("OriginalPropertyCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(0.15f, 0.18f, 0.2f);
        target = new RenderTexture(768, 1600, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("OriginalPropertyCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1; canvas.scaleFactor = 768f / 1080;
        events = new GameObject("SimulatedPropertyPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var host = new GameObject("OriginalPropertyScope", typeof(RectTransform)); host.transform.SetParent(canvas.transform, false); host.SetActive(false);
        ((RectTransform)host.transform).sizeDelta = new Vector2(1080, 2300);
        var root = Object.Instantiate(assets.PropertyPrefab, host.transform, false); var panel = root.GetComponent<NpcDetailPanelPropContent>();
        Require(root.GetComponentsInChildren<Transform>(true).Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)) == 0, "original detail region scripts bound");
        panel.Configure(services); panel.Init(npc, info); root.SetActive(true); host.SetActive(true); yield return null; yield return null;
        Require(Get<WorldMultiLanguageText>(panel, "mLevelText").Text.Contains("7"), "actual saved level text");
        var visible = source.CalcPlayerNpcProps(8401).Where(row => row.Value != 0 && services.IsUnlockTravelNpcProp(row.Key)).ToList();
        Require(Get<Transform>(panel, "mSlot").GetComponentsInChildren<NpcFriendshipPropItemGroup>().Length == (visible.Count + 1) / 2, "original two-column property groups");
        Capture("detail-property-open.png");
        Click(Get<Button>(panel, "mCollectionBtn").gameObject); Require(!npc.IsCollected, "pin response deferred");
        while (!npc.IsCollected) yield return null;
        Require(OfflineMainlineProgressSession.OpenCharacter(slots, slot).Save.Player.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8401).IsCollected, "pin saved before native response");
        Require(Get<Image>(panel, "mCollectedImg").gameObject.activeSelf, "original pinned image"); Capture("detail-property-pinned.png");
        Click(Get<Button>(panel, "mCollectionBtn").gameObject); while (npc.IsCollected) yield return null;
        Require(Get<Image>(panel, "mUnCollectedImg").gameObject.activeSelf, "live saved view supports subsequent unpin");
        Require(!OfflineMainlineProgressSession.OpenCharacter(slots, slot).Save.Player.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8401).IsCollected, "unpin saved");
        npc = new OfflinePartnerDetailNpcData(session, giftPlayer.GetNpcByClassId(10110));
        info = new PartnerDetailNpcInfo { Id = 10110,
            TreasureClassId = Convert.ToInt32(selected.Rows("npc").Single(row => Convert.ToInt32(row["Id"]) == 10110)["TreasureClassId"]), Friendship = config.GetNpcInfo(10110) };
        Require(info.TreasureClassId > 0 && session.ApplyCharacterMutation(value => value.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure { ClassId = info.TreasureClassId, Active = true })).IsApplied, "actual configured active treasure state");
        panel.Init(npc, info); yield return null; yield return null;
        Require(Get<Transform>(panel, "mSlot").GetComponentsInChildren<NpcFriendshipPropItem>().Any(item => Get<WorldMultiLanguageText>(item, "mTxtPropValueAdd").gameObject.activeSelf), "original active treasure addition text");
        Capture("detail-property-treasure.png");
        Click(Get<Button>(panel, "mTipBtn").gameObject); Require(presentation.TipCalls == 1, "native friendship button route");
        Click(Get<Button>(panel, "mPropBtn").gameObject); Require(presentation.BriefCalls == 1, "native property brief route");
        var callback = presentation.LastCallback; Require(callback.Valid, "original callback stays reusable");
        Click(Get<Button>(panel, "mJumpBtn").gameObject); Require(presentation.NavigateCalls == 1 && presentation.Messages.Last() == "fixture-navigation-tip", "original failed-navigation valid tip");
        presentation.NavigationAvailable = false; panel.Init(npc, info); yield return null; Require(!Get<Button>(panel, "mJumpBtn").gameObject.activeSelf, "navigation visibility follows current result");
        host.SetActive(false); Object.Destroy(host); yield return null;
        Require(!callback.Valid && presentation.Failures.Count == 0 && errors == 0, "original callback release and lifecycle");
    }
    private void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)go.transform;
        var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == go, "unobscured original pointer target: " + go.name);
        ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG()); RenderTexture.active = null; Object.Destroy(image);
    }
    private static FieldInfo Field(object value, string name)
    { for (Type type = value.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; } throw new MissingFieldException(name); }
    private static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
    private static void Set(object value, string name, object field) => Field(value, name).SetValue(value, field);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native partner detail: " + message); }
}
