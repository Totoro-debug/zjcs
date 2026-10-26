using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    // Exercises original environment callbacks and async state machine on test fixtures.
    public static class OfficialMap13EnvironmentAcceptance
    {
        private const string Key = "Zjcs.Map13.EnvironmentExecution";
        private static bool started;
        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class Texture { public string path; public Mip[] mips; }
        [Serializable] private sealed class SceneSource { public Texture[] textures; }

        public static void Run()
        {
            OfficialMap13EnvironmentTools.RestoreAndValidate();
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
                Debug.LogError("Map13 environment execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 environment test runner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 environment Play Mode passed: 106 region slots, original registration/day-night, two-second async light fade, cancellation, recession center, scene activation and configuration binding. Test fixtures do not verify native map rendering, mouse input or saves.");
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator VerifySteps()
        {
            var sceneCatalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-scene-catalog");
            Require(sceneCatalog != null && sceneCatalog.Bindings.Length == 202, "Native scene resource catalog is not bound");
            var sourcePrefab = sceneCatalog.Load<GameObject>("Assets/Partition/MapGround_13/MapGround_13_bare");
            Require(sourcePrefab != null && sourcePrefab.GetComponent<MapGroundRoot>().EnvEffect != null,
                "Original bare prefab environment reference is not bound");
            string repository = Path.GetFullPath(Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? "../../.." : "..");
            var sceneSource = JsonUtility.FromJson<SceneSource>(File.ReadAllText(Path.Combine(repository,
                "assets/reference/official-map/map13-native-scene-source-manifest.json")));
            int gpuMips = 0;
            foreach (var source in sceneSource.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source.path.Substring("unity/".Length));
                Require(texture != null, "Original native texture did not load: " + source.path);
                foreach (var mip in source.mips)
                {
                    var readback = AsyncGPUReadback.Request(texture, mip.level);
                    while (!readback.done) yield return null;
                    Require(!readback.hasError, "Native texture GPU readback failed: " + source.path);
                    using (var hash = SHA256.Create())
                        Require(BitConverter.ToString(hash.ComputeHash(readback.GetData<byte>().ToArray())).Replace("-", "").ToLowerInvariant()
                            == mip.rgba_sha256, "Native texture GPU bytes/color/orientation differ: " + source.path + " mip " + mip.level);
                    gpuMips++;
                }
            }
            Debug.Log("Map13 native texture GPU upload verified in Play Mode: " + sceneSource.textures.Length + " textures, "
                + gpuMips + " original mip levels; all RGBA bytes match the decoded source including color/orientation.");
            var host = new GameObject("Original environment lifecycle fixture");
            host.SetActive(false);
            var root = host.AddComponent<MapGroundRoot>();
            var envObject = Child(host, "Environment");
            var env = envObject.AddComponent<MapEnvEffect>();
            env.Day = Child(envObject, "Day");
            env.Night = Child(envObject, "Night");
            root.EnvEffect = env;
            root.Sea = Child(host, "Sea");
            var config = Resources.Load<MapEnvData>("Map13/Native/map-env-data");
            Require(config != null && config.FadeTime == 2f, "Original two-second environment asset is not bound");
            var services = new Services(root, config);
            env.Bind(services);
            host.SetActive(true);
            yield return null;
            Require(env.RegionLights.Length == 106 && env.RegionVfxs.Length == 106 && env.RegionObjects.Length == 106,
                "Original region hub count differs");
            var dayEffect = Effect(host, "Day effect", DayNightState.Day, services);
            var nightEffect = Effect(host, "Night effect", DayNightState.Night, services);
            var dayLight = Light(host, "Day light", DayNightState.Day, services);
            var nightLight = Light(host, "Night light", DayNightState.Night, services);
            var regionObject = Child(host, "Region light");
            regionObject.SetActive(false);
            var region = regionObject.AddComponent<MapRegionLight>();
            region.Region = 1;
            region.RegionShadow = Child(regionObject, "Shadow").AddComponent<Light2D>();
            region.DayNode = Child(regionObject, "Day node");
            region.Bind(services);
            regionObject.SetActive(true);
            yield return null;
            yield return null;
            Require(!dayEffect.activeSelf && nightEffect.activeSelf && !dayLight.gameObject.activeSelf && nightLight.gameObject.activeSelf,
                "Private region registered day/night objects incorrectly");
            Require(env.RegionLights[1].NightLights.Contains(nightLight.Light) && env.RegionLights[1].DayLights.Contains(dayLight.Light),
                "Original light registration lists differ");
            Require(region.RegionShadow.gameObject.activeSelf && !region.DayNode.activeSelf, "Original private region shadow state differs");
            var position = new Vector3(2.25f, -6.75f, 0);
            env.UnlockRegion(position);
            Require(dayEffect.activeSelf && !nightEffect.activeSelf && services.Unlocks == 1 && services.LastRegion == 1,
                "Region unlock environment/visibility dispatch differs");
            Require(region.RegionShadow.RecessionCenter == position, "Original region light recession center differs");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (nightLight.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!nightLight.gameObject.activeSelf && dayLight.gameObject.activeSelf && nightLight.Light.intensity == 0,
                "Original async light fade did not finish");
            var cancelObject = Child(host, "Cancellation fixture");
            var cancelLight = cancelObject.AddComponent<Light2D>();
            cancelLight.intensity = 0.8f;
            var hub = new RegionLightHub();
            hub.NightLights.Add(cancelLight);
            hub.FadeOut(2f);
            yield return null;
            hub.Clear();
            float cancelledIntensity = cancelLight.intensity;
            yield return null;
            yield return null;
            Require(cancelLight.intensity == cancelledIntensity && cancelObject.activeSelf, "Original fade cancellation continued changing state");
            root.OnSceneDeactivate();
            Require(!envObject.activeSelf && !root.Sea.activeSelf, "Original scene deactivation differs");
            root.OnSceneActivate();
            Require(envObject.activeSelf && root.Sea.activeSelf, "Original scene activation differs");
            UnityEngine.Object.Destroy(host);
            yield return null;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static GameObject Effect(GameObject parent, string name, DayNightState state, Services services)
        {
            var child = Child(parent, name);
            child.SetActive(false);
            var effect = child.AddComponent<MapEffect>();
            effect.Region = 1; effect.State = state; effect.Bind(services);
            child.SetActive(true);
            return child;
        }

        private static MapLight Light(GameObject parent, string name, DayNightState state, Services services)
        {
            var child = Child(parent, name);
            child.SetActive(false);
            var light = child.AddComponent<Light2D>();
            light.intensity = 0.65f;
            var component = child.AddComponent<MapLight>();
            component.Light = light; component.Region = 1; component.State = state; component.Bind(services);
            child.SetActive(true);
            return component;
        }

        private sealed class Services : IMapEnvironmentServices<MapGroundRoot, MapEnvData>
        {
            public MapGroundRoot MapGroundRoot { get; }
            public MapEnvData EnvironmentData { get; }
            public int MapClassId => 13;
            public int Unlocks, LastRegion;
            private readonly List<MapRegionStatus> statuses = Enumerable.Repeat(MapRegionStatus.Private, 106).ToList();
            public Services(MapGroundRoot root, MapEnvData data) { MapGroundRoot = root; EnvironmentData = data; statuses[0] = MapRegionStatus.Public; }
            public List<MapRegionStatus> GetCurrentMapStatus() => statuses;
            public Pos2d PosToGrid(Vector3 position) => new Pos2d(2, 6);
            public int QueryRegion(Pos2d position) => 1;
            public void UnlockAllRegionPositions(int mapClassId, int region)
            {
                Require(mapClassId == 13, "Wrong map dispatched for visibility unlock");
                Unlocks++; LastRegion = region;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
