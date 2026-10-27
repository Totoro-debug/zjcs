using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativeMap14Labels;
using Zjcs.Offline.World.NativePartition;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.World.NativeMap12;
using TerrainRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using LabelRegionStatus = Zjcs.Official.World.NativeMap14Labels.MapRegionStatus;

namespace Zjcs.Presentation.World.NativeMap14
{
    public interface IMap14NativeWorldState
    {
        Vector3 PlayerWorldPosition { get; }
        int PlayerLevel { get; }
        int PlayerSubRank { get; }
        long PlayerCombatRating { get; }
        bool IsPublicRegion(int region);
        int GetRegionProgress(int region);
        int GetRegionGoddessId(int region);
    }

    public sealed class OfficialMap14NativeWorldView : MonoBehaviour, IDisposable
    {
        public MapGroundRoot GroundRoot { get; private set; }
        public bool IsReady { get; private set; }

        private NativeMap13AssetCatalog catalog;
        private Map14WorldEnvironment environment;
        private Map14WorldLabels labels;
        private RenderPipelineAsset previousPipeline, previousQuality;
        private bool pipelineBound, disposed;
        private UniversalAdditionalCameraData addedCameraData;

        public static OfficialMap14NativeWorldView Create(Transform parent, Camera camera, IMap14NativeWorldState state)
        {
            var host = new GameObject("Map14 original native world");
            host.SetActive(false);
            host.transform.SetParent(parent, false);
            var view = host.AddComponent<OfficialMap14NativeWorldView>();
            try
            {
                view.Initialize(camera, state);
                host.SetActive(true);
                view.StartCoroutine(view.LoadInitialPosition());
                return view;
            }
            catch
            {
                view.Dispose();
                Destroy(host);
                throw;
            }
        }

        private void Initialize(Camera camera, IMap14NativeWorldState state)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Map14 original assets require the APK's Linear project color space.");
            if (camera == null || state == null) throw new ArgumentNullException(camera == null ? nameof(camera) : nameof(state));

            OfficialMap12NativeWorldAssets shared = OfficialMap12NativeWorldAssets.Load();
            previousPipeline = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = shared.Pipeline;
            QualitySettings.renderPipeline = null;
            pipelineBound = true;
            if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
                addedCameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.GetUniversalAdditionalCameraData().SetRenderer(0);

            NativeMap13AssetCatalog terrain = LoadCatalog("native-terrain-catalog", 241);
            NativeMap13AssetCatalog scene = LoadCatalog("native-scene-catalog", 242);
            NativeMap13AssetCatalog environmentCatalog = LoadCatalog("native-environment-scene-catalog", 389);
            NativeMap13AssetCatalog labelCatalog = LoadCatalog("native-region-labels-catalog", 97);
            catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            catalog.Bindings = Merge(terrain, scene, environmentCatalog, labelCatalog);

            GameObject instance = Instantiate(scene.Load<GameObject>(
                "Assets/Partition/MapGround_14/MapGround_14_bare"), transform, false);
            GroundRoot = instance.GetComponent<MapGroundRoot>();
            if (GroundRoot == null) throw new InvalidOperationException("Original Map14 bare prefab has no MapGroundRoot.");
            PartitionData data = terrain.Load<PartitionData>("Assets/MonoBehaviour/partition_data");
            var labelsData = JsonUtility.FromJson<Map14LabelData>(
                Resources.Load<TextAsset>("Map14/Native/region-label-data")?.text);
            if (labelsData == null || labelsData.manifest != "871_156598" || labelsData.regions == null
                || labelsData.regions.Length != 111)
                throw new InvalidOperationException("Official Map14 region label data is not bound.");
            if (labelsData.width <= 0 || labelsData.height <= 0 || labelsData.origin == null
                || labelsData.origin.Length != 3 || labelsData.regionGrid == null
                || labelsData.regionGrid.Length != labelsData.width * labelsData.height
                || labelsData.maxProgress == null || labelsData.maxProgress.Length != labelsData.regions.Length
                || labelsData.regions.Any(region => region == null || region.ClassId != 14
                    || region.Region < 0 || region.Region >= labelsData.regions.Length)
                || labelsData.regions.Select(region => region.Region).Distinct().Count() != labelsData.regions.Length)
                throw new InvalidDataException("Original Map14 region labels are malformed or not index-aligned.");

