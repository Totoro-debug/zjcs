using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Per-request ownership for the separately packaged current scene pages.
    // Configuration/placeholder assets stay owned by the source catalog.
    public sealed class OfflineProfessionSceneTextureResources : IProfessionAppearanceTextureBindings, IDisposable
    {
        private sealed class Entry { internal Texture2D Asset; internal string Resource; internal int References; }
        private sealed class Handle<T> : IProfessionUnsafeAssetHandle<T> where T : Object
        {
            private OfflineProfessionSceneTextureResources owner;
            private Entry entry;
            internal Handle(OfflineProfessionSceneTextureResources owner, Entry entry) { this.owner = owner; this.entry = entry; }
            public bool IsValid => entry != null && entry.Asset;
            public int Status => IsValid ? 2 : 0;
            public T Result => entry?.Asset as T;
            public void WaitForAsyncComplete() { if (!IsValid) throw new ObjectDisposedException(nameof(Handle<T>)); }
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed { add { value?.Invoke(this); } remove { } }
            public void Release()
            {
                if (entry == null) return;
                owner.Release(entry); entry = null; owner = null;
            }
        }
        private readonly ProfessionNativeSceneAppearanceResources resources;
        private static readonly Dictionary<string, Entry> shared = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> loaded = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool disposed;
        public int LoadedTextureCount => loaded.Count;
        public OfflineProfessionSceneTextureResources(ProfessionNativeSceneAppearanceResources resources)
        { this.resources = resources ? resources : throw new ArgumentNullException(nameof(resources)); }
        public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : Object
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineProfessionSceneTextureResources));
            if (!typeof(T).IsAssignableFrom(typeof(Texture2D))) throw new NotSupportedException("Original scene texture request has incompatible type.");
            string path = resources.TextureResource(guid);
            if (!shared.TryGetValue(path, out var entry))
            {
                var asset = Resources.Load<Texture2D>(path);
                if (!asset) throw new InvalidOperationException("Packaged original scene texture is unavailable: " + guid);
                entry = new Entry { Asset = asset, Resource = path };
                shared.Add(path, entry);
            }
            loaded.TryGetValue(path, out int references);
            loaded[path] = references + 1;
            entry.References++;
            return new Handle<T>(this, entry);
        }
        private void Release(Entry entry)
        {
            int references = loaded[entry.Resource] - 1;
            if (references == 0) loaded.Remove(entry.Resource);
            else loaded[entry.Resource] = references;
            if (--entry.References != 0) return;
            shared.Remove(entry.Resource);
            if (entry.Asset) Resources.UnloadAsset(entry.Asset);
            entry.Asset = null;
        }
        public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
        {
            foreach (var model in resources.Models) if (model.Atlas == atlas) return model.Atlas.MaterialAt(materialIndex);
            throw new NotSupportedException("Original current scene atlas identity is unbound.");
        }
        // Outstanding requests remain valid until their own final release.
        public void Dispose() { disposed = true; }
    }
}
