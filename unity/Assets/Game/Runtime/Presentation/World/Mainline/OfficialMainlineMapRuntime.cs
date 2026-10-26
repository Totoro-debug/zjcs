using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Zjcs.Presentation.World", null, "OfficialMap10GameRuntime")]
    [DisallowMultipleComponent]
    public sealed partial class OfficialMainlineMapRuntime : MonoBehaviour
    {
        private const float MovementSpeed = 4f;

        private sealed class Map10RuntimeStoryFrame
        {
            public Map10RuntimeStory Parent;
            public int NextNodeId;
        }

        private readonly Dictionary<int, Map10RuntimeRoster> rosters = new Dictionary<int, Map10RuntimeRoster>();
        private readonly Dictionary<int, Map10RuntimeStage> stages = new Dictionary<int, Map10RuntimeStage>();
        private readonly Dictionary<int, Map10RuntimeBattleScene> battleScenes = new Dictionary<int, Map10RuntimeBattleScene>();
        private readonly Dictionary<int, Map10RuntimeEntity> mapEntities = new Dictionary<int, Map10RuntimeEntity>();
        private readonly Dictionary<string, Map10RuntimeStory> stories = new Dictionary<string, Map10RuntimeStory>(StringComparer.Ordinal);
        private readonly Dictionary<string, Map10RuntimeStory> storySubgraphs = new Dictionary<string, Map10RuntimeStory>(StringComparer.Ordinal);
        private readonly Dictionary<int, Map10RuntimeDialogRole> dialogRoles = new Dictionary<int, Map10RuntimeDialogRole>();
        private readonly Dictionary<int, Map10RuntimeNativeDialog> nativeDialogs = new Dictionary<int, Map10RuntimeNativeDialog>();
        private readonly Dictionary<string, Map10RuntimePlayable> officialPlayables =
            new Dictionary<string, Map10RuntimePlayable>(StringComparer.Ordinal);
        private readonly Dictionary<int, Map10RuntimeLogicTag> mapLogicTags = new Dictionary<int, Map10RuntimeLogicTag>();
        private readonly Dictionary<int, Map10RuntimeTalkCondition> mapTalkConditions = new Dictionary<int, Map10RuntimeTalkCondition>();
        private readonly Dictionary<string, Map10RuntimeStoryOption> storyOptions = new Dictionary<string, Map10RuntimeStoryOption>(StringComparer.Ordinal);
        private readonly Dictionary<int, GameObject> storyEntities = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, int> storyEntityClasses = new Dictionary<int, int>();
        private Map10RuntimeMapTalkEntry[] mapTalkEntries = Array.Empty<Map10RuntimeMapTalkEntry>();
        private OfficialMainlineRegionPolicy regionPolicy;
        private Dictionary<int, OfficialMainlineRegionConfig> regionConfigs => regionPolicy.regionConfigs;
        private readonly Dictionary<int, int> regionPoints = new Dictionary<int, int>();
        private Dictionary<int, int> regionPointLimits = new Dictionary<int, int>();
        private readonly Dictionary<int, Map10RuntimeBattleEntityStageBinding> battleEntityStageBindings =
            new Dictionary<int, Map10RuntimeBattleEntityStageBinding>();
        private readonly Stack<Map10RuntimeStoryFrame> storyFrames = new Stack<Map10RuntimeStoryFrame>();
        private readonly Dictionary<int, Map10RuntimeFixedReward> fixedRewards = new Dictionary<int, Map10RuntimeFixedReward>();
        private readonly Dictionary<int, Map10RuntimeEntityTemplate> entityTemplates =
            new Dictionary<int, Map10RuntimeEntityTemplate>();
        private Map11RuntimeWitchInfo[] witchInfos = Array.Empty<Map11RuntimeWitchInfo>();
        private OfficialWorldMapSession session;
        private OfflineMainlineProgressSession progressionSave;
        private OfflineCharacterProgressionService progression;
        private Map10LocalSave localSave;
        private string slotId;
        private string persistentDataPath;
        private string mapSavePath;
        private string characterName;
        private Action closeCallback;
        private Transform uiParent;
        private Transform mapRoot;
        private Transform entityRoot;
        private Transform battleRoot;
        private GameObject avatar;
        private Camera worldCamera;
        private OfficialMap10EntityView entityView;
        private OfficialMap10EnvironmentView environmentView;
        private OfficialMainlineRegionFog regionFog;
        private OfficialMap10UiCatalog uiCatalog;
        private OfficialWorldUiRenderer ui;
        private OfficialMap10DialoguePortraitView dialoguePortraits;
        private OfficialMap10DialoguePortraitView optionDialoguePortraits;
        private WorldDialogueTypeWriter dialogueTypeWriter;
        private bool dialogueContentSwitchWaiting;
        private OfficialBattleRuntimeController battleController;
        private FightDamageFormula damageFormula;
        private Map10RuntimeEntity pendingInteraction;
        private Map10RuntimeEntity selectedStageEntity;
        private Action selectedStageCompletion;
        private List<MapGridPoint> movementPath;
        private int movementIndex;
        private Action movementCompletion;
        private MapGridPoint currentGrid;
        private Vector3 cameraVelocity;
        private float battleStepTimer;
        private bool dialogueWaiting;
        private bool dialogueAdvanceRequested;
        private string dialoguePanelKey = "TalkDialog";
        private bool nativeDialogActive;
        private bool nativeDialogChoicesWaiting;
        private bool nativeDialogPlayableWaiting;
        private Action dialogueFinished;
        private int dialogueLineIndex;
        private Map10RuntimeDialogueLine[] dialogueLines = Array.Empty<Map10RuntimeDialogueLine>();
        private Map10RuntimeNativeDialog activeNativeDialog;
        private Map10RuntimeEntity nativeDialogContext;
        private Action nativeDialogCompletion;
        private GameObject nativeDialogNpc;
        private int nativeDialogNodeId;
        private int[] nativeDialogChoiceTargets = Array.Empty<int>();
        private Map10RuntimeStory activeStory;
        private string activeStoryRootKey = string.Empty;
        private Map10RuntimeEntity storyContextEntity;
        private Action storyFinished;
        private bool storyRunning;
        private bool storyOptionsWaiting;
        private bool storyWorldUiHidden;
        private int storyOptionCount;
        private Action<int> storyOptionSelected;
        private GameObject storyHiddenContextEntity;
        private Vector3 storyCameraTarget;
        private Vector3 storyCameraVelocity;
        private bool storyCameraActive;
        private float storyCameraSizeTarget;
        private Coroutine noticeRoutine;
        private RawImage minimapImage;
        private RenderTexture minimapTexture;
        private Camera minimapCamera;
        private RectTransform minimapPlayerMarker;
        private GameObject firstBattleNpc;
        private GameObject chaosNpc;
        private bool closed;
        private CommerceScreenController commerceScreen;
        private GameObject firstPayEntry;
        private int mapClassId = 10;
        private bool localSaveCreated;

        private OfficialMainlineMapProfile MapProfile => OfficialMainlineMapProfile.Get(mapClassId);
        private string MapResource(string path) => MapProfile.Resource(path);
        private string NativeUiResource(string path) => MapProfile.NativeUi(path);
        private string MapReceiptPrefix => "map" + mapClassId + ":";

        public static int ResolveHomeReturnMap(OfflineMainlineSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            return HasClaimedMap11Unlock(save) ? 11 : 10;
        }

        private static bool HasClaimedMap11Unlock(OfflineMainlineSave save) =>
            save?.Tasks?.ClaimedMainlineTaskIds?.Contains(1040) == true;

        public void Begin(string selectedSlotId, string savesRoot, Transform targetUiParent,
            GameObject characterModel, string displayName, Action onClose)
        {
            Begin(10, selectedSlotId, savesRoot, targetUiParent, characterModel, displayName, onClose);
        }

        public void Begin(int selectedMapClassId, string selectedSlotId, string savesRoot,
            Transform targetUiParent, GameObject characterModel, string displayName, Action onClose)
        {
            if (session != null) throw new InvalidOperationException("A world map is already open.");
            OfficialMainlineMapProfile.Get(selectedMapClassId);
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId)) throw new ArgumentException("Invalid profile slot id.", nameof(selectedSlotId));
            if (string.IsNullOrWhiteSpace(savesRoot)) throw new ArgumentException("A persistent data root is required.", nameof(savesRoot));
            if (targetUiParent == null) throw new ArgumentNullException(nameof(targetUiParent));
            if (characterModel == null) throw new ArgumentNullException(nameof(characterModel));

            mapClassId = selectedMapClassId;
            slotId = selectedSlotId;
            persistentDataPath = Path.GetFullPath(savesRoot);
            mapSavePath = Path.Combine(new ProfileSlotCatalog(persistentDataPath).GetSlotDirectory(slotId),
                "map-" + mapClassId + "-save.json");
            uiParent = targetUiParent;
            avatar = selectedMapClassId == 11 ? MountMap11Player(characterModel) : characterModel;
            characterName = displayName ?? string.Empty;
            closeCallback = onClose;
            DontDestroyOnLoad(gameObject);

            try
            {
                LoadOfficialData();
                InitializeMap();
                LoadLocalProgress();
                RefreshRegionProgress();
                InitializeView();
                InitializeMapAudio();
                currentGrid = new MapGridPoint(localSave.gridX, localSave.gridY);
                SetAvatarPosition(currentGrid);
                UpdateWorldLabels();
                if (mapClassId == 10 && !HasStory("FirstEnterMap")) PlayStory("FirstEnterMap", 0, () =>
                {
                    CompleteStory("FirstEnterMap");
                    SaveLocalProgress();
                    StartFirstBattleIntroduction();
                });
                else if (mapClassId == 10) StartFirstBattleIntroduction();
            }
            catch
            {
                DisposeRuntime();
                throw;
            }
        }

        private void Update()
        {
            if (session == null || closed) return;
            RefreshStaminaPoints();
            RefreshExploration();
            dialoguePortraits?.Tick(Time.unscaledDeltaTime);
            optionDialoguePortraits?.Tick(Time.unscaledDeltaTime);
            UpdateMiniMapFilter();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (exploreWindows.Count != 0) CloseExploreUi();
                else if (worldFunctions != null) CloseNativeWorldFunctions();
                else if (fullMiniMapPanel != null) CloseNativeFullMiniMap();
                else if (quickBuyDialog != null) quickBuyDialog.Cancel();
                else if (witchDialog != null) RequestWitchDialogClose();
                else if (dialogueWaiting) RequestDialogueAdvance();
                else if (storyRunning) { }
                else if (battleController != null) LeaveBattle();
                else if (goddessDialog != null) goddessDialog.Close();
                else if (selectedStageEntity != null) CloseStageDialog();
                else CloseMap();
            }

            if (dialogueWaiting)
            {
                if (dialogueAdvanceRequested)
                {
                    if (nativeDialogActive) AdvanceNativeDialog();
                    else AdvanceDialogue();
                }
                return;
            }

            if (worldFunctions != null || fullMiniMapPanel != null || (storyRunning && battleController == null) || goddessDialog != null || witchDialog != null) return;

            if (battleController != null)
            {
                if (battleController.Checkpoint != null && !battleController.CanAdvance
                    && !battleController.Checkpoint.Finished) return;
                battleStepTimer -= Time.deltaTime;
                if (battleStepTimer <= 0f)
                {
                    try
                    {
                        if (battleController.Checkpoint == null || battleController.Checkpoint.Finished)
                            FinishBattle();
                        else
                        {
                            battleController.Advance();
                            battleStepTimer = battleController.LastStepSeconds;
                            SaveActiveBattle(false);
                            if (battleController.Checkpoint.Finished) FinishBattle();
                        }
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error, this);
                        battleStepTimer = 0.5f;
                        AbortBattle();
                        ShowOfficialNotice("战斗未完成，本次结果未记录");
                    }
                }
                return;
            }

            if (TryStartWitchSpawnBattle()) return;

            if (movementPath != null)
            {
                AdvanceMovement(Time.deltaTime);
                return;
            }

            ProcessMapPointerPress();
        }

        private void LateUpdate()
        {
            if (worldCamera == null || avatar == null || closed) return;
            if (UpdateBattleSceneCamera()) return;
            RefreshMap11CameraResolution();
            Vector3 target = storyCameraActive
                ? storyCameraTarget
                : avatar.transform.position + new Vector3(0f, 0f, -20f);
            worldCamera.transform.position = storyCameraActive
                ? Vector3.SmoothDamp(worldCamera.transform.position, target, ref storyCameraVelocity, 0.18f)
                : Vector3.SmoothDamp(worldCamera.transform.position, target, ref cameraVelocity, 0.18f);
            if (storyCameraActive)
                worldCamera.orthographicSize = Mathf.MoveTowards(worldCamera.orthographicSize,
                    storyCameraSizeTarget, Time.unscaledDeltaTime * 12f);
            UpdateMinimapMarker();
        }

        private void InitializeMap()
        {
            var slots = new ProfileSlotCatalog(persistentDataPath);
            progressionSave = mapClassId == 11
                ? OfflineMainlineProgressSession.Open(slots, slotId, OfficialMainlineTaskCatalog.LoadDefault())
                : OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            if (mapClassId == 11 && !HasClaimedMap11Unlock(progressionSave.Save))
                throw new InvalidOperationException("完成并领取 Map10 主线任务 1040 后才能进入森之国。");
            if (!progressionSave.Save.Player.CharacterCreated || progressionSave.Save.Player.InitialWeaponType == 0)
                throw new InvalidOperationException("Finish character creation and weapon selection before entering Map" + mapClassId + ".");
            OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            progression = new OfflineCharacterProgressionService(progressionSave, characterAssets.Load());
            progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
            session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), mapClassId);
            if (session.Definition.MaxRegionProgress == null || session.Definition.MaxRegionProgress.Length == 0)
                throw new InvalidDataException("Official Map" + mapClassId + " region progress definitions are missing.");
            LoadRegionConditions();
            int[] regions = session.Definition.MaxRegionProgress.Select(value => value.Region).ToArray();
            ProgressionResult initialized = progressionSave.InitializeMap(mapClassId, regions);
            if (initialized.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(initialized.Message);
        }

        private void InitializeView()
        {
            mapRoot = new GameObject("OfficialMap" + mapClassId + "World").transform;
            mapRoot.SetParent(transform, false);
            WorldMapAssets assets = session.Assets;
            GameObject terrain = Instantiate(assets.GroundPrefab, mapRoot, false);
            terrain.name = "Map" + mapClassId + "Terrain";

            if (mapClassId == 10)
            {
                var environmentObject = new GameObject("Map10Environment");
                environmentObject.transform.SetParent(mapRoot, false);
                environmentView = environmentObject.AddComponent<OfficialMap10EnvironmentView>();
                environmentView.Initialize();
            }

            CreateWorldCamera();

            var entityObject = new GameObject("Map" + mapClassId + "EntityRuntime");
            entityObject.transform.SetParent(mapRoot, false);
            entityRoot = entityObject.transform;
            entityView = entityObject.AddComponent<OfficialMap10EntityView>();
            string entityResource = MapResource("Entities/catalog");
            TextAsset entityData = Resources.Load<TextAsset>(entityResource);
            if (entityData == null) throw new InvalidOperationException("Official Map" + mapClassId + " entity data is missing.");
            Vector2 mapOrigin = new Vector2(session.Definition.Navigation.Origin.X,
                session.Definition.Navigation.Origin.Y);
            entityView.Initialize(entityData, entityRoot, mapOrigin, worldCamera, mapClassId);
            entityView.EntitySelected += OnEntitySelected;
            mapEntities.Clear();
            foreach (OfficialMap10EntityInfo info in entityView.EntityInfos)
            {
                if (info == null || info.PlacementId <= 0 || info.Grid == null || info.Grid.Length != 2
                    || mapEntities.ContainsKey(info.PlacementId))
                    throw new InvalidDataException("Official Map" + mapClassId + " entity placement is malformed or duplicated.");
                var entity = new Map10RuntimeEntity(info);
                if (mapClassId == 11 && !entityTemplates.ContainsKey(info.EntityClassId))
                    throw new InvalidDataException("Official Map11 EC entity template is missing: " + info.EntityClassId);
                if (mapLogicTags.TryGetValue(info.PlacementId, out Map10RuntimeLogicTag tag))
                {
                    if (tag.EntityClass != info.EntityClassId)
                        throw new InvalidDataException("Official Map10 entity tag class differs from its placement: " + info.PlacementId);
                    entity.TalkTag = tag.Tag;
                }
                mapEntities.Add(info.PlacementId, entity);
            }
            if (mapClassId == 11) { ExploreService.Cleanup(ExploreNow); SynchronizeExploreEntities(); }
            InitializeMap11WorldMonsters();
            InitializeStorySpatial();
            ValidateBattleEntityStageBindings();
            ValidateEntityLockGroups();
            InitializePuzzles();
            RecoverWitchStates();
            RecoverHeroStates();

            uiCatalog = OfficialMap10UiCatalog.LoadDefault();
            ui = new OfficialWorldUiRenderer(uiCatalog);
            ui.Mount(uiParent, BindWorldControl, mapClassId);
            if (mapClassId == 11) InitializeNativeWorldHud();
            ui.SetPanelActive("WorldCanvas", true);
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", characterName);
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/LevelBgImg/LevelTxt",
                progression.Player.Level.ToString(CultureInfo.InvariantCulture) + "级");
            ui.SetPanelActive("TalkDialog", false);
            CreateCommerceHudEntries();
            CreateMinimap(assets.MiniMapPrefab);

            avatar.transform.SetParent(mapRoot, false);
            avatar.name = "Map" + mapClassId + "Player_" + slotId;
            if (localSaveCreated && mapClassId == 11)
            {
                int entryId = GetDefaultEntryId();
                if (!mapEntities.TryGetValue(entryId, out Map10RuntimeEntity entry))
                    throw new InvalidDataException("Official Map11 default entry placement is missing: " + entryId);
                localSave.gridX = entry.Grid.X;
                localSave.gridY = entry.Grid.Y;
            }
            currentGrid = new MapGridPoint(localSave.gridX, localSave.gridY);
            SetAvatarPosition(currentGrid);
            progressionSave.Save.Player.UnlockedMapClassIds ??= new List<int>();
            if (!progressionSave.Save.Player.UnlockedMapClassIds.Contains(mapClassId))
            {
                OfflinePlayerProgress player = progressionSave.Save.Player;
                ProgressionResult unlocked = progressionSave.SetPlayerProgress(player.Level, player.Rank,
                    player.SubRank, player.MaxProfessionRank, player.GuildName ?? string.Empty,
                    player.UnlockedMapClassIds.Concat(new[] { mapClassId }));
                if (unlocked.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(unlocked.Message);
            }
            SaveLocalProgress();
            RefreshEntityActivity();
            if (mapClassId == 11)
            {
                regionFog = new GameObject("Official region fog").AddComponent<OfficialMainlineRegionFog>();
                regionFog.transform.SetParent(mapRoot, false);
                regionFog.Initialize(worldCamera, terrain.transform, IsPublicRegion);
                regionFog.BindCamera(minimapCamera);
            }
        }

        private void CreateWorldCamera()
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                var cameraObject = new GameObject("Map10WorldCamera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(transform, false);
                worldCamera = cameraObject.AddComponent<Camera>();
                worldCamera.orthographic = true;
                worldCamera.orthographicSize = 7.5f;
                worldCamera.clearFlags = CameraClearFlags.SolidColor;
                worldCamera.backgroundColor = new Color(0.08f, 0.12f, 0.12f, 1f);
                worldCamera.transform.position = new Vector3(0f, 0f, -20f);
            }
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = DefaultWorldCameraSize();
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.08f, 0.12f, 0.12f, 1f);
            worldCamera.nearClipPlane = mapClassId == 11 ? map11WorldViewSource.cameraNear : 0.1f;
            worldCamera.farClipPlane = mapClassId == 11 ? map11WorldViewSource.cameraFar : 100f;
            worldCamera.cullingMask &= ~(1 << 30);
            worldCamera.transform.position = avatar.transform.position + new Vector3(0f, 0f, -20f);
        }

        private int GetDefaultEntryId()
        {
            return GetWorldInfo().DefEntryId;
        }

        private Map10RuntimeWorldInfo GetWorldInfo()
        {
            foreach (OfficialWorldConfigRecord record in session.Definition.Configs ?? Array.Empty<OfficialWorldConfigRecord>())
            {
                if (record == null || record.TableName != "TbMapWorldInfo") continue;
                Map10RuntimeWorldInfo info = JsonUtility.FromJson<Map10RuntimeWorldInfo>(record.Json);
                if (info != null && info.ClassId == mapClassId) return info;
            }
            throw new InvalidDataException("Official Map" + mapClassId + " default entry is missing.");
        }

        private bool HasStory(string name) => localSave.completedStories.Contains(name)
            || progressionSave.StoryStatus(name) == 2;

        private int GetStoryPlayCount(string name) => Math.Max(
            localSave.storyPlayCounts.FirstOrDefault(value => value != null && value.key == name)?.count ?? 0,
            progressionSave.StoryCount(name));

        private void IncrementStoryPlayCount(string name)
        {
            Map10RuntimeStoryCount current = localSave.storyPlayCounts
                .FirstOrDefault(value => value != null && value.key == name);
            int nextCount = checked(GetStoryPlayCount(name) + 1);
            if (current == null)
            {
                localSave.storyPlayCounts = localSave.storyPlayCounts.Concat(new[]
                {
                    new Map10RuntimeStoryCount { key = name, count = nextCount }
                }).ToArray();
            }
            else current.count = nextCount;
            SetStoryProgress(name, nextCount, 2);
        }

        private void CompleteStory(string name)
        {
            if (!HasStory(name)) localSave.completedStories = localSave.completedStories.Concat(new[] { name }).ToArray();
            SetStoryStatus(name, 2);
        }

        private void SetStoryStatus(string name, int status)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            SetStoryProgress(name, GetStoryPlayCount(name), status);
        }

        private void SetStoryProgress(string name, int count, int status)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            ProgressionResult result = progressionSave.SetStoryProgress(name, count, status,
                MapReceiptPrefix + slotId + ":story:" + name + ":" + count + ":" + status);
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(result.Message);
            Map10RuntimeStoryCount current = localSave.storyPlayCounts
                .FirstOrDefault(value => value != null && value.key == name);
            if (current == null)
                localSave.storyPlayCounts = localSave.storyPlayCounts.Concat(new[]
                {
                    new Map10RuntimeStoryCount { key = name, count = count }
                }).ToArray();
            else current.count = count;
            SaveLocalProgress();
        }

        private void CloseMap()
        {
            if (closed) return;
            closed = true;
            DisposeRuntime();
            closeCallback?.Invoke();
            Destroy(gameObject);
        }

        private void DisposeRuntime()
        {
            CloseNativeFullMiniMap();
            CloseNativeWorldFunctions();
            if (playerHeadTexture != null) Destroy(playerHeadTexture);
            playerHeadTexture = null;
            DisposeMapAudio();
            CloseGoddessDialog();
            CloseWitchDialog();
            foreach (var window in exploreWindows) { ReleaseExploreMask(window); window.Destroy(); }
            exploreWindows.Clear();
            foreach (var texture in exploreSnapshots.Values) RenderTexture.ReleaseTemporary(texture);
            exploreSnapshots.Clear();
            exploreMasks.Clear();
            if (exploreCanvas != null) Destroy(exploreCanvas);
            exploreCanvas = null;
            // Dispose the modal without running its story continuation while the map closes.
            quickBuyDialog?.Abandon();
            if (quickBuyCanvas != null) Destroy(quickBuyCanvas);
            quickBuyCanvas = null;
            quickBuyDialog = null;
            if (entityView != null) entityView.EntitySelected -= OnEntitySelected;
            if (battleController != null)
            {
                battleController.HudControlClicked -= OnBattleHudControl;
                battleController.DisposeSession();
                Destroy(battleController.gameObject);
            }
            dialoguePortraits?.Dispose();
            dialoguePortraits = null;
            optionDialoguePortraits?.Dispose();
            optionDialoguePortraits = null;
            ui?.Dispose();
            if (avatar != null) Destroy(avatar);
            if (minimapCamera != null) minimapCamera.targetTexture = null;
            if (minimapTexture != null)
            {
                minimapTexture.Release();
                Destroy(minimapTexture);
            }
            if (minimapImage != null) Destroy(minimapImage.gameObject);
            if (worldCamera != null && worldCamera.transform.IsChildOf(transform))
                Destroy(worldCamera.gameObject);
            if (mapRoot != null) Destroy(mapRoot.gameObject);
            if (battleRoot != null) Destroy(battleRoot.gameObject);
            if (commerceScreen != null) Destroy(commerceScreen.gameObject);
            commerceScreen = null;
            if (firstPayEntry != null) Destroy(firstPayEntry);
            firstPayEntry = null;
            session = null;
        }

        private void OnDestroy()
        {
            if (!closed && session != null) DisposeRuntime();
        }

        private static List<FightPropertyValue> ToProperties(IReadOnlyDictionary<int, long> values)
        {
            return values.OrderBy(value => value.Key)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
        }

        /// <summary>
        /// Evaluates the official condition ids attached to the battle speed table against the local
        /// character. Types this save cannot prove stay locked instead of opening for free.
        /// </summary>
        private bool IsBattleSpeedConditionMet(int conditionId)
        {
            var context = new OfficialConditionContext { Level = progression.Player.Level };
            foreach (int mapClassId in progression.Player.UnlockedMapClassIds)
                context.UnlockedMapClassIds.Add(mapClassId);
            return progression.Catalog.EvaluateCondition(conditionId, context);
        }
        private static long Property(IDictionary<int, long> properties, int id)
        {
            return properties.TryGetValue(id, out long value) ? value : 0;
        }

        private static int CheckedProperty(IDictionary<int, long> properties, int id)
        {
            long value = Property(properties, id);
            return value <= 0 ? 1 : checked((int)Math.Min(value, int.MaxValue));
        }

        private static int StableSeed(string profile, int stage)
        {
            unchecked
            {
                int value = 17;
                foreach (char character in profile) value = value * 31 + character;
                return Math.Abs(value * 31 + stage);
            }
        }

        private static string StoryOptionKey(string storyName, int optionId) => storyName + "#" + optionId;

        private static bool Compare(int current, string operation, int target)
        {
            switch (operation)
            {
                case "=": return current == target;
                case ">": return current > target;
                case "<": return current < target;
                case ">=": return current >= target;
                case "<=": return current <= target;
                case "!=": return current != target;
                default: throw new InvalidDataException("Unsupported official MapTalk comparison: " + operation);
            }
        }

        private static int Manhattan(MapGridPoint left, MapGridPoint right)
        {
            return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }

        [Serializable]
        private sealed class Map10RuntimeWorldInfo
        {
            public int ClassId;
            public int DefEntryId;
            public int Type;
            public bool AllowPrivateTeam, AllowPublicTeam, CanExplore;
        }
    }
}
