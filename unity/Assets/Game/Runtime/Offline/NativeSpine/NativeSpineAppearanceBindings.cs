using System;
using System.Collections.Generic;
using System.Linq;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.NativeSpine
{
    // Shared local resource ownership for native Spine application controllers.
    // The original texture loader and material manager still decide requests,
    // reference counts, replacement materials and unload timing.
    public sealed class NativeSpineAppearanceBindings : IProfessionAppearanceUILiteBindings,
        IProfessionAppearanceTextureBindings, IDisposable
    {
        sealed class AssetHandle : IProfessionAppearanceCharacterAsset
        {
            ScriptableObject value;
            Action release;
            internal AssetHandle(ScriptableObject value, Action release) { this.value = value; this.release = release; }
            public ScriptableObject Result => value ? value : throw new ObjectDisposedException(nameof(AssetHandle));
            public void Dispose() { value = null; var callback = release; release = null; callback?.Invoke(); }
        }
        sealed class TextureHandle<T> : IProfessionUnsafeAssetHandle<T> where T : Object
        {
            T value;
            Action release;
            internal TextureHandle(T value, Action release) { this.value = value; this.release = release; }
            public bool IsValid => value;
            public int Status => IsValid ? 2 : 0;
            public T Result => value;
            public void WaitForAsyncComplete() { if (!IsValid) throw new ObjectDisposedException(nameof(TextureHandle<T>)); }
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed { add { value?.Invoke(this); } remove { } }
            public void Release() { value = null; var callback = release; release = null; callback?.Invoke(); }
        }
        sealed class LoadedTexture { internal Texture2D Asset; internal int References; }
        static readonly Dictionary<string, LoadedTexture> shared = new Dictionary<string, LoadedTexture>(StringComparer.Ordinal);
        readonly Dictionary<string, ScriptableObject> assets;
        readonly Dictionary<string, string> textures;
        readonly HashSet<SpineAtlasAsset> atlases;
        readonly Dictionary<string, int> requests = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly IDisposable registration;
        bool disposed;
        public int OpenAssetHandles { get; private set; }
        public int OpenTextureHandles => requests.Values.Sum();
        public AppearanceConfig Config { get; }
        public IProfessionAppearanceUIAssetManager AssetManager { get; }

        public NativeSpineAppearanceBindings(NativeSpineAppearanceCatalog catalog, IProfessionAppearanceUIAssetManager assetManager)
        {
            if (!catalog || !catalog.Config) throw new ArgumentException("The original appearance catalog and configuration are required.", nameof(catalog));
            Config = catalog.Config;
            AssetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
            assets = catalog.Assets.ToDictionary(value => value.Path, value => value.Asset, StringComparer.Ordinal);
            textures = catalog.Textures.ToDictionary(value => value.Guid, value => value.Resource, StringComparer.Ordinal);
            atlases = new HashSet<SpineAtlasAsset>(catalog.Atlases);
            if (assets.Any(pair => string.IsNullOrEmpty(pair.Key) || !pair.Value) ||
                textures.Any(pair => string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value)) || atlases.Any(value => !value))
                throw new InvalidOperationException("The native Spine appearance catalog contains an unbound resource.");
            registration = ProfessionAppearanceTextureBindings.Register(this, textures.Keys, atlases.Cast<ScriptableObject>());
        }
        void CheckLifetime() { if (disposed) throw new ObjectDisposedException(nameof(NativeSpineAppearanceBindings)); }
        public IProfessionAppearanceCharacterAsset LoadAsset(string path)
        {
            CheckLifetime();
            if (!assets.TryGetValue(path, out var asset)) throw new InvalidOperationException("Original native Spine asset is unbound: " + path);
            OpenAssetHandles++;
            return new AssetHandle(asset, () => OpenAssetHandles--);
        }
        public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : Object
        {
            CheckLifetime();
            if (!textures.TryGetValue(guid, out var resource)) throw new InvalidOperationException("Original native Spine texture GUID is unbound: " + guid);
            if (!typeof(T).IsAssignableFrom(typeof(Texture2D))) throw new NotSupportedException("The original native Spine request is not a Texture2D.");
            if (!shared.TryGetValue(resource, out var entry))
            {
                var texture = Resources.Load<Texture2D>(resource);
                if (!texture) throw new InvalidOperationException("Original native Spine texture is unavailable: " + resource);
                shared.Add(resource, entry = new LoadedTexture { Asset = texture });
            }
            requests.TryGetValue(resource, out int count); requests[resource] = count + 1;
            entry.References++;
            return new TextureHandle<T>(entry.Asset as T, () => Release(resource, entry));
        }
        void Release(string resource, LoadedTexture entry)
        {
            int count = requests[resource] - 1;
            if (count == 0) requests.Remove(resource); else requests[resource] = count;
            if (--entry.References != 0) return;
            shared.Remove(resource);
            if (entry.Asset) Resources.UnloadAsset(entry.Asset);
            entry.Asset = null;
        }
        public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
        {
            CheckLifetime();
            if (!(atlas is SpineAtlasAsset native) || !atlases.Contains(native))
                throw new InvalidOperationException("Original native Spine atlas identity is unbound.");
            return native.Materials.ElementAt(materialIndex);
        }
        public void Dispose()
        {
            if (disposed) return;
            if (OpenAssetHandles != 0 || OpenTextureHandles != 0)
                throw new InvalidOperationException("Destroy the native Spine render owners before disposing their resource catalog.");
            registration.Dispose();
            disposed = true;
        }
    }
}
