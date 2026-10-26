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

        [MenuItem("Zjcs/Validate/Map10 Battle Play Mode")]
        public static void Run() => Start(false);

        [MenuItem("Zjcs/Validate/Map10 Battle Portrait Play Mode")]
        public static void RunPortraitOnly() => Start(true);

        [MenuItem("Zjcs/Validate/Map10 Battle Real Input Play Mode")]
        public static void RunRealInput() => Start(false, true);

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

            string root = Path.Combine(Path.GetTempPath(), "zjcs-map10-battle-" + Guid.NewGuid().ToString("N"));
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
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
                File.WriteAllText(Path.Combine(slots.GetSlotDirectory(slot), "map-10-save.json"),
                    "{\"schema\":1,\"slotId\":\"" + slot + "\",\"gridX\":24,\"gridY\":23,"
                    + "\"firstBattleIntroShown\":true,\"completedStories\":[\"FirstEnterMap\"]}");

                SessionState.SetString(RootKey, root);
                SessionState.SetString(SlotKey, slot);
                SessionState.SetInt(ResultKey, 2);
                SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 240f);
                SessionState.SetBool(PortraitOnlyKey, portraitOnly);
                SessionState.SetBool(RealInputKey, realInput);
                SessionState.SetBool(ActiveKey, true);
                started = false;
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
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Map10 battle acceptance did not start before its deadline."));
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
                startStage.Invoke(runtime, new[] { entity, null });
                yield return null;
                controller = controllerField.GetValue(runtime);
                if (controller != null) break;
            }
            Require(controller != null, "No Map10 entity started a battle.");

            var sceneController = (Zjcs.Presentation.Combat.OfficialBattleRuntimeController)controller;
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
            Require(((Zjcs.Presentation.Combat.OfficialBattleRuntimeController)controller)
                    .TryGetSceneCamera(out _, out float sourceCameraSize)
                && Camera.main != null && Mathf.Approximately(Camera.main.orthographicSize, sourceCameraSize),
                "The recovered scene camera did not use its official orthographic size.");
            Renderer[] battleModels = UnityEngine.Object.FindObjectsOfType<Renderer>()
                .Where(value => value.name.StartsWith("Native draw ", StringComparison.Ordinal)).ToArray();
            Require(battleModels.Length > 0 && battleModels.Any(value => value.isVisible),
                "The official native monster meshes are outside the battle camera.");
            Require(UnityEngine.Object.FindObjectsOfType<Animator>().Any(value =>
                value.name.StartsWith("Map10Player_", StringComparison.Ordinal)),
                "The battle test did not mount the real character model.");
            Require(UnityEngine.Object.FindObjectsOfType<MeshRenderer>().Any(value => value.isVisible
                && value.GetComponentInParent<Animator>() != null),
                "The real character appearance has no visible mesh in the battle camera.");
            Require(playerAnimator.GetCurrentAnimatorClipInfo(0).Any(value => value.weight > 0f
                    && value.clip != null && value.clip.name == "100/idle_01"),
                "The native character entered an empty source state instead of its converted idle clip.");
            Renderer[] characterParts = playerAnimator.GetComponentsInChildren<Renderer>(true);
            foreach (string part in new[] { "hair_", "eye_", "brow_", "mouth_" })
                Require(characterParts.Any(value => value.name == "Official native attachment geometry"
                        && value.transform.parent.name.StartsWith("__skin__" + part, StringComparison.Ordinal)
                        && value.enabled && value.gameObject.activeInHierarchy
                        && value.GetComponent<MeshFilter>().sharedMesh.bounds.size.sqrMagnitude > .00001f),
                    "The created character lost its native appearance part in battle: " + part);
            Require(playerAnimator.transform.parent != worldModelParent
                && Mathf.Approximately(Mathf.Abs(playerAnimator.transform.parent.localScale.x), 0.3f),
                "The reused character did not receive the original fight model transform.");

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
            setPaused.Invoke(controller, new object[] { false });
            Require(!(bool)pausedProperty.GetValue(controller), "Battle did not resume.");

            if (SessionState.GetBool(RealInputKey, false)) yield return Capture("playing.png");
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
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_STATISTICS") == "1")
                    Combat.OfficialBattleReportStatisticsAcceptance.Verify(
                        (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TEST_REPORT_SKILL_ROW") == "1")
                    yield return Combat.OfficialBattleReportSkillRowAcceptance.Run(
                        (Zjcs.Combat.Official.FightBattleCheckpoint)checkpoint());
                Require(controllerField.GetValue(runtime) == null && worldTerrain.activeSelf,
                    "The battle did not restore its suspended world roots.");
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
                ScreenCapture.CaptureScreenshot(path);
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
            if (type == LogType.Error || type == LogType.Exception)
                Finish(false, new InvalidOperationException(message + "\n" + stackTrace));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
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
