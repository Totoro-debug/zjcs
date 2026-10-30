using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityPrefabPlayAcceptance
    {
        private const string Key = "Map14.EntityPrefabPlay";
        private static bool started;
        private static int errors;

        public static void Run()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")))
                throw new InvalidDataException("Explicit validation directory required.");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 entity prefab Play Mode timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; errors = 0;
            Application.logMessageReceived += Observe;
            new GameObject("Map14 entity prefab acceptance").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= Observe;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        private static IEnumerator Verify()
        {
                Application.runInBackground = true;
                foreach (var startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(value =>
                    value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
                {
                    startup.StopAllCoroutines();
                    UnityEngine.Object.Destroy(startup.gameObject);
                }
                var canvas = new GameObject("Map14 entity acceptance UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                string saves = Path.Combine(output, "saves");
                var slots = new ProfileSlotCatalog(saves);
                string slot = slots.CreateSlot();
                var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                save.Player.CharacterCreated = true; save.Player.CharacterName = "龙之国实体验证"; save.Player.InitialWeaponType = 201;
                var assets = OfficialCharacterAssetCatalog.LoadDefault().Load();
                save.Player.Level = 90; save.Player.Experience = assets.CumulativeLevelExperience[90];
                save.Player.SubRank = assets.LevelLimits.Where(value => value.PlayerBlessLevel == 0 && value.PlayerLevel >= 90).Min(value => value.SubRank);
                save.Player.Rank = assets.RankOfSubRank(save.Player.SubRank); save.Player.Wardrobe.Profession = 1;
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
                var progression = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                var wardrobe = new OfflineCharacterProgressionService(progression, assets).InitializeWardrobe("map14-entity-prefab-wardrobe");
                if (wardrobe.Status == ProgressionResultStatus.InvalidState) throw new InvalidDataException(wardrobe.Message);
                GameObject model = CharacterCreationNativeModelFactory.CreateWorldModel(slot, saves, out string name);
                var runtime = new GameObject("Map14 entity prefab formal runtime").AddComponent<OfficialMainlineMapRuntime>();
                runtime.MapMusicVolume = () => 0; runtime.MapVoiceVolume = () => 0;
                runtime.Begin(14, slot, saves, canvas.transform, model, name, null);
                FieldInfo nativeWorldField = typeof(OfficialMainlineMapRuntime).GetField("nativeMap14World", BindingFlags.Instance | BindingFlags.NonPublic);
                float readyDeadline = Time.realtimeSinceStartup + 90f;
                object nativeWorld = null;
                while (Time.realtimeSinceStartup < readyDeadline)
                {
                    nativeWorld = nativeWorldField?.GetValue(runtime);
                    if (nativeWorld != null && (bool)nativeWorld.GetType().GetProperty("IsReady").GetValue(nativeWorld)) break;
                    yield return new WaitForSecondsRealtime(1f);
                }
                if (nativeWorld == null || !(bool)nativeWorld.GetType().GetProperty("IsReady").GetValue(nativeWorld))
                    throw new InvalidDataException("Original Map14 partition did not finish loading at the player position.");
                yield return new WaitForSecondsRealtime(4);
                var view = runtime.GetComponentInChildren<OfficialMap10EntityView>(true);
                if (view == null || view.EntityCount != 2961) throw new InvalidDataException("Map14 entity placement count is not 2961.");
                var sample = view.FindEntityObject(101);
                if (sample == null || sample.GetComponentsInChildren<Transform>(true).Length < 2)
                    throw new InvalidDataException("Map14 original entity prefab was not instantiated at placement 101.");
                OfficialMap10EntityInfo entry = view.EntityInfos.Single(value => value.PlacementId == 101);
                object localSave = typeof(OfficialMainlineMapRuntime).GetField("localSave", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                int gridX = (int)localSave.GetType().GetField("gridX").GetValue(localSave);
                int gridY = (int)localSave.GetType().GetField("gridY").GetValue(localSave);
                if (entry.Grid == null || entry.Grid.Length != 2 || gridX != entry.Grid[0] || gridY != entry.Grid[1])
                    throw new InvalidDataException("New Map14 save did not spawn at official entry placement 101.");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "map14-entity-prefab-entry.png"));
                typeof(OfficialMainlineMapRuntime).GetMethod("CloseMap", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, null);
                yield return null; yield return null;
                if (errors != 0) throw new InvalidDataException("Map14 entity prefab entry emitted runtime errors: " + errors);
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original entity prefab Play Mode passed: 2961 placements, 416 original prefab roots and entry visual instantiation.");
            EditorApplication.isPlaying = false;
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
