using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityLoading
{
    // Consumption boundaries for the original loader. These interfaces supply
    // existing shared state and platform services; they define no map rules.
    public interface IEntityLoadSharedContainer
    {
        ECViewEntityData RootEntity { get; }
        IReadOnlyDictionary<EcEntityIdentifier, ECViewEntityData> EntityDict { get; }
    }
    public interface IEntityLoadContainer
    {
        MonoBehaviour Host { get; }
        MyContainer LifecycleContainer { get; }
        IEntityLoadSharedContainer Container { get; }
        Transform EntitySlot { get; }
        bool ExistEntityGroupByClassId(int classId);
    }
    public interface IEntityLoadFacts
    {
        bool IsMapForbidLowMemoryDeal { get; }
        bool UseGameObjectPool { get; }
        ECViewEntityData GetCommonPrefab(int classId);
        bool IsRemoving(ECViewEntityData entity);
        bool HasGridGround(ECViewEntityData entity);
    }
    public interface IEntityLoadInstantiation
    {
        GameObject Instantiate(GameObject prefab, Vector3 worldPosition, Quaternion rotation, Transform parent);
    }
    public interface IEntityPoolScheduler
    {
        IDisposable Subscribe(TimeSpan interval, Action callback);
    }
    public interface IApplicationService
    {
        IDisposable SubscribeLowMemory(Action callback);
    }
    public interface IMyEntityIndexHandler
    {
        string GetDir(int entityClassId);
        int[] GetIdsByDir(string directory);
        string GetEntityPath(int entityClassId);
    }
    public interface IResourceLoader
    {
        AssetOperationHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object;
    }
    public abstract class AssetOperationHandle<T> : IDisposable where T : UnityEngine.Object
    {
        public abstract int Status { get; }
        public abstract T Result { get; }
        public abstract event Action<AssetOperationHandle<T>> Completed;
        public abstract void Dispose();
    }
    public static class EntityLoadServices
    {
        public static T Require<T>(IServiceProvider provider) where T : class =>
            provider?.GetService(typeof(T)) as T ??
            throw new InvalidOperationException("Original entity loading service is not bound: " + typeof(T).FullName);
    }
}
