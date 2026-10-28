using System;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Offline.World.NativeMap13StoryPlayables
{
    // A map-owned DI boundary around the original manager/center/FIFO pools.
    // Custom pool creation uses the original factory overload and does not
    // replace another map's global StoryPlayableInstantiation resolver.
    public sealed class NativeMap13StoryPlayableScope : MonoBehaviour, IStoryPlayableInstanceFactory, IDisposable
    {
        private MyPlayableCenter center;
        private MyPlayableManager manager;
        private GameObject staging;
        private GAction lowMemory;
        private bool disposed;
        public MyPlayableManager Manager => manager;

        public void Initialize()
        {
            if (manager != null || disposed) throw new InvalidOperationException("Map13 story playable scope is not new.");
            center = new MyPlayableCenter();
            lowMemory = new GAction();
            staging = new GameObject("Map13 original playable injection");
            staging.SetActive(false); staging.transform.SetParent(transform, false);
            manager = new MyPlayableManager { ScopeRoot = gameObject, InstanceFactory = this,
                MyPlayableCenter = center, LowMemory = lowMemory };
            manager.Initialize();
            manager.SetObjectPool(new GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>>(
                manager.PlayableSlot, (root, prefab) => new GameObjectPool<MyPlayable>(root,
                    (slot, unused, autoActive) =>
                    {
                        var instance = Instantiate(prefab, slot, false);
                        if (autoActive) instance.SetActive(true);
                        return instance;
                    }, prefab.name)));
            Application.lowMemory += OnLowMemory;
        }

        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if (disposed || !staging || center == null) throw new ObjectDisposedException(nameof(NativeMap13StoryPlayableScope));
            var instance = UnityEngine.Object.Instantiate(prefab, staging.transform, worldPositionStays);
            foreach (var playable in instance.GetComponentsInChildren<MyPlayable>(true)) playable.MyPlayableCenter = center;
            instance.transform.SetParent(parent, worldPositionStays);
            return instance;
        }
        public MyPlayable Play(GameObject prefab, GameObject source, GameObject target)
        {
            if (manager == null || disposed) throw new ObjectDisposedException(nameof(NativeMap13StoryPlayableScope));
            return manager.Play(prefab, source, target);
        }
        private void OnLowMemory() => lowMemory?.Invoke();
        private void Update() { if (!disposed) center?.Update(Time.deltaTime); }
        private void LateUpdate() { if (!disposed) center?.LateUpdate(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Application.lowMemory -= OnLowMemory;
            if (manager != null)
            {
                var slot = manager.PlayableSlot;
                manager.RemoveAllPlayable(true);
                center.RemoveAll();
                manager.ForceReleaseObjectPool();
                manager.Dispose(); manager = null;
                if (slot) Destroy(slot.gameObject);
            }
            if (staging) Destroy(staging);
            center = null;
        }
        private void OnDestroy() => Dispose();
    }
}
