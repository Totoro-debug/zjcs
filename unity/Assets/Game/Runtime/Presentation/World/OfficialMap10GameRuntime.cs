using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class OfficialMap10GameRuntime : MonoBehaviour
    {
        private const string BattleDataResource = "Map10/battle-runtime";
        private const string StoryDataResource = "Map10/stories";
        private const string EntityDataResource = "Map10/Entities/catalog";
        private const string MapSaveName = "map-10-save.json";
        private const float MovementSpeed = 4f;

        private readonly Dictionary<int, Map10RuntimeRoster> rosters = new Dictionary<int, Map10RuntimeRoster>();
        private readonly Dictionary<int, Map10RuntimeStage> stages = new Dictionary<int, Map10RuntimeStage>();
        private readonly Dictionary<int, Map10RuntimeBattleScene> battleScenes = new Dictionary<int, Map10RuntimeBattleScene>();
        private readonly Dictionary<int, Map10RuntimeEntity> mapEntities = new Dictionary<int, Map10RuntimeEntity>();
        private readonly Dictionary<string, Map10RuntimeStory> stories = new Dictionary<string, Map10RuntimeStory>(StringComparer.Ordinal);
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
        private readonly Dictionary<int, Map10RuntimeRegionConfig> regionConfigs = new Dictionary<int, Map10RuntimeRegionConfig>();
        private readonly Dictionary<int, Map10RuntimeCondition> mapConditions = new Dictionary<int, Map10RuntimeCondition>();
        private readonly Dictionary<int, int> regionPoints = new Dictionary<int, int>();
        private readonly Dictionary<int, int> regionPointLimits = new Dictionary<int, int>();
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
        private OfficialMap10UiCatalog uiCatalog;
        private OfficialMap10UiRenderer ui;
        private OfficialMap10DialoguePortraitView dialoguePortraits;
        private OfficialMap10DialoguePortraitView optionDialoguePortraits;
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

        public void Begin(string selectedSlotId, string savesRoot, Transform targetUiParent,
            GameObject characterModel, string displayName, Action onClose)
        {
            if (session != null) throw new InvalidOperationException("Map10 is already open.");
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId)) throw new ArgumentException("Invalid profile slot id.", nameof(selectedSlotId));
            if (string.IsNullOrWhiteSpace(savesRoot)) throw new ArgumentException("A persistent data root is required.", nameof(savesRoot));
            if (targetUiParent == null) throw new ArgumentNullException(nameof(targetUiParent));
            if (characterModel == null) throw new ArgumentNullException(nameof(characterModel));

            slotId = selectedSlotId;
            persistentDataPath = Path.GetFullPath(savesRoot);
            mapSavePath = Path.Combine(new ProfileSlotCatalog(persistentDataPath).GetSlotDirectory(slotId), MapSaveName);
            uiParent = targetUiParent;
            avatar = characterModel;
            characterName = displayName ?? string.Empty;
            closeCallback = onClose;
            DontDestroyOnLoad(gameObject);

            try
            {
                LoadOfficialData();
                LoadLocalProgress();
                InitializeMap();
                InitializeView();
                currentGrid = new MapGridPoint(localSave.gridX, localSave.gridY);
                SetAvatarPosition(currentGrid);
                UpdateWorldLabels();
                if (!HasStory("FirstEnterMap")) PlayStory("FirstEnterMap", 0, () =>
                {
                    CompleteStory("FirstEnterMap");
                    SaveLocalProgress();
                    StartFirstBattleIntroduction();
                });
                else StartFirstBattleIntroduction();
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
            dialoguePortraits?.Tick(Time.unscaledDeltaTime);
            optionDialoguePortraits?.Tick(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (dialogueWaiting) RequestDialogueAdvance();
                else if (storyRunning) { }
                else if (battleController != null) LeaveBattle();
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

            if (storyRunning) return;

            if (battleController != null)
            {
                battleStepTimer -= Time.deltaTime;
                if (battleStepTimer <= 0f)
                {
                    battleStepTimer = 0.35f;
                    try
                    {
                        if (battleController.Checkpoint == null || battleController.Checkpoint.Finished)
                            FinishBattle();
                        else
                        {
                            battleController.Advance();
                            if (battleController.Checkpoint.Finished) FinishBattle();
                        }
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error, this);
                        AbortBattle();
                        ShowOfficialNotice("战斗未完成，本次结果未记录");
                    }
                }
                return;
            }

            if (movementPath != null)
            {
                AdvanceMovement(Time.deltaTime);
                return;
            }

            if (Input.GetMouseButtonDown(0) && (EventSystem.current == null
                || !EventSystem.current.IsPointerOverGameObject()))
            {
                if (entityView == null || !entityView.TrySelectScreenPosition(Input.mousePosition))
                    TryMoveToPointer();
            }
        }

        private void LateUpdate()
        {
            if (worldCamera == null || avatar == null || closed) return;
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

        private void LoadOfficialData()
        {
            TextAsset battleData = Resources.Load<TextAsset>(BattleDataResource);
            TextAsset storyData = Resources.Load<TextAsset>(StoryDataResource);
            if (battleData == null || storyData == null)
                throw new InvalidOperationException("Official Map10 battle and story payloads are missing from Resources.");

            Map10RuntimeBattleData battles = JsonUtility.FromJson<Map10RuntimeBattleData>(battleData.text);
            Map10RuntimeStoryData storyDataValue = JsonUtility.FromJson<Map10RuntimeStoryData>(storyData.text);
            if (battles == null || battles.manifest != "854_156383" || battles.mapClassId != 10
                || battles.stages == null || battles.monsterRosters == null || battles.battleScenes == null
                || battles.stages.Length != 21 || battles.monsterRosters.Length != 21
                || storyDataValue == null || storyDataValue.manifest != "854_156383"
                || storyDataValue.mapClassId != 10 || storyDataValue.stories == null
                || storyDataValue.dialogRoles == null || storyDataValue.mapLogicTags == null
                || storyDataValue.mapTalkEntries == null || storyDataValue.mapTalkConditions == null
                || storyDataValue.storyOptions == null || storyDataValue.nativeDialogs == null
                || storyDataValue.playables == null)
                throw new InvalidDataException("Official Map10 runtime data does not match the pinned source.");

            foreach (Map10RuntimeStage value in battles.stages) stages.Add(value.Id, value);
            foreach (Map10RuntimeRoster value in battles.monsterRosters) rosters.Add(value.StageId, value);
            foreach (Map10RuntimeBattleScene value in battles.battleScenes) battleScenes.Add(value.GameId, value);
            foreach (Map10RuntimeStory value in storyDataValue.stories)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.key) || value.nodes == null
                    || value.nodes.Length == 0 || value.entryNodeId <= 0 || value.dialogueGroups == null)
                    throw new InvalidDataException("Official Map10 story graph is malformed.");
                stories.Add(value.key, value);
            }
            foreach (Map10RuntimeDialogRole value in storyDataValue.dialogRoles)
            {
                if (value == null || value.RoleId <= 0 || value.EntityClassId <= 0
                    || string.IsNullOrWhiteSpace(value.RoleName) || string.IsNullOrWhiteSpace(value.PortraitPath))
                    throw new InvalidDataException("Official Map10 dialogue role is malformed.");
                dialogRoles.Add(value.RoleId, value);
            }
            if (dialogRoles.Count != 4 || !dialogRoles.ContainsKey(840200) || !dialogRoles.ContainsKey(840401)
                || !dialogRoles.ContainsKey(840402) || !dialogRoles.ContainsKey(841700))
                throw new InvalidDataException("Official Map10 dialogue roles are incomplete.");
            foreach (Map10RuntimeNativeDialog value in storyDataValue.nativeDialogs)
            {
                if (value == null || value.Id <= 0 || value.EntryNodeId <= 0 || value.Nodes == null
                    || value.Nodes.Length == 0 || nativeDialogs.ContainsKey(value.Id))
                    throw new InvalidDataException("Official Map10 native dialog is malformed.");
                foreach (Map10RuntimeNativeDialogNode node in value.Nodes)
                {
                    if (node == null || node.NodeId <= 0 || node.NextNodes == null
                        || (node.NodeType == 0 && (string.IsNullOrEmpty(node.Text) || !dialogRoles.ContainsKey(node.RoleId)))
                        || (node.NodeType == 1 && string.IsNullOrEmpty(node.Text))
                        || (node.ActionType == 4 && (node.AwardItems == null || node.AwardItems.Length == 0)))
                        throw new InvalidDataException("Official Map10 native dialog node is malformed: " + value.Id);
                }
                nativeDialogs.Add(value.Id, value);
            }
            foreach (Map10RuntimePlayable value in storyDataValue.playables)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.AssetGuid) || value.Duration <= 0f
                    || officialPlayables.ContainsKey(value.AssetGuid))
                    throw new InvalidDataException("Official Map10 playable binding is malformed.");
                officialPlayables.Add(value.AssetGuid, value);
                foreach (string key in value.Keys ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(key) || officialPlayables.ContainsKey(key))
                        throw new InvalidDataException("Official Map10 playable key is duplicated or empty.");
                    officialPlayables.Add(key, value);
                }
            }
            if (!officialPlayables.ContainsKey("FeiLe_appear")
                || !officialPlayables.ContainsKey("FeiLe_disappear")
                || !officialPlayables.ContainsKey("9b248cde91299ee4287533b25b7dd468"))
                throw new InvalidDataException("Official Map10 story playable bindings are incomplete.");
            foreach (Map10RuntimeStory story in stories.Values)
                foreach (Map10RuntimeStoryNode node in story.nodes.Where(value => value != null
                             && value.type == "PlayPlayableNode"))
                    if (node.playableDuration <= 0f || !officialPlayables.ContainsKey(node.playableAssetGuid))
                        throw new InvalidDataException("Official Map10 story playable data is incomplete: " + story.key);
            foreach (Map10RuntimeLogicTag value in storyDataValue.mapLogicTags)
            {
                if (value == null || value.MapClass != 10 || value.LogicId <= 0
                    || value.EntityClass <= 0 || string.IsNullOrWhiteSpace(value.Tag))
                    throw new InvalidDataException("Official Map10 logic entity tag is malformed.");
                mapLogicTags.Add(value.LogicId, value);
            }
            foreach (Map10RuntimeTalkConditionInfo value in storyDataValue.mapTalkConditions)
            {
                if (value == null || value.ClassId <= 0 || value.Condition == null
                    || mapTalkConditions.ContainsKey(value.ClassId))
                    throw new InvalidDataException("Official Map10 MapTalk condition is malformed.");
                mapTalkConditions.Add(value.ClassId, value.Condition);
            }
            mapTalkEntries = storyDataValue.mapTalkEntries;
            foreach (Map10RuntimeStoryOption value in storyDataValue.storyOptions)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.StoryName) || value.Id <= 0
                    || string.IsNullOrWhiteSpace(value.Text))
                    throw new InvalidDataException("Official Map10 story option is malformed.");
                storyOptions.Add(StoryOptionKey(value.StoryName, value.Id), value);
            }
            string[] requiredStories =
            {
                "FirstEnterMap", "FirstBattle", "FirstMeetChaos", "SavingChick",
                "ChickYell", "ChickYell2", "ChickChat", "FangjianbeiActive", "UnlockFangBei"
            };
            if (stories.Count != requiredStories.Length || requiredStories.Any(name => !stories.ContainsKey(name)))
                throw new InvalidDataException("Official Map10 story payload is incomplete.");
        }

        private void LoadLocalProgress()
        {
            SaveReadResult read = LocalJsonSaveFile.Read(mapSavePath, ValidLocalSave);
            if (read.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("Map10 local save is corrupt; the profile remains unchanged.");
            localSave = read.Json == null
                ? new Map10LocalSave { slotId = slotId, gridX = 24, gridY = 23 }
                : JsonUtility.FromJson<Map10LocalSave>(read.Json);
            if (localSave == null || localSave.slotId != slotId)
                throw new InvalidDataException("Map10 save belongs to another profile slot.");
            localSave.completedStories ??= Array.Empty<string>();
            localSave.storyPlayCounts ??= Array.Empty<Map10RuntimeStoryCount>();
            localSave.completedStages ??= Array.Empty<int>();
            localSave.completedEntityPlacements ??= Array.Empty<int>();
            localSave.mapLogicStates ??= Array.Empty<Map10RuntimeMapLogicState>();
            localSave.receipts ??= Array.Empty<string>();
            localSave.nativeDialogAwardReceipts ??= Array.Empty<string>();
            localSave.battleResults ??= Array.Empty<Map10RuntimeBattleResult>();
        }

        private void InitializeMap()
        {
            var slots = new ProfileSlotCatalog(persistentDataPath);
            progressionSave = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            if (!progressionSave.Save.Player.CharacterCreated || progressionSave.Save.Player.InitialWeaponType == 0)
                throw new InvalidOperationException("Finish character creation and weapon selection before entering Map10.");
            OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            progression = new OfflineCharacterProgressionService(progressionSave, characterAssets.Load());
            session = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), 10);
            if (session.Definition.MaxRegionProgress == null || session.Definition.MaxRegionProgress.Length == 0)
                throw new InvalidDataException("Official Map10 region progress definitions are missing.");
            LoadRegionConditions();
            int[] regions = session.Definition.MaxRegionProgress.Select(value => value.Region).ToArray();
            ProgressionResult initialized = progressionSave.InitializeMap(10, regions);
            if (initialized.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(initialized.Message);
            RefreshRegionProgress();
        }

        private void InitializeView()
        {
            mapRoot = new GameObject("OfficialMap10World").transform;
            mapRoot.SetParent(transform, false);
            WorldMapAssets assets = session.Assets;
            GameObject terrain = Instantiate(assets.GroundPrefab, mapRoot, false);
            terrain.name = "Map10Terrain";

            CreateWorldCamera();

            var entityObject = new GameObject("Map10EntityRuntime");
            entityObject.transform.SetParent(mapRoot, false);
            entityRoot = entityObject.transform;
            entityView = entityObject.AddComponent<OfficialMap10EntityView>();
            TextAsset entityData = Resources.Load<TextAsset>(EntityDataResource);
            if (entityData == null) throw new InvalidOperationException("Official Map10 entity data is missing.");
            Vector2 mapOrigin = new Vector2(session.Definition.Navigation.Origin.X,
                session.Definition.Navigation.Origin.Y);
            entityView.Initialize(entityData, entityRoot, mapOrigin, worldCamera);
            entityView.EntitySelected += OnEntitySelected;
            mapEntities.Clear();
            foreach (OfficialMap10EntityInfo info in entityView.EntityInfos)
            {
                if (info == null || info.PlacementId <= 0 || info.Grid == null || info.Grid.Length != 2
                    || mapEntities.ContainsKey(info.PlacementId))
                    throw new InvalidDataException("Official Map10 entity placement is malformed or duplicated.");
                var entity = new Map10RuntimeEntity(info);
                if (mapLogicTags.TryGetValue(info.PlacementId, out Map10RuntimeLogicTag tag))
                {
                    if (tag.EntityClass != info.EntityClassId)
                        throw new InvalidDataException("Official Map10 entity tag class differs from its placement: " + info.PlacementId);
                    entity.TalkTag = tag.Tag;
                }
                mapEntities.Add(info.PlacementId, entity);
            }
            ValidateEntityLockGroups();

            uiCatalog = OfficialMap10UiCatalog.LoadDefault();
            ui = new OfficialMap10UiRenderer(uiCatalog);
            ui.Mount(uiParent, BindWorldControl);
            ui.SetPanelActive("WorldCanvas", true);
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", characterName);
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/LevelText",
                "Lv." + progression.Player.Level);
            ui.SetPanelActive("TalkDialog", false);
            CreateCommerceHudEntries();
            CreateMinimap(assets.MiniMapPrefab);

            avatar.transform.SetParent(mapRoot, false);
            avatar.name = "Map10Player_" + slotId;
            currentGrid = new MapGridPoint(localSave.gridX, localSave.gridY);
            SetAvatarPosition(currentGrid);
            progressionSave.Save.Player.UnlockedMapClassIds ??= new List<int>();
            if (!progressionSave.Save.Player.UnlockedMapClassIds.Contains(10))
            {
                OfflinePlayerProgress player = progressionSave.Save.Player;
                ProgressionResult unlocked = progressionSave.SetPlayerProgress(player.Level, player.Rank,
                    player.SubRank, player.MaxProfessionRank, player.GuildName ?? string.Empty,
                    player.UnlockedMapClassIds.Concat(new[] { 10 }));
                if (unlocked.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(unlocked.Message);
            }
            SaveLocalProgress();
            RefreshEntityActivity();
        }

        private void CreateWorldCamera()
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                var cameraObject = new GameObject("Map10WorldCamera");
                cameraObject.transform.SetParent(transform, false);
                worldCamera = cameraObject.AddComponent<Camera>();
                worldCamera.orthographic = true;
                worldCamera.orthographicSize = 7.5f;
                worldCamera.clearFlags = CameraClearFlags.SolidColor;
                worldCamera.backgroundColor = new Color(0.08f, 0.12f, 0.12f, 1f);
                worldCamera.transform.position = new Vector3(0f, 0f, -20f);
            }
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = 7.5f;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color(0.08f, 0.12f, 0.12f, 1f);
            worldCamera.nearClipPlane = 0.1f;
            worldCamera.farClipPlane = 100f;
            worldCamera.transform.position = avatar.transform.position + new Vector3(0f, 0f, -20f);
        }

        private void CreateMinimap(GameObject prefab)
        {
            if (prefab == null) throw new InvalidOperationException("Official Map10 minimap prefab is missing.");
            var viewRoot = new GameObject("Map10MinimapCameraRoot");
            viewRoot.transform.SetParent(transform, false);
            GameObject map = Instantiate(prefab, viewRoot.transform, false);
            map.name = "NativeMiniMap_10";
            map.SetActive(true);
            SetLayerRecursively(map, 30);
            minimapTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
            {
                name = "Map10OfficialMinimap",
                filterMode = FilterMode.Bilinear
            };
            minimapTexture.Create();
            var cameraObject = new GameObject("Map10MinimapCamera");
            cameraObject.transform.SetParent(viewRoot.transform, false);
            minimapCamera = cameraObject.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = 32f;
            minimapCamera.aspect = 1f;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            minimapCamera.cullingMask = 1 << 30;
            minimapCamera.targetTexture = minimapTexture;
            minimapCamera.transform.position = new Vector3(0f, 0f, -50f);
            var minimapObject = new GameObject("OfficialMap10MiniMap", typeof(RectTransform), typeof(RawImage));
            minimapObject.transform.SetParent(uiParent, false);
            minimapImage = minimapObject.GetComponent<RawImage>();
            minimapImage.texture = minimapTexture;
            minimapImage.raycastTarget = false;
            RectTransform rect = minimapObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-18f, -20f);
            rect.sizeDelta = new Vector2(156f, 156f);
        }

        private void BindWorldControl(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.IndexOf("/WorldStageDialog/", StringComparison.Ordinal) >= 0
                && path.EndsWith("/BattleBtn", StringComparison.Ordinal))
                button.onClick.AddListener(StartSelectedStage);
            else if (path.IndexOf("/WorldStageDialog/", StringComparison.Ordinal) >= 0
                && path.EndsWith("/CommonBtn_12", StringComparison.Ordinal))
                button.onClick.AddListener(CloseStageDialog);
            else if (path.IndexOf("MiniMap", StringComparison.OrdinalIgnoreCase) >= 0)
                button.onClick.AddListener(ToggleExpandedMinimap);
            else if (path.IndexOf("/FunctionEntryGroup/", StringComparison.Ordinal) >= 0
                && (path.EndsWith("/EntryBtnFolded", StringComparison.Ordinal)
                    || path.EndsWith("/EntryBtnExpanded", StringComparison.Ordinal)))
                button.onClick.AddListener(OpenCommerceShop);
            else if (path.IndexOf("BackBtn", StringComparison.OrdinalIgnoreCase) >= 0)
                button.onClick.AddListener(CloseMap);
        }

        private void OpenCommerceShop()
        {
            if (commerceScreen != null) return;
            try
            {
                commerceScreen = CommerceScreenController.OpenShop(slotId, null, CloseCommerceShop);
            }
            catch (Exception exception)
            {
                ShowOfficialNotice("商城无法打开：" + exception.Message);
            }
        }

        private void OpenCommerceFirstPay()
        {
            if (commerceScreen != null) return;
            try
            {
                commerceScreen = CommerceScreenController.OpenShop(slotId, null, CloseCommerceShop, true);
            }
            catch (Exception exception)
            {
                ShowOfficialNotice("首购无法打开：" + exception.Message);
            }
        }

        private void CloseCommerceShop()
        {
            commerceScreen = null;
            RefreshCommerceHud();
        }

        private void CreateCommerceHudEntries()
        {
            try
            {
                Transform container = ui.Find("WorldCanvas",
                    "/WorldCanvas/Content/Content/WorldPanel/Anim/Right/EventAndPurchaseEntrys")?.transform;
                if (container == null) return;
                firstPayEntry = CommerceScreenController.CreateOfficialElement("commerce_first_entry", container,
                    (path, node) =>
                    {
                        Button button = node.GetComponent<Button>();
                        if (button == null) return;
                        if (path.EndsWith("/AccumulatedPayEntryGo/BgImg", StringComparison.Ordinal))
                            button.onClick.AddListener(OpenCommerceFirstPay);
                    });
                firstPayEntry.name = "CommerceFirstPayEntry";
                RectTransform rect = firstPayEntry.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-12f, -12f);
                Transform title = firstPayEntry.transform.Find("TitleTxt");
                if (title != null && title.GetComponent<TMPro.TMP_Text>() is TMPro.TMP_Text text)
                    text.text = "首购";
                Transform redDot = firstPayEntry.transform.Find("RedDotGo");
                if (redDot != null) redDot.gameObject.SetActive(true);
                RefreshCommerceHud();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Commerce HUD entry unavailable: " + exception.Message);
            }
        }

        private void RefreshCommerceHud()
        {
            if (firstPayEntry != null)
                firstPayEntry.SetActive(CommerceHudStatus.FirstPayShouldShow(slotId));
        }

        private void ToggleExpandedMinimap()
        {
            if (minimapImage == null) return;
            RectTransform rect = minimapImage.rectTransform;
            bool expanded = rect.sizeDelta.x < 400f;
            rect.sizeDelta = expanded ? new Vector2(560f, 560f) : new Vector2(156f, 156f);
            rect.anchoredPosition = expanded ? Vector2.zero : new Vector2(-18f, -20f);
            rect.anchorMin = rect.anchorMax = expanded ? new Vector2(0.5f, 0.5f) : Vector2.one;
            rect.pivot = expanded ? new Vector2(0.5f, 0.5f) : Vector2.one;
        }

        private void TryMoveToPointer()
        {
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(worldCamera.transform.position.z);
            Vector3 world = worldCamera.ScreenToWorldPoint(screen);
            OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                new OfficialMapVector3 { X = world.x, Y = world.y, Z = 0f }, out int x, out int y);
            StartMovement(new MapGridPoint(x, y), null);
        }

        private void OnEntitySelected(OfficialMap10EntityInfo entity)
        {
            if (entity == null || entity.Grid == null || entity.Grid.Length != 2 || battleController != null)
                return;
            if (!mapEntities.TryGetValue(entity.PlacementId, out Map10RuntimeEntity record)) return;
            StartMovement(record.Grid, record);
        }

        private void StartMovement(MapGridPoint target, Map10RuntimeEntity interact)
        {
            if (movementPath != null || battleController != null) return;
            List<MapGridPoint> path = session.FindPath(new OfficialMapPathRequest
            {
                Start = currentGrid,
                End = target,
                PreciseTarget = interact == null,
                PreferDistance = interact == null ? 0 : 1,
                CanEnd = interact == null ? null : point => Manhattan(point, target) <= 1
            });
            if (path == null)
            {
                movementCompletion = null;
                return;
            }
            if (path.Count == 0)
            {
                if (interact != null && Manhattan(currentGrid, target) <= 1) Interact(interact);
                if (currentGrid.Equals(target))
                {
                    Action complete = movementCompletion;
                    movementCompletion = null;
                    complete?.Invoke();
                }
                return;
            }
            pendingInteraction = interact;
            movementPath = path;
            movementIndex = path[0].Equals(currentGrid) ? 1 : 0;
            if (movementIndex >= movementPath.Count) FinishMovement();
        }

        private void AdvanceMovement(float deltaTime)
        {
            if (movementPath == null || movementIndex >= movementPath.Count)
            {
                FinishMovement();
                return;
            }
            MapGridPoint point = movementPath[movementIndex];
            Vector3 target = GridToWorld(point);
            avatar.transform.position = Vector3.MoveTowards(avatar.transform.position, target,
                MovementSpeed * deltaTime);
            if (Vector2.Distance(avatar.transform.position, target) < 0.025f)
            {
                currentGrid = point;
                movementIndex++;
                SaveLocalProgress();
                if (movementIndex >= movementPath.Count) FinishMovement();
            }
        }

        private void FinishMovement()
        {
            if (movementPath != null && movementPath.Count > 0)
                currentGrid = movementPath[movementPath.Count - 1];
            movementPath = null;
            movementIndex = 0;
            SaveLocalProgress();
            Map10RuntimeEntity selected = pendingInteraction;
            pendingInteraction = null;
            if (selected != null) Interact(selected);
            Action complete = movementCompletion;
            movementCompletion = null;
            complete?.Invoke();
        }

        private void Interact(Map10RuntimeEntity entity)
        {
            if (localSave.completedEntityPlacements.Contains(entity.PlacementId)) return;
            if (IsEntityLocked(entity))
            {
                ShowOfficialNotice("前方有魔物阻挡");
                return;
            }
            if (entity.PlacementId == 216 && !HasStory("FirstMeetChaos"))
            {
                if (chaosNpc != null) entityView.RemoveStoryEntity(chaosNpc);
                MapGridPoint npcGrid = FindAdjacentStoryGrid();
                chaosNpc = entityView.SpawnStoryEntity(8402,
                    "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view",
                    new Vector2(npcGrid.X, npcGrid.Y));
                PlayStory("FirstMeetChaos", 0, () => ShowStageDialog(entity, () =>
                {
                    entityView.RemoveStoryEntity(chaosNpc);
                    chaosNpc = null;
                    CompleteStory("FirstMeetChaos");
                    SaveLocalProgress();
                }));
                return;
            }
            if (entity.TypeId == 503)
            {
                CompleteEntity(entity);
                ShowOfficialNotice("已探索");
                return;
            }
            if (entity.AwardItems.Length > 0)
            {
                CollectEntityAwards(entity);
                return;
            }
            int scenario = entity.TypeId == 4 ? 1 : entity.TypeId == 103 ? 6 : 0;
            Map10RuntimeMapTalkEntry talk = scenario > 0 ? ResolveMapTalk(entity, scenario) : null;
            if (talk != null && !string.IsNullOrEmpty(talk.TalkId))
            {
                StartStoryGraph(talk.TalkId, entity, () =>
                {
                    if (entity.StageIds.Any(value => value > 0)) ShowStageDialog(entity, null);
                });
                return;
            }
            if (talk != null && talk.DialogId > 0)
            {
                PlayMapTalkDialog(talk, entity);
                return;
            }
            int stageId = entity.StageIds.FirstOrDefault(value => value > 0);
            if (stageId <= 0)
            {
                ShowOfficialNotice("该实体暂无可执行的本地交互");
                return;
            }
            if (stageId == 1001 && !HasStory("FirstBattle"))
            {
                if (!localSave.firstBattleIntroShown)
                {
                    StartFirstBattleIntroduction();
                    return;
                }
                ShowStageDialog(entity, () => PlayStory("FirstBattle", 2, () =>
                    {
                        CompleteStory("FirstBattle");
                        SaveLocalProgress();
                    }));
                return;
            }
            ShowStageDialog(entity, null);
        }

        private Map10RuntimeMapTalkEntry ResolveMapTalk(Map10RuntimeEntity entity, int scenario)
        {
            string entityId = "entity_" + entity.EntityClassId + "_name";
            bool stopTagFallback = false;
            Map10RuntimeMapTalkEntry match = FindMapTalk(entityId, scenario, entity.TalkTag, ref stopTagFallback);
            if (match != null || stopTagFallback || string.IsNullOrEmpty(entity.TalkTag)) return match;
            return FindMapTalk(entityId, scenario, string.Empty, ref stopTagFallback);
        }

        private Map10RuntimeMapTalkEntry FindMapTalk(string entityId, int scenario, string tag,
            ref bool stopTagFallback)
        {
            foreach (Map10RuntimeMapTalkEntry entry in mapTalkEntries)
            {
                if (entry == null || entry.EntityId != entityId || entry.Scenario != scenario
                    || (entry.Tag ?? string.Empty) != (tag ?? string.Empty))
                    continue;
                if (!IsMapTalkConditionMet(entry.Condition)) continue;
                bool hasStory = !string.IsNullOrEmpty(entry.TalkId);
                if (!hasStory && entry.DialogId <= 0) continue;
                if (hasStory && stories.TryGetValue(entry.TalkId, out Map10RuntimeStory story)
                    && story.maxCount > 0 && GetStoryPlayCount(entry.TalkId) >= story.maxCount)
                {
                    if (!string.IsNullOrEmpty(entry.Tag) && entry.FouceStopOnTagEqual)
                    {
                        stopTagFallback = true;
                        return null;
                    }
                    continue;
                }
                return entry;
            }
            return null;
        }

        private bool IsMapTalkConditionMet(Map10RuntimeTalkCondition condition)
        {
            if (condition == null || condition._type == "NoneCondition") return true;
            if (condition._type == "CompositeAndCondition")
                return condition.Ids != null && condition.Ids.Length > 0
                    && condition.Ids.All(id => mapTalkConditions.TryGetValue(id, out Map10RuntimeTalkCondition part)
                        && IsMapTalkConditionMet(part));
            if (condition._type == "PlayerProfessionCondition")
                return condition.Profession == 0;
            if (condition._type == "InitialWeaponCondition")
                return progressionSave != null && progressionSave.Save.Player.InitialWeaponType == condition.WeaponType;
            if (condition._type == "MaxProfessionRankCondition")
                return progressionSave != null && Compare(progressionSave.Save.Player.MaxProfessionRank,
                    condition.Op, condition.Rank);
            return false;
        }

        private void StartStoryGraph(string key, Map10RuntimeEntity context, Action completed)
        {
            if (storyRunning) throw new InvalidOperationException("A Map10 story is already running.");
            if (!stories.TryGetValue(key, out activeStory))
                throw new InvalidDataException("Official Map10 story is missing: " + key);
            storyRunning = true;
            storyContextEntity = context;
            storyFinished = completed;
            storyEntities.Clear();
            storyEntityClasses.Clear();
            if (key == "SavingChick" && context != null && context.EntityClassId == 8417)
            {
                storyHiddenContextEntity = mapEntityObject(context.PlacementId);
                if (storyHiddenContextEntity != null) storyHiddenContextEntity.SetActive(false);
            }
            RunStoryGraph(activeStory.entryNodeId);
        }

        private void RunStoryGraph(int nodeId)
        {
            try
            {
                for (int count = 0; count < 256 && storyRunning; count++)
                {
                    Map10RuntimeStoryNode node = FindStoryNode(activeStory, nodeId);
                    if (node == null) throw new InvalidDataException("Official Map10 story node is missing: " + nodeId);
                    switch (node.type)
                    {
                        case "StartStoryNode":
                        case "WaitStartStoryNode":
                        case "WaitAllNoTipWindowClosedNode":
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "FinishStoryNode":
                            CompleteStory(activeStory.key);
                            IncrementStoryPlayCount(activeStory.key);
                            SaveLocalProgress();
                            FinishStoryGraph();
                            return;
                        case "DialogNode":
                            PlayStory(activeStory.key, node.dialogId, () => RunStoryGraph(StoryNext(node, "Exit")));
                            return;
                        case "OptionDialogNode":
                            PlayStory(activeStory.key, node.dialogId, () =>
                                ShowStoryOptions(activeStory.key, node, index =>
                                    RunStoryGraph(StoryNext(node, "Options " + index)
                                        is int target && target > 0 ? target : StoryNext(node, "Exit"))));
                            return;
                        case "IfNode":
                            nodeId = StoryNext(node, EvaluateStoryIf(activeStory, node) ? "True" : "False");
                            continue;
                        case "WaitForSecondsNode":
                            StartCoroutine(ContinueStoryAfterDelay(node.seconds, StoryNext(node, "Exit")));
                            return;
                        case "AddMapExclusiveNpcAtPosNode":
                            AddStoryEntity(node, new Vector2(node.positionX, node.positionY));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "AddMapExclusiveNpcAroundPlayerNode":
                            MapGridPoint nearPlayer = FindAdjacentStoryGrid();
                            AddStoryEntity(node, new Vector2(nearPlayer.X, nearPlayer.Y));
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "RemoveMapExclusiveNpcNode":
                            RemoveStoryEntities(node.removeEntityClassIds);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "MapEntityMoveNode":
                            int entityNodeId = StoryNext(node, "OperateEntity");
                            if (!storyEntities.TryGetValue(entityNodeId, out GameObject movingEntity))
                                throw new InvalidDataException("Official Map10 story move has no spawned entity: " + entityNodeId);
                            StartCoroutine(MoveStoryEntity(movingEntity, node.destinationX, node.destinationY,
                                Mathf.Max(0.1f, node.moveSpeed), StoryNext(node, "Exit")));
                            return;
                        case "EnterStoryWorldCameraNode":
                            if (node.needHideUi) SetStoryWorldUiHidden(true);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "ExitStoryWorldCameraNode":
                            storyCameraActive = false;
                            if (node.needShowUi) SetStoryWorldUiHidden(false);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "HideAllUINode":
                            SetStoryWorldUiHidden(true);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "ShowAllUINode":
                            SetStoryWorldUiHidden(false);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "WorldCameraNode":
                        case "CameraMoveAndUnlockAreaNode":
                            Vector2 cameraPoint = ResolveStoryCameraPoint(activeStory, node);
                            storyCameraTarget = StoryWorldPoint(cameraPoint.x, cameraPoint.y) + new Vector3(0f, 0f, -20f);
                            storyCameraSizeTarget = Mathf.Clamp(7.5f + node.viewSizeOffset, 4f, 30f);
                            storyCameraActive = true;
                            StartCoroutine(ContinueStoryAfterDelay(node.duration, StoryNext(node, "Exit")));
                            return;
                        case "SetMapLogicEntitySetStateNode":
                            ApplyStoryMapLogicStates(node.mapLogicStates);
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        case "PlayPlayableNode":
                            if (string.IsNullOrWhiteSpace(node.playableAssetGuid)
                                || !officialPlayables.ContainsKey(node.playableAssetGuid))
                                throw new InvalidDataException("Official Map10 story playable is not loaded: " + node.playableAssetGuid);
                            if (node.playableAssetGuid == "9b248cde91299ee4287533b25b7dd468")
                            {
                                GameObject npc = storyEntities.OrderBy(value => value.Key).Select(value => value.Value)
                                    .LastOrDefault(value => value != null);
                                if (npc != null) entityView.PlayStoryEntityAnimation(npc, "disappear_01");
                            }
                            if (node.waitPlayableStop)
                            {
                                StartCoroutine(ContinueStoryAfterDelay(node.playableDuration,
                                    StoryNext(node, "Exit")));
                                return;
                            }
                            nodeId = StoryNext(node, "Exit");
                            continue;
                        default:
                            throw new InvalidDataException("Unsupported official Map10 story node: " + node.type);
                    }
                }
                if (storyRunning) throw new InvalidDataException("Official Map10 story exceeded its node limit: " + activeStory.key);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                AbortStoryGraph();
                ShowOfficialNotice("剧情暂时无法继续");
            }
        }

        private IEnumerator ContinueStoryAfterDelay(float seconds, int nextNodeId)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private IEnumerator MoveStoryEntity(GameObject entity, int destinationX, int destinationY,
            float speed, int nextNodeId)
        {
            if (entity == null || session == null)
            {
                AbortStoryGraph();
                yield break;
            }
            OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                new OfficialMapVector3 { X = entity.transform.position.x, Y = entity.transform.position.y },
                out int startX, out int startY);
            List<MapGridPoint> path = session.FindPath(new OfficialMapPathRequest
            {
                Start = new MapGridPoint(startX, startY),
                End = new MapGridPoint(destinationX, destinationY),
                PreciseTarget = true
            });
            if (path == null || path.Count == 0)
            {
                AbortStoryGraph();
                ShowOfficialNotice("剧情移动路线不可用");
                yield break;
            }
            foreach (MapGridPoint point in path)
            {
                if (entity == null || !storyRunning) yield break;
                Vector3 target = GridToWorld(point);
                while (Vector2.Distance(entity.transform.position, target) > 0.025f)
                {
                    entity.transform.position = Vector3.MoveTowards(entity.transform.position, target,
                        speed * Time.deltaTime);
                    yield return null;
                }
            }
            if (storyRunning) RunStoryGraph(nextNodeId);
        }

        private void AddStoryEntity(Map10RuntimeStoryNode node, Vector2 grid)
        {
            string modelPath = node.entityClassId == 8402
                ? "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view"
                : mapEntities.Values.FirstOrDefault(value => value.EntityClassId == node.entityClassId)?.ModelPath;
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new InvalidDataException("Official Map10 story NPC model is missing: " + node.entityClassId);
            GameObject entity = entityView.SpawnStoryEntity(node.entityClassId, modelPath, grid);
            storyEntities[node.id] = entity;
            storyEntityClasses[node.id] = node.entityClassId;
        }

        private void RemoveStoryEntities(int[] classIds)
        {
            if (classIds == null || classIds.Length == 0) return;
            foreach (int nodeId in storyEntityClasses.Where(value => classIds.Contains(value.Value))
                         .Select(value => value.Key).ToArray())
            {
                if (storyEntities.TryGetValue(nodeId, out GameObject entity)) entityView.RemoveStoryEntity(entity);
                storyEntities.Remove(nodeId);
                storyEntityClasses.Remove(nodeId);
            }
        }

        private Vector2 ResolveStoryCameraPoint(Map10RuntimeStory story, Map10RuntimeStoryNode cameraNode)
        {
            Map10RuntimeStoryPort input = cameraNode.ports?.FirstOrDefault(value => value.name == "MoveToTargetPos");
            Map10RuntimeStoryConnection link = input?.connections?.FirstOrDefault();
            Map10RuntimeStoryNode targetNode = link == null ? null : FindStoryNode(story, link.nodeId);
            if (targetNode == null)
                return new Vector2(cameraNode.moveTargetX, cameraNode.moveTargetY);
            if (targetNode.type == "GetPosAroundStoryObjectNode")
            {
                Map10RuntimeEntity registered = targetNode.registerKey == "Fangjianbei"
                    ? mapEntities.Values.FirstOrDefault(value => value.EntityClassId == 6001)
                    : null;
                if (registered == null)
                    throw new InvalidDataException("Official Map10 story register key is not bound: " + targetNode.registerKey);
                return new Vector2(registered.Grid.X + targetNode.offsetX,
                    registered.Grid.Y + targetNode.offsetY);
            }
            if (targetNode.type != "GetEntityPosNode")
                return new Vector2(cameraNode.moveTargetX, cameraNode.moveTargetY);
            if (targetNode.selectGetType == 6)
            {
                GameObject storyNpc = storyEntities.OrderBy(value => value.Key).Select(value => value.Value)
                    .LastOrDefault(value => value != null);
                if (storyNpc != null) return StoryGridPosition(storyNpc.transform.position);
                if (storyContextEntity != null)
                    return new Vector2(storyContextEntity.Grid.X, storyContextEntity.Grid.Y);
            }
            else if (targetNode.selectGetType == 0)
            {
                if (mapEntities.TryGetValue(targetNode.loEntityId, out Map10RuntimeEntity targetEntity))
                    return new Vector2(targetEntity.Grid.X, targetEntity.Grid.Y);
                Map10RuntimeLogicTag tag = mapLogicTags.Values.FirstOrDefault(value => value.Tag == targetNode.tag);
                string target = tag == null ? "logic entity " + targetNode.loEntityId : "Map" + tag.MapClass + " " + targetNode.tag;
                throw new InvalidDataException("Official story camera target is outside the loaded map: " + target);
            }
            throw new InvalidDataException("Official Map10 story camera target cannot be resolved: " + targetNode.tag);
        }

        private Vector3 StoryWorldPoint(float x, float y)
        {
            OfficialMapVector3 origin = session.Definition.Navigation.Origin;
            return new Vector3(origin.X + x, origin.Y + y, origin.Z - y * 0.001f - 0.1f);
        }

        private Vector2 StoryGridPosition(Vector3 world)
        {
            OfficialMapNavigationView.WorldToGrid(session.Definition.Navigation,
                new OfficialMapVector3 { X = world.x, Y = world.y }, out int x, out int y);
            return new Vector2(x, y);
        }

        private void SetStoryWorldUiHidden(bool hidden)
        {
            if (storyWorldUiHidden == hidden) return;
            storyWorldUiHidden = hidden;
            if (ui != null) ui.SetPanelActive("WorldCanvas", !hidden);
            if (minimapImage != null) minimapImage.gameObject.SetActive(!hidden);
        }

        private static Map10RuntimeStoryNode FindStoryNode(Map10RuntimeStory story, int nodeId) =>
            story?.nodes?.FirstOrDefault(value => value != null && value.id == nodeId);

        private static int StoryNext(Map10RuntimeStoryNode node, string portName)
        {
            Map10RuntimeStoryConnection connection = node?.ports?
                .FirstOrDefault(value => value.name == portName)?.connections?.FirstOrDefault();
            return connection == null ? 0 : connection.nodeId;
        }

        private static int StoryOptionId(Map10RuntimeStoryNode node, int index) =>
            node.options != null && index >= 0 && index < node.options.Length ? node.options[index] : 0;

        private bool EvaluateStoryIf(Map10RuntimeStory story, Map10RuntimeStoryNode node)
        {
            int compareId = StoryNext(node, "Input");
            Map10RuntimeStoryNode compare = FindStoryNode(story, compareId);
            int queryId = StoryNext(compare, "Origin");
            Map10RuntimeStoryNode query = FindStoryNode(story, queryId);
            if (compare == null || query == null || query.type != "QueryStoryCountNode")
                throw new InvalidDataException("Official Map10 story count condition is malformed: " + story.key);
            int current = GetStoryPlayCount(query.storyName);
            int target = compare.compareTargetInt;
            switch (compare.compareOption)
            {
                case 0: return current == target;
                case 1: return current > target;
                case 2: return current < target;
                case 3: return current >= target;
                case 4: return current <= target;
                case 5: return current != target;
                default: throw new InvalidDataException("Unsupported official story comparison option: " + compare.compareOption);
            }
        }

        private void ShowStoryOptions(string storyName, Map10RuntimeStoryNode node, Action<int> selected)
        {
            if (node.options == null || node.options.Length == 0 || node.options.Length > 5)
                throw new InvalidDataException("Official Map10 story option count is invalid: " + storyName);
            storyOptionsWaiting = true;
            storyOptionCount = node.options.Length;
            storyOptionSelected = selected;
            ui.SetPanelActive("OptionTalkDialog", true);
            for (int index = 0; index < 5; index++)
            {
                string root = StoryOptionSlotPath(index);
                bool active = index < storyOptionCount;
                ui.SetNodeActive("OptionTalkDialog", root, active);
                if (!active) continue;
                int optionId = StoryOptionId(node, index);
                if (!storyOptions.TryGetValue(StoryOptionKey(storyName, optionId), out Map10RuntimeStoryOption option))
                    throw new InvalidDataException("Official Map10 story option is missing: " + storyName + "/" + optionId);
                ui.SetText("OptionTalkDialog", root + "/Content/OptionText/Text", option.Text);
                Sprite icon = uiCatalog.FindSprite(option.Icon);
                if (icon != null) ui.SetSprite("OptionTalkDialog", root + "/Content/TypeImg", icon);
                GameObject item = ui.Find("OptionTalkDialog", root);
                Button button = item == null ? null : item.GetComponent<Button>();
                if (button == null) throw new InvalidDataException("Official Map10 option button is missing: " + root);
                int capturedIndex = index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectStoryOption(capturedIndex));
            }
        }

        private void SelectStoryOption(int index)
        {
            if (!storyOptionsWaiting || index < 0 || index >= storyOptionCount) return;
            storyOptionsWaiting = false;
            ui.SetPanelActive("OptionTalkDialog", false);
            for (int i = 0; i < 5; i++) ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            Action<int> selected = storyOptionSelected;
            storyOptionSelected = null;
            selected?.Invoke(index);
        }

        private static string StoryOptionSlotPath(int index)
        {
            const string root = "/OptionTalkDialog/OptionSlots/Content/OptionItem ";
            return index == 0 ? root : root + " (" + index + ")";
        }

        private GameObject mapEntityObject(int placementId)
        {
            GameObject entity = entityView == null ? null : entityView.FindEntityObject(placementId);
            if (entity != null) entity.SetActive(false);
            return entity;
        }

        private void FinishStoryGraph()
        {
            FinishStoryGraph(true);
        }

        private void FinishStoryGraph(bool invokeCompletion)
        {
            Action completed = invokeCompletion ? storyFinished : null;
            SetStoryWorldUiHidden(false);
            storyCameraActive = false;
            if (worldCamera != null) worldCamera.orthographicSize = 7.5f;
            cameraVelocity = Vector3.zero;
            storyCameraVelocity = Vector3.zero;
            if (storyHiddenContextEntity != null) storyHiddenContextEntity.SetActive(true);
            storyHiddenContextEntity = null;
            foreach (GameObject entity in storyEntities.Values) entityView.RemoveStoryEntity(entity);
            storyEntities.Clear();
            storyEntityClasses.Clear();
            ui.SetPanelActive("TalkDialog", false);
            ui.SetPanelActive("OptionTalkDialog", false);
            dialoguePortraits?.Clear();
            optionDialoguePortraits?.Clear();
            activeStory = null;
            storyContextEntity = null;
            storyFinished = null;
            storyRunning = false;
            storyOptionsWaiting = false;
            storyOptionSelected = null;
            completed?.Invoke();
        }

        private void AbortStoryGraph()
        {
            FinishStoryGraph(false);
            if (nativeDialogActive) FinishNativeDialog();
        }

        private void StartFirstBattleIntroduction()
        {
            if (HasStory("FirstBattle") || localSave.firstBattleIntroShown || dialogueWaiting
                || movementPath != null || battleController != null)
                return;
            if (firstBattleNpc == null)
                firstBattleNpc = entityView.SpawnStoryEntity(8402,
                    "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view", new Vector2(23, 23));
            PlayStory("FirstBattle", 0, () =>
            {
                movementCompletion = () => PlayStory("FirstBattle", 1, () =>
                {
                    localSave.firstBattleIntroShown = true;
                    SaveLocalProgress();
                });
                StartMovement(new MapGridPoint(23, 22), null);
            });
        }

        private MapGridPoint FindAdjacentStoryGrid()
        {
            MapGridPoint[] candidates =
            {
                new MapGridPoint(currentGrid.X, currentGrid.Y + 1),
                new MapGridPoint(currentGrid.X + 1, currentGrid.Y),
                new MapGridPoint(currentGrid.X, currentGrid.Y - 1),
                new MapGridPoint(currentGrid.X - 1, currentGrid.Y)
            };
            foreach (MapGridPoint candidate in candidates)
            {
                if (Manhattan(currentGrid, candidate) != 1) continue;
                List<MapGridPoint> path = session.FindPath(new OfficialMapPathRequest
                {
                    Start = currentGrid,
                    End = candidate,
                    PreciseTarget = true
                });
                if (path != null && path.Count > 0) return candidate;
            }
            throw new InvalidOperationException("Map10 has no free adjacent cell for the official story NPC.");
        }

        private void ShowStageDialog(Map10RuntimeEntity entity, Action completed)
        {
            int stageId = entity.StageIds.FirstOrDefault(value => value > 0);
            if (stageId <= 0 || !stages.TryGetValue(stageId, out Map10RuntimeStage stage)
                || !rosters.TryGetValue(stageId, out Map10RuntimeRoster roster)
                || roster.Waves == null || roster.Waves.All(wave => wave == null || wave.Units == null || wave.Units.Length == 0))
            {
                ShowOfficialNotice("战斗配置缺失：" + stageId);
                return;
            }

            selectedStageEntity = entity;
            selectedStageCompletion = completed;
            string root = "/WorldStageDialog/BgDialog3/Content/DialogContent (1)";
            ui.SetText("WorldStageDialog", root + "/TitleGo/TitleGo/TitleTxt", "Stage " + stage.Id);
            ui.SetText("WorldStageDialog", root + "/DescGo/DescText", string.Empty);
            ui.SetText("WorldStageDialog", root + "/UnlockGo/BtnContent/BattleBtn/Content/Text", "开始战斗");
            ui.SetNodeActive("WorldStageDialog", root + "/LockGo", false);
            ui.SetNodeActive("WorldStageDialog", root + "/UnlockGo", true);
            ui.SetNodeActive("WorldStageDialog", root + "/UnlockGo/BtnContent/BattleBtn", true);
            ui.SetNodeActive("WorldStageDialog", root + "/BottomContent/CommonBtn_12", true);
            ui.SetPanelActive("WorldStageDialog", true);
        }

        private void StartSelectedStage()
        {
            Map10RuntimeEntity entity = selectedStageEntity;
            if (entity == null) return;
            Action completed = selectedStageCompletion;
            selectedStageEntity = null;
            selectedStageCompletion = null;
            ui.SetPanelActive("WorldStageDialog", false);
            StartEntityStage(entity, completed);
        }

        private void CloseStageDialog()
        {
            selectedStageEntity = null;
            selectedStageCompletion = null;
            ui.SetPanelActive("WorldStageDialog", false);
        }

        private void StartEntityStage(Map10RuntimeEntity entity, Action completed)
        {
            int stageId = entity.StageIds.FirstOrDefault(value => value > 0);
            if (stageId <= 0 || !stages.TryGetValue(stageId, out Map10RuntimeStage stage)
                || !rosters.TryGetValue(stageId, out Map10RuntimeRoster roster))
            {
                ShowOfficialNotice("战斗配置缺失：" + stageId);
                return;
            }
            if (stageId == 9910)
            {
                CompleteEntity(entity);
                completed?.Invoke();
                return;
            }
            if (roster.Waves == null || roster.Waves.All(wave => wave == null || wave.Units == null || wave.Units.Length == 0))
            {
                CompleteEntity(entity);
                completed?.Invoke();
                return;
            }
            if (!battleScenes.TryGetValue(stage.GameId, out Map10RuntimeBattleScene scene))
                throw new InvalidDataException("Official Map10 battle scene is missing for GameId " + stage.GameId);
            if (stage.Repeat >= 0 && localSave.completedStages.Contains(stageId))
            {
                CompleteEntity(entity);
                completed?.Invoke();
                return;
            }
            BeginBattle(entity, stage, roster, scene, completed);
        }

        private void BeginBattle(Map10RuntimeEntity entity, Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene, Action completed)
        {
            FightBattleCheckpoint checkpoint = BuildCheckpoint(entity, stage, roster, scene);
            Map10RuntimeFactionInfo playerFaction = scene.FightComponent.FactionInfoList.First(value => value.Faction == 0);
            Map10RuntimePoint playerEntry = playerFaction.EntryPosList[0];
            var entryPoint = new FightGridPoint(playerEntry.X, playerEntry.Y);
            var grid = checkpoint.Grid;
            Vector3 battlePlayerPoint = new Vector3(grid.OriginX + entryPoint.X,
                grid.OriginY + entryPoint.Y, grid.OriginZ);
            var anchorObject = new GameObject("Map10BattleAnchor_" + stage.Id);
            anchorObject.transform.SetParent(mapRoot, false);
            anchorObject.transform.position = avatar.transform.position - battlePlayerPoint;
            battleRoot = anchorObject.transform;
            entityRoot.gameObject.SetActive(false);

            battleController = new GameObject("Map10OfficialBattle").AddComponent<OfficialBattleRuntimeController>();
            battleController.transform.SetParent(transform, false);

            battleController.HudControlClicked += OnBattleHudControl;

            OfficialBattleGridMapper mapper = new OfficialBattleGridMapper(battleRoot, grid);
            Sprite fallbackPortrait = uiCatalog.FindSprite("res://assets/official/sprites/C_none-fa3e084e40.png");
            if (fallbackPortrait == null) throw new InvalidOperationException("Official Map10 fallback battle portrait is missing.");
            battleController.Begin(checkpoint, battleRoot, uiParent, mapper,
                _ => fallbackPortrait,
                _ => fallbackPortrait,
                unit => unit.Id == 1 ? null : OfficialBattleModelCatalog.LoadDefault().ResolveModelKey(unit),
                (_, __) => "idle_01",
                (root, unit) =>
                {
                    Vector3 scale = root.localScale;
                    scale.x = Mathf.Abs(scale.x) * (unit.Direction == 1 ? -1f : 1f);
                    root.localScale = scale;
                });
            battleController.AttachUnitTransform(1, avatar.transform);
            battleController.ConfigureSpeeds(
                OfficialCombatAssetCatalog.LoadDefault().Load().SpeedSettings, 0,
                IsBattleSpeedConditionMet);
            battleStepTimer = 0.8f;
            pendingInteraction = entity;
            battleCompletion = completed;
        }

        private Action battleCompletion;

        private FightBattleCheckpoint BuildCheckpoint(Map10RuntimeEntity entity, Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene)
        {
            if (scene.FightGridGroundComponent == null || scene.FightGridGroundComponent.Size == null
                || scene.FightGridGroundComponent.PosInfos == null)
                throw new InvalidDataException("Official Map10 battle grid is malformed for GameId " + scene.GameId);
            var grid = new FightBattleGrid
            {
                Width = scene.FightGridGroundComponent.Size.X,
                Height = scene.FightGridGroundComponent.Size.Y,
                OriginX = scene.FightGridGroundComponent.OriginPos.X,
                OriginY = scene.FightGridGroundComponent.OriginPos.Y,
                OriginZ = scene.FightGridGroundComponent.OriginPos.Z,
                DefaultGroundType = 2
            };
            int moveMask = 0;
            foreach (Map10RuntimeBattleCell cell in scene.FightGridGroundComponent.PosInfos)
            {
                grid.Cells.Add(new FightBattleGridCell
                {
                    X = cell.Pos.X, Y = cell.Pos.Y, GroundType = cell.GroundType, Height = cell.Height
                });
                if (cell.Height >= 999) grid.BlockedPositions.Add(new FightGridPoint(cell.Pos.X, cell.Pos.Y));
                else if (cell.GroundType >= 0 && cell.GroundType < 31) moveMask |= 1 << cell.GroundType;
            }
            if (moveMask == 0) throw new InvalidDataException("Official Map10 battle grid has no walkable ground.");

            var checkpoint = new FightBattleCheckpoint
            {
                StageId = stage.Id,
                MaxRound = stage.MaxRound > 0 ? stage.MaxRound : roster.MaxRound,
                RandomSeed = StableSeed(slotId, stage.Id),
                Grid = grid
            };
            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combatCatalog = combatAssets.Load();
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            var formula = combatAssets.LoadDamageFormula();
            var playerProperties = new Dictionary<int, long>(progression.BaseBattleProperties());
            foreach (KeyValuePair<int, long> property in progression.AppearanceProperties(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            {
                playerProperties.TryGetValue(property.Key, out long current);
                playerProperties[property.Key] = checked(current + property.Value);
            }
            int playerSubRank = progression.Player.SubRank;
            ApplyPlayerPassiveSkills(combatCatalog, skillItems, playerProperties, playerSubRank);
            Map10RuntimeFactionInfo faction = scene.FightComponent.FactionInfoList.First(value => value.Faction == 0);
            if (faction.EntryPosList == null || faction.EntryPosList.Length == 0)
                throw new InvalidDataException("Official Map10 battle scene has no player entry position.");
            Map10RuntimePoint playerEntry = faction.EntryPosList[0];
            var player = new FightBattleUnitState
            {
                Id = 1,
                Side = 1,
                CharacterType = 1,
                Level = progression.Player.Level,
                SubRank = playerSubRank,
                Profession = progression.Player.Wardrobe.Profession,
                X = playerEntry.X,
                Y = playerEntry.Y,
                Hp = Property(playerProperties, 3),
                MaxHp = Property(playerProperties, 3),
                LastMaxHp = Property(playerProperties, 3),
                MoveMask = moveMask,
                Properties = ToProperties(playerProperties)
            };
            if (player.MaxHp <= 0) throw new InvalidDataException("Official character battle MaxHp is not positive.");
            player.MoveDist = formula.CalculateMoveDist(playerProperties);
            player.Turn.Id = player.Id;
            player.Turn.Hp = player.Hp;
            player.Turn.Speed = CheckedProperty(playerProperties, 4);
            checkpoint.Units.Add(player);
            AssemblePlayerSkills(combatCatalog, skillItems, player);

            int nextUnitId = 2;
            foreach (Map10RuntimeWave sourceWave in roster.Waves.OrderBy(value => value.Index))
            {
                var wave = new FightBattleWaveState { Index = sourceWave.Index, Spawned = sourceWave.Index == 0 };
                foreach (Map10RuntimeMonster source in sourceWave.Units)
                {
                    Dictionary<int, long> properties = source.Properties.ToDictionary(value => value.Id, value => value.Value);
                    int monsterLevel = Mathf.RoundToInt(source.Level);
                    ApplyMonsterPassiveSkills(combatCatalog, skillItems, properties, source,
                        monsterLevel, source.SubRank);
                    long maxHp = Property(properties, 3);
                    if (maxHp <= 0) throw new InvalidDataException("Official Map10 monster MaxHp is not positive: " + source.MonsterId);
                    var unit = new FightBattleUnitState
                    {
                        Id = nextUnitId++,
                        Side = 2,
                        CharacterType = 2,
                        MonsterId = source.MonsterId,
                        MonsterType = source.MonsterType,
                        Level = Mathf.RoundToInt(source.Level),
                        SubRank = source.SubRank,
                        Wave = sourceWave.Index,
                        X = source.Position.X,
                        Y = source.Position.Y,
                        Hp = maxHp,
                        MaxHp = maxHp,
                        LastMaxHp = maxHp,
                        MoveMask = moveMask,
                        Properties = ToProperties(properties)
                    };
                    unit.MoveDist = formula.CalculateMoveDist(properties);
                    unit.Turn.Id = unit.Id;
                    unit.Turn.Hp = unit.Hp;
                    unit.Turn.Speed = CheckedProperty(properties, 4);
                    checkpoint.Units.Add(unit);
                    wave.UnitIds.Add(unit.Id);
                    AssembleMonsterSkills(combatCatalog, unit, source);
                }
                checkpoint.Waves.Add(wave);
            }
            return checkpoint;
        }

        private void ApplyPlayerPassiveSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties, int subRank)
        {
            OfflineSkillProfile profile = progression.Player.Skills;
            int[] passives = OfflineSkillProgression.EquippedPassiveSkills(profile);
            for (int slot = 0; slot < passives.Length; slot++)
            {
                int itemId = passives[slot];
                if (itemId <= 0) continue;
                int level = profile.PassiveSlotLevels != null && slot < profile.PassiveSlotLevels.Length
                    ? profile.PassiveSlotLevels[slot] : 1;
                ApplyPassiveSkill(combatCatalog, skillItems, properties, itemId,
                    OfflineSkillProgression.OwnedRank(profile, itemId), level, subRank);
            }
        }

        private static void ApplyMonsterPassiveSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties,
            Map10RuntimeMonster source, int level, int subRank)
        {
            if (source.PassiveSkills == null) return;
            foreach (int itemId in source.PassiveSkills)
                if (itemId > 0)
                    ApplyPassiveSkill(combatCatalog, skillItems, properties, itemId, 1, level, subRank);
        }

        private static void ApplyPassiveSkill(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties, int itemId,
            int rank, int level, int subRank)
        {
            if (!skillItems.TryGet(itemId, out FightOfficialSkillItem item))
                throw new InvalidDataException("Official passive skill item is missing: " + itemId);
            if (item.PassiveRankPropId <= 0 && item.PassiveGroupLevelPropId <= 0) return;
            IReadOnlyDictionary<int, long> passive = combatCatalog.CalculatePassiveSkillProps(
                item.PassiveRankPropId, item.PassiveGroupLevelPropId, item.PassiveRankFactorId,
                item.PassivePropFactors, Math.Max(1, rank), Math.Max(1, level), subRank);
            foreach (KeyValuePair<int, long> property in passive)
                properties[property.Key] = properties.TryGetValue(property.Key, out long current)
                    ? checked(current + property.Value) : property.Value;
        }

        private void AssemblePlayerSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState player)
        {
            OfflineSkillProfile profile = progression.Player.Skills;
            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(profile);
            if (!equipped.Any(value => value > 0))
            {
                int starter = skillItems.InitialActiveSkill(progression.Player.InitialWeaponType);
                if (starter > 0) equipped = new[] { starter, 0, 0, 0 };
            }

            IReadOnlyDictionary<int, long> sourceProps = PropertyMap(player.Properties);
            var bindings = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                if (!skillItems.TryGet(itemId, out FightOfficialSkillItem item))
                    throw new InvalidDataException("Official player skill item is missing: " + itemId);
                if (item.EntityId <= 0)
                    throw new InvalidDataException("Official active skill item has no fight entity: " + itemId);
                int rank = OfflineSkillProgression.OwnedRank(profile, itemId);
                if (rank <= 0) rank = 1;
                int level = profile != null && profile.ActiveSlotLevels != null
                    && slot < profile.ActiveSlotLevels.Length ? profile.ActiveSlotLevels[slot] : 1;
                if (level <= 0) level = 1;
                bindings.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId,
                    Slot = slot,
                    ItemId = itemId,
                    Rank = rank,
                    Level = level,
                    SubRank = player.SubRank,
                    Cooldown = combatCatalog.CalculateSkillCooldown(item.EntityId, rank, level,
                        player.SubRank, sourceProps),
                    Properties = ToProperties(combatCatalog.CalculateSkillProps(item.EntityId, rank,
                        level, player.SubRank))
                });
            }
            if (bindings.Count == 0)
                throw new InvalidDataException("Official player has no equipped or initial active skill.");
            combatCatalog.AssembleUnit(player, bindings, null);
        }

        private static void AssembleMonsterSkills(FightOfficialCombatCatalog combatCatalog,
            FightBattleUnitState unit, Map10RuntimeMonster source)
        {
            IReadOnlyDictionary<int, long> sourceProps = PropertyMap(unit.Properties);
            var bindings = new List<FightOfficialSkillBinding>();
            foreach (Map10RuntimeSkill skill in source.ActiveSkills ?? Array.Empty<Map10RuntimeSkill>())
            {
                if (skill == null || skill.EntityId <= 0 || skill.ItemId <= 0) continue;
                int rank = Math.Max(1, skill.Rank);
                int level = Math.Max(1, unit.Level);
                bindings.Add(new FightOfficialSkillBinding
                {
                    EntityId = skill.EntityId,
                    Slot = skill.Slot,
                    ItemId = skill.ItemId,
                    Rank = rank,
                    Level = level,
                    SubRank = unit.SubRank,
                    Cooldown = combatCatalog.CalculateSkillCooldown(skill.EntityId, rank, level,
                        unit.SubRank, sourceProps),
                    Properties = ToProperties(combatCatalog.CalculateSkillProps(skill.EntityId, rank,
                        level, unit.SubRank))
                });
            }
            combatCatalog.AssembleUnit(unit, bindings, null);
        }

        private static IReadOnlyDictionary<int, long> PropertyMap(List<FightPropertyValue> properties)
        {
            var result = new Dictionary<int, long>();
            if (properties == null) return result;
            foreach (FightPropertyValue property in properties)
                if (property != null && property.Id > 0)
                {
                    result.TryGetValue(property.Id, out long current);
                    result[property.Id] = checked(current + property.Value);
                }
            return result;
        }


        private void OnBattleHudControl(string path)
        {
            if (!string.IsNullOrEmpty(path) && path.IndexOf("Leave", StringComparison.OrdinalIgnoreCase) >= 0)
                LeaveBattle();
        }


        private void FinishBattle()
        {
            if (battleController == null) return;
            FightBattleCheckpoint checkpoint = battleController.Checkpoint;
            if (checkpoint == null || !checkpoint.Finished) return;
            Map10RuntimeEntity entity = pendingInteraction;
            Action callback = battleCompletion;
            bool storyBattle = callback != null;
            FightBattleOutcome outcome = checkpoint.Outcome;
            bool won = outcome != null && outcome.Won;
            if (entity != null)
            {
                if (localSave.battleResults == null) localSave.battleResults = Array.Empty<Map10RuntimeBattleResult>();
                localSave.battleResults = localSave.battleResults.Concat(new[]
                {
                    new Map10RuntimeBattleResult
                    {
                        placementId = entity.PlacementId,
                        stageId = checkpoint.StageId,
                        reason = outcome == null ? 0 : outcome.Reason,
                        won = won,
                        round = outcome == null ? 0 : outcome.Round,
                        playerHp = outcome == null ? 0 : outcome.PlayerHp,
                        enemyHp = outcome == null ? 0 : outcome.EnemyHp
                    }
                }).ToArray();
            }

            bool cleared = won || storyBattle;
            if (cleared && entity != null)
            {
                string receipt = "map10:" + slotId + ":battle:" + entity.PlacementId + ":" + checkpoint.StageId
                    + ":" + (localSave.battleResults == null ? 0 : localSave.battleResults.Length);
                if (!localSave.receipts.Contains(receipt))
                {
                    ProgressionResult recorded = progressionSave.RecordMapBattle(10, entity.Region, 0, receipt);
                    if (recorded.Status == ProgressionResultStatus.InvalidState)
                        throw new InvalidOperationException(recorded.Message);
                    localSave.receipts = localSave.receipts.Concat(new[] { receipt }).ToArray();
                }
                if (!stages.TryGetValue(checkpoint.StageId, out Map10RuntimeStage stage)
                    || stage.Repeat >= 0)
                {
                    if (!localSave.completedStages.Contains(checkpoint.StageId))
                        localSave.completedStages = localSave.completedStages.Concat(new[] { checkpoint.StageId }).ToArray();
                    CompleteEntity(entity);
                }
            }

            Action completed = DestroyBattleSession();
            SaveLocalProgress();
            if (!cleared) ShowOfficialNotice("战斗失败");
            completed?.Invoke();
        }

        private void LeaveBattle()
        {
            if (battleController == null) return;
            Action callback = DestroyBattleSession();
            SaveLocalProgress();
            callback?.Invoke();
        }

        private void AbortBattle()
        {
            if (battleController == null) return;
            DestroyBattleSession();
            SaveLocalProgress();
        }

        private Action DestroyBattleSession()
        {
            Action callback = battleCompletion;
            battleCompletion = null;
            if (battleController != null)
            {
                battleController.HudControlClicked -= OnBattleHudControl;
                battleController.DisposeSession();
                Destroy(battleController.gameObject);
                battleController = null;
            }
            if (battleRoot != null) Destroy(battleRoot.gameObject);
            battleRoot = null;
            if (entityRoot != null) entityRoot.gameObject.SetActive(true);
            SetAvatarPosition(currentGrid);
            pendingInteraction = null;
            return callback;
        }

        private void PlayMapTalkDialog(Map10RuntimeMapTalkEntry talk, Map10RuntimeEntity entity)
        {
            if (talk == null || !nativeDialogs.TryGetValue(talk.DialogId, out activeNativeDialog))
                throw new InvalidDataException("Official Map10 native dialog is missing: " + (talk == null ? 0 : talk.DialogId));
            nativeDialogContext = entity;
            nativeDialogCompletion = entity != null && entity.StageIds.Any(value => value > 0)
                ? () => ShowStageDialog(entity, null) : null;
            nativeDialogNpc = null;
            nativeDialogActive = true;
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
            RunNativeDialog(activeNativeDialog.EntryNodeId);
        }

        private void RunNativeDialog(int nodeId)
        {
            if (!nativeDialogActive || activeNativeDialog == null) return;
            for (int count = 0; count < 64 && nativeDialogActive; count++)
            {
                Map10RuntimeNativeDialogNode node = activeNativeDialog.Nodes.FirstOrDefault(value =>
                    value != null && value.NodeId == nodeId);
                if (node == null) throw new InvalidDataException("Official Map10 dialog node is missing: " + nodeId);
                nativeDialogNodeId = node.NodeId;

                if (node.NodeType == 0)
                {
                    ShowNativeDialogText(node);
                    return;
                }
                if (node.NodeType == 1)
                {
                    ShowNativeDialogOptions(new[] { node.NodeId });
                    return;
                }
                if (node.NodeType != 2)
                    throw new InvalidDataException("Unsupported official Map10 dialog node type: " + node.NodeType);

                int next = node.NextNodes.FirstOrDefault();
                switch (node.ActionType)
                {
                    case 0:
                        if (node.NextNodes.Length == 0) FinishNativeDialog();
                        else if (node.NextNodes.Length == 1) { nodeId = next; continue; }
                        else { ShowNativeDialogOptions(node.NextNodes); return; }
                        return;
                    case 1:
                        ChangeNativeDialogProfession(node);
                        nodeId = next;
                        continue;
                    case 4:
                        string receipt = "map10:" + slotId + ":dialog:" + activeNativeDialog.Id + ":node:" + node.NodeId;
                        if (localSave.nativeDialogAwardReceipts.Contains(receipt))
                        {
                            if (node.AwardLimitedNodeId > 0
                                && activeNativeDialog.Nodes.Any(value => value.NodeId == node.AwardLimitedNodeId))
                            {
                                nodeId = node.AwardLimitedNodeId;
                                continue;
                            }
                        }
                        HideNativeDialogPanel();
                        nativeDialogPlayableWaiting = true;
                        GrantNativeDialogAwards(node, receipt);
                        StartCoroutine(ContinueNativeDialogAfterAward(next));
                        return;
                    case 6:
                        if (string.IsNullOrWhiteSpace(node.Story) || !stories.ContainsKey(node.Story))
                            throw new InvalidDataException("Official Map10 dialog references a missing story: " + node.Story);
                        HideNativeDialogPanel();
                        dialogueWaiting = false;
                        StartStoryGraph(node.Story, nativeDialogContext, () =>
                        {
                            if (!nativeDialogActive) return;
                            dialogueWaiting = true;
                            RunNativeDialog(next);
                        });
                        return;
                    case 10:
                        SpawnNativeDialogNpc(node);
                        HideNativeDialogPanel();
                        nativeDialogPlayableWaiting = true;
                        StartCoroutine(ContinueNativeDialogAfterPlayable(
                            OfficialPlayableDuration(node.PlayableKey), next));
                        return;
                    case 11:
                        HideNativeDialogPanel();
                        dialogueWaiting = true;
                        nativeDialogPlayableWaiting = true;
                        StartCoroutine(FinishNativeDialogAfterDelay(OfficialPlayableDuration(node.PlayableKey)));
                        return;
                    default:
                        throw new InvalidDataException("Unsupported official Map10 dialog action: " + node.ActionType);
                }
            }
            if (nativeDialogActive)
                throw new InvalidDataException("Official Map10 dialog exceeded its node limit: " + activeNativeDialog.Id);
        }

        private void ShowNativeDialogText(Map10RuntimeNativeDialogNode node)
        {
            if (!dialogRoles.TryGetValue(node.RoleId, out Map10RuntimeDialogRole role))
                throw new InvalidDataException("Official Map10 dialog role is missing: " + node.RoleId);
            nativeDialogChoicesWaiting = false;
            nativeDialogChoiceTargets = Array.Empty<int>();
            for (int i = 0; i < 5; i++) ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            ui.SetNodeActive("OptionTalkDialog", "/OptionTalkDialog/NextBtn", true);
            ui.SetPanelActive("OptionTalkDialog", true);
            ui.SetText("OptionTalkDialog", "/OptionTalkDialog/Content/CharacterAndTalkContent/TalkContent/TalkText", node.Text);
            ui.SetText("OptionTalkDialog", "/OptionTalkDialog/Content/CharacterAndTalkContent/TalkContent/NpcName/NpcNameText", role.RoleName);
            optionDialoguePortraits ??= new OfficialMap10DialoguePortraitView(ui, "OptionTalkDialog");
            optionDialoguePortraits.SetSpeaker(role, node.RoleFaceType);
            GameObject next = ui.Find("OptionTalkDialog", "/OptionTalkDialog/NextBtn");
            Button button = next == null ? null : next.GetComponent<Button>();
            if (button == null) throw new InvalidDataException("Official Map10 native dialog next button is missing.");
            button.onClick.RemoveListener(RequestDialogueAdvance);
            button.onClick.AddListener(RequestDialogueAdvance);
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
        }

        private void ShowNativeDialogOptions(IEnumerable<int> nodeIds)
        {
            nativeDialogChoiceTargets = nodeIds.Select(id => activeNativeDialog.Nodes.FirstOrDefault(value =>
                    value != null && value.NodeId == id))
                .Where(value => value != null && value.NodeType == 1)
                .Where(value => value.OptionFilterType != 1
                    || progressionSave.Save.Player.Wardrobe.Profession != value.OptionFilterProfession)
                .Select(value => value.NodeId).Take(5).ToArray();
            if (nativeDialogChoiceTargets.Length == 0)
                throw new InvalidDataException("Official Map10 native dialog has no available choices: " + nativeDialogNodeId);

            nativeDialogChoicesWaiting = true;
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
            ui.SetNodeActive("OptionTalkDialog", "/OptionTalkDialog/NextBtn", false);
            for (int index = 0; index < 5; index++)
            {
                string root = StoryOptionSlotPath(index);
                bool active = index < nativeDialogChoiceTargets.Length;
                ui.SetNodeActive("OptionTalkDialog", root, active);
                if (!active) continue;
                Map10RuntimeNativeDialogNode option = activeNativeDialog.Nodes.First(value =>
                    value.NodeId == nativeDialogChoiceTargets[index]);
                ui.SetText("OptionTalkDialog", root + "/Content/OptionText/Text", option.Text);
                Sprite icon = uiCatalog.FindSprite(option.OptionIconPath);
                if (icon != null) ui.SetSprite("OptionTalkDialog", root + "/Content/TypeImg", icon);
                GameObject item = ui.Find("OptionTalkDialog", root);
                Button button = item == null ? null : item.GetComponent<Button>();
                if (button == null) throw new InvalidDataException("Official Map10 native option button is missing: " + root);
                int capturedIndex = index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectNativeDialogOption(capturedIndex));
            }
            ui.SetPanelActive("OptionTalkDialog", true);
        }

        private void SelectNativeDialogOption(int index)
        {
            if (!nativeDialogActive || !nativeDialogChoicesWaiting
                || index < 0 || index >= nativeDialogChoiceTargets.Length) return;
            int target = nativeDialogChoiceTargets[index];
            nativeDialogChoicesWaiting = false;
            for (int i = 0; i < 5; i++) ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            RunNativeDialog(target);
        }

        private void AdvanceNativeDialog()
        {
            dialogueAdvanceRequested = false;
            Map10RuntimeNativeDialogNode current = activeNativeDialog?.Nodes?.FirstOrDefault(value =>
                value != null && value.NodeId == nativeDialogNodeId);
            if (current == null) throw new InvalidDataException("Official Map10 native dialog lost its current node.");
            if (current.NextNodes == null || current.NextNodes.Length == 0) FinishNativeDialog();
            else if (current.NextNodes.Length == 1) RunNativeDialog(current.NextNodes[0]);
            else ShowNativeDialogOptions(current.NextNodes);
        }

        private void SpawnNativeDialogNpc(Map10RuntimeNativeDialogNode node)
        {
            if (node.NpcEntityClassId != 8402)
                throw new InvalidDataException("Unsupported official Map10 dialog NPC class: " + node.NpcEntityClassId);
            if (nativeDialogNpc != null) entityView.RemoveStoryEntity(nativeDialogNpc);
            MapGridPoint grid = FindAdjacentStoryGrid();
            nativeDialogNpc = entityView.SpawnStoryEntity(node.NpcEntityClassId,
                "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view", new Vector2(grid.X, grid.Y));
            entityView.PlayStoryEntityAnimation(nativeDialogNpc, "appear_01");
        }

        private float OfficialPlayableDuration(string keyOrGuid)
        {
            if (!officialPlayables.TryGetValue(keyOrGuid ?? string.Empty, out Map10RuntimePlayable playable))
                throw new InvalidDataException("Official Map10 playable is not loaded: " + keyOrGuid);
            return playable.Duration;
        }

        private IEnumerator ContinueNativeDialogAfterPlayable(float seconds, int nextNodeId)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (!nativeDialogActive) yield break;
            nativeDialogPlayableWaiting = false;
            dialogueWaiting = true;
            RunNativeDialog(nextNodeId);
        }

        private IEnumerator ContinueNativeDialogAfterAward(int nextNodeId)
        {
            yield return new WaitForSecondsRealtime(2f);
            HideOfficialNotice();
            if (!nativeDialogActive) yield break;
            nativeDialogPlayableWaiting = false;
            dialogueWaiting = true;
            RunNativeDialog(nextNodeId);
        }

        private void ChangeNativeDialogProfession(Map10RuntimeNativeDialogNode node)
        {
            if (node.Profession != 1 && node.Profession != 10)
                throw new InvalidDataException("Unsupported official Map10 dialog profession: " + node.Profession);
            ProgressionResult result = progressionSave.ApplyCharacterMutation(
                player => player.Wardrobe.Profession = node.Profession,
                "map10:" + slotId + ":dialog:" + activeNativeDialog.Id + ":profession:" + node.Profession);
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(result.Message);
            progression = new OfflineCharacterProgressionService(progressionSave, progression.Catalog);
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", characterName);
        }

        private void GrantNativeDialogAwards(Map10RuntimeNativeDialogNode node, string receipt)
        {
            ProgressionResult result = progressionSave.ApplyInventoryTransaction(receipt, save =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (Map10RuntimeAwardItem award in node.AwardItems)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        award.ItemId, award.Count, now);
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Official Map10 dialog award could not be applied: " + award.ItemId);
                }
            });
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(result.Message);
            if (!localSave.nativeDialogAwardReceipts.Contains(receipt))
                localSave.nativeDialogAwardReceipts = localSave.nativeDialogAwardReceipts.Concat(new[] { receipt }).ToArray();
            SaveLocalProgress();
            ShowOfficialNotice("獲得物品 " + string.Join("、", node.AwardItems.Select(value => value.ItemId + " ×" + value.Count)));
        }

        private IEnumerator FinishNativeDialogAfterDelay(float seconds)
        {
            if (nativeDialogNpc != null) entityView.PlayStoryEntityAnimation(nativeDialogNpc, "disappear_01");
            yield return new WaitForSecondsRealtime(seconds);
            if (nativeDialogNpc != null) entityView.RemoveStoryEntity(nativeDialogNpc);
            nativeDialogNpc = null;
            nativeDialogPlayableWaiting = false;
            FinishNativeDialog();
        }

        private void HideNativeDialogPanel()
        {
            ui.SetPanelActive("OptionTalkDialog", false);
            for (int i = 0; i < 5; i++) ui.SetNodeActive("OptionTalkDialog", StoryOptionSlotPath(i), false);
            optionDialoguePortraits?.Clear();
        }

        private void FinishNativeDialog()
        {
            HideNativeDialogPanel();
            if (nativeDialogNpc != null) entityView.RemoveStoryEntity(nativeDialogNpc);
            nativeDialogNpc = null;
            Action completed = nativeDialogCompletion;
            nativeDialogActive = false;
            nativeDialogChoicesWaiting = false;
            nativeDialogPlayableWaiting = false;
            nativeDialogChoiceTargets = Array.Empty<int>();
            activeNativeDialog = null;
            nativeDialogContext = null;
            nativeDialogCompletion = null;
            nativeDialogNodeId = 0;
            dialogueWaiting = false;
            dialogueAdvanceRequested = false;
            completed?.Invoke();
        }

        private void PlayStory(string key, int groupId, Action complete)
        {
            if (!stories.TryGetValue(key, out Map10RuntimeStory story))
                throw new InvalidDataException("Official Map10 story is missing: " + key);
            Map10RuntimeDialogueGroup group = story.dialogueGroups.FirstOrDefault(value => value.Id == groupId);
            if (group == null || group.Lines == null || group.Lines.Length == 0)
            {
                complete?.Invoke();
                return;
            }
            dialogueLines = group.Lines.OrderBy(value => value.Order).ToArray();
            dialogueLineIndex = 0;
            dialogueFinished = complete;
            dialogueWaiting = true;
            dialogueAdvanceRequested = false;
            ShowDialogueLine();
        }

        private void ShowDialogueLine()
        {
            if (dialogueLineIndex >= dialogueLines.Length)
            {
                ui.SetPanelActive("TalkDialog", false);
                dialoguePortraits?.Clear();
                dialogueWaiting = false;
                Action complete = dialogueFinished;
                dialogueFinished = null;
                complete?.Invoke();
                return;
            }
            Map10RuntimeDialogueLine line = dialogueLines[dialogueLineIndex];
            if (!dialogRoles.TryGetValue(line.RoleId, out Map10RuntimeDialogRole role))
                throw new InvalidDataException("Official Map10 dialogue references an unknown role: " + line.RoleId);
            ui.SetPanelActive("TalkDialog", true);
            ui.SetText("TalkDialog", "/TalkDialog/Content/CharacterAndTalkContent/TalkContent/TalkText", line.Text);
            ui.SetText("TalkDialog", "/TalkDialog/Content/CharacterAndTalkContent/TalkContent/NpcName/NpcNameText", role.RoleName);
            dialoguePortraits ??= new OfficialMap10DialoguePortraitView(ui);
            dialoguePortraits.SetSpeaker(role, line.RoleFaceType);
            GameObject next = ui.Find("TalkDialog", "/TalkDialog/NextBtn");
            Button button = next == null ? null : next.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(RequestDialogueAdvance);
                button.onClick.AddListener(RequestDialogueAdvance);
            }
        }

        private void RequestDialogueAdvance()
        {
            if (nativeDialogPlayableWaiting) return;
            dialogueAdvanceRequested = true;
        }

        private void AdvanceDialogue()
        {
            dialogueAdvanceRequested = false;
            dialogueLineIndex++;
            ShowDialogueLine();
        }

        private void CompleteEntity(Map10RuntimeEntity entity)
        {
            if (!localSave.completedEntityPlacements.Contains(entity.PlacementId))
            {
                localSave.completedEntityPlacements = localSave.completedEntityPlacements
                    .Concat(new[] { entity.PlacementId }).ToArray();
                ProgressionResult recorded = progressionSave.RecordEntityTagCompleted("map10:entity:" + entity.PlacementId,
                    "map10:" + slotId + ":entity:" + entity.PlacementId);
                if (recorded.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(recorded.Message);
            }
            RefreshRegionProgress();
            RefreshEntityActivity();
            SaveLocalProgress();
        }

        private void RefreshRegionProgress()
        {
            var completed = new HashSet<int>(localSave.completedEntityPlacements);
            Dictionary<int, int> points = OfficialMapProgressAdapter.CalculateRegionPoints(
                session.Definition, new Dictionary<int, int>(), completed);
            regionPoints.Clear();
            foreach (KeyValuePair<int, int> region in points) regionPoints.Add(region.Key, region.Value);
            foreach (KeyValuePair<int, int> region in points)
            {
                if (!regionPointLimits.TryGetValue(region.Key, out int maximum) || maximum <= 0 || region.Value > maximum)
                    throw new InvalidDataException("Official Map10 region progress exceeds its pinned maximum: " + region.Key);
                int fixedProgress = OfficialMapProgressCalculator.CalculateRegionProgress(
                    session.Definition, region.Key, region.Value);
                ProgressionResult result = progressionSave.SetMapRegionProgress(10, region.Key, fixedProgress);
                if (result.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(result.Message);
            }
        }

        private void CollectEntityAwards(Map10RuntimeEntity entity)
        {
            foreach (Map10RuntimeAwardItem award in entity.AwardItems)
            {
                string receipt = "map10:" + slotId + ":entity:" + entity.PlacementId + ":item:" + award.ItemId;
                ProgressionResult result = progressionSave.ApplyInventoryTransaction(receipt, save =>
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        award.ItemId, award.Count, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Official Map10 item award could not be applied: " + award.ItemId);
                });
                if (result.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidOperationException(result.Message);
            }

            CompleteEntity(entity);
            Map10RuntimeAwardItem first = entity.AwardItems[0];
            ShowOfficialNotice("获得物品 " + first.ItemId + " ×" + first.Count);
        }

        private bool IsEntityLocked(Map10RuntimeEntity entity)
        {
            if (!IsRegionAccessible(entity.Region)) return true;
            if (entity.LockType <= 0 || entity.LockedByEntityGroups.Length == 0) return false;
            return entity.LockedByEntityGroups.Any(groupId => !IsEntityGroupComplete(groupId));
        }

        private bool IsEntityGroupComplete(int groupId)
        {
            if (!mapEntities.TryGetValue(groupId, out Map10RuntimeEntity conditionEntity)) return false;
            return localSave.completedEntityPlacements.Contains(groupId)
                || conditionEntity.StageIds.Any(localSave.completedStages.Contains);
        }

        private void ValidateEntityLockGroups()
        {
            foreach (Map10RuntimeEntity entity in mapEntities.Values)
                foreach (int groupId in entity.LockedByEntityGroups)
                    if (!mapEntities.ContainsKey(groupId))
                        throw new InvalidDataException("Official Map10 lock references a missing entity group: " + groupId);
        }

        private void RefreshEntityActivity()
        {
            if (entityView == null || localSave == null) return;
            foreach (Map10RuntimeEntity entity in mapEntities.Values)
            {
                bool active = entity.Active && IsRegionAccessible(entity.Region)
                    && !localSave.completedEntityPlacements.Contains(entity.PlacementId)
                    && !IsMapLogicEntityDestroyed(entity.PlacementId);
                entityView.SetEntityActive(entity.PlacementId, active);
            }
        }

        private bool IsMapLogicEntityDestroyed(int placementId)
        {
            if (!mapLogicTags.TryGetValue(placementId, out Map10RuntimeLogicTag tag)) return false;
            return localSave.mapLogicStates.Any(value => value != null && value.Tag == tag.Tag && value.State < 0);
        }

        private void ApplyStoryMapLogicStates(Map10RuntimeMapLogicState[] states)
        {
            foreach (Map10RuntimeMapLogicState state in states ?? Array.Empty<Map10RuntimeMapLogicState>())
            {
                if (state == null || string.IsNullOrWhiteSpace(state.Tag))
                    throw new InvalidDataException("Official Map10 story map-logic state is malformed.");
                Map10RuntimeLogicTag logicTag = mapLogicTags.Values.FirstOrDefault(value => value.Tag == state.Tag);
                if (logicTag == null || !mapEntities.ContainsKey(logicTag.LogicId))
                    throw new InvalidDataException("Official Map10 story map-logic tag is not in the loaded map: " + state.Tag);
                Map10RuntimeMapLogicState saved = localSave.mapLogicStates.FirstOrDefault(value =>
                    value != null && value.Tag == state.Tag);
                if (saved == null)
                    localSave.mapLogicStates = localSave.mapLogicStates.Concat(new[]
                    {
                        new Map10RuntimeMapLogicState { Tag = state.Tag, State = state.State }
                    }).ToArray();
                else saved.State = state.State;
            }
            RefreshEntityActivity();
            SaveLocalProgress();
        }

        private void LoadRegionConditions()
        {
            regionConfigs.Clear();
            mapConditions.Clear();
            regionPointLimits.Clear();
            foreach (OfficialMapRegionProgress progress in session.Definition.MaxRegionProgress)
            {
                if (progress == null || progress.Value <= 0 || regionPointLimits.ContainsKey(progress.Region))
                    throw new InvalidDataException("Official Map10 region progress maximum is malformed.");
                regionPointLimits.Add(progress.Region, progress.Value);
            }

            foreach (OfficialWorldConfigRecord record in session.Definition.Configs ?? Array.Empty<OfficialWorldConfigRecord>())
            {
                if (record == null) continue;
                if (record.TableName == "TbMapRegionInfo")
                {
                    Map10RuntimeRegionConfig region = JsonUtility.FromJson<Map10RuntimeRegionConfig>(record.Json);
                    if (region == null || region.ClassId != 10 || region.Region < 0 || region.Areas == null
                        || regionConfigs.ContainsKey(region.Region))
                        throw new InvalidDataException("Official Map10 region configuration is malformed.");
                    regionConfigs.Add(region.Region, region);
                }
                else if (record.TableName == "TbConditionInfo")
                {
                    Map10RuntimeConditionRecord row = JsonUtility.FromJson<Map10RuntimeConditionRecord>(record.Json);
                    if (row == null || row.ClassId <= 0 || row.Condition == null || mapConditions.ContainsKey(row.ClassId))
                        throw new InvalidDataException("Official Map10 access condition is malformed.");
                    mapConditions.Add(row.ClassId, row.Condition);
                }
            }

            if (regionConfigs.Count != regionPointLimits.Count)
                throw new InvalidDataException("Official Map10 regions and progress limits differ.");
            foreach (Map10RuntimeRegionConfig region in regionConfigs.Values)
                if (region.AccessibleConditionId > 0 && !mapConditions.ContainsKey(region.AccessibleConditionId))
                    throw new InvalidDataException("Map10 region references a missing official access condition: " + region.AccessibleConditionId);

            foreach (int id in new[] { 1201, 1202, 1203, 1204 })
            {
                if (!mapConditions.TryGetValue(id, out Map10RuntimeCondition condition)
                    || condition.Type != "WorldMapProgressCondition" || condition.MapClassId != 10
                    || condition.Progress != 10000 || !regionPointLimits.ContainsKey(condition.Region))
                    throw new InvalidDataException("Pinned Map10 full-region access condition is invalid: " + id);
            }
        }

        private bool IsRegionAccessible(int regionId)
        {
            if (!regionConfigs.TryGetValue(regionId, out Map10RuntimeRegionConfig region))
                throw new InvalidDataException("Map10 entity references an unknown region: " + regionId);
            if (region.AccessibleConditionId <= 0) return true;
            if (!mapConditions.TryGetValue(region.AccessibleConditionId, out Map10RuntimeCondition condition)
                || condition.Type != "WorldMapProgressCondition" || condition.MapClassId != 10
                || condition.Progress != 10000)
                throw new InvalidDataException("Unsupported official Map10 access condition: " + region.AccessibleConditionId);
            if (!regionPointLimits.TryGetValue(condition.Region, out int maximum) || maximum <= 0)
                throw new InvalidDataException("Map10 access condition references a region without a progress maximum.");
            return regionPoints.TryGetValue(condition.Region, out int progress) && progress >= maximum;
        }

        private bool HasStory(string name) => localSave.completedStories.Contains(name);

        private int GetStoryPlayCount(string name) => localSave.storyPlayCounts
            .FirstOrDefault(value => value != null && value.key == name)?.count ?? 0;

        private void IncrementStoryPlayCount(string name)
        {
            Map10RuntimeStoryCount current = localSave.storyPlayCounts
                .FirstOrDefault(value => value != null && value.key == name);
            if (current == null)
            {
                localSave.storyPlayCounts = localSave.storyPlayCounts.Concat(new[]
                {
                    new Map10RuntimeStoryCount { key = name, count = 1 }
                }).ToArray();
                return;
            }
            current.count = checked(current.count + 1);
        }

        private void CompleteStory(string name)
        {
            if (!HasStory(name)) localSave.completedStories = localSave.completedStories.Concat(new[] { name }).ToArray();
        }

        private void ShowOfficialNotice(string message)
        {
            ui.SetPanelActive("MapNotifyUI", true);
            ui.SetText("MapNotifyUI", "/MapNotifyUI/Content/Text", message);
            if (noticeRoutine != null) StopCoroutine(noticeRoutine);
            noticeRoutine = StartCoroutine(HideOfficialNoticeAfterDelay(1.8f));
        }

        private IEnumerator HideOfficialNoticeAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            HideOfficialNotice();
        }

        private void HideOfficialNotice()
        {
            if (ui != null) ui.SetPanelActive("MapNotifyUI", false);
            noticeRoutine = null;
        }

        private void UpdateWorldLabels()
        {
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", characterName);
        }

        private void UpdateMinimapMarker()
        {
            if (minimapPlayerMarker == null || avatar == null) return;
            Vector2 local = avatar.transform.position - new Vector3(session.Definition.Navigation.Origin.X,
                session.Definition.Navigation.Origin.Y, 0f);
            float x = Mathf.Clamp01(local.x / session.Definition.Navigation.Width);
            float y = Mathf.Clamp01(local.y / session.Definition.Navigation.Height);
            minimapPlayerMarker.anchorMin = minimapPlayerMarker.anchorMax = new Vector2(x, y);
        }

        private void StartMovementToCurrentGrid()
        {
            SetAvatarPosition(currentGrid);
            localSave.gridX = currentGrid.X;
            localSave.gridY = currentGrid.Y;
        }

        private void SetAvatarPosition(MapGridPoint point)
        {
            currentGrid = point;
            if (avatar != null) avatar.transform.position = GridToWorld(point);
            if (localSave != null)
            {
                localSave.gridX = point.X;
                localSave.gridY = point.Y;
            }
        }

        private Vector3 GridToWorld(MapGridPoint point)
        {
            OfficialMapVector3 world = OfficialMapNavigationView.GridToWorld(session.Definition.Navigation, point.X, point.Y);
            return new Vector3(world.X, world.Y, world.Z - 0.1f);
        }

        private void SaveLocalProgress()
        {
            if (localSave == null || string.IsNullOrEmpty(mapSavePath)) return;
            localSave.gridX = currentGrid.X;
            localSave.gridY = currentGrid.Y;
            LocalJsonSaveFile.Write(mapSavePath, JsonUtility.ToJson(localSave, true), ValidLocalSave);
        }

        private bool ValidLocalSave(string json)
        {
            try
            {
                Map10LocalSave parsed = JsonUtility.FromJson<Map10LocalSave>(json);
                return parsed != null && parsed.schema == 1 && parsed.slotId == slotId
                    && parsed.gridX >= 0 && parsed.gridX < 60 && parsed.gridY >= 0 && parsed.gridY < 60
                    && (parsed.completedStories == null || parsed.completedStories.All(value => value != null))
                    && (parsed.storyPlayCounts == null || parsed.storyPlayCounts.All(value => value != null
                        && !string.IsNullOrEmpty(value.key) && value.count >= 0))
                    && (parsed.completedEntityPlacements == null || parsed.completedEntityPlacements.All(value => value > 0))
                    && (parsed.mapLogicStates == null || parsed.mapLogicStates.All(value => value != null
                        && !string.IsNullOrWhiteSpace(value.Tag)))
                    && (parsed.receipts == null || parsed.receipts.All(value => value != null));
            }
            catch { return false; }
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

        private sealed class Map10RuntimeEntity
        {
            public int PlacementId;
            public int EntityClassId;
            public int TypeId;
            public string TalkTag;
            public string ModelPath;
            public int Region;
            public MapGridPoint Grid;
            public int[] StageIds;
            public bool Active;
            public int LockType;
            public int[] LockedByEntityGroups;
            public bool ConditionEntityNeverDestroy;
            public bool ConditionEntityInactiveInUnlock;
            public Map10RuntimeAwardItem[] AwardItems;
            public int ChangeMapClassId;

            public Map10RuntimeEntity(OfficialMap10EntityInfo info)
            {
                PlacementId = info.PlacementId;
                EntityClassId = info.EntityClassId;
                TypeId = info.TypeId;
                ModelPath = info.ModelPath;
                Region = info.Region;
                Grid = new MapGridPoint(info.Grid[0], info.Grid[1]);
                StageIds = info.StageIds ?? Array.Empty<int>();
                Active = info.Active;
                LockType = info.LockType;
                LockedByEntityGroups = info.LockedByEntityGroups ?? Array.Empty<int>();
                ConditionEntityNeverDestroy = info.ConditionEntityNeverDestroy;
                ConditionEntityInactiveInUnlock = info.ConditionEntityInactiveInUnlock;
                AwardItems = info.AwardItems ?? Array.Empty<Map10RuntimeAwardItem>();
                ChangeMapClassId = info.ChangeMapClassId;
            }
        }

        [Serializable]
        private sealed class Map10RuntimeRegionConfig
        {
            public int ClassId;
            public int Region;
            public int AccessibleConditionId;
            public int[] Areas;
            public string NotAccessibleMapTalkId;
        }

        [Serializable]
        private sealed class Map10RuntimeConditionRecord
        {
            public int ClassId;
            public Map10RuntimeCondition Condition;
        }

        [Serializable]
        private sealed class Map10RuntimeCondition
        {
            public string _type;
            public int MapClassId;
            public int Region;
            public int Progress;
            public string Type => _type;
        }
    }
}
