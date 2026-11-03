using System;
using System.IO;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
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
        private MainlineMiniMap(GameObject owner, MainlineNativeMiniMapRoot root)
        { this.owner = owner; this.root = root; }

        public static MainlineMiniMap Create(GameObject prefab, Transform parent, MainlineMiniMapBindings bindings)
        {
            if (prefab == null || bindings?.State == null || bindings.Settings == null
                || bindings.Characters == null || bindings.ResourcePoints == null || bindings.OfflineData == null
                || bindings.MarkerServices == null || bindings.MarkerPerformance == null || bindings.ClickInput == null
                || bindings.CameraControl == null || bindings.ClickPublisher == null)
                throw new ArgumentException("Complete original minimap bindings are required.");
            var template = prefab.GetComponent<MainlineNativeMiniMapRoot>();
            if (template == null || template.MapClassId != bindings.State.MapClassId
                || template.MapClassId != bindings.ResourcePoints.MapClassId
                || bindings.Settings.GetMinimapSetting(bindings.MinimapConfigId) == null)
                throw new InvalidDataException("Original minimap identity or setting differs from its local bindings.");

            var owner = new GameObject(prefab.name + "_Instance");
            owner.SetActive(false);
            owner.transform.SetParent(parent, false);
            try
            {
                var instance = Object.Instantiate(prefab, owner.transform, false);
                var root = instance.GetComponent<MainlineNativeMiniMapRoot>();
                root.Bindings = bindings;
                // MapRootSwitchMapSupport.CreateMiniMap binds the shared slot's renderer.
                root.NonMaskableManager = bindings.NonMaskableManager;
                foreach (var ui in instance.GetComponentsInChildren<UIBase>(true)) ui.Services = bindings.State.LabelServices;
                foreach (var ui in instance.GetComponentsInChildren<MapMeCharacterUIBase>(true)) ui.MapMyCharacterManager = bindings.Characters;
                foreach (var spawn in instance.GetComponentsInChildren<MiniMapMarkSpawnManager>(true))
                { spawn.ServiceProvider = bindings.MarkerServices; spawn.PerformanceMiniMapMarkProxy = bindings.MarkerPerformance; }
                foreach (var marks in instance.GetComponentsInChildren<OfflineMiniMapMarkManager>(true)) marks.ServiceProvider = bindings.MarkerServices;
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
            if (owner != null) { owner.SetActive(false); Object.Destroy(owner); }
            if (texture != null) { texture.Release(); Object.Destroy(texture); texture = null; }
        }
    }
}
