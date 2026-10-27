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
        private readonly Dictionary<int, Map10RuntimeBattleEntityStageBinding> battleEntityStageBindings =
            new Dictionary<int, Map10RuntimeBattleEntityStageBinding>();
        private readonly Stack<Map10RuntimeStoryFrame> storyFrames = new Stack<Map10RuntimeStoryFrame>();
        private readonly Dictionary<int, Map10RuntimeFixedReward> fixedRewards = new Dictionary<int, Map10RuntimeFixedReward>();
        private readonly Dictionary<int, Map13RuntimeHandInItem> handInItems = new Dictionary<int, Map13RuntimeHandInItem>();
        private readonly Dictionary<int, Map10RuntimeEntityTemplate> entityTemplates =
            new Dictionary<int, Map10RuntimeEntityTemplate>();
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
        private OfficialMap10DialoguePortraitView dialoguePortraits;
        private OfficialMap10DialoguePortraitView optionDialoguePortraits;
        private WorldDialogueTypeWriter dialogueTypeWriter;
        private bool dialogueContentSwitchWaiting;
        private OfficialBattleRuntimeController battleController;
        private FightDamageFormula damageFormula;
        private Map10RuntimeEntity selectedStageEntity;
        private Action selectedStageCompletion;
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
        private GameObject firstBattleNpc;
        private GameObject chaosNpc;
        private bool closed;
        private int mapClassId = 10;
        private bool localSaveCreated;

        private OfficialMainlineMapProfile MapProfile => OfficialMainlineMapProfile.Get(mapClassId);
        private string MapResource(string path) => MapProfile.Resource(path);
        private string NativeUiResource(string path) => MapProfile.NativeUi(path);
        private string MapReceiptPrefix => "map" + mapClassId + ":";

        public static int ResolveHomeReturnMap(OfflineMainlineSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Player?.UnlockedMapClassIds?.Contains(12) == true) return 12;
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
            avatar = MapProfile.HasNativeUi ? MountMap11Player(characterModel) : characterModel;
            characterName = displayName ?? string.Empty;
            closeCallback = onClose;
            // Original FollowSceneActiveSelfController enables the global
            // map light only in its active Unity scene. Map12 closes through
            // this runtime's existing lifecycle and has no scene transition.
            if (mapClassId != 12) DontDestroyOnLoad(gameObject);

            try
            {
                InitializeModules();
                LoadOfficialData();
                InitializeMap();
                LoadLocalProgress();
                regions.RefreshRegionProgress();
                InitializeView();
                InitializeMapAudio();
                navigation.currentGrid = new MapGridPoint(localSave.gridX, localSave.gridY);
                navigation.SetAvatarPosition(navigation.currentGrid);
                hud.UpdateWorldLabels();
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
            FlushPuzzleRegionProgress();
            FlushMap12ChestProgress();
            stamina.RefreshStaminaPoints();
            exploration.RefreshExploration();
            dialoguePortraits?.Tick(Time.unscaledDeltaTime);
            optionDialoguePortraits?.Tick(Time.unscaledDeltaTime);
            miniMap.UpdateMiniMapFilter();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (exploration.exploreWindows.Count != 0) exploration.CloseExploreUi();
                else if (hud.worldFunctions != null) hud.CloseNativeWorldFunctions();
                else if (miniMap.fullMiniMapPanel != null) miniMap.CloseNativeFullMiniMap();
                else if (hud.quickBuyDialog != null) hud.quickBuyDialog.Cancel();
                else if (statues.witchDialog != null) statues.RequestWitchDialogClose();
                else if (dialogueWaiting) RequestDialogueAdvance();
                else if (storyRunning) { }
                else if (battleController != null) LeaveBattle();
                else if (statues.goddessDialog != null) statues.goddessDialog.Close();
                else if (selectedStageEntity != null) CloseStageDialog();
                else if (map12BuildingDialog != null) CloseMap12BuildingDialog();
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

            if (hud.worldFunctions != null || miniMap.fullMiniMapPanel != null || (storyRunning && battleController == null) || statues.goddessDialog != null || statues.witchDialog != null) return;

            if (battleController != null)
            {
                if (battleController.IsPresentingStep)
                {
                    battleStepTimer = Mathf.Max(0f, battleStepTimer - Time.deltaTime);
                    return;
                }
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
                            if (battleController.CanCompleteBattle) FinishBattle();
                        }
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error, this);
                        battleStepTimer = 0.5f;
                        AbortBattle();
                        hud.ShowOfficialNotice("战斗未完成，本次结果未记录");
                    }
                }
                return;
            }

            if (statues.TryStartWitchSpawnBattle()) return;

            if (navigation.movementPath != null)
            {
                navigation.AdvanceMovement(Time.deltaTime);
                return;
            }

            navigation.ProcessMapPointerPress();
        }

        private void LateUpdate()
        {
            if (worldCamera == null || avatar == null || closed) return;
            RefreshNativeMap12State();
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
            miniMap.UpdateMinimapMarker();
        }

        private void InitializeMap()
        {
            var slots = new ProfileSlotCatalog(persistentDataPath);
            progressionSave = MapProfile.HasNativeUi
                ? OfflineMainlineProgressSession.Open(slots, slotId, OfficialMainlineTaskCatalog.LoadDefault())
                : OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            if (mapClassId == 11 && !HasClaimedMap11Unlock(progressionSave.Save))
                throw new InvalidOperationException("完成并领取 Map10 主线任务 1040 后才能进入森之国。");
            if (mapClassId == 12 && progressionSave.Save.Player.UnlockedMapClassIds?.Contains(12) != true)
                throw new InvalidOperationException("激活森之国方尖碑后才能进入山之国。");
            if (!progressionSave.Save.Player.CharacterCreated || progressionSave.Save.Player.InitialWeaponType == 0)
                throw new InvalidOperationException("Finish character creation and weapon selection before entering Map" + mapClassId + ".");
            OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            progression = new OfflineCharacterProgressionService(progressionSave, characterAssets.Load());
            progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
            session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), mapClassId);
            if (session.Definition.MaxRegionProgress == null || session.Definition.MaxRegionProgress.Length == 0)
                throw new InvalidDataException("Official Map" + mapClassId + " region progress definitions are missing.");
            this.regions.LoadRegionConditions();
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
            GameObject terrain = null;
            if (mapClassId != 12)
            {
                terrain = Instantiate(assets.GroundPrefab, mapRoot, false);
                terrain.name = "Map" + mapClassId + "Terrain";
            }

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
            string entityResource = MapResource(mapClassId == 12 ? "Entities/native-catalog" : "Entities/catalog");
            TextAsset entityData = Resources.Load<TextAsset>(entityResource);
            if (entityData == null) throw new InvalidOperationException("Official Map" + mapClassId + " entity data is missing.");
            Vector2 mapOrigin = new Vector2(session.Definition.Navigation.Origin.X,
                session.Definition.Navigation.Origin.Y);
            entityView.Initialize(entityData, entityRoot, mapOrigin, worldCamera, mapClassId);
            entityView.EntitySelected += navigation.OnEntitySelected;
            mapEntities.Clear();
            foreach (OfficialMap10EntityInfo info in entityView.EntityInfos)
            {
                if (info == null || info.PlacementId <= 0 || info.Grid == null || info.Grid.Length != 2
                    || mapEntities.ContainsKey(info.PlacementId))
                    throw new InvalidDataException("Official Map" + mapClassId + " entity placement is malformed or duplicated.");
                var entity = new Map10RuntimeEntity(info);
                if (MapProfile.HasNativeUi && !entityTemplates.ContainsKey(info.EntityClassId))
                    throw new InvalidDataException("Official Map11 EC entity template is missing: " + info.EntityClassId);
                if (mapLogicTags.TryGetValue(info.PlacementId, out Map10RuntimeLogicTag tag))
                {
                    if (tag.EntityClass != info.EntityClassId)
                        throw new InvalidDataException("Official Map10 entity tag class differs from its placement: " + info.PlacementId);
                    entity.TalkTag = tag.Tag;
                }
                mapEntities.Add(info.PlacementId, entity);
            }
            if (MapProfile.HasNativeUi) { exploration.ExploreService.Cleanup(OfficialMainlineExplorationModule.ExploreNow); exploration.SynchronizeExploreEntities(); }
            InitializeMap11WorldMonsters();
            InitializeStorySpatial();
            ValidateBattleEntityStageBindings();
            ValidateEntityLockGroups();
            InitializePuzzles();
            statues.RecoverWitchStates();
            statues.RecoverHeroStates();

            hud.Mount();
            miniMap.CreateMinimap(assets.MiniMapPrefab);

            avatar.transform.SetParent(mapRoot, false);
            avatar.name = "Map" + mapClassId + "Player_" + slotId;
            if (localSaveCreated && MapProfile.HasNativeUi)
            {
                int entryId = GetDefaultEntryId();
                if (!mapEntities.TryGetValue(entryId, out Map10RuntimeEntity entry))
                    throw new InvalidDataException("Official Map11 default entry placement is missing: " + entryId);
                localSave.gridX = entry.Grid.X;
                localSave.gridY = entry.Grid.Y;
            }
            navigation.currentGrid = new MapGridPoint(localSave.gridX, localSave.gridY);
            navigation.SetAvatarPosition(navigation.currentGrid);
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
            if (mapClassId == 12) InitializeNativeMap12();
            else regions.InitializeFog(terrain.transform);
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
            worldCamera.nearClipPlane = MapProfile.HasNativeUi ? map11WorldViewSource.cameraNear : 0.1f;
            worldCamera.farClipPlane = MapProfile.HasNativeUi ? map11WorldViewSource.cameraFar : 100f;
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
            // A story can revisit the same count/status after cancellation or a
            // selector branch. The session already skips an unchanged value;
            // a permanent event ID for the value would suppress later changes.
            ProgressionResult result = progressionSave.SetStoryProgress(name, count, status);
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
            CloseHandInView();
            if (map12BuildingDialog != null) Destroy(map12BuildingDialog);
            map12BuildingDialog = null;
            nativeMap12World?.Dispose();
            miniMap.CloseNativeFullMiniMap();
            hud.CloseNativeWorldFunctions();
            hud.DisposePortrait();
            DisposeMapAudio();
            statues.CloseGoddessDialog();
            statues.CloseWitchDialog();
            exploration.Dispose();
            hud.DisposeQuickBuy();
            if (entityView != null) entityView.EntitySelected -= navigation.OnEntitySelected;
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
            hud.ui?.Dispose();
            if (avatar != null) Destroy(avatar);
            miniMap.DisposeRendering();
            if (worldCamera != null && worldCamera.transform.IsChildOf(transform))
                Destroy(worldCamera.gameObject);
            if (mapRoot != null) Destroy(mapRoot.gameObject);
            if (battleRoot != null) Destroy(battleRoot.gameObject);
            hud.DisposeCommerce();
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
