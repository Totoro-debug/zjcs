using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.World.NativePartition
{
    public interface IPartitionResourceLoader
    {
        PartitionAssetHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object;
    }

    public interface IPartitionInstanceFactory
    {
        GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays);
    }

    public interface IPartitionWorldContext<TStatus>
    {
        int MapClassId { get; }
        int CurrentRegion { get; }
        Vector3 PlayerWorldPosition { get; }
        event Action<int, List<int>> OnWorldMapProgressUpdate;
        event Action<int, int, TStatus, TStatus> OnMapStatusUpdate;
        List<TStatus> GetCurrentMapStatus();
        int GetMapRegionGoddessId(int mapClassId, int region);
        int GetWorldMapRegionProgress(int mapClassId, int region);
        bool WorldMapRegionCleared(int mapClassId, int region, int progress);
    }

    // Completion is dispatched after the official caller has stored the handle.
    // A local catalog owns asset lifetimes; releasing a handle releases this reference.
    public sealed class PartitionAssetHandle<T> where T : UnityEngine.Object
    {
        public T Result { get; private set; }
        public event Action<PartitionAssetHandle<T>> Completed;
        private bool released;
        private bool completed;

        public PartitionAssetHandle(T asset)
        {
            Result = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
        }

        public void DispatchCompletion()
        {
            if (completed || released) return;
            completed = true;
            Completed?.Invoke(this);
        }

        public void Release(bool tryUnloadAsset)
        {
            released = true;
            Result = null;
            Completed = null;
        }
    }
}
