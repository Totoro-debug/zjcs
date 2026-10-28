using System;
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
            internal TextureHandle(T asset) { this.asset = asset; }
            public bool IsValid => asset;
            public int Status => IsValid ? 2 : 0;
            public T Result => asset;
            public void WaitForAsyncComplete()
            { if (!IsValid) throw new ObjectDisposedException(nameof(TextureHandle<T>)); }
            public void Release() { asset = null; }
            public event Action<IProfessionUnsafeAssetHandle<T>> Completed
            { add { value?.Invoke(this); } remove { } }
        }
        private readonly ProfessionNativeWeaponTextureResources resources;
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
            var asset = resources.TextureByGuid(guid) as T;
            if (!asset) throw new InvalidOperationException("Original weapon texture GUID has incompatible native type: " + guid);
            return new TextureHandle<T>(asset);
        }
        public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
        {
            if (atlas != resources.Atlas) throw new NotSupportedException("Original weapon atlas identity is unbound.");
            return resources.Atlas.MaterialAt(materialIndex);
        }
    }
}
