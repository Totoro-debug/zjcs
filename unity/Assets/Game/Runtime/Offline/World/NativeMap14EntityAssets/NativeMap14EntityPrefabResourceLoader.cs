using System;
using UnityEngine;
using Zjcs.Official.EntityLoading;

namespace Zjcs.Offline.World.NativeMap14EntityAssets
{
    public sealed class NativeMap14EntityPrefabResourceLoader : IResourceLoader
    {
        private readonly NativeMap14EntityPrefabCatalog catalog;

        public NativeMap14EntityPrefabResourceLoader(NativeMap14EntityPrefabCatalog catalog)
        { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

        public AssetOperationHandle<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object
        {
            if (typeof(T) != typeof(GameObject))
                throw new InvalidOperationException("Map14 entity loader only exposes original GameObject prefabs.");
            return new ImmediateAssetOperationHandle<T>(catalog.Load(path) as T);
        }

        private sealed class ImmediateAssetOperationHandle<T> : AssetOperationHandle<T> where T : UnityEngine.Object
        {
            private readonly T result;
            public ImmediateAssetOperationHandle(T result) { this.result = result; }
            public override int Status => result ? 2 : 3;
            public override T Result => result;
            public override event Action<AssetOperationHandle<T>> Completed
            {
                add { value?.Invoke(this); }
                remove { }
            }
            public override void Dispose() { }
        }
    }
}
