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
using Zjcs.Offline.World.NativeMap14SharedEntities;
using GConfig;
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
    public interface IMap14NativeWorldState : IMainlineWorldGroundState
    {
    }

    public sealed class OfficialMap14NativeWorldView : MonoBehaviour, IDisposable
    {
        public MapGroundRoot GroundRoot { get; private set; }
        public bool IsReady { get; private set; }
        public NativeMap14SharedRoot SharedRoot { get; private set; }

        private NativeMap13AssetCatalog catalog;
        private MainlineWorldEnvironment environment;
        private MainlineWorldLabels labels;
        private RenderPipelineAsset previousPipeline, previousQuality;
        private bool pipelineBound, disposed;
        private UniversalAdditionalCameraData addedCameraData;
        private NativeMap14SharedAssemblies sharedAssemblies;
        private NativeMap14SharedConfiguration sharedConfiguration;
        private NativeMap14SharedData sharedData;

        public static OfficialMap14NativeWorldView Create(Transform parent, Camera camera, IMainlineWorldGroundState state)
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

        private void Initialize(Camera camera, IMainlineWorldGroundState state)
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

            environment = new MainlineWorldEnvironment(GroundRoot, shared.EnvironmentData, labelsData, state);
            labels = new MainlineWorldLabels(labelsData, state, shared.Font);
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
            var sharedCatalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            sharedAssemblies = new NativeMap14SharedAssemblies(sharedCatalog);
            sharedConfiguration = new NativeMap14SharedConfiguration(sharedAssemblies);
            sharedData = new NativeMap14SharedData(sharedAssemblies, sharedConfiguration);
            // Finish each local synchronous parser job before yielding. An
            // interrupted map load therefore leaves no outstanding config job.
            var load = sharedData.LoadAll(false);
            while (load.MoveNext())
            {
                if (load.Current is ConfigRequest request)
                    sharedConfiguration.Manager.Wait(new[] { request });
                yield return load.Current;
            }
            SharedRoot = new NativeMap14SharedRoot(sharedAssemblies, sharedConfiguration, sharedData, 14);
            SharedRoot.Start();
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
            IsReady = false;
            StopAllCoroutines();
            try { ReleaseSharedRoot(); }
            finally { ReleaseWorldView(); }
        }

        private void ReleaseSharedRoot()
        {
            try { SharedRoot?.Dispose(); }
            finally
            {
                SharedRoot = null;
                try { sharedData?.Dispose(); }
                finally
                {
                    sharedData = null;
                    try { sharedConfiguration?.Dispose(); }
                    finally
                    {
                        sharedConfiguration = null;
                        sharedAssemblies?.Dispose();
                        sharedAssemblies = null;
                    }
                }
            }
        }

        private void ReleaseWorldView()
        {
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

}
