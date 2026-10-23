using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    public static class OfficialMap10PlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Map10.Acceptance.Active";
        private const string RootKey = "Zjcs.Map10.Acceptance.Root";
        private const string SlotKey = "Zjcs.Map10.Acceptance.Slot";
        private const string ResultKey = "Zjcs.Map10.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.Map10.Acceptance.Deadline";
        private static bool started;

        [MenuItem("Zjcs/Validate/Map10 Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running Map10 acceptance.");

            string root = Path.Combine(Path.GetTempPath(), "zjcs-map10-acceptance-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                var save = new OfflineMainlineSave
                {
                    CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                save.Player.CharacterCreated = true;
                save.Player.CharacterName = "Map10 acceptance";
                save.Player.InitialWeaponType = 201;
                save.Player.Level = 1;
                save.Player.Rank = 1;
                save.Player.SubRank = 1;
                save.Player.SeasonRank = 1;
                save.Player.Wardrobe.Profession = 1;
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));

                SessionState.SetString(RootKey, root);
                SessionState.SetString(SlotKey, slot);
                SessionState.SetInt(ResultKey, 2);
                SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 180f);
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
                Finish(false, new TimeoutException("Map10 acceptance did not start before its deadline."));
                return;
            }

            started = true;
            try
            {
                Canvas canvas = CreateCanvas();
                GameObject avatar = new GameObject("Map10AcceptanceAvatar", typeof(SpriteRenderer));
                var runtimeObject = new GameObject("Map10AcceptanceRuntime");
                OfficialMap10GameRuntime runtime = runtimeObject.AddComponent<OfficialMap10GameRuntime>();
                runtime.Begin(SessionState.GetString(SlotKey, string.Empty), SessionState.GetString(RootKey, string.Empty),
                    canvas.transform, avatar, "Map10 acceptance", null);

                var runner = new GameObject("Map10AcceptanceRunner");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<AcceptanceRunner>().StartCoroutine(VerifyMapStarted(runtime, runtimeObject));
            }
            catch (Exception error)
            {
                Finish(false, error);
            }
        }

        private static IEnumerator VerifyMapStarted(OfficialMap10GameRuntime runtime, GameObject runtimeObject)
        {
            yield return null;
            yield return null;

            OfficialMap10EntityView entities = runtimeObject.GetComponentInChildren<OfficialMap10EntityView>(true);
            OfficialMap10EnvironmentView environment = runtimeObject.GetComponentInChildren<OfficialMap10EnvironmentView>(true);
            GameObject uiRoot = GameObject.Find("OfficialMap10UI");
            Transform world = uiRoot != null ? uiRoot.transform.Find("WorldCanvas") : null;
            Transform talk = uiRoot != null ? uiRoot.transform.Find("TalkDialog") : null;
            Require(entities != null && entities.EntityCount == 82, "Map10 did not instantiate all 82 official entities.");
            Require(environment != null && environment.WaterMeshCount == 4 && environment.ButterflyCount == 15,
                "Map10 environment renderers did not initialize.");
            Require(world != null && world.gameObject.activeInHierarchy, "The official Map10 HUD did not appear.");
            Require(talk != null && talk.gameObject.activeInHierarchy, "The FirstEnterMap dialogue did not appear.");

            TMP_Text dialogue = talk.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(value => value.name == "TalkText");
            Button next = talk.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "NextBtn" && value.gameObject.activeInHierarchy);
            Require(dialogue != null && !string.IsNullOrWhiteSpace(dialogue.text), "The first official dialogue line is empty.");
            Require(next != null, "The FirstEnterMap dialogue has no active next button.");

            string savePath = Path.Combine(new ProfileSlotCatalog(SessionState.GetString(RootKey, string.Empty))
                .GetSlotDirectory(SessionState.GetString(SlotKey, string.Empty)), "map-10-save.json");
            Require(File.Exists(savePath), "Opening Map10 did not create its local progress save.");
            string firstLine = dialogue.text;
            next.onClick.Invoke();
            yield return null;
            yield return null;
            Require(!talk.gameObject.activeInHierarchy || dialogue.text != firstLine,
                "The official dialogue next button did not advance the opening story.");

            Debug.Log("Map10 Play Mode acceptance passed: HUD and FirstEnterMap dialogue loaded, 82 entities and "
                + "19 environment renderers initialized, local map progress saved, and the dialogue advanced.");
            Finish(true, null);
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Map10AcceptanceCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (EventSystem.current == null)
                new GameObject("Map10AcceptanceEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
