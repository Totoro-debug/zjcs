using System;
using UnityEngine;

namespace Zjcs.Offline.HomeNativeAssets
{
    // Local replacement for the original service-provider Instantiate boundary.
    // The creator/controller still owns the returned instance and its lifetime.
    public sealed class HomeNativePrefabFactory : IDisposable
    {
        readonly Action<GameObject> install;
        readonly GameObject staging;
        bool disposed;

        public HomeNativePrefabFactory(Action<GameObject> install)
        {
            this.install = install ?? throw new ArgumentNullException(nameof(install));
            staging = new GameObject("HomeNativePrefabStaging");
            staging.SetActive(false);
        }

        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HomeNativePrefabFactory));
            if (!prefab) throw new ArgumentNullException(nameof(prefab));
            if (!ReferenceEquals(parent, null) && !parent) throw new MissingReferenceException("The native prefab parent was destroyed.");
            // The staging transform is an identity root. Both modes therefore
            // preserve Unity's original local/world parenting semantics, while
            // Awake and OnEnable cannot run before every dependency is assigned.
            var instance = UnityEngine.Object.Instantiate(prefab, staging.transform, worldPositionStays);
            try
            {
                install(instance);
                if (disposed) throw new ObjectDisposedException(nameof(HomeNativePrefabFactory));
                if (!ReferenceEquals(parent, null) && !parent) throw new MissingReferenceException("The native prefab parent was destroyed during installation.");
                instance.transform.SetParent(parent, worldPositionStays);
                return instance;
            }
            catch
            {
                if (instance) UnityEngine.Object.Destroy(instance);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (staging) UnityEngine.Object.Destroy(staging);
        }
    }
}
