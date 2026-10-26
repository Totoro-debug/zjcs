using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.World.NativePartition;

namespace Zjcs.Offline.World.NativeMap13
{
    public sealed class NativeMap13ResourceLoader : MonoBehaviour, IPartitionResourceLoader, IPartitionInstanceFactory
    {
        public NativeMap13AssetCatalog Catalog;
        public Action<GameObject> BindInstance;
        private readonly Queue<Action> completions = new Queue<Action>();

        public PartitionAssetHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
        {
            if (Catalog == null) throw new InvalidOperationException("Map13 native asset catalog is not bound.");
            var handle = new PartitionAssetHandle<T>(Catalog.Load<T>(path));
            completions.Enqueue(handle.DispatchCompletion);
            return handle;
        }

        private void Update()
        {
            int count = completions.Count;
            while (count-- > 0) completions.Dequeue()();
        }

        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            // Bind dependencies while inactive, before any original component Awake callback.
            var staging = new GameObject("Map13 native instance binding");
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
            try
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, staging.transform, worldPositionStays);
                BindInstance?.Invoke(instance);
                instance.transform.SetParent(parent, worldPositionStays);
                return instance;
            }
            finally { Destroy(staging); }
        }
    }
}
