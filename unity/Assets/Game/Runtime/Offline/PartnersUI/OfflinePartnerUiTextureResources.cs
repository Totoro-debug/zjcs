using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.PartnersUI
{
    // Per-window service, with shared texture ownership across open actor views.
    // Original loaders decide when to release; each local handle releases once.
    public sealed class OfflinePartnerUiTextureResources : IProfessionAppearanceUILiteBindings, IProfessionAppearanceTextureBindings
    {
        private sealed class ResourceHandle : IProfessionAppearanceCharacterAsset
        {
            private ScriptableObject asset;
            internal ResourceHandle(ScriptableObject value) { asset = value; }
            public ScriptableObject Result => asset ? asset : throw new ObjectDisposedException(nameof(ResourceHandle));
            public void Dispose() { asset = null; }
        }
        private sealed class Handle<T> : IProfessionUnsafeAssetHandle<T> where T : Object
        {
            private T asset;
            private Action release;
            internal Handle(T value, Action release) { asset = value; this.release = release; }
            public bool IsValid => asset;
            public int Status => IsValid ? 2 : 0;
            public T Result => asset;
            public void WaitForAsyncComplete()
            { if (!IsValid) throw new ObjectDisposedException(nameof(Handle<T>)); }
            public void Release() { asset = null; var callback = release; release = null; callback?.Invoke(); }
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed
            { add { value?.Invoke(this); } remove { } }
        }
        private sealed class LoadedTexture { internal Texture2D Asset; internal string Resource; internal int References; }
        private static readonly Dictionary<string, LoadedTexture> shared = new Dictionary<string, LoadedTexture>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> requests = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly PartnerNativeDetailActorAssets assets;
        private readonly IProfessionAppearanceTextureBindings fallback;
        public AppearanceConfig Config => assets.Config;
        public IProfessionAppearanceUIAssetManager AssetManager { get; }
        public int LoadedResourceTextureCount => requests.Count;
        public OfflinePartnerUiTextureResources(PartnerNativeDetailActorAssets assets,
            IProfessionAppearanceUIAssetManager manager, IProfessionAppearanceTextureBindings fallback = null)
        {
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            AssetManager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.fallback = fallback;
        }
        public IProfessionAppearanceCharacterAsset LoadAsset(string path) => new ResourceHandle(assets.LoadAsset(path));
        public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : Object
        {
            if (!assets.TryTextureResource(guid, out string resource))
                return fallback?.LoadAssetSyncByGuid<T>(guid) ?? throw new InvalidOperationException("Original partner texture GUID is unbound: " + guid);
            if (!typeof(T).IsAssignableFrom(typeof(Texture2D)))
                throw new ArgumentException("Original partner texture request has an incompatible native type.");
            if (!shared.TryGetValue(resource, out var entry))
            {
                var texture = Resources.Load<Texture2D>(resource);
                if (!texture) throw new InvalidOperationException("Packaged original partner texture is unavailable: " + resource);
                entry = new LoadedTexture { Asset = texture, Resource = resource }; shared.Add(resource, entry);
            }
            requests.TryGetValue(resource, out int count); requests[resource] = count + 1; entry.References++;
            return new Handle<T>(entry.Asset as T, () => Release(entry));
        }
        private void Release(LoadedTexture entry)
        {
            int count = requests[entry.Resource] - 1;
            if (count == 0) requests.Remove(entry.Resource); else requests[entry.Resource] = count;
            if (--entry.References != 0) return;
            shared.Remove(entry.Resource);
            if (entry.Asset) Resources.UnloadAsset(entry.Asset);
            entry.Asset = null;
        }
        public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
        {
            if (assets.HasAtlas(atlas)) return ((ProfessionNativeTextureAtlas)atlas).MaterialAt(materialIndex);
            return fallback?.GetAtlasMaterial(atlas, materialIndex) ??
                throw new InvalidOperationException("Original partner atlas is unbound.");
        }
        public void BindTree(GameObject root)
        {
            if (!root || root.activeInHierarchy)
                throw new InvalidOperationException("Bind the original partner appearance before enabling its prefab.");
            foreach (var appearance in root.GetComponentsInChildren<AppearanceUILite>(true)) appearance.Configure(this);
        }
    }
}
