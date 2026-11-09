using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // The scope supplies the actual source prefab: different bundles reuse names.
    public sealed class EmbeddedMonsterMecanimResources : IDisposable
    {
        private readonly EmbeddedMonsterMecanimCatalog catalog;
        private AppearanceAssetManager manager;
        private OfflineProfessionWeaponTextureResources appearance;
        private IDisposable textureRoutes;
        private bool disposed;
        public EmbeddedMonsterMecanimResources() { catalog = EmbeddedMonsterMecanimCatalog.Load(); }
        public int BindOriginalPrefab(GameObject sourcePrefab, GameObject instance)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EmbeddedMonsterMecanimResources));
            if (sourcePrefab != catalog.SourcePrefab) return 0;
            if (!instance || instance.activeInHierarchy) throw new InvalidOperationException("Bind embedded Mecanim before Awake.");
            EnsureAppearance();
            Transform node = instance.transform.Find(catalog.SkeletonPath)
                ?? throw new InvalidDataException("The original embedded Mecanim hierarchy is unbound.");
            var original = node.GetComponents<MonoBehaviour>().Single(value => value && value.GetType().FullName == "Spine.Unity.SkeletonMecanim");
            var lite = node.GetComponent<AppearanceRendererLite>()
                ?? throw new InvalidDataException("The original embedded appearance renderer is unbound.");
            if (lite.SkeletonRenderer != original || lite.LevelOfResolution != LevelOfResolution.Low
                || lite.RenderExistingMeshes == null || lite.RenderExistingMeshes.Count != 0)
                throw new InvalidDataException("The original embedded appearance binding changed.");
            var native = BattleNativeMonsterRenderer.BindEmbedded(node.gameObject, catalog.NativeAssets, catalog.ModelId);
            lite.SkeletonRenderer = native;
            lite.Configure(appearance);
            foreach (var follower in instance.GetComponentsInChildren<AttachmentFollower>(true))
                if (follower.SkeletonRenderer == original) follower.SkeletonRenderer = native;
            foreach (var follower in instance.GetComponentsInChildren<SkeletonRendererFollower>(true))
                if (follower.SkeletonRenderer == original) follower.SkeletonRenderer = native;
            UnityEngine.Object.DestroyImmediate(original);
            return 1;
        }
        private void EnsureAppearance()
        {
            if (appearance != null) return;
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig")
                ?? throw new FileNotFoundException("The original appearance paths are not packaged.");
            var resources = catalog.TextureResources;
            try {
                manager = new AppearanceAssetManager(resources.ManagerConfig, callback => new LowMemorySubscription(callback));
                appearance = new OfflineProfessionWeaponTextureResources(config, manager, resources);
                textureRoutes = ProfessionAppearanceTextureBindings.Register(appearance, resources.TextureGuids,
                    new ScriptableObject[] { resources.Atlas });
            } catch { manager?.Dispose(); manager = null; appearance = null; throw; }
        }
        public void Update() { if (!disposed) manager?.Update(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            manager?.Dispose(); textureRoutes?.Dispose();
            manager = null; textureRoutes = null; appearance = null;
        }
        private sealed class LowMemorySubscription : IDisposable
        {
            private Application.LowMemoryCallback callback;
            public LowMemorySubscription(Action callback) { this.callback = callback.Invoke; Application.lowMemory += this.callback; }
            public void Dispose() { if (callback == null) return; Application.lowMemory -= callback; callback = null; }
        }
    }
}
