using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using TMPro;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.World.NativeLabelResources;
using Zjcs.Offline.World.NativeMap13Labels;
using Labels = Zjcs.Official.World.NativeMap13Labels;
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

namespace Zjcs.Editor.Map13
{
        // Tests original native renderer assets and texture upload; uses isolated fixtures, never user saves.
    public static class OfficialMap13RegionLabelAcceptance
    {
        private const string Key = "Zjcs.Map13.RegionLabelExecution";
        private static bool started;

        public static void Run()
        {
            OfficialMap13RegionLabelTools.RestoreAndValidate();
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.defaultRenderPipeline = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog")
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
                Debug.LogError("Map13 pipeline execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 pipeline test runner").AddComponent<Runner>().StartCoroutine(Verify());
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
            Debug.Log("Map13 full partition and world-label Play Mode passed: 200 original chunks, 271 original scene prefabs, 106 lights, 105 region descriptions/630 localized text components, original binary language, state/event transitions and DX12 rendering. Real player state, map gameplay/input and saves remain unverified.");
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
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 || QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Original environment fixture requires native DX12 and original Linear settings.");
            string rootPath = OfficialMap13FlagTools.Repository;
            VerifyOriginalContainers();
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(rootPath,
                "assets/reference/official-map/map13-native-region-labels-source-manifest.json")));
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
            var terrain = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-terrain-catalog");
            var bare = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-scene-catalog");
            var environment = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-environment-scene-catalog");
            if (!environment || environment.Bindings.Length != 183) throw new InvalidDataException("Original environment directory is incomplete.");
            var catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();
            var labels = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-region-labels-catalog");
            if (!labels || labels.Bindings.Length != 88) throw new InvalidDataException("Original label directory is incomplete.");
            catalog.Bindings = terrain.Bindings.Concat(bare.Bindings).Concat(environment.Bindings).Concat(labels.Bindings).GroupBy(b => b.OfficialPath).Select(g => g.First()).ToArray();
            