            environment = new Map14WorldEnvironment(GroundRoot, shared.EnvironmentData, labelsData, state);
            labels = new Map14WorldLabels(environment, state, shared.Font);
            NativeMap13EnvironmentBinding.Bind(instance, environment);
            labels.Bind(instance);

            NativeMap13ResourceLoader resources = gameObject.AddComponent<NativeMap13ResourceLoader>();
            resources.Catalog = catalog;
            resources.BindInstance = child =>
            {
                NativeMap13EnvironmentBinding.Bind(child, environment);
                labels.Bind(child);
            };

            var controller = GroundRoot.PartitionController;
            controller.PartitionData = data;
            controller.Context = environment;
            controller.Loader.ResourceLoader = resources;
            controller.Loader.ServiceProvider = resources;
            controller.Loader.IsCameraReady = () => camera != null && camera.enabled;
            controller.Loader.IsOtherGameRunning = () => false;
        }

        private static NativeMap13AssetCatalog LoadCatalog(string name, int expectedCount)
        {
            NativeMap13AssetCatalog value = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/" + name);
            if (value == null || value.Bindings == null || value.Bindings.Length != expectedCount)
                throw new InvalidOperationException("Original Map14 native resource directory is incomplete: " + name);
            return value;
        }

        private static NativeMap13AssetCatalog.Binding[] Merge(params NativeMap13AssetCatalog[] sources)
        {
            var values = new Dictionary<string, NativeMap13AssetCatalog.Binding>(StringComparer.Ordinal);
            foreach (NativeMap13AssetCatalog source in sources)
                foreach (NativeMap13AssetCatalog.Binding binding in source.Bindings)
                {
                    if (binding == null || binding.Asset == null || string.IsNullOrWhiteSpace(binding.OfficialPath))
                        throw new InvalidOperationException("Original Map14 native resource binding is malformed.");
                    if (values.TryGetValue(binding.OfficialPath, out NativeMap13AssetCatalog.Binding previous))
                    {
                        if (previous.Asset != binding.Asset)
                            throw new InvalidOperationException("Different original Map14 assets share a resource path: " + binding.OfficialPath);
                        continue;
                    }
                    values.Add(binding.OfficialPath, binding);
                }
            return values.Values.ToArray();
        }

        private IEnumerator LoadInitialPosition()
        {
            yield return GroundRoot.PartitionController.LoadByPlayerPos();
            IsReady = GroundRoot.PartitionController.Loader.IsPosReady(environment.PlayerWorldPosition);
        }

        public void RefreshState()
        {
            if (disposed || environment == null) return;
            environment.Refresh();
            labels.Refresh();
        }

        public void UnlockRegion(Vector3 worldPosition)
        {
            RefreshState();
            GroundRoot.EnvEffect.UnlockRegion(worldPosition);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            StopAllCoroutines();
            if (GroundRoot)
            {
                if (gameObject.activeInHierarchy)
                {
                    var controller = GroundRoot.PartitionController;
                    controller.StopAllCoroutines();
                    controller.Loader.ReleaseOfflineAssets();
                }
                GroundRoot.gameObject.SetActive(false);
            }
            if (pipelineBound)
            {
                GraphicsSettings.defaultRenderPipeline = previousPipeline;
                QualitySettings.renderPipeline = previousQuality;
                pipelineBound = false;
            }
            if (addedCameraData) Destroy(addedCameraData);
        }

