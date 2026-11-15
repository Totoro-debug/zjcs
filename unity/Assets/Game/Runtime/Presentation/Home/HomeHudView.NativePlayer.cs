using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.HomeAvatarUI;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.HomeLevelUpUI;
using Zjcs.Offline.HomePlayerHealthUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeLevelUpUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private OfflineHomePlayerPanel nativePlayerPanel;
        private OfflineHomeAvatarPortraits nativePlayerPortraits;
        private OfflinePartnerActorUiRuntime nativePlayerActors;
        private OfflineHomeExperiencePlayer nativeExperiencePlayer;
        private OfflineHomeExperienceServices nativeExperience;
        private OfflineHomePanelPlayer nativePanelPlayer;
        private OfflineHomePlayerStatuses nativePlayerStatuses;
        private NativePlayerServices nativePlayerServices;
        private OfflineHomeLevelUpWindows nativeLevelUpWindows;
        private OfflineHomeLevelUpProperties nativeLevelUpProperties;
        private OfflineProfessionSoundHost nativePlayerSound;
        private OfflineProfessionAudioCatalog nativePlayerAudio;
        private IProfessionSoundHost previousPlayerSound;
        private OfflineMainlineSave nativePlayerObservedSave;

        private void BuildNativePlayerPanel()
        {
            try
            {
                var profile = host.Profile;
                profile.EnsureLocalActorIdentities();
                var progression = host.DailyDungeonProgression;
                var assets = Resources.Load<OfficialHomePlayerPanelAssetCatalog>("Home/OfficialHomePlayerPanelAssetCatalog");
                if (!assets) throw new InvalidOperationException("The complete original home player panel is unbound.");
                var tables = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
                var environment = host.CreateHomeExperienceEnvironment(tables);
                nativeExperiencePlayer = new OfflineHomeExperiencePlayer(profile, tables, environment.Capture);
                nativePlayerStatuses = new OfflineHomePlayerStatuses(profile, OfflineHomeStatusConfiguration.LoadDefault());
                nativePlayerServices = new NativePlayerServices(this, assets, tables, nativePlayerStatuses);
                var health = new OfflinePlayerHealth(profile, progression, () => nativePlayerServices.Maps.Count,
                    nativePlayerServices.ReadHomeGames);
                nativePanelPlayer = new OfflineHomePanelPlayer(profile, nativeExperiencePlayer, health,
                    nativePlayerServices, nativePlayerServices.Health);
                nativeLevelUpProperties = OfflineHomeLevelUpProperties.LoadDefault(() => profile.Save.Player);
                var levelAssets = OfflineHomeLevelUpAssets.LoadDefault();
                nativePlayerAudio = ScriptableObject.CreateInstance<OfflineProfessionAudioCatalog>();
                nativePlayerAudio.Events = assets.sounds.Select(row => new OfflineProfessionAudioCatalog.Entry {
                    EventId = row.eventId, Clip = row.clip
                }).Concat(new[] { new OfflineProfessionAudioCatalog.Entry {
                    EventId = OfflineHomeLevelUpAssets.SoundEventId, Clip = levelAssets.sound
                }}).ToArray();
                previousPlayerSound = OfficialWwiseEvent.Host;
                nativePlayerSound = canvas.gameObject.AddComponent<OfflineProfessionSoundHost>();
                nativePlayerSound.FallbackHost = previousPlayerSound;
                nativePlayerSound.Bind(nativePlayerAudio);
                nativeLevelUpWindows = new OfflineHomeLevelUpWindows(new OfflineHomeLevelUpWindowBindings {
                    Canvas = canvas, Properties = ProfessionPropertyBindings.Current,
                    SoundHost = nativePlayerSound, PostSound = PostPartnerPromptSound,
                    Showing = popup => OfflineHomeWindowOrder.Show(canvas, popup),
                    Closed = _ => { } // The next show reads the remaining active popup roots.
                }, nativeLevelUpProperties, () => nativeExperiencePlayer.Level, levelAssets);
                nativeExperience = new OfflineHomeExperienceServices(nativeExperiencePlayer,
                    new OfflineHomeExperienceStore(nativeExperiencePlayer), null,
                    previous => LevelUpNotifyTip.TryPopup(nativeLevelUpWindows, previous),
                    value => CultureNumberArg.Compact(value), nativePlayerServices.GetSeasonNameByRank,
                    subRank => progression.Catalog.RankOfSubRank(subRank));
                nativePlayerPortraits = new OfflineHomeAvatarPortraits(Path.Combine(
                    new ProfileSlotCatalog(host.SavesRoot).GetSlotDirectory(host.ActiveProfileSlotId), "home-portrait-cache"),
                    profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId,
                    () => profile.Save.Player, id => progression.Catalog.ItemType(id));
                nativePlayerActors = new OfflinePartnerActorUiRuntime(profile, progression, host.PartnerCatalog,
                    ProfessionPropertyBindings.Current.Resources, nativePlayerServices.Text,
                    host.OpenPartnerDetail, OpenCharacterPage);
                // The original full subtree keeps its original RectTransform
                // below MainCanvas/Content/Main/Public/Anim.
                nativePlayerPanel = new OfflineHomePlayerPanel(nativePublicSlot, nativePlayerActors.TeamIcons,
                    nativePlayerPortraits.Manager, nativePlayerServices, ProfessionPropertyBindings.Current, assets);
                nativePlayerObservedSave = profile.Save;
            }
            catch { ReleaseNativePlayerPanel(); throw; }
        }

        private void TickNativePlayerPanel()
        {
            nativePlayerPortraits?.Update();
            if (nativePlayerPanel != null)
            {
                if (CharacterPageOpen) nativePlayerPanel.Visibility.Hide(this);
                else nativePlayerPanel.Visibility.TryShow(this);
            }
            if (nativePlayerPanel != null && !ReferenceEquals(nativePlayerObservedSave, host.Profile.Save))
                RefreshNativePlayerPanel();
        }

        private void RefreshNativePlayerPanel()
        {
            if (nativePlayerPanel == null) return;
            nativePlayerServices.RefreshProperties();
            nativePlayerStatuses.SynchronizeCommittedPlayer();
            nativePanelPlayer.SynchronizeCommittedPlayer();
            nativePlayerServices.SynchronizeWorldRank();
            nativePlayerPortraits.RefreshAppearance();
            nativePlayerObservedSave = host.Profile.Save;
        }

        private void ReleaseNativePlayerPanel()
        {
            nativePlayerPanel?.Dispose(); nativePlayerPanel = null;
            nativeLevelUpWindows?.Dispose(); nativeLevelUpWindows = null;
            nativeExperience?.Dispose(); nativeExperience = null;
            nativePanelPlayer?.Dispose(); nativePanelPlayer = null;
            nativeExperiencePlayer?.Dispose(); nativeExperiencePlayer = null;
            nativePlayerServices?.Dispose(); nativePlayerServices = null;
            nativePlayerStatuses?.Dispose(); nativePlayerStatuses = null;
            nativePlayerPortraits?.Dispose(); nativePlayerPortraits = null;
            nativePlayerActors?.Dispose(); nativePlayerActors = null;
            nativeLevelUpProperties?.Dispose(); nativeLevelUpProperties = null;
            if (nativePlayerSound)
            {
                if (ReferenceEquals(OfficialWwiseEvent.Host, nativePlayerSound)) OfficialWwiseEvent.Host = previousPlayerSound;
                Object.Destroy(nativePlayerSound);
            }
            nativePlayerSound = null; previousPlayerSound = null;
            if (nativePlayerAudio) Object.Destroy(nativePlayerAudio);
            nativePlayerAudio = null; nativePlayerObservedSave = null;
        }

        private sealed class NativePlayerServices : IHomePlayerPanelBindings, IHomePanelResourceLoader,
            IOfflineHomePanelState, IDisposable
        {
            private readonly HomeHudView view;
            private readonly OfficialHomePlayerPanelAssetCatalog assets;
            private readonly HomeExperienceTables tables;
            private readonly OfflineHomePlayerStatuses statuses;
            private readonly OfflineHomeStatusConfiguration statusRules;
            private readonly OfflineHomeNoBlessConfiguration noBless;
            private readonly OfflinePartnerLocalizationService language;
            // This owner belongs only to the home scene. Map scenes have their
            // own live map collections; saved map progress is never used here.
            private readonly int[] homeMaps = Array.Empty<int>();
            private int worldRank;
            public OfflineHomePlayerHealthConfiguration Health { get; }
            public NativePlayerServices(HomeHudView view, OfficialHomePlayerPanelAssetCatalog assets,
                HomeExperienceTables tables, OfflineHomePlayerStatuses statuses)
            {
                this.view = view; this.assets = assets; this.tables = tables; this.statuses = statuses;
                statusRules = OfflineHomeStatusConfiguration.LoadDefault();
                noBless = OfflineHomeNoBlessConfiguration.LoadDefault();
                Health = OfflineHomePlayerHealthConfiguration.LoadDefault();
                language = new OfflinePartnerLocalizationService(assets.localizedTexts,
                    OfficialHomePlayerInfoAssetCatalog.LoadDefault().LocalizedTexts);
                worldRank = view.host.Profile.Save.Player.CharacterProperties.TreasurePlaneRank;
                RefreshProperties();
            }
            public IHomePanelPlayer MainPlayer => view.nativePanelPlayer;
            IHomePlayerInfoPlayer IHomePlayerInfoBindings.MainPlayer => MainPlayer;
            IHomeExperiencePlayer IHomeExperienceServices.MainPlayer => MainPlayer;
            public bool ManagerAvailable => view.nativeExperience?.ManagerAvailable ?? false;
            public SafeAction OnPlayerUpdate => view.nativeExperience.OnPlayerUpdate;
            public IHomeLevelExpParser LevelExpParser => view.nativeExperience.LevelExpParser;
            public IHomeLevelBlessParser LevelBlessInfoParser => view.nativeExperience.LevelBlessInfoParser;
            public IHomeExperienceRewards Rewards => view.nativeExperience.Rewards;
            public int PlayerExpItemId => tables.PlayerExpItemId;
            public int PlanesRank => view.nativeExperiencePlayer.PlanesRank;
            public void ResolveSubRank(int rank) => view.nativeExperience.ResolveSubRank(rank);
            public object Compact(long value) => CultureNumberArg.Compact(value);
            public string GetSeasonNameByRank(int rank) => Text("astrological_season_" +
                tables.AstrologicalSeasonConfigParser.GetSeasonByRank((Rank)rank) + "_name");
            public bool TMPEffectFontSizeIssueFixed => TMPEffect.FixFontSizeIssue;
            public Vector3 PointerPosition => Input.mousePosition;
            public string Text(string key) => language.TryGetText(key, out var value) ? value :
                LanguageManager.Instance.GetText(key);
            public string TextFormatted(string key, params object[] args) => string.Format(Text(key), args);
            public IProfessionTipDotSystem TipDotSystem => view.host.PartnerGiftBindings.TipDotSystem;
            public float PlayerHpRedDotThreshold => Health.playerHpRedDotThreshold;
            public IHomePanelResourceLoader Resources => this;
            public DateTime GetDateTime() => DateTime.UtcNow;
            public HomePanelItemInfo GetItemInfo(int id) => Health.GetItemInfo(id);
            public HomePanelEntryInfo GetEntry(int id) => statusRules.Entries.TryGetValue(id, out var row)
                ? new HomePanelEntryInfo { Icon = (string)row["Icon"], Bg = (string)row["Bg"] } : null;
            public string GetSeasonLevelUnEffectDesc(int rank) => TextFormatted("ui_blessing_player_level_tip_2", GetSeasonNameByRank(rank));
            public string GetEntryNameKey(int id) => "entry_" + id + "_name";
            public string GetEntryDescKey(int id) => "entry_" + id + "_desc";
            public string GetStatusEffectVaildShowScopesName(IHomePanelPlayer player, int classId)
            {
                var scopes = statusRules.Get(classId).Scopes;
                return string.Join(Text("ui_common_comma"), scopes.Where((id, index) => index == 0
                    || statusRules.Scopes[id]["Condition"] == null
                    || statusRules.Scopes[id]["Condition"].Type == Newtonsoft.Json.Linq.JTokenType.Null
                    || IsConditionMet(statusRules.Scopes[id]["Condition"]))
                    .Select(id => Text("status_effect_scope_name_" + id)));
            }
            private bool IsConditionMet(Newtonsoft.Json.Linq.JToken condition)
            {
                var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
                var rules = configuration.CreateConditions(view.host.PartnerCatalog.Conditions);
                return rules.IsMet(condition.ToObject<Dictionary<string,object>>(),
                    new OfflineHomeConditionPlayer(configuration, () => view.host.Profile.Save, view.host.HasFurnitureForSkin));
            }
            public bool TryGetCookingBuffEntryDesc(IHomePanelPlayer player, int entryId, out string desc)
            {
                // This path is used by general entry-detail consumers; the
                // original home status list supplies its concrete status class.
                throw new NotSupportedException("Cooking entry-detail ownership has not been migrated.");
            }
            public void PopupIntroduction(UIBase owner, HomeInfoTextKey title, IReadOnlyList<string> content, Vector3 position)
                => view.Notify("说明窗口尚未迁移");
            public void PopupEntries(UIBase owner, IReadOnlyList<HomeEntryDetailData> entries)
                => view.Notify("状态详情窗口尚未迁移");
            public void PopupEntry(UIBase owner, Func<HomeEntryDetailData> entry)
                => PopupEntries(owner, new[] { entry() });
            public long CombatRating { get; private set; }
            public long NoBlessCombatRating { get; private set; }
            public IReadOnlyCollection<int> Maps { get { RequireHomeScope(); return homeMaps; } }
            public IReadOnlyDictionary<int,IHomePanelStatusEffect> StatusEffects => statuses.Statuses;
            public event Action<int,int> BlessLevelDataReset;
            public event Action<IHomePanelStatusEffect> StatusEffectDataUpdate
            { add => statuses.StatusUpdated += value; remove => statuses.StatusUpdated -= value; }
            public void RefreshProperties()
            {
                var host = view.host; var growth = host.DailyDungeonProgression;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                CombatRating = growth.EvaluateProps(now).CombatRating;
                NoBlessCombatRating = new OfflineHomeNoBlessPlayerView(host.Profile.Save.Player,
                    (Rank)host.Profile.Save.Player.CharacterProperties.TreasurePlaneRank, noBless)
                    .Evaluate(growth.PropertyCalculator, host.Profile.Save.World, now).CombatRating;
            }
            public void SynchronizeWorldRank()
            {
                int current = view.host.Profile.Save.Player.CharacterProperties.TreasurePlaneRank;
                if (current == worldRank) return;
                int previous = worldRank; worldRank = current; BlessLevelDataReset?.Invoke(previous,current);
            }
            private void RequireHomeScope()
            {
                if (view.host.IsBattlePresentationActive)
                    throw new InvalidOperationException("The home HP owner is suspended during battle presentation.");
            }
            public IReadOnlyList<bool> ReadHomeGames() { RequireHomeScope(); return Array.Empty<bool>(); }
            public IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string guid) where T : Object
            {
                Object value = assets.statusPrefabs.FirstOrDefault(row => row.guid == guid)?.prefab;
                if (!value) value = assets.sprites.FirstOrDefault(row => row.guid == guid)?.sprite;
                if (!(value is T result)) throw new InvalidOperationException("Unbound original player-panel asset: " + guid);
                return new NativePlayerHandle<T>(result);
            }
            public IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => LoadAssetByGuid<T>(guid);
            public IHomeHudAssetHandle<Sprite> LoadSprite(string path)
            {
                var operation = ProfessionPropertyBindings.Current.Resources.LoadAsset<Sprite>("Assets/Image/" + path);
                return new NativePlayerHandle<Sprite>(operation);
            }
            public void Dispose() { BlessLevelDataReset = null; }
        }

        private sealed class NativePlayerHandle<T> : IHomeHudAssetHandle<T> where T : Object
        {
            private T result;
            private IProfessionAssetOperation<T> operation;
            public NativePlayerHandle(T value) { result = value; }
            public NativePlayerHandle(IProfessionAssetOperation<T> value) { operation = value; result = value.Result; }
            public bool IsValid => result;
            public int Status => result ? 2 : 3;
            public T Result => result;
            public T WaitForAsyncComplete() => result;
            public event Action<IHomeHudAssetHandle<T>> Completed { add => value?.Invoke(this); remove { } }
            public void Dispose() { operation?.Dispose(); operation = null; result = null; }
        }
    }
}
