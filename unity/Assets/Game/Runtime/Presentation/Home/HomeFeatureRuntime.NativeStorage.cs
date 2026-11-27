using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.HomeHangUpUI;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Offline.HomeNativeTime;
using Zjcs.Offline.NativeSpine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeEntityRuntime;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeFruitTreeOutput;
using Zjcs.Official.HomeFruitTreeWorld;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeNativeTime;
using Zjcs.Official.HomeRelations;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeCamera;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using GAction = Zjcs.Official.StoryPlayables.GAction;
using HomeViewMode = Zjcs.Official.HomeFurniture.HomeViewMode;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private NativeFurniture storage;

        public GameObject NativeStorageInstance => storage?.GetInstance(EFurniture.StorageBox);
        public GameObject NativeFurnitureInstance(EFurniture furniture) => storage?.GetInstance(furniture);
        public IStoryNodeRegisterMgr NativeStorageStoryNodes => storage?.StoryNodes;
        internal void OpenTreasurePanelFromHome() => OpenFurniture((int)EFurniture.Desk);
        public void RefreshNativeFurnitureSkins() => storage?.RefreshSkin();
        public void SetHomeViewMode(HomeViewMode mode) => storage?.SetViewMode(mode);
        public void PublishHomeActionEvent(ActionEventData data) => storage?.PublishActionEvent(data);
        internal void SetNativeSceneUiActive(bool active) => storage?.SetSceneUiActive(active);

        private void OpenNativeStorage()
        {
            var owner = new NativeFurniture(this);
            try
            {
                owner.Open();
                storage = owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        private void CloseNativeStorage()
        {
            var owner = storage;
            storage = null;
            owner?.Dispose();
        }

        /// <summary>
        /// Shared multi-furniture owner for official native Home building prefabs,
        /// managing House slots, Spine weapon/furniture bindings, timelines, time-driven
        /// animations, TipDot linkers, NPC dialogue bubbles, conditions, and stories.
        /// </summary>
        internal sealed class NativeFurniture : IHomeFurnitureServices, IHomeFurniturePlayer,
            IHomeFurnitureViewModes, IHomeFurnitureActionEvents, IHomeWeaponAnimationBindings,
            IHomeEntityManagerContext, IHomeEntitySkinData, IHomeFurnitureChangePublisher,
            IHomeNpcBubbleTipServices, IHomePlayerUiServices, IHomeSceneUiServices,
            IHomeFruitTreeProductionServices, IHomePetTreeFeatureServices, IHomeFruitTreeOutputServices,
            IHomeHangUpFurnitureServices, IDisposable
        {
            private static readonly EFurniture[] SupportedFurniture =
            {
                EFurniture.Bed,
                EFurniture.StorageBox,
                EFurniture.DiningTable,
                EFurniture.Cart,
                EFurniture.Desk
            };

            private sealed class HangUpAwardMessage : IHomeHangUpAwardMessage
            {
                internal readonly List<OfflineHomeCollectedReward> Rewards = new List<OfflineHomeCollectedReward>();
                internal readonly ArrayList Data = new ArrayList();
                public IList DataList => Data;
                public object SourceData => this;
            }

            private sealed class EntityState
            {
                public EFurniture Type;
                public GameObject Instance;
                public readonly List<AnimationClipPlayable> Clips = new List<AnimationClipPlayable>();
                public PlayableGraph Timeline;
            }

            private sealed class StoryContextToken : IHomeFurnitureStoryContext
            {
                public string Owner;
            }

            private sealed class ConditionSubscription
            {
                public IHomeFurnitureCondition Condition;
                public bool LastMet;
                public Action Callback;
            }

            private sealed class ActionDisposable : IDisposable
            {
                private Action release;
                public ActionDisposable(Action release) => this.release = release;
                public void Dispose() { Action action = release; release = null; action?.Invoke(); }
            }

            private sealed class HomeConditionAdapter : IHomeFurnitureCondition
            {
                private readonly NativeFurniture owner;
                internal readonly HomeConditionNode Node;

                internal HomeConditionAdapter(NativeFurniture owner, HomeConditionNode node)
                {
                    this.owner = owner;
                    Node = node;
                }

                public bool IsMet() => owner.home.rules.IsMet(Node, owner.home.context);
            }

            private sealed class ConditionNoticeAdapter : IHomeFurnitureConditionNotice
            {
                private readonly NativeFurniture owner;
                private readonly IHomeFurnitureCondition condition;

                internal ConditionNoticeAdapter(NativeFurniture owner, IHomeFurnitureCondition condition)
                {
                    this.owner = owner;
                    this.condition = condition;
                }

                public Action<string, object[]> OnNoticeTxtUpdate { get; set; }

                public void Refresh()
                {
                    if (TryResolveLevelNotice(out int level))
                    {
                        OnNoticeTxtUpdate?.Invoke("<size=50>{0}</size>级解锁", new object[] { level });
                        return;
                    }
                    OnNoticeTxtUpdate?.Invoke("满足条件后解锁", Array.Empty<object>());
                }

                public bool OnUpdate(float deltaTime) => false;

                public string MessageTip()
                {
                    if (TryResolveLevelNotice(out int level))
                        return "等级达到" + level + "级后可解锁";
                    return "尚未满足解锁条件";
                }

                private bool TryResolveLevelNotice(out int level)
                {
                    level = 0;
                    if (!(condition is HomeConditionAdapter adapter) || adapter.Node == null) return false;
                    return TryFindLevelTarget(owner.home.rules, adapter.Node, out level);
                }
            }

            private readonly HomeFeatureRuntime home;
            private readonly HomeNativePrefabLoader loader = new HomeNativePrefabLoader();
            private OfflineHomePetTreeTips petTreeTips;
            private readonly StoryNodeRegisterMgr storyNodes = new StoryNodeRegisterMgr();
            private readonly GAction changed = new GAction();
            // HomeData.OnPostHomeChange belongs to switching the viewed home.
            // This owner lives for one local home; inventory/profile refreshes
            // must not restart native furniture states or their repair effects.
            private readonly GAction postHomeChanged = new GAction();
            private readonly GAction<HomeViewMode> viewModes = new GAction<HomeViewMode>();
            private readonly GAction<ActionEventData> actions = new GAction<ActionEventData>();
            private readonly GAction bubbleDialogueChanged = new GAction();
            private readonly GAction<SetHomeSceneUIActiveData> uiActiveEvents = new GAction<SetHomeSceneUIActiveData>();
            private readonly GAction<HangUpTipLevelChangeData> hangUpTipEvents = new GAction<HangUpTipLevelChangeData>();
            private readonly UnityEvent playerUpdateEvent = new UnityEvent();
            private readonly HashSet<Func<HomeFurnitureAward, bool>> awardHandlers = new HashSet<Func<HomeFurnitureAward, bool>>();
            private readonly HashSet<int> observedUnlockedHangUps = new HashSet<int>();
            private readonly Dictionary<IHomeHangUpAwardMessage, (EFurniture Furniture, List<OfflineHomeCollectedReward> Rewards)> pendingHangUpAwards =
                new Dictionary<IHomeHangUpAwardMessage, (EFurniture, List<OfflineHomeCollectedReward>)>();
            private readonly List<ConditionSubscription> conditionSubscriptions = new List<ConditionSubscription>();
            private readonly Dictionary<EFurniture, EntityState> entities = new Dictionary<EFurniture, EntityState>();
            private readonly Dictionary<GameObject, EntityState> instances = new Dictionary<GameObject, EntityState>();
            private readonly HashSet<Func<HomeFruitTreeAward, bool>> fruitTreeAwardHandlers = new HashSet<Func<HomeFruitTreeAward, bool>>();
            private readonly FruitTreeOutputItem fruitTreeOutputItem;

            private AppearanceAssetManager materials;
            private NativeSpineAppearanceBindings textures;
            private HomeNativeWeaponRackServices rackServices;
            private OfflineProfessionAppearanceBindings appearance;
            private OfflinePlayerProgress renderedPlayer;
            private string renderedWardrobe;
            private HomeNativePrefabFactory factory;
            private HomeEntityCreationCatalog tables;
            private HomeEntityPrefabPaths paths;
            private FurnitureEntityCreator creator;
            private sealed class HomeLanguageTypeService : ILanguageTypeService
            {
                public Zjcs.Official.World.NativeLabelUI.Framework.LanguageType CurrentLanguage { get; set; }
                    = Zjcs.Official.World.NativeLabelUI.Framework.LanguageType.zh_CN;
                public bool IsShowLanguageKey { get; set; }
                public bool IsShowMissingKeyText { get; set; }
                public event Action<Zjcs.Official.World.NativeLabelUI.Framework.LanguageType> OnLanguageChanged
                {
                    add { }
                    remove { }
                }
            }

            private OfflineHomeNativeTime nativeTime;
            private readonly HomeLanguageTypeService languageService = new HomeLanguageTypeService();
            private LanguageGroupConfig languageGroupConfig;
            private GameObject anchor;
            private GameObject fruitTreeInstance;
            private Transform fruitTreeSlot;
            private AudioSource clickSound;
            private OfflineProfessionImageResourceLoader npcIconLoader;
            private Dictionary<int, string> npcIconPaths;
            private HomeRelationManager relationManager;
            private HomeExperienceTables tipExperience;
            private OfflineHomeHangUpTipSource hangUpTipSource;
            private Sprite bedRewardSprite;
            private Sprite cartRewardSprite;
            private int lastPlayerLevel;
            private HomeViewMode currentViewMode = HomeViewMode.Owner;
            private Action<EFurniture> onRepairFurniture;
            private Action<long> onDebugTimeOffset;
            private bool disposed;
            private HomeNativeSnowEffectView snowEffects;

            internal NativeFurniture(HomeFeatureRuntime home)
            {
                this.home = home;
                fruitTreeOutputItem = new FruitTreeOutputItem(this);
            }
            internal IStoryNodeRegisterMgr StoryNodes => storyNodes;

            internal GameObject GetInstance(EFurniture type)
                => type == EFurniture.FruitTree ? fruitTreeInstance
                    : entities.TryGetValue(type, out EntityState state) ? state.Instance : null;

            internal bool TryGetFurnitureMaskTarget(int furnitureType, out GameObject target)
            {
                target = null;
                GameObject instance = furnitureType == (int)EFurniture.FruitTree || furnitureType == (int)EFurniture.FruitTree2 ? fruitTreeInstance
                    : entities.TryGetValue((EFurniture)furnitureType, out EntityState state) ? state.Instance : null;
                if (!instance)
                    return false;
                if (furnitureType == (int)EFurniture.FruitTree || furnitureType == (int)EFurniture.FruitTree2)
                {
                    var proxy = instance.GetComponentsInChildren<MonoBehaviour>(true)
                        .OfType<IFurnitureProxy<FurnitureBase>>().FirstOrDefault();
                    FurnitureBase component = proxy?.GetComponent((EFurniture)furnitureType);
                    if (!component) throw new InvalidOperationException("Original fruit-tree furniture proxy binding is missing: " + furnitureType);
                    target = component.gameObject;
                    return true;
                }
                Collider activeCollider = instance.GetComponentsInChildren<Collider>(true)
                    .FirstOrDefault(collider => collider != null && collider.enabled && collider.gameObject.activeInHierarchy);
                target = activeCollider != null ? activeCollider.gameObject : instance;
                return true;
            }

            internal void Open()
            {
                var assets = Resources.Load<HomeNativeStorageAssets>("Home/NativeStorageAssets");
                if (!assets || !assets.WeaponAppearance)
                    throw new InvalidOperationException("The original Home weapon appearance catalog is missing.");
                var control = assets.WeaponAppearance.Config;
                var professionTables = ProfessionAppearanceTables.Load(Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text);
                var defaults = new ProfessionAppearanceSkinDefaults(professionTables,
                    Resources.Load<TextAsset>("Combat/SceneAppearance/skin-defaults").text, Debug.LogError);
                var animations = Resources.Load<AppearanceAnimNameCombineRule>("Combat/SceneAppearance/AppearanceAnimNameCombineRule");
                var config = Resources.Load<AppearanceAssetManagerConfig>("Partners/appearance-asset-manager-config");
                renderedPlayer = home.profile.Save.Player;
                renderedWardrobe = JsonUtility.ToJson(renderedPlayer.Wardrobe);
                appearance = new OfflineProfessionAppearanceBindings(renderedPlayer.CharacterProperties.LocalActorIdentities.PlayerId,
                    () => home.profile.Save.Player, id => home.progression.Catalog.ItemType(id), professionTables, control, animations, defaults);
                materials = new AppearanceAssetManager(config, changed.Subscribe);
                textures = new NativeSpineAppearanceBindings(assets.WeaponAppearance, materials);
                rackServices = new HomeNativeWeaponRackServices(() => appearance, () => appearance,
                    new ProfessionAppearanceResolver(professionTables), professionTables, changed.Subscribe, textures, this);
                nativeTime = new OfflineHomeNativeTime(() => Time.deltaTime, () => Time.timeScale);
                relationManager = new HomeRelationManager();
                OfflineHomeHangUpTipConfiguration tipConfig = OfflineHomeHangUpTipConfiguration.LoadDefault();
                tipExperience = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
                hangUpTipSource = new OfflineHomeHangUpTipSource(home.Session, tipConfig,
                    () => DateTimeOffset.UtcNow.ToUnixTimeSeconds(), () => true,
                    () => home.profile.Save.Player.Level, () => home.CanGetHangUpExperience(tipExperience),
                    level => tipExperience.LevelExpParser.GetLevelUpExp(1, level),
                    home.GetHangUpQuickReceiveState, (_, _) => { },
                    data => hangUpTipEvents.Invoke(data))
                {
                    HasFurnitureOverride = furniture => home.HasFurnitureForSkin((int)furniture)
                };
                lastPlayerLevel = home.profile.Save.Player.Level;
                observedUnlockedHangUps.Clear();
                if (home.session?.Definitions != null)
                {
                    foreach (OfflineHomeIncomeDefinition definition in home.session.Definitions)
                        if (definition.Unlocked) observedUnlockedHangUps.Add(definition.HangUpId);
                }
                languageGroupConfig = CreateLanguageGroupConfig();
                factory = new HomeNativePrefabFactory(Install);
                tables = HomeEntityCreationCatalog.Load();
                paths = new HomeEntityPrefabPaths(tables);

                using var house = loader.LoadPrefab(paths.GetThemePrefabPath(home.rules.Data.themeSkins
                    .Single(value => value.isDefault && value.themeType == (int)HomeThemeType.House).skinItemId));
                var housePrefab = house.Result;
                if (!housePrefab || !housePrefab.TryGetComponent<HomeSlotEntityData>(out var roomSlots))
                    throw new InvalidOperationException("The official Room has no furniture slots.");

                anchor = new GameObject("NativeIndoorFurniture");
                anchor.transform.SetParent(home.scene.Root.transform, false);
                clickSound = anchor.AddComponent<AudioSource>();
                clickSound.playOnAwake = false;
                HomeAudioSettings.Register(clickSound, HomeAudioChannel.Sound);

                var slotDict = new Dictionary<EFurniture, Transform>();
                foreach (EFurniture type in SupportedFurniture)
                {
                    if (type == EFurniture.Cart)
                    {
                        var cartSlot = new GameObject("Cart").transform;
                        cartSlot.SetParent(anchor.transform, false);
                        cartSlot.localPosition = new Vector3(-8.92f, -4.51f, 0f);
                        slotDict[type] = cartSlot;
                        continue;
                    }
                    if (!roomSlots.FurnitureDict.TryGetValue(type, out Transform slot)
                        || !slot || !slot.IsChildOf(housePrefab.transform))
                        throw new InvalidOperationException("The official Room has no slot for " + type + ".");
                    slotDict[type] = ProjectSlot(slot, housePrefab.transform, anchor.transform);
                }

                creator = new FurnitureEntityCreator(slotDict, null);
                foreach (EFurniture type in SupportedFurniture)
                {
                    if (!creator.GetOrCreate(type, this))
                        throw new InvalidOperationException("The original native furniture prefab is missing: " + type);
                }
                OpenNativeFruitTree();
                snowEffects = new HomeNativeSnowEffectView(home.scene.Root.transform, this, home.environment, this);
                nativeTime.Enable();
            }

            private void OpenNativeFruitTree()
            {
                string location = GetFruitTreePrefabPath();
                using var handle = loader.LoadPrefab(location);
                if (!handle.Result)
                    throw new InvalidOperationException("The original native fruit-tree prefab is missing: " + location);
                fruitTreeSlot = HomeNativeSceneSlots.Create(anchor.transform).Find("FruitTreeSlot");
                fruitTreeInstance = factory.Instantiate(handle.Result, fruitTreeSlot, false);
                fruitTreeInstance.name = "NativeFruitTree";
                home.scene.BindNativeFruitTree(fruitTreeInstance);
            }

            internal void RefreshSkin()
            {
                if (disposed || creator == null) return;
                foreach (EFurniture type in SupportedFurniture)
                {
                    if (entities.ContainsKey(type))
                        creator.OnSkinChanged(type, GetFurnitureSkinId(type), this, out _);
                }
                RefreshNativeFruitTree();
                snowEffects?.Refresh();
            }

            private void RefreshNativeFruitTree()
            {
                if (disposed || !anchor) return;
                string location = GetFruitTreePrefabPath();
                using var handle = loader.LoadPrefab(location);
                if (!handle.Result)
                    throw new InvalidOperationException("The original native fruit-tree prefab is missing: " + location);
                if (fruitTreeInstance) Object.Destroy(fruitTreeInstance);
                fruitTreeInstance = factory.Instantiate(handle.Result, fruitTreeSlot, false);
                fruitTreeInstance.name = "NativeFruitTree";
                home.scene.BindNativeFruitTree(fruitTreeInstance);
            }

            private string GetFruitTreePrefabPath()
            {
                int skinId = GetFurnitureSkinId(EFurniture.FruitTree);
                HomeSkinWeatherOverridePrefabInfo weather = tables.GetWeatherOverride(skinId,
                    GetEnvironmentSkinId(HomeEnvironmentType.Weather));
                return weather == null ? paths.GetFurniturePrefabPath(skinId)
                    : paths.GetFurniturePrefabPath(skinId, weather.PrefabName);
            }

            internal void SetViewMode(HomeViewMode mode)
            {
                currentViewMode = mode;
                viewModes.Invoke(mode);
            }

            internal void SetSceneUiActive(bool isActive)
                => uiActiveEvents.Invoke(new SetHomeSceneUIActiveData { IsActive = isActive });

            internal void PublishActionEvent(ActionEventData data) => actions.Invoke(data);

            internal void NotifyRepaired(EFurniture furniture)
            {
                onRepairFurniture?.Invoke(furniture);
                hangUpTipSource?.NotifyRepaired(furniture);
                NotifyHomeChanged();
            }

            internal void NotifyHangUpUnlock(int hangUpId)
            {
                if (disposed) return;
                observedUnlockedHangUps.Add(hangUpId);
                hangUpTipSource?.NotifyUnlocked(hangUpId);
                NotifyHomeChanged();
            }

            internal void NotifyHangUpLevelUp(int hangUpId, int level)
            {
                if (disposed) return;
                hangUpTipSource?.NotifyDataChanged(hangUpId);
                NotifyHomeChanged();
            }

            internal LanguageGroupConfig NativeLanguageGroupConfig => languageGroupConfig;
            internal ILanguageTypeService NativeLanguageTypeService => languageService;
            internal IHomeFurnitureServices FurnitureServices => this;
            internal IHomeFurnitureCondition CreateCondition(HomeConditionNode node)
                => new HomeConditionAdapter(this, node);

            internal void NotifyHangUpDataChanged(int hangUpId)
            {
                if (disposed) return;
                hangUpTipSource?.NotifyDataChanged(hangUpId);
                NotifyHomeChanged();
            }

            internal void NotifyFastForward(int fastHangUpId)
            {
                if (disposed) return;
                OnFastHangUp?.Invoke(fastHangUpId);
                HomeFastHangUpRecord fast = home.rules.FastHangUp(fastHangUpId);
                if (fast?.hangUpIdList != null)
                {
                    foreach (int hangUpId in fast.hangUpIdList)
                        hangUpTipSource?.NotifyDataChanged(hangUpId);
                }
                NotifyHomeChanged();
            }

            internal void NotifyDialogueChanged()
            {
                if (disposed) return;
                bubbleDialogueChanged.Invoke();
                NotifyHomeChanged();
            }

            internal void NotifyHomeChanged()
            {
                if (disposed) return;
                int playerLevel = home.profile.Save.Player.Level;
                if (playerLevel != lastPlayerLevel)
                {
                    lastPlayerLevel = playerLevel;
                    hangUpTipSource?.NotifyLevelChanged(playerLevel);
                }
                if (home.session?.Definitions != null)
                {
                    foreach (OfflineHomeIncomeDefinition definition in home.session.Definitions)
                    {
                        if (definition.Unlocked && observedUnlockedHangUps.Add(definition.HangUpId))
                            hangUpTipSource?.NotifyUnlocked(definition.HangUpId);
                    }
                }
                foreach (ConditionSubscription entry in conditionSubscriptions.ToArray())
                {
                    bool met = entry.Condition == null || entry.Condition.IsMet();
                    if (met == entry.LastMet) continue;
                    entry.LastMet = met;
                    entry.Callback?.Invoke();
                }
                changed.Invoke();
            }

            private static Transform ProjectSlot(Transform slot, Transform house, Transform parent)
            {
                if (slot != house) parent = ProjectSlot(slot.parent, house, parent);
                var node = new GameObject(slot.name).transform;
                node.SetParent(parent, false);
                node.SetLocalPositionAndRotation(slot.localPosition, slot.localRotation);
                node.localScale = slot.localScale;
                return node;
            }

            private void Install(GameObject root)
            {
                foreach (var ui in root.GetComponentsInChildren<HomePlayerUIBase>(true))
                    ui.PlayerServices = this;
                var achievementHints = root.GetComponentsInChildren<Zjcs.Official.HomePetGachaUI.AchievementRedDotItem>(true);
                var materialHints = root.GetComponentsInChildren<Zjcs.Official.HomePetGachaUI.GachaBlueDotBase>(true);
                if (achievementHints.Length + materialHints.Length > 0 && petTreeTips == null)
                {
                    var limits = new Zjcs.Offline.HomeUI.OfflineHomeFunctionLimits(
                        Zjcs.Offline.HomeUI.OfficialCurrentHomeRuleAssetCatalog.LoadDefault(),
                        home.partnerCatalog.Conditions, () => home.furnitureConditionPlayer,
                        Zjcs.Offline.State.OfflineOpeningPolicy.Evaluate);
                    petTreeTips = new OfflineHomePetTreeTips(home.petGacha,
                        () => limits.IsLimited(136), chest => limits.IsLimited(5, chest), home.profile.InventoryCount);
                }
                foreach (var hint in achievementHints) hint.AchievementServices = petTreeTips;
                foreach (var hint in materialHints) hint.GachaServices = petTreeTips;
                foreach (var tree in root.GetComponentsInChildren<HomeFruitTree>(true))
                {
                    tree.FurnitureServices = this;
                    tree.Localize = LocalizeUiText;
                }
                foreach (var dayNightSwitcher in root.GetComponentsInChildren<HomeSpineDayNightSwitcher>(true))
                    dayNightSwitcher.EnvironmentServices = home.environment;
                foreach (var produce in root.GetComponentsInChildren<HomeFruitTreeProduceFruit>(true))
                    produce.ProductionServices = this;
                foreach (var pet in root.GetComponentsInChildren<HomeFruitTreePet>(true))
                    pet.PetTreeFeatureServices = this;
                foreach (var output in root.GetComponentsInChildren<HomeFruitTreeHangUpHandlerFruitItem>(true))
                    output.OutputServices = this;
                foreach (var furniture in root.GetComponentsInChildren<FurnitureBase>(true))
                {
                    furniture.Services = this;
                    if (furniture is BaseHangUpFurniture hangUp)
                        hangUp.HangUpServices = this;
                    EFurniture type = furniture.Furniture;
                    furniture.OnClickAction.AddListener(() => home.FurnitureClickedForTutorial?.Invoke((int)type));
                }
                foreach (var mutex in root.GetComponentsInChildren<HomeMutex>(true))
                    mutex.InjectRelationManager = relationManager;
                foreach (var clickable in root.GetComponentsInChildren<ClickableHomeObj>(true))
                    home.nativeSceneInput.Bind(clickable);
                foreach (var popClick in root.GetComponentsInChildren<DiningTableNpcPopClick>(true))
                    popClick.Services = this;
                foreach (var bubble in root.GetComponentsInChildren<HomeNpcBubbleTip>(true))
                {
                    bubble.PlayerServices = this;
                    bubble.FurnitureServices = this;
                    bubble.BubbleServices = this;
                    bubble.AnimationBindings = this;
                }
                foreach (var notice in root.GetComponentsInChildren<HomeNotice>(true))
                    notice.Services = this;
                foreach (var dayNight in root.GetComponentsInChildren<HomeDayNight>(true))
                    dayNight.Bind(home.environment);
                foreach (var rack in root.GetComponentsInChildren<HomeWeaponRack>(true))
                    rack.Services = rackServices;
                foreach (var mode in root.GetComponentsInChildren<HomeViewModeControl>(true))
                    mode.ViewModes = this;
                foreach (var action in root.GetComponentsInChildren<HomeActionEventHandler>(true))
                    action.ActionEvents = this;
                foreach (var screen in root.GetComponentsInChildren<HomeScreenShootActive>(true))
                    screen.Services = this;
                foreach (var anim in root.GetComponentsInChildren<HomeTimeDriveAnimationElement>(true))
                    anim.Publisher = nativeTime.Events;
                foreach (var skel in root.GetComponentsInChildren<HomeTimeDriveSkeletonElement>(true))
                    skel.Publisher = nativeTime.Events;
                foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                {
                    if (text.FontProvider == null) text.FontProvider = () => home.catalog.Font;
                    if (text.Localizer == null) text.Localizer = LocalizeUiText;
                }
                foreach (var tmpController in root.GetComponentsInChildren<MultiLanguageTMPControler>(true))
                {
                    tmpController.LanguageGroupConfig = languageGroupConfig;
                    tmpController.LanguageTypeService = languageService;
                }
                foreach (var node in root.GetComponentsInChildren<Zjcs.Official.StoryNodes.StoryNodeRegister>(true))
                    node.NodeRegisterManager = storyNodes;
                foreach (var node in root.GetComponentsInChildren<Zjcs.Official.ProfessionUI.StoryNodeRegister>(true))
                    node.Init(storyNodes);
                foreach (var sound in root.GetComponentsInChildren<EntityClickSound>(true))
                {
                    sound.InputOperationModule = home.nativeSceneInput.InputModule;
                    sound.Post = (clip, _) => { if (clip && clickSound) clickSound.PlayOneShot(clip); };
                }
                home.PartnerTipDots?.BindNativeTree(root);
                home.BindBagTipNativeTree(root);
                home.BindHomeTipNativeTree(root);
            }

            private string LocalizeUiText(string key)
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;
                if (WorldMultiLanguageText.Localize != null)
                {
                    string localized = WorldMultiLanguageText.Localize(key);
                    if (!string.IsNullOrEmpty(localized) && localized != key) return localized;
                }
                return home.catalog != null ? home.catalog.LocalizedText(key, key) : key;
            }

            private static void SetupTimeline(EntityState state)
            {
                if (state.Timeline.IsValid()) state.Timeline.Destroy();
                state.Clips.Clear();
                var clipBind = state.Instance ? state.Instance.GetComponent<HomePlayableClipBind>() : null;
                if (clipBind == null || clipBind.ClipDatas == null || clipBind.ClipDatas.Count == 0) return;
                state.Timeline = PlayableGraph.Create("HomeFurnitureClips_" + state.Type);
                state.Timeline.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                foreach (var binding in clipBind.ClipDatas)
                {
                    if (!binding.Clip || !binding.Animator) continue;
                    var clip = AnimationClipPlayable.Create(state.Timeline, binding.Clip);
                    clip.SetSpeed(0);
                    var output = AnimationPlayableOutput.Create(state.Timeline, binding.Clip.name, binding.Animator);
                    output.SetSourcePlayable(clip);
                    state.Clips.Add(clip);
                }
                state.Timeline.Play();
            }

            internal void Tick(float dayPhase)
            {
                if (disposed) return;
                petTreeTips?.Synchronize();
                var next = home.profile.Save.Player;
                bool playerReplaced = !ReferenceEquals(renderedPlayer, next);
                if (playerReplaced)
                {
                    renderedPlayer = next;
                    string wardrobe = JsonUtility.ToJson(next.Wardrobe);
                    if (wardrobe != renderedWardrobe)
                    {
                        renderedWardrobe = wardrobe;
                        appearance.Refresh(AppearanceOperateSource.Normal);
                    }
                    NotifyHomeChanged();
                }
                else if (next.Level != lastPlayerLevel)
                {
                    lastPlayerLevel = next.Level;
                    hangUpTipSource?.NotifyLevelChanged(lastPlayerLevel);
                }
                nativeTime?.Tick();
                foreach (EntityState state in entities.Values)
                {
                    if (!state.Instance || !state.Instance.activeInHierarchy || !state.Timeline.IsValid()) continue;
                    foreach (var clip in state.Clips) clip.SetTime(dayPhase * clip.GetAnimationClip().length);
                    state.Timeline.Evaluate();
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                petTreeTips?.Dispose();
                snowEffects?.Dispose();
                snowEffects = null;
                creator?.ReleaseAll(this);
                foreach (EntityState state in entities.Values)
                {
                    if (state.Timeline.IsValid()) state.Timeline.Destroy();
                    state.Clips.Clear();
                }
                entities.Clear();
                instances.Clear();
                conditionSubscriptions.Clear();
                pendingHangUpAwards.Clear();
                observedUnlockedHangUps.Clear();
                fruitTreeAwardHandlers.Clear();
                HomeAudioSettings.Unregister(clickSound);
                clickSound = null;
                if (anchor) { anchor.SetActive(false); Object.Destroy(anchor); }
                ReleaseAfterInstances(anchor).Forget();
            }

            private async UniTask ReleaseAfterInstances(GameObject root)
            {
                while (root) await UniTask.NextFrame();
                nativeTime?.Dispose();
                nativeTime = null;
                factory?.Dispose();
                rackServices?.Dispose();
                textures?.Dispose();
                materials?.Dispose();
                if (languageGroupConfig)
                {
                    Object.Destroy(languageGroupConfig);
                    languageGroupConfig = null;
                }
                loader.Dispose();
            }

            private static LanguageGroupConfig CreateLanguageGroupConfig()
            {
                var config = ScriptableObject.CreateInstance<LanguageGroupConfig>();
                config.LanguageGroupMappingDict = new Zjcs.Official.World.NativeMap13Labels.MonoDictionary<LanguageGroup, List<Zjcs.Official.World.NativeLabelUI.Framework.LanguageType>>
                {
                    { LanguageGroup.Zh, new List<Zjcs.Official.World.NativeLabelUI.Framework.LanguageType> {
                        Zjcs.Official.World.NativeLabelUI.Framework.LanguageType.zh_CN,
                        Zjcs.Official.World.NativeLabelUI.Framework.LanguageType.zh_TW } },
                    { LanguageGroup.West, new List<Zjcs.Official.World.NativeLabelUI.Framework.LanguageType> {
                        Zjcs.Official.World.NativeLabelUI.Framework.LanguageType.en_US } }
                };
                return config;
            }

            public IHomeEntityResourceLoader ResourceLoader => loader;
            public IHomeFurnitureChangePublisher FurnitureChangePublisher => this;
            IHomeEntitySkinData IHomeEntityManagerContext.PlayerData => this;
            public IHomeEntityPrefabTables Tables => tables;
            public HomeEntityPrefabPaths PrefabPaths => paths;
            public int PreviewWeatherId => 0;
            public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
                => factory.Instantiate(prefab, parent, worldPositionStays);

            public void OnEntityWillDestroy(GameObject go)
            {
                if (!go || !instances.TryGetValue(go, out EntityState state)) return;
                foreach (var dayNight in go.GetComponentsInChildren<HomeDayNight>(true))
                    dayNight.Bind(null);
                if (state.Timeline.IsValid()) state.Timeline.Destroy();
                state.Clips.Clear();
                instances.Remove(go);
                if (entities.TryGetValue(state.Type, out EntityState current) && current == state)
                    entities.Remove(state.Type);
            }

            public void OnEntityCreated(GameObject go)
            {
                FurnitureBase furniture = go.GetComponent<FurnitureBase>();
                if (!furniture)
                    throw new InvalidOperationException("Native furniture prefab has no FurnitureBase controller: " + go.name);
                var state = new EntityState { Type = furniture.Furniture, Instance = go };
                entities[state.Type] = state;
                instances[go] = state;
                SetupTimeline(state);
                home.scene.BindNativeFurniture((int)state.Type, go);
            }

            public int GetFurnitureSkinId(EFurniture type)
            {
                var defaultSkin = home.rules.Data.furnitureSkins.First(value => value.furnitureType == (int)type && value.isDefault);
                return home.session.SkinOf(0, (int)type)?.SkinItemId ?? defaultSkin.skinItemId;
            }

            public int GetThemeSkinId(HomeThemeType type)
            {
                var defaultSkin = home.rules.Data.themeSkins.First(value => value.themeType == (int)type && value.isDefault);
                return home.session.SkinOf(1, (int)type)?.SkinItemId ?? defaultSkin.skinItemId;
            }

            public int GetEnvironmentSkinId(HomeEnvironmentType type)
            {
                var defaultSkin = home.rules.Data.environmentSkins.FirstOrDefault(value => value.environmentType == (int)type && value.isDefault);
                return home.session.SkinOf(2, (int)type)?.SkinItemId ?? defaultSkin?.skinItemId ?? 0;
            }

            public void Publish(HomeFurnitureChangeData data) => changed.Invoke();

            public bool InMyHome => true;
            public IHomeFurniturePlayer Player => this;
            public IHomeFurniturePlayerData PlayerData => this;
            public HomeViewMode HomeViewMode => currentViewMode;
            public IDisposable Subscribe(Action<HomeViewMode> action) => viewModes.Subscribe(action);
            public IDisposable Subscribe(Action<ActionEventData> action) => actions.Subscribe(action);
            public bool HasFurniture(EFurniture furniture) => home.HasFurnitureForSkin((int)furniture);
            public bool CheckFunctionDisabled(EFurniture furniture) => home.hud.IsClosedByNetworkPolicy((int)furniture);
            public void PopupPlayerBag() => home.hud.ShowStorage();
            public void PopupTreasurePanel() => home.OpenTreasurePanelFromHome();
            public void PopupRepair(EFurniture furniture, bool isBuild) => home.hud.ShowRepair((int)furniture, isBuild);
            public void RegisterAwardHandler(Func<HomeFurnitureAward, bool> handler) => awardHandlers.Add(handler);
            public void RemoveAwardHandler(Func<HomeFurnitureAward, bool> handler) => awardHandlers.Remove(handler);
            public IDisposable SubscribePostHomeChange(Action action) => postHomeChanged.Subscribe(action);
            public void ShowError(int textId) => home.hud.ShowNotice(home.catalog.LocalizedText("TextError." + textId, textId.ToString()));
            public void SystemMessage(string message) => home.hud.ShowNotice(message);
            public bool IsFunctionLimit(int functionType) => home.rules.IsFunctionLimited(functionType);
            public void PopupNpcPanel(int defaultTab = 0) => home.OpenPartnerTab((NpcPanelTabType)defaultTab);
            public DateTime GetDateTime() => DateTime.UtcNow.AddHours(8);
            public DateTime GetDailyResetTimeFromDate(DateTime dateTime) => OfflineRefreshClock.DayStart(dateTime);
            public void RefreshDailyDialogues(bool invokeByPlayer) => home.RefreshPartnerDerivedState();
            public bool HasStartedStory(string name) => home.StoryStatus(name) > 0;
            public void DoStoryAction(Action action) => action?.Invoke();

            public bool AwardManagerAvailable => !disposed;
            public int FruitId => OfflineFruitTreeService.FruitItemId;
            public void RegisterAwardHandler(Func<HomeFruitTreeAward, bool> handler) => fruitTreeAwardHandlers.Add(handler);
            public void RemoveAwardHandler(Func<HomeFruitTreeAward, bool> handler) => fruitTreeAwardHandlers.Remove(handler);
            public void CollectFruitTreeReward(Action<object, int> completed)
            {
                if (disposed) { completed?.Invoke(null, 1); return; }
                completed?.Invoke(null, home.OnFruitTreeClicked() ? 0 : 1);
            }
            public void PopupAward(HomeFruitTreeAward award)
            {
                if (!(award?.Original is FruitTreeResult result) || !result.IsApplied)
                    throw new InvalidOperationException("The committed fruit-tree award payload is missing.");
                home.hud.ShowHomeAward(OfflineFruitTreeService.RewardItemId, result.CollectedCount);
                home.hud.RefreshAll();
            }
            internal void PresentFruitTreeAward(FruitTreeResult result)
            {
                var award = new HomeFruitTreeAward {
                    SourceOperate = 22, // Original SourceOperate.FruitTree.
                    AwardIds = new[] { OfflineFruitTreeService.RewardItemId }, Original = result };
                foreach (var handler in fruitTreeAwardHandlers.ToArray())
                    if (handler(award)) return;
                throw new InvalidOperationException("The original fruit-tree award handler is not registered.");
            }
            public bool IsPetGachaDisabled() => false;
            public void PopupPetGacha() => home.hud.ShowPetGacha();
            public bool HasPlayer => !disposed && home.profile != null;
            public int RandomRange(int minInclusive, int maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive);
            public long GetItemAmount(int itemId) => itemId == FruitId
                ? home.fruitTree.State.StoredCount : home.profile.InventoryCount(itemId);
            public IHomeFruitTreeOutputItem GetItemById(int itemId)
                => itemId == FruitId ? fruitTreeOutputItem : null;
            public IEnumerable<IHomeFruitTreeOutputItem> FindItemsById(int itemId)
            {
                if (itemId == FruitId) yield return fruitTreeOutputItem;
            }
            public EFurniture FastHangUpFurniture(int fastHangUpId)
                => (EFurniture)(home.rules.FastHangUp(fastHangUpId)?.bindFurniture ?? 0);
            public double CalcElapsedSeconds(DateTime since)
                => Math.Max(0.0, (DateTime.UtcNow - since.ToUniversalTime()).TotalSeconds);
            public void ShowFruitNotRipe(TimeSpan remaining)
            {
                long seconds = Math.Max(0L, (long)remaining.TotalSeconds);
                string duration = seconds < 60 ? seconds + " 秒"
                    : seconds < 3600 ? seconds / 60 + " 分钟"
                    : seconds / 3600 + " 小时 " + seconds % 3600 / 60 + " 分钟";
                home.hud.ShowNotice("下一颗果实将在 " + duration + " 后成熟");
            }
            public event Action<int> OnFastHangUp;
            public GameObject LoadEffectPrefab(string assetGuid) => HomeFruitTreePrefabCatalog.LoadDefault().Load(assetGuid);
            public GameObject InstantiateEffect(GameObject prefab, Transform parent) => factory.Instantiate(prefab, parent, false);

            private sealed class FruitTreeOutputItem : IHomeFruitTreeOutputItem
            {
                private readonly NativeFurniture owner;
                internal FruitTreeOutputItem(NativeFurniture owner) => this.owner = owner;
                private OfflineFruitTreeProgress State => owner.home.fruitTree.State;
                public long Count => State.StoredCount;
                public double Decimal => State.FractionalCount;
                public double IncreaseSpeed => owner.home.fruitTree.SpeedPerHourAt(HomeFeatureRuntime.Now());
                public DateTime LastUpdateTime => DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, State.LastUpdateUnixSeconds)).UtcDateTime;
            }

            public FurnitureInfo GetFurnitureInfo(EFurniture furniture)
            {
                HomeFurnitureRecord row = home.rules.FurnitureOf((int)furniture);
                return new FurnitureInfo
                {
                    RepairNoticeCondition = new HomeConditionAdapter(this, row?.repairNotice),
                    UnlockCondition = new HomeConditionAdapter(this, row?.unlock),
                    RepairStoryName = row?.repairStory ?? string.Empty
                };
            }

            public bool IsMetFurnitureRepairCondition(FurnitureInfo furniture) => furniture?.UnlockCondition?.IsMet() ?? true;
            public bool IsMetFurnitureNoticeCondition(FurnitureInfo furniture) => furniture?.RepairNoticeCondition?.IsMet() ?? true;

            public IDisposable SubscribeCondition(IHomeFurnitureCondition condition, Action action)
            {
                if (action == null) return new ActionDisposable(null);
                var entry = new ConditionSubscription
                {
                    Condition = condition,
                    LastMet = condition == null || condition.IsMet(),
                    Callback = action
                };
                conditionSubscriptions.Add(entry);
                return new ActionDisposable(() => conditionSubscriptions.Remove(entry));
            }

            public IHomeFurnitureConditionNotice CreateConditionNotice(IHomeFurnitureCondition condition)
                => new ConditionNoticeAdapter(this, condition);

            public IHomeFurnitureStoryContext CreateStoryContext(string owner)
                => new StoryContextToken { Owner = owner };

            public void PlayStory(IHomeFurnitureStoryContext context, string name)
                => home.TryPlayFurnitureStory(name);

            public void DisposeStory(IHomeFurnitureStoryContext context) { }

            public event Action<EFurniture> OnRepairFurniture
            {
                add => onRepairFurniture += value;
                remove => onRepairFurniture -= value;
            }

            public event Action<long> OnDebugTimeOffset
            {
                add => onDebugTimeOffset += value;
                remove => onDebugTimeOffset -= value;
            }

            public Spine.Animation GetOrLoadSpineAnimation(ISkeletonComponent component, string name)
                => component != null && component.SkeletonDataAsset && !string.IsNullOrEmpty(name)
                    ? component.SkeletonDataAsset.GetSkeletonData(true).FindAnimation(name) : null;

            public void UpdateAnimation(SkeletonRenderer renderer)
            {
                if (!renderer) return;
                if (renderer.TryGetComponent<NativeSpineAppearanceAdapter>(out var adapter))
                    adapter.UpdateAnimationAndMesh();
                else if (renderer is SkeletonAnimation skeletonAnimation)
                {
                    skeletonAnimation.ApplyAnimation();
                    skeletonAnimation.LateUpdateMesh();
                }
                else renderer.LateUpdateMesh();
            }

            public void UpdateAnimation(SkeletonGraphic graphic) { graphic.ApplyAnimation(); graphic.UpdateMesh(); }

            IDisposable IHomeNpcBubbleTipServices.SubscribeDialogueChanged(Action action) => bubbleDialogueChanged.Subscribe(action);
            IDisposable IHomeNpcBubbleTipServices.SubscribePostHomeChange(Action action) => postHomeChanged.Subscribe(action);

            bool IHomeNpcBubbleTipServices.TryFindActiveDialogueNpcClassId(out int npcClassId)
            {
                npcClassId = 0;
                OfflinePartnerConversationUiSession conversations = home.PartnerConversations;
                if (conversations == null) return false;
                var playback = conversations.Playback;
                var comparer = Comparer<PartnerDialogueData>.Create(PartnerDialogueListRules.CompareDialogues);
                foreach (PartnerDialogueData dialogue in playback.Dialogues.Values.OrderBy(value => value, comparer))
                {
                    if (!PartnerDialogueListRules.HasRedDot(dialogue, playback)) continue;
                    if (playback.Npcs.TryGetValue(dialogue.NpcId, out PartnerDialogueNpcData npc) && npc.NpcClassId > 0)
                    {
                        npcClassId = npc.NpcClassId;
                        return true;
                    }
                }
                return false;
            }

            string IHomeNpcBubbleTipServices.GetNpcIconPath(int npcClassId)
            {
                if (npcIconPaths == null)
                {
                    npcIconPaths = new Dictionary<int, string>();
                    foreach (var row in home.PartnerCatalog.Rows("npc"))
                    {
                        int id = Convert.ToInt32(row["Id"]);
                        npcIconPaths[id] = Convert.ToString(row["Icon"]) ?? string.Empty;
                    }
                }
                return npcIconPaths.TryGetValue(npcClassId, out string path) ? path : string.Empty;
            }

            Sprite IHomeNpcBubbleTipServices.LoadNpcIcon(string iconPath)
            {
                if (string.IsNullOrEmpty(iconPath)) return null;
                if (npcIconLoader == null)
                {
                    var catalog = Resources.Load<OfflineProfessionImageCatalog>("Partners/OfficialPartnerNpcIconImages");
                    if (!catalog) throw new InvalidOperationException("Official NPC icon catalog is not packaged.");
                    npcIconLoader = new OfflineProfessionImageResourceLoader(catalog);
                }
                return npcIconLoader.LoadAsset<Sprite>(iconPath).Result;
            }

            void IHomeNpcBubbleTipServices.UnloadNpcIcon(string iconPath) { }

            bool IHomePlayerUiServices.HasPlayer => home.profile?.Save?.Player != null;
            bool IHomePlayerUiServices.ManagerAvailable => true;
            UnityEvent IHomePlayerUiServices.OnPlayerUpdate => playerUpdateEvent;
            IProfessionTipDotSystem IHomePlayerUiServices.TipDotSystem => home.PartnerTipDots?.System;

            IHomeDirectionCameraServices IHomeSceneUiServices.Camera => null;
            IDragControllerManager IHomeSceneUiServices.DragControllerManager => null;
            float IHomeSceneUiServices.DirectionGuideFocusDuration => 0f;
            IHomeDirectionFurniture IHomeSceneUiServices.GetFurniture(EFurniture type) => null;
            IDisposable IHomeSceneUiServices.SubscribeHangUpTips(Action<HangUpTipLevelChangeData> callback) => hangUpTipEvents.Subscribe(callback);
            UnityEvent IHomeSceneUiServices.OnHomeNpcRefresh => null;
            List<IHomeDirectionNpcData> IHomeSceneUiServices.HomeNpcDatas => null;
            int IHomeSceneUiServices.AwardRetrieveHomeNpcId => 0;
            Transform IHomeSceneUiServices.GetSpawnPoint(int index) => null;
            HomeDirectionNpcInfo IHomeSceneUiServices.GetNpcInfo(int id) => null;
            IHomeMenuPrefabHandle IHomeSceneUiServices.LoadPrefab(string path) => null;
            GameObject IHomeSceneUiServices.InstantiatePrefab(GameObject prefab, Transform slot) => null;
            void IHomeSceneUiServices.FreezeSkeletonGraphic(GameObject instance) { }
            Zjcs.Official.HomeSceneUiControls.HomeViewMode IHomeSceneUiServices.HomeViewMode
                => (Zjcs.Official.HomeSceneUiControls.HomeViewMode)(int)currentViewMode;
            IDisposable IHomeSceneUiServices.SubscribeUiActive(Action<SetHomeSceneUIActiveData> callback) => uiActiveEvents.Subscribe(callback);

            IHomeHangUpTipServices IHomeHangUpFurnitureServices.Tips => hangUpTipSource;

            Sprite IHomeHangUpFurnitureServices.LoadAwardSprite(EFurniture furniture)
            {
                if (furniture == EFurniture.Bed)
                {
                    if (!bedRewardSprite)
                        bedRewardSprite = OfficialHomeHangUpWindowAssetCatalog.LoadDefault().bedRewardImage;
                    return bedRewardSprite;
                }
                if (furniture == EFurniture.Cart)
                {
                    if (!cartRewardSprite && home.CartAssets != null)
                        home.CartAssets.TryGetSprite("UI/Furniture/ImgItem_Cart_0", out cartRewardSprite);
                    return cartRewardSprite;
                }
                return null;
            }

            void IHomeHangUpFurnitureServices.CollectHangUpRewards(EFurniture furniture, IReadOnlyList<int> hangUpIds,
                Action<IHomeHangUpAwardMessage, int> completed)
            {
                var message = new HangUpAwardMessage();
                if (hangUpIds != null)
                {
                    for (int i = 0; i < hangUpIds.Count; i++)
                    {
                        OfflineHomeCollectedReward reward = home.PrepareHomeCollection(hangUpIds[i]);
                        if (reward != null && reward.Count > 0)
                        {
                            message.Rewards.Add(reward);
                            message.Data.Add(reward);
                        }
                    }
                }
                pendingHangUpAwards[message] = (furniture, message.Rewards);
                completed?.Invoke(message, 0);
            }

            void IHomeHangUpFurnitureServices.AwardCollected(IList dataList, object sourceData)
                => home.AwardPreparedHomeCollections();

            void IHomeHangUpFurnitureServices.MergeAndPopupAward(EFurniture furniture, IHomeHangUpAwardMessage response)
            {
                if (response == null || !pendingHangUpAwards.TryGetValue(response, out var entry)) return;
                if (furniture == EFurniture.Cart)
                {
                    var cartRewards = new List<OfflineHomeCollectedReward>();
                    foreach (OfflineHomeCollectedReward reward in entry.Rewards)
                        if (reward != null && reward.Count > 0)
                            cartRewards.Add(reward);
                    if (cartRewards.Count > 0)
                        home.Hud.ShowCartAwards(cartRewards);
                    return;
                }
                foreach (OfflineHomeCollectedReward reward in entry.Rewards)
                {
                    if (reward != null && reward.Count > 0 && (furniture == EFurniture.Bed || reward.HangUpId == 1))
                        home.Hud.ShowBedAward(reward);
                }
            }

            void IHomeHangUpFurnitureServices.PublishHangUpCollect(EFurniture furniture, IReadOnlyList<int> hangUpIds)
            {
                var matched = pendingHangUpAwards
                    .Where(kv => kv.Value.Furniture == furniture)
                    .ToArray();
                foreach (var entry in matched)
                    pendingHangUpAwards.Remove(entry.Key);

                var notifiedIds = new HashSet<int>();
                foreach (var entry in matched)
                {
                    foreach (OfflineHomeCollectedReward reward in entry.Value.Rewards)
                    {
                        if (reward == null || reward.Count <= 0) continue;
                        home.PublishHomeCollection(reward);
                        notifiedIds.Add(reward.HangUpId);
                    }
                }
                if (hangUpIds != null)
                {
                    for (int i = 0; i < hangUpIds.Count; i++)
                    {
                        if (notifiedIds.Add(hangUpIds[i]))
                        {
                            hangUpTipSource?.NotifyDataChanged(hangUpIds[i]);
                            home.HangUpHud?.Source.NotifyDataChanged(hangUpIds[i]);
                        }
                    }
                }
                home.SynchronizeHomeTipDots();
            }

            void IHomeHangUpFurnitureServices.PopupFurnitureWindow(EFurniture furniture)
                => home.OpenFurniture((int)furniture);

            void IHomeHangUpFurnitureServices.OnTipLevelUpdated(EFurniture furniture, int tipLevel) { }
        }
    }
}
