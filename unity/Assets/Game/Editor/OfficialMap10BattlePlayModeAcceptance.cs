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

            AssetDatabase.ImportAsset("Assets/Game/Resources/Combat/skill-playables.json",
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Require(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Game/Resources/Combat/skill-playables.json") != null,
                "The official skill Playable JSON did not import as a TextAsset.");
            foreach (string name in new[] { "battle-hud-graphic-bindings", "battle-hud-graphic-bindings-source-manifest" })
            {
                string path = "Assets/Game/Resources/Combat/" + name + ".json";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Require(AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null, "The official HUD binding did not import: " + path);
            }

            string root = Path.Combine(Path.GetTempPath(), "zjcs-map10-battle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                var save = new OfflineMainlineSave
                {
                    CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                save.Player.CharacterCreated = true;
                save.Player.CharacterName = "Map10 battle acceptance";
                save.Player.InitialWeaponType = 201;
                save.Player.Level = 30;
                save.Player.Rank = 1;
                save.Player.SubRank = 3;
                save.Player.SeasonRank = 1;
                save.Player.Wardrobe.Profession = 1;
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
                GameObject avatar = new GameObject("Map10BattleAvatar", typeof(SpriteRenderer));
                var runtimeObject = new GameObject("Map10BattleRuntime");
                OfficialMap10GameRuntime runtime = runtimeObject.AddComponent<OfficialMap10GameRuntime>();
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

        private static IEnumerator VerifyBattle(OfficialMap10GameRuntime runtime)
        {
            yield return null;
            yield return null;

            FieldInfo entitiesField = typeof(OfficialMap10GameRuntime)
                .GetField("mapEntities", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo startStage = typeof(OfficialMap10GameRuntime)
                .GetMethod("StartEntityStage", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo controllerField = typeof(OfficialMap10GameRuntime)
                .GetField("battleController", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(entitiesField != null && startStage != null && controllerField != null,
                "Map10 battle entry points are missing.");

            var entities = ((System.Collections.IDictionary)entitiesField.GetValue(runtime));
            Require(entities != null && entities.Count > 0, "Map10 did not instantiate its entities.");
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

            if (SessionState.GetBool(PortraitOnlyKey, false))
            {
                VerifyTimelinePortraits(checkpoint());
                Debug.Log("Map10 battle portrait Play Mode acceptance passed.");
                Finish(true, null);
                yield break;
            }

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
            Transform skillCard = all.FirstOrDefault(value => value.name == "GameSkillIconItem_0");
            Require(skillCard != null, "Battle HUD did not create the first skill slot.");
            Image skillImage = skillCard.Find("Content/Bg/Anim/Icon/SkillIconImg")?.GetComponent<Image>();
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
                Image portraitImage = portrait == null ? null : portrait.GetComponent<Image>();
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
            Require(active() || (bool)checkpoint().GetType().GetField("Finished").GetValue(checkpoint()),
                "Battle left Play without a final result. Phase=" + phaseProperty.GetValue(controller));

            Debug.Log("Map10 battle acceptance passed: battle reached Play, counter=" + checkpointField("Counter")
                + ", damage events=" + checkpointField("DamageEventSerial")
                + ", skill icons and timeline bound, round text and speed controls work.");
            Finish(true, null);
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
                if (monsterId > 0)
                {
                    Transform icon = card == null ? null : card.Find("IconGo/MonsterIconImg");
                    Image image = icon == null ? null : icon.GetComponent<Image>();
                    Require(image != null && image.sprite != null && image.sprite.name == "G_" + monsterId,
                        "Map10 monster timeline portrait did not bind G_" + monsterId + ".");
                    monsterChecked = true;
                }
                else
                {
                    Transform icon = card == null ? null : card.Find("IconGo/PlayerHeadETC2Go/HeadImg");
                    RawImage image = icon == null ? null : icon.GetComponent<RawImage>();
                    Require(image != null && image.texture != null,
                        "Map10 player timeline RawImage did not bind its fallback portrait.");
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
            ScreenCapture.CaptureScreenshot(Path.Combine(output, filename));
            yield return new WaitForSecondsRealtime(0.25f);
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
