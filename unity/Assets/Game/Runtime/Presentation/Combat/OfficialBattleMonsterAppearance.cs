using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    // Binds the original actor components to the shared native skeleton backend.
    // The original appearance manager owns texture loading and material isolation.
    internal sealed class OfficialBattleMonsterAppearance : IDisposable
    {
        private AppearanceAssetManager manager;
        private IDisposable textureRoutes;
        private AppearanceRendererLite appearance;
        private MeshMaterialProperty property;
        private bool disposed;

        internal OfficialBattleMonsterAppearance(BattleNativeMonsterRenderer renderer,
            BattleNativeMonsterAppearanceCatalog.Binding binding)
        {
            if (!renderer || binding == null) throw new ArgumentNullException();
            if (renderer.SkeletonDataName != binding.SkeletonName
                || renderer.EntityRoot.Find(binding.RendererPath) != renderer.transform)
                throw new InvalidDataException("Original monster appearance hierarchy differs.");
            var propertyNode = string.IsNullOrEmpty(binding.MaterialPropertyPath)
                ? renderer.EntityRoot : renderer.EntityRoot.Find(binding.MaterialPropertyPath);
            if (!propertyNode || renderer.GetComponent<AppearanceRendererLite>() || propertyNode.GetComponent<MeshMaterialProperty>())
                throw new InvalidOperationException("Bind the original monster appearance once.");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig")
                ?? throw new FileNotFoundException("Original appearance paths are not packaged.");
            var resources = binding.Textures;
            if (!resources) throw new InvalidDataException("Original monster appearance textures are unbound.");
            bool active = renderer.EntityRoot.gameObject.activeSelf;
            renderer.EntityRoot.gameObject.SetActive(false);
            try
            {
                manager = new AppearanceAssetManager(resources.ManagerConfig, callback => new LowMemorySubscription(callback));
                var adapter = new OfflineProfessionWeaponTextureResources(config, manager, resources);
                textureRoutes = ProfessionAppearanceTextureBindings.Register(adapter, resources.TextureGuids,
                    new ScriptableObject[] { resources.Atlas });
                appearance = renderer.gameObject.AddComponent<AppearanceRendererLite>();
                appearance.SkeletonRenderer = renderer;
                appearance.LevelOfResolution = LevelOfResolution.Low;
                appearance.RenderExistingMeshes = new List<Component>();
                appearance.Configure(adapter);
                appearance.enabled = binding.AppearanceEnabled;
                property = propertyNode.gameObject.AddComponent<MeshMaterialProperty>();
                property.MeshRenderer = binding.MaterialPropertyHasMeshRenderer ? renderer.GetComponent<MeshRenderer>() : null;
                property.SetAppearanceTarget(appearance.gameObject);
                property.enabled = binding.MaterialPropertyEnabled;
            }
            catch { Dispose(); throw; }
            finally { renderer.EntityRoot.gameObject.SetActive(active); }
            renderer.UpdateAnimationAndMesh();
        }

        internal void Tick() { if (!disposed) manager?.Update(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Return materials/texture requests while their manager still exists.
            // The actor owner stops its playables before releasing these components.
            if (property) UnityEngine.Object.DestroyImmediate(property);
            if (appearance) UnityEngine.Object.DestroyImmediate(appearance);
            manager?.Dispose();
            textureRoutes?.Dispose();
            property = null; appearance = null; manager = null; textureRoutes = null;
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
