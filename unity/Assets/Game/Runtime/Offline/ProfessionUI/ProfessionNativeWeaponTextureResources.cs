using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class ProfessionNativeWeaponTextureResources : ScriptableObject
    {
        [Serializable] private sealed class TextureBinding { public string Guid; public Texture Texture; }
        [SerializeField] private string loaderPath, loaderGuid;
        [SerializeField] private YooAssetTextureLoader loader;
        [SerializeField] private ProfessionNativeTextureAtlas atlas;
        [SerializeField] private AppearanceAssetManagerConfig managerConfig;
        [SerializeField] private TextureBinding[] textures;
        public string LoaderPath => loaderPath;
        public string LoaderGuid => loaderGuid;
        public YooAssetTextureLoader Loader => loader;
        public ProfessionNativeTextureAtlas Atlas => atlas;
        public AppearanceAssetManagerConfig ManagerConfig => managerConfig;
        public ScriptableObject LoadPath(string path)
        {
            if (path == loaderPath) return loader;
            if (path == atlas.SourcePath) return atlas;
            throw new NotSupportedException("Original weapon texture resource is unbound: " + path);
        }
        public Texture TextureByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("Original texture GUID is empty.", nameof(guid));
            foreach (var entry in textures)
                if (entry.Guid == guid) return entry.Texture ? entry.Texture : throw new InvalidOperationException("Original weapon texture payload is unbound: " + guid);
            throw new NotSupportedException("Original weapon high texture is not converted: " + guid);
        }
    }
}
