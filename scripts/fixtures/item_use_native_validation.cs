using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.GachaUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ItemUseNativeValidation
{
    const string Key = "Zjcs.ItemUseNative";
    public static readonly List<string> Checks = new List<string>();
    static ItemUseNativeValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("OriginalItemUseFixture").AddComponent<ItemUseNativeRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", float.MaxValue))
                Finish(new TimeoutException("Original item-use fixture timed out"));
        };
    }
    public static void Run()
    {
        var project = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_ITEM_USE_PROJECT"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(project + Path.DirectorySeparatorChar)) throw new InvalidOperationException("Independent source project required");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Checks.Add("PASS " + message); }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) { Debug.LogException(error); Checks.Add(error.ToString()); }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_ITEM_USE_OUTPUT"), "report.json"),
            JsonConvert.SerializeObject(new { success = error == null, checks = Checks }, Formatting.Indented));
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

// UI services are controlled here; all prefab trees, controllers, resources,
// selected rules and the actual isolated inventory transaction use source assets.
public sealed class ItemUseNativeRunner : MonoBehaviour, IProfessionItemIconContext, IProfessionItemColors,
    IProfessionItemPlayer, IItemDetailBindings, IItemUseResultBindings, IItemUseDescriptionBindings,
    IItemUseContentServices, IItemChooseDialogBindings, IUseCountSliderServices, IHomeCountColors, IUseResultIconBindings
{
    OfficialPetItemDetailAssetCatalog assets;
    JObject rules;
    Dictionary<string, string> texts;
    OfficialChestUsePreviewRules preview;
    OfflineMainlineProgressSession profile;
    OfflineOptionalChestService chestService;
    OfflineProfessionImageCatalog catalog;
    IProfessionImageResourceLoader images;
    ItemIconAttachmentManager attachments;
    FontAssetFontService fonts;
    IDisposable metrics;
    readonly List<IDisposable> bindings = new List<IDisposable>();
    readonly List<GameObject> roots = new List<GameObject>();
    Camera cameraUi;
    RenderTexture target;
    Canvas canvas;
    EventSystem events;
    GameObject chooseRoot;
    ItemDetailDialogUseContent useContent;
    OptionalChestOpenResult committed;
    readonly List<string> runtimeFailures = new List<string>();
    int receipt, subscribers, awardSubscribers;
    Action<IProfessionItemData, long, object> inventoryChanged;
    IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var steps = Steps();
        while (true) { object next; try { if (!steps.MoveNext()) break; next = steps.Current; }
            catch (Exception error) { Application.logMessageReceived -= OnLog; ItemUseNativeValidation.Finish(error); yield break; } yield return next; }
        Application.logMessageReceived -= OnLog;
        ItemUseNativeValidation.Finish(runtimeFailures.Count == 0 ? null : new Exception(string.Join("\n", runtimeFailures)));
    }
    void OnLog(string message, string trace, LogType type)
    { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) runtimeFailures.Add(message + "\n" + trace); }
    IEnumerator Steps()
    {
        AudioListener.volume = 0;
        assets = OfficialPetItemDetailAssetCatalog.LoadDefault(); rules = JObject.Parse(assets.Rules.text);
        texts = ((JArray)JObject.Parse(assets.Texts.text)["texts"]).ToDictionary(row => (string)row["key"], row => (string)row["text"]);
        LanguageManager.Initialize(new OfflinePartnerLocalizationService(assets.Texts));
        fonts = OfflineHomeHudAssets.LoadDefault().CreateFontService(); fonts.Initialize();
        metrics = ScreenMetrics.Bind(new Metrics());
        catalog = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        catalog.Entries = assets.Images.Select(row => new OfflineProfessionImageCatalog.Entry { Path = row.path, AssetGuid = row.assetGuid, Asset = row.asset }).ToArray();
        images = new OfflineProfessionImageResourceLoader(catalog);
        attachments = new ItemIconAttachmentManager(images, () => assets.AttachmentConfig, new GAction());
        preview = new OfficialChestUsePreviewRules(Resources.Load<TextAsset>("PetGacha/chest-use-preview").text);
        var slots = new ProfileSlotCatalog(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_ITEM_USE_OUTPUT"), "profiles"));
        string slot = slots.CreateSlot(); profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        chestService = new OfflineOptionalChestService(preview, profile);
        Check(profile.ApplyInventoryTransaction("seed", save => {
            foreach (int id in new[] { 6950, 6951, 6952 })
                if (!OfflineInventoryOperations.Grant(save.Player, id, 5, 1000).IsApplied) throw new Exception("Seed grant failed");
        }).Status == ProgressionResultStatus.Applied, "real isolated inventory seeded");
        cameraUi = new GameObject("OriginalItemCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.orthographic = true; cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(.12f, .2f, .28f);
        target = new RenderTexture(608, 1080, 24); target.Create(); cameraUi.targetTexture = target;
        canvas = new GameObject("OriginalItemCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scale = canvas.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1080, 1920); scale.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("ProcessLocalItemPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (int id in new[] { 6950, 6951, 6952 })
        {
            var root = Create(assets.UseDetail, canvas.transform, false); roots.Add(root); root.SetActive(true);
            root.GetComponent<ItemUseDetailDialog>().Init(id);
            root.GetComponent<WorldPopupWindow>().Show(); yield return new WaitForSeconds(.8f);
            Check(root.GetComponentsInChildren<MonoBehaviour>(true).All(row => row), "every original detail script resolves: " + id);
            useContent = root.GetComponentInChildren<ItemDetailDialogUseContent>(true);
            Check(useContent.CurrentUseCount == 5 && useContent.CanUseItemCount == 5, "original initial use count: " + id);
            var result = root.GetComponentInChildren<ItemDetailDialogUseResult>(true);
            Check(result.UseCount == 5 && result.PerUseCount == preview.OptionalChest(id).Options.First().Value,
                "original optional preview count: " + id);
            Check(root.GetComponentsInChildren<UseResultIcon>().Length == 3
                && root.GetComponentsInChildren<UseResultIcon>().All(cell => cell.Count == preview.OptionalChest(id).Options[cell.ClassId] * 5),
                "original loop creates all scaled reward cells: " + id);
            Click(useContent.SetCountSlider.MinusBtn); Click(useContent.SetCountSlider.MinusBtn); Click(useContent.SetCountSlider.MinusBtn);
            Check(useContent.CurrentUseCount == 2 && result.UseCount == 2, "native quantity controls update preview: " + id);
            Capture("detail-" + id + ".png");
            Click(useContent.UseItemBtn); yield return new WaitForSeconds(.8f);
            var choose = chooseRoot.GetComponent<ItemChooseDialog>();
            var rows = chooseRoot.GetComponentsInChildren<ItemChooseDialogItem>();
            var options = preview.OptionalChest(id).Options;
            Check(rows.Select(row => row.ClassId).SequenceEqual(options.Keys), "original non-skill option order: " + id);
            var confirm = Field<Button>(choose, "mConfirmBtn");
            Check(!confirm.interactable, "unselected confirm disabled: " + id);
            Click(Field<Button>(rows[0], "mToggleBtn")); Click(Field<Button>(rows[0], "mToggleBtn"));
            Check(rows[0].ToggleState == ItemChooseDialogItem.SelectState.On, "same single option stays selected: " + id);
            Click(Field<Button>(rows[1], "mToggleBtn"));
            Check(rows[0].ToggleState == ItemChooseDialogItem.SelectState.Off && rows[1].ToggleState == ItemChooseDialogItem.SelectState.On && confirm.interactable,
                "single selection moves and enables confirmation: " + id);
            Capture("choice-" + id + ".png"); Click(confirm); yield return null;
            Check(committed != null && committed.IsApplied && profile.InventoryCount(id) == 3
                && profile.InventoryCount(rows[1].ClassId) == options[rows[1].ClassId] * 2,
                "native confirm calls actual atomic local transaction: " + id);
            Check(useContent.CanUseItemCount == 3 && useContent.CurrentUseCount == 3, "inventory event refreshes original quantity controller: " + id);
            var reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Check(reloaded.InventoryCount(id) == 3 && reloaded.InventoryCount(rows[1].ClassId) == options[rows[1].ClassId] * 2,
                "actual disk reload preserves native selection: " + id);
            Click(useContent.UseItemBtn); yield return new WaitForSeconds(.8f);
            byte[] before = File.ReadAllBytes(slots.GetProgressPath(slot));
            Click(Field<Button>(chooseRoot.GetComponent<ItemChooseDialog>(), "mCancelBtn")); yield return null;
            Check(before.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "native cancel leaves real save unchanged: " + id);
            Close(root); yield return null;
            Check(subscribers == 0 && awardSubscribers == 0, "detail close releases player and award subscriptions: " + id);
        }
        foreach (var binding in bindings) binding.Dispose(); attachments.Dispose(); fonts.Dispose(); metrics.Dispose();
    }
    GameObject Create(GameObject prefab, Transform parent, bool keepWorld)
    {
        var stage = new GameObject("InactiveItemBinding", typeof(RectTransform)); stage.SetActive(false); stage.transform.SetParent(canvas.transform, false);
        try {
            var root = Object.Instantiate(prefab, stage.transform, false); root.SetActive(false);
            bindings.Add(OfflineUiBindingModule.Register(root.transform, new OfflineUiBindings { Localize = Text,
                Font = () => OfflineHomeHudAssets.LoadDefault().defaultFont, Images = _ => images,
                Pool = (scope, p, slot, keep) => { var clone = Create(p, slot, keep); clone.SetActive(true); return clone; } }));
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.font) fonts.SetupFont(text.font);
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = Text;
            foreach (var icon in root.GetComponentsInChildren<ItemIcon>(true)) icon.Bind(this);
            foreach (var attachment in root.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(attachments);
            foreach (var detail in root.GetComponentsInChildren<ItemDetailDialog>(true)) detail.Bind(this);
            foreach (var use in root.GetComponentsInChildren<ItemDetailDialogUseContent>(true)) use.Bind(this);
            foreach (var slider in root.GetComponentsInChildren<SetCountSlider>(true)) slider.GlobalColors = this;
            foreach (var slider in root.GetComponentsInChildren<SetUseCountSlider>(true)) slider.LimitServices = this;
            foreach (var hold in root.GetComponentsInChildren<LongTouch>(true)) hold.PointerPositionOverride = () => Vector2.zero;
            foreach (var choose in root.GetComponentsInChildren<ItemChooseDialog>(true)) choose.Bind(this);
            foreach (var row in root.GetComponentsInChildren<ItemChooseDialogItem>(true)) row.Bind(this);
            foreach (var row in root.GetComponentsInChildren<UseResultIcon>(true)) row.Bind(this);
            foreach (var loop in root.GetComponentsInChildren<LoopScrollRect>(true))
                loop.DefaultPrefabSource.InstantiatePrefab = (p, slot, keep) => Create(p, slot, keep);
            foreach (var output in root.GetComponentsInChildren<OutputAdditionDetailButton>(true)) output.BindNoOutputGroup(new ItemDetailDefinition());
            foreach (var gm in root.GetComponentsInChildren<ItemDetailGmTools>(true)) gm.BindReleaseActor(false, false);
            foreach (var decomposition in root.GetComponentsInChildren<ItemDecomposContent>(true)) decomposition.Bind(null, () => Close(root));
            root.transform.SetParent(parent, keepWorld); return root;
        } finally { Object.Destroy(stage); }
    }
    public GameObject InstantiateUi(GameObject prefab, Transform parent) { var root = Create(prefab, parent, false); root.SetActive(true); return root; }
    public ItemDetailDefinition Detail(int id)
    {
        var row = Row(id); return new ItemDetailDefinition { Info = new Info(row), NameKey = GetItemNameKey(id),
            TypeKey = "ItemType." + (string)rules["item_type_names"][(string)row["ItemType"]],
            DescriptionKey = "item_" + id + "_desc", FunctionDescriptionKey = "item_" + id + "_func_desc",
            ShowPlayerItemCount = (bool)row["ShowPlayerItemCount"], ShowUseDetail = (bool)row["ShowUseDetail"] };
    }
    JObject Row(int id) => ((JArray)rules["tables"]["item"]).OfType<JObject>().Single(row => (int)row["ClassId"] == id);
    public ItemUseResultData LoadUseResult(int id)
    { var data = preview.Preview(id); return new ItemUseResultData { OptionCount = data.OptionCount,
        Entries = Enumerable.Range(0, data.Count).Select(index => new ItemUseResultEntry(data.ItemAt(index), data.CountAt(index), id)).ToArray() }; }
    public void SetFunctionDescription(WorldMultiLanguageText text, ItemDetailDefinition detail, long count) => text.SetKey(detail.FunctionDescriptionKey, count);
    public string Text(string key) => texts.TryGetValue(key, out var value) ? value : key;
    public bool ContainsText(string key) => texts.ContainsKey(key);
    public Sprite LoadSprite(string path) { using var operation = images.LoadAsset<Sprite>("Assets/Image/" + path); return operation.Result; }
    public Sprite QualityBackground(Quality quality, bool small) => LoadSprite("UI/ItemDetailDialog/Img" + quality + "_PopUps" + (small ? "_Small" : ""));
    Color Palette(string key) => rules["global_colors"][key].ToObject<Color>();
    Color Theme(string config, int theme, int type)
    { var row = ((JArray)rules["global_colors"][config]["TextColorLists"]).Single(value => (int)value["ThemeColorType"] == theme);
        var types = row["TypeList"].Values<int>().ToArray(); var color = row["TextColors"][Array.IndexOf(types, type)];
        Check((int)color["EnableGradient"] == 0, "selected native text color is solid"); return color["Color"].ToObject<Color>(); }
    public Color QualityTextColor(Quality quality) => Theme("QualityColorConfig", (int)ThemeColorType.Normal, (int)quality);
    public IReadOnlyList<ItemDetailSource> Sources(int id) => Array.Empty<ItemDetailSource>(); // controlled source-navigation port
    public void Close(GameObject root) { root.SetActive(false); }
    public IProfessionItemIconContext Items => this;
    public ItemIconConfig Config => assets.IconConfig;
    public UnityEngine.Object GrayConfig => assets.GrayMaterial;
    public IProfessionItemColors Colors => this;
    public IProfessionItemPlayer Player => this;
    public IProfessionItemInfo GetItemInfo(int id) => new Info(Row(id));
    public object Compact(long count) => CultureNumberArg.Compact(count);
    public object CultureText(string key) => new CultureTextArg(Text(key), Array.Empty<object>(), false);
    public string GetItemNameKey(int id) => "item_" + id + "_name";
    public bool TryGetSourceData(int id, out IReadOnlyList<IProfessionItemSource> sources) { sources = null; return false; }
    public bool CanMerge(int id, bool recursive) => false;
    public object GetLocationInfos(int id) => null;
    public void SetUIGray(UnityEngine.Object config, GameObject root, bool gray) => ItemDetailUiGray.Apply(root, gray, assets.GrayMaterial, assets.GrayFontMaterial);
    public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) { }
    public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flags = EUIInstantiateFlag.All) => InstantiateUi(prefab, parent);
    public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw new NotSupportedException(prefab);
    public void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long count, Action<GameObject> callback) => throw new NotSupportedException("Nested item details are separate integration work");
    public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long count, Action<GameObject> callback) => throw new NotSupportedException("Nested item details are separate integration work");
    public long GetItemAmount(int id) => profile.InventoryCount(id);
    public event Action<IProfessionItemData, long, object> OnItemCountUpdate { add { subscribers++; inventoryChanged += value; } remove { subscribers--; inventoryChanged -= value; } }
    public bool CanGetItemFromMapLocation(object location) => false;
    public Color Green => Palette("Green"); public Color White => Palette("White");
    public Color LightRed => GetTextColor(ThemeColorType.Light, ColorType.Red);
    public Color GetTextColor(ThemeColorType theme, ColorType type) => Theme("TextColorConfig", (int)theme, (int)type);
    public string WrapColor(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>";
    public long ItemBatchUseMinCount => (long)rules["tables"]["game_settings"]["ItemBatchUseMinCount"];
    public long ItemBatchUseMaxCount => (long)rules["tables"]["game_settings"]["ItemBatchUseMaxCount"];
    public bool CanUseInBatches(int id) => (bool)Row(id)["CanUseInBatches"];
    public bool CanOverlap(int id) => (bool)Row(id)["CanOverlap"];
    public bool IsCanUse(int id, long count) => (bool)Row(id)["CanUse"];
    public string UseButtonText(int id) => null;
    public void UseItem(int id, long count, IWindowOperator owner, Action<long> success)
    {
        var definition = preview.OptionalChest(id);
        chooseRoot = Create(assets.Choose, canvas.transform, false); roots.Add(chooseRoot); chooseRoot.SetActive(true);
        chooseRoot.GetComponent<ItemChooseDialog>().Init(definition.Options.ToDictionary(row => row.Key, row => row.Value), definition.OptionCount, (int)count, selected => {
            committed = chestService.Open(id, (int)count, selected, 1000, "native:" + ++receipt);
            if (committed.IsApplied) { success(count); inventoryChanged?.Invoke(new Data(GetItemInfo(id), profile.InventoryCount(id)), count + profile.InventoryCount(id), null); }
        });
        chooseRoot.GetComponent<WorldPopupWindow>().Show();
    }
    public void ItemNotEnough(int id) => throw new InvalidOperationException("Unexpected not-enough request");
    public void CloseParent(GameObject root) => Close(root.GetComponentInParent<WorldPopupWindow>().gameObject);
    public IDisposable RegisterAwardHandler(Func<IItemUseAward, bool> handler) { awardSubscribers++; return new Release(() => awardSubscribers--); }
    public void MergeAndShowAward(IItemUseAward award, IWindowOperator owner) => throw new NotSupportedException("Production award presentation remains separate work");
    public void SortOptions(List<int> ids) { } // selected source SkillOptionSortUtils returns for non-skill candidates
    public void SetConfirmGray(GameObject button, bool gray) => SetUIGray(null, button, gray);
    public ItemChooseFlags ChoiceFlags(int id) => new ItemChooseFlags(false, false, false);
    public void ApplyTreasureOwnership(ItemIcon icon, int id, bool show) => throw new NotSupportedException();
    public ItemIcon GetIcon(int id, Transform parent) => InstantiateUi(assets.ItemIcon, parent).GetComponent<ItemIcon>();
    public void RecycleIcon(ItemIcon icon) { icon.Clear(); icon.gameObject.SetActive(false); Object.Destroy(icon.gameObject); }
    public bool IsTreasure(int id) => false;
    public bool ShowGained(int id) => false;
    public bool IsGotItem(int id) => false;
    public object ProbabilityPercent(double probability) => probability.ToString("P2");
    public double FastItemMaxCountMessageColdDown => (double)rules["home_settings"]["mFastItemMaxCountMessageColdDown"];
    public float Time => UnityEngine.Time.time;
    public string TextFormatted(string key, params object[] arguments) => string.Format(Text(key), arguments);
    public void SystemMessage(string message) { }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    void Click(Selectable button)
    {
        Canvas.ForceUpdateCanvases(); Check(button && button.isActiveAndEnabled && button.interactable, "enabled native pointer control " + button.name);
        var rect = (RectTransform)button.transform;
        var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
        Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject, "first native raycast " + button.name);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(608, 1080, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 608, 1080), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_ITEM_USE_OUTPUT"), name), image.EncodeToPNG()); RenderTexture.active = previous; Object.Destroy(image);
    }
    static void Check(bool value, string message) => ItemUseNativeValidation.Check(value, message);
    sealed class Release : IDisposable { Action callback; public Release(Action value) { callback = value; } public void Dispose() { var value = callback; callback = null; value?.Invoke(); } }
    sealed class Metrics : IScreenMetrics { public int Width => 608; public int Height => 1080; public ScreenOrientation Orientation => ScreenOrientation.Portrait; public Rect SafeArea => new Rect(0, 0, 608, 1080); }
    sealed class Data : IProfessionItemData
    { public Data(IProfessionItemInfo info, long count) { Info = info; Count = count; } public int Id => Info.ClassId; public long Count { get; }
        public IProfessionItemInfo Info { get; } public Quality GetQuality() => Info.Quality; public bool CanExpire() => false; }
    sealed class Info : IProfessionItemInfo
    { readonly JObject row; public Info(JObject value) { row = value; } public int ClassId => (int)row["ClassId"]; public string Icon => (string)row["RawIcon"]; public string IconEffect => (string)row["IconEffect"];
        public Quality Quality => (Quality)(int)row["Quality"]; public bool CanOverlap => (bool)row["CanOverlap"]; public object GetFakeIcon() => null; }
}
