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
        private static bool started;

        [MenuItem("Zjcs/Validate/Map10 Battle Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the battle acceptance.");

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
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));

                SessionState.SetString(RootKey, root);
                SessionState.SetString(SlotKey, slot);
                SessionState.SetInt(ResultKey, 2);
                SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 240f);
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
                Application.logMessageReceived += OnRuntimeLog;
            if (state != PlayModeStateChange.EnteredEditMode) return;

            Application.logMessageReceived -= OnRuntimeLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            string root = SessionState.GetString(RootKey, string.Empty);
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            if (Directory.Exists(root)) Directory.Delete(root, true);
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || started) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Map10 battle acceptance did not start before its deadline."));
                return;
            }

            started = true;
            try
            {
                Canvas canvas = CreateCanvas();
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
            Func<object> checkpoint = () => checkpointProperty.GetValue(controller);
            Func<string, long> checkpointField = name =>
            {
                object value = checkpoint();
                FieldInfo field = value == null ? null : value.GetType().GetField(name);
                return field == null ? 0L : Convert.ToInt64(field.GetValue(value));
            };

            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!active()) break;
                if (checkpointField("Sequence") >= 1 && checkpointField("DamageEventSerial") >= 1) break;
                yield return null;
            }

            Require(checkpoint() != null, "Battle checkpoint is missing after the battle started.");
            Require(checkpointField("Sequence") >= 1, "Battle did not execute its first step.");
            Require(checkpointField("DamageEventSerial") >= 1, "Battle did not record damage during the first steps.");
            Require(active() && phaseProperty.GetValue(controller).ToString() == "Play",
                "Battle is not active in the Play phase. Phase=" + phaseProperty.GetValue(controller));

            Transform[] all = UnityEngine.Object.FindObjectsOfType<Transform>(true);
            Transform skillCard = all.FirstOrDefault(value => value.name == "GameSkillIconItem_0");
            Require(skillCard != null, "Battle HUD did not create the first skill slot.");
            Image skillImage = skillCard.GetComponentsInChildren<Image>(true).FirstOrDefault();
            Require(skillImage != null && skillImage.sprite != null
                && skillImage.sprite.name.StartsWith("official_skill_icon_", StringComparison.Ordinal),
                "Skill slot is not bound to an official icon: "
                + (skillImage == null || skillImage.sprite == null ? "<none>" : skillImage.sprite.name));

            int timelineCards = all.Count(value => value.name.StartsWith("GameRoundCharacterItem_", StringComparison.Ordinal));
            Require(timelineCards >= 1, "Battle timeline did not create any fighter card.");
            TMP_Text roundText = all.Where(value => value.name == "RoundText")
                .Select(value => value.GetComponent<TMP_Text>()).FirstOrDefault(value => value != null);
            Require(roundText != null && roundText.text.Contains("/"), "Battle round text is empty.");

            Require((bool)setSpeed.Invoke(controller, new object[] { 2 }), "Battle speed could not be switched.");
            Require((int)speedProperty.GetValue(controller) == 2, "Battle speed index mismatch after switching.");
            setPaused.Invoke(controller, new object[] { true });
            yield return null;
            Require((bool)pausedProperty.GetValue(controller), "Battle did not pause.");
            setPaused.Invoke(controller, new object[] { false });
            Require(!(bool)pausedProperty.GetValue(controller), "Battle did not resume.");

            Debug.Log("Map10 battle acceptance passed: battle reached Play, sequence=" + checkpointField("Sequence")
                + ", damage events=" + checkpointField("DamageEventSerial")
                + ", skill icons and timeline bound, round text and speed controls work.");
            Finish(true, null);
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Map10BattleAcceptanceCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (EventSystem.current == null)
                new GameObject("Map10BattleAcceptanceEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return canvas;
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
