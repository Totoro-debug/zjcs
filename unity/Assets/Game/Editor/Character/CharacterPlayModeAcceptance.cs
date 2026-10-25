using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;

namespace Zjcs.Editor.Character
{
    /// <summary>
    /// Play Mode logic acceptance for the character page. It boots a temporary profile, opens the
    /// official panel and drives the real buttons; rendering/pointer input still needs a windowed run.
    /// </summary>
    public static class CharacterPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Character.Acceptance.Active";
        private const string RunnerStartedKey = "Zjcs.Character.Acceptance.RunnerStarted";
        private const string RootKey = "Zjcs.Character.Acceptance.Root";
        private const string SlotKey = "Zjcs.Character.Acceptance.Slot";
        private const string ResultKey = "Zjcs.Character.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.Character.Acceptance.Deadline";
        private const string InteractiveReadyKey = "Zjcs.Character.Acceptance.InteractiveReady";

        [MenuItem("Zjcs/Validate/Character Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running character acceptance.");
            CharacterAcceptancePreparation.ValidatePreparedModels();
            try
            {
                Type equipmentValidation = Type.GetType(
                    "Zjcs.Editor.Equipment.OfficialEquipmentValidation, Zjcs.Editor.Equipment");
                equipmentValidation?.GetMethod("Validate")?.Invoke(null, null);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character acceptance could not install the equipment catalog: " + error.Message);
            }
            string root = CreateAcceptanceProfile(out string slot);

            SessionState.SetString(RootKey, root);
            SessionState.SetString(SlotKey, slot);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetBool(InteractiveReadyKey, false);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 1200f);
            SessionState.SetBool(RunnerStartedKey, false);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void AutoRunInWindowedEditor()
        {
            string autorun = Environment.GetEnvironmentVariable("ZJCS_CHARACTER_AUTORUN");
            Debug.Log("[Character acceptance] autorun check env=" + (autorun ?? "<null>"));
            if (autorun != "1") return;
            Environment.SetEnvironmentVariable("ZJCS_CHARACTER_AUTORUN", "0");
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= AutoRunTick;
            EditorApplication.update += AutoRunTick;
        }

