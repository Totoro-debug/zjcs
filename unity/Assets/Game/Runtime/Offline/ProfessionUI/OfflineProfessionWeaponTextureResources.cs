using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // The explicit packaged catalog owns Unity assets. Each original request
    // has its own lifetime; releasing it never unloads another owner's asset.
    public sealed class OfflineProfessionWeaponTextureResources : IProfessionAppearanceUILiteBindings, IProfessionAppearanceTextureBindings
    {
        private sealed class ResourceHandle : IProfessionAppearanceCharacterAsset
        {
            private ScriptableObject asset;
            internal ResourceHandle(ScriptableObject asset) { this.asset = asset; }
            public ScriptableObject Result => asset ? asset : throw new ObjectDisposedException(nameof(ResourceHandle));
            public void Dispose() { asset = null; }
        }
        private sealed class TextureHandle<T> : IProfessionUnsafeAssetHandle<T> where T : Object
        {
            private T asset;
            private Action release;
            internal TextureHandle(T asset, Action release = null) { this.asset = asset; this.release = release; }
            public bool IsValid => asset;
            public int Status => IsValid ? 2 : 0;
            public T Result => asset;
            public void WaitForAsyncComplete()
            { if (!IsValid) throw new ObjectDisposedException(nameof(TextureHandle<T>)); }
            public void Release() { asset = null; var callback = release; release = null; callback?.Invoke(); }
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed
            { add { value?.Invoke(this); } remove { } }
        }
        private readonly ProfessionNativeWeaponTextureResources resources;
        private sealed class LoadedTexture { internal Texture2D Asset; internal string Resource; internal int References; }
        private static readonly Dictionary<string, LoadedTexture> shared = new Dictionary<string, LoadedTexture>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> requests = new Dictionary<string, int>(StringComparer.Ordinal);
        public int LoadedResourceTextureCount => requests.Count;
        public AppearanceConfig Config { get; }
        public IProfessionAppearanceUIAssetManager AssetManager { get; }
        public OfflineProfessionWeaponTextureResources(AppearanceConfig config, IProfessionAppearanceUIAssetManager manager, ProfessionNativeWeaponTextureResources resources)
        {
            Config = config ? config : throw new ArgumentNullException(nameof(config));
            AssetManager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.resources = resources ? resources : throw new ArgumentNullException(nameof(resources));
        }
        public IProfessionAppearanceCharacterAsset LoadAsset(string path) => new ResourceHandle(resources.LoadPath(path));
        public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : Object
        {
            string resource = resources.TextureResource(guid);
            if (!string.IsNullOrEmpty(resource))
            {
                if (!typeof(T).IsAssignableFrom(typeof(Texture2D)))
                    throw new NotSupportedException("Original weapon texture request has incompatible native type.");
                if (!shared.TryGetValue(resource, out var entry))
                {
                    var texture = Resources.Load<Texture2D>(resource);
                    if (!texture) throw new InvalidOperationException("Packaged original weapon texture is unavailable: " + guid);
                    entry = new LoadedTexture { Asset = texture, Resource = resource };
                    shared.Add(resource, entry);
                }
                requests.TryGetValue(resource, out int references); requests[resource] = references + 1;
                entry.References++;
                return new TextureHandle<T>(entry.Asset as T, () => Release(entry));
            }
            var asset = resources.TextureByGuid(guid) as T;
            if (!asset) throw new InvalidOperationException("Original weapon texture GUID has incompatible native type: " + guid);
            return new TextureHandle<T>(asset);
        }
        private void Release(LoadedTexture entry)
        {
            int references = requests[entry.Resource] - 1;
            if (references == 0) requests.Remove(entry.Resource); else requests[entry.Resource] = references;
            if (--entry.References != 0) return;
            shared.Remove(entry.Resource);
            if (entry.Asset) Resources.UnloadAsset(entry.Asset);
            entry.Asset = null;
        }
        public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
        {
            if (atlas != resources.Atlas) throw new NotSupportedException("Original weapon atlas identity is unbound.");
            return resources.Atlas.MaterialAt(materialIndex);
        }
    }
}
