using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // The original appearance implementation owns materials and texture requests.
    // The native actor keeps its existing pose, geometry and follower interface.
    internal sealed class PartnerNativeBattleAppearance : IDisposable
    {
        private AppearanceAssetManager manager;
        private readonly List<IDisposable> routes = new List<IDisposable>();
        private readonly List<AppearanceRendererLite> appearances = new List<AppearanceRendererLite>();
        private MeshMaterialProperty property;
        private bool disposed;

        internal PartnerNativeBattleAppearance(PartnerNativeBattleActor actor,
            PartnerNativeBattleAppearanceCatalog.Binding binding)
        {
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig")
                ?? throw new FileNotFoundException("Original appearance paths are not packaged.");
            Transform root = actor.Attachments.Agent.transform;
            bool active = actor.Root.gameObject.activeSelf;
            actor.Root.gameObject.SetActive(false);
            try
            {
                foreach (var source in binding.Renderers)
                {
                    var node = root.Find(source.Path);
                    var renderer = node ? node.GetComponent<PartnerNativeBattleRendererPort>() : null;
                    var resources = source.Textures;
                    if (!renderer || renderer.SkeletonDataName != source.SkeletonDataName || !resources
                        || renderer.GetComponent<AppearanceRendererLite>())
                        throw new InvalidDataException("Original partner appearance renderer differs.");
                    if (manager == null)
                        manager = new AppearanceAssetManager(resources.ManagerConfig, callback => new LowMemorySubscription(callback));
                    var textures = new OfflineProfessionWeaponTextureResources(config, manager, resources);
                    routes.Add(ProfessionAppearanceTextureBindings.Register(textures, resources.TextureGuids,
                        new ScriptableObject[] { resources.Atlas }));
                    var appearance = node.gameObject.AddComponent<AppearanceRendererLite>();
                    appearances.Add(appearance);
                    appearance.SkeletonRenderer = renderer;
                    appearance.LevelOfResolution = source.LevelOfResolution;
                    appearance.RenderExistingMeshes = new List<Component>();
                    appearance.Configure(textures);
                    appearance.enabled = source.Enabled;
                }
                var propertyNode = string.IsNullOrEmpty(binding.PropertyPath) ? root : root.Find(binding.PropertyPath);
                var target = root.Find(binding.AppearanceTargetPath);
                if (!propertyNode || !target || !target.GetComponent<AppearanceRendererLite>()
                    || propertyNode.GetComponent<MeshMaterialProperty>())
                    throw new InvalidDataException("Original partner material-property reference differs.");
                property = propertyNode.gameObject.AddComponent<MeshMaterialProperty>();
                property.MeshRenderer = null;
                property.SetAppearanceTarget(target.gameObject);
                property.enabled = binding.PropertyEnabled;
            }
            catch { Dispose(); throw; }
            finally { actor.Root.gameObject.SetActive(active); }
        }

        internal void Tick() { if (!disposed) manager?.Update(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (property) UnityEngine.Object.DestroyImmediate(property);
            foreach (var appearance in appearances)
                if (appearance) UnityEngine.Object.DestroyImmediate(appearance);
            appearances.Clear();
            manager?.Dispose(); manager = null;
            foreach (var route in routes) route.Dispose();
            routes.Clear();
        }

        private sealed class LowMemorySubscription : IDisposable
        {
            private Application.LowMemoryCallback callback;
            internal LowMemorySubscription(Action callback)
            { this.callback = callback.Invoke; Application.lowMemory += this.callback; }
            public void Dispose()
            { if (callback == null) return; Application.lowMemory -= callback; callback = null; }
        }
    }
}
