using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeFunctionMenuUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.Partners;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private OfflineHomeFunctionMenu nativeFunctionMenu;
        private OfflineHomeHudConditions nativeMenuConditions;
        private OfflineHomeFunctionMenuPlayer nativeMenuPlayer;
        private NativeFunctionMenuServices nativeMenuServices;
        private NativeMenuConditionLifetime nativeMenuConditionLifetime;
        private FontAssetFontService nativeMenuFonts;
        private AudioSource nativeMenuSound;
        private int menuLevel, menuSubRank;
        private string menuInventory, menuMaps, menuStories, menuProfessions, menuFurniture, menuNpcs;

        private void BuildNativeFunctionMenu()
        {
            try
            {
                var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
                int firstTrace = (int)OfflineHomeMainlineTaskData.LoadDefault().CopyTaskSettings()["TaskFirstTraceConditionId"];
                nativeMenuConditions = new OfflineHomeHudConditions(host.Profile, configuration,
                    OfficialPartnerAssetCatalog.LoadDefault().Conditions, firstTrace, host.HasFurnitureForSkin,
                    () => false, () => false);
                nativeMenuConditionLifetime = new NativeMenuConditionLifetime(nativeMenuConditions);
                nativeMenuPlayer = new OfflineHomeFunctionMenuPlayer(host.Profile, nativeMenuConditions);
                nativeMenuServices = new NativeFunctionMenuServices(this, new OfflineHomeFunctionMenuConfiguration());
                var hudAssets = OfflineHomeHudAssets.LoadDefault();
                nativeMenuFonts = hudAssets.CreateFontService();
                nativeMenuFonts.Initialize();
                nativeFunctionMenu = new OfflineHomeFunctionMenu(canvas.transform, new HomeFunctionMenuBindings {
                    Camera = host.Scene.Camera, Screen = nativeFrameScreen, Menu = nativeMenuServices,
                    Activities = nativeMenuServices, Wiki = nativeMenuServices, Download = nativeMenuServices,
                    Time = nativeMenuServices, Font = hudAssets.defaultFont, PostSound = PlayNativeMenuSound });
                functionsPanel = nativeFunctionMenu.Root;
                CaptureNativeMenuPlayer();
            }
            catch { ReleaseNativeFunctionMenu(); throw; }
        }

        private void CaptureNativeMenuPlayer()
        {
            menuLevel = host.Profile.Save.Player.Level;
            menuSubRank = host.Profile.Save.Player.SubRank;
            menuInventory = MenuInventoryState();
            menuMaps = MenuMapState();
            menuStories = MenuStoryState();
            menuProfessions = MenuProfessionState();
            menuFurniture = MenuFurnitureState();
            menuNpcs = MenuNpcState();
        }
        private string MenuInventoryState() => string.Join(";", host.Profile.Save.Player.Inventory
            .Where(stack => stack != null).Select(stack => stack.ItemId + ":" + stack.Count));

        private string MenuMapState() => string.Join(";", host.Profile.Save.Player.UnlockedMapClassIds)
            + "|" + string.Join(";", host.Profile.Save.World.ActivatedGoddessRegions.Select(value => value.MapClassId + ":" + value.Region))
            + "|" + string.Join(";", host.Profile.Save.World.StageWinCounts.Select(value => value.StageId + ":" + value.Count));
        private string MenuStoryState() => string.Join(";", host.Profile.Save.Player.Stories.Select(value => value.Key + ":" + value.Count));
        private string MenuProfessionState() => host.Profile.Save.Player.Wardrobe.Profession + "|"
            + string.Join(";", host.Profile.Save.Player.ProfessionLevels.Select(value => value.Profession));
        private string MenuFurnitureState() => string.Join(";", Enumerable.Range(0, 14).Select(host.HasFurnitureForSkin));
        private string MenuNpcState() => string.Join(";", host.Profile.Save.Player.CharacterProperties.NpcFriendships.Select(value => value.NpcClassId));

        // Existing transaction callers refresh the HUD after committing. Forward
        // matching changes once; the original menu owns its enabled listeners.
        private void RefreshNativeFunctionMenuPlayer()
        {
            if (nativeMenuPlayer == null) return;
            var player = host.Profile.Save.Player;
            string inventory = MenuInventoryState();
            bool level = menuLevel != player.Level, rank = menuSubRank != player.SubRank;
            bool items = menuInventory != inventory;
            bool maps = menuMaps != MenuMapState(), stories = menuStories != MenuStoryState();
            bool professions = menuProfessions != MenuProfessionState(), furniture = menuFurniture != MenuFurnitureState();
            bool npcs = menuNpcs != MenuNpcState();
            CaptureNativeMenuPlayer();
            if (maps) { nativeMenuConditions.NotifyWorldMapProgressUpdate(); nativeMenuConditions.NotifyMapDataAdd(); nativeMenuConditions.NotifyMapStatusUpdate(); }
            if (stories) { nativeMenuConditions.NotifyStoryCountUpdate(); nativeMenuConditions.NotifyStoryStatusUpdate(); }
            if (professions) nativeMenuConditions.NotifyProfessionPromote();
            if (furniture) { nativeMenuConditions.NotifyHangUpUnlock(); nativeMenuConditions.NotifyItemCountUpdate(); }
            if (npcs) nativeMenuConditions.NotifyAddNewNpc();
            if (level) { nativeMenuConditions.NotifyLevelUpdate(); nativeMenuPlayer.NotifyLevelUpdate(); }
            if (rank) { nativeMenuConditions.NotifySubRankUpdate(); nativeMenuPlayer.NotifySubRankUpdate(); }
            if (items) nativeMenuConditions.NotifyItemCountUpdate();
        }

        private void PlayNativeMenuSound(AudioClip clip, GameObject emitter)
        {
            if (!clip) return;
            if (!nativeMenuSound)
            {
                nativeMenuSound = canvas.gameObject.AddComponent<AudioSource>();
                nativeMenuSound.playOnAwake = false;
                nativeMenuSound.spatialBlend = 0;
                HomeAudioSettings.Register(nativeMenuSound, HomeAudioChannel.Sound);
            }
            nativeMenuSound.PlayOneShot(clip);
        }

        private void ReleaseNativeFunctionMenu()
        {
            nativeFunctionMenu?.Dispose(); nativeFunctionMenu = null; functionsPanel = null;
            nativeMenuConditionLifetime?.Dispose(); nativeMenuConditionLifetime = null;
            nativeMenuConditions = null; nativeMenuPlayer = null;
            nativeMenuServices = null;
            nativeMenuFonts?.Dispose(); nativeMenuFonts = null;
            if (nativeMenuSound)
            {
                HomeAudioSettings.Unregister(nativeMenuSound);
                UnityEngine.Object.Destroy(nativeMenuSound);
                nativeMenuSound = null;
            }
        }

        // Unity defers OnDestroy until the frame ends. Awake-owned listeners
        // (notably ShopEntryBtn) may deregister after the entire scope is released.
        private sealed class NativeMenuConditionLifetime : IHomeHudConditionHandler, IDisposable
        {
            private OfflineHomeHudConditions conditions;
            public NativeMenuConditionLifetime(OfflineHomeHudConditions conditions) { this.conditions = conditions; }
            public void Register(IHomeHudCondition condition, Action action)
                => (conditions ?? throw new ObjectDisposedException(nameof(NativeMenuConditionLifetime))).Register(condition, action);
            public void Deregister(IHomeHudCondition condition, Action action) => conditions?.Deregister(condition, action);
            public void Dispose() { var value = conditions; conditions = null; value?.Dispose(); }
        }

        // The menu issues original typed requests. Existing feature pages own
        // their UI; unavailable pages remain explicit unfinished endpoints.
        private sealed class NativeFunctionMenuServices : IHomeFunctionMenuServices,
            IHomeMenuActivityServices, IHomeMenuWikiServices, IHomeMenuWikiPlayer,
            IHomeMenuPredownloadServices, IHomeMenuTimeServices
        {
            private readonly HomeHudView view;
            private readonly OfflineHomeFunctionMenuConfiguration configuration;
            private readonly OfflineHomeFunctionMenuPlayer player;
            private readonly IHomeHudConditionHandler conditionHandler;
            private Action<object> activityOpen, activityClose;
            private Action<HomeMenuPredownloadStateUpdate> downloadUpdates;
            public NativeFunctionMenuServices(HomeHudView view, OfflineHomeFunctionMenuConfiguration configuration)
            { this.view = view; this.configuration = configuration; player = view.nativeMenuPlayer; conditionHandler = view.nativeMenuConditionLifetime; }
            public IHomeMenuPlayer Player => player;
            IHomeMenuWikiPlayer IHomeMenuWikiServices.Player => this;
            public IHomeHudConditionHandler ConditionHandler => conditionHandler;
            public HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type) => player.GetFunctionLimit(type);
            public bool IsMet(IHomeHudCondition condition) => player.IsMet(condition);
            public bool IsFunctionLimit(FunctionType type) => player.IsFunctionLimit(type);
            public bool IsHomeScene => true;
            public event Action<int> OnLevelUpdate { add { player.OnLevelUpdate += value; } remove { player.OnLevelUpdate -= value; } }
            public event Action<int> OnSubRankUpdate { add { player.OnSubRankUpdate += value; } remove { player.OnSubRankUpdate -= value; } }
            public void ReportClick(string panel, string eventId) { } // Offline: no remote analytics.
            public void PopupHomeSkin(Action<IHomeMenuPanel> initialize) => initialize(new Panel(() => view.ShowHomeSkins()));
            public void PopupSettings(IWindowOperator owner, Action<IHomeMenuSettingsDialog> initialize)
                => initialize(new Settings(view));
            public void NavigateTaskPanel(object argument, object completion)
            {
                if (argument != null || completion != null) throw new NotSupportedException("Only the original menu task request is bound here.");
                view.OpenTaskPanel(view.host.Profile, view.NavigateMainlineTask);
            }
            public void PopupFriend(IWindowOperator owner) => view.Notify("离线模式不开放好友");
            public void PopupPersonalTop(string eventId, string source) => view.Notify("真人排行榜按离线范围关闭");
            public void PopSocial() => view.Notify("社交入口按离线范围关闭");
            public void PopupUser() => view.Notify("用户中心依赖联网服务，离线模式不开放");
            public void PopAnnounce() => view.Notify("公告依赖联网服务，离线模式不开放");
            public void PopupAchievementGroups(IWindowOperator owner, Action<IHomeMenuAchievementPanel> initialize) => Pending("成就");
            public IEnumerable<object> GetAllAchievementGroups() => throw new NotSupportedException("Achievement page data is not bound yet.");
            public void PopupCollectBook(IWindowOperator owner, Action<IHomeMenuCollectBookPanel> initialize) => Pending("图鉴");
            public void PopupLocalFightRecord(Action<IHomeMenuPanel> initialize) => Pending("本地战报列表");
            public void PopupShopActivityEntry(Action<IHomeMenuPanel> initialize) => Pending("商城活动入口");
            public void PopupPlanesMerge(Action<IHomeMenuPanel> initialize) => Pending("界域合并");
            public void OpenScreenShot() => Pending("拍照");
            private void Pending(string name) => view.Notify(name + "页面尚未迁移");
            public void RemoveNewFunction(FunctionType type) { } // Full notification graph remains unbound.
            public bool CheckFunctionDisabled(string type) => false; // No remote disabled-function records.
            public void ShowError(int id, object args, bool translate) => view.Notify(view.catalog.LocalizedText(id.ToString(), id.ToString()));

            // The initial local activity source has no open records. Configuration
            // is retained, without treating its mere presence as an open activity.
            public IEnumerable<IHomeMenuActivityEntryData> ActivityEntryDatas => Array.Empty<IHomeMenuActivityEntryData>();
            public bool ActivityDataManagerExists => true;
            public bool IsMapScene => false;
            public event Action<object> OnActivityOpen { add { activityOpen += value; } remove { activityOpen -= value; } }
            public event Action<object> OnActivityClose { add { activityClose += value; } remove { activityClose -= value; } }
            public HomeMenuActivityRecommendInfo GetRecommend(ActivityEntryType type, int id) => configuration.GetRecommend(type, id);
            public IReadOnlyDictionary<int, HomeMenuActivityEntryInfo> GetEntries(ActivityEntryType type) => configuration.GetEntries(type);
            public int RandomRange(int minimum, int maximum) => UnityEngine.Random.Range(minimum, maximum);
            public IHomeMenuPrefabHandle LoadPrefab(string path) => throw new NotSupportedException("No migrated activity has supplied an entry prefab: " + path);
            public GameObject Instantiate(GameObject prefab, Transform parent) => UnityEngine.Object.Instantiate(prefab, parent, false);
            public IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position)
            {
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, results);
                return results;
            }
            public void ReportRecommendationClick(string panel, string eventId, Dictionary<string, string> extend) { }

            // Original CN configuration/conditions hide Wiki; no added disable flag.
            public bool CheckWikiDisabled() => false;
            public HomeMenuWikiInfo GetWikiInfo(string type) => configuration.GetWikiInfo(type);
            public string GetUrl(string type) => configuration.GetWikiUrl(type);
            public string GetTitleKey(string type) => configuration.GetWikiTitleKey(type);
            public IHomeHudCondition GetCondition(int id) => view.nativeMenuConditions.GetCondition(id);
            public string GetText(string key) => OfflineHomeFunctionMenuAssets.LoadDefault().Text(key);
            public void SystemMessage(string text) => view.Notify(text);
            public void TrySystemMessage(string text) => view.Notify(text);
            public void PublishNewTipClick() { }
            public UniTask<string> OpenHtml(string url, string style, string orientation, string close, object parameter)
                => throw new NotSupportedException("Selected CN Wiki has no local HTML content.");
            public Dictionary<string, object> CreateEventData(string name, string accountId, string account) => new Dictionary<string, object>();
            public void ReportEvent(string name, Dictionary<string, object> values) { }
            public string Name => view.host.Profile.Save.Player.CharacterName;
            public int Level => view.host.Profile.Save.Player.Level;
            public string GetShuShuAccountId() => throw NoRemoteIdentity();
            public string Account => throw NoRemoteIdentity();
            public long ServerId => throw NoRemoteIdentity();
            public long Id => throw NoRemoteIdentity();
            private static Exception NoRemoteIdentity() => new NotSupportedException("The offline character has no remote SDK account/server identity.");

            // Packaged local assets have no pending network download jobs.
            public PredownloadState CurrentDownloadState => PredownloadState.Downloaded;
            public long TotalDownloadedBytes => 0;
            public long CurrentDownloadedBytes => 0;
            public bool TimeSynced => true;
            public IDisposable SubscribeStateUpdates(Action<HomeMenuPredownloadStateUpdate> callback)
            {
                downloadUpdates += callback;
                return new Subscription(() => downloadUpdates -= callback);
            }
            public void PopupTipDialog() => throw new InvalidOperationException("No pending download jobs.");
            public void PopupProgressDialog(Action<IHomeMenuPanel> initialize) => throw new InvalidOperationException("No pending download jobs.");
            public void ClickEntry(bool clearNeedTip) => throw new InvalidOperationException("No pending download jobs.");
            public IReadOnlyCollection<int> DisplayServerTimeRegionList => configuration.DisplayServerTimeRegionList;
            public int RegionCode => (int)HomeMenuConfigurationRules.RegionCodeEnum.CN;
            public int TimeZone => throw new NotSupportedException("No remote server time zone is assigned.");
            public DateTime GetDateTime() => DateTime.UtcNow;
            public DateTime GetClientLocalDateTime() => DateTime.Now;
            public DateTime GetDateTimeWithTimeZone(DateTime value) => throw new NotSupportedException("The selected CN menu has no server-time display.");
            public string GetServerTimeWithZoneTip(int timeZone) => throw new NotSupportedException("The selected CN menu has no server-time display.");

            private sealed class Panel : IHomeMenuPanel
            {
                private readonly Action initialize;
                public Panel(Action initialize) { this.initialize = initialize; }
                public void Init() => initialize();
            }
            private sealed class Settings : IHomeMenuSettingsDialog
            {
                private readonly HomeHudView view;
                public Settings(HomeHudView view) { this.view = view; }
                public void Init(HomeMenuSettingStyle style)
                {
                    if (style != HomeMenuSettingStyle.Normal) throw new ArgumentOutOfRangeException(nameof(style));
                    view.OpenSettings(false);
                }
            }
            private sealed class Subscription : IDisposable
            {
                private Action release;
                public Subscription(Action release) { this.release = release; }
                public void Dispose() { var callback = release; release = null; callback?.Invoke(); }
            }
        }
    }
}
