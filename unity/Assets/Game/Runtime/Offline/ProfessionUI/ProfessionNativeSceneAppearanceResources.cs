using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // The full current low loader keeps original GUIDs. Main/dye texture payloads
    // have individual Resources addresses, so loading this catalog loads no main atlases.
    public sealed class ProfessionNativeSceneAppearanceResources : ScriptableObject
    {
        [Serializable] public sealed class Model
        {
            public string Sex, Guid;
            public ProfessionNativeSceneCharacterAsset Asset;
            public ProfessionNativeTextureAtlas Atlas;
            public string[] Pages;
        }
        [Serializable] public sealed class TextureAddress { public string Guid, Resource; }
        [SerializeField] private string skinGuid, loaderGuid;
        [SerializeField] private SkinTextureAsset skins;
        [SerializeField] private YooAssetTextureLoader loader;
        [SerializeField] private Model[] models;
        [SerializeField] private TextureAddress[] textures;
        public SkinTextureAsset Skins => skins ? skins : throw new InvalidOperationException("Original scene skin configuration is unbound.");
        public YooAssetTextureLoader Loader => loader ? loader : throw new InvalidOperationException("Original scene low loader is unbound.");
        public string SkinGuid => skinGuid;
        public string LoaderGuid => loaderGuid;
        public int TextureCount => textures.Length;
        public Model[] Models => (Model[])models.Clone();
        public TextureAddress[] TextureAddresses => (TextureAddress[])textures.Clone();
        public ScriptableObject ConfigurationByGuid(string guid)
        {
            if (guid == skinGuid) return Skins;
            if (guid == loaderGuid) return Loader;
            foreach (var model in models) if (model.Guid == guid) return model.Asset;
            throw new NotSupportedException("Original scene configuration GUID is unbound: " + guid);
        }
        public Model FindModel(ScriptableObject asset)
        {
            foreach (var model in models) if (model.Asset == asset) return model;
            throw new NotSupportedException("Original current scene model identity is unbound.");
        }
        public string TextureResource(string guid)
        {
            foreach (var entry in textures) if (entry.Guid == guid) return entry.Resource;
            throw new NotSupportedException("Original scene low texture GUID is unbound: " + guid);
        }
        public Material MaterialForPage(ScriptableObject asset, string page)
        {
            var model = FindModel(asset);
            int index = Array.IndexOf(model.Pages, page);
            if (index < 0) throw new NotSupportedException("Original scene atlas page is unbound: " + page);
            return model.Atlas.MaterialAt(index);
        }
    }
}
