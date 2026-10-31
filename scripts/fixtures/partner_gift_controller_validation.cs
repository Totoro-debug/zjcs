// Native hierarchy/controller integration. Controlled levels, inventory, popup
// contents, palette and responses are fixture endpoints, not production rules.
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
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using Prop = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Quality = Zjcs.Official.ProfessionUI.Quality;

[InitializeOnLoad]
public static class PartnerGiftControllerValidation
{
    static PartnerGiftControllerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerGiftController", false))
                new GameObject("OriginalGiftControllerFixture").AddComponent<PartnerGiftControllerRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool("PartnerGiftController", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("PartnerGiftController.Deadline", float.MaxValue))
                Finish(new TimeoutException("Native gift controller timeout"));
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerGiftController", true);
        SessionState.SetFloat("PartnerGiftController.Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerGiftController", false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_GIFT_CONTROLLER_PASSED: actual gift subtree, pointer selection/reduction/reset/auto/give, rank/max/overflow, title callbacks and lifecycle. Controlled local/popup endpoints; production host/save not covered.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerGiftControllerRunner : MonoBehaviour
{
    [Serializable] private sealed class TextRow { public string key, text; }
    [Serializable] private sealed class TextCatalog { public TextRow[] texts; }
    [Serializable] private sealed class GiftRow { public int ClassId, Friendship; }
    [Serializable] private sealed class Rules { public GiftRow[] gifts; }
    private sealed class NpcData : IPartnerGiftNpcData
    {
        public int NpcClassId => 8401; public int Level = 1; public long Exp; public string Tag = "";
        public int FriendShipLevel => Level; public long FriendShip => Exp; public string CustomFriendShipTag => Tag;
    }
    private sealed class ItemData : IProfessionItemData
    {
        public IProfessionItemInfo Info { get; set; } public int Id => Info.ClassId; public long Count { get; set; }
        public Quality GetQuality() => Info.Quality; public bool CanExpire() => false;
    }
    private sealed class SpriteHandle : IProfessionAssetOperation<Sprite>
    {
        public SpriteHandle(Sprite sprite) { Result = sprite; } public bool IsValid => true; public int Status => 2;
        public Sprite Result { get; private set; } public event Action<IProfessionAssetOperation<Sprite>> Completed { add { value(this); } remove { } }
        public void Dispose() { Result = null; }
    }
    private sealed class Input : IPartnerGiftInputDialog
    {
        public GameObject GameObject { get; } = new GameObject("FixtureInputEndpoint"); public Action<string> Submit; public int Masks;
        public void Init(string title, string placeholder, Action<string> callback) { Require(title == "ui_npc_change_tag" && placeholder == "ui_npc_input_tag", "original input keys"); Submit = callback; }
        public void SetNpcFriendshipTagModerationClickMask() { Masks++; }
    }
    private sealed class Confirm : IPartnerGiftConfirmDialog
    {
        public Action Accept; public void Init(string key, Action callback) { Require(key == "ui_npc_detail_give_gift_overflow_tip", "original overflow key"); Accept = callback; }
    }
    private sealed class Context : PartnerFriendshipDialogRunner.Context, IPartnerGiftUiServices, IPartnerGiftPlayer, IPartnerGiftLevels, IPartnerGiftExperience, IProfessionUpgradeBindings, IProfessionUpgradePlayer, IProfessionTipDotSystem
    {
        public PartnerGiftPreferenceProbe Icons; public NpcData Data = new NpcData(); public PartnerGiftNpcInfo Npc;
        public Dictionary<int, PartnerGiftInfo> GiftInfos; public List<ItemData> Inventory = new List<ItemData>();
        public List<string> Messages = new List<string>(); public int Gives, TagWrites, Closes, LastError, RankMax = 3;
        public bool AddressLimited, CharactersValid = true; public int TagLegal; public Action PendingGive; public Action<string> PendingTag;
        public Input Input; public Confirm Confirm; public Sprite TagSprite; public int TipCalls;
        public IPartnerGiftPlayer Player => this; public IPartnerGiftLevels Levels => this; public IPartnerGiftExperience Gifts => this;
        public IProfessionUpgradeBindings PlayerBindings => this; public double NpcGiftDefaultPreferenceFactor => 1;
        public int CanCustomTagFriendShipLevel => 1;
        public PartnerGiftNpcInfo GetNpcInfo(int id) { Require(id == 8401, "NPC identity"); return Npc; }
        public PartnerGiftInfo GetGiftInfo(IProfessionItemInfo item) => GiftInfos[item.ClassId];
        public void ConfigureGiftIcon(ItemIconNpcGift icon)
        {
            Icons.BindIcon(icon);
            foreach (var progress in icon.GetComponentsInChildren<WorldItemProgress>(true)) progress.Compact = count => count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public bool IsNpcGift(IProfessionItemInfo item) => true;
        public bool IsUnlockTravelNpcProp(Prop prop) => true;
        public PartnerGiftTagInfo GetTagInfoByRoleTypeAndLevel(int role, int level) => new PartnerGiftTagInfo { ClassId = 1, TagBg = "UI/NPCFavor/Tag/Level_1" };
        public string GetNpcFriendshipTagNameKey(int id) => "fixture_title";
        public IProfessionAssetOperation<Sprite> LoadSprite(string path) { Require(path == "UI/NPCFavor/Tag/Level_1", "original tag path"); return new SpriteHandle(TagSprite); }
        public bool IsAllCharacterValid(string input) => CharactersValid;
        public int CheckNpcFriendshipTagLegal(string input) => TagLegal;
        public void ShowError(int error, object args, bool flag) { Require(args == null && !flag, "original error arguments"); LastError = error; }
        public string GetText(string key) => Text(key);
        public void SystemMessage(string text) { Messages.Add(text); }
        public void PopupInput(Action<IPartnerGiftInputDialog> callback) { Input = new Input(); callback(Input); }
        public void PopupConfirm(Action<IPartnerGiftConfirmDialog> callback) { Confirm = new Confirm(); callback(Confirm); }
        public void PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> callback) { TipCalls++; callback(null); }
        public object Compact(long count) => count.ToString();
        public void Close(GameObject window) { Require(window == Input.GameObject, "input close identity"); Closes++; }
        public void SetCustomTag(int id, string input, Action<string> success) { TagWrites++; PendingTag = success; }
        public void GiveGift(int id, Dictionary<int, long> gifts, Action success) { Require(gifts.Count > 0 && gifts.Values.All(v => v > 0), "positive original gift request"); Gives++; PendingGive = success; }
        public bool IsAddressListFunctionLimited => AddressLimited;
        public IEnumerable<KeyValuePair<long, IPartnerGiftNpcData>> Npcs => new[] { new KeyValuePair<long, IPartnerGiftNpcData>(1, Data) };
        public event Action<int, int> OnAddNewNpc;
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        public event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        public event Action<int> OnNpcFriendshipUpdate;
        public int Subscribers => new Delegate[] { OnAddNewNpc, OnItemCountUpdate, OnNpcFriendshipLevelUpdate, OnNpcFriendshipUpdate }.Sum(d => d?.GetInvocationList().Length ?? 0);
        public IPartnerGiftNpcData GetNpcByClassId(int id) => Data;
        public IEnumerable<IProfessionItemData> GetAllCanSendToNpcGift(int id) => Inventory.Where(item => item.Count > 0);
        public long GetItemAmount(int id) => Inventory.FirstOrDefault(item => item.Id == id)?.Count ?? 0;
        public void SetNpcFriendshipTag(int id, string tag) { Data.Tag = tag; }
        public int GetMaxLevel(int id) => 3;
        public int GetMaxSubRankLevel(int id, int rank) => RankMax;
        public int GetFriendshipLevelByExp(int id, long exp, int rank) => Math.Min(RankMax, exp >= 10000 ? 3 : exp >= 5000 ? 2 : 1);
        public PartnerGiftLevelInfo GetMaxLevelInfo(int id) => Get(id, 3);
        public IReadOnlyDictionary<int, PartnerGiftLevelInfo> Get(int id) => Enumerable.Range(1, 3).ToDictionary(level => level, level => Get(id, level));
        public PartnerGiftLevelInfo Get(int id, int level) => new PartnerGiftLevelInfo { FriendshipExp = (level - 1) * 5000 };
        public int GetAllExpByDict(Dictionary<int, long> gifts, Dictionary<int, double> preferences)
        { int result = 0; foreach (var item in gifts) result += (int)Math.Round((double)(GiftInfos[item.Key].Friendship * item.Value) * (preferences.TryGetValue(item.Key, out var factor) ? factor : 1)); return result; }
        public void RaiseInventory() { OnItemCountUpdate?.Invoke(Inventory[0], Inventory[0].Count, null); }
        public void RaiseFriendship() { OnNpcFriendshipUpdate?.Invoke(8401); }
        public IProfessionUpgradePlayer MainPlayer => this; public bool ManagerAvailable => true; public SafeAction OnPlayerUpdate { get; } = new SafeAction();
        public IProfessionTipDotSystem TipDotSystem => this; public bool IsShowTipDot => true; public bool TempLimitShowTip => false;
        public Action OnTipDotShowChanged { get; set; } public SafeAction OnTipDotSystemInited { get; set; } = new SafeAction(); public SafeAction OnTipDotSystemPreClear { get; set; } = new SafeAction();
        public TipDotGraphType? GetGraphType(string name) => null;
        public int Profession => 1; public bool IsProfessionFunctionLimit => false; public bool IsProfessionUnlocked(int id) => true; public int GetProfessionLevel(int id) => 1;
        public event Action<int, int> OnProfessionUpgrade, OnSkillRankRewardUpdate; public event Action<IProfessionItemData, int, int> OnSkillRankUp; public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
        public ProfessionDetailDefinition GetProfession(int id) => throw new NotSupportedException(); public int GetLevelPropId(int id) => throw new NotSupportedException();
        public bool TryGetLevelInfo(int id, int level, out ProfessionUpgradeCostDefinition info) => throw new NotSupportedException();
        public IReadOnlyDictionary<int, long> GetProps(int id, int level) => throw new NotSupportedException(); public Dictionary<int, long> GetProfessionLevelPropDiff(int id, int from, int to) => throw new NotSupportedException();
        public string GetPropNameText(int type) => throw new NotSupportedException(); public string FormatPropValue(int type, long value) => throw new NotSupportedException();
        public void InitPropItem(ProfessionPlayerPropItem item, int type, long value, long next) => throw new NotSupportedException(); public int GetItemType(IProfessionItemInfo item) => throw new NotSupportedException();
        public long CalcAwardBadge(ProfessionDetailDefinition definition, IProfessionUpgradePlayer player) => throw new NotSupportedException();
        public ItemIcon GetIcon(int id, Transform parent) => throw new NotSupportedException(); public void RecycleIcon(ItemIcon icon) => throw new NotSupportedException();
        public void Upgrade(int id, Action success) => throw new NotSupportedException(); public int MoneyId => throw new NotSupportedException(); public void ShowLackOfItem(int id, long amount) => throw new NotSupportedException();
        public bool IsItemSourceFromMerge(IProfessionUpgradePlayer player, int id) => throw new NotSupportedException(); public long CalcCanMergeAmount(IProfessionUpgradePlayer player, int id, bool recursion) => throw new NotSupportedException();
    }
    private int errors; private Camera cameraUi; private RenderTexture target; private EventSystem events;
    private IEnumerator Start()
    {
        var check = Check();
        while (true) { object current; try { if (!check.MoveNext()) break; current = check.Current; } catch (Exception error) { PartnerGiftControllerValidation.Finish(error); yield break; } yield return current; }
        PartnerGiftControllerValidation.Finish(errors == 0 ? null : new Exception("Native gift errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var text = JsonUtility.FromJson<TextCatalog>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/PartnerGift/partner-gift-controller-texts.json").text);
        var rules = JsonUtility.FromJson<Rules>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Editor/gift-controller-rules.json").text);
        var context = new Context { Texts = text.texts.ToDictionary(row => row.key, row => row.text),
            Values = new Dictionary<Prop, long> { [Prop.Attack] = 10, [Prop.HangUpExpAdd] = 2 }, GiftInfos = rules.gifts.ToDictionary(row => row.ClassId, row => new PartnerGiftInfo { ClassId = row.ClassId, Friendship = row.Friendship }) };
        context.Texts["fixture_title"] = "陌生的感觉";
        context.Icons = new PartnerGiftPreferenceProbe();
        context.Texts["ui_ItemIcon_count_key"] = context.Icons.CountFormat;
        context.Npc = new PartnerGiftNpcInfo { Id = 8401, Properties = new PartnerFriendshipNpcInfo { Id = 8401, LevelPropId = 77 },
            CanCustomFriendshipTag = true, GiftPreferenceDict = context.Icons.Preferences.ToDictionary(row => row.Key, row => row.Value) };
        foreach (int id in new[] { 1130, 1131, 1100, 1300 }) context.Inventory.Add(new ItemData { Info = context.Icons.GetItemInfo(id), Count = 8 });
        PartnerGiftUiServices.Current = context; ProfessionPropertyBindings.Current = context; LanguageManager.Initialize(context); OfficialWwiseEvent.Host = context;
        ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => Object.Instantiate(prefab, parent, keepWorld);
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>()); GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        WorldMultiLanguageText.Localize = key => context.Text(key);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PartnerGift/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic_0.asset");
        font = Object.Instantiate(font); font.atlasTextures = font.atlasTextures.Select(value => Object.Instantiate(value)).ToArray(); font.material = Object.Instantiate(font.material); font.material.mainTexture = font.atlasTexture;
        Set(font, "m_SourceFontFile", AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("361d52fe7fad65d6413db534072fdf5c"))); font.fallbackFontAssetTable.Clear(); WorldMultiLanguageText.LanguageFont = () => font;
        cameraUi = new GameObject("GiftNativeCamera", typeof(Camera)).GetComponent<Camera>(); cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(.15f, .18f, .2f);
        target = new RenderTexture(768, 1600, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("NativeGiftFixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1; canvas.scaleFactor = .65f;
        events = new GameObject("SimulatedGiftPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var host = new GameObject("InactiveGiftBindingHost", typeof(RectTransform)); host.transform.SetParent(canvas.transform, false); host.SetActive(false);
        // Fixture parent represents the original MiddleGo's 100-pixel inset
        // under a 1080x2400 portrait viewport. Native gift geometry stays intact.
        ((RectTransform)host.transform).sizeDelta = new Vector2(1080, 2300); host.transform.localPosition = new Vector3(0, 50, 0);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PartnerGift/NpcGiftContent.prefab"); Require(prefab != null, "native gift root");
        var root = Object.Instantiate(prefab, host.transform, false); Require(root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "all original gift scripts bound");
        var panel = root.GetComponent<NpcDetailPanelGiftContent>(); var level = root.GetComponentInChildren<NpcFriendshipLevelContent>(true);
        context.TagSprite = Get<Image>(panel, "mFriendshipTagBg").sprite; panel.Configure(context); level.Configure(context);
        level.Init(context.Data, context.Npc); int given = 0; panel.Init(context.Data, context.Npc, () => given++, level.ResetExpPreview, level.ShowExpPreview);
        root.SetActive(true); host.SetActive(true); yield return null; yield return null;
        var scroll = Get<LoopVerticalScrollRect>(panel, "mScrollRect"); var blue = Get<NpcGiftBlueDotItem>(panel, "mNpcGiftBlueDotItem");
        Require(scroll.totalCount == 4 && blue.IsShow, "native list and preferred inventory blue dot");
        var icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Require(icons.Count == 4, "original loop materializes inventory");
        Require(root.GetComponentsInChildren<WorldMultiLanguageText>(true).All(text => text.myKey != "null"), "original empty language keys retain static separator text");
        Require(icons[1130].GetAttachment(ItemIconAttachmentType.Upgrade).gameObject.activeSelf, "preferred gift upgrade hint uses original blue-dot result");
        Capture("gift-controller-open.png");
        Click(icons[1130].gameObject); Require(Selected(panel)[1130] == 1 && Get<Slider>(level, "mPreviewSlider").value == 3000, "pointer adds one and previews rounded preference experience");
        Capture("gift-controller-selected.png"); Click(icons[1130].GetAttachment(ItemIconAttachmentType.ReduceTip).gameObject); Require(Selected(panel).Count == 0, "pointer removes final selected gift");
        Click(Get<Button>(panel, "mAutoSelectBtn").gameObject); Require(Selected(panel).Values.Sum() > 0, "automatic selection fills next target");
        Require(!Selected(panel).ContainsKey(1100), "auto selection consumes preferred gifts before non-preferences");
        Require(context.GetAllExpByDict(Selected(panel), context.Npc.GiftPreferenceDict) >= 5000, "auto selection reaches next level");
        Click(Get<Button>(panel, "mResetBtn").gameObject); Require(Selected(panel).Count == 0 && Get<Slider>(level, "mPreviewSlider").value == 0, "reset rebuilds list and preview");
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); Click(Get<Button>(panel, "mGiveGiftBtn").gameObject);
        Require(context.Gives == 1 && given == 0 && Selected(panel).Count == 1, "give waits for response"); context.PendingGive(); Require(given == 1 && Selected(panel).Count == 0, "success refresh precedes completion callback");
        context.Data.Exp = 7000; context.Data.Level = 2; level.Init(context.Data, context.Npc); panel.Init(context.Data, context.Npc, () => given++, level.ResetExpPreview, level.ShowExpPreview); yield return null; yield return null;
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); Click(Get<Button>(panel, "mGiveGiftBtn").gameObject); Require(context.Gives == 2 && context.Confirm == null, "exact maximum experience does not prompt overflow"); context.PendingGive();
        context.Data.Exp = 9900; context.Data.Level = 2; level.Init(context.Data, context.Npc); panel.Init(context.Data, context.Npc, () => given++, level.ResetExpPreview, level.ShowExpPreview); yield return null; yield return null;
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); Click(Get<Button>(panel, "mGiveGiftBtn").gameObject); Require(context.Confirm != null && context.Gives == 2, "overflow prompts before transaction");
        context.Confirm.Accept(); Require(context.Gives == 3, "overflow confirm sends once"); context.PendingGive();
        context.RankMax = 2; context.Data.Exp = 5000; context.Data.Level = 2; level.Init(context.Data, context.Npc); panel.Init(context.Data, context.Npc, () => given++, level.ResetExpPreview, level.ShowExpPreview); yield return null; yield return null;
        Require(Get<WorldStateSwitcher<NpcDetailPanelGiftContent.GiftState>>(panel, "mStateSwitcher") != null, "original rank state switch bound");
        Capture("gift-controller-rank-limit.png");
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); Require(Selected(panel).Count == 0 && context.Messages.Last() == context.Text("ui_npc_detail_select_gift_level_limit_tip"), "rank limit rejects pointer selection");
        context.RankMax = 3; context.Data.Exp = 10000; context.Data.Level = 3; level.Init(context.Data, context.Npc); panel.Init(context.Data, context.Npc, () => given++, level.ResetExpPreview, level.ShowExpPreview); yield return null; yield return null; Capture("gift-controller-max-level.png");
        icons = Get<Dictionary<int, ItemIcon>>(panel, "mGiftItemIconDict"); Click(icons[1130].gameObject); Require(Selected(panel).Count == 0 && context.Messages.Last() == context.Text("ui_npc_detail_give_gift_max_level_tip"), "max level rejects pointer selection before rank check");
        context.Data.Level = 1; context.Data.Exp = 0; panel.Init(context.Data, context.Npc, () => given++, level.ResetExpPreview, level.ShowExpPreview);
        Click(Get<Button>(panel, "mCustomFriendshipTagBtn").gameObject); Require(context.Input.Masks == 1, "original title moderation endpoint");
        context.CharactersValid = false; context.Input.Submit("X"); Require(context.LastError == 1150 && context.TagWrites == 0, "unsupported character rejected");
        context.CharactersValid = true; context.TagLegal = 123; context.Input.Submit("X"); Require(context.LastError == 123 && context.TagWrites == 0, "official legality error forwarded");
        context.TagLegal = 0; context.Input.Submit("伙伴"); Require(context.TagWrites == 1 && context.Closes == 0, "title waits for response"); context.PendingTag("伙伴"); Require(context.Data.Tag == "伙伴" && context.Closes == 1 && Get<WorldMultiLanguageText>(panel, "mFriendshipTagText").Text == "伙伴", "title success updates data then label and closes");
        context.Input.Submit(""); context.PendingTag(""); Require(context.Data.Tag == "伙伴" && context.Closes == 2 && Get<WorldMultiLanguageText>(panel, "mFriendshipTagText").Text == "伙伴", "empty response retains original current-tag fallback branch");
        foreach (var item in context.Inventory) item.Count = 0; context.RaiseInventory(); Require(!blue.IsShow, "inventory event clears preferred gift blue dot");
        context.Inventory[0].Count = 8; context.RaiseInventory(); Require(blue.IsShow, "inventory event restores enough preferred experience");
        context.AddressLimited = true; context.RaiseInventory(); Require(!blue.IsShow, "address-list function gate hides gift blue dot"); context.AddressLimited = false;
        host.SetActive(false); Require(context.Subscribers == 0 && context.OnTipDotShowChanged == null, "gift blue dot and friendship event listeners removed");
        Object.Destroy(host); context.Icons.Dispose(); yield return null; Require(errors == 0, "native controller render/input/lifecycle errors");
    }
    private static Dictionary<int, long> Selected(object panel) => Get<Dictionary<int, long>>(panel, "mSelectedGiftDict");
    private void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)go.transform; var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits); Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == go, "native click unobscured: " + go.name);
        ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target }; RenderPipeline.SubmitRenderRequest(cameraUi, request);
        RenderTexture.active = target; var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG()); RenderTexture.active = null; Object.Destroy(image);
    }
    private static FieldInfo Field(object target, string name) { for (Type type = target.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (field != null) return field; } throw new MissingFieldException(name); }
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native gift controller: " + message); }
}