        private static void AutoRunTick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= AutoRunTick;
            Debug.Log("[Character acceptance] autorun start");
            if (!SessionState.GetBool(ActiveKey, false)) Run();
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (EditorApplication.isPlaying && SessionState.GetBool(RunnerStartedKey, false))
            {
                ResetStaleAcceptanceObjects();
                DeleteAcceptanceRoot(SessionState.GetString(RootKey, string.Empty));
                string root = CreateAcceptanceProfile(out string slot);
                SessionState.SetString(RootKey, root);
                SessionState.SetString(SlotKey, slot);
                SessionState.SetInt(ResultKey, 2);
                SessionState.SetBool(InteractiveReadyKey, false);
                SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 1200f);
                SessionState.SetBool(RunnerStartedKey, false);
                Debug.Log("[Character acceptance] recovered after assembly reload; isolated profile restarted.");
            }
            else if (SessionState.GetBool(RunnerStartedKey, false))
            {
                DeleteAcceptanceRoot(SessionState.GetString(RootKey, string.Empty));
                SessionState.SetBool(ActiveKey, false);
                SessionState.SetBool(RunnerStartedKey, false);
                SessionState.SetBool(InteractiveReadyKey, false);
                Debug.Log("[Character acceptance] cleared stale run after leaving Play Mode.");
            }
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static string CreateAcceptanceProfile(out string slot)
        {
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "character-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var slots = new ProfileSlotCatalog(root);
            slot = slots.CreateSlot();
            OfflineMainlineSave save = new OfflineMainlineSave();
            save.CreatedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "人物验收";
            OfficialCharacterCatalog acceptanceCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            save.Player.Level = 20;
            save.Player.Experience = acceptanceCatalog.CumulativeLevelExperience[
                Math.Min(20, acceptanceCatalog.CumulativeLevelExperience.Count - 1)];
            save.Player.Rank = 1;
            save.Player.SubRank = 1;
            save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Wardrobe.Dyes.Add(new OfflineAppearanceDye
            {
                Type = 1, ColorId = 1, Hue = 0, Saturation = 0, Value = 0
            });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 1, Count = 100000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 39600, Count = 1 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 32504, Count = 1 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 12000, Count = 120 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 41300, Count = 5000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 5001, Count = 3 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 41302, Count = 5000 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 701, Count = 100 });
            try
            {
                OfficialEquipmentCatalog equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
                OfflineEquipmentInstance instance = OfficialEquipmentGenerator.Generate(equipment, 20000, 1,
                    new EquipmentRollContext { DropLevel = 20, PlayerLevel = 20, SubRank = 1, SourceOperate = 0 },
                    () => 0.5f);
                if (instance == null) throw new InvalidOperationException("The acceptance equipment could not be generated.");
                save.Player.Equipment.Instances.Add(instance);
                save.Player.Equipment.NextInstanceId = 2;
                save.Player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = 10, InstanceId = 1 });
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character acceptance without equipment: " + error.Message);
            }
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            return root;
        }

        private static void ResetStaleAcceptanceObjects()
        {
            foreach (AcceptanceRunner runner in UnityEngine.Object.FindObjectsOfType<AcceptanceRunner>(true))
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name != "CharacterAcceptanceView"
                    && transform.name != "CharacterAcceptanceBackgroundCamera") continue;
                UnityEngine.Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static void DeleteAcceptanceRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            string outputRoot = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "output"));
            string fullRoot = Path.GetFullPath(root);
            if (!string.Equals(Path.GetDirectoryName(fullRoot), outputRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullRoot).StartsWith("character-smoke-", StringComparison.Ordinal)
                || !Directory.Exists(fullRoot)) return;
            Directory.Delete(fullRoot, true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            string root = SessionState.GetString(RootKey, string.Empty);
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            if (Directory.Exists(root)) Directory.Delete(root, true);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(RunnerStartedKey, false);
            if (Environment.GetEnvironmentVariable("ZJCS_CHARACTER_KEEP_EDITOR") != "1"
                && (Application.isBatchMode || Environment.GetEnvironmentVariable("ZJCS_CHARACTER_AUTORUN") == "1"))
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Character acceptance exceeded its deadline."));
                return;
            }
            if (SessionState.GetBool(RunnerStartedKey, false)) return;
            SessionState.SetBool(RunnerStartedKey, true);
            var runner = new GameObject("CharacterAcceptanceRunner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<AcceptanceRunner>().StartCoroutine(Guarded(Scenario()));
        }

        public sealed class AcceptanceRunner : MonoBehaviour
        {
        }

        private static IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                object value;
                bool moved;
                try
                {
                    moved = current.MoveNext();
                    value = moved ? current.Current : null;
                }
                catch (Exception error)
                {
                    Finish(false, error);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
                yield return value;
            }
        }

        private static IEnumerator Scenario()
        {
            foreach (Canvas existing in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
                existing.gameObject.SetActive(false);
            Camera sceneCamera = null;
            foreach (Camera existing in UnityEngine.Object.FindObjectsOfType<Camera>(true))
            {
                if (existing.name == "Main Camera")
                {
                    sceneCamera = existing;
                    continue;
                }
                existing.gameObject.SetActive(false);
            }
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(true);
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(0.055f, 0.075f, 0.08f);
                sceneCamera.cullingMask = 0;
                sceneCamera.depth = -1000f;
            }
            var backgroundCameraObject = new GameObject("CharacterAcceptanceBackgroundCamera");
            Camera backgroundCamera = backgroundCameraObject.AddComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(0.055f, 0.075f, 0.08f);
            backgroundCamera.orthographic = true;
            backgroundCamera.orthographicSize = 10f;
            backgroundCamera.transform.position = new Vector3(0f, 0f, -10f);
            backgroundCamera.cullingMask = 1 << 31;
            backgroundCamera.depth = 1000f;
            var backgroundObject = new GameObject("CharacterAcceptanceBackground", typeof(SpriteRenderer));
            backgroundObject.layer = 31;
            backgroundObject.transform.SetParent(backgroundCamera.transform, false);
            backgroundObject.transform.localPosition = new Vector3(0f, 0f, 10f);
            backgroundObject.transform.localScale = new Vector3(2500f, 2500f, 1f);
            SpriteRenderer backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();
            backgroundRenderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 100f);
            backgroundRenderer.color = new Color(0.055f, 0.075f, 0.08f);

            string slot = SessionState.GetString(SlotKey, string.Empty);
            string savesRoot = SessionState.GetString(RootKey, string.Empty);
            var slots = new ProfileSlotCatalog(savesRoot);
            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            OfficialEquipmentCatalog equipment = null;
            try
            {
                equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
                progression.AttachEquipmentCatalog(equipment);
                OfficialSkillRecommendationSet recommendations = assets.LoadSkillRecommendations();
                progression.AttachSkillRules(assets.LoadSkillRules(), recommendations.recommendations);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character acceptance optional catalogs: " + error.Message);
            }
            Require(equipment != null, "The official equipment catalog is unavailable for the character acceptance.");
            Debug.Log("[Character acceptance] checking equipment cabinet transaction");
            ValidateEquipmentCabinetMutation(session, progression, equipment);
            Debug.Log("[Character acceptance] checking NPC recruitment transaction");
            ValidateNpcMeetMutation(session, progression);
            Debug.Log("[Character acceptance] checking NPC gift transaction");
            long npcGiftExperience = ValidateNpcGiftMutation(session, progression);
            Debug.Log("[Character acceptance] checking guild skill transaction");
            ValidateLegionSkillMutation(session, progression);
            Debug.Log("[Character acceptance] checking guild building progression");
            ValidateLegionBuildingMutation(progression.Catalog);
            PetGachaRuntimeCatalog petCatalog = OfficialPetGachaAssets.LoadDefault();
            PetGachaItem firstPetItem = petCatalog.Item(1601);
            PetGachaItem secondPetItem = petCatalog.Item(1602);
            Require(firstPetItem != null && secondPetItem != null,
                "The official character pet candidates are unavailable.");
            OfflinePetInstance firstPet = OfflinePetProgressionService.RegisterAcquiredPet(
                progression.Player, firstPetItem.ClassId, firstPetItem.Quality);
            OfflinePetInstance secondPet = OfflinePetProgressionService.RegisterAcquiredPet(
                progression.Player, secondPetItem.ClassId, secondPetItem.Quality);
            Require(progression.PetProgression.SetFollowingPet(firstPet.Index).IsApplied,
                "The acceptance following pet could not be saved.");
            Require(progression.PetProgression.EquipPlanPet(0, firstPet.Index).IsApplied
                && progression.PetProgression.EquipPlanPet(1, firstPet.Index).IsApplied
                && progression.PetProgression.EquipPlanPet(2, secondPet.Index).IsApplied,
                "The acceptance skill plans could not save their pet assignments.");
            ProgressionResult wardrobe = progression.InitializeWardrobe("character-acceptance-wardrobe");
            Require(wardrobe.Status != ProgressionResultStatus.InvalidState,
                "Wardrobe initialization failed: " + wardrobe.Message);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            progression.SynchronizeExpiredItems(now, "character-acceptance-expiry");
            Debug.Log("[Character acceptance] constructing official character panel");
            var host = new GameObject("CharacterAcceptanceView");
            UnityEngine.Object.DontDestroyOnLoad(host);
            Canvas acceptanceCanvas = host.AddComponent<Canvas>();
            acceptanceCanvas.overrideSorting = true;
            acceptanceCanvas.sortingOrder = 1000;
            var view = host.AddComponent<OfficialCharacterPanelView>();
            view.Initialize(assets, progression, slot, savesRoot);
            acceptanceCanvas.targetDisplay = 0;
            acceptanceCanvas.overrideSorting = true;
            acceptanceCanvas.sortingOrder = 32767;
            Require(acceptanceCanvas.enabled && acceptanceCanvas.gameObject.activeInHierarchy
                && view.transform.Find("CharacterPanel")?.gameObject.activeInHierarchy == true,
                "The official character panel root is not active on its acceptance canvas.");
            CharacterPetEquipmentView petView = host.GetComponentInChildren<CharacterPetEquipmentView>(true);
            Require(petView != null, "The character pet equipment view was not initialized.");
            Require(view.transform.Find("CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EmptyPetContent/Contet/Add") != null,
                "The recursive official character panel parse omitted its deep empty-pet selector node.");
            yield return WaitFrames(3);
            Canvas.ForceUpdateCanvases();
            Require(EventSystem.current != null && EventSystem.current.currentInputModule != null
                && EventSystem.current.currentInputModule.isActiveAndEnabled,
                "The character page EventSystem did not activate its input module.");
            LogCanvasDiagnostics(view);
            GameObject nativeCharacter = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .FirstOrDefault(value => value.name == "CharacterPageNativeModel")?.gameObject;
            Require(nativeCharacter != null && nativeCharacter.GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer.enabled),
                "The official native character model is missing or has no visible renderer.");
            CharacterModelPreview characterPreview = view.GetComponentInChildren<CharacterModelPreview>(true);
            RectTransform characterPreviewRect = characterPreview == null
                ? null : characterPreview.GetComponent<RectTransform>();
            Require(characterPreviewRect != null && characterPreview.name == "MyPlayerModel_UI"
                && characterPreviewRect.rect.width >= 200f && characterPreviewRect.rect.height >= 330f,
                "The native model preview is not mounted on the official 207x337 MyPlayerModel_UI node.");
            Animator characterAnimator = nativeCharacter.GetComponent<Animator>();
            Require(characterAnimator != null && characterAnimator.runtimeAnimatorController != null
                && CurrentAnimationClipName(characterAnimator) == "home/idle_01",
                "The native character model did not enter its official home idle animation.");
            Debug.Log("[Character acceptance] profile level=" + progression.Player.Level
                + " profession=" + progression.Player.Wardrobe.Profession
                + " created=" + progression.Player.CharacterCreated
                + " name=" + progression.Player.CharacterName);
            Require(progression.Player.CharacterCreated && progression.Player.Wardrobe.Profession == 1,
                "The acceptance profile was not loaded.");
            Require(FindButton("CharacterPanelSkillBtn") != null, "The skill entry button is missing.");
            Button petEntryButton = FindButton("CharacterPanelPetBtn");
            Require(petEntryButton != null && petEntryButton.interactable,
                "The character pet entry button is missing or not interactable.");
            Transform petModel = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .FirstOrDefault(value => value.name.StartsWith("EquippedPet_", StringComparison.Ordinal));
            Require(petModel != null && petModel.GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer.enabled),
                "The official equipped pet model is missing or has no visible renderer.");
            Require(FindButton("CharacterPanelEquipBtn") != null || FindButton("CharacterPanelSkillBtn") != null,
                "The character panel buttons were not bound.");
            Image[] icons = UnityEngine.Object.FindObjectsOfType<Image>(true)
                .Where(image => image.name == "icon").ToArray();
            GameObject[] slotRoots = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                .Where(rect => rect.name == "WeaponEquipItem" || rect.name == "HelmetEquipItem")
                .Select(rect => rect.gameObject).ToArray();
            Debug.Log("[Character acceptance] slot roots=" + slotRoots.Length + " children="
                + string.Join(";", slotRoots.Take(5).Select(rect => rect.transform.childCount + ":" +
                    string.Join("/", Enumerable.Range(0, rect.transform.childCount)
                        .Select(i => rect.transform.GetChild(i).name)))));
            Image[] allImages = UnityEngine.Object.FindObjectsOfType<Image>(true);
            Debug.Log("[Character acceptance] icon-like=" + string.Join(",",
                allImages.Select(image => image.name).Where(name =>
                    name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0).Distinct().Take(10)));
            Debug.Log("[Character acceptance] icon images=" + icons.Length + " with sprite="
                + icons.Count(image => image.enabled && image.sprite != null)
                + " allImages=" + allImages.Length + " allWithSprite="
                + allImages.Count(image => image.sprite != null)
                + " equipped=" + progression.Player.Equipment.Equipped.Count
                + " instances=" + progression.Player.Equipment.Instances.Count
                + " names=" + string.Join(",", allImages.Take(12).Select(image => image.name)));
            if (icons.Length == 0)
                Debug.LogWarning("[Character acceptance] Equipped slot icons were not created in batchmode; "
                    + "visual icon checks need a windowed run.");

            LogCharacterVisualDiagnostics(view);
            yield return Capture("character-01-panel");
            if (Environment.GetEnvironmentVariable("ZJCS_CHARACTER_INTERACTIVE") == "1")
            {
                Debug.Log("[Character acceptance] interactive-ready; click the pet-management entry, then click the equipped pet's selector control and close it with ConfirmBtn before pressing F8.");
                SessionState.SetBool(InteractiveReadyKey, true);
                bool petEntryWasHit = false;
                bool petEntryWasClickedByMouse = false;
                bool petModuleWasRequested = false;
                bool selectorCloseWasHit = false;
                bool selectorCloseWasClickedByMouse = false;
                var pointerHits = new List<RaycastResult>();
                UnityEngine.Events.UnityAction petEntryClickObserver = () =>
                {
                    petEntryWasClickedByMouse = true;
                    Debug.Log("[Character acceptance] pet-management entry onClick position=" + Input.mousePosition);
                };
                petEntryButton.onClick.AddListener(petEntryClickObserver);
                Action<string> moduleRequestObserver = module =>
                {
                    if (module == "pets") petModuleWasRequested = true;
                };
                view.OfficialModuleRequested += moduleRequestObserver;
                while (!petEntryWasClickedByMouse || !petModuleWasRequested)
                {
                    if (Input.GetMouseButtonDown(0))
                        petEntryWasHit |= LogRealPointerHits("pet-entry", petEntryButton, pointerHits);
                    yield return null;
                }
                petEntryButton.onClick.RemoveListener(petEntryClickObserver);
                view.OfficialModuleRequested -= moduleRequestObserver;
                Require(petEntryWasHit && petEntryWasClickedByMouse && petModuleWasRequested,
                    "The official pet-management entry did not dispatch its module request from a mouse click.");
                Debug.Log("[Character acceptance] real-click-requested-pet-management");
                Button selectorEntryButton = view.transform.Find("CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EquipPetContent/RecommendBtn")
                    ?.GetComponent<Button>();
                Require(selectorEntryButton != null, "The equipped pet selector button is missing.");
                bool selectorEntryWasHit = false;
                bool selectorEntryWasClickedByMouse = false;
                UnityEngine.Events.UnityAction selectorEntryClickObserver = () =>
                    selectorEntryWasClickedByMouse = true;
                selectorEntryButton.onClick.AddListener(selectorEntryClickObserver);
                Debug.Log("[Character acceptance] interactive-ready; click the equipped pet selector control, close it with ConfirmBtn, then press F8.");
                while (!petView.IsSelectorOpen)
                {
                    if (Input.GetMouseButtonDown(0))
                        selectorEntryWasHit |= LogRealPointerHits("pet-selector-entry", selectorEntryButton, pointerHits);
                    yield return null;
                }
                selectorEntryButton.onClick.RemoveListener(selectorEntryClickObserver);
                Require(selectorEntryWasHit && selectorEntryWasClickedByMouse,
                    "The following-pet selector did not open from a mouse hit on the equipped pet control.");
                Debug.Log("[Character acceptance] real-click-opened-pet-selector");
                Button selectorCloseButton = FindButton("ConfirmBtn");
                if (selectorCloseButton != null)
                {
                    UnityEngine.Events.UnityAction selectorCloseClickObserver = () =>
                    {
                        selectorCloseWasClickedByMouse = true;
                        Debug.Log("[Character acceptance] pet selector close onClick position=" + Input.mousePosition);
                    };
                    selectorCloseButton.onClick.AddListener(selectorCloseClickObserver);
                    while (petView.IsSelectorOpen)
                    {
                        if (Input.GetMouseButtonDown(0))
                            selectorCloseWasHit |= LogRealPointerHits("pet-selector-close", selectorCloseButton, pointerHits);
                        yield return null;
                    }
                    selectorCloseButton.onClick.RemoveListener(selectorCloseClickObserver);
                }
                else
                {
                    while (petView.IsSelectorOpen) yield return null;
                }
                Require(selectorCloseWasHit && selectorCloseWasClickedByMouse,
                    "The pet selector did not close from a mouse hit and click on its close button.");
                Debug.Log("[Character acceptance] real-click-closed-pet-selector");
                Debug.Log("[Character acceptance] interactive-ready-continue; press F8 to continue.");
                while (!Input.GetKeyDown(KeyCode.F8)) yield return null;
                SessionState.SetBool(InteractiveReadyKey, false);
            }

            if (petView.IsSelectorOpen)
            {
                Button closePetSelector = FindButton("ConfirmBtn");
                if (closePetSelector != null) closePetSelector.onClick.Invoke();
            }
            Button petSelect = view.transform.Find("CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EquipPetContent/RecommendBtn")
                ?.GetComponent<Button>();
            Require(petSelect != null, "The equipped character pet selector button is missing.");
            petSelect.onClick.Invoke();
            yield return WaitFrames(2);
            Button noPet = FindButton("NoPet");
            Require(noPet != null, "The official pet selector has no empty candidate row.");
            noPet.onClick.Invoke();
            Require(progression.PetProgression.State.FollowPetIndex == -1,
                "The empty pet candidate did not clear the following pet.");
            Button closeSelector = FindButton("ConfirmBtn");
            Require(closeSelector != null, "The official pet selector close button is missing.");
            closeSelector.onClick.Invoke();
            yield return WaitFrames(2);
            Button addPet = view.transform.Find("CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EmptyPetContent/Contet/Add")
                ?.GetComponent<Button>();
            Require(addPet != null, "The empty character pet slot has no selector button.");
            addPet.onClick.Invoke();
            yield return WaitFrames(2);
            Button firstPetRow = FindButton("Pet_" + firstPet.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Require(firstPetRow != null, "The acquired official pet was not listed as a selector candidate.");
            firstPetRow.onClick.Invoke();
            Require(progression.PetProgression.State.FollowPetIndex == firstPet.Index,
                "Selecting an owned candidate did not save the following pet.");
            closeSelector = FindButton("ConfirmBtn");
            if (closeSelector != null) closeSelector.onClick.Invoke();
            yield return WaitFrames(2);
            Button skillButton = FindButton("CharacterPanelSkillBtn");
            skillButton.onClick.Invoke();
            yield return WaitFrames(3);
            yield return Capture("character-02-skills");
            Button nextPlan = FindButton("NextPlanBtn");
            Require(nextPlan != null, "The skill plan switch button is missing.");
            int beforePlan = progression.Player.Skills.SelectedPlan;
            nextPlan.onClick.Invoke();
            yield return WaitFrames(2);
            Require(progression.Player.Skills.SelectedPlan != beforePlan,
                "The skill plan was not switched by the real button.");

            int pieceId = progression.SkillDefinition(10000)?.PieceItemId ?? 0;
            long piecesBefore = progression.Player.Inventory.Where(value => value.ItemId == pieceId)
                .Sum(value => value.Count);
            ProgressionResult merged = progression.MergeSkill(10000);
            Require(merged.IsApplied && progression.SkillDefinition(10000) != null
                && progression.Player.Skills.Skills.Any(skill => skill.ClassId == 10000),
                "The skill piece merge did not add the skill: " + merged.Message);
            Require(progression.Player.Inventory.Where(value => value.ItemId == pieceId)
                .Sum(value => value.Count) < piecesBefore, "The skill piece merge did not consume pieces.");
            ProgressionResult equipped = progression.EquipSkill(10000, 0);
            Require(equipped.IsApplied, "The merged skill could not be installed: " + equipped.Message);
            ProgressionResult swapped = progression.SwapSkillSlots(100, 0, 1);
            Require(swapped.IsApplied || swapped.Status == ProgressionResultStatus.Unchanged,
                "The skill slot swap failed: " + swapped.Message);
            Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills[1] == 10000,
                "The skill slot swap did not move the installed skill.");
            int instanceCount = progression.Player.Equipment.Instances.Count;
            long piece701 = progression.Player.Inventory.Where(value => value.ItemId == 701)
                .Sum(value => value.Count);
            ProgressionResult pieceMerge = progression.MergeEquipmentPiece(701, 20027);
            Require(pieceMerge.IsApplied
                && progression.Player.Equipment.Instances.Count == instanceCount + 1
                && progression.Player.Inventory.Where(value => value.ItemId == 701)
                    .Sum(value => value.Count) < piece701,
                "The equipment piece merge did not create an instance: " + pieceMerge.Message);

            OfflineEquipmentInstance equippedItem = progression.Player.Equipment.Instances
                .FirstOrDefault(value => value.ItemId == 20000);
            if (equippedItem != null && equippedItem.Terms != null && equippedItem.Terms.Count > 0)
            {
                long costBefore = progression.Player.Inventory.Where(value => value.ItemId == 41302)
                    .Sum(value => value.Count);
                ProgressionResult purified = progression.PurifyEquipment(equippedItem.InstanceId,
                    Array.Empty<int>(), "character-acceptance-purify");
                Require(purified.IsApplied, "The equipment purify failed: " + purified.Message);
                Require(progression.Player.Inventory.Where(value => value.ItemId == 41302)
                    .Sum(value => value.Count) < costBefore, "The equipment purify did not consume materials.");
            }

            bool wardrobeNew = progression.IsWardrobeItemNew(39600);
            ProgressionResult seen = progression.MarkWardrobeItemSeen(39600);
            Require(seen.IsApplied && wardrobeNew && !progression.IsWardrobeItemNew(39600),
                "The wardrobe new-item mark did not clear.");

            ProgressionResult title = progression.WearTitle(39600, "character-acceptance-title");
            Require(title.IsApplied && progression.Player.Wardrobe.WornTitleId == 39600,
                "The title could not be worn through the character service.");
            view.Refresh();
            yield return WaitFrames(2);
            yield return Capture("character-03-title");

            Button wardrobeButton = FindButton("AppearanceBtn");
            Require(wardrobeButton != null, "The wardrobe entry button is missing.");
            wardrobeButton.onClick.Invoke();
            yield return WaitFrames(3);
            Animator wardrobeAnimator = CurrentCharacterAnimator();
            string wardrobeIdle = CurrentAnimationClipName(wardrobeAnimator);
            FieldInfo currentAnimationField = typeof(CharacterModelPreview).GetField("currentNativeAnimation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string requestedWardrobeIdle = currentAnimationField?.GetValue(characterPreview) as string ?? string.Empty;
            int wardrobeStateHash = wardrobeAnimator == null ? 0
                : wardrobeAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Require(requestedWardrobeIdle.EndsWith("/idle_01", StringComparison.Ordinal)
                && (requestedWardrobeIdle.StartsWith("100/", StringComparison.Ordinal)
                    || requestedWardrobeIdle.StartsWith("200/", StringComparison.Ordinal))
                && wardrobeAnimator != null
                && wardrobeStateHash == Animator.StringToHash("Base Layer." + requestedWardrobeIdle),
                "Entering the wardrobe did not play the profession idle animation: requested="
                    + requestedWardrobeIdle + ", clip=" + wardrobeIdle + ", stateHash=" + wardrobeStateHash);
            Button weaponTab = UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(button =>
                button.name == "AppearancePanelTabGroupItem_31"
                && TransformPath(button.transform).Contains("/WeaponTabGroup/TabGroup/", StringComparison.Ordinal));
            Require(weaponTab != null, "The official wardrobe weapon category tab is missing.");
            weaponTab.onClick.Invoke();
            yield return WaitFrames(60);
            Animator weaponAnimator = CurrentCharacterAnimator();
            string weaponIdle = CurrentAnimationClipName(weaponAnimator);
            string weaponAnimation = currentAnimationField?.GetValue(characterPreview) as string ?? string.Empty;
            int weaponStateHash = weaponAnimator == null ? 0
                : weaponAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Require(weaponAnimation.EndsWith("/show_01", StringComparison.Ordinal)
                && weaponAnimator != null
                && weaponStateHash == Animator.StringToHash("Base Layer." + weaponAnimation),
                "The official idle-to-01 wardrobe transition did not settle into its appearance loop: requested="
                    + weaponAnimation + ", clip=" + weaponIdle + ", stateHash=" + weaponStateHash);
            Button secondWeaponTab = UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(button =>
                button.name == "AppearancePanelTabGroupItem_31 (1)"
                && TransformPath(button.transform).Contains("/WeaponTabGroup/TabGroup/", StringComparison.Ordinal));
            Require(secondWeaponTab != null, "The official wardrobe second weapon category tab is missing.");
            secondWeaponTab.onClick.Invoke();
            yield return WaitFrames(60);
            Animator secondWeaponAnimator = CurrentCharacterAnimator();
            string secondWeaponIdle = CurrentAnimationClipName(secondWeaponAnimator);
            string secondWeaponAnimation = currentAnimationField?.GetValue(characterPreview) as string ?? string.Empty;
            int secondWeaponStateHash = secondWeaponAnimator == null ? 0
                : secondWeaponAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Require(secondWeaponAnimation.EndsWith("/show_02", StringComparison.Ordinal)
                && secondWeaponAnimator != null
                && secondWeaponStateHash == Animator.StringToHash("Base Layer." + secondWeaponAnimation),
                "The official 01-to-02 wardrobe transition did not settle into its appearance loop: requested="
                    + secondWeaponAnimation + ", clip=" + secondWeaponIdle + ", stateHash=" + secondWeaponStateHash);
            Button tryOn = FindButton("WardrobePreviewButton");
            Require(tryOn != null, "The appearance try-on button is missing.");
            int wornBeforePreview = progression.Player.Wardrobe.WornItems.Count;
            tryOn.onClick.Invoke();
            yield return WaitFrames(3);
            Require(UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                    .Any(rect => rect.name == "PreviewAppearanceDialog"),
                "The official appearance preview dialog did not open.");
            Require(progression.Player.Wardrobe.WornItems.Count == wornBeforePreview,
                "Appearance preview changed the saved wardrobe state.");
            yield return Capture("character-04-appearance-preview");
            Button closePreview = FindButton("ClickRaycast");
            Require(closePreview != null, "The official preview close target is missing.");
            closePreview.onClick.Invoke();
            yield return WaitFrames(2);
            Require(progression.Player.Wardrobe.WornItems.Count == wornBeforePreview,
                "Closing appearance preview changed the saved wardrobe state.");

            int switchedPlan = progression.Player.Skills.SelectedPlan;
            OfflineMainlineProgressSession reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(reloaded.Save.Player.Wardrobe.WornTitleId == 39600,
                "The worn title was not persisted to the profile save.");
            Require(reloaded.Save.Player.Skills.SelectedPlan == switchedPlan,
                "The switched skill plan was not persisted to the profile save.");
            Require(reloaded.Save.Player.Skills.Skills.Any(skill => skill.ClassId == 10000),
                "The merged skill was not persisted to the profile save.");
            Require(reloaded.Save.Player.Pets.Pets.Any(pet => pet.Index == firstPet.Index)
                && reloaded.Save.Player.Pets.FollowPetIndex == progression.Player.Pets.FollowPetIndex
                && reloaded.Save.Player.Skills.Plans[0].PetIndex == firstPet.Index
                && reloaded.Save.Player.Skills.Plans[1].PetIndex == firstPet.Index
                && reloaded.Save.Player.Skills.Plans[2].PetIndex == secondPet.Index,
                "Pet instances, following state or shared multi-plan assignments were not persisted.");
            Require(reloaded.Save.Player.Wardrobe.SeenItems.Contains(39600),
                "The wardrobe seen mark was not persisted to the profile save.");
            OfflineNpcFriendship giftedNpc = reloaded.Save.Player.CharacterProperties.NpcFriendships
                .FirstOrDefault(value => value.NpcClassId == 10608);
            Require(giftedNpc != null && giftedNpc.FriendshipExp == npcGiftExperience
                && giftedNpc.Level == progression.FriendshipLevel(10608, npcGiftExperience,
                    reloaded.Save.Player.SubRank),
                "The official NPC gift transaction was not restored from the profile save.");
            OfflineNpcFriendship metNpc = reloaded.Save.Player.CharacterProperties.NpcFriendships
                .FirstOrDefault(value => value.NpcClassId == 8401);
            Require(metNpc != null && metNpc.Level == 1,
                "The NPC meet transaction was not restored from the profile save.");
            Require(reloaded.Save.Player.Equipment.Instances.Count == progression.Player.Equipment.Instances.Count,
                "The merged equipment instance was not persisted to the profile save.");

            Debug.Log("[Character acceptance] Play Mode logic passed: panel built, skill panel opened, "
                + "skill merged from pieces, skill slots swapped, equipment merged and purified, wardrobe "
                + "seen marks, NPC meet and gift friendship, temporary appearance preview without save mutation, plan and title persisted. "
                + "Equip icon rendering and pointer input need a "
                + "windowed run.");
            Finish(true, null);
        }

        private static void ValidateNpcMeetMutation(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression)
        {
            const int npcClassId = 8401;
            Require(!progression.CanMeetNpc(npcClassId),
                "An NPC with an unmet item condition was available for recruitment.");
            ProgressionResult insufficient = progression.MeetNpc(npcClassId,
                "character-acceptance-npc-meet-insufficient");
            Require(insufficient.Status == ProgressionResultStatus.InvalidState
                && !progression.Player.CharacterProperties.NpcFriendships.Any(value =>
                    value.NpcClassId == npcClassId),
                "An NPC was recruited without satisfying its official meet condition.");

            IReadOnlyDictionary<int, long> requirements = progression.NpcMeetItemRequirements(npcClassId);
            long requiredCount = 0;
            bool hasRequirement = requirements.TryGetValue(1300, out requiredCount);
            Require(requirements.Count == 1 && hasRequirement && requiredCount == 3,
                "The official NPC meet-item requirement was not read from its HasItemCondition.");
            Dictionary<string, object> initialRow = progression.Catalog.Rows("npc_friendship_level")
                .FirstOrDefault(value => Convert.ToInt32(value["Id"],
                        System.Globalization.CultureInfo.InvariantCulture) == npcClassId
                    && Convert.ToInt32(value["Level"],
                        System.Globalization.CultureInfo.InvariantCulture) == 1);
            Require(initialRow != null,
                "The official initial NPC friendship experience was unavailable.");
            long initialExperience = Convert.ToInt64(initialRow["FriendshipExp"],
                System.Globalization.CultureInfo.InvariantCulture);
            long before = progression.Player.Inventory.Where(value => value.ItemId == 1300)
                .Sum(value => value.Count);
            ProgressionResult seeded = session.ApplyCharacterMutation(player =>
            {
                OfflineItemStack item = player.Inventory.FirstOrDefault(value => value.ItemId == 1300);
                if (item == null) player.Inventory.Add(new OfflineItemStack { ItemId = 1300, Count = requiredCount });
                else item.Count = checked(item.Count + requiredCount);
            });
            Require(seeded.IsApplied && progression.CanMeetNpc(npcClassId),
                "Satisfying the official meet-item condition did not enable NPC recruitment.");

            ProgressionResult met = progression.MeetNpc(npcClassId, "character-acceptance-npc-meet");
            Require(met.IsApplied, "The NPC meet transaction failed: " + met.Message);
            long after = progression.Player.Inventory.Where(value => value.ItemId == 1300)
                .Sum(value => value.Count);
            OfflineNpcFriendship friendship = progression.Player.CharacterProperties.NpcFriendships
                .FirstOrDefault(value => value.NpcClassId == npcClassId);
            Require(after == before && friendship != null && friendship.Level == 1
                && friendship.FriendshipExp == initialExperience,
                "NPC recruitment did not consume its official gate items and create the default friendship.");
            ProgressionResult replay = progression.MeetNpc(npcClassId, "character-acceptance-npc-meet");
            Require(replay.Status == ProgressionResultStatus.Unchanged
                && progression.Player.Inventory.Where(value => value.ItemId == 1300).Sum(value => value.Count) == after,
                "Replaying an NPC meet event consumed its gate items more than once.");
        }

        private static long ValidateNpcGiftMutation(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression)
        {
            const int npcClassId = 10608;
            Dictionary<string, object> npc = progression.Catalog.Find("npc", npcClassId, "Id");
            Require(npc != null && Convert.ToInt32(npc["NpcRoleType"],
                    System.Globalization.CultureInfo.InvariantCulture) == 2
                && npc["GiftPreferenceDict"] is Dictionary<string, object>,
                "The pinned Star God NPC gift profile is missing.");
            var preferences = (Dictionary<string, object>)npc["GiftPreferenceDict"];
            int giftItemId = preferences.Keys.Select(value => Convert.ToInt32(value,
                    System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault(itemId => progression.Catalog.Find("npc_gift", itemId) != null
                    && progression.Catalog.FindItem(itemId) != null);
            Require(giftItemId > 0, "The pinned NPC has no sendable preferred gift.");

            Dictionary<string, object> giftInfo = progression.Catalog.Find("npc_gift", giftItemId);
            long baseFriendship = Convert.ToInt64(giftInfo["Friendship"],
                System.Globalization.CultureInfo.InvariantCulture);
            double factor = Convert.ToDouble(preferences[giftItemId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)],
                System.Globalization.CultureInfo.InvariantCulture);
            long expectedExperience = checked((long)Math.Round((double)baseFriendship * factor));
            Dictionary<int, long> selected = new Dictionary<int, long> { [giftItemId] = 1 };
            long giftCountBefore = progression.Player.Inventory
                .Where(value => value.ItemId == giftItemId).Sum(value => value.Count);
            ProgressionResult seeded = session.ApplyCharacterMutation(player =>
            {
                if (player.CharacterProperties == null)
                    player.CharacterProperties = new OfflineCharacterPropertyProgress();
                OfflineCharacterPropertyProgress.Normalize(player.CharacterProperties);
                OfflineNpcFriendship friendship = player.CharacterProperties.NpcFriendships
                    .FirstOrDefault(value => value.NpcClassId == npcClassId);
                if (friendship == null)
                    player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship
                    {
                        NpcClassId = npcClassId
                    });
                else
                {
                    friendship.Level = 0;
                    friendship.FriendshipExp = 0;
                }

                OfflineItemStack gift = player.Inventory.FirstOrDefault(value => value.ItemId == giftItemId);
                if (gift == null)
                    player.Inventory.Add(new OfflineItemStack { ItemId = giftItemId, Count = 1 });
                else gift.Count = checked(gift.Count + 1);
            });
            Require(seeded.IsApplied, "The isolated NPC gift test state could not be saved.");
            Require(progression.NpcGiftCandidates(npcClassId).Contains(giftItemId),
                "The Star God NPC gift candidate filter excluded its preferred gift.");
            Require(progression.NpcGiftFriendshipExperience(npcClassId, selected) == expectedExperience,
                "NPC gift friendship does not match the official preference multiplier and rounding.");

            ProgressionResult given = progression.GiveNpcGifts(npcClassId, selected,
                "character-acceptance-npc-gift");
            Require(given.IsApplied, "The NPC gift transaction failed: " + given.Message);
            OfflineNpcFriendship savedFriendship = progression.Player.CharacterProperties.NpcFriendships
                .First(value => value.NpcClassId == npcClassId);
            long giftCountAfter = progression.Player.Inventory
                .Where(value => value.ItemId == giftItemId).Sum(value => value.Count);
            Require(savedFriendship.FriendshipExp == expectedExperience
                && savedFriendship.Level == progression.FriendshipLevel(npcClassId,
                    expectedExperience, progression.Player.SubRank)
                && giftCountAfter == giftCountBefore,
                "NPC gifting did not atomically consume the item and update official friendship state.");
            return expectedExperience;
        }

        private static void ValidateEquipmentCabinetMutation(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression, OfficialEquipmentCatalog equipment)
        {
            OfflinePlayerProgress current = progression.Player;
            var occupiedPositions = new System.Collections.Generic.HashSet<int>(
                current.Equipment.Equipped.Select(value => value.Position));
            var cabinetScoreKeys = new System.Collections.Generic.HashSet<long>();
            foreach (Dictionary<string, object> cabinetScoreRow in progression.Catalog.Rows("equip_cabinet_score"))
            {
                int rankLimit = Convert.ToInt32(cabinetScoreRow["RankLimit"],
                    System.Globalization.CultureInfo.InvariantCulture);
                if (!(cabinetScoreRow["Score"] is Dictionary<string, object> scores)) continue;
                foreach (string qualityKey in scores.Keys)
                    if (int.TryParse(qualityKey, out int quality))
                        cabinetScoreKeys.Add(((long)rankLimit << 32) | (uint)quality);
            }
            int itemId = 0;
            int position = 0;
            foreach (Dictionary<string, object> row in equipment.Rows("equip"))
            {
                int candidateId = Convert.ToInt32(row["ClassId"],
                    System.Globalization.CultureInfo.InvariantCulture);
                int candidatePosition = OfflineEquipmentOperations.PositionForItem(equipment, candidateId);
                if (occupiedPositions.Contains(candidatePosition)
                    || !equipment.CanWear(candidateId, current.Wardrobe.Profession)) continue;
                Dictionary<string, object> item = equipment.Find("item", candidateId);
                if (item == null) continue;
                int limitRank = Convert.ToInt32(item["LimitRank"],
                    System.Globalization.CultureInfo.InvariantCulture);
                int quality = Convert.ToInt32(item["Quality"],
                    System.Globalization.CultureInfo.InvariantCulture);
                bool hasScore = cabinetScoreKeys.Contains(((long)limitRank << 32) | (uint)quality);
                if (!hasScore) continue;
                itemId = candidateId;
                position = candidatePosition;
                break;
            }
            Require(itemId > 0 && position > 0,
                "Find a wearable official equipment item for an empty position and cabinet score row.");

            Dictionary<string, object> season = progression.Catalog.Rows("astrological_season_config")
                .OrderBy(value => Convert.ToInt32(value["Season"],
                    System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault();
            Require(season != null, "The official first equipment cabinet season is unavailable.");
            int seasonId = Convert.ToInt32(season["Season"],
                System.Globalization.CultureInfo.InvariantCulture);
            int seasonRank = Convert.ToInt32(season["Rank"],
                System.Globalization.CultureInfo.InvariantCulture);
            int seasonSubRank = progression.Catalog.Rows("player_subrank")
                .Select(value => Convert.ToInt32(value["SubRank"],
                    System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault(value => progression.Catalog.RankOfSubRank(value) == seasonRank);
            Require(seasonId > 0 && seasonRank > 0 && seasonSubRank > 0,
                "The official first season rank has no matching character subrank.");
            Dictionary<string, object> seasonCondition = season["Condition"] as Dictionary<string, object>;
            int requiredLevel = seasonCondition != null && seasonCondition.TryGetValue("Level", out object rawLevel)
                ? Convert.ToInt32(rawLevel, System.Globalization.CultureInfo.InvariantCulture) : 0;
            IReadOnlyList<long> cumulativeExperience = progression.Catalog.CumulativeLevelExperience;
            Require(requiredLevel > 0 && requiredLevel < cumulativeExperience.Count,
                "The official first season level condition is invalid.");

            int previousRank = current.Rank;
            int previousSubRank = current.SubRank;
            int previousSeasonRank = current.SeasonRank;
            int previousLevel = current.Level;
            int previousSeasonBaseLevel = current.SeasonBaseLevel;
            long previousExperience = current.Experience;
            int previousPropertySeason = current.CharacterProperties.SeasonId;
            ProgressionResult seasonReady = session.ApplyCharacterMutation(player =>
            {
                player.Rank = seasonRank;
                player.SubRank = seasonSubRank;
                player.SeasonRank = seasonId;
                player.Level = Math.Max(player.Level, requiredLevel);
                player.SeasonBaseLevel = player.Level;
                player.Experience = cumulativeExperience[Math.Min(player.Level,
                    cumulativeExperience.Count - 1)];
            }, "character-acceptance-equip-cabinet-season");
            Require(seasonReady.IsApplied,
                "Prepare the isolated profile for the official equipment cabinet season: "
                    + seasonReady.Status + " " + (seasonReady.Message ?? string.Empty));

            OfflinePlayerProgress seasonPlayer = progression.Player;
            long instanceId = seasonPlayer.Equipment.NextInstanceId;
            OfflineEquipmentInstance instance = OfficialEquipmentGenerator.Generate(equipment, itemId, instanceId,
                new EquipmentRollContext
                {
                    DropLevel = Math.Max(1, seasonPlayer.Level),
                    PlayerLevel = Math.Max(1, seasonPlayer.Level),
                    SubRank = seasonPlayer.SubRank
                }, () => 0.5f);
            ProgressionResult seeded = session.ApplyCharacterMutation(player =>
            {
                player.Equipment.Instances.Add(instance);
                player.Equipment.NextInstanceId = instanceId + 1;
            }, "character-acceptance-equip-cabinet-seed");
            Require(seeded.IsApplied, "Seed an official equipment instance for cabinet synchronization.");

            int beforeWear = progression.CurrentEquipCabinetScore();
            ProgressionResult worn = progression.WearEquipment(equipment, instanceId,
                "character-acceptance-equip-cabinet-wear");
            int afterWear = progression.CurrentEquipCabinetScore();
            OfflineEquipCabinetSeasonScore score = progression.Player.CharacterProperties.EquipCabinetScores
                .FirstOrDefault(value => value.SeasonId == seasonId);
            Require(worn.IsApplied && afterWear > beforeWear && score != null && score.Score == afterWear,
                "Wearing equipment must record the current season cabinet score.");

            ProgressionResult takenOff = progression.TakeOffEquipped(position,
                "character-acceptance-equip-cabinet-takeoff");
            int afterTakeOff = progression.CurrentEquipCabinetScore();
            OfflineEquipCabinetSeasonScore afterTakeOffScore = progression.Player.CharacterProperties.EquipCabinetScores
                .FirstOrDefault(value => value.SeasonId == seasonId);
            Require(takenOff.IsApplied && afterTakeOff == beforeWear
                && afterTakeOffScore != null && afterTakeOffScore.Score == afterTakeOff,
                "Taking off equipment must update the current season cabinet snapshot.");

            OfflineMainlineProgressSession reopened = OfflineMainlineProgressSession.OpenCharacter(
                new ProfileSlotCatalog(SessionState.GetString(RootKey, string.Empty)),
                SessionState.GetString(SlotKey, string.Empty));
            OfflineEquipCabinetSeasonScore persistedScore = reopened.Save.Player.CharacterProperties.EquipCabinetScores
                .FirstOrDefault(value => value.SeasonId == seasonId);
            Require(persistedScore != null && persistedScore.Score == afterTakeOff,
                "The current season equipment cabinet score did not survive profile reload.");
            ProgressionResult restored = session.ApplyCharacterMutation(player =>
            {
                player.Rank = previousRank;
                player.SubRank = previousSubRank;
                player.SeasonRank = previousSeasonRank;
                player.Level = previousLevel;
                player.SeasonBaseLevel = previousSeasonBaseLevel;
                player.Experience = previousExperience;
                player.CharacterProperties.SeasonId = previousPropertySeason;
            }, "character-acceptance-equip-cabinet-restore");
            Require(restored.IsApplied, "Restore the isolated profile after the cabinet season test.");
        }

        private static void ValidateLegionSkillMutation(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression)
        {
            Dictionary<string, object> definition = progression.Catalog.Find("legion_map_skill", 1011,
                "SkillClassId");
            Require(definition != null && progression.LegionMapSkillLevel(1011) == 0,
                "The official first guild skill is unavailable or already learned in the isolated profile.");
            Require(progression.LegionLibraryLevel == 1,
                "A new character must start with a level 1 guild library.");
            ProgressionResult lockedByLibrary = progression.UpgradeLegionMapSkill(1041,
                "character-acceptance-legion-skill-library-gate");
            ProgressionResult lockedByPrerequisite = progression.UpgradeLegionMapSkill(1021,
                "character-acceptance-legion-skill-prerequisite-gate");
            Require(lockedByLibrary.Status == ProgressionResultStatus.InvalidState
                && lockedByPrerequisite.Status == ProgressionResultStatus.InvalidState
                && progression.LegionMapSkillLevel(1011) == 0
                && progression.LegionMapSkillLevel(1021) == 0,
                "Guild skill upgrades bypassed the official library or prerequisite gate.");
            IReadOnlyDictionary<int, long> cost = progression.LegionMapSkillUpgradeCost(1011);
            Require(cost.Count > 0, "The first official guild skill level has no material cost.");
            ProgressionResult seeded = session.ApplyCharacterMutation(player =>
            {
                foreach (KeyValuePair<int, long> item in cost)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == item.Key);
                    if (stack == null)
                        player.Inventory.Add(stack = new OfflineItemStack { ItemId = item.Key });
                    stack.Count = checked(stack.Count + item.Value);
                }
            }, "character-acceptance-legion-skill-seed");
            Require(seeded.IsApplied, "Seed official guild skill materials in the isolated profile.");

            ProgressionResult upgraded = progression.UpgradeLegionMapSkill(1011,
                "character-acceptance-legion-skill-upgrade");
            Require(upgraded.IsApplied && progression.LegionMapSkillLevel(1011) == 1,
                "The official guild skill upgrade did not persist its new level.");
            foreach (KeyValuePair<int, long> item in cost)
                Require(session.Save.Player.Inventory.First(value => value.ItemId == item.Key).Count == 0,
                    "The official guild skill upgrade did not consume its configured materials.");
            ProgressionResult replay = progression.UpgradeLegionMapSkill(1011,
                "character-acceptance-legion-skill-upgrade");
            Require(replay.Status == ProgressionResultStatus.Unchanged
                && progression.LegionMapSkillLevel(1011) == 1,
                "Replaying a guild skill upgrade event changed the saved level or materials.");
            OfflineMainlineProgressSession reopened = OfflineMainlineProgressSession.OpenCharacter(
                new ProfileSlotCatalog(SessionState.GetString(RootKey, string.Empty)),
                SessionState.GetString(SlotKey, string.Empty));
            Require(reopened.Save.Player.CharacterProperties.LegionLibraryLevel == 1,
                "The starting guild library level did not survive profile reload.");
        }

        private static void ValidateLegionBuildingMutation(OfficialCharacterCatalog catalog)
        {
            string root = Path.Combine(SessionState.GetString(RootKey, string.Empty), "legion-building");
            Directory.CreateDirectory(root);
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave
            {
                CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "建筑挂机验收";
            save.Player.Level = 1;
            save.Player.Rank = 1;
            save.Player.SubRank = 1;
            save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));

            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, catalog);
            OfflineLegionBuildingProgressionService buildings = progression.LegionBuildings;
            Require(buildings.Level(OfflineLegionBuildingProgressionService.HallBuildingId) == 1
                && buildings.Level(OfflineLegionBuildingProgressionService.LibraryBuildingId) == 1
                && Enumerable.Range(1, 12).Where(id => id != 1 && id != 4)
                    .All(id => buildings.Level(id) == 0),
                "A new profile did not start with the confirmed guild building levels.");
            foreach (int buildingId in new[] { 2, 3, 5, 6, 7 })
                Require(buildings.OperationCost(buildingId).Count > 0,
                    "A building allowed at Hall level 1 was incorrectly locked: " + buildingId);
            foreach (int buildingId in new[] { 8, 9, 10, 12 })
                Require(buildings.OperationCost(buildingId).Count == 0,
                    "A building locked by an official Hall or composite condition was available: " + buildingId);
            Require(buildings.OperationCost(1).Count == 0 && buildings.OperationCost(4).Count == 0,
                "The Hall rank or Library Hall-level condition was bypassed.");
            IReadOnlyDictionary<int, long> cost = buildings.OperationCost(2);
            Require(cost.Count > 0 && cost.All(value => value.Key > 0 && value.Value > 0),
                "The official building construction cost was not read from its level row.");

            ProgressionResult shortSeed = session.ApplyCharacterMutation(player =>
            {
                foreach (KeyValuePair<int, long> item in cost)
                {
                    OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == item.Key);
                    if (stack == null)
                        player.Inventory.Add(stack = new OfflineItemStack { ItemId = item.Key });
                    stack.Count = item.Value - 1;
                }
            }, "character-acceptance-building-short-seed");
            Require(shortSeed.IsApplied && !buildings.CanStartOperation(2, out _),
                "A building construction started without all official materials.");

            ProgressionResult fullSeed = session.ApplyCharacterMutation(player =>
            {
                foreach (KeyValuePair<int, long> item in cost)
                    player.Inventory.First(value => value.ItemId == item.Key).Count = item.Value;
            }, "character-acceptance-building-full-seed");
            Require(fullSeed.IsApplied && buildings.CanStartOperation(2, out _),
                "The official construction materials did not unlock building construction.");

            const long startTime = 2000000000L;
            ProgressionResult started = buildings.StartOperation(2, startTime,
                "character-acceptance-building-start");
            Require(started.IsApplied && buildings.ActiveOperation != null
                && buildings.ActiveOperation.TargetLevel == 1,
                "The official construction HangUp operation did not start.");
            foreach (KeyValuePair<int, long> item in cost)
                Require(session.Save.Player.Inventory.First(value => value.ItemId == item.Key).Count == 0,
                    "Starting the construction HangUp did not consume its official materials.");

            int period = buildings.SettlePeriodSeconds;
            int speed = buildings.HangUpSpeedPerHour(2);
            OfflineLegionBuildingUpgrade active = buildings.ActiveOperation;
            Require(period > 0 && speed > 0 && active.RequiredProgress > 0,
                "The official building HangUp rate or period is unavailable.");
            ProgressionResult early = buildings.Settle(startTime + period - 1,
                "character-acceptance-building-early");
            Require(early.Status == ProgressionResultStatus.Unchanged
                && buildings.ActiveOperation.Progress == 0,
                "Building HangUp progress advanced before a complete official settlement period.");

            long periodProduct = checked((long)speed * period);
            long periodsToComplete = checked(active.RequiredProgress * 3600L + periodProduct - 1)
                / periodProduct;
            ProgressionResult firstTick = buildings.Settle(startTime + period,
                "character-acceptance-building-first-tick");
            Require(firstTick.IsApplied, "The first complete building HangUp period was not saved.");
            if (periodsToComplete > 1)
            {
                OfflineLegionBuildingUpgrade afterFirstTick = buildings.ActiveOperation;
                long firstNumerator = checked((long)speed * period);
                Require(afterFirstTick != null
                    && afterFirstTick.Progress == firstNumerator / 3600L
                    && afterFirstTick.ProgressRemainder == firstNumerator % 3600L,
                    "Building HangUp progress did not use the official hourly rate and fractional remainder.");
                session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                progression = new OfflineCharacterProgressionService(session, catalog);
                buildings = progression.LegionBuildings;
                Require(buildings.Level(2) == 0 && buildings.ActiveOperation != null
                    && buildings.ActiveOperation.Progress == afterFirstTick.Progress,
                    "The active building HangUp operation did not survive profile reload.");
            }
            else
            {
                Require(buildings.Level(2) == 1 && buildings.ActiveOperation == null,
                    "A one-period building construction did not complete at its configured threshold.");
            }

            if (periodsToComplete > 1)
            {
                ProgressionResult completed = buildings.Settle(startTime + periodsToComplete * period,
                    "character-acceptance-building-complete");
                Require(completed.IsApplied && buildings.Level(2) == 1
                    && buildings.ActiveOperation == null,
                    "The building was not completed after the required official HangUp periods.");
            }
            OfflineMainlineProgressSession reopened = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            OfflineCharacterPropertyProgress saved = reopened.Save.Player.CharacterProperties;
            Require(saved.LegionBuildingLevels.Any(value => value.BuildingId == 2 && value.Level == 1)
                && saved.LegionBuildingLevels.Count == 12
                && new[] { 3, 5, 6, 7, 8, 9, 10, 11, 12 }
                    .All(id => saved.LegionBuildingLevels.Any(value => value.BuildingId == id && value.Level == 0))
                && saved.LegionBuildingUpgrade == null,
                "The building levels did not survive profile reload.");
        }

        private static Button FindButton(string name)
        {
            Button[] matches = UnityEngine.Object.FindObjectsOfType<Button>(true)
                .Where(button => button != null && button.name == name).ToArray();
            return matches.FirstOrDefault(button => button.gameObject.activeInHierarchy) ?? matches.FirstOrDefault();
        }

        private static bool LogRealPointerHits(string stage, Button expected, List<RaycastResult> hits)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            hits.Clear();
            var pointer = new PointerEventData(eventSystem) { position = Input.mousePosition };
            eventSystem.RaycastAll(pointer, hits);
            bool expectedHit = expected != null && hits.Any(hit => hit.gameObject != null
                && (hit.gameObject.transform == expected.transform
                    || hit.gameObject.transform.IsChildOf(expected.transform)));
            string hitPaths = string.Join(";", hits.Take(8).Select(hit => hit.gameObject == null
                ? "<null>" : GetTransformPath(hit.gameObject.transform)));
            Debug.Log("[Character acceptance] real pointer " + stage + " position=" + Input.mousePosition
                + " expected=" + (expected == null ? "<none>" : GetTransformPath(expected.transform))
                + " expectedHit=" + expectedHit + " raycasts=" + hitPaths);
            return expectedHit;
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null) return string.Empty;
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return "/" + path;
        }

        private static string CurrentAnimationClipName(Animator animator)
        {
            return animator == null ? string.Empty : animator.GetCurrentAnimatorClipInfo(0)
                .Select(info => info.clip == null ? string.Empty : info.clip.name)
                .FirstOrDefault(name => !string.IsNullOrEmpty(name)) ?? string.Empty;
        }

        private static Animator CurrentCharacterAnimator()
        {
            return UnityEngine.Object.FindObjectsOfType<Animator>(true)
                .FirstOrDefault(animator => animator.gameObject.name == "CharacterPageNativeModel");
        }

        private static string TransformPath(Transform target)
        {
            var names = new List<string>();
            while (target != null)
            {
                names.Add(target.name);
                target = target.parent;
            }
            names.Reverse();
            return "/" + string.Join("/", names);
        }

        private static void LogCharacterVisualDiagnostics(OfficialCharacterPanelView view)
        {
            if (view == null) return;
            Debug.Log("[Character acceptance] visual screen=" + Screen.width + "x" + Screen.height);
            Image[] visibleImages = view.GetComponentsInChildren<Image>(true)
                .Where(image => image != null && image.enabled && image.gameObject.activeInHierarchy
                    && image.color.a > 0.01f && image.rectTransform != null
                    && image.rectTransform.rect.width * Mathf.Abs(image.rectTransform.lossyScale.x) >= 32f
                    && image.rectTransform.rect.height * Mathf.Abs(image.rectTransform.lossyScale.y) >= 32f)
                .OrderByDescending(image => image.rectTransform.rect.width * image.rectTransform.lossyScale.x
                    * image.rectTransform.rect.height * image.rectTransform.lossyScale.y)
                .Take(80).ToArray();
            foreach (Image image in visibleImages)
            {
                RectTransform rect = image.rectTransform;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                Debug.Log("[Character acceptance] visual image path=" + TransformPath(image.transform)
                    + " sprite=" + (image.sprite == null ? "<null>" : image.sprite.name)
                    + " texture=" + (image.mainTexture == null ? "<null>" : image.mainTexture.name)
                    + " rect=" + rect.rect.size + " screenRect=" + bottomLeft + ".." + topRight
                    + " color=" + image.color + " type=" + image.type);
            }

            RawImage[] visibleRawImages = view.GetComponentsInChildren<RawImage>(true)
                .Where(image => image != null && image.enabled && image.gameObject.activeInHierarchy
                    && image.color.a > 0.01f && image.rectTransform != null
                    && image.rectTransform.rect.width * Mathf.Abs(image.rectTransform.lossyScale.x) >= 32f
                    && image.rectTransform.rect.height * Mathf.Abs(image.rectTransform.lossyScale.y) >= 32f)
                .Take(40).ToArray();
            foreach (RawImage image in visibleRawImages)
                Debug.Log("[Character acceptance] visual raw path=" + TransformPath(image.transform)
                    + " texture=" + (image.texture == null ? "<null>" : image.texture.name)
                    + " rect=" + image.rectTransform.rect.size
                    + " scale=" + image.rectTransform.lossyScale);

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo nativeModelField = typeof(CharacterModelPreview).GetField("nativeModel", flags);
            FieldInfo nativeCameraField = typeof(CharacterModelPreview).GetField("nativeCamera", flags);
            foreach (CharacterModelPreview preview in view.GetComponentsInChildren<CharacterModelPreview>(true))
            {
                GameObject model = nativeModelField?.GetValue(preview) as GameObject;
                Camera camera = nativeCameraField?.GetValue(preview) as Camera;
                if (model == null)
                {
                    Debug.Log("[Character acceptance] visual model preview=" + TransformPath(preview.transform)
                        + " native model is not initialized");
                    continue;
                }
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(false)
                    .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .ToArray();
                bool hasBounds = false;
                Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (Renderer renderer in renderers)
                {
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Debug.Log("[Character acceptance] visual model path=" + TransformPath(model.transform)
                    + " preview=" + TransformPath(preview.transform)
                    + " rect=" + preview.GetComponent<RectTransform>()?.rect.size
                    + " camera=" + (camera == null ? "<null>" : camera.name)
                    + " cameraSize=" + (camera == null ? 0f : camera.orthographicSize)
                    + " aspect=" + (camera == null ? 0f : camera.aspect)
                    + " cameraPos=" + (camera == null ? Vector3.zero : camera.transform.position)
                    + " rendererCount=" + renderers.Length + " bounds=" + (hasBounds ? bounds.ToString() : "<none>"));
                foreach (Renderer renderer in renderers.Take(40))
                    Debug.Log("[Character acceptance] visual renderer=" + TransformPath(renderer.transform)
                        + " bounds=" + renderer.bounds);
            }

            foreach (string nodeName in new[] { "Add", "EmptyPetContent", "EquipPetContent", "RecommendBtn", "MyPlayerModel_UI" })
            {
                string nodePath = "CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/";
                if (nodeName == "Add")
                    nodePath += "PetSelectContent/EmptyPetContent/Contet/Add";
                else if (nodeName == "EmptyPetContent")
                    nodePath += "PetSelectContent/EmptyPetContent";
                else if (nodeName == "EquipPetContent")
                    nodePath += "PetSelectContent/EquipPetContent";
                else if (nodeName == "RecommendBtn")
                    nodePath += "PetSelectContent/EquipPetContent/RecommendBtn";
                else
                    nodePath += "PlayerModelRoot/MyPlayerModel_UI";
                Transform target = view.transform.Find(nodePath);
                if (target == null)
                    target = view.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(transform => transform.name == nodeName);
                RectTransform rect = target as RectTransform;
                if (rect == null) continue;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                Vector2 screenMin = Vector2.Min(bottomLeft, topRight);
                Vector2 screenMax = Vector2.Max(bottomLeft, topRight);
                Button button = target.GetComponent<Button>();
                Debug.Log("[Character acceptance] visual target path=" + TransformPath(target)
                    + " active=" + target.gameObject.activeInHierarchy + " rect=" + rect.rect.size
                    + " screenRect=" + screenMin + ".." + screenMax
                    + " button=" + (button == null ? "<none>" : "enabled=" + button.enabled));
            }
        }

        private static void LogCanvasDiagnostics(OfficialCharacterPanelView view)
        {
            foreach (Camera camera in UnityEngine.Object.FindObjectsOfType<Camera>(true))
                Debug.Log("[Character acceptance] camera path=" + TransformPath(camera.transform)
                    + " objectActive=" + camera.gameObject.activeInHierarchy + " enabled=" + camera.enabled
                    + " depth=" + camera.depth + " display=" + camera.targetDisplay
                    + " mask=" + camera.cullingMask + " clear=" + camera.clearFlags
                    + " target=" + (camera.targetTexture == null ? "<screen>" : camera.targetTexture.name));

            foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
                Debug.Log("[Character acceptance] canvas path=" + TransformPath(canvas.transform)
                    + " active=" + canvas.isActiveAndEnabled + " root=" + canvas.isRootCanvas
                    + " mode=" + canvas.renderMode + " display=" + canvas.targetDisplay
                    + " sorting=" + canvas.sortingLayerName + ":" + canvas.sortingOrder
                    + " pixelRect=" + canvas.pixelRect + " scale=" + canvas.scaleFactor);

            Graphic[] graphics = view.GetComponentsInChildren<Graphic>(true);
            Graphic[] active = graphics.Where(graphic => graphic != null && graphic.isActiveAndEnabled).ToArray();
            int drawable = active.Count(graphic => !graphic.canvasRenderer.cull
                && graphic.canvasRenderer.GetAlpha() > 0.001f);
            Debug.Log("[Character acceptance] canvas graphics total=" + graphics.Length
                + " active=" + active.Length + " drawable=" + drawable
                + " raycaster=" + (view.GetComponent<GraphicRaycaster>()?.isActiveAndEnabled ?? false));
            foreach (CanvasGroup group in view.GetComponentsInChildren<CanvasGroup>(true))
                Debug.Log("[Character acceptance] canvas group path=" + TransformPath(group.transform)
                    + " active=" + group.isActiveAndEnabled + " alpha=" + group.alpha
                    + " interactable=" + group.interactable + " blocks=" + group.blocksRaycasts);
            foreach (Graphic graphic in active.Take(16))
            {
                RectTransform rect = graphic.rectTransform;
                Debug.Log("[Character acceptance] canvas graphic=" + TransformPath(graphic.transform)
                    + " type=" + graphic.GetType().Name + " enabled=" + graphic.isActiveAndEnabled
                    + " cull=" + graphic.canvasRenderer.cull
                    + " alpha=" + graphic.canvasRenderer.GetAlpha()
                    + " color=" + graphic.color
                    + " rect=" + rect.rect.size
                    + " texture=" + (graphic.mainTexture == null ? "<null>" : graphic.mainTexture.name));
            }
        }

        private static IEnumerator Capture(string name)
        {
            if (Application.isBatchMode) yield break;
            string directory = Path.Combine(SessionState.GetString(RootKey, string.Empty), "captures");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 120 && !File.Exists(path); i++) yield return null;
            Debug.Log("[Character acceptance] snapshot " + path + " exists=" + File.Exists(path));
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (error != null) Debug.LogException(error);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
