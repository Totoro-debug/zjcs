using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativePartition;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeEnvironment;

namespace Zjcs.Presentation.World.NativeMap13
{
    public sealed class OfficialMap13NativeWorldView : MonoBehaviour, IDisposable
    {
        public MapGroundRoot GroundRoot { get; private set; }
        public bool IsReady { get; private set; }
        private NativeMap13AssetCatalog catalog;
        private Map13WorldEnvironment environment;
        private Map13WorldLabels labels;
        private RenderPipelineAsset previousPipeline, previousQuality;
        private bool pipelineBound, disposed;
        private UniversalAdditionalCameraData addedCameraData;

        public static OfficialMap13NativeWorldView Create(Transform parent, Camera camera, IMap13NativeWorldState state)
        {
            var host = new GameObject("Map13 original native world");
            host.SetActive(false);
            host.transform.SetParent(parent, false);
            var view = host.AddComponent<OfficialMap13NativeWorldView>();
            try
            {
                view.Initialize(OfficialMap13NativeWorldAssets.Load(), camera, state);
                host.SetActive(true);
                view.StartCoroutine(view.LoadInitialPosition());
                return view;
            }
            catch { view.Dispose(); Destroy(host); throw; }
        }

        private void Initialize(OfficialMap13NativeWorldAssets assets, Camera camera, IMap13NativeWorldState state)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Map13 original assets require the APK's Linear project color space.");
            previousPipeline = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = assets.Pipeline;
            QualitySettings.renderPipeline = null;
            pipelineBound = true;
            if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
                addedCameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.GetUniversalAdditionalCameraData().SetRenderer(0);
            catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();
            catalog.hideFlags = HideFlags.DontSave;
            catalog.Bindings = new[] { assets.Terrain, assets.Scene, assets.Environment, assets.Labels }
                .SelectMany(c => c.Bindings).GroupBy(b => b.OfficialPath).Select(group =>
                {
                    if (group.Select(b => b.Asset).Distinct().Count() != 1)
                        throw new InvalidOperationException("Different native assets share an official Map13 resource path: " + group.Key);
                    return group.First();
                }).ToArray();
            var instance = Instantiate(assets.Scene.Load<GameObject>("Assets/Partition/MapGround_13/MapGround_13_bare"), transform, false);
            GroundRoot = instance.GetComponent<MapGroundRoot>();
            var data = assets.Terrain.Load<PartitionData>("Assets/MonoBehaviour/partition_data");
            environment = new Map13WorldEnvironment(GroundRoot, assets.EnvironmentData, state);
            labels = new Map13WorldLabels(state, environment, assets.Font);
            NativeMap13EnvironmentBinding.Bind(instance, environment);
            labels.Bind(instance);
            var resources = gameObject.AddComponent<NativeMap13ResourceLoader>();
            resources.Catalog = catalog;
            resources.BindInstance = child => { NativeMap13EnvironmentBinding.Bind(child, environment); labels.Bind(child); };
            var controller = GroundRoot.PartitionController;
            controller.PartitionData = data;
            controller.Context = environment;
            controller.Loader.ResourceLoader = resources;
            controller.Loader.ServiceProvider = resources;
            controller.Loader.IsCameraReady = () => camera != null && camera.enabled;
            controller.Loader.IsOtherGameRunning = () => false;
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
            // Child OnDestroy unregisters original UI events at the end of the
            // frame, before releasing the font copies used by those children.
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
