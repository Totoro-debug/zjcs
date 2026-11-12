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
            if (!resources && binding.LevelOfResolution != LevelOfResolution.None)
                throw new InvalidDataException("Original monster appearance textures are unbound.");
            bool active = renderer.EntityRoot.gameObject.activeSelf;
            renderer.EntityRoot.gameObject.SetActive(false);
            try
            {
                IProfessionAppearanceUILiteBindings adapter;
                if (resources)
                {
                    manager = new AppearanceAssetManager(resources.ManagerConfig, callback => new LowMemorySubscription(callback));
                    var textures = new OfflineProfessionWeaponTextureResources(config, manager, resources);
                    textureRoutes = ProfessionAppearanceTextureBindings.Register(textures, resources.TextureGuids,
                        new ScriptableObject[] { resources.Atlas });
                    adapter = textures;
                }
                else adapter = new DirectTextureBindings(config, binding.SkeletonName);
                appearance = renderer.gameObject.AddComponent<AppearanceRendererLite>();
                appearance.SkeletonRenderer = renderer;
                appearance.LevelOfResolution = binding.LevelOfResolution;
                appearance.RenderExistingMeshes = new List<Component>();
                foreach (string path in binding.RenderExistingMeshPaths)
                {
                    var node = renderer.EntityRoot.Find(path);
                    var copy = node ? node.GetComponent<BattleNativeMonsterMeshCopy>() : null;
                    if (!copy) throw new InvalidDataException("Original appearance mesh-copy reference is unbound: " + path);
                    appearance.RenderExistingMeshes.Add(copy);
                }
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

        // The verified None-prefab has no configured skeleton path or packaged
        // loader. Preserve the original client's null-loader branch: its cloned
        // materials keep their original textures and mesh-copy replacements.
        private sealed class DirectTextureBindings : IProfessionAppearanceUILiteBindings
        {
            private readonly string loaderPath;
            public AppearanceConfig Config { get; }
            public IProfessionAppearanceUIAssetManager AssetManager => null;
            internal DirectTextureBindings(AppearanceConfig config, string skeletonName)
            {
                Config = config;
                if (!string.IsNullOrEmpty(config.GetSkeletonPath(skeletonName)))
                    throw new InvalidDataException("Review the original None appearance loader binding.");
                loaderPath = "/" + skeletonName + "_TextureLoader_None.asset";
            }
            public IProfessionAppearanceCharacterAsset LoadAsset(string path)
            {
                if (path != loaderPath) throw new FileNotFoundException("Original direct appearance resource is unbound: " + path);
                return new MissingLoader();
            }
            private sealed class MissingLoader : IProfessionAppearanceCharacterAsset
            {
                public ScriptableObject Result => null;
                public void Dispose() { }
            }
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
