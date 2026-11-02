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

namespace Zjcs.Editor.MainlineNativeGround
{
    // Cross-map check of the production ground Interface. Region/player state
    // is an explicit fixture; this does not test map entry, HUD, input or saves.
    public static class NativeGroundAcceptance
    {
        private const string Key = "Zjcs.Mainline.NativeGroundAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
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
            var steps = Steps();
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
                Debug.Log("Shared native ground Play Mode passed for Map15 and Map16: original component bindings, native geometry, private/public refresh, camera renders, disposal, pipeline restoration and remount. Fixture states only; map entry, timed access, HUD, input, progression saves and Player release are not covered.");
            }
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
                var data = JsonUtility.FromJson<Map14LabelData>(Resources.Load<TextAsset>($"Map{mapId}/Native/region-label-data").text);
                var prefab = Resources.Load<GameObject>($"Map{mapId}/Native/current-ground");
                Require(definition.ClassId == mapId && data.mapClassId == mapId && prefab, "Map-specific native resource identities.");
                Require(data.regions.Length == definition.MaxRegionProgress.Length, "Original region closure.");
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Unbound original script: " + transform.name);
                var entry = definition.Entities.Single(e => e.Id == 101);
                var origin = definition.Navigation.Origin;
                var state = new State { Position = new Vector3(origin.X + entry.Grid[0], origin.Y + entry.Grid[1], origin.Z) };
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
                    data, environment, pipeline, font, _ => true))
                {
                    for (int i = 0; i < 15; i++) yield return null;
                    Require(parent.GetComponentsInChildren<MapGroundRoot>(true).Length == 1, "One original ground root.");
                    Require(parent.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Original native renderers.");
                    Capture(camera, target, Path.Combine(output, $"map{mapId}-entry-private.png"));
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
                    data, environment, pipeline, font, _ => true))
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
        }
        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            RenderTexture.active = previous; File.WriteAllBytes(path, texture.EncodeToPNG());
            Require(texture.GetPixels32().Select(c => c.r + 256 * c.g + 65536 * c.b).Distinct().Take(32).Count() >= 32, "Rendered original scene detail.");
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
            public Vector3 Position; public int PublicRegion = -1, RevealedRegion = -1;
            public Vector3 PlayerWorldPosition => Position;
            public int PlayerLevel => 130;
            public int PlayerSubRank => 12;
            public long PlayerCombatRating => 6000000;
            public bool IsPublicRegion(int region) => region == PublicRegion;
            public int GetRegionProgress(int region) => 0;
            public int GetRegionGoddessId(int region) => 0;
            public void UnlockAllRegionPositions(int region) { RevealedRegion = region; }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