            var originalData = terrain.Load<PartitionData>("Assets/MonoBehaviour/partition_data");
            string originalSerialization = JsonUtility.ToJson(originalData);
            var cameraObject = new GameObject("Original partition rendering camera fixture"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12;
            camera.transform.position = new Vector3(30, -60, -100); camera.farClipPlane = 1000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 1);
            camera.allowHDR = false; camera.allowMSAA = false;
            var additional = cameraObject.AddComponent<UniversalAdditionalCameraData>(); additional.SetRenderer(0); additional.renderPostProcessing = false;
            var target = new RenderTexture(384, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create(); camera.targetTexture = target;
            var requested = new HashSet<string>();
            foreach (var status in new[] { MapRegionStatus.Public, MapRegionStatus.Private })
            {
                var data = UnityEngine.Object.Instantiate(originalData);
                var host = new GameObject("Original partition " + status + " fixture"); host.SetActive(false);
                var instance = UnityEngine.Object.Instantiate(bare.Load<GameObject>("Assets/Partition/MapGround_13/MapGround_13_bare"), host.transform, false);
                var root = instance.GetComponent<MapGroundRoot>();
                var services = new Services(root, Resources.Load<MapEnvData>("Map13/Native/map-env-data"), status);
                var labelServices = new LabelFixture(services);
                labelServices.MapReady = status != MapRegionStatus.Public;
                NativeMap13EnvironmentBinding.Bind(instance, services);
                labelServices.Bind(instance);
                var resources = host.AddComponent<NativeMap13ResourceLoader>(); resources.Catalog = catalog;
                resources.BindInstance = child => { NativeMap13EnvironmentBinding.Bind(child, services); labelServices.Bind(child); };
                var controller = root.PartitionController; var loader = controller.Loader;
                controller.PartitionData = data; controller.Context = services;
                loader.ResourceLoader = new TrackingResources(resources, requested); loader.ServiceProvider = resources;
                loader.IsCameraReady = () => true; loader.IsOtherGameRunning = () => false; loader.SetIgnoreCamera(true);
                host.SetActive(true);
                for (int y = data.MinY; y <= data.MaxY; y++)
                    for (int x = data.MinX; x <= data.MaxX; x++) loader.LoadVicinity(x, y);
                for (int frame = 0; frame < 8; frame++) yield return null;
                if (!labelServices.MapReady)
                {
                    if (root.GetComponentsInChildren<Labels.MapRegionDescItemUI>(true).Any(item => Field(item, "mRegionCfg") != null))
                        throw new InvalidDataException("Original labels initialized before the original map-ready event.");
                    labelServices.InitializeMap(); yield return null;
                }
                if (lifecycleError) throw new InvalidDataException("Original partition Awake/Start/register callback logged an error.");
                var expectedRegions = environment.Bindings.Concat(labels.Bindings).SelectMany(b => ((GameObject)b.Asset).GetComponentsInChildren<MapRegionLight>(true)).Select(l => l.Region).OrderBy(r => r).ToArray();
                var actualRegions = root.EnvEffect.RegionLights.Where(h => h.RegionLight).Select(h => h.RegionLight.Region).OrderBy(r => r).ToArray();
                if (root.EnvEffect.RegionLights.Length != 106 || expectedRegions.Length != 106 || !actualRegions.SequenceEqual(expectedRegions))
                    throw new InvalidDataException("Original region-light registration differs: expected " + expectedRegions.Length + " registered " + actualRegions.Length);
                if (root.GetComponentsInChildren<NativeMap13FlagMesh>(true).Length != 17)
                    throw new InvalidDataException("Original shrine partition did not load through the original controller.");
                VerifyLabels(root.gameObject, labelServices);
                int activeTiles = root.GetComponentsInChildren<Tilemap>(true).Count(t => t.GetUsedTilesCount() > 0);
                if (activeTiles == 0) throw new InvalidDataException("Original partition controller did not fill its original Tilemaps.");
                foreach (var view in new[] { LabelView(root.gameObject), new Vector3(30, -60, -100) })
                {
                    camera.transform.position = view;
                    yield return new WaitForSeconds(0.15f); yield return null; yield return null;
                    var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                    while (!request.done) yield return null;
                    if (request.hasError) throw new InvalidDataException("Original partition DX12 camera readback failed.");
                    var image = new Texture2D(384, 640, TextureFormat.RGBA32, false, true);
                    image.SetPixels32(request.GetData<Color32>().ToArray()); image.Apply();
                    File.WriteAllBytes(Path.Combine(rootPath, ".tools/map13-label-partition-" + status + "-" + (view.x == 30 ? "shrine" : "label") + ".png"), image.EncodeToPNG());
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
                labelServices.Dispose();
                Debug.Log("Map13 original partition fixture passed " + status + ": original native hierarchy, 106 original region-light registrations and 105 world labels, original Tilemaps, shrine animation, scene hide/show and clear.");
            }
            if (!environment.Bindings.Concat(labels.Bindings).All(b => requested.Contains(b.OfficialPath)) || requested.Count(p => p.Contains("/Chunks/")) != 200)
                throw new InvalidDataException("Original controller did not request all 183 non-label prefabs and 200 tile chunks.");
            if (JsonUtility.ToJson(originalData) != originalSerialization) throw new InvalidDataException("Full-scene fixture modified original partition data.");
            Debug.Log("Map13 original environment GPU and controller fixtures passed: " + source.textures.Length + " textures, " + mips + " exact mips, 271 prefab paths and 200 original chunks through native local handles.");
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
            public int MapClassId => 13;
            public int CurrentRegion => 0;
            public Vector3 PlayerWorldPosition => new Vector3(30, -60, 0);
            public event Action<int, List<int>> OnWorldMapProgressUpdate;
            public event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
            private readonly List<MapRegionStatus> statuses;
            public Services(MapGroundRoot root, MapEnvData config, MapRegionStatus status)
            { MapGroundRoot = root; EnvironmentData = config; statuses = Enumerable.Repeat(status, 106).ToList(); }
            public List<MapRegionStatus> GetCurrentMapStatus() => statuses;
            public int GetMapRegionGoddessId(int mapClassId, int region) => 0;
            public int GetWorldMapRegionProgress(int mapClassId, int region) => 0;
            public bool WorldMapRegionCleared(int mapClassId, int region, int progress) => false;
            public Pos2d PosToGrid(Vector3 position) => new Pos2d(0, 0);
            public int QueryRegion(Pos2d position) => 0;
            public void UnlockAllRegionPositions(int mapClassId, int region) { }
        }
        private static object Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static int State(object owner, string field)
        {
            object value = Field(owner, field);
            return Convert.ToInt32(value.GetType().GetProperty("State").GetValue(value));
        }
        private static void VerifyLabels(GameObject root, LabelFixture service)
        {
            VerifyOriginalColors(service);
            var items = root.GetComponentsInChildren<Labels.MapRegionDescItemUI>(true);
            if (items.Length != 105 || root.GetComponentsInChildren<MultiLanguageText>(true).Length != 630)
                throw new InvalidDataException("Original label prefab component closure differs: " + items.Length);
            foreach (var item in items)
            {
                int region = (int)Field(item, "mRegion");
                if (region < 0 || region >= 106) throw new InvalidDataException("Original label world position did not resolve its region.");
                var cfg = (Labels.MapRegionInfo)Field(item, "mRegionCfg");
                if (cfg.Region != region) throw new InvalidDataException("Original region label bound the wrong region.");
                foreach (var text in (List<MultiLanguageText>)Field(item, "mRegionNameTexts"))
                    if (text.Text != service.Lookup.GetText("region_13_" + region, LanguageType.zh_CN)) throw new InvalidDataException("Original region name was not localized.");
            }
            VerifyDescriptionStates(root, service);
            service.SetLevel(100);
            VerifyDescriptionStates(root, service);
            service.SetStats((Labels.SubRank)85, 0);
            foreach (var strength in root.GetComponentsInChildren<Labels.MapRegionStrengthDisplayUI>(true))
                if (strength.gameObject.activeInHierarchy) { strength.enabled = false; strength.enabled = true; }
            VerifyDescriptionStates(root, service);
            foreach (var color in root.GetComponentsInChildren<Labels.UIColorByLevel>(true))
            {
                if (!color.gameObject.activeInHierarchy) continue;
                var strength = color.GetComponent<Labels.MapRegionStrengthDisplayUI>();
                if (!strength) strength = color.GetComponentInParent<Labels.MapRegionStrengthDisplayUI>();
                if (!strength) continue;
                var cfg = (Labels.MapRegionInfo)Field(strength, "mRegionCfg");
                int level = Labels.Map13LabelFormula.GetCombatRatingLevel(service.CombatRating, cfg.CombatRatingLevelValues);
                service.Colors.TryGetLevelColor((Labels.CombatRateingColorType)Field(color, "mCombatRateColorType"), level, out Color expected);
                foreach (var proxy in (List<TextMeshProColorProxy>)Field(color, "mTmpMediators"))
                    if (((TextMeshPro)Field(proxy, "mTextMeshPro")).color != expected.linear)
                        throw new InvalidDataException("Original combat-rating text color did not preserve linear conversion.");
            }
            service.SetProgress(true);
            foreach (var item in items)
                foreach (var text in (List<MultiLanguageText>)Field(item, "mProgressTexts"))
                {
                    string expected = service.RegionIsActived(13, (int)Field(item, "mRegion")) ? "0" : "100";
                    if (text.Text != string.Format(service.Lookup.GetText(text.myKey, LanguageType.zh_CN), expected))
                        throw new InvalidDataException("Original progress text or active-region special case differs.");
                }
            service.SetProgress(false);
            foreach (var item in items) { item.enabled = false; item.enabled = true; }
            // All original world prefabs have an empty progress list. Exercise the
            // retained optional branch on an instance, then restore its original binding.
            var probe = items.First(item => item.mRegionLevelText && item.gameObject.activeInHierarchy);
            var progressField = probe.GetType().GetField("mProgressTexts", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalProgress = (List<MultiLanguageText>)progressField.GetValue(probe);
            var progressText = probe.mRegionLevelText;
            string progressKey = progressText.myKey;
            progressField.SetValue(probe, new List<MultiLanguageText> { progressText });
            service.SetProgress(true);
            int progressRegion = (int)Field(probe, "mRegion");
            string progressValue = service.RegionIsActived(13, progressRegion) ? "0" : "100";
            if (progressText.Text != string.Format(service.Lookup.GetText(progressKey, LanguageType.zh_CN), progressValue))
                throw new InvalidDataException("Original optional progress display lost its active-region value of one.");
            // OnEnable immediately reads current progress and unsubscribes again
            // if it is still complete. Restore the fixture value while disabled.
            probe.enabled = false; service.SetProgress(false); probe.enabled = true;
            if (progressText.Text != string.Format(service.Lookup.GetText(progressKey, LanguageType.zh_CN), "0"))
                throw new InvalidDataException("Original progress update did not resume after re-enable.");
            progressField.SetValue(probe, originalProgress);
            progressText.SetKey(progressKey, ((Labels.MapRegionInfo)Field(probe, "mRegionCfg")).Level);
            service.SetPublic(true);
            foreach (var item in items)
            {
                var slot = (GameObject)Field(item, "mExtendSlot");
                if (slot && slot.activeSelf) throw new InvalidDataException("Original public-region label retained private details.");
            }
            service.SetLevel(1); service.SetStats((Labels.SubRank)5, 100000); service.SetPublic(service.InitialPublic);
            VerifyDescriptionStates(root, service);
            foreach (var text in root.GetComponentsInChildren<TextMeshPro>(true))
                if (text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text))
                {
                    text.ForceMeshUpdate(true, false);
                    if (text.textInfo.characterCount == 0) throw new InvalidDataException("Original text produced no glyphs: " + text.text);
                }
            Debug.Log("Map13 labels passed: 105 original region bindings, 630 localized texts; level limit transitions, progress update/re-enable, public/private details and native TMP glyph generation. No user save fixture is used.");
        }
        private static void VerifyDescriptionStates(GameObject root, LabelFixture service)
        {
            foreach (var strength in root.GetComponentsInChildren<Labels.MapRegionStrengthDisplayUI>(true))
            {
                if (!strength.gameObject.activeInHierarchy) continue;
                var cfg = (Labels.MapRegionInfo)Field(strength, "mRegionCfg");
                var helper = Field(strength, "mRegionDescHelper");
                int expected = service.RegionIsActived(13, cfg.Region) ? 1 : !cfg.ExistLimit() ? 2 : cfg.IsLimited(service.Level) ? 4 : 3;
                if (State(helper, "mRegionStrengthDisplaySwitcher") != expected || State(strength, "mRegionStrengthDisplaySwitcher") != (int)cfg.StrengthDisplayType)
                    throw new InvalidDataException("Original region description/strength state differs: " + cfg.Region);
                if (State(strength, "mRankSuppressionSwitcher") != (service.Data.CalcRankSuppression(service.SubRank, cfg.SubRank) > 0 ? 1 : 0))
                    throw new InvalidDataException("Original rank suppression style differs: " + cfg.Region);
            }
        }
        private static Vector3 LabelView(GameObject root)
        {
            var text = root.GetComponentsInChildren<MultiLanguageText>(true).First(t => t.myKey == "region_13_0");
            Vector3 p = text.transform.position; return new Vector3(p.x, p.y, -100);
        }
        private sealed class LabelFixture : Labels.IMap13LabelServices, Labels.IMap13LabelPlayer, ILanguageTypeService, IDisposable
        {
            private readonly Services environment;
            private readonly MultiLanguageUIService ui;
            private readonly Map13LabelFontInstance font;
            private bool? publicOverride;
            private bool fullProgress;
            public bool MapReady = true;
            private readonly string sourceFontHash, sourceAtlasHash;
            private const string FontPath = "Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset";
            private const string AtlasPath = "Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic-Atlas.texture.asset";
            public LocalizationBinaryTextService Lookup { get; }
            public Labels.Map13LabelData Data { get; }
            public Labels.CombatRatingConfig Colors { get; }
            public bool InitialPublic => (int)environment.GetCurrentMapStatus()[0] == (int)Labels.MapRegionStatus.Public;
            public LabelFixture(Services environment)
            {
                this.environment = environment;
                Data = JsonUtility.FromJson<Labels.Map13LabelData>(Resources.Load<TextAsset>("Map13/Native/region-label-data").text);
                if (Data.manifest != "871_156598" || Data.regions.Length != 106 || Data.maxProgress.Length != 106)
                    throw new InvalidDataException("Original Map13 label configuration is incomplete.");
                foreach (var region in Data.regions) region.ValidateScope();
                Colors = Resources.Load<Labels.CombatRatingConfig>("Map13/Native/combat-rating-config");
                if (!Colors) throw new InvalidDataException("Original combat color config is not bound.");
                sourceFontHash = Hash(File.ReadAllBytes(FontPath)); sourceAtlasHash = Hash(File.ReadAllBytes(AtlasPath));
                font = new Map13LabelFontInstance(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
                Lookup = new LocalizationBinaryTextService(this, null, new ILanguageTextDataProvider[] { new LaunchLanguageBinaryTextDataProvider("Assets/Launch/") }, new LocalLanguageFiles(Application.streamingAssetsPath));
                Lookup.AddDataProvider(new GameLanguageBinaryTextDataProvider("Assets/Config/"));
                LanguageManager.Initialize(Lookup);
                ui = new MultiLanguageUIService(this);
            }
            public void Bind(GameObject instance) => Map13LabelBinding.Bind(instance, this, ui, this, Lookup, font);
            public void Dispose()
            {
                if (OnMapStatusUpdate != null || OnWorldMapProgressUpdate != null || OnCombatRatingChanged != null || OnLevelUpdate != null || OnPlanesRankUnlock != null || OnDebugTimeOffset != null || Initialized != null)
                    throw new InvalidDataException("Original label destruction retained player/map subscriptions.");
                ui.Dispose(); Lookup.Dispose(); font.Dispose();
                if (Hash(File.ReadAllBytes(FontPath)) != sourceFontHash || Hash(File.ReadAllBytes(AtlasPath)) != sourceAtlasHash)
                    throw new InvalidDataException("Dynamic label rendering changed original packaged font assets.");
                if (OnLanguageChanged != null) throw new InvalidDataException("Original language destruction retained subscriptions.");
            }
            public int MapClassId => 13;
            public object Map => MapReady ? environment.MapGroundRoot : null;
            public event Action Initialized;
            public void InitializeMap() { MapReady = true; Initialized?.Invoke(); }
            public Labels.IMap13LabelPlayer Player => this;
            public bool IsTeamMember => false;
            public int QueryRegion(Vector3 p) => Data.QueryRegion(p);
            public Labels.MapRegionInfo GetRegion(int map, int region) => Data.regions.Single(r => r.ClassId == map && r.Region == region);
            public DateTime Now => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            public double CalcRemainSecond(DateTime end) => (end - Now).TotalSeconds;
            public int Level { get; private set; } = 1;
            public Labels.SubRank SubRank { get; private set; } = (Labels.SubRank)5;
            public long CombatRating { get; private set; } = 100000;
            public event Action<int> OnLevelUpdate;
            public event Action<Labels.Rank> OnPlanesRankUnlock;
            public event Action<long, long> OnCombatRatingChanged;
            public event Action<int, List<int>> OnWorldMapProgressUpdate;
            public event Action<int, int, Labels.MapRegionStatus, Labels.MapRegionStatus> OnMapStatusUpdate;
            public event Action<long> OnDebugTimeOffset;
            public bool RegionIsActived(int map, int region) => GetMapRegionStatus(map, region) == Labels.MapRegionStatus.Public;
            public Labels.MapRegionStatus GetMapRegionStatus(int map, int region) => map != 13 || region < 0 || region >= 106 ? Labels.MapRegionStatus.None : publicOverride.HasValue ? (publicOverride.Value ? Labels.MapRegionStatus.Public : Labels.MapRegionStatus.Private) : (Labels.MapRegionStatus)(int)environment.GetCurrentMapStatus()[region];
            public int GetWorldMapRegionProgress(int map, int region) => region < 0 ? 0 : fullProgress ? Data.maxProgress[region] : 0;
            public void SetLevel(int value) { Level = value; OnLevelUpdate?.Invoke(value); }
            public void SetStats(Labels.SubRank subRank, long combatRating)
            { SubRank = subRank; long previous = CombatRating; CombatRating = combatRating; OnCombatRatingChanged?.Invoke(previous, combatRating); }
            public void SetProgress(bool full) { fullProgress = full; OnWorldMapProgressUpdate?.Invoke(13, Data.regions.Select(r => GetWorldMapRegionProgress(13, r.Region)).ToList()); }
            public void SetPublic(bool value)
            {
                var previous = Data.regions.Select(r => GetMapRegionStatus(13, r.Region)).ToArray();
                publicOverride = value;
                foreach (var region in Data.regions) OnMapStatusUpdate?.Invoke(13, region.Region, previous[region.Region], GetMapRegionStatus(13, region.Region));
            }
            private LanguageType language = LanguageType.zh_CN;
            public LanguageType CurrentLanguage { get => language; set { language = value; OnLanguageChanged?.Invoke(value); } }
            public bool IsShowLanguageKey { get; set; }
            public bool IsShowMissingKeyText { get; set; }
            public event Action<LanguageType> OnLanguageChanged;
        }
        [Serializable] private sealed class ColorPair { public int Key; public Color Value; }
        [Serializable] private sealed class ColorBuffer { public ColorPair[] mSerializeBuffer; }
        [Serializable] private sealed class ThemePair { public int Key, Value; }
        [Serializable] private sealed class ThemeBuffer { public ThemePair[] mSerializeBuffer; }
        [Serializable] private sealed class Theme { public ColorBuffer LevelColors; }
        [Serializable] private sealed class ColorSource { public ThemeBuffer mTypeToThemes; public Theme[] mColorTemplates; }
        [Serializable] private sealed class DictionaryFixture { public Labels.MonoDictionary<int, string> Value; }
        private static void VerifyOriginalColors(LabelFixture service)
        {
            var source = JsonUtility.FromJson<ColorSource>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-label-combat-colors.json"));
            foreach (var theme in source.mTypeToThemes.mSerializeBuffer.GroupBy(p => p.Key).Select(g => g.First()))
                foreach (var level in source.mColorTemplates[theme.Value].LevelColors.mSerializeBuffer.GroupBy(p => p.Key).Select(g => g.First()))
                    if (!service.Colors.TryGetLevelColor((Labels.CombatRateingColorType)theme.Key, level.Key, out var actual) || actual != level.Value)
                        throw new InvalidDataException("Original color config or serialized dictionary differs: " + theme.Key + ":" + level.Key);
            foreach (var region in service.Data.regions)
            {
                foreach (int progress in new[] { 0, 1, service.Data.maxProgress[region.Region] - 1, service.Data.maxProgress[region.Region] })
                {
                    int max = service.Data.maxProgress[region.Region];
                    int expected = max == 0 ? 0 : (int)((double)((float)progress / (float)max) * 10000.0);
                    if (service.Data.CalcWorldMapRegionProgress(13, region.Region, progress) != expected)
                        throw new InvalidDataException("Original progress float/double conversion differs.");
                }
                if (region.HasCombatRatingLevelValues)
                    for (int i = 0; i < region.CombatRatingLevelValues.Count; i++)
                    {
                        long threshold = region.CombatRatingLevelValues[i];
                        int expected = region.CombatRatingLevelValues.Count(v => threshold < v);
                        if (Labels.Map13LabelFormula.GetCombatRatingLevel(threshold, region.CombatRatingLevelValues) != expected)
                            throw new InvalidDataException("Original combat threshold equality differs.");
                    }
            }
        }
        private static void VerifyOriginalContainers()
        {
            var dictionary = JsonUtility.FromJson<DictionaryFixture>("{\"Value\":{\"mSerializeBuffer\":[{\"Key\":1,\"Value\":\"first\"},{\"Key\":1,\"Value\":\"last\"},{\"Key\":2,\"Value\":\"second\"}]}}").Value;
            if (dictionary.Count != 2 || dictionary[1] != "first" || dictionary.Remove(1) || dictionary.ContainsKey(1) || !dictionary.Remove(2, out var value) || value != "second")
                throw new InvalidDataException("Original MonoDictionary first-entry or double-remove behavior differs from managed IL.");
            var action = new Labels.GAction<int>(); var calls = new List<string>(); bool changed = false;
            Action<int> second = _ => calls.Add("second");
            action += _ => { calls.Add("first"); if (!changed) { changed = true; action -= second; action += __ => calls.Add("new"); } };
            action += second;
            action.Invoke(0);
            if (!calls.SequenceEqual(new[] { "first" })) throw new InvalidDataException("Original GAction ran a removed/new observer during dispatch.");
            calls.Clear(); action.Invoke(1);
            if (!calls.SequenceEqual(new[] { "first", "new" })) throw new InvalidDataException("Original GAction lost its next-dispatch observer.");
        }
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
