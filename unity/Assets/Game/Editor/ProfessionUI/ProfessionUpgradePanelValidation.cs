using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;
using SimulatedMouse = Zjcs.Editor.ProfessionUI.ProfessionItemIconValidation.SimulatedMouse;
using LocalizationBinaryFileReader = Zjcs.Official.World.NativeLabelUI.LocalizationBinaryFileReader;

namespace Zjcs.Editor.ProfessionUI
{
    // The actual original upgrade subtree and original property/cost prefabs.
    // Player/config/formula/transaction are fixtures; Chinese text is original.
    [InitializeOnLoad]
    public static class ProfessionUpgradePanelValidation
    {
        private const string Active = "Zjcs.ProfessionUpgradePanel.Active";
        private const string Native = "Assets/Official/ProfessionUI/Native/";
        private static string Cache => Path.Combine(ProfessionValidationWorkspace.Repository, ".tools/source-packages/current/871_156598/profession-ui/typed");
        [Serializable] private sealed class FontFixture { public string config_asset, resource_catalog; }
        [Serializable] private sealed class LanguageFonts { public string DefaultRegion; public RegionFonts RegionFontAssetDict; public LanguageFontKeys FontAssetDict; }
        [Serializable] private sealed class RegionFonts { public RegionFont CN; }
        [Serializable] private sealed class RegionFont { public string Default; public string[] Fallbacks; }
        [Serializable] private sealed class LanguageFontKeys { public string zh_CN; }
        [Serializable] private sealed class Fixture { public Reward[] items; public Palette global_colors; public string prefab, config, catalog; }
        [Serializable] private sealed class Reward { public Info info; }
        [Serializable] private sealed class Info : IProfessionItemInfo
        {
            public int ClassId, Quality; public string RawIcon, IconEffect; public bool CanOverlap;
            int IProfessionItemInfo.ClassId => ClassId;
            string IProfessionItemInfo.Icon => RawIcon;
            string IProfessionItemInfo.IconEffect => IconEffect;
            Quality IProfessionItemInfo.Quality => (Quality)Quality;
            bool IProfessionItemInfo.CanOverlap => CanOverlap;
            public object GetFakeIcon() => throw new InvalidOperationException("Original reward icon is required.");
        }
        [Serializable] private sealed class ColorValue { public Color Color; }
        [Serializable] private sealed class ColorList { public int ThemeColorType; public int[] TypeList; public ColorValue[] TextColors; }
        [Serializable] private sealed class ColorConfig { public ColorList[] TextColorLists; }
        [Serializable] private sealed class Palette : IProfessionItemColors
        {
            public Color Green, White; public ColorConfig TextColorConfig;
            Color IProfessionItemColors.Green => Green;
            Color IProfessionItemColors.White => White;
            public Color LightRed => GetTextColor(ThemeColorType.Light, ColorType.Red);
            public Color GetTextColor(ThemeColorType theme, ColorType type)
            {
                foreach (var row in TextColorConfig.TextColorLists)
                    if (row.ThemeColorType == (int)theme)
                        for (int i = 0; i < row.TypeList.Length; i++)
                            if (row.TypeList[i] == (int)type) return row.TextColors[i].Color;
                throw new InvalidOperationException("Original color binding is missing.");
            }
        }
        private sealed class ItemData : IProfessionItemData
        {
            public int Id { get; set; } public long Count => 1;
            public IProfessionItemInfo Info { get; set; }
            public Quality GetQuality() => Info.Quality;
            public bool CanExpire() => false;
        }
        private sealed class Services : IProfessionUpgradeBindings, IProfessionUpgradePlayer, IProfessionItemIconContext, IProfessionItemPlayer, IProfessionTipDotSystem, IProfessionTipDotSystemController
        {
            public int Profession { get; set; } = 10;
            public bool IsProfessionFunctionLimit { get; set; }
            public bool Unlocked = true, TipVisible = true;
            public int Level = 1, Max = 3, CountSubscribers, RankSubscribers, Claims, Recycles, AwardCalculations, InfoPopups, Uses, Shortages;
            public long PendingBadge, MergeAmount;
            public bool MergeSource;
            public Action Pending;
            public readonly Dictionary<int, long> Inventory = new Dictionary<int, long> { [301] = 100, [302] = 100 };
            public readonly Dictionary<int, int> ItemTypes = new Dictionary<int, int> { [301] = 316, [302] = 300 };
            public readonly List<string> Calls = new List<string>();
            public readonly List<ItemIcon> Borrowed = new List<ItemIcon>();
            public readonly List<ProfessionPlayerPropItem> Rows = new List<ProfessionPlayerPropItem>();
            public readonly Dictionary<int, Dictionary<int, long>> Props = new Dictionary<int, Dictionary<int, long>>
            {
                [1] = new Dictionary<int, long> { [1] = 100, [2] = 10, [3] = 20, [4] = 0, [5] = 50, [6] = 60 },
                [2] = new Dictionary<int, long> { [1] = 110, [2] = 10, [3] = 20, [4] = 1, [5] = 0, [6] = 60, [7] = 7 },
                [3] = new Dictionary<int, long> { [1] = 120, [2] = 10, [3] = 20, [4] = 1, [5] = 0, [6] = 60, [7] = 7 }
            };
            private Action<IProfessionItemData, long, object> count;
            private Action<int, int> upgraded, rewarded;
            private Action<IProfessionItemData, int, int> ranked;
            private Action<Dictionary<int, int>, Dictionary<int, int>> batch;
            public event Action<IProfessionItemData, long, object> OnItemCountUpdate { add { count += value; CountSubscribers++; } remove { count -= value; CountSubscribers--; } }
            public event Action<int, int> OnProfessionUpgrade { add { upgraded += value; RankSubscribers++; } remove { upgraded -= value; RankSubscribers--; } }
            public event Action<int, int> OnSkillRankRewardUpdate { add { rewarded += value; RankSubscribers++; } remove { rewarded -= value; RankSubscribers--; } }
            public event Action<IProfessionItemData, int, int> OnSkillRankUp { add { ranked += value; RankSubscribers++; } remove { ranked -= value; RankSubscribers--; } }
            public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch { add { batch += value; RankSubscribers++; } remove { batch -= value; RankSubscribers--; } }
            public void EmitAll()
            {
                var item = new ItemData { Id = 301, Info = GetItemInfo(301) };
                count?.Invoke(item, 0, null); upgraded?.Invoke(10, Level); rewarded?.Invoke(301, 1); ranked?.Invoke(item, 1, 2);
                batch?.Invoke(new Dictionary<int, int>(), new Dictionary<int, int>());
            }
            public IProfessionUpgradePlayer MainPlayer => this;
            public bool ManagerAvailable => true;
            public SafeAction OnPlayerUpdate { get; } = new SafeAction();
            public IProfessionTipDotSystem TipDotSystem => this;
            public bool IsShowTipDot => TipVisible;
            public bool TempLimitShowTip => false;
            public Action OnTipDotShowChanged { get; set; }
            public SafeAction OnTipDotSystemInited { get; set; } = new SafeAction();
            public SafeAction OnTipDotSystemPreClear { get; set; } = new SafeAction();
            public TipDotGraphType? GetGraphType(string graphName) => TipDotGraphType.Dynamic;
            public bool LinkNodeStateChange(TipDotNodeKey key, GameObject owner, Action<TipDotStateInfo> callback) { callback(default); return true; }
            public bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject owner, Action<TipDotStateInfo> callback) => true;
            public bool IsProfessionUnlocked(int profession) { Calls.Add("unlocked:" + profession); return Unlocked; }
            public int GetProfessionLevel(int profession) { Calls.Add("level:" + profession); return Level; }
            public long GetItemAmount(int item) => Inventory.TryGetValue(item, out var value) ? value : 0;
            public ProfessionDetailDefinition GetProfession(int profession) { Calls.Add("definition:" + profession); return new ProfessionDetailDefinition { Profession = profession, BadgeClassId = 301 }; }
            public int GetLevelPropId(int profession) => 71;
            public bool TryGetLevelInfo(int profession, int level, out ProfessionUpgradeCostDefinition info)
            {
                Calls.Add("cost:" + level);
                info = level <= Max ? new ProfessionUpgradeCostDefinition { UpgradeCost = new Dictionary<int, long> { [301] = 20, [302] = 30 } } : null;
                return info != null;
            }
            public IReadOnlyDictionary<int, long> GetProps(int id, int level) { Require(id == 71, "source level-prop ID"); Calls.Add("props:" + level); return Props[level]; }
            public Dictionary<int, long> GetProfessionLevelPropDiff(int profession, int from, int to) { Calls.Add("diff:" + from + ":" + to); return new Dictionary<int, long> { [1] = 10, [4] = 1 }; }
            public string GetPropNameText(int type) => "p" + type;
            public string FormatPropValue(int type, long value) => value.ToString(CultureInfo.InvariantCulture);
            public void InitPropItem(ProfessionPlayerPropItem item, int type, long value, long next)
            { Require(next == -1, "source no-preview sentinel"); Rows.Add(item); item.Init("p" + type, value.ToString(CultureInfo.InvariantCulture), (Sprite)null); }
            public int GetItemType(IProfessionItemInfo item) => ItemTypes.TryGetValue(item.ClassId, out var type) ? type : 100;
            public long CalcAwardBadge(ProfessionDetailDefinition profession, IProfessionUpgradePlayer player) { AwardCalculations++; return PendingBadge; }
            public ItemIcon GetIcon(int itemId, Transform parent)
            {
                var go = Object.Instantiate(iconPrefab, inactive.transform, false);
                foreach (var attachment in go.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(attachments);
                foreach (var progress in go.GetComponentsInChildren<WorldItemProgress>(true)) progress.Compact = value => value.ToString(CultureInfo.InvariantCulture);
                var icon = go.GetComponent<ItemIcon>(); icon.Bind(this); go.transform.SetParent(parent, false); icon.Init(GetItemInfo(itemId));
                Borrowed.Add(icon); Calls.Add("icon:" + itemId); return icon;
            }
            public void RecycleIcon(ItemIcon icon) { Recycles++; icon.Clear(); icon.transform.SetParent(inactive.transform, false); }
            public void Upgrade(int profession, Action onSuccess) { Claims++; Require(profession == 10, "exact upgrade profession"); Pending = onSuccess; }
            public int MoneyId => 301;
            public void ShowLackOfItem(int id, long amount) { Require(id == 301 && amount == 19, "money-only shortage amount"); Shortages++; }
            public bool IsItemSourceFromMerge(IProfessionUpgradePlayer player, int item) => MergeSource;
            public long CalcCanMergeAmount(IProfessionUpgradePlayer player, int item, bool recursion) { Require(recursion, "source recursive merge calculation"); return MergeAmount; }
            public ItemIconConfig Config { get; set; }
            public Object GrayConfig => throw Unexpected();
            public IProfessionItemColors Colors { get; set; }
            public IProfessionItemPlayer Player => this;
            public IProfessionItemInfo GetItemInfo(int item) => fixture.items.First(i => i.info.ClassId == item).info;
            public object Compact(long value) => value.ToString(CultureInfo.InvariantCulture);
            public object CultureText(string key) => key;
            public string GetItemNameKey(int item) => "item-" + item;
            public bool TryGetSourceData(int item, out IReadOnlyList<IProfessionItemSource> sources) { sources = Array.Empty<IProfessionItemSource>(); return false; }
            public bool CanMerge(int item, bool recursion) => false;
            public object GetLocationInfos(int item) => throw Unexpected();
            public bool CanGetItemFromMapLocation(object location) => false;
            public void SetUIGray(Object config, GameObject target, bool gray) => throw Unexpected();
            public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) => throw Unexpected();
            public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All) => Object.Instantiate(prefab, parent, false);
            public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw Unexpected();
            public void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long required, Action<GameObject> callback) => throw Unexpected();
            public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback) { InfoPopups++; }
        }
        private static bool ready, initialized, shuttingDown;
        private static int frame, phase, clickPhase, errors;
        private static float changedAt;
        private static GameObject inactive, root;
        private static GameObject iconPrefab;
        private static ProfessionUpgradePanel panel;
        private static PropUpgradeEffectAgent effect;
        private static PlayerUseItemContent use;
        private static Services services;
        private static Fixture fixture;
        private static OfflineProfessionImageResourceLoader loader;
        private static FontAssetFontService fonts;
        private static ItemIconAttachmentManager attachments;
        private static Camera camera;
        private static RenderTexture target;
        private static EventSystem events;
        private static SimulatedMouse mouse;
        private static Button click;
        private static Func<Image, IProfessionImageResourceLoader> previousImages;
        private static Func<string, string> previousLanguage;
        private static Func<TMP_FontAsset> previousFont;
        private static Transform canvasTransform;
        private static float openedAt;
        private static Func<Transform, GameObject, Transform, bool, GameObject> previousPool;
        private static IProfessionUpgradeBindings previousUpgrade;
        private static LocalizationBinaryFileReader launchLanguage, gameLanguage;
        private static ProfessionPlayerPropItem[] initialRows;
        static ProfessionUpgradePanelValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var native = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Profession/ProfessionPanel.prefab");
                Require(native && native.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "original full detail prefab has no missing MonoBehaviours");
                var original = native.GetComponentInChildren<ProfessionUpgradePanel>(true);
                Require(original && original.CostTrans && original.PropItemSlot && original.PropItemPrefab && original.AwardPropText && original.BtnUpgrade && original.UpAnimation && original.ProfessionRedDot, "all original upgrade serialized references");
                Require(original.BtnUpgrade.transform.IsChildOf(original.transform) && original.UpAnimation.transform.IsChildOf(native.transform), "original detail window owns upgrade button and separate top-level animation");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ConfigurePortraitGameView();
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true; SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Initialize()
        {
            Require(Screen.width == 1080 && Screen.height == 1920, "Game View and rendered native viewport share the same screen/safe-area dimensions");
            initialized = true;
            Application.logMessageReceived += Log;
            fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Cache, "item-icon-fixture.json")));
            var font = JsonUtility.FromJson<FontFixture>(File.ReadAllText(Path.Combine(Cache, "font-service-fixture.json")));
            var catalog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(fixture.catalog));
            catalog.Entries = catalog.Entries.Concat(AssetDatabase.LoadAssetAtPath<OfflineProfessionImageCatalog>(font.resource_catalog).Entries).ToArray();
            loader = new OfflineProfessionImageResourceLoader(catalog);
            previousImages = ProfessionImageResourceBridge.Provider; ProfessionImageResourceBridge.Provider = image => loader;
            previousPool = ProfessionGameObjectPoolBridge.Provider; ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, world) => Object.Instantiate(prefab, parent, world);
            previousLanguage = WorldMultiLanguageText.Localize;
            string languageRoot = Path.Combine(Application.streamingAssetsPath, "Map13/NativeText");
            string launchPath = Path.Combine(languageRoot, "launch-zh_CN.bytes"), gamePath = Path.Combine(languageRoot, "game-zh_CN.bytes");
            CheckBinary(launchPath, "a068ee47db184c96c8923172a104ed90b37f72e2b4adb5d73211d894d7ec9001");
            CheckBinary(gamePath, "7e8be8308de8c611097b37b116c697510827379b1d6f7aca99949d1e05832f24");
            launchLanguage = new LocalizationBinaryFileReader(launchPath); gameLanguage = new LocalizationBinaryFileReader(gamePath);
            WorldMultiLanguageText.Localize = key => launchLanguage.TryGetTextByKey(key, out var text) || gameLanguage.TryGetTextByKey(key, out text) ? text : key;
            fonts = new FontAssetFontService(loader, AssetDatabase.LoadAssetAtPath<OfflineProfessionFontConfig>(font.config_asset).Deserialize); fonts.Initialize();
            var fontConfig = JsonUtility.FromJson<LanguageFonts>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/ProfessionUI/Fonts/MultiLanguageFontConfig.json").text);
            Require(fontConfig.DefaultRegion == "CN" && fontConfig.RegionFontAssetDict.CN.Default == fontConfig.FontAssetDict.zh_CN && fontConfig.RegionFontAssetDict.CN.Fallbacks.Length == 0, "selected original CN language font configuration");
            var languageFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Native + "TextMesh Pro/Assets/Fonts & Materials/" + fontConfig.RegionFontAssetDict.CN.Default + ".asset");
            languageFont.fallbackFontAssetTable.Clear(); fonts.SetupFont(languageFont);
            previousFont = WorldMultiLanguageText.LanguageFont; WorldMultiLanguageText.LanguageFont = () => languageFont;
            new GameObject("UpgradeFixtureAudio").AddComponent<OfflineProfessionSoundHost>().Bind(AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset"));
            new GameObject("UpgradeFixtureAudioListener").AddComponent<AudioListener>();
            attachments = new ItemIconAttachmentManager(loader, () => AssetDatabase.LoadAssetAtPath<ItemIconAttachmentConfig>(Native + "ScriptableConfig/Modules/ItemIcon/ItemIconAttachmentConfig.asset"), new GAction());
            services = new Services { Config = AssetDatabase.LoadAssetAtPath<ItemIconConfig>(fixture.config), Colors = fixture.global_colors };
            previousUpgrade = ProfessionUpgradeBindings.Current; ProfessionUpgradeBindings.Current = services;
            inactive = new GameObject("UpgradeInactivePool"); inactive.SetActive(false);
            iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fixture.prefab);
            camera = new GameObject("NativeUpgradeSubtreeCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 960;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var canvas = new GameObject("NativeUpgradeSubtreeCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvasTransform = canvas.transform;
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Profession/ProfessionPanel.prefab");
            root = Object.Instantiate(original, inactive.transform, false); root.SetActive(false);
            panel = root.GetComponentInChildren<ProfessionUpgradePanel>(true); panel.Configure(services);
            effect = (PropUpgradeEffectAgent)Get(panel, "mEffectAgent");
            use = root.GetComponentInChildren<PlayerUseItemContent>(true); if (use) use.Configure(services);
            foreach (var linker in root.GetComponentsInChildren<TipDotNodeLinker>(true)) { linker.TipDotSystem = services; linker.Controller = services; }
            // Use the original serialized state configuration to activate its
            // upgrade branch. Parent detail initialization is outside this test.
            var state = Get(root.GetComponent<ProfessionPanel>(), "mStateSwitcher");
            state.GetType().GetMethod("SetState").Invoke(state, new[] { Enum.ToObject(state.GetType().GetGenericArguments()[0], 1) });
            root.transform.SetParent(canvas.transform, false);
            // Original UIUtils.Instantiate(GameObject, parent, All) reset order.
            root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
            root.SetActive(true); panel.Init(10); root.GetComponent<WorldPopupWindow>().Show(); openedAt = Time.time;
            initialRows = Rows();
            Require(initialRows.Length == 5 && initialRows.Select(r => r.myBg.enabled).SequenceEqual(new[] { true, true, false, false, true }), "zero omission and original modulo-four row backgrounds");
            Require(effect.GetPropItemCount() == 0 && services.Borrowed.Count == 2 && panel.AwardPropText.Text == "p1+10 p4+1 ", "level-one has no comparison effect; original cost/diff order and trailing space");
            foreach (var existing in Object.FindObjectsOfType<EventSystem>(true)) { existing.enabled = false; foreach (var module in existing.GetComponents<BaseInputModule>()) module.enabled = false; }
            var input = new GameObject("NativeUpgradeInput", typeof(EventSystem), typeof(StandaloneInputModule)); events = input.GetComponent<EventSystem>();
            mouse = input.AddComponent<SimulatedMouse>(); input.GetComponent<StandaloneInputModule>().inputOverride = mouse;
            click = panel.BtnUpgrade; frame = Time.frameCount;
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native upgrade validation timed out");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount <= frame + 2) return;
                frame = Time.frameCount;
                if (phase == 0) { if (Time.time - openedAt < 1) return; ValidateRedDot(); Capture("upgrade-native"); phase = 1; }
                else if (phase == 1 || phase == 3 || phase == 6)
                {
                    if (!Click()) return;
                    if (phase == 1)
                    {
                        Require(services.Claims == 1 && services.Level == 1 && !(bool)Get(panel, "mNeedEffect"), "simulated upgrade waits for success without changing state");
                        services.Pending = null; phase = 2;
                    }
                    else if (phase == 3)
                    {
                        Require(services.Claims == 2 && services.Pending != null, "second simulated request is independent after failure");
                        services.Level = 2; services.Rows.Clear(); services.Calls.Clear(); var success = services.Pending; services.Pending = null; success();
                        var rows = Rows();
                        Require(rows.Length == 6 && initialRows.All(r => rows.Contains(r)) && services.Recycles == 2, "original FIFO property reuse and cost icon recycle");
                        Require(rows.Select(r => r.myBg.enabled).SequenceEqual(new[] { true, true, false, false, true, true }), "six original background states");
                        Require(effect.GetPropItemCount() == 3 && (bool)Get(panel, "mNeedEffect") && panel.UpAnimation.isPlaying, "only changed/new nonzero properties animate after success");
                        Require(services.Calls.IndexOf("props:1") < services.Calls.IndexOf("props:2") && services.Calls.IndexOf("diff:2:3") < services.Calls.LastIndexOf("icon:301"), "source previous/current/diff/cost execution order");
                        changedAt = Time.time; phase = 4;
                    }
                    else
                    {
                        Require(services.Shortages == 1 && services.Uses == 0, "actual native use-item click routes money-only shortage");
                        services.Inventory[302] = 0; phase = 7;
                    }
                }
                else if (phase == 2)
                {
                    Require(services.Level == 1 && effect.GetPropItemCount() == 0 && !(bool)Get(panel, "mNeedEffect"), "failed request does not start property effects"); phase = 3;
                }
                else if (phase == 4)
                {
                    if (Time.time - changedAt < 1) return;
                    Require(Rows().All(r => !r.myEffect || !r.myEffect.activeSelf), "original effect coroutines finish and hide effects"); Capture("upgrade-success");
                    services.Level = 3; int borrowed = services.Borrowed.Count; panel.Init(10);
                    Require(services.Borrowed.Count == borrowed && services.Recycles == 2 && !panel.ProfessionRedDot.ShowRedDot(), "max-level branch preserves prior costs and hides red dot as original");
                    root.SetActive(false); Require(services.CountSubscribers == 0 && services.RankSubscribers == 0 && !panel.ProfessionRedDot.IsMeet.HasValue && effect.GetPropItemCount() == 0 && !(bool)Get(panel, "mNeedEffect"), "disable returns icons/properties, clears effects and all five red-dot subscriptions");
                    Require(services.OnTipDotShowChanged == null, "original tip-dot callback removed");
                    services.Level = 1; root.SetActive(true); panel.Init(10); phase = 5;
                }
                else if (phase == 5)
                {
                    Require(services.RankSubscribers == 4 && panel.ProfessionRedDot.IsShow, "reenable restores each red-dot subscription exactly once");
                    services.OnPlayerUpdate.Invoke(); Require(services.RankSubscribers == 4, "original active player reinit unsubscribes before subscribing");
                    // PlayerUseItemContent and ProfessionUpgradePanel own the
                    // same native slot in different application flows. Test
                    // the use-item owner independently after releasing upgrade.
                    root.SetActive(false); Object.DestroyImmediate(root);
                    var nativeUse = AssetDatabase.LoadAssetAtPath<GameObject>(Native + "Prefabs/UI/Profession/ProfessionPanel.prefab").GetComponentInChildren<PlayerUseItemContent>(true);
                    root = Object.Instantiate(nativeUse.gameObject, inactive.transform, false);
                    use = root.GetComponent<PlayerUseItemContent>(); use.Configure(services);
                    root.transform.SetParent(canvasTransform, false); root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity; root.transform.localScale = Vector3.one;
                    root.SetActive(true);
                    use.Init(new Dictionary<int, long> { [301] = 20, [302] = 30 }); use.SetUseItemBtnHandler(() => services.Uses++);
                    services.Inventory[301] = 1; services.Inventory[302] = 100;
                    click = use.UseItemBtn; phase = 6;
                }
                else if (phase == 7)
                {
                    if (!Click()) return;
                    Require(services.Shortages == 1 && services.Uses == 1, "other material shortage falls through to original use callback");
                    use.ClearItemIconList(); Require(use.ItemIconList.Count == 0, "source list-only clear");
                    root.SetActive(false); Object.DestroyImmediate(root); Require(services.CountSubscribers == 0 && services.RankSubscribers == 0, "destroyed original subtree releases subscriptions");
                    Require(errors == 0, "no runtime lifecycle errors");
                    Debug.Log("Original profession upgrade subtree Play Mode passed: original prefab references and native controls with verified original Chinese binary text; zero omission, modulo-four backgrounds, FIFO rows, previous/current/diff/cost order, badge/nonbadge/lock/max red-dot branches and five player subscriptions; simulated upgrade failure/deferred success, three changed-property effects and original animation, native use-item money-only shortage and fallthrough clicks, disable/re-enable/player-reinit cleanup. Configuration/player/formula/transaction are fixtures; full typed property format, detail page, popup contents, saves and Windows release remain incomplete.");
                    Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void ValidateRedDot()
        {
            var dot = panel.ProfessionRedDot;
            Require(services.RankSubscribers == 4 && dot.IsShow && dot.IsMeet == true, "original player-start init and dirty refresh");
            services.Profession = 0; Require(!dot.ShowRedDot(), "None profession short circuit"); services.Profession = 10;
            services.IsProfessionFunctionLimit = true; Require(!dot.ShowRedDot(), "function-limit short circuit"); services.IsProfessionFunctionLimit = false;
            services.Unlocked = false; Require(!dot.ShowRedDot(), "locked profession"); services.Unlocked = true;
            services.Inventory[301] = 1; services.PendingBadge = 18; Require(!dot.ShowRedDot(), "pending badge below required amount");
            services.PendingBadge = 19; Require(dot.ShowRedDot(), "unclaimed rank badges contribute only to badge shortage");
            services.Inventory[302] = 29; Require(!dot.ShowRedDot(), "nonbadge shortage still blocks");
            services.Inventory[301] = 100; services.Inventory[302] = 100; int previous = services.AwardCalculations; Require(dot.ShowRedDot() && services.AwardCalculations == previous, "sufficient inventory skips pending-badge calculation");
            services.TipVisible = false; services.OnTipDotShowChanged?.Invoke(); Require(dot.IsShow && !dot.myRedDotObj.activeSelf, "global visibility masks rendered dot without changing eligibility");
            services.TipVisible = true; services.EmitAll(); Require(dot.IsShow && dot.myRedDotObj.activeSelf, "five player events refresh original dot");
            dot.SetProfessions(new List<int> { 10 }); Require(dot.Professions.Count == 1, "original list setter");
        }
        private static bool Click()
        {
            if (clickPhase == 0)
            {
                Canvas.ForceUpdateCanvases(); mouse.Point = RectTransformUtility.WorldToScreenPoint(camera, click.transform.position);
                var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
                Require(hits.Count > 0 && (hits[0].gameObject == click.gameObject || hits[0].gameObject.transform.IsChildOf(click.transform)), "native button is unoccluded for simulated mouse");
                clickPhase = 1; return false;
            }
            if (clickPhase == 1) { mouse.Down = mouse.Held = true; clickPhase = 2; return false; }
            if (clickPhase == 2) { mouse.Down = mouse.Held = false; mouse.Up = true; clickPhase = 3; return false; }
            mouse.Up = false; clickPhase = 0; return true;
        }
        private static ProfessionPlayerPropItem[] Rows() => ((List<ProfessionPlayerPropItem>)Get(panel, "mPropItems")).ToArray();
        private static void ConfigurePortraitGameView()
        {
            var editor = typeof(UnityEditor.Editor).Assembly;
            var sizesType = editor.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), 1080, 1920, "Profession original upgrade validation" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            var viewType = editor.GetType("UnityEditor.GameView");
            var view = Resources.FindObjectsOfTypeAll(viewType).Cast<EditorWindow>().FirstOrDefault() ?? EditorWindow.GetWindow(viewType, false, "Game", false);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        }
        private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); RenderTexture.active = previous;
            Require(image.GetPixels32().Count(p => p.r > 100 && p.g > 100) > 1000, "original subtree renders on Direct3D");
            File.WriteAllBytes(ProfessionValidationWorkspace.Output(name + ".png"), image.EncodeToPNG()); Object.Destroy(image);
        }
        private static void Log(string text, string trace, LogType type) { if (!shuttingDown && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors++; }
        private static void CheckBinary(string path, string expected)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() == expected, "selected original Chinese binary hash");
        }
        private static Exception Unexpected() => new InvalidOperationException("Unexpected fixture dependency.");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Profession upgrade: " + message); }
        private static void Finish(int result)
        {
            shuttingDown = true; SessionState.SetBool(Active, false); Application.logMessageReceived -= Log;
            if (root) { root.SetActive(false); Object.DestroyImmediate(root); }
            foreach (var icon in services?.Borrowed ?? new List<ItemIcon>()) if (icon) { icon.Clear(); Object.DestroyImmediate(icon.gameObject); }
            attachments?.Dispose(); fonts?.Dispose();
            launchLanguage?.Dispose(); gameLanguage?.Dispose();
            ProfessionImageResourceBridge.Provider = previousImages; ProfessionGameObjectPoolBridge.Provider = previousPool;
            WorldMultiLanguageText.Localize = previousLanguage; WorldMultiLanguageText.LanguageFont = previousFont; ProfessionUpgradeBindings.Current = previousUpgrade;
            if (camera) camera.targetTexture = null; if (target) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(result);
        }
    }
}
