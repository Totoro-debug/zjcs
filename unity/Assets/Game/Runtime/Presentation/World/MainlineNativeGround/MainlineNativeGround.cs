using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Official.World.NativeMap14Labels;

namespace Zjcs.Presentation.World
{
    /// <summary>Loads an original non-partitioned ground with dependencies bound before Awake.</summary>
    public sealed class MainlineNativeGround : IDisposable
    {
        private readonly GameObject root;
        private readonly MainlineWorldEnvironment environment;
        private readonly MainlineWorldLabels labels;
        private readonly RenderPipelineAsset previousPipeline, previousQuality;
        private readonly RenderPipelineAsset pipeline;
        private UniversalAdditionalCameraData addedCameraData;
        private bool disposed;

        public MainlineNativeGround(Transform parent, GameObject prefab, Camera camera,
            IMainlineWorldGroundState state, Map14LabelData data, MapEnvData environmentData,
            RenderPipelineAsset pipeline, TMP_FontAsset font, Func<int, bool> regionTimeReached,
            IMapRegionTime regionTime = null)
        {
            if (!prefab || !camera || data == null || !pipeline)
                throw new ArgumentException("Original ground, camera, region data and pipeline are required.");
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Original native ground requires the APK's Linear color space.");
            this.pipeline = pipeline;
            previousPipeline = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            root = new GameObject("Official Map" + data.mapClassId + " native ground");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            try
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = null;
                if (!camera.GetComponent<UniversalAdditionalCameraData>())
                    addedCameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                GameObject instance = UnityEngine.Object.Instantiate(prefab, root.transform, false);
                var ground = instance.GetComponent<MapGroundRoot>();
                if (!ground) throw new InvalidOperationException("The original native prefab has no MapGroundRoot.");
                environment = new MainlineWorldEnvironment(ground, environmentData, data, state);
                labels = new MainlineWorldLabels(data, state, font, regionTimeReached, regionTime);
                NativeMap13EnvironmentBinding.Bind(instance, environment);
                labels.Bind(instance);
                root.SetActive(true);
            }
            catch { Dispose(); throw; }
        }

        public void Refresh() { environment?.Refresh(); labels?.Refresh(); }

        public void UnlockRegion(Vector3 worldPosition)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineNativeGround));
            var effect = environment.MapGroundRoot.EnvEffect;
            if (!effect) throw new InvalidOperationException("Original region activation requires MapEnvEffect.");
            // The original activation action starts lighting before revealing
            // region cells. A status refresh alone does not execute that action.
            Refresh();
            effect.UnlockRegion(worldPosition);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // OnDisable releases subscriptions while the bound services are alive.
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            labels?.Dispose();
            if (addedCameraData) UnityEngine.Object.Destroy(addedCameraData);
            if (GraphicsSettings.defaultRenderPipeline == pipeline)
                GraphicsSettings.defaultRenderPipeline = previousPipeline;
            if (QualitySettings.renderPipeline == null)
                QualitySettings.renderPipeline = previousQuality;
        }
    }
}