        private void OnDestroy()
        {
            Dispose();
            labels?.Dispose();
            if (catalog) Destroy(catalog);
        }
    }

    internal sealed class Map14WorldEnvironment : IMapEnvironmentServices<MapGroundRoot, MapEnvData>,
        IPartitionWorldContext<TerrainRegionStatus>
    {
        private readonly IMap14NativeWorldState state;
        private readonly List<TerrainRegionStatus> statuses;
        private List<int> progress;

        public MapGroundRoot MapGroundRoot { get; }
        public MapEnvData EnvironmentData { get; }
        public Map14LabelData Data { get; }
        public int MapClassId => 14;
        public int CurrentRegion => Data.QueryRegion(PlayerWorldPosition);
        public Vector3 PlayerWorldPosition => state.PlayerWorldPosition;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, TerrainRegionStatus, TerrainRegionStatus> OnMapStatusUpdate;

        public Map14WorldEnvironment(MapGroundRoot root, MapEnvData config, Map14LabelData data, IMap14NativeWorldState state)
        {
            MapGroundRoot = root;
            EnvironmentData = config;
            Data = data;
            this.state = state;
            statuses = Data.regions.Select(region => Status(region.Region)).ToList();
            progress = Data.regions.Select(region => state.GetRegionProgress(region.Region)).ToList();
        }

        private TerrainRegionStatus Status(int region) => state.IsPublicRegion(region)
            ? TerrainRegionStatus.Public : TerrainRegionStatus.Private;
        public List<TerrainRegionStatus> GetCurrentMapStatus() => statuses;
        public int GetMapRegionGoddessId(int mapClassId, int region) => mapClassId == 14 ? state.GetRegionGoddessId(region) : 0;
        public int GetWorldMapRegionProgress(int mapClassId, int region) => mapClassId == 14 ? state.GetRegionProgress(region) : 0;
        public bool WorldMapRegionCleared(int mapClassId, int region, int value) => mapClassId == 14 && value != 0
            && (region < 0 || region >= Data.maxProgress.Length || Data.maxProgress[region] == 0
                || value >= Data.maxProgress[region]);
        public Pos2d PosToGrid(Vector3 position) => new Pos2d(
            (int)(position.x - Data.origin[0] + 0.5f), (int)(position.y - Data.origin[1] + 0.5f));
        public int QueryRegion(Pos2d position) => position.X < 0 || position.X >= Data.width
            || position.Y < 0 || position.Y >= Data.height ? -1 : Data.regionGrid[position.Y * Data.width + position.X];
        public void UnlockAllRegionPositions(int mapClassId, int region) { }

        public void Refresh()
        {
            foreach (MapRegionInfo region in Data.regions)
            {
                int id = region.Region;
                TerrainRegionStatus next = Status(id), previous = statuses[id];
                if (next == previous) continue;
                statuses[id] = next;
                OnMapStatusUpdate?.Invoke(14, id, previous, next);
            }
            List<int> nextProgress = Data.regions.Select(region => state.GetRegionProgress(region.Region)).ToList();
            if (progress.SequenceEqual(nextProgress)) return;
            progress = nextProgress;
            OnWorldMapProgressUpdate?.Invoke(14, progress);
        }
    }

    internal sealed class Map14WorldLabels : IMap14LabelServices, IMap14LabelPlayer,
        ILanguageTypeService, ILocalizationTextService, IDisposable
    {
        private readonly Map14WorldEnvironment environment;
        private readonly IMap14NativeWorldState state;
        private readonly Dictionary<string, string> texts;
        private readonly MultiLanguageUIService ui;
        private readonly Map14LabelFontInstance font;
        private readonly LabelRegionStatus[] statuses;
        private List<int> progress;

        public Map14WorldLabels(Map14WorldEnvironment environment, IMap14NativeWorldState state, TMP_FontAsset sourceFont)
        {
            this.environment = environment;
            this.state = state;
            texts = environment.Data.texts.ToDictionary(value => value.key, value => value.value);
            font = new Map14LabelFontInstance(sourceFont);
            ui = new MultiLanguageUIService(this);
            Level = state.PlayerLevel;
            SubRank = state.PlayerSubRank;
            CombatRating = state.PlayerCombatRating;
            statuses = environment.Data.regions.Select(region => Status(region.Region)).ToArray();
            progress = environment.Data.regions.Select(region => state.GetRegionProgress(region.Region)).ToList();
        }

        public void Bind(GameObject instance) => Map14LabelBinding.Bind(instance, this, ui, this, this, font);
        public void Dispose() { ui.Dispose(); font.Dispose(); }
        public int MapClassId => 14;
        public bool IsMapInitialized => true;
        public event Action Initialized { add { } remove { } }
        public IMap14LabelPlayer Player => this;
        public bool IsTeamMember => false;
        public int QueryRegion(Vector3 position) => environment.Data.QueryRegion(position);
        public MapRegionInfo GetRegion(int mapClassId, int region) => environment.Data.regions.Single(value => value.ClassId == mapClassId && value.Region == region);
        public string GetRegionNameKey(int mapClassId, int region) => "region_" + mapClassId + "_" + region;
        public object CompactNumber(long value) => Zjcs.Offline.Progression.OfficialPropFormat.Compact(value);
        public int CalcWorldMapRegionProgress(int mapClassId, int region, int value) => environment.Data.CalcWorldMapRegionProgress(mapClassId, region, value);
        public int CalcRankSuppression(int mine, int other) => environment.Data.CalcRankSuppression(mine, other);
        public bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color) => environment.Data.TryGetLevelColor(type, level, out color);
        public DateTime Now => throw new InvalidOperationException("Map14 has no timed region limits.");
        public double CalcRemainSecond(DateTime endTime) => throw new InvalidOperationException("Map14 has no timed region limits.");
        public object FormatTimeSpan(TimeSpan span, int format) => throw new InvalidOperationException("Map14 has no timed region limits.");
        public int Level { get; private set; }
        public int SubRank { get; private set; }
        public long CombatRating { get; private set; }
        public event Action<int> OnLevelUpdate;
        public event Action<long, long> OnCombatRatingChanged;
        public event Action<int, List<int>> OnWorldMapProgressUpdate;
        public event Action<int, int, LabelRegionStatus, LabelRegionStatus> OnMapStatusUpdate;
        public event Action<int> OnPlanesRankUnlock { add { } remove { } }
        public event Action<long> OnDebugTimeOffset { add { } remove { } }
        public bool RegionIsActived(int mapClassId, int region) => GetMapRegionStatus(mapClassId, region) == LabelRegionStatus.Public;
        public LabelRegionStatus GetMapRegionStatus(int mapClassId, int region) => mapClassId != 14 || region < 0 || region >= statuses.Length
            ? LabelRegionStatus.None : statuses[region];
        public int GetWorldMapRegionProgress(int mapClassId, int region) => mapClassId != 14 || region < 0 || region >= statuses.Length
            ? 0 : state.GetRegionProgress(region);

        public void Refresh()
        {
            int nextLevel = state.PlayerLevel;
            if (nextLevel != Level) { Level = nextLevel; OnLevelUpdate?.Invoke(Level); }
            SubRank = state.PlayerSubRank;
            long nextRating = state.PlayerCombatRating;
            if (nextRating != CombatRating)
            {
                long previous = CombatRating;
                CombatRating = nextRating;
                OnCombatRatingChanged?.Invoke(previous, nextRating);
            }
            foreach (MapRegionInfo region in environment.Data.regions)
            {
                int id = region.Region;
                LabelRegionStatus next = Status(id), previous = statuses[id];
                if (next == previous) continue;
                statuses[id] = next;
                OnMapStatusUpdate?.Invoke(14, id, previous, next);
            }
            List<int> nextProgress = environment.Data.regions.Select(region => state.GetRegionProgress(region.Region)).ToList();
            if (progress.SequenceEqual(nextProgress)) return;
            progress = nextProgress;
            OnWorldMapProgressUpdate?.Invoke(14, progress);
        }

        private LabelRegionStatus Status(int region) => state.IsPublicRegion(region)
            ? LabelRegionStatus.Public : LabelRegionStatus.Private;
        public LanguageType CurrentLanguage
        {
            get => LanguageType.zh_CN;
            set { if (value != LanguageType.zh_CN) throw new NotSupportedException("Map14 binds the official Simplified Chinese text closure."); }
        }
        public bool IsShowLanguageKey { get; set; }
        public bool IsShowMissingKeyText { get; set; }
        public event Action<LanguageType> OnLanguageChanged { add { } remove { } }
        public bool ContainsKey(string key) => texts.ContainsKey(key);
        public string GetText(string key, LanguageType language, params object[] args) => texts.TryGetValue(key, out string value)
            ? value : throw new KeyNotFoundException("Original Map14 world-label key is unbound: " + key);
        public bool TryGetOriginalText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
        public bool TryGetText(string key, LanguageType language, out string result) => texts.TryGetValue(key, out result);
        public bool TryGetText(string key, out string result) => texts.TryGetValue(key, out result);
        public string GetTextFormated(string key, LanguageType language, params object[] args) => string.Format(GetText(key, language), args);
        public void ReloadData() => throw new NotSupportedException("Map14 original world-label text data is immutable.");
        public void SetReplacer(ITextReplacer replacer) => throw new NotSupportedException("No world-label text replacer is configured.");
        public void AddDataProvider(ILanguageTextDataProvider provider) => throw new NotSupportedException("Map14 original world-label text data is immutable.");
    }
}
