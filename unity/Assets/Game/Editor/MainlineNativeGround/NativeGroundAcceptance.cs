using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.World;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Presentation.World;
using Zjcs.Offline.World;
using Zjcs.Offline.Progression;

namespace Zjcs.Editor.MainlineNativeGround
{
    // Cross-map check of the production ground Interface. Region/player state
    // is an explicit fixture; this does not test map entry, HUD, input or saves.
    public static partial class NativeGroundAcceptance
    {
        private const string Key = "Zjcs.Mainline.NativeGroundAcceptance";
        private static bool started;
        private static int errors;
        public static void RunLabels()
        {
            SessionState.SetBool(Key + ".labels", true);
            Run();
        }
        public static void Run()
        {
            Require(TMP_Settings.instance, "The original TMP package settings must be staged before text Awake.");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            if (Environment.GetEnvironmentVariable("ZJCS_ISOLATED_NATIVE_GROUND") == "1")
                Debug.Log("Explicit native ground assembly isolation; full ProjectTools.Validate is not executed in this project.");
            else Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 360);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".labels", false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Shared native ground acceptance timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Shared original ground verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            bool labels = SessionState.GetBool(Key + ".labels", false);
            var steps = labels ? VerifyLabelContext() : Steps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log(labels ? "Shared label context Play Mode passed: original Map13/16 data, region-ID state/events, copied configuration, independent clocks, rank bypass and disposal. Original rendering is checked by the Map13 world host fixture; formal minimap integration, input, saves and Player release are not covered."
                    : "Shared native ground Play Mode passed for Map15 and Map16: original bindings, geometry, region activation, timed labels, camera renders, disposal, pipeline restoration and remount. Timed-region rules/profile cold save are checked separately; formal map entry, HUD, input, world progression saves and Player release are not covered.");
            }
            SessionState.SetBool(Key + ".labels", false);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? throw new InvalidOperationException("Explicit repository required.");
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidOperationException("Owned validation workspace required.");
            // Use exactly the three installed dependencies supplied by the
            // production caller, without loading unrelated Map12 terrain.
            string shared = File.ReadAllText(Path.Combine(repository, "unity/Assets/Game/Resources/Map12/Native/world-assets.asset"));
            var pipeline = Shared<UniversalRenderPipelineAsset>(shared, "Pipeline");
            var environment = Shared<MapEnvData>(shared, "EnvironmentData");
            var font = Shared<TMP_FontAsset>(shared, "Font");
            foreach (int mapId in new[] { 15, 16 })
            {
                string raw = File.ReadAllText(Path.Combine(repository, $"unity/Assets/Official/World/Map/{mapId}/map{mapId}.json"));
                var definition = JsonUtility.FromJson<OfficialMapDefinition>(raw);
                if (mapId == 16) VerifyRegionTimes(definition, output);
                var data = JsonUtility.FromJson<Map14LabelData>(Resources.Load<TextAsset>($"Map{mapId}/Native/region-label-data").text);
                var prefab = Resources.Load<GameObject>($"Map{mapId}/Native/current-ground");
                Require(definition.ClassId == mapId && data.mapClassId == mapId && prefab, "Map-specific native resource identities.");
                Require(data.regions.Length == definition.MaxRegionProgress.Length, "Original region closure.");
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Unbound original script: " + transform.name);
                var entry = definition.Entities.Single(e => e.Id == 101);
                var origin = definition.Navigation.Origin;
                var state = new State { Position = new Vector3(origin.X + entry.Grid[0], origin.Y + entry.Grid[1], origin.Z) };
                if (mapId == 16) state.SubRank = Array.FindIndex(data.subRankToRank, rank => rank == 4);
                var clock = DateTime.Now;
                var rankPlayer = new OfflinePlayerProgress { Rank = 4 };
                rankPlayer.RankUnlockTimes.Add(new OfflineRankUnlockTime
                    { Rank = 4, UnixSeconds = new DateTimeOffset(clock.AddDays(-13)).ToUnixTimeSeconds() });
                IMapRegionTime regionTime = mapId == 16 ? new OfflineCharacterRankRegionTime(definition, () => rankPlayer, () => clock) : null;
                RenderPipelineAsset previousPipeline = GraphicsSettings.defaultRenderPipeline, previousQuality = QualitySettings.renderPipeline;
                var cameraObject = new GameObject("Original ground render fixture"); cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .12f, 1);
                camera.farClipPlane = 1000; camera.allowHDR = false; camera.allowMSAA = false;
                camera.transform.position = state.Position + new Vector3(0, 0, -250);
                var target = new RenderTexture(432, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target;
                var parent = new GameObject("Original native ground fixture parent");
                using (var view = new Zjcs.Presentation.World.MainlineNativeGround(parent.transform, prefab, camera, state,
                    data, environment, pipeline, font, mapId == 15 ? (Func<int, bool>)(_ => true) : null, regionTime))
                {
                    for (int i = 0; i < 15; i++) yield return null;
                    Require(parent.GetComponentsInChildren<MapGroundRoot>(true).Length == 1, "One original ground root.");
                    Require(parent.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Original native renderers.");
                    Capture(camera, target, Path.Combine(output, $"map{mapId}-entry-private.png"));
                    if (mapId == 16)
                    {
                        var timers = parent.GetComponentsInChildren<ShowRemainTimeInfo>();
                        Require(timers.Any(timer => timer.mRemainTimeText && !string.IsNullOrEmpty(
                            timer.mRemainTimeText.GetComponent<TMP_Text>().text)), "Original active countdown text.");
                        foreach (var timer in timers.Where(value => value.mRemainTimeText.gameObject.activeInHierarchy))
                        {
                            var text = timer.mRemainTimeText.GetComponent<TMP_Text>();
                            text.ForceMeshUpdate();
                            Require(text.textInfo != null && text.textInfo.characterCount > 0 && text.mesh.vertexCount > 0,
                                "Original active countdown generates its glyph geometry.");
                        }
                        var label = parent.GetComponentsInChildren<MapRegionDescSupportUI>().First(value =>
                        {
                            int region = value.Services.QueryRegion(value.transform.position);
                            return region >= 0 && !regionTime.IsUnlockTimeReached(region);
                        });
                        Vector3 entryCamera = camera.transform.position;
                        float entrySize = camera.orthographicSize;
                        camera.transform.position = new Vector3(label.transform.position.x, label.transform.position.y, entryCamera.z);
                        camera.orthographicSize = 4;
                        yield return null; yield return null;
                        Capture(camera, target, Path.Combine(output, "map16-time-locked.png"));
                        int timedRegion = label.Services.QueryRegion(label.transform.position);
                        var selectedTimers = label.GetComponentsInChildren<ShowRemainTimeInfo>()
                            .Where(value => value.mRemainTimeText.gameObject.activeInHierarchy).ToArray();
                        Require(selectedTimers.Length > 0, "Selected original region has an active time lock.");
                        clock = regionTime.GetUnlockTime(timedRegion);
                        // Let the original UniTask/provider refresh on its own;
                        // no view.Refresh or business callback drives expiry.
                        yield return new WaitForSecondsRealtime(1.25f);
                        Require(selectedTimers.All(value => !value.mRemainTimeText.gameObject.activeInHierarchy),
                            "The original timed provider removes the region time lock at expiry.");
                        Capture(camera, target, Path.Combine(output, "map16-time-expired.png"));
                        var laterLabel = parent.GetComponentsInChildren<MapRegionDescSupportUI>().First(value =>
                        {
                            int region = value.Services.QueryRegion(value.transform.position);
                            return region >= 0 && !regionTime.IsUnlockTimeReached(region)
                                && value.GetComponentsInChildren<ShowRemainTimeInfo>().Any(timer => timer.mRemainTimeText.gameObject.activeInHierarchy);
                        });
                        camera.transform.position = new Vector3(laterLabel.transform.position.x, laterLabel.transform.position.y, entryCamera.z);
                        Capture(camera, target, Path.Combine(output, "map16-time-rank-bypass-locked.png"));
                        rankPlayer.Rank = 5;
                        state.SubRank = Array.FindIndex(data.subRankToRank, rank => rank == 5);
                        view.Refresh();
                        yield return null; yield return null;
                        Capture(camera, target, Path.Combine(output, "map16-time-higher-rank.png"));
                        camera.transform.position = entryCamera;
                        camera.orthographicSize = entrySize;
                    }
                    state.PublicRegion = entry.Region;
                    if (mapId == 16)
                    {
                        view.UnlockRegion(state.Position);
                        Require(state.RevealedRegion == entry.Region, "Original activation reveals the selected region through its callback.");
                    }
                    else view.Refresh();
                    yield return new WaitForSecondsRealtime(5.25f);
                    Capture(camera, target, Path.Combine(output, $"map{mapId}-entry-public.png"));
                }
                yield return null; yield return null;
                Require(parent.transform.childCount == 0, "Disposed original instance.");
                Require(GraphicsSettings.defaultRenderPipeline == previousPipeline && QualitySettings.renderPipeline == previousQuality, "Restore previous render pipelines.");
                Require(!camera.GetComponent<UniversalAdditionalCameraData>(), "Release added camera data.");
                using (var view = new Zjcs.Presentation.World.MainlineNativeGround(parent.transform, prefab, camera, state,
                    data, environment, pipeline, font, mapId == 15 ? (Func<int, bool>)(_ => true) : null, regionTime))
                {
                    for (int i = 0; i < 15; i++) yield return null;
                    Capture(camera, target, Path.Combine(output, $"map{mapId}-remount.png"));
                }
                yield return null; yield return null;
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(parent);
                yield return null;
                Require(errors == 0, "Original ground lifecycle logged errors for Map" + mapId);
            }
            var miniMapSteps = VerifyMiniMapRegions(output, pipeline);
            while (miniMapSteps.MoveNext()) yield return miniMapSteps.Current;
        }
        private static void Capture(Camera camera, RenderTexture target, string path, int minimumColors = 32)
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            RenderTexture.active = previous; File.WriteAllBytes(path, texture.EncodeToPNG());
            Require(texture.GetPixels32().Select(c => c.r + 256 * c.g + 65536 * c.b).Distinct().Take(minimumColors).Count() >= minimumColors, "Rendered original scene detail.");
            UnityEngine.Object.Destroy(texture);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private static T Shared<T>(string binding, string field) where T : UnityEngine.Object
        {
            string guid = Regex.Match(binding, @"(?m)^  " + field + @":.*guid: ([a-f0-9]{32})").Groups[1].Value;
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (!asset) throw new InvalidDataException("Installed original ground dependency missing: " + field);
            return asset;
        }
        private sealed class State : IMainlineWorldGroundState
        {
            public Vector3 Position; public int PublicRegion = -1, RevealedRegion = -1, SubRank = 12;
            public Vector3 PlayerWorldPosition => Position;
            public int PlayerLevel => 130;
            public int PlayerSubRank => SubRank;
            public long PlayerCombatRating => 6000000;
            public bool IsPublicRegion(int region) => region == PublicRegion;
            public int GetRegionProgress(int region) => 0;
            public int GetRegionGoddessId(int region) => 0;
            public void UnlockAllRegionPositions(int region) { RevealedRegion = region; }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
