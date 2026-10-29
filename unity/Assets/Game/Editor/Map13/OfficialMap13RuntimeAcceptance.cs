using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13RuntimeAcceptance
    {
        private const string Key = "Zjcs.Map13.RuntimeAcceptance";
        private static bool started;

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }

        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 runtime acceptance timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13RuntimeAcceptanceRunner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return current;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 mainline runtime entry passed: official map payloads, 2690 entity placements, native player model and isolated local save loaded.");
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator VerifySteps()
        {
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(value => value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            {
                startup.StopAllCoroutines();
                UnityEngine.Object.Destroy(startup.gameObject);
            }
            if (EventSystem.current == null) new GameObject("Map13RuntimeEventSystem", typeof(EventSystem));
            var canvas = new GameObject("Map13RuntimeCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            string root = Path.GetFullPath("../.tools/map13-runtime-acceptance-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "泽之国入口验证";
            save.Player.InitialWeaponType = 200;
            save.Player.Level = 90;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[90];
            save.Player.Rank = 1;
            save.Player.SubRank = 12;
            save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.UnlockedMapClassIds.AddRange(new[] { 11, 12, 13 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var progression = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            new OfflineCharacterProgressionService(progression, OfficialCharacterAssetCatalog.LoadDefault().Load())
                .InitializeWardrobe("map13-runtime-acceptance-wardrobe");
            GameObject avatar = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
            var runtime = new GameObject("Map13Runtime").AddComponent<OfficialMainlineMapRuntime>();
            runtime.MapMusicVolume = () => 0f;
            runtime.MapVoiceVolume = () => 0f;
            runtime.Begin(13, slot, root, canvas.transform, avatar, name, null);
            yield return new WaitForSecondsRealtime(2f);
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            if (view == null || view.EntityCount != 2690)
                throw new InvalidDataException("Map13 runtime did not create the official 2690-entity view.");
            if (runtime.transform.GetComponentsInChildren<Camera>(true).Length == 0)
                throw new InvalidDataException("Map13 runtime did not create a world camera.");
            string output = Path.GetFullPath("../.tools/map13-runtime-acceptance-render");
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "entry.png"));
            Debug.Log("Map13 runtime fixture entity count: " + view.EntityCount + ".");
            yield return null;
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Object.Destroy(canvas);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
