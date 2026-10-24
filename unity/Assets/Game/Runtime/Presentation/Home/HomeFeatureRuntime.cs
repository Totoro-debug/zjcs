using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Inventory;
using Zjcs.Presentation.Cooking;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Wishes;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Entry point of the offline home module.  It opens the character save and
    /// the home save, rebuilds the official scene and HUD, and routes every
    /// furniture action through the offline rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeFeatureRuntime : MonoBehaviour, IInventoryHomeNavigationContext
    {
        private const int OfficialMailFunctionType = 52;
        private const int HomeTutorialFixedRewardId = 7;
        private const string HomeTutorialFixedRewardReceipt = "home:tutorial:fixed-reward:7";
        private ProfileSlotCatalog slots;
        private OfflineMainlineProgressSession profile;
        private OfflineCharacterProgressionService progression;
        private OfflineAstrologicalService astrological;
        private float astrologicalChargeStartTime;
        private float astrologicalNextChargeTick;
        private bool equipmentPropertiesLoaded;
        private OfficialHomeRules rules;
        private HomeTutorialCatalog tutorials;
        private OfflineHomeProgression homeProgression;
        private WishRuntimeCatalog tutorialRewardCatalog;
        private OfflineHomeIncomeSession session;
        private HomeAssetCatalog catalog;
        private HomeSceneView scene;
        private HomePlayerActorView homePlayer;
        private HomeInputController input;
        private HomeEnvironmentView environment;
        private HomeMeteorView meteorView;
        private HomeRewardView rewards;
        private HomeHudView hud;
        private TreasureGachaRuntimeCatalog treasureGachaCatalog;
        private OfflineTreasureGachaService treasureGacha;
        private PetGachaRuntimeCatalog petGachaCatalog;
        private OfflinePetGachaService petGacha;
        private OfficialPetGachaAssetCatalog petGachaAssets;
        private OfflineFruitTreeService fruitTree;
        private WishScreenController wishes;
        private CookingFeatureRuntime cookingRuntime;
        private HomeShopScreenController homeShop;
        private HomeTutorialHost tutorialHost;
        private HomeTutorialRunner tutorialRunner;
        private HomeConditionContext context;
        private Action onClose;
        private Action onOpenMainland;
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
        private HomeSettingsProfile homeSettings;
        private AudioSource settingsClickSource;
        private AudioClip settingsClickClip;
        private bool settingsClickLoading;
        private bool tutorialAutoStartEnabled = true;

        public event Action<int> FurnitureClickedForTutorial;
        public event Action<int> FurnitureRepairedForTutorial;
        public event Action<int, long, string> HangUpCollectedForTutorial;
        public event Action<string> FastForwardAppliedForTutorial;

        public string ActiveProfileSlotId => slotId;
        public OfflineHomeIncomeSession Session => session;
        public OfflineHomeProgression HomeProgression => homeProgression;
        public HomeAssetCatalog Catalog => catalog;
        public CookingFeatureRuntime CookingRuntime => cookingRuntime;
        public HomeHudView Hud => hud;
        public OfficialHomeRules Rules => rules;
        public HomeTutorialCatalog Tutorials => tutorials;
        public HomeSettingsProfile HomeSettings => homeSettings;
        public OfflineMainlineProgressSession Profile => profile;
        public OfflineAstrologicalService Astrological
        {
            get
            {
                EnsureAstrological();
                return astrological;
            }
        }
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
        public HomeTutorialRunner TutorialRunner => tutorialRunner;
        public bool TutorialAutoStartEnabled
        {
            get => tutorialAutoStartEnabled;
            set => tutorialAutoStartEnabled = value;
        }
        public bool IsWishScreenOpen => wishes != null && wishes.IsOpen;

        public bool HasFurnitureForSkin(int type)
        {
            if (type == OfflineFruitTreeService.ProductionFurnitureType)
                return profile != null && profile.Save.Player.FruitTree.ProductionRepaired;
            if (type == OfflineFruitTreeService.PetFurnitureType)
                return profile != null && profile.Save.Player.FruitTree.PetFacilityRepaired;
            return session != null && session.OwnsFurniture(type);
        }

        public void RefreshRewards() => rewards?.Refresh();

        public void RefreshCounts() => hud?.RefreshCounts();

        public float CookingMaxHp()
        {
            EnsureProgression();
            if (!equipmentPropertiesLoaded)
                throw new InvalidOperationException("Official equipment properties are required for cooking.");
            CharacterPropCalculator calculator = progression.PropertyCalculator;
            CharacterPropSnapshot snapshot = calculator.Evaluate(profile.Save.Player, Now());
            if (!snapshot.OutOfFightBattleProps.TryGetValue(calculator.Props.Id("MaxHp"), out long maxHp)
                || maxHp <= 0)
                throw new InvalidOperationException("The official character MaxHp property is unavailable.");
            return maxHp;
        }

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
        public bool FeatureModalOpen => cookingRuntime != null && cookingRuntime.gameObject.activeInHierarchy
            || homeShop != null && homeShop.IsOpen;

        public static HomeFeatureRuntime Begin(string selectedSlotId, string savesRoot, Transform uiParent,
            Action onClose = null, Action onOpenMainland = null)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("An existing profile slot is required.", nameof(selectedSlotId));
            var go = new GameObject("HomeGameRuntime");
            HomeFeatureRuntime runtime = go.AddComponent<HomeFeatureRuntime>();
            runtime.Open(selectedSlotId, string.IsNullOrEmpty(savesRoot) ? Application.persistentDataPath : savesRoot,
                uiParent, onClose, onOpenMainland);
            return runtime;
        }

        private void Open(string selected, string root, Transform uiParent, Action close, Action openMainland)
        {
            slotId = selected;
            savesRoot = root;
            this.uiParent = uiParent;
            onClose = close;
            onOpenMainland = openMainland;
            slots = new ProfileSlotCatalog(root);
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile does not exist.");
            homeSettings = new HomeSettingsProfile(Path.Combine(slots.GetSlotDirectory(slotId), "settings.json"));
            homeSettings.Load();
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
            catalog.LoadPanelExtension(Path.Combine(Application.streamingAssetsPath,
                "Home", "Settings", "settings-ui.json"));
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
            tutorials = HomeTutorialCatalog.LoadDefault();
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
            BuildHomePlayer();
            environment = new HomeEnvironmentView(scene, Now(), profile.Save.CreatedAtUnixSeconds);
            meteorView = new HomeMeteorView(scene, environment.MeteorRuntime);
            environment.MeteorTriggered += meteorView.Play;
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
            HomeAudioSettings.Register(fruitTreeTutorialVoiceSource, HomeAudioChannel.Voice);
            if (!string.IsNullOrEmpty(pendingFruitClockRollbackMessage))
            {
                hud.ShowNotice(pendingFruitClockRollbackMessage);
                pendingFruitClockRollbackMessage = null;
            }
            ApplyFurnitureStates();
            scene.ApplyHomeSkins(catalog.HomeSkins, session.Skins, rules);
            RefreshIncomeCatalog();
            DrainHomeInventory();
            hud.RefreshAll();
            tutorialHost = new HomeTutorialHost(this, hud);
            tutorialRunner = new HomeTutorialRunner(tutorials, tutorialHost,
                Path.Combine(slots.GetSlotDirectory(slotId), "home-tutorials.json"));
            tutorialHost.RecordOptionSelection = tutorialRunner.RecordOptionSelection;
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public string FormatTopBarCurrency(long count)
        {
            return homeSettings == null
                ? count.ToString(CultureInfo.InvariantCulture)
                : homeSettings.FormatNumber(count);
        }

        public void PlaySettingsClick()
        {
            if (settingsClickSource == null)
            {
                settingsClickSource = gameObject.AddComponent<AudioSource>();
                settingsClickSource.playOnAwake = false;
                settingsClickSource.loop = false;
                settingsClickSource.spatialBlend = 0f;
                HomeAudioSettings.Register(settingsClickSource, HomeAudioChannel.Sound);
            }
            if (settingsClickClip != null)
            {
                settingsClickSource.PlayOneShot(settingsClickClip);
                return;
            }
            if (!settingsClickLoading) StartCoroutine(LoadSettingsClick());
        }

        private IEnumerator LoadSettingsClick()
        {
            settingsClickLoading = true;
            string path = Path.Combine(Application.streamingAssetsPath, "Home", "Settings", "click.wav");
            if (!File.Exists(path) || !Uri.TryCreate(path, UriKind.Absolute, out Uri uriValue))
            {
                settingsClickLoading = false;
                Debug.LogWarning("Official Settings click audio is unavailable.");
                yield break;
            }
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uriValue.AbsoluteUri,
                AudioType.WAV))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    settingsClickLoading = false;
                    Debug.LogWarning("Official Settings click audio could not be loaded: " + request.error);
                    yield break;
                }
                settingsClickClip = DownloadHandlerAudioClip.GetContent(request);
            }
            settingsClickLoading = false;
            if (settingsClickClip != null && settingsClickSource != null)
                settingsClickSource.PlayOneShot(settingsClickClip);
        }

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
                StoryCount = profile.StoryCount,
                StoryStatus = profile.StoryStatus,
                HasItemOfType = itemType => player.Inventory.Any(stack => stack != null && stack.Count > 0
                    && rules.Item(stack.ItemId)?.type == itemType),
                AnyGoddessActive = profile.Save.World.ActivatedGoddessRegions != null
                    && profile.Save.World.ActivatedGoddessRegions.Count > 0,
                HasManualMeetNpc = player.HasManualMeetNpc,
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
            EnsureProgression();
            if (!equipmentPropertiesLoaded)
                throw new InvalidOperationException("Official equipment properties are required for home stats.");
            CharacterPropSnapshot snapshot = progression.PropertyCalculator.Evaluate(
                profile.Save.Player, profile.Save.World, Now());
            foreach (KeyValuePair<int, long> pair in snapshot.OutOfFightBattleProps)
                merged[pair.Key] = pair.Value;
            return merged;
        }

        private void EnsureProgression()
        {
            if (progression != null) return;
            OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            OfficialCharacterCatalog character = characterAssets.Load();
            progression = new OfflineCharacterProgressionService(profile, character);
            try
            {
                progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
                progression.AttachSkillRules(characterAssets.LoadSkillRules());
                equipmentPropertiesLoaded = true;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Official equipment properties are required for home stats.", error);
            }
        }

        private void BuildHomePlayer()
        {
            EnsureProgression();
            CharacterCreationAssetCatalog creationAssets = CharacterCreationAssetCatalog.LoadDefault();
            var creationService = new CharacterCreationOfflineService(slots, creationAssets,
                OfficialCharacterAssetCatalog.LoadDefault());
            homePlayer = new HomePlayerActorView(scene,
                Path.Combine(Application.streamingAssetsPath, HomeAssetCatalog.FolderName, "player-runtime.json"),
                profile.Save.Player.Wardrobe, progression, creationAssets, creationService);
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

        public bool CanRepairFurnitureForTutorial(int type)
        {
            HomeFurnitureRecord record = rules?.FurnitureOf(type);
            return record != null && session != null && !session.OwnsFurniture(type)
                && rules.IsMet(record.unlock, context)
                && (record.repairCost ?? Array.Empty<HomeCostEntry>())
                    .Where(cost => cost != null && cost.itemId > 0 && cost.count > 0)
                    .All(cost => profile.InventoryCount(cost.itemId) >= cost.count);
        }

        public bool CanUnlockAnyHangUpForFurniture(int furnitureType) => rules != null && session != null
            && rules.HangUpsOf(furnitureType).Any(record => record != null
                && !session.IsHangUpUnlocked(record.id) && rules.IsMet(record.unlock, context));

        public void SetTutorialCameraDragEnabled(bool enabled)
        {
            if (input != null) input.CameraDragEnabled = enabled;
        }

        public bool TutorialFastForwardReceiptApplied(string nodeReceipt) =>
            !string.IsNullOrEmpty(nodeReceipt) && session != null
            && session.Save.AppliedReceipts.Any(value => value != null
                && value.StartsWith(nodeReceipt + ":fast:", StringComparison.Ordinal));

        public bool TutorialCanGetFixedReward(int fixedRewardId)
        {
            WishFixedReward reward = TutorialFixedReward(fixedRewardId);
            return profile != null && reward != null && reward.CanGetTimes > 0
                && !profile.Save.Player.AppliedInventoryEventIds.Contains(HomeTutorialFixedRewardReceipt);
        }

        public bool TryClaimTutorialFixedReward(int fixedRewardId, string nodeReceipt, out string reason)
        {
            reason = null;
            WishFixedReward fixedReward = TutorialFixedReward(fixedRewardId);
            if (fixedReward == null)
            {
                reason = "No official guide fixed-reward rule is installed for reward " + fixedRewardId + ".";
                return false;
            }
            if (string.IsNullOrWhiteSpace(nodeReceipt))
            {
                reason = "The fixed-reward tutorial node has no durable execution receipt.";
                return false;
            }
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(HomeTutorialFixedRewardReceipt)) return true;

            WishAmount[] rewards = fixedReward.Reward ?? Array.Empty<WishAmount>();
            if (rewards.Length == 0)
            {
                reason = "Official fixed reward " + fixedRewardId + " has no reward items.";
                return false;
            }
            foreach (WishAmount reward in rewards)
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0 || rules.Item(reward.ItemId) == null)
                {
                    reason = "Official fixed reward " + fixedRewardId
                        + " references an invalid or missing local item " + (reward == null ? 0 : reward.ItemId) + ".";
                    return false;
                }

            ProgressionResult result = profile.ApplyInventoryTransaction(HomeTutorialFixedRewardReceipt, save =>
            {
                foreach (WishAmount reward in rewards)
                {
                    InventoryOperationResult grant = OfflineInventoryOperations.Grant(save.Player,
                        reward.ItemId, reward.Count, Now());
                    if (grant.Status != InventoryOperationStatus.Applied)
                        throw new InvalidOperationException("Official fixed reward item grant failed: " + reward.ItemId);
                }
            });
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged)
            {
                reason = "Official fixed reward could not be committed locally: " + result.Message;
                return false;
            }
            ReloadContext();
            hud?.RefreshAll();
            hud?.ShowTutorialFixedReward(rewards);
            return true;
        }

        private WishFixedReward TutorialFixedReward(int fixedRewardId)
        {
            if (tutorialRewardCatalog == null)
            {
                TextAsset source = Resources.Load<TextAsset>("Wishes/wishes-runtime");
                if (source == null) throw new InvalidOperationException("The pinned official wish rules are missing.");
                tutorialRewardCatalog = WishRuntimeCatalog.Parse(source.text);
            }
            if (fixedRewardId != HomeTutorialFixedRewardId) return null;
            return tutorialRewardCatalog.FixedReward(fixedRewardId);
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
            FurnitureClickedForTutorial?.Invoke(type);
            if (type == 6)
            {
                if (rules.IsFunctionLimited(OfficialMailFunctionType))
                    hud.ShowNotice("邮箱暂不可用");
                else hud.ShowMail();
                return;
            }
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
            else if (type == 7) hud.ShowStorage();
            else if (type == 9) hud.ShowTreasureGacha();
            else if (type == OfflineFruitTreeService.PetFurnitureType) hud.ShowPetGacha();
            else if (type == 5) OpenWishes();
            else if (type == 3) OpenCooking();
            else if (type == 8) OpenHomeShop();
            else if (type == 12)
            {
                OfflineAstrologicalService service = Astrological;
                int rank = profile.Save.Player.Rank;
                if (service.SeasonForRank(rank) < service.MinimumSeason)
                    hud.ShowNotice("当前位阶尚未开放命运星象");
                else hud.ShowAstrologicalDestiny();
            }
            else hud.ShowFurniturePlaceholder(type);
        }

        public bool BeginAstrologicalCharge()
        {
            EnsureAstrological();
            float now = Time.time;
            if (!astrological.BeginCharge(now))
            {
                hud?.ShowNotice(astrological.CurrentCost >= astrological.MaximumCost
                    ? "星命契约已达上限" : "原初之星不足");
                return false;
            }
            astrologicalChargeStartTime = now;
            astrologicalNextChargeTick = now;
            hud?.RefreshAstrologicalDestiny();
            return true;
        }

        public void EndAstrologicalCharge()
        {
            if (astrological == null || !astrological.IsCharging) return;
            ProgressionResult result = astrological.EndCharge("home:astrological:" + slotId + ":"
                + Guid.NewGuid().ToString("N"));
            if (result.Status == ProgressionResultStatus.InvalidState)
                hud?.ShowNotice(result.Message ?? "星命契约灌注失败");
            else if (result.IsApplied)
                hud?.RefreshAll();
            hud?.RefreshAstrologicalDestiny();
        }

        private void EnsureAstrological()
        {
            EnsureProgression();
            astrological ??= new OfflineAstrologicalService(profile, progression.Catalog);
        }

        private void TickAstrologicalCharge()
        {
            if (astrological == null || !astrological.IsCharging || Time.time < astrologicalNextChargeTick)
                return;
            float now = Time.time;
            float damping = Mathf.Clamp01(1f - ((astrologicalChargeStartTime + 1.5f) - now) / 1.5f);
            astrological.StepCharge(now, damping);
            astrologicalNextChargeTick += 0.05f;
            hud?.RefreshAstrologicalDestiny();
        }

        private void OpenCooking()
        {
            if (cookingRuntime != null && cookingRuntime.gameObject != null) return;
            try
            {
                cookingRuntime = CookingFeatureRuntime.Open(profile, hud.CanvasTransform, CookingMaxHp);
            }
            catch (Exception error)
            {
                cookingRuntime = null;
                Debug.LogWarning("Home cooking unavailable: " + error.Message);
                hud.ShowNotice("料理页面打开失败：" + error.Message);
            }
        }

        private void OpenHomeShop()
        {
            if (homeShop != null && homeShop.IsOpen) return;
            try
            {
                homeShop = HomeShopScreenController.Open(this, hud.CanvasTransform);
            }
            catch (Exception error)
            {
                homeShop = null;
                Debug.LogWarning("Home shop unavailable: " + error.Message);
                hud.ShowNotice("售货机页面打开失败：" + error.Message);
            }
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
                string costReceipt = receipt + ":cost:" + System.Guid.NewGuid().ToString("N");
                repaired = homeProgression.RepairFurniture(session, type, context, profile.InventoryCount,
                    costs => ConsumeItems(costs, costReceipt), costs => RestoreItems(costs, costReceipt + ":refund"), receipt);
            }
            if (repaired)
            {
                ApplyFurnitureStates();
                RefreshIncomeCatalog();
                DrainHomeInventory();
                hud.RefreshAll();
                FurnitureRepairedForTutorial?.Invoke(type);
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
            string receipt = tutorialHost?.CollectionReceiptFor(hangUpId)
                ?? ("home:collect:" + slotId + ":" + hangUpId + ":" + Guid.NewGuid().ToString("N"));
            long count = session.Collect(hangUpId, receipt);
            if (count > 0)
            {
                HangUpCollectedForTutorial?.Invoke(hangUpId, count, receipt);
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
            string receipt = tutorialHost?.FastForwardReceiptFor(hangUpId, fast.fastHangUpId)
                ?? ("home:fast:" + slotId + ":" + fast.fastHangUpId + ":" + Guid.NewGuid().ToString("N"));
            long total = session.ApplyFreeFastForwardGroup(fast.fastHangUpId,
                fast.hangUpIdList ?? Array.Empty<int>(), fast.freeHours, receipt);
            if (total > 0)
            {
                FastForwardAppliedForTutorial?.Invoke(receipt);
                DrainHomeInventory();
                hud.RefreshAll();
            }
            return total;
        }

        public long TryFastForwardItems(int fastHangUpId, int itemCount)
        {
            HomeFastHangUpRecord fast = rules.FastHangUp(fastHangUpId);
            HomeFastHangUpItem item = fast?.items?.FirstOrDefault(value => value != null && value.itemId > 0);
            if (fast == null || item == null || itemCount <= 0
                || itemCount > Math.Min(100, fast.maxUseItemCount)
                || profile.InventoryCount(item.itemId) < itemCount) return 0;
            int[] unlocked = (fast.hangUpIdList ?? Array.Empty<int>())
                .Where(id => session.Definitions.Any(definition => definition.HangUpId == id && definition.Unlocked))
                .Distinct().ToArray();
            if (unlocked.Length == 0) return 0;
            string receipt = "home:fast-item:" + slotId + ":" + Guid.NewGuid().ToString("N");
            if (!ConsumeItems(item.itemId, itemCount, receipt + ":cost")) return 0;
            long total = session.ApplyFastForwardBatch(unlocked, itemCount * item.hours, receipt + ":income");
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
            HomeLevelUpResult check = homeProgression.CheckLevelUp(session, hangUpId, context, BlessingLevel,
                profile.InventoryCount);
            if (!check.CanApply) return check;
            long now = Now();
            if (now < session.Save.LastLocalUnixSeconds)
            {
                check.CanApply = false;
                check.Error = "clock_rollback";
                return check;
            }
            session.AdvanceTo(now);
            long settled = session.Collect(hangUpId, "home:levelup-settle:" + slotId + ":" + hangUpId
                + ":" + session.Save.LastLocalUnixSeconds);
            if (settled > 0) DrainHomeInventory();
            string costReceipt = "home:levelup:" + slotId + ":" + hangUpId + ":cost:"
                + System.Guid.NewGuid().ToString("N");
            HomeLevelUpResult result = homeProgression.LevelUp(session, hangUpId, context, BlessingLevel,
                profile.InventoryCount, costs => ConsumeItems(costs, costReceipt),
                costs => RestoreItems(costs, costReceipt + ":refund"),
                "home:levelup:" + slotId + ":" + hangUpId + ":" + check.Level);
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

        private bool ConsumeItems(IReadOnlyList<HomeCostEntry> costs, string receipt)
        {
            if (costs == null || costs.Count == 0) return true;
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (IGrouping<int, HomeCostEntry> group in costs
                    .Where(value => value != null && value.itemId > 0 && value.count > 0)
                    .GroupBy(value => value.itemId))
                {
                    long count = group.Aggregate(0L, (total, value) => checked(total + value.count));
                    OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == group.Key);
                    if (stack == null || stack.Count < count) throw new InvalidOperationException("Not enough items.");
                    stack.Count -= count;
                    if (stack.UnitRecords != null && stack.UnitRecords.Count > stack.Count)
                        stack.UnitRecords.RemoveRange((int)stack.Count, stack.UnitRecords.Count - (int)stack.Count);
                    if (stack.Count == 0) save.Player.Inventory.Remove(stack);
                }
            });
            if (!InventoryTransactionSucceeded(result.Status)) return false;
            ReloadContext();
            return true;
        }

        private void RestoreItems(IReadOnlyList<HomeCostEntry> costs, string receipt)
        {
            if (costs == null || costs.Count == 0) return;
            ProgressionResult result = profile.ApplyInventoryTransaction(receipt, save =>
            {
                foreach (IGrouping<int, HomeCostEntry> group in costs
                    .Where(value => value != null && value.itemId > 0 && value.count > 0)
                    .GroupBy(value => value.itemId))
                {
                    long count = group.Aggregate(0L, (total, value) => checked(total + value.count));
                    OfflineItemStack stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == group.Key);
                    if (stack == null)
                    {
                        stack = new OfflineItemStack { ItemId = group.Key };
                        save.Player.Inventory.Add(stack);
                    }
                    stack.Count = checked(stack.Count + count);
                }
            });
            if (!InventoryTransactionSucceeded(result.Status))
                Debug.LogError("Home upgrade cost compensation failed: " + result.Status);
            else ReloadContext();
        }

        private static bool InventoryTransactionSucceeded(ProgressionResultStatus status)
        {
            return status == ProgressionResultStatus.Applied || status == ProgressionResultStatus.Unchanged
                || status == ProgressionResultStatus.Ready;
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

        public IReadOnlyList<int> TreasureGachaSourceTypes(int itemId)
        {
            if (treasureGacha == null) return Array.Empty<int>();
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var types = new List<int>(2);
            if (treasureGacha.ResolveItemSource(itemId, now) != null) types.Add(0);
            TreasureItemConfig sourceItem = treasureGacha.Catalog.Item(itemId);
            TreasureGachaSourceNavigation elementSource = sourceItem == null ? null
                : treasureGacha.ResolveElementItemSource(itemId, sourceItem.ElementType, now);
            if (elementSource != null) types.Add(elementSource.SelectedElementGachaType);
            return types.Distinct().ToArray();
        }

        public bool OpenTreasureGachaSource(int itemId, int gachaType, Action<int> returnToSource)
        {
            if (gachaType == 0)
                return OpenTreasureGachaFromItemSource(itemId, 0, returnToSource) != null;
            TreasureItemConfig sourceItem = treasureGacha?.Catalog.Item(itemId);
            return sourceItem != null && this.OpenTreasureGachaFromElementItemSource(itemId,
                sourceItem.ElementType, returnToSource) != null;
        }

        public void ShowInventoryNotice(string message) => hud?.ShowNotice(message);

        public bool EnterMainland()
        {
            if (onOpenMainland == null)
            {
                hud?.ShowNotice("大陆入口当前不可用");
                return false;
            }
            onOpenMainland();
            return true;
        }

        public void Close()
        {
            Close(true);
        }

        public void CloseForNavigation()
        {
            Close(false);
        }

        private void Close(bool invokeCallback)
        {
            tutorialRunner?.Suspend();
            tutorialRunner = null;
            tutorialHost?.Dispose();
            tutorialHost = null;
            WishScreenController activeWishes = wishes;
            wishes = null;
            if (activeWishes != null) activeWishes.Close();
            if (rewards != null) rewards.Dispose();
            homePlayer?.Dispose();
            homePlayer = null;
            if (environment != null && meteorView != null)
                environment.MeteorTriggered -= meteorView.Play;
            meteorView?.Dispose();
            meteorView = null;
            environment?.Dispose();
            environment = null;
            if (scene != null) scene.Dispose();
            scene = null;
            if (catalog != null) catalog.Release();
            catalog = null;
            if (hud != null) hud.Dispose();
            hud = null;
            if (invokeCallback) onClose?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            input?.Update();
            TickAstrologicalCharge();
            environment?.Tick(Time.unscaledDeltaTime);
            scene?.Tick(Time.unscaledDeltaTime);
            hud?.Tick();
            wishes?.Tick();
            if (tutorialAutoStartEnabled && tutorialRunner != null)
            {
                if (!tutorialRunner.IsBlocked && !tutorialRunner.HasActiveGuide && !tutorialRunner.HasQueuedGuide)
                {
                    HomeTutorialGuide next = tutorials.Guides.OrderBy(value => value.order)
                        .FirstOrDefault(value => !tutorialRunner.IsGuideCompleted(value.name));
                    if (next != null) tutorialRunner.QueueGuide(next.name);
                }
                tutorialRunner.Tick(Time.unscaledDeltaTime);
            }
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
            EndAstrologicalCharge();
            tutorialRunner?.Suspend();
            tutorialRunner = null;
            tutorialHost?.Dispose();
            tutorialHost = null;
            if (input != null) input.FurnitureClicked -= OnFurnitureClicked;
            if (environment != null && meteorView != null)
                environment.MeteorTriggered -= meteorView.Play;
            meteorView?.Dispose();
            meteorView = null;
            environment?.Dispose();
            environment = null;
            homePlayer?.Dispose();
            homePlayer = null;
            StopFruitTreeTutorialVoice();
            HomeAudioSettings.Unregister(fruitTreeTutorialVoiceSource);
            HomeAudioSettings.Unregister(settingsClickSource);
            if (settingsClickClip != null) Destroy(settingsClickClip);
            settingsClickClip = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) tutorialRunner?.Suspend();
        }

        private void OnApplicationQuit() => tutorialRunner?.Suspend();
    }
}
