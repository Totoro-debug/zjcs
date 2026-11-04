using System;
using System.IO;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Offline.World.NativeMap13MiniMapResources;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Owns the complete original instance and binds every injected dependency
    // before Awake/OnEnable. No original hierarchy or layout is reconstructed.
    public sealed partial class MainlineMiniMap : IDisposable
    {
        private readonly GameObject owner;
        private readonly MainlineNativeMiniMapRoot root;
        private RenderTexture texture;
        private bool disposed;
        private IDisposable ownedResourcePoints;
        private MainlineMiniMap(GameObject owner, MainlineNativeMiniMapRoot root)
        { this.owner = owner; this.root = root; }

        public static MainlineMiniMap Create(GameObject prefab, Transform parent, MainlineMiniMapBindings bindings,
            MiniMapResourcePointAssets resources, Func<string, Sprite> resourceSpriteResolver = null)
        {
            if (bindings?.State == null || bindings.ResourcePoints != null)
                throw new ArgumentException("Provide the original resource catalog or an external resource context, once.");
            var context = new LocalMiniMapResourcePointContext(resources, bindings.State.LabelServices, resourceSpriteResolver);
            try
            {
                var map = Create(prefab, parent, bindings.WithResourcePoints(context));
                map.ownedResourcePoints = context;
                return map;
            }
            catch
            {
                context.Dispose();
                throw;
            }
        }

        public static MainlineMiniMap Create(GameObject prefab, Transform parent, MainlineMiniMapBindings bindings)
        {
            if (prefab == null || bindings?.State == null || bindings.WorldData == null || bindings.Entities == null || bindings.Settings == null
                || bindings.Characters == null || bindings.ResourcePoints == null || bindings.OfflineData == null
                || bindings.MarkerServices == null || bindings.MarkerPerformance == null || bindings.ClickInput == null
                || bindings.CameraControl == null || bindings.ClickPublisher == null)
                throw new ArgumentException("Complete original minimap bindings are required.");
            var template = prefab.GetComponent<MainlineNativeMiniMapRoot>();
            if (template == null || template.MapClassId != bindings.State.MapClassId
                || template.MapClassId != bindings.ResourcePoints.MapClassId
                || bindings.Settings.GetMinimapSetting(bindings.MinimapConfigId) == null)
                throw new InvalidDataException("Original minimap identity or setting differs from its local bindings.");
            if (!bindings.WorldData.TryGetMapLiteData(template.MapClassId, out _)
                || bindings.WorldData.GetMinimapConfigId(template.MapClassId) != bindings.MinimapConfigId)
                throw new InvalidDataException("Original world data differs from its minimap binding.");
            if (bindings.Visibility != null && (template.MapClassId != bindings.State.CurrentMapClassId
                || bindings.Visibility.Host?.Entity?.ClassId != template.MapClassId
                || !bindings.WorldData.TryGetMapLiteData(template.MapClassId, out var ground)
                || ground.MapGroundSize.X != bindings.Visibility.Width || ground.MapGroundSize.Y != bindings.Visibility.Height))
                throw new InvalidDataException("Live minimap visibility must belong to the current original ground.");
            if (bindings.RegionTime != null && bindings.RegionTime.MapClassId != template.MapClassId)
                throw new InvalidDataException("Region time must belong to the original minimap.");

            var owner = new GameObject(prefab.name + "_Instance");
            owner.SetActive(false);
            owner.transform.SetParent(parent, false);
            MainlineMiniMapVisibilityAgent visibility = null;
            try
            {
                var instance = Object.Instantiate(prefab, owner.transform, false);
                var root = instance.GetComponent<MainlineNativeMiniMapRoot>();
                root.Bindings = bindings;
                root.WorldQueries = new MainlineMiniMapWorldQueries(bindings.WorldData, bindings.State, bindings.Entities, bindings.MarkerServices);
                if (bindings.RegionTime != null)
                    root.TimedLabels = new MainlineMiniMapLabels(bindings.State, bindings.RegionTime, bindings.WorldData);
                if (bindings.Visibility != null)
                {
                    visibility = instance.AddComponent<MainlineMiniMapVisibilityAgent>();
                    visibility.Bind(bindings.Visibility);
                    root.LiveVisibility = visibility;
                }
                // MapRootSwitchMapSupport.CreateMiniMap binds the shared slot's renderer.
                root.NonMaskableManager = bindings.NonMaskableManager;
                foreach (var ui in instance.GetComponentsInChildren<UIBase>(true)) ui.Services = root.LabelServices;
                foreach (var ui in instance.GetComponentsInChildren<MapMeCharacterUIBase>(true)) ui.MapMyCharacterManager = bindings.Characters;
                foreach (var spawn in instance.GetComponentsInChildren<MiniMapMarkSpawnManager>(true))
                { spawn.ServiceProvider = root.WorldQueries; spawn.PerformanceMiniMapMarkProxy = bindings.MarkerPerformance; }
                foreach (var marks in instance.GetComponentsInChildren<OfflineMiniMapMarkManager>(true)) marks.ServiceProvider = root.WorldQueries;
                foreach (var offline in instance.GetComponentsInChildren<OfflineMiniMap>(true)) offline.DataSource = bindings.OfflineData;
                foreach (var mask in instance.GetComponentsInChildren<MiniMapMaskHandler>(true)) mask.GridToWorld = root.GridToWorld;
                foreach (var click in instance.GetComponentsInChildren<MiniMapMarkClick>(true))
                { click.mMapInput = bindings.ClickInput; click.CameraControlManager = bindings.CameraControl; click.OnClickEventPublisher = bindings.ClickPublisher; }
                if (root.Character == null || root.KeepSizeHandler == null || root.ResourcePointManager == null)
                    throw new InvalidDataException("The original minimap prefab has incomplete root references.");
                instance.SetActive(true);
                owner.SetActive(true);
                return new MainlineMiniMap(owner, root);
            }
            catch
            {
                owner.SetActive(false);
                visibility?.Dispose();
                Object.Destroy(owner);
                throw;
            }
        }

        public RenderTexture Texture
        {
            get
            {
                if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMap));
                if (texture == null) texture = root.Character.GetOrAddRenderTexture();
                return texture;
            }
        }
        public event Action CloseRequested
        { add => root.OnWantToCloseFullScreen += value; remove => root.OnWantToCloseFullScreen -= value; }
        public void ShowFullScreen(MiniMapCameraController camera) => root.OnShowFullScreenMiniMap(camera);
        public void HideFullScreen(MiniMapCameraController camera) => root.OnHideFullScreenMiniMap(camera);
        public void SetActive(bool active)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMap));
            if (!active) CloseFullScreen();
            if (!active && hud != null) hud.SetActive(false);
            owner.SetActive(active);
            if (active && hud != null) hud.SetActive(true);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CloseFullScreen();
            DisposeHud();
            if (owner != null) { owner.SetActive(false); root.LiveVisibility?.Dispose(); Object.Destroy(owner); }
            ownedResourcePoints?.Dispose();
            ownedResourcePoints = null;
            if (texture != null) { texture.Release(); Object.Destroy(texture); texture = null; }
        }
    }
}
