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
using TMPro;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    /// <summary>
    /// Drives one real Map10 battle in Play Mode: starts the battle through the runtime's own
    /// entity-stage entry, then checks the official HUD, skill icons, timeline, speed controls and
    /// rule execution (sequence, damage serial). The temporary profile is deleted on exit.
    /// </summary>
    public static class OfficialMap10BattlePlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Map10.Battle.Acceptance.Active";
        private const string RootKey = "Zjcs.Map10.Battle.Acceptance.Root";
        private const string SlotKey = "Zjcs.Map10.Battle.Acceptance.Slot";
        private const string ResultKey = "Zjcs.Map10.Battle.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.Map10.Battle.Acceptance.Deadline";
        private const string PortraitOnlyKey = "Zjcs.Map10.Battle.Acceptance.PortraitOnly";
        private const string RealInputKey = "Zjcs.Map10.Battle.Acceptance.RealInput";
        private static bool started;
        private static bool enteredPlayMode;
        private static Exception pendingRuntimeError;

        [MenuItem("Zjcs/Validate/Map10 Battle Play Mode")]
        public static void Run() => Start(false);

        [MenuItem("Zjcs/Validate/Map10 Battle Portrait Play Mode")]
        public static void RunPortraitOnly() => Start(true);

        [MenuItem("Zjcs/Validate/Map10 Battle Real Input Play Mode")]
        public static void RunRealInput() => Start(false, true);

        public static void RunLeave()
        {
            ProjectTools.Validate();
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_TEST_LEAVE", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT",
                Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
            RunRealInput();
        }

        public static void RunSharedReport()
        {
            ProjectTools.Validate();
            RunRealInput();
        }

        private static void Start(bool portraitOnly, bool realInput = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the battle acceptance.");

            // Renderer.isVisible needs a rendered Game view before the first scene assertion.
            if (realInput) Combat.BattleGameViewInput.Focus();

            AssetDatabase.ImportAsset("Assets/Game/Resources/Combat/skill-playables.json",
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Require(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Resources/Combat/skill-playables.json") != null,
                "The official skill Playable JSON did not import as a TextAsset.");
            foreach (string name in new[] { "battle-hud-graphic-bindings", "battle-hud-graphic-bindings-source-manifest",
                         "battle-scene-bindings", "battle-scene-bindings-source-manifest" })
            {
                string path = "Assets/Game/Resources/Combat/" + name + ".json";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Require(AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null, "The official HUD binding did not import: " + path);
            }

            string validationRoot = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(validationRoot))
                throw new InvalidOperationException("Set ZJCS_VALIDATION_DIR to a session created by validation_workspace.ps1.");
            string root = Path.Combine(validationRoot, "map10-battle-profile-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                var creation = CharacterCreationOfflineService.OpenDefault(root);
                string slot = creation.CreateSlot();
                var draft = creation.OpenDraft(slot);
                draft.name = "战斗测试";
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_BODY") == "female") draft.body_type = "female";
                Require(creation.TryCreateCharacter(slot, draft, out string creationError), creationError);
                Require(creation.TrySetInitialWeapon(slot, 201, out string weaponError), weaponError);
                var save = OfflineMainlineProgressSession.OpenCharacter(slots, slot).Save;
                save.Player.Level = 30;
                save.Player.Rank = 1;
                save.Player.SubRank = 3;
                save.Player.SeasonRank = 1;
                save.Player.Wardrobe.Profession = 1;
                if (!save.Player.Skills.Skills.Any(value => value.ClassId == 10011))
                    save.Player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill { ClassId = 10011, Rank = 1 });
                save.Player.Skills.Plans[0].ActiveSkills[0] = 10011;
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_SLOT_BLESSING") == "1")
                {
                    save.Player.SubRank = 10;
                    save.Player.Rank = save.Player.SeasonRank = 4;
                    save.Player.HasActiveSeasonRecord = true;
                    save.Player.Skills.ActiveSlotLevels[0] = 10;
                    save.Player.Skills.PassiveSlotLevels[0] = 12;
                    save.Player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill { ClassId = 10128, Rank = 6 });
                    save.Player.Skills.Plans[0].PassiveSkills[0] = 10128;
                    save.Player.Skills.BlessRecords.Add(new Zjcs.Offline.Skills.OfflineSkillBlessRecord
                    {
                        SeasonId = 4, ActiveSlotLevels = new[] { 3, 0, 0, 0 },
                        PassiveSlotLevels = new[] { 5, 0, 0, 0 }
                    });
                    save.Player.Skills.BlessRecords.Add(new Zjcs.Offline.Skills.OfflineSkillBlessRecord
                    {
                        SeasonId = 5, ActiveSlotLevels = new[] { 99, 0, 0, 0 },
                        PassiveSlotLevels = new[] { 99, 0, 0, 0 }
                    });
                }
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
                File.WriteAllText(Path.Combine(slots.GetSlotDirectory(slot), "map-10-save.json"),
                    "{\"schema\":1,\"slotId\":\"" + slot + "\",\"gridX\":24,\"gridY\":23,"
                    + "\"firstBattleIntroShown\":true,\"completedStories\":[\"FirstEnterMap\"]}");

                SessionState.SetString(RootKey, root);
                SessionState.SetString(SlotKey, slot);
                SessionState.SetInt(ResultKey, 2);
                SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup
                    + (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_LEAVE") == "1" ? 600f : 240f));
                SessionState.SetBool(PortraitOnlyKey, portraitOnly);
                SessionState.SetBool(RealInputKey, realInput);
                SessionState.SetBool(ActiveKey, true);
                started = false;
                pendingRuntimeError = null;
                HookEditorEvents();
                EditorApplication.isPlaying = true;
            }
            catch
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                throw;
            }
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (SessionState.GetBool(ActiveKey, false)) HookEditorEvents();
        }

        private static void HookEditorEvents()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                enteredPlayMode = true;
                Application.logMessageReceived += OnRuntimeLog;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;

            Application.logMessageReceived -= OnRuntimeLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            string root = SessionState.GetString(RootKey, string.Empty);
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            if (Directory.Exists(root)) Directory.Delete(root, true);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(PortraitOnlyKey, false);
            if (Application.isBatchMode || Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION") == "1")
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying
                || !enteredPlayMode) return;
            if (pendingRuntimeError != null)
            {
                Exception error = pendingRuntimeError;
                pendingRuntimeError = null;
                Finish(false, error);
                return;
            }
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Map10 battle acceptance exceeded its overall deadline."));
                return;
            }
            if (started) return;

            started = true;
            try
            {
                // This test owns its Play Mode scene. Remove the normal character-creation
                // bootstrap and its canvases so they cannot intercept the battle's real input.
                foreach (Transform root in UnityEngine.Object.FindObjectsOfType<Transform>()
                    .Where(value => value.parent == null).ToArray())
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                Application.runInBackground = true;
                Canvas canvas = CreateCanvas();
                canvas.gameObject.AddComponent<AudioListener>();
                GameObject avatar = Zjcs.Presentation.CharacterCreation.CharacterCreationNativeModelFactory.CreateWorldModel(
                    SessionState.GetString(SlotKey, string.Empty), SessionState.GetString(RootKey, string.Empty), out _);
                var runtimeObject = new GameObject("Map10BattleRuntime");
                OfficialMainlineMapRuntime runtime = runtimeObject.AddComponent<OfficialMainlineMapRuntime>();
                runtime.Begin(SessionState.GetString(SlotKey, string.Empty), SessionState.GetString(RootKey, string.Empty),
                    canvas.transform, avatar, "Map10 battle acceptance", null);

                var runner = new GameObject("Map10BattleAcceptanceRunner");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<AcceptanceRunner>().StartCoroutine(VerifyBattle(runtime));
            }
            catch (Exception error)
            {
                Finish(false, error);
            }
        }

        // Exercise the same interface as the mode adapters; no executor or playback internals.
        private static void VerifyCommonBattleInterface(
            Zjcs.Presentation.Combat.OfficialBattleRuntimeController controller)
        {
            var checkpoint = controller.Checkpoint;
            int counter = checkpoint.Counter;
            foreach (float delta in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                bool rejected = false;
                try { controller.Tick(delta); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected && controller.Checkpoint == checkpoint && checkpoint.Counter == counter,
                    "Invalid frame time altered the active common battle session.");
            }
            bool duplicateRejected = false;
            try { controller.Begin(new Zjcs.Presentation.Combat.OfficialBattleRequest()); }
            catch (InvalidOperationException) { duplicateRejected = true; }
            Require(duplicateRejected && controller.Checkpoint == checkpoint && checkpoint.Counter == counter,
                "A duplicate Begin replaced the active common battle session.");
        }

        private static IEnumerator VerifyBattle(OfficialMainlineMapRuntime runtime)
        {
            yield return null;
            yield return null;

            CharacterNativePortraitValidation.Validate(
                SessionState.GetString(SlotKey, string.Empty), SessionState.GetString(RootKey, string.Empty));

            FieldInfo entitiesField = typeof(OfficialMainlineMapRuntime)
                .GetField("mapEntities", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo startStage = typeof(OfficialMainlineMapRuntime)
                .GetMethod("StartEntityStage", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo controllerField = typeof(OfficialMainlineMapRuntime)
                .GetField("battleController", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(entitiesField != null && startStage != null && controllerField != null,
                "Map10 battle entry points are missing.");

            var entities = ((System.Collections.IDictionary)entitiesField.GetValue(runtime));
            Require(entities != null && entities.Count > 0, "Map10 did not instantiate its entities.");
            GameObject worldTerrain = GameObject.Find("Map10Terrain");
            Require(worldTerrain != null, "The map terrain is missing before battle entry.");
            float worldCameraSize = Camera.main.orthographicSize;
            var worldUi = (OfficialWorldUiRenderer)OfficialMainlineMapInspection.Get(runtime, "ui");
            var worldHudStates = worldUi.Find("WorldCanvas", "/WorldCanvas").transform.Cast<Transform>()
                .ToDictionary(value => value.gameObject, value => value.gameObject.activeSelf);
            Animator playerAnimator = UnityEngine.Object.FindObjectsOfType<Animator>()
                .Single(value => value.name.StartsWith("Map10Player_", StringComparison.Ordinal));
            Transform worldModelParent = playerAnimator.transform.parent;
            Vector3 worldModelScale = playerAnimator.transform.localScale;
            int worldAnimationState = playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            object controller = null;
            foreach (object entity in entities.Values)
            {
                FieldInfo stageIds = entity.GetType().GetField("StageIds");
                int[] ids = stageIds == null ? null : (int[])stageIds.GetValue(entity);
                if (ids == null || !ids.Any(value => value > 0)) continue;
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_LEAVE") == "1")
                {
                    var resolveStage = typeof(OfficialMainlineMapRuntime).GetMethod("ResolveEntityStageId",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    int resolvedId = (int)resolveStage.Invoke(runtime, new[] { entity });
                    var stageRows = (System.Collections.IDictionary)OfficialMainlineMapInspection.Get(runtime, "stages");
                    var leaveStage = stageRows[resolvedId] as Map10RuntimeStage;
                    // Tutorial 1001 intentionally has no leave button in either phase.
                    if (leaveStage == null || !leaveStage.LeaveInPlaying) continue;
                    Debug.Log("Map leave fixture uses official stage " + resolvedId);
                }
                startStage.Invoke(runtime, new[] { entity, null });
                yield return null;
                GameObject invitation = GameObject.Find("BattleAssistSelection");
                if (invitation != null)
                {
                    // The production map adapter now offers NPC assistance before
                    // opening the shared session. Confirm the native window through input.
                    Combat.BattleGameViewInput.Focus();
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Capture("battle-assistance.png");
                    Button confirm = invitation.transform.Find("BgPanel1/Content/MiddleGo/FriendRobotGo/BottomGo")
                        .GetComponentsInChildren<Button>().Single(value => value.name == "CommonBtn_12");
                    yield return Combat.BattleGameViewInput.Click(confirm);
                    yield return null;
                }
                controller = controllerField.GetValue(runtime);
                if (controller != null) break;
            }
            Require(controller != null, "No Map10 entity started a battle.");

            var sceneController = (Zjcs.Presentation.Combat.OfficialBattleRuntimeController)controller;
            Require(worldHudStates.All(value => !value.Value || !value.Key.activeInHierarchy),
                "The battle did not suspend the separate native world HUD.");
            VerifyCommonBattleInterface(sceneController);
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_LEAVE") == "1")
            {
                // Hold only the test mode's executor loop, as in the existing real-input fixture.
                // Original UI, animations and the production leave callback remain live.
                runtime.enabled = false;
                float leaveDeadline = Time.realtimeSinceStartup + 30;
                while (sceneController.Phase == Zjcs.Combat.Official.FightBattlePhase.Preparing
                    || !UnityEngine.Object.FindObjectsOfType<Button>().Any(value =>
                        value.name == "CloseBtn_normal" && value.transform.parent.name == "CloseBtn"))
                {
                    Require(Time.realtimeSinceStartup < leaveDeadline, "Map leave HUD did not finish its original delayed mount.");
                    var start = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(value =>
                        value.name == "Btn" && value.transform.parent.name == "StartBtn" && value.interactable);
                    if (start != null) yield return Combat.BattleGameViewInput.Click(start);
                    yield return null;
                }
                var beforeLeave = ReadLeaveSaveReceipt();
                Require(!string.IsNullOrEmpty(beforeLeave.activeBattle) && beforeLeave.activeBattleStageId > 0,
                    "The map leave fixture never persisted an active battle.");
                var close = UnityEngine.Object.FindObjectsOfType<Button>().Single(value =>
                    value.name == "CloseBtn_normal" && value.transform.parent.name == "CloseBtn");
                yield return Combat.BattleGameViewInput.Click(close);
                yield return new WaitForSecondsRealtime(.5f);
                var confirmation = UnityEngine.Object.FindObjectsOfType<Zjcs.Official.WorldUI.WorldConfirmDialog>().Single();
                yield return Capture("map-leave-confirm.png");
                yield return Combat.BattleGameViewInput.Click(confirmation.ConfirmBtn);
                yield return new WaitForSecondsRealtime(.5f);
                Require(controllerField.GetValue(runtime) == null && sceneController == null,
                    "The original close path did not reach the map leave transaction.");
                Require(worldTerrain.activeInHierarchy && Mathf.Approximately(Camera.main.orthographicSize, worldCameraSize)
                    && worldHudStates.All(value => value.Key.activeSelf == value.Value)
                    && playerAnimator.transform.parent == worldModelParent && playerAnimator.transform.localScale == worldModelScale,
                    "Map abandonment did not restore the terrain, world HUD, camera and original player parent/scale.");
                var receipt = ReadLeaveSaveReceipt();
                Require(string.IsNullOrEmpty(receipt.activeBattle) && receipt.activeBattleStageId == 0
                    && receipt.activeBattlePlacementId == 0, "Map abandonment retained its disk battle checkpoint.");
                yield return Capture("map-left.png");
                Debug.Log("Map10 battle leave passed: simulated original close/confirm input, abort transaction, disk checkpoint cleanup and world restoration.");
                Finish(true, null);
                yield break;
            }
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_SLOT_BLESSING") == "1")
            {
                var player = sceneController.Checkpoint.Units.Single(value => value.Id == 1);
                Require(player.Skills.Single(value => value.ItemId == 10011).Level == 13,
                    "Mainline active skill lost its current-season blessing or included another season.");
                Require(player.ReportSkillSnapshots.Single(value => value.ItemId == 10128).Level == 17,
                    "Mainline passive snapshot lost its current-season blessing.");
                Require(player.Statuses.Any(value => value.ApplyingSkillItemId == 10128 && value.SourceSkillLevel == 17),
                    "Mainline passive status lost its full source skill level.");
                var restored = Zjcs.Combat.Official.FightBattleCheckpointSerializer.Load(
                    Zjcs.Combat.Official.FightBattleCheckpointSerializer.Save(sceneController.Checkpoint));
                Require(restored.Units.Single(value => value.Id == 1).ReportSkillSnapshots
                    .Single(value => value.ItemId == 10128).Level == 17,
                    "Checkpoint roundtrip lost the blessed passive skill snapshot.");
                Debug.Log("Mainline slot blessing passed: active 13, passive/status 17, other season excluded, checkpoint restored.");
            }
            Combat.OfficialBattleSummonModelAcceptance summonModelTest = null;
            bool finalPresentationHeld = false, finalPresentationCompleted = false;
            sceneController.StepCompleted += result =>
            {
                int counter = result.Checkpoint.Counter;
                Require(!sceneController.Tick(float.MaxValue) && result.Checkpoint.Counter == counter,
                    "The common interface advanced again while a step was still presenting.");
                if (!result.Checkpoint.Finished) return;
                Require(sceneController.IsPresentingStep && !sceneController.CanCompleteBattle,
                    "The final settled result bypassed its presentation wait.");
                finalPresentationHeld = true;
            };
            sceneController.StepPresentationCompleted += () =>
            {
                if (sceneController.Checkpoint == null || !sceneController.Checkpoint.Finished) return;
                Require(sceneController.CanCompleteBattle
                    && sceneController.Phase == Zjcs.Combat.Official.FightBattlePhase.BattleEnd,
                    "Final presentation did not hand off through BattleEnd.");
                finalPresentationCompleted = true;
            };
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_STATUS_COUNTER") == "1")
                Combat.OfficialBattleStatusCounterAcceptance.Run();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_TABS") == "1")
                yield return Combat.OfficialBattleReportTabAcceptance.Run();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_GRAPHICS") == "1")
                yield return Combat.OfficialBattleReportGraphicsAcceptance.Run();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_FOUNDATION") == "1")
                yield return Combat.OfficialBattleReportFoundationAcceptance.Run();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_DROPDOWN") == "1")
                yield return Combat.OfficialBattleReportDropdownAcceptance.Run();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_TEXTS") == "1")
                yield return Combat.OfficialBattleReportTextAcceptance.Run();
            Combat.OfficialBattleDamageProcessAcceptance damageProcessTest = null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_DAMAGE_PROCESS") == "1")
                damageProcessTest = Combat.OfficialBattleDamageProcessAcceptance.Install(sceneController);
            Combat.OfficialBattleElementConfrontationAcceptance elementTest = null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_ELEMENT_CONFRONTATION") == "1")
                elementTest = Combat.OfficialBattleElementConfrontationAcceptance.Install(sceneController);
            Combat.OfficialBattleDamageDisplayAcceptance.Probe damageDisplayTest = null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_DAMAGE_DISPLAY") == "1")
                damageDisplayTest = Combat.OfficialBattleDamageDisplayAcceptance.Probe.Install(sceneController);
            Combat.OfficialBattleDamageVisibilityAcceptance.Probe damageVisibilityTest = null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_DAMAGE_VISIBILITY") == "1")
                damageVisibilityTest = Combat.OfficialBattleDamageVisibilityAcceptance.Probe.Install(sceneController);
            Combat.OfficialBattleWorldTipsAcceptance.Probe worldTipTest=null;
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_SHIELDS") == "1")
                Combat.OfficialBattleReportStatisticsAcceptance.InstallShieldFixture(sceneController);
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_SOURCES") == "1")
                Combat.OfficialBattleReportStatisticsAcceptance.InstallSourceFixture(sceneController);
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_PLAYERS") == "1")
                Combat.OfficialBattleReportStatisticsAcceptance.InstallPlayerCountersFixture(sceneController);
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_WORLD_TIP")=="1")
                worldTipTest=Combat.OfficialBattleWorldTipsAcceptance.Probe.Install(sceneController);
            bool accumulatedDamageTest = Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_ACCUMULATED") == "1";
            if (accumulatedDamageTest)
            {
                // Exercise a restored pending status end through the actual presentation loop.
                var owner = sceneController.Checkpoint.Units.First(value => value.MonsterId > 0);
                var mark = Zjcs.Presentation.Combat.OfficialCombatAssetCatalog.LoadDefault().Load()
                    .CreateStatus(24062, owner.Id, 1, 1);
                mark.AccumulatedDamage.HpDamage = 100;
                mark.AccumulatedDamage.Contributions.Add(new Zjcs.Combat.Official.FightBattleDamageContribution
                    { SourceId = 1, SkillEntityId = 10011, HpDamage = 60 });
                mark.AccumulatedDamage.Contributions.Add(new Zjcs.Combat.Official.FightBattleDamageContribution
                    { SourceId = 1, SkillEntityId = 10012, HpDamage = 40 });
                owner.EndedAccumulatedDamageStatuses.Add(mark);
            }
            Debug.Log("Battle scene: stage=" + sceneController.Checkpoint.StageId
                + " camera=" + Camera.main?.transform.position + " size=" + Camera.main?.orthographicSize
                + " units=" + string.Join(";", sceneController.Checkpoint.Units.Select(value =>
                    value.Id + "/" + value.MonsterId + "/" + value.X + "," + value.Y)));
            foreach (Renderer modelRenderer in UnityEngine.Object.FindObjectsOfType<Renderer>()
                .Where(value => value.name == "Native draw 00" || value.GetComponentInParent<Animator>() != null).Take(4))
                Debug.Log("Battle model: " + modelRenderer.name + " position=" + modelRenderer.transform.position
                    + " scale=" + modelRenderer.transform.lossyScale + " bounds=" + modelRenderer.bounds);
            Require(!worldTerrain.activeSelf, "World terrain remained active over the battle scene.");
            VerifyOriginalCamera(sceneController);
            yield return new WaitForEndOfFrame();
            Renderer[] battleModels = sceneController.Checkpoint.Units.Where(value => value.MonsterId > 0)
                .Select(value => sceneController.TryGetUnitTransform(value.Id))
                .Where(value => value != null).SelectMany(value => value.GetComponentsInChildren<MeshRenderer>())
                .Where(value => value.enabled && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)
                .Cast<Renderer>().ToArray();
            Require(battleModels.Length > 0 && battleModels.Any(value => value.isVisible),
                "The official native monster meshes are outside the battle camera.");
            Transform battlePlayer = sceneController.TryGetUnitTransform(1);
            Require(battlePlayer != null && battlePlayer.name == "3100",
                "The mainline host did not create the shared original player entity.");
            var battleRenderer = battlePlayer.GetComponentInChildren<Zjcs.Offline.ProfessionUI.ProfessionNativeSceneRenderer>();
            Animator battleAnimator = battleRenderer != null ? battleRenderer.GetComponent<Animator>() : null;
            Require(battleAnimator != null && battleAnimator.GetCurrentAnimatorClipInfo(0).Any(value =>
                    value.weight > 0f && value.clip != null && value.clip.name == "100/idle_01"),
                "The native battle character did not enter its original Game idle clip.");
            Require(battlePlayer.Find("3100_view/body/PlayerModel_Game") == battleRenderer.transform
                && Mathf.Approximately(Mathf.Abs(battleRenderer.transform.localScale.x), 0.3f),
                "The shared player lost its original fight model hierarchy or scale.");
            Require(battlePlayer.GetComponentsInChildren<MeshRenderer>().Any(value => value.isVisible
                    && value.enabled && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0),
                "The native battle player has no visible attachment geometry.");
            Require(!playerAnimator.gameObject.activeInHierarchy && playerAnimator.transform.parent == worldModelParent
                && playerAnimator.transform.localScale == worldModelScale,
                "The mainline battle borrowed or changed its suspended world avatar.");
            yield return Zjcs.Presentation.Home.SkillDamageTestValidation.VerifyBattleCharacterMotion(battlePlayer);

            Type controllerType = controller.GetType();
            PropertyInfo phaseProperty = controllerType.GetProperty("Phase");
            PropertyInfo activeProperty = controllerType.GetProperty("IsBattleActive");
            PropertyInfo checkpointProperty = controllerType.GetProperty("Checkpoint");
            PropertyInfo speedProperty = controllerType.GetProperty("SpeedIndex");
            PropertyInfo pausedProperty = controllerType.GetProperty("IsPaused");
            MethodInfo setSpeed = controllerType.GetMethod("SetSpeedIndex");
            MethodInfo setPaused = controllerType.GetMethod("SetPaused");
            Require(phaseProperty != null && activeProperty != null && checkpointProperty != null
                && speedProperty != null && pausedProperty != null && setSpeed != null && setPaused != null,
                "Battle controller API changed.");

            Func<bool> active = () => (bool)activeProperty.GetValue(controller);
            object sessionCheckpoint = checkpointProperty.GetValue(controller);
            Func<object> checkpoint = () => checkpointProperty.GetValue(controller) ?? sessionCheckpoint;
            Func<string, long> checkpointField = name =>
            {
                object value = checkpoint();
                FieldInfo field = value == null ? null : value.GetType().GetField(name);
                return field == null ? 0L : Convert.ToInt64(field.GetValue(value));
            };

            VerifyTimelinePortraits(checkpoint());
            if (SessionState.GetBool(PortraitOnlyKey, false))
            {
                Debug.Log("Map10 battle portrait Play Mode acceptance passed.");
                Finish(true, null);
                yield break;
            }

            // Keep this short, high-level test battle from ending while real
            // mouse input exercises the HUD. UI/Animator updates remain live.
            if (SessionState.GetBool(RealInputKey, false)) runtime.enabled = false;

            yield return Combat.OfficialBattleTimelinePortraitAcceptance.VerifyNpcInPlayMode();
            yield return Combat.OfficialBattleHudMaterialTools.VerifyInPlayMode();

            if (phaseProperty.GetValue(controller).ToString() == "Preparing")
            {
                long counter = checkpointField("Counter");
                Require(!sceneController.Tick(float.MaxValue) && checkpointField("Counter") == counter,
                    "The common interface bypassed preparation with a large frame delta.");
                yield return new WaitForSecondsRealtime(1f);
                Require(checkpointField("Counter") == counter && !active(),
                    "Preparation advanced the combat executor before readiness.");
                Transform start = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                    .FirstOrDefault(value => value.name == "Btn" && value.parent != null
                        && value.parent.name == "StartBtn" && value.gameObject.activeInHierarchy);
                Require(start != null, "The official StartGameBtn is not visible during preparation.");
                if (SessionState.GetBool(RealInputKey, false))
                {
                    Combat.BattleGameViewInput.Focus();
                    yield return new WaitForSecondsRealtime(1f);
                    if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_PREPARATION") == "1")
                        yield return Combat.OfficialBattlePreparationAcceptance.Verify(sceneController);
                    yield return Capture("preparing.png");
                    yield return Combat.BattleGameViewInput.Click(start.GetComponent<Button>());
                }
                else controllerType.GetMethod("Ready").Invoke(controller, null);
                float startDeadline = Time.realtimeSinceStartup + 5f;
                while (!active() && Time.realtimeSinceStartup < startDeadline) yield return null;
                Require(active(), "The start control did not leave preparation.");
                Debug.Log("Battle input: preparation entered " + phaseProperty.GetValue(controller));
                Require(!start.gameObject.activeInHierarchy, "StartGameBtn remained visible after readiness.");
            }

            Transform[] all = UnityEngine.Object.FindObjectsOfType<Transform>(true);
            Transform quickChat = all.FirstOrDefault(value => value.name == "FunctionEntryChatQuick"
                && value.GetComponentInParent<Zjcs.Presentation.Combat.OfficialBattleHudAnimationPlayer>() != null);
            Require(quickChat != null, "The official quick-chat entry is missing.");
            Transform quickChatPanel = quickChat.Find("Content/Panel");
            Require(quickChatPanel != null && !quickChatPanel.gameObject.activeSelf && quickChatPanel.localScale == Vector3.zero,
                "The official quick-chat panel did not start in its folded Animator state.");
            Image miniChatBackground = all.First(value => value.name == "ChatMiniPanel")
                .Find("miniContent/BgImg").GetComponent<Image>();
            Require(miniChatBackground.type == Image.Type.Sliced
                && miniChatBackground.sprite.border == new Vector4(40, 40, 40, 40),
                "The mini-chat background lost its original nine-slice border.");
            if (SessionState.GetBool(RealInputKey, false))
            {
                long heldCounter = checkpointField("Counter");
                yield return VerifyQuickChat(quickChat, quickChatPanel);
                Require(checkpointField("Counter") == heldCounter,
                    "The UI fixture advanced the battle while its controller loop was held.");
                // Keep the battle held through all layout captures. A short battle can
                // finish and destroy the HUD while the Game view is being resized.
            }
            var skillBindings = Zjcs.Presentation.Combat.OfficialBattleSkillSlotBindings.Load();
            Transform mainHud = all.First(value => value.name == "FightMainPanel");
            Combat.OfficialBattleSkillSlotAcceptance.VerifyActual(mainHud.gameObject, sceneController.Checkpoint);
            if (SessionState.GetBool(RealInputKey, false))
            {
                yield return Combat.BattleGameViewInput.ResizePortrait(300f);
                Combat.OfficialBattleSkillSlotAcceptance.VerifyActual(mainHud.gameObject, sceneController.Checkpoint);
                yield return Capture("skill-slots-small.png");
                yield return Combat.BattleGameViewInput.ResizePortrait(450f);
                Combat.OfficialBattleSkillSlotAcceptance.VerifyActual(mainHud.gameObject, sceneController.Checkpoint);
            }
            string iconPath = skillBindings.slots[0].icon;
            Image skillImage = mainHud.Find(iconPath.Substring("/FightMainPanel/".Length))?.GetComponent<Image>();
            Require(skillImage != null && skillImage.sprite != null
                && skillImage.sprite.name.StartsWith("official_skill_icon_", StringComparison.Ordinal),
                "Skill slot is not bound to an official icon: "
                + (skillImage == null || skillImage.sprite == null ? "<none>" : skillImage.sprite.name));

            int timelineCards = all.Count(value => value.name.StartsWith("GameRoundCharacterItem_", StringComparison.Ordinal));
            Require(timelineCards >= 1, "Battle timeline did not create any fighter card.");
            object checkpointState = checkpointProperty.GetValue(controller);
            System.Collections.IEnumerable unitRows = checkpointState.GetType().GetField("Units").GetValue(checkpointState)
                as System.Collections.IEnumerable;
            bool checkedMonsterPortrait = false;
            foreach (object unit in unitRows)
            {
                int monsterId = Convert.ToInt32(unit.GetType().GetField("MonsterId").GetValue(unit));
                if (monsterId <= 0) continue;
                int unitId = Convert.ToInt32(unit.GetType().GetField("Id").GetValue(unit));
                Transform card = all.FirstOrDefault(value => value.name == "GameRoundCharacterItem_" + unitId);
                Transform portrait = card == null ? null : card.Find("IconGo/MonsterIconImg");
                SpriteRenderer portraitImage = portrait == null ? null : portrait.GetComponent<SpriteRenderer>();
                Require(portraitImage != null && portraitImage.sprite != null
                    && portraitImage.sprite.name == "G_" + monsterId,
                    "Monster timeline portrait was not resolved for IconId " + monsterId);
                checkedMonsterPortrait = true;
                break;
            }
            Require(checkedMonsterPortrait, "Battle acceptance found no monster portrait to verify.");
            TMP_Text roundText = all.Where(value => value.name == "RoundText")
                .Select(value => value.GetComponent<TMP_Text>()).FirstOrDefault(value => value != null);
            Require(roundText != null && roundText.text.Contains("/"), "Battle round text is empty.");

            var speeds = ((IEnumerable)controllerType.GetProperty("UnlockedSpeedIndices").GetValue(controller))
                .Cast<int>().ToArray();
            int currentSpeed = (int)speedProperty.GetValue(controller);
            int nextSpeed = speeds.FirstOrDefault(value => value != currentSpeed);
            if (speeds.Any(value => value != currentSpeed))
            {
                Require((bool)setSpeed.Invoke(controller, new object[] { nextSpeed }), "An unlocked battle speed could not be selected.");
                Require((int)speedProperty.GetValue(controller) == nextSpeed, "Battle speed index mismatch after switching.");
            }
            else Require(!(bool)setSpeed.Invoke(controller, new object[] { 2 }), "Battle selected a locked speed.");
            setPaused.Invoke(controller, new object[] { true });
            yield return null;
            Require((bool)pausedProperty.GetValue(controller), "Battle did not pause.");
            int pausedCounter = sceneController.Checkpoint.Counter;
            Require(!sceneController.Tick(float.MaxValue) && sceneController.Checkpoint.Counter == pausedCounter,
                "The common interface advanced the executor while paused.");
            setPaused.Invoke(controller, new object[] { false });
            Require(!(bool)pausedProperty.GetValue(controller), "Battle did not resume.");

            if (SessionState.GetBool(RealInputKey, false)) yield return Capture("playing.png");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_SUMMON_MODEL")))
                summonModelTest = Combat.OfficialBattleSummonModelAcceptance.Install(sceneController);
            if (SessionState.GetBool(RealInputKey, false) || damageDisplayTest != null) runtime.enabled = true;

            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!active()) break;
                if (checkpointField("Counter") >= 1 && checkpointField("DamageEventSerial") >= 1) break;
                yield return null;
            }

            Require(checkpoint() != null, "Battle checkpoint is missing after the battle started.");
            Require(checkpointField("Counter") >= 1, "Battle did not execute its first step.");
            Require(checkpointField("DamageEventSerial") >= 1, "Battle did not record damage during the first steps.");
            if (summonModelTest != null)
            {
                runtime.enabled = false;
                yield return summonModelTest.Verify(sceneController);
                runtime.enabled = true;
            }
            if (damageProcessTest != null)
            {
                while (active() && !damageProcessTest.HasExecuted(sceneController.Checkpoint)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                damageProcessTest.Verify(sceneController.Checkpoint);
                yield return Capture("damage-process.png");
            }
            if (elementTest != null)
            {
                while (active() && !elementTest.HasExecuted(sceneController.Checkpoint)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                elementTest.Verify(sceneController.Checkpoint);
                float elementDeadline = Time.realtimeSinceStartup + 5f;
                bool elementDisplayed;
                while (!(elementDisplayed = elementTest.TryVerifyPresentation())
                    && Time.realtimeSinceStartup < elementDeadline) yield return null;
                Require(elementDisplayed,"Original element feedback did not render through the live battle loop.");
                sceneController.SetPaused(true);
                yield return Capture("element-confrontation.png");
                sceneController.SetPaused(false);
            }
            if (damageDisplayTest != null)
            {
                float displayDeadline = Time.realtimeSinceStartup + 5f;
                bool displayed;
                while (!(displayed = damageDisplayTest.TryVerify(sceneController.Checkpoint))
                    && Time.realtimeSinceStartup < displayDeadline) yield return null;
                Require(displayed, "The shield-only number did not render through the actual battle loop. "
                    + damageDisplayTest.Observation);
                sceneController.SetPaused(true);
                yield return Capture("damage-display.png");
                sceneController.SetPaused(false);
            }
            if (damageVisibilityTest != null)
            {
                float displayDeadline=Time.realtimeSinceStartup+8f;
                bool displayed;
                while (!(displayed=damageVisibilityTest.TryVerify(sceneController.Checkpoint))
                    && Time.realtimeSinceStartup<displayDeadline) yield return null;
                Require(displayed,"The original zero-damage filter did not render correctly. "+damageVisibilityTest.Observation);
                sceneController.SetPaused(true);
                yield return Capture("damage-visibility.png");
                sceneController.SetPaused(false);
            }
            if(worldTipTest!=null)
            {
                float displayDeadline=Time.realtimeSinceStartup+8f;
                bool displayed;
                while(!(displayed=worldTipTest.TryVerify(sceneController.Checkpoint))
                    && Time.realtimeSinceStartup<displayDeadline)yield return null;
                Require(displayed,"The original world tip did not render through the live battle loop. "+worldTipTest.Observation);
                sceneController.SetPaused(true);
                yield return Capture("world-tip.png");
                sceneController.SetPaused(false);
            }
            if (accumulatedDamageTest)
            {
                var state = (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint();
                Require(state.Receipts.SelectMany(value => value.Event.DamageEvents)
                    .Any(value => value.SkillEntityId == 13148 && value.HitEntityId == 13149 && value.Damage == -30),
                    "The restored accumulated-damage status did not execute through the real battle presentation.");
                Require(state.Statistics.Skill(1, 10011, 0).AdditionalAttributedDamage == 18
                    && state.Statistics.Skill(1, 10012, 0).AdditionalAttributedDamage == 12
                    && state.Statistics.Skill(1, 13148, 0).Damage == 30,
                    "The real battle loop did not retain the mark hit and add proportional source credit.");
                Debug.Log("Accumulated-damage Play Mode passed: independent 30 HP hit, additional source skill credit 18/12.");
            }
            Require(active() || (bool)checkpoint().GetType().GetField("Finished").GetValue(checkpoint()),
                "Battle left Play without a final result. Phase=" + phaseProperty.GetValue(controller));

            Debug.Log("Map10 battle acceptance passed: battle reached Play, counter=" + checkpointField("Counter")
                + ", damage events=" + checkpointField("DamageEventSerial")
                + ", skill icons and timeline bound, round text and speed controls work.");
            if (SessionState.GetBool(RealInputKey, false) || damageProcessTest != null)
            {
                float endDeadline = Time.realtimeSinceStartup + 120f;
                while (controllerField.GetValue(runtime) != null && Time.realtimeSinceStartup < endDeadline)
                    yield return null;
                yield return null; // Allow deferred destruction of the temporary renderer group.
                Require(checkpointField("Finished") != 0, "The complete battle did not reach a final result.");
                var mainlineReport = runtime.FinalReport;
                var expectedMainlineReport = Zjcs.Combat.Official.FightBattleReportBuilder.Build(
                    (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                Require(mainlineReport != null && mainlineReport.StageId == expectedMainlineReport.StageId
                    && mainlineReport.Won == expectedMainlineReport.Won
                    && mainlineReport.Round == expectedMainlineReport.Round
                    && mainlineReport.Units.Count == expectedMainlineReport.Units.Count
                    && mainlineReport.Skills.Count == expectedMainlineReport.Skills.Count
                    && mainlineReport.TotalDamageDealt == expectedMainlineReport.TotalDamageDealt
                    && mainlineReport.TotalDamageTaken == expectedMainlineReport.TotalDamageTaken
                    && mainlineReport.TotalHealDealt == expectedMainlineReport.TotalHealDealt
                    && mainlineReport.TotalShieldGranted == expectedMainlineReport.TotalShieldGranted,
                    "The mainline adapter did not retain the shared final report matching its completed battle.");
                foreach (var expectedUnit in expectedMainlineReport.Units)
                    Require(mainlineReport.Unit(expectedUnit.UnitId)?.DamageAndShieldDamageDealt
                        == expectedUnit.DamageAndShieldDamageDealt,
                        "The mainline adapter changed the official damage-and-shield statistic for unit "
                        + expectedUnit.UnitId + ".");
                Require(finalPresentationHeld && finalPresentationCompleted,
                    "The complete battle did not exercise final presentation hold and release.");
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_WINDOW") == "1")
                    yield return Combat.OfficialSharedBattleReportAcceptance.Exercise(
                        (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_STATISTICS") == "1")
                    Combat.OfficialBattleReportStatisticsAcceptance.Verify(
                        (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_PLAYER_ROW") == "1")
                    yield return Combat.OfficialBattleReportPlayerRowAcceptance.Run(
                        (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_SKILL_ROW") == "1")
                    yield return Combat.OfficialBattleReportSkillRowAcceptance.Run(
                        (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                Require(controllerField.GetValue(runtime) == null && worldTerrain.activeSelf,
                    "The battle did not restore its suspended world roots.");
                Require(worldHudStates.All(value => value.Key.activeSelf == value.Value),
                    "The battle did not restore the native world HUD states.");
                Require(Mathf.Approximately(Camera.main.orthographicSize, worldCameraSize),
                    "The battle did not restore the world camera size.");
                Require(playerAnimator != null && playerAnimator.transform.parent == worldModelParent
                    && playerAnimator.transform.localScale == worldModelScale,
                    "The battle did not restore the original world character hierarchy and scale.");
                Require(playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == worldAnimationState,
                    "The battle did not restore the original world character animation state.");
                Require(UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.SortingGroup>()
                    .All(value => !value.name.StartsWith("Map10Player_", StringComparison.Ordinal)),
                    "The reused map avatar retained its temporary battle sorting group.");
                yield return null;
                Require(UnityEngine.Object.FindObjectsOfType<Zjcs.Presentation.Combat.OfficialBattleSceneTimelineView>(true).Length == 0
                    && UnityEngine.Object.FindObjectsOfType<Zjcs.Presentation.CharacterCreation.CharacterNativePortraitLease>(true).Length == 0,
                    "The battle retained its scene timeline or captured portrait after returning.");
                yield return Capture("returned.png");
                Debug.Log("Battle scene return passed: final result, world roots, camera and avatar sorting restored.");
            }
            Finish(true, null);
        }

        private static IEnumerator VerifyQuickChat(Transform entry, Transform panel)
        {
            var controller = entry.GetComponent<Zjcs.Official.WorldUI.WorldFunctionEntrysPanelBase>();
            Require(controller != null, "The official function-entry controller is not bound.");
            Button folded = entry.Find("Content/Entry/EntryBtnFolded").GetComponent<Button>();
            Button expanded = entry.Find("Content/Entry/EntryBtnExpanded").GetComponent<Button>();
            Require(folded.gameObject.activeSelf && !expanded.gameObject.activeSelf,
                "The official initial entry-button visibility is wrong.");
            yield return Combat.BattleGameViewInput.Click(folded);
            yield return new WaitForSecondsRealtime(0.35f);
            Require(controller.Expanded && panel.gameObject.activeInHierarchy && expanded.gameObject.activeSelf && !folded.gameObject.activeSelf
                && Mathf.Approximately(panel.localScale.x, 1f) && Mathf.Approximately(panel.localScale.y, 1f),
                "The real quick-chat click did not finish the official expansion animation.");
            yield return Capture("quick-chat-expanded.png");
            yield return Combat.BattleGameViewInput.Click(expanded);
            yield return new WaitForSecondsRealtime(0.5f);
            Require(!controller.Expanded && !panel.gameObject.activeSelf && panel.localScale == Vector3.zero,
                "The real quick-chat entry click did not fold the official panel.");
            yield return Combat.BattleGameViewInput.Click(folded);
            yield return new WaitForSecondsRealtime(0.35f);
            Require(controller.Expanded, "Quick chat did not expand a second time.");
            yield return Combat.BattleGameViewInput.ClickScreenPoint(new Vector2(Screen.width * .5f, Screen.height * .5f));
            yield return new WaitForSecondsRealtime(0.5f);
            Require(!controller.Expanded && !panel.gameObject.activeSelf && panel.localScale == Vector3.zero,
                "A real click outside the official self rectangles did not fold quick chat.");
            yield return Capture("quick-chat-folded.png");
            Debug.Log("Battle quick-chat "
                + (Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED") == "1" ? "simulated pointer" : "real")
                + " input passed: default Folded, expand/fold buttons, animation events and outside-click dismissal.");
        }

        private static void VerifyTimelinePortraits(object checkpoint)
        {
            Require(checkpoint != null, "Map10 battle checkpoint is missing before its first step.");
            FieldInfo unitsField = checkpoint.GetType().GetField("Units");
            IEnumerable units = unitsField == null ? null : unitsField.GetValue(checkpoint) as IEnumerable;
            Require(units != null, "Map10 battle units are missing before the first step.");

            Transform[] nodes = UnityEngine.Object.FindObjectsOfType<Transform>(true);
            bool playerChecked = false;
            bool monsterChecked = false;
            foreach (object unit in units)
            {
                int unitId = Convert.ToInt32(unit.GetType().GetField("Id").GetValue(unit));
                int monsterId = Convert.ToInt32(unit.GetType().GetField("MonsterId").GetValue(unit));
                Transform card = nodes.FirstOrDefault(value => value.name == "GameRoundCharacterItem_" + unitId);
                if (card != null)
                {
                    Transform particleParent = card.Find("Eff_UI_Icon_Death01/UIParticle");
                    Require(particleParent != null && !particleParent.gameObject.activeSelf
                        && particleParent.GetComponentsInChildren<ParticleSystem>(true).Length == 2,
                        "The scene timeline did not preserve its original disabled particle branch.");
                }
                if (monsterId > 0)
                {
                    Transform icon = card == null ? null : card.Find("IconGo/MonsterIconImg");
                    SpriteRenderer image = icon == null ? null : icon.GetComponent<SpriteRenderer>();
                    Require(image != null && image.sprite != null && image.sprite.name == "G_" + monsterId,
                        "Map10 monster timeline portrait did not bind G_" + monsterId + ".");
                    monsterChecked = true;
                }
                else
                {
                    Transform icon = card == null ? null : card.Find("IconGo/PlayerHeadETC2Go/HeadImg");
                    var circle = icon == null ? null : icon.GetComponent<Zjcs.Official.WorldUI.WorldCircleSpriteRenderer>();
                    Texture2D texture = circle == null || circle.Sprite == null ? null : circle.Sprite.texture;
                    Require(texture != null && texture.width == 100 && texture.height == 120
                        && texture.GetPixels32().Count(value => value.a > 128) > 1000
                        && icon.GetComponent<MeshFilter>().sharedMesh.vertexCount == 33,
                        "Map10 player scene portrait did not bind its native head texture and original circular mesh.");
                    playerChecked = true;
                }
                if (playerChecked && monsterChecked) break;
            }
            Require(playerChecked, "Map10 battle timeline has no bound player portrait.");
            Require(monsterChecked, "Map10 battle timeline has no bound monster portrait.");
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Map10BattleAcceptanceCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1;
            if (EventSystem.current == null)
                new GameObject("Map10BattleAcceptanceEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return canvas;
        }

        [Serializable] private sealed class CameraBindings { public CameraBinding[] cameras; }
        [Serializable] private sealed class CameraBinding
        {
            public int gameId;
            public float baseSize;
            public float[] baseAspect;
        }

        private static void VerifyOriginalCamera(Zjcs.Presentation.Combat.OfficialBattleRuntimeController controller)
        {
            Require(controller.OwnsPresentationCamera && Camera.main != null,
                "The common battle module did not take ownership of the map camera.");
            var cameraRoot = UnityEngine.Object.FindObjectsOfType<Transform>()
                .Single(value => value.name.StartsWith("Official Battle Camera ", StringComparison.Ordinal));
            var source = JsonUtility.FromJson<CameraBindings>(
                Resources.Load<TextAsset>("Combat/battle-camera-bindings").text).cameras
                .Single(value => cameraRoot.name == "Official Battle Camera " + value.gameId);
            // GameCameraIniter.Adaptor preserves the original width on taller viewports.
            // Comparing with the unadapted scene size incorrectly rejects a valid portrait view.
            float expected = source.baseSize * Mathf.Max(1f,
                source.baseAspect[0] / source.baseAspect[1] / Camera.main.aspect);
            Require(Mathf.Approximately(Camera.main.orthographicSize, expected),
                "The battle camera differs from the original aspect adapter: expected=" + expected
                + " actual=" + Camera.main.orthographicSize + " aspect=" + Camera.main.aspect);
        }

        [Serializable] private sealed class LeaveSaveReceipt
        {
            public string activeBattle;
            public int activeBattleStageId, activeBattlePlacementId;
        }

        private static LeaveSaveReceipt ReadLeaveSaveReceipt()
        {
            var slots = new ProfileSlotCatalog(SessionState.GetString(RootKey, string.Empty));
            return JsonUtility.FromJson<LeaveSaveReceipt>(File.ReadAllText(Path.Combine(
                slots.GetSlotDirectory(SessionState.GetString(SlotKey, string.Empty)), "map-10-save.json")));
        }

        private static IEnumerator Capture(string filename)
        {
            yield return null;
            string output = Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_OUTPUT");
            if (string.IsNullOrEmpty(output)) yield break;
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, filename);
            float deadline = Time.realtimeSinceStartup + 5f;
            int width=0,height=0,expectedWidth=0,expectedHeight=0;
            do
            {
                // A native Game view resize can settle one frame after Screen reports its first size.
                yield return null;
                Canvas.ForceUpdateCanvases();
                expectedWidth=Screen.width;expectedHeight=Screen.height;
                if (File.Exists(path)) File.Delete(path);
                yield return Combat.BattleGameViewInput.CaptureGameViewScreenshot(path);
                do { yield return null; }
                while (!File.Exists(path) && Time.realtimeSinceStartup<deadline);
                if (!File.Exists(path)) break;
                byte[] png=File.ReadAllBytes(path);
                Require(png.Length>=24,"The runtime screenshot is incomplete.");
                width=(png[16]<<24)|(png[17]<<16)|(png[18]<<8)|png[19];
                height=(png[20]<<24)|(png[21]<<16)|(png[22]<<8)|png[23];
                if (width==expectedWidth && height==expectedHeight
                    && Screen.width==expectedWidth && Screen.height==expectedHeight)
                {
                    Debug.Log("Battle runtime screenshot matches input/layout: "+filename+" "+width+"x"+height);
                    yield break;
                }
            } while (Time.realtimeSinceStartup<deadline);
            throw new InvalidOperationException("The screenshot viewport did not settle with input/layout: "+width+"x"+height
                +" versus "+expectedWidth+"x"+expectedHeight);
        }


        private static void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            // Resource import can invoke this inside native engine loading.
            // Exit on the next Editor tick after that native stack has unwound.
            if ((type == LogType.Error || type == LogType.Exception) && pendingRuntimeError == null)
                pendingRuntimeError = new InvalidOperationException(message + "\n" + stackTrace);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            pendingRuntimeError = null;
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            Application.logMessageReceived -= OnRuntimeLog;
            if (error != null) Debug.LogException(error);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        public sealed class AcceptanceRunner : MonoBehaviour
        {
        }
    }
}
