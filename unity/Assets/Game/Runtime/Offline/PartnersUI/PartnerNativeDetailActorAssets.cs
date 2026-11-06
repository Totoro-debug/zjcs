using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerNativeDetailActorAssets : ScriptableObject
    {
        [Serializable] public sealed class AssetAddress { public string Path, Resource; public ScriptableObject Asset; }
        [Serializable] public sealed class PrefabAddress { public string Path, Resource; public GameObject Prefab; }
        [Serializable] public sealed class TextureAddress { public string Guid, Resource; }
        [SerializeField] private AppearanceConfig config;
        [SerializeField] private AssetAddress[] assets;
        [SerializeField] private PrefabAddress[] prefabs;
        [SerializeField] private TextureAddress[] textures;
        [SerializeField] private ProfessionNativeTextureAtlas[] atlases;
        [SerializeField] private string[] atlasSourceGuids = Array.Empty<string>();
        public AppearanceConfig Config => config ? config : throw new InvalidOperationException("Bind the original appearance configuration.");
        public ScriptableObject LoadAsset(string path)
        {
            foreach (var item in assets)
                if (item.Path == path)
                {
                    var asset = item.Asset ? item.Asset : Resources.Load<ScriptableObject>(item.Resource);
                    return asset ? asset : throw new InvalidOperationException("Packaged original partner actor resource is unavailable: " + item.Resource);
                }
            throw new InvalidOperationException("Packaged original partner actor asset is unavailable: " + path);
        }
        public GameObject LoadPrefab(string path)
        {
            foreach (var item in prefabs)
                if (item.Path == path)
                {
                    var prefab = item.Prefab ? item.Prefab : Resources.Load<GameObject>(item.Resource);
                    return prefab ? prefab : throw new InvalidOperationException("Packaged original partner prefab resource is unavailable: " + item.Resource);
                }
            return null; // The original live controller first tries the optional ID-specific prefab.
        }
        public bool TryTextureResource(string guid, out string resource)
        {
            foreach (var item in textures) if (item.Guid == guid) { resource = item.Resource; return true; }
            resource = null; return false;
        }
        public bool HasAtlas(ScriptableObject atlas)
        {
            foreach (var item in atlases) if (item == atlas) return true;
            if (atlas is ProfessionNativeTextureAtlas native && Array.IndexOf(atlasSourceGuids, native.SourceGuid) >= 0) return true;
            return false;
        }
    }
}
