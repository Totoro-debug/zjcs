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
using Zjcs.Offline.World.NativePartition;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativeMap14;

namespace Zjcs.Editor.Map14
{
        // Tests original native renderer assets and texture upload; uses isolated fixtures, never user saves.
    public static partial class OfficialMap14EnvironmentSceneAcceptance
    {
        private const string Key = "Zjcs.Map14.EnvironmentSceneExecution";
        private static bool started;

        public static void Run() { RunCore(); }
        private static void RunCore()
        {
            OfficialMap14EnvironmentSceneTools.RestoreAndValidate();
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline = OfficialMap14EnvironmentSceneTools.SharedAsset<NativeMap13AssetCatalog>(true)
                .Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
            QualitySettings.renderPipeline = null;
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
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
            PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map14 pipeline execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map14 pipeline test runner").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Map14 native environment Play Mode passed: 240 original tile chunks and 389 original non-label scene resources through original partition/controller/environment callbacks, public/private fixtures, native texture GPU upload and rendering. The 97 world labels, real world state, map interaction and saves remain unverified.");
            EditorApplication.isPlaying = false;
        }


        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class Texture { public string path; public Mip[] mips; }
        [Serializable] private sealed class Source { public Texture[] textures; }
        private static bool lifecycleError;
        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) lifecycleError = true;
        }

        private static IEnumerator VerifySteps()
        {
            lifecycleError = false; Application.logMessageReceived += Observe;
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Original environment fixture requires original Linear settings.");
            OfficialMap14ButterflyChecks.Validate();
            yield return null;
            string rootPath = OfficialMap14EnvironmentSceneTools.Repository;
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(rootPath,
                "assets/reference/official-map/map14-native-environment-scene-source-manifest.json")));
            var bareSource = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(rootPath,
                "assets/reference/official-map/map14-native-scene-source-manifest.json")));
            source.textures = source.textures.Concat(bareSource.textures).ToArray();
            source.textures = source.textures.Concat(JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(rootPath,
                "assets/reference/official-map/map14-native-butterflies-source-manifest.json"))).textures).ToArray();
            int mips = 0;
            foreach (var record in source.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(record.path.Substring(6));
                foreach (var mip in record.mips)
                {
                    var request = AsyncGPUReadback.Request(texture, mip.level);
                    while (!request.done) yield return null;
                    if (request.hasError || Hash(request.GetData<byte>().ToArray()) != mip.rgba_sha256)
                        throw new InvalidDataException("Original environment native texture/mip bytes differ: " + record.path);
                    mips++;
                }
            }
            var terrain = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-terrain-catalog");
            var bare = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-scene-catalog");
            var environment = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-environment-scene-catalog");
            if (!environment || environment.Bindings.Length != 389) throw new InvalidDataException("Original environment directory is incomplete.");
            var catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();
            catalog.Bindings = terrain.Bindings.Concat(bare.Bindings).Concat(environment.Bindings).GroupBy(b => b.OfficialPath).Select(g => g.First()).ToArray();

            var allowed = new HashSet<string>(catalog.Bindings.Select(b => b.OfficialPath));
            var originalData = terrain.Load<PartitionData>("Assets/MonoBehaviour/partition_data");
            string originalSerialization = JsonUtility.ToJson(originalData);
            var cameraObject = new GameObject("Original partition rendering camera fixture"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12;
            camera.transform.position = new Vector3(0, -40, -100); camera.farClipPlane = 1000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 1);
            camera.allowHDR = false; camera.allowMSAA = false;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>(); additional.SetRenderer(0); additional.renderPostProcessing = false;
            var target = new RenderTexture(384, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create(); camera.targetTexture = target;
            var requested = new HashSet<string>();
            foreach (var status in new[] { MapRegionStatus.Public, MapRegionStatus.Private })
            {
                // This is an explicit partial-scene fixture. Clone the original data
                // and omit only the 97 unconverted world-label resource entries.
                var data = UnityEngine.Object.Instantiate(originalData);
                {
                data.SceneChunks.RemoveAll(c => !allowed.Contains(c.Res));
                data.Extensions.RemoveAll(c => !allowed.Contains(c.Res));
                foreach (var region in data.RegionDatas)
                    foreach (var layer in region.LayerDatas ?? Array.Empty<PartitionData.LayerData>())
                    {
                        layer?.Scenes?.RemoveAll(c => !allowed.Contains(c.Res));
                        layer?.Extensions?.RemoveAll(c => !allowed.Contains(c.Res));
                    }
                }
                var host = new GameObject("Original partition " + status + " fixture"); host.SetActive(false);
                var instance = UnityEngine.Object.Instantiate(bare.Load<GameObject>("Assets/Partition/MapGround_14/MapGround_14_bare"), host.transform, false);
                var root = instance.GetComponent<MapGroundRoot>();
                var services = new Services(root, OfficialMap14EnvironmentSceneTools.SharedAsset<MapEnvData>(false), status, data.RegionDatas.Length);
                NativeMap13EnvironmentBinding.Bind(instance, services);
                var resources = host.AddComponent<NativeMap13ResourceLoader>(); resources.Catalog = catalog;
                resources.BindInstance = child => NativeMap13EnvironmentBinding.Bind(child, services);
                var controller = root.PartitionController; var loader = controller.Loader;
                controller.PartitionData = data; controller.Context = services;
                loader.ResourceLoader = new TrackingResources(resources, requested); loader.ServiceProvider = resources;
                loader.IsCameraReady = () => true; loader.IsOtherGameRunning = () => false; loader.SetIgnoreCamera(true);
                host.SetActive(true);
                for (int y = data.MinY; y <= data.MaxY; y++)
                    for (int x = data.MinX; x <= data.MaxX; x++) loader.LoadVicinity(x, y);
                for (int frame = 0; frame < 8; frame++) yield return null;
                if (lifecycleError) throw new InvalidDataException("Original partition Awake/Start/register callback logged an error.");
                var expectedRegions = environment.Bindings.SelectMany(b => ((GameObject)b.Asset).GetComponentsInChildren<MapRegionLight>(true)).Select(l => l.Region).OrderBy(r => r).ToArray();
                var actualRegions = root.EnvEffect.RegionLights.Where(h => h.RegionLight).Select(h => h.RegionLight.Region).OrderBy(r => r).ToArray();
                if (root.EnvEffect.RegionLights.Length != originalData.RegionDatas.Length || !actualRegions.SequenceEqual(expectedRegions))
                    throw new InvalidDataException("Original region-light registration differs: expected " + expectedRegions.Length + " registered " + actualRegions.Length);

                int activeTiles = root.GetComponentsInChildren<Tilemap>(true).Count(t => t.GetUsedTilesCount() > 0);
                if (activeTiles == 0) throw new InvalidDataException("Original partition controller did not fill its original Tilemaps.");
                var butterflies = root.GetComponentsInChildren<NativeMap14ButterflyMesh>(true);
                if (butterflies.Length == 0 || butterflies.Any(b => b.isActiveAndEnabled && !b.DeformedMesh))
                    throw new InvalidDataException("Original partition butterfly meshes did not initialize.");
                Debug.Log("Map14 original partition butterfly instances " + status + ": " + butterflies.Length);
                foreach (var group in butterflies.Where(b => b.isActiveAndEnabled).GroupBy(b => b.skeletonDataAsset.ModelName))
                {
                    var butterfly = group.First();
                    camera.orthographicSize = .8f;
                    camera.transform.position = butterfly.transform.position + new Vector3(0, 0, -100);
                    yield return null; yield return null;
                    float before = butterfly.Elapsed;
                    yield return new WaitForSeconds(.2f); yield return null;
                    if (butterfly.Elapsed <= before || !butterfly.GetComponent<MeshRenderer>().isVisible)
                        throw new InvalidDataException("Original visible butterfly animation clock did not advance: " + group.Key);
                    var readback = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                    while (!readback.done) yield return null;
                    if (readback.hasError) throw new InvalidDataException("Original butterfly camera readback failed.");
                    var closeup = new Texture2D(384, 640, TextureFormat.RGBA32, false, true);
                    closeup.SetPixels32(readback.GetData<Color32>().ToArray()); closeup.Apply();
                    File.WriteAllBytes(Path.Combine(rootPath, "output/map14-butterfly-" + status + "-" + group.Key + ".png"), closeup.EncodeToPNG());
                    UnityEngine.Object.Destroy(closeup);
                }
                camera.orthographicSize = 12;
                foreach (var view in new[] { new Vector3(0, -40, -100), new Vector3(0, 0, -100) })
                {
                    camera.transform.position = view;
                    yield return new WaitForSeconds(0.15f); yield return null; yield return null;
                    var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                    while (!request.done) yield return null;
                    if (request.hasError) throw new InvalidDataException("Original partition camera readback failed.");
                    var image = new Texture2D(384, 640, TextureFormat.RGBA32, false, true);
                    image.SetPixels32(request.GetData<Color32>().ToArray()); image.Apply();
                    File.WriteAllBytes(Path.Combine(rootPath, "output/map14-partition-" + status + "-" + (view.y == 0 ? "center" : view.x == 0 ? "mountain" : "region") + "-20261027.png"), image.EncodeToPNG());
                    UnityEngine.Object.Destroy(image);
                }
                root.OnSceneDeactivate(); yield return null;
                if (root.Sea.activeSelf || loader.ChunkHost.gameObject.activeSelf || loader.ExtensionHost.gameObject.activeSelf)
                    throw new InvalidDataException("Original complete scene deactivation did not hide its partition hosts.");
                root.OnSceneActivate(); yield return null; yield return null;
                if (!root.Sea.activeSelf || !loader.ChunkHost.gameObject.activeSelf || !loader.ExtensionHost.gameObject.activeSelf)
                    throw new InvalidDataException("Original scene activation did not restore its partition hosts.");
                foreach (var chunk in data.TileChunks) loader.RemoveAsset(chunk.Pos, chunk.Id, chunk.Res, ChunkAssetType.Tile);
                foreach (var chunk in data.SceneChunks) loader.RemoveAsset(chunk.Pos, chunk.Id, chunk.Res, ChunkAssetType.Scene);
                foreach (var extension in data.Extensions) loader.RemoveExtensionAsset(extension.Chunks, extension.Id, extension.Res);
                foreach (var region in data.RegionDatas)
                {
                    controller.RemoveLayer(region.GetLayerData(PartitionLayer.Day));
                    if (status == MapRegionStatus.Private) controller.RemoveLayer(region.GetLayerData(PartitionLayer.Night));
                }
                yield return null; yield return null;
                if (loader.ChunkHost.childCount != 0 || loader.ExtensionHost.childCount != 0 || root.EnvEffect.RegionLights.Any(h => h.RegionLight))
                    throw new InvalidDataException("Original partition removal retained scene instances or registered region lights.");
                UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(data); yield return null; yield return null;
                if (lifecycleError) throw new InvalidDataException("Original partition clear/destroy callbacks logged an error.");

                Debug.Log("Map14 original partition fixture passed " + status + ": native hierarchy, " + expectedRegions.Length + " region lights; Tilemaps, scene hide/show, complete clear and label subscription release.");
            }
            if (!environment.Bindings.All(b => requested.Contains(b.OfficialPath)) || requested.Count(p => p.Contains("/Chunks/")) != 240)
                throw new InvalidDataException("Original controller did not request all 389 non-label prefabs and 240 tile chunks.");

            if (JsonUtility.ToJson(originalData) != originalSerialization) throw new InvalidDataException("Partial-scene fixture modified original partition data.");
            Debug.Log("Map14 original environment GPU and controller fixtures passed: " + source.textures.Length + " textures, " + mips + " exact mips, 389 prefab paths and 240 original chunks through native local handles.");
            Application.logMessageReceived -= Observe;
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(catalog);
        }

        private sealed class TrackingResources : IPartitionResourceLoader
        {
            private readonly NativeMap13ResourceLoader resources;
            private readonly HashSet<string> requested;
            public TrackingResources(NativeMap13ResourceLoader resources, HashSet<string> requested) { this.resources = resources; this.requested = requested; }
            public PartitionAssetHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
            { requested.Add(path); return resources.LoadAssetAsync<T>(path); }
        }
        private sealed class Services : IMapEnvironmentServices<MapGroundRoot, MapEnvData>, IPartitionWorldContext<MapRegionStatus>
        {
            public MapGroundRoot MapGroundRoot { get; }
            public MapEnvData EnvironmentData { get; }
            public int MapClassId => 14;
            public int CurrentRegion => 0;
            public Vector3 PlayerWorldPosition => new Vector3(0, -40, 0);
            public event Action<int, List<int>> OnWorldMapProgressUpdate;
            public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
            private readonly List<MapRegionStatus> statuses;
            public Services(MapGroundRoot root, MapEnvData config, MapRegionStatus status, int regionCount)
            { MapGroundRoot = root; EnvironmentData = config; statuses = Enumerable.Repeat(status, regionCount).ToList(); }
            public List<MapRegionStatus> GetCurrentMapStatus() => statuses;
            public int GetMapRegionGoddessId(int mapClassId, int region) => 0;
            public int GetWorldMapRegionProgress(int mapClassId, int region) => 0;
            public bool WorldMapRegionCleared(int mapClassId, int region, int progress) => false;
            public Pos2d PosToGrid(Vector3 position) => new Pos2d(0, 0);
            public int QueryRegion(Pos2d position) => 0;
            public void UnlockAllRegionPositions(int mapClassId, int region) { }
        }
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
