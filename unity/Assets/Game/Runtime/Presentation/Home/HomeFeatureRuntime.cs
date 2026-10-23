using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Wishes;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Entry point of the offline home module.  It opens the character save and
    /// the home save, rebuilds the official scene and HUD, and routes every
    /// furniture action through the offline rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeFeatureRuntime : MonoBehaviour
    {
        private ProfileSlotCatalog slots;
        private OfflineMainlineProgressSession profile;
        private OfflineCharacterProgressionService progression;
        private OfficialHomeRules rules;
        private OfflineHomeProgression homeProgression;
        private OfflineHomeIncomeSession session;
        private HomeAssetCatalog catalog;
        private HomeSceneView scene;
        private HomeInputController input;
        private HomeEnvironmentView environment;
        private HomeRewardView rewards;
        private HomeHudView hud;
        private TreasureGachaRuntimeCatalog treasureGachaCatalog;
        private OfflineTreasureGachaService treasureGacha;
        private PetGachaRuntimeCatalog petGachaCatalog;
        private OfflinePetGachaService petGacha;
        private OfficialPetGachaAssetCatalog petGachaAssets;
        private OfflineFruitTreeService fruitTree;
        private WishScreenController wishes;
        private HomeConditionContext context;
        private Action onClose;
        private string slotId;
        private string savesRoot;
        private Transform uiParent;
        private float advanceTimer;
        private long lastAdvanceUnix;
        private bool fruitClockRollbackWarned;
        private string pendingFruitClockRollbackMessage;
        private int pendingFruitTreeRepairTutorialType = -1;
        private bool pendingFruitTreeCollectionTutorial;
        private AudioSource fruitTreeTutorialVoiceSource;
        private AudioClip currentFruitTreeTutorialVoice;
        private int fruitTreeTutorialVoiceRequestVersion;

        public string ActiveProfileSlotId => slotId;
        public OfflineHomeIncomeSession Session => session;
        public OfflineHomeProgression HomeProgression => homeProgression;
        public HomeAssetCatalog Catalog => catalog;
        public HomeHudView Hud => hud;
        public OfficialHomeRules Rules => rules;
        public OfflineMainlineProgressSession Profile => profile;
        public HomeConditionContext Context => context;
        public OfflineTreasureGachaService TreasureGacha => treasureGacha;
        public TreasureGachaRuntimeCatalog TreasureGachaCatalog => treasureGachaCatalog;
        public OfflinePetGachaService PetGacha => petGacha;
        public PetGachaRuntimeCatalog PetGachaCatalog => petGachaCatalog;
        public OfficialPetGachaAssetCatalog PetGachaAssets => petGachaAssets;
        public int BlessingLevel { get; private set; } = 1;
        public HomeEnvironmentView Environment => environment;
        public HomeSceneView Scene => scene;
        public string SavesRoot => savesRoot;
        public HomeRewardView Rewards => rewards;

        public void RefreshRewards() => rewards?.Refresh();

        public void RefreshCounts() => hud?.RefreshCounts();

        public TreasureGachaSourceNavigation OpenTreasureGachaFromItemSource(int sourceItemId,
            int elementGachaType = 0, Action<int> returnToSource = null)
        {
            if (treasureGacha == null || hud == null || elementGachaType != 0
                && (elementGachaType < 8 || elementGachaType > 12)) return null;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            TreasureGachaSourceNavigation navigation = treasureGacha.ResolveItemSource(sourceItemId, now);
            if (navigation == null) return null;
            if (navigation.SelectedSeriesId > 0
                && treasureGacha.State.SelectedSeriesId != navigation.SelectedSeriesId)
            {
                TreasureGachaResult selected = treasureGacha.SelectSourceSeries(navigation.SelectedSeriesId,
                    now, "treasure-gacha:source:" + Guid.NewGuid().ToString("N"));
                if (!selected.IsApplied && selected.Code != TreasureGachaResultCode.Unchanged) return null;
            }
            if (elementGachaType >= 8)
            {
                TreasureGachaResult selected = treasureGacha.SelectElementType(elementGachaType,
                    "treasure-gacha:source-element:" + Guid.NewGuid().ToString("N"));
                if (!selected.IsApplied && selected.Code != TreasureGachaResultCode.Unchanged) return null;
            }
            hud.ShowTreasureGachaFromItemSource(sourceItemId, navigation.SelectedSeriesId,
                elementGachaType, returnToSource);
            return navigation;
        }

        public string CharacterName => profile?.Save.Player.CharacterName ?? string.Empty;
        public int Level => profile?.Save.Player.Level ?? 1;
        public int SubRank => profile?.Save.Player.SubRank ?? 1;
        public int Rank => profile?.Save.Player.Rank ?? 1;
        public string GuildName => profile?.Save.Player.GuildName ?? string.Empty;

        public static HomeFeatureRuntime Begin(string selectedSlotId, string savesRoot, Transform uiParent,
            Action onClose = null)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("An existing profile slot is required.", nameof(selectedSlotId));
            var go = new GameObject("HomeGameRuntime");
            HomeFeatureRuntime runtime = go.AddComponent<HomeFeatureRuntime>();
            runtime.Open(selectedSlotId, string.IsNullOrEmpty(savesRoot) ? Application.persistentDataPath : savesRoot,
                uiParent, onClose);
            return runtime;
        }

        private void Open(string selected, string root, Transform uiParent, Action close)
        {
            slotId = selected;
            savesRoot = root;
            this.uiParent = uiParent;
            onClose = close;
            slots = new ProfileSlotCatalog(root);
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile does not exist.");
            profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            profile.Reload();
            fruitTree = new OfflineFruitTreeService(profile);
            catalog = HomeAssetCatalog.LoadDefault();
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "TreasureGacha", "ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "PetGacha", "ui.json"));
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "PetGacha", "performance-ui.json"));
            try
            {
                treasureGachaCatalog = OfficialTreasureGachaAssets.LoadDefault();
                treasureGacha = new OfflineTreasureGachaService(profile, treasureGachaCatalog);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home treasure gacha unavailable: " + error.Message);
            }
            try
            {
                petGachaCatalog = OfficialPetGachaAssets.LoadDefault();
                petGachaAssets = Resources.Load<OfficialPetGachaAssetCatalog>(
                    "PetGacha/official-pet-gacha-assets");
                if (petGachaAssets == null)
                    throw new InvalidOperationException("Official pet gacha native models are not installed.");
                petGachaAssets.Validate(petGachaCatalog);
                petGacha = new OfflinePetGachaService(profile, petGachaCatalog);
            }
            catch (Exception error)
            {
                petGacha = null;
                Debug.LogWarning("Home pet gacha unavailable: " + error.Message);
            }
            rules = OfficialHomeRules.LoadDefault();
            homeProgression = new OfflineHomeProgression(rules);
            BlessingLevel = 1;
            ReloadContext();
            long openedAt = Now();
            fruitTree.TryAutoRepairProduction(profile.Save.Player.Level, context.AnyGoddessActive,
                openedAt, "home:fruit-tree:auto-repair:" + slotId);
            AdvanceFruitTree(openedAt);
            session = OpenSession();
            lastAdvanceUnix = openedAt;
            scene = new HomeSceneView(catalog, transform);
            environment = new HomeEnvironmentView(scene, Now());
            rewards = new HomeRewardView(this, catalog, transform);
            input = new HomeInputController(scene, () => hud != null && hud.AnyModalOpen
                || wishes != null && wishes.IsOpen);
            input.FurnitureClicked += OnFurnitureClicked;
            input.RewardClicked += OnRewardClicked;
            input.SetRewardHit(OnRewardHit);
            hud = new HomeHudView(this, uiParent);
            hud.Build();
            fruitTreeTutorialVoiceSource = gameObject.AddComponent<AudioSource>();
            fruitTreeTutorialVoiceSource.playOnAwake = false;
            fruitTreeTutorialVoiceSource.loop = false;
            fruitTreeTutorialVoiceSource.spatialBlend = 0f;
            if (!string.IsNullOrEmpty(pendingFruitClockRollbackMessage))
            {
                hud.ShowNotice(pendingFruitClockRollbackMessage);
                pendingFruitClockRollbackMessage = null;
            }
            ApplyFurnitureStates();
            RefreshIncomeCatalog();
            DrainHomeInventory();
            hud.RefreshAll();
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public void PlayFruitTreeTutorialVoice(string eventKey)
        {
            StopFruitTreeTutorialVoice();
            string relativePath = HomeFruitTreeTutorialCatalog.VoiceFile(eventKey);
            StartCoroutine(LoadFruitTreeTutorialVoice(relativePath, eventKey,
                fruitTreeTutorialVoiceRequestVersion));
        }

        public void StopFruitTreeTutorialVoice()
        {
            fruitTreeTutorialVoiceRequestVersion++;
            if (fruitTreeTutorialVoiceSource != null)
            {
                fruitTreeTutorialVoiceSource.Stop();
                fruitTreeTutorialVoiceSource.clip = null;
            }
            if (currentFruitTreeTutorialVoice != null) Destroy(currentFruitTreeTutorialVoice);
            currentFruitTreeTutorialVoice = null;
        }

        private IEnumerator LoadFruitTreeTutorialVoice(string relativePath, string eventKey, int version)
        {
            string path = Path.Combine(Application.streamingAssetsPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || !Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue))
            {
                Debug.LogWarning("Official fruit tree tutorial voice path is unavailable: " + eventKey);
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri,
                AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (version != fruitTreeTutorialVoiceRequestVersion) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Official fruit tree tutorial voice could not be loaded for "
                        + eventKey + ": " + request.error);
                    yield break;
                }

                currentFruitTreeTutorialVoice = DownloadHandlerAudioClip.GetContent(request);
                if (currentFruitTreeTutorialVoice == null)
                {
                    Debug.LogWarning("Official fruit tree tutorial voice decoded to an empty clip: " + eventKey);
                    yield break;
                }
                fruitTreeTutorialVoiceSource.clip = currentFruitTreeTutorialVoice;
                fruitTreeTutorialVoiceSource.Play();
            }

            while (version == fruitTreeTutorialVoiceRequestVersion
                && fruitTreeTutorialVoiceSource != null && fruitTreeTutorialVoiceSource.isPlaying)
                yield return null;
            if (version == fruitTreeTutorialVoiceRequestVersion) StopFruitTreeTutorialVoice();
        }

        private void ReloadContext()
        {
            OfflinePlayerProgress player = profile.Save.Player;
            context = new HomeConditionContext
            {
                Level = player.Level,
                SubRank = player.SubRank,
                Rank = player.Rank,
                MaxProfessionRank = player.MaxProfessionRank,
                InitialWeaponType = player.InitialWeaponType,
                Profession = player.Wardrobe.Profession,
                ItemCount = profile.InventoryCount,
                StoryCount = _ => 0,
                StoryPlayed = _ => false,
                HasItemOfType = _ => false,
                AnyGoddessActive = profile.Save.World.ActivatedGoddessRegions != null
                    && profile.Save.World.ActivatedGoddessRegions.Count > 0,
                UnlockedMapClassIds = new HashSet<int>(player.UnlockedMapClassIds ?? new List<int>())
            };
            rules.CurrentContext = context;
        }

        private OfflineHomeIncomeSession OpenSession()
        {
            var properties = PlayerProperties();
            session = OfflineHomeIncomeSession.Open(slots, slotId, new[] { PlaceholderDefinition() },
                properties, new HashSet<int>(), Array.Empty<OfflineHomeRandomRewardDefinition>(), Now());
            OfflineHomeIncomeDefinition[] definitions = homeProgression.BuildIncomeDefinitions(
                session, context, BlessingLevel);
            OfflineHomeRandomRewardDefinition[] rewards = homeProgression.BuildRandomRewardDefinitions(session, context);
            session.Reconfigure(definitions, properties, new HashSet<int>(), rewards);
            return session;
        }

        private static OfflineHomeIncomeDefinition PlaceholderDefinition()
        {
            return new OfflineHomeIncomeDefinition
            {
                HangUpId = 1,
                OutputItemId = 50,
                Rule = new HangUpIncomeRule { HourlyOutput = 0, LevelNum = 1, MaxStoreHours = 36 }
            };
        }

        public IReadOnlyDictionary<int, long> PlayerProperties()
        {
            var merged = new Dictionary<int, long>();
            try
            {
                EnsureProgression();
                foreach (KeyValuePair<int, long> pair in progression.BaseBattleProperties()) merged[pair.Key] = pair.Value;
                foreach (KeyValuePair<int, long> pair in progression.AppearanceProperties(Now()))
                    merged[pair.Key] = merged.TryGetValue(pair.Key, out long existing) ? existing + pair.Value : pair.Value;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home properties unavailable: " + error.Message);
            }
            return merged;
        }

        private void EnsureProgression()
        {
            if (progression != null) return;
            OfficialCharacterCatalog character = OfficialCharacterAssetCatalog.LoadDefault().Load();
            progression = new OfflineCharacterProgressionService(profile, character);
        }

        public void RefreshIncomeCatalog()
        {
            OfflineHomeIncomeDefinition[] definitions = homeProgression.BuildIncomeDefinitions(session, context, BlessingLevel);
            OfflineHomeRandomRewardDefinition[] rewards = homeProgression.BuildRandomRewardDefinitions(session, context);
            session.Reconfigure(definitions, PlayerProperties(), new HashSet<int>(), rewards);
        }

        private void ApplyFurnitureStates()
        {
            foreach (HomeFurnitureRecord record in rules.Furniture)
            {
                HomeFurnitureState state = FurnitureState(record.type);
                scene.SetFurnitureState(record.type, state);
            }
            scene.SetFruitTreeState(FurnitureState(OfflineFruitTreeService.ProductionFurnitureType),
                FurnitureState(OfflineFruitTreeService.PetFurnitureType),
                profile.Save.Player.FruitTree.StoredCount);
        }

        private HomeFurnitureState FurnitureState(int type)
        {
            if (type == OfflineFruitTreeService.ProductionFurnitureType
                && profile.Save.Player.FruitTree.ProductionRepaired) return HomeFurnitureState.Functional;
            if (type == OfflineFruitTreeService.PetFurnitureType
                && profile.Save.Player.FruitTree.PetFacilityRepaired) return HomeFurnitureState.Functional;
            if (type != OfflineFruitTreeService.ProductionFurnitureType
                && type != OfflineFruitTreeService.PetFurnitureType)
                return homeProgression.FurnitureState(session, type, context);
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return HomeFurnitureState.Locked;
            HomeFurnitureState state = rules.IsMet(record.unlock, context)
                ? HomeFurnitureState.CanRepair
                : !rules.IsMet(record.repairNotice, context)
                    ? HomeFurnitureState.Locked : HomeFurnitureState.RepairNotice;
            if (type == OfflineFruitTreeService.ProductionFurnitureType)
            {
                HomeFurnitureRecord pet = rules.FurnitureOf(OfflineFruitTreeService.PetFurnitureType);
                HomeFurnitureState petState = pet == null || !rules.IsMet(pet.repairNotice, context)
                    ? HomeFurnitureState.Locked
                    : rules.IsMet(pet.unlock, context)
                        ? HomeFurnitureState.CanRepair : HomeFurnitureState.RepairNotice;
                if (petState == HomeFurnitureState.CanRepair || petState == HomeFurnitureState.RepairNotice)
                    return HomeFurnitureState.Locked;
            }
            return state;
        }

        private int OnRewardHit(Vector2 world) => rewards == null ? -1 : rewards.HitTest(world);

        private void OnRewardClicked(int slot)
        {
            long gained = TryCollectRandomReward(slot);
            if (gained <= 0) hud?.ShowNotice("该奖励暂时无法领取");
        }

        public long TryCollectRandomReward(int slot)
        {
            string receipt = "home:reward:" + slotId + ":" + slot + ":" + Guid.NewGuid().ToString("N");
            bool collected = session.CollectRandomReward(slot, receipt);
            if (!collected) return 0;
            DrainHomeInventory();
            rewards?.Refresh();
            hud?.RefreshAll();
            return 1;
        }
        private void OnFurnitureClicked(int type)
        {
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return;
            HomeFurnitureState state = FurnitureState(type);
            switch (state)
            {
                case HomeFurnitureState.Functional:
                    if (type == OfflineFruitTreeService.ProductionFurnitureType
                        || type == OfflineFruitTreeService.PetFurnitureType)
                        OnFruitTreeClicked();
                    else if (!hud.IsClosedByNetworkPolicy(type)) OpenFurniture(type);
                    break;
                case HomeFurnitureState.CanRepair:
                {
                    if (pendingFruitTreeRepairTutorialType == type)
                    {
                        pendingFruitTreeRepairTutorialType = -1;
                        hud.HideFruitTreeTutorialMask();
                        hud.ShowRepair(type);
                        hud.ShowFruitTreeRepairButtonMask();
                        break;
                    }
                    OfflineFruitTreeProgress tutorialState = profile.Save.Player.FruitTree;
                    if (type == OfflineFruitTreeService.ProductionFurnitureType
                        && !tutorialState.ProductionRepairStorySeen)
                        hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 0, () =>
                        {
                            pendingFruitTreeRepairTutorialType = type;
                            hud.ShowFruitTreeFurnitureMask(type);
                        });
                    else if (type == OfflineFruitTreeService.PetFurnitureType
                        && !tutorialState.PetRepairStorySeen)
                        hud.ShowFruitTreeTutorial("FruitTreeUnlock2", 0, () =>
                        {
                            pendingFruitTreeRepairTutorialType = type;
                            hud.ShowFruitTreeFurnitureMask(type);
                        });
                    else hud.ShowRepair(type);
                    break;
                }
                case HomeFurnitureState.RepairNotice:
                    hud.ShowNotice(rules.IsMet(record.repairNotice, context)
                        ? HomeNoticeText(type)
                        : "尚未满足解锁条件");
                    break;
                case HomeFurnitureState.Locked:
                    OfflineFruitTreeProgress fruitState = profile.Save.Player.FruitTree;
                    if (type == OfflineFruitTreeService.ProductionFurnitureType
                        && !fruitState.ProductionRepaired && !fruitState.PetFacilityRepaired)
                        hud.ShowNotice(catalog.LocalizedText("HomeFruitTree.InitialStateTip", "该果树设施尚未开放"));
                    break;
            }
        }

        private string HomeNoticeText(int type)
        {
            HomeFurnitureRecord record = rules.FurnitureOf(type);
            if (record == null) return string.Empty;
            return "满足条件后可维修 " + rules.FurnitureNameForType(type);
        }

        private void OpenFurniture(int type)
        {
            if (type == 0) hud.ShowBed();
            else if (type == 10) hud.ShowCart();
            else if (type == 6) hud.ShowMail();
            else if (type == 9) hud.ShowTreasureGacha();
            else if (type == OfflineFruitTreeService.PetFurnitureType) hud.ShowPetGacha();
            else if (type == 5) OpenWishes();
            else hud.ShowFurniturePlaceholder(type);
        }

        private void OpenWishes()
        {
            if (wishes != null && wishes.IsOpen) return;
            Action refreshWishCurrency = null;
            wishes = WishScreenController.Open(profile, slotId, uiParent,
                slot => refreshWishCurrency = hud.BuildWishCurrencyBar(slot),
                () => refreshWishCurrency?.Invoke(),
                () =>
                {
                    wishes = null;
                    refreshWishCurrency = null;
                });
        }

        private void OnFruitTreeClicked()
        {
            if (!AdvanceFruitTree(Now()))
            {
                hud.HideFruitTreeTutorialMask();
                return;
            }
            OfflineFruitTreeProgress state = profile.Save.Player.FruitTree;
            if (state.ProductionRepaired && !state.ProductionRepairStorySeen)
            {
                hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 1, CompleteProductionRepairTutorial);
                return;
            }
            FruitTreeResult collected = fruitTree.Collect("home:fruit-tree:collect:" + slotId + ":"
                + Guid.NewGuid().ToString("N"));
            if (collected.IsApplied)
            {
                ReloadContext();
                hud.RefreshAll();
                ApplyFurnitureStates();
                Action showReward = () => hud.ShowNotice("收取 " + collected.CollectedCount + " 个果实，获得 "
                    + rules.ItemName(OfflineFruitTreeService.RewardItemId) + " × " + collected.CollectedCount);
                bool showCollectionTutorial = pendingFruitTreeCollectionTutorial
                    || !profile.Save.Player.FruitTree.CollectionTutorialSeen;
                pendingFruitTreeCollectionTutorial = false;
                hud.HideFruitTreeTutorialMask();
                if (showCollectionTutorial)
                    hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 2, () =>
                    {
                        fruitTree.MarkCollectionTutorialSeen();
                        showReward();
                    });
                else showReward();
                return;
            }

            if (profile.Save.Player.FruitTree.PetFacilityRepaired)
            {
                OpenFurniture(OfflineFruitTreeService.PetFurnitureType);
                return;
            }
            long seconds = fruitTree.SecondsUntilNextFruit();
            hud.ShowNotice(seconds == long.MaxValue ? "果树暂时没有成熟果实"
                : "下一颗果实将在 " + FormatFruitDuration(seconds) + " 后成熟");
        }

        private static string FormatFruitDuration(long seconds)
        {
            if (seconds < 60) return seconds + " 秒";
            if (seconds < 3600) return (seconds / 60) + " 分钟";
            return (seconds / 3600) + " 小时 " + (seconds % 3600 / 60) + " 分钟";
        }

        private bool AdvanceFruitTree(long now)
        {
            if (fruitTree == null) return true;
            FruitTreeResult result = fruitTree.AdvanceTo(now);
            if (result.Code == FruitTreeResultCode.ClockRollback)
            {
                if (!fruitClockRollbackWarned)
                {
                    if (hud == null) pendingFruitClockRollbackMessage = result.Message;
                    else hud.ShowNotice(result.Message);
                }
                fruitClockRollbackWarned = true;
                if (scene != null)
                    scene.SetFruitTreeState(FurnitureState(OfflineFruitTreeService.ProductionFurnitureType),
                        FurnitureState(OfflineFruitTreeService.PetFurnitureType),
                        profile.Save.Player.FruitTree.StoredCount);
                return false;
            }
            else if (profile.Save.Player.FruitTree.LastUpdateUnixSeconds <= now)
            {
                fruitClockRollbackWarned = false;
                pendingFruitClockRollbackMessage = null;
            }
            if (scene != null)
                scene.SetFruitTreeState(FurnitureState(OfflineFruitTreeService.ProductionFurnitureType),
                    FurnitureState(OfflineFruitTreeService.PetFurnitureType),
                    profile.Save.Player.FruitTree.StoredCount);
            return true;
        }

        public bool TryRepair(int type)
        {
            string receipt = "home:repair:" + slotId + ":" + type;
            bool repaired;
            if (type == OfflineFruitTreeService.ProductionFurnitureType
                || type == OfflineFruitTreeService.PetFurnitureType)
            {
                long now = Now();
                profile.Reload();
                ReloadContext();
                if (!AdvanceFruitTree(now)) return false;
                profile.Reload();
                ReloadContext();
                FruitTreeResult result = fruitTree.TryRepair(type, profile.Save.Player.Level,
                    context.AnyGoddessActive, now, receipt);
                repaired = result.IsApplied;
                if (!repaired) hud?.ShowNotice(result.Code == FruitTreeResultCode.ConditionNotMet
                    ? "尚未满足维修条件" : result.Message);
                if (repaired)
                {
                    ReloadContext();
                    scene?.PlayFruitTreeRepairEffect(type);
                }
            }
            else
            {
                repaired = homeProgression.RepairFurniture(session, type, context, profile.InventoryCount,
                    (item, count) => ConsumeItems(item, count, receipt + ":cost"), receipt);
            }
            if (repaired)
            {
                ApplyFurnitureStates();
                RefreshIncomeCatalog();
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return repaired;
        }

        public void OnFruitTreeRepairDialogClosed(int type)
        {
            OfflineFruitTreeProgress state = profile.Save.Player.FruitTree;
            if (type == OfflineFruitTreeService.ProductionFurnitureType && !state.ProductionRepairStorySeen)
                hud.ShowFruitTreeTutorial("FruitTreeUnlock1", 1,
                    CompleteProductionRepairTutorial);
            else if (type == OfflineFruitTreeService.PetFurnitureType && !state.PetRepairStorySeen)
                hud.ShowFruitTreeTutorial("FruitTreeUnlock2", 1,
                    () => fruitTree.MarkRepairStorySeen(type));
        }

        private void CompleteProductionRepairTutorial()
        {
            FruitTreeResult result = fruitTree.MarkRepairStorySeen(OfflineFruitTreeService.ProductionFurnitureType);
            if (result.Code != FruitTreeResultCode.Applied && result.Code != FruitTreeResultCode.Unchanged)
            {
                hud.ShowNotice("果树教学状态未能保存");
                return;
            }
            pendingFruitTreeCollectionTutorial = true;
            hud.ShowFruitTreeCollectionMask();
        }

        public long TryCollect(int hangUpId)
        {
            string receipt = "home:collect:" + slotId + ":" + hangUpId + ":" + Guid.NewGuid().ToString("N");
            long count = session.Collect(hangUpId, receipt);
            if (count > 0)
            {
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return count;
        }

        public long TryFastForward(int hangUpId)
        {
            OfflineHomeIncomeDefinition definition = session.Definitions.FirstOrDefault(value => value.HangUpId == hangUpId);
            if (definition == null || definition.FastHangUpId <= 0) return 0;
            HomeFastHangUpRecord fast = rules.FastHangUp(definition.FastHangUpId);
            if (fast == null) return 0;
            long total = 0;
            foreach (int id in fast.hangUpIdList ?? Array.Empty<int>())
            {
                string receipt = "home:fast:" + slotId + ":" + id + ":" + Guid.NewGuid().ToString("N");
                total += session.ApplyFastForward(id, fast.freeHours, receipt);
            }
            if (total > 0)
            {
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return total;
        }

        public void TryOpenMail() => hud.ShowMail();

        public bool TryUnlockHangUp(int hangUpId)
        {
            bool unlocked = homeProgression.UnlockHangUp(session, hangUpId, context,
                "home:unlock:" + slotId + ":" + hangUpId);
            if (unlocked)
            {
                RefreshIncomeCatalog();
                hud.RefreshAll();
            }
            return unlocked;
        }

        public HomeLevelUpResult TryLevelUpHangUp(int hangUpId)
        {
            HomeLevelUpResult result = homeProgression.LevelUp(session, hangUpId, context, BlessingLevel,
                profile.InventoryCount, (item, count) => ConsumeItems(item, count,
                    "home:levelup:" + slotId + ":" + hangUpId + ":" + item), "home:levelup:" + slotId + ":" + hangUpId);
            if (result.Success)
            {
                RefreshIncomeCatalog();
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return result;
        }

        private bool ConsumeItems(int itemId, long count, string receipt)
        {
            if (itemId <= 0 || count <= 0) return true;
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
                if (stack == null || stack.Count < count) throw new InvalidOperationException("Not enough items.");
                stack.Count -= count;
                if (stack.UnitRecords != null && stack.UnitRecords.Count > stack.Count)
                    stack.UnitRecords.RemoveRange((int)stack.Count, stack.UnitRecords.Count - (int)stack.Count);
                if (stack.Count == 0) save.Player.Inventory.Remove(stack);
            });
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged && result.Status != ProgressionResultStatus.Ready)
                return false;
            ReloadContext();
            return true;
        }

        /// <summary>Moves claimed home rewards into the real bag or character experience.</summary>
        public void DrainHomeInventory()
        {
            foreach (OfflineHomeItemStack stack in session.Save.Inventory.ToArray())
            {
                if (stack == null || stack.ItemId <= 0 || stack.Count <= 0) continue;
                string receipt = "home:grant:" + slotId + ":" + stack.Serial;
                if (stack.ItemId == 50)
                {
                    try
                    {
                        EnsureProgression();
                        progression.AwardExperience(stack.Count, receipt);
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning("Home experience could not be granted: " + error.Message);
                        continue;
                    }
                }
                else
                {
                    ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
                    {
                        OfflineItemStack target = save.Player.Inventory.FirstOrDefault(value => value.ItemId == stack.ItemId);
                        if (target == null)
                        {
                            target = new OfflineItemStack { ItemId = stack.ItemId };
                            save.Player.Inventory.Add(target);
                        }
                        target.Count += stack.Count;
                    });
                    if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged && result.Status != ProgressionResultStatus.Ready)
                        continue;
                }
                session.AcknowledgeInventory(stack.Serial, receipt + ":ack");
            }
            ReloadContext();
        }

        public long ItemCount(int itemId) => profile == null ? 0 : profile.InventoryCount(itemId);

        public string ItemName(int itemId) => rules.ItemName(itemId);

        public string ItemNameForHangUp(int hangUpId)
        {
            HomeHangUpRecord record = rules.HangUp(hangUpId);
            HomeHangUpRewardRecord reward = record == null ? null : rules.HangUpReward(record.rewardId);
            HomeRewardRuleRecord rule = reward == null ? null : rules.SelectRewardRule(reward.rewardRuleId, context);
            int itemId = rule != null && rule.ids != null && rule.ids.Length > 0 ? rule.ids[0] : 0;
            string name = rules.ItemName(itemId);
            return string.IsNullOrEmpty(name) ? "挂机产出 " + hangUpId : name;
        }

        public Sprite ItemIcon(int itemId)
        {
            string file = rules.ItemIcon(itemId);
            return string.IsNullOrEmpty(file) ? null : catalog.FindSprite(file);
        }

        public void Close()
        {
            WishScreenController activeWishes = wishes;
            wishes = null;
            if (activeWishes != null) activeWishes.Close();
            if (rewards != null) rewards.Dispose();
            if (scene != null) scene.Dispose();
            scene = null;
            if (catalog != null) catalog.Release();
            catalog = null;
            if (hud != null) hud.Dispose();
            hud = null;
            onClose?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            input?.Update();
            environment?.Tick(Time.unscaledDeltaTime);
            scene?.Tick(Time.unscaledDeltaTime);
            hud?.Tick();
            wishes?.Tick();
            advanceTimer += Time.unscaledDeltaTime;
            if (advanceTimer < 5f) return;
            advanceTimer = 0;
            long now = Now();
            if (now == lastAdvanceUnix) return;
            lastAdvanceUnix = now;
            try
            {
                if (!AdvanceFruitTree(now)) return;
                session.AdvanceTo(now);
                rewards?.Refresh();
                DrainHomeInventory();
                hud?.RefreshAll();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Home income advance failed: " + error.Message);
            }
        }

        private void OnDestroy()
        {
            if (input != null) input.FurnitureClicked -= OnFurnitureClicked;
            StopFruitTreeTutorialVoice();
        }
    }
}
