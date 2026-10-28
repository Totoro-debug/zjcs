using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // The catalog owns packaged Unity assets; each operation owns its result
    // reference until Clear/Dispose releases it. Local requests are already
    // complete, so subscribing to Completed invokes immediately, as on the
    // original completed AssetOperationHandle.
    public sealed class OfflineProfessionImageResourceLoader : IProfessionImageResourceLoader
    {
        private readonly Dictionary<string, UnityEngine.Object> paths = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private readonly Dictionary<string, UnityEngine.Object> guids = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        public OfflineProfessionImageResourceLoader(OfflineProfessionImageCatalog catalog)
        {
            if (catalog == null || catalog.Entries == null) throw new ArgumentNullException(nameof(catalog));
            foreach (var entry in catalog.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Path) || string.IsNullOrEmpty(entry.AssetGuid) || entry.Asset == null)
                    throw new InvalidOperationException("Original profession image catalog has an incomplete binding: " + (entry == null ? "<null entry>" : entry.Path + " guid=" + entry.AssetGuid + " asset=" + entry.Asset));
                paths.Add(entry.Path, entry.Asset);
                guids.Add(entry.AssetGuid, entry.Asset);
            }
        }
        public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object => Load<T>(paths, path);
        public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object => LoadAsset<T>(path);
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object => Load<T>(guids, guid);
        public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => LoadAssetByGuid<T>(guid);
        private static IProfessionAssetOperation<T> Load<T>(Dictionary<string, UnityEngine.Object> index, string key) where T : UnityEngine.Object
        {
            index.TryGetValue(key, out var asset);
            return new Operation<T>(asset as T);
        }
        private sealed class Operation<T> : IProfessionAssetOperation<T> where T : UnityEngine.Object
        {
            private T result;
            private readonly int status;
            private bool disposed;
            public Operation(T value) { result = value; status = value != null ? 2 : 3; }
            public bool IsValid => !disposed;
            public int Status { get { Check(); return status; } }
            public T Result { get { Check(); return result; } }
            public event Action<IProfessionAssetOperation<T>> Completed
            {
                add { Check(); value(this); }
                remove { Check(); }
            }
            public void Dispose() { disposed = true; result = null; }
            private void Check() { if (disposed) throw new ObjectDisposedException(nameof(Operation<T>)); }
        }
    }
}
