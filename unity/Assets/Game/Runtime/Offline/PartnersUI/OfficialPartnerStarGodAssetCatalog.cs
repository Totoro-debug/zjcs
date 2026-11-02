using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerStarGodAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class ImageBinding { public string path, assetGuid; public Sprite sprite; }
        [Serializable] public sealed class TextBinding { public string key, text; }
        [SerializeField] private GameObject pagePrefab;
        [SerializeField] private PartnerStarGodNpcDefinition[] definitions;
        [SerializeField] private ImageBinding[] images;
        [SerializeField] private TextBinding[] texts;
        public GameObject PagePrefab => pagePrefab;
        public PartnerStarGodNpcDefinition[] Definitions => definitions;
        public ImageBinding[] Images => images;
        public TextBinding[] Texts => texts;
        public OfflineProfessionImageCatalog CreateImageCatalog()
        {
            var value = CreateInstance<OfflineProfessionImageCatalog>();
            value.Entries = new OfflineProfessionImageCatalog.Entry[images.Length];
            for (int i = 0; i < images.Length; i++) value.Entries[i] = new OfflineProfessionImageCatalog.Entry {
                Path = "Assets/Image/" + images[i].path, AssetGuid = images[i].assetGuid, Asset = images[i].sprite };
            return value;
        }
        public static OfficialPartnerStarGodAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerStarGodAssetCatalog>("Partners/OfficialPartnerStarGodAssetCatalog");
            if (!value || !value.pagePrefab || value.definitions == null || value.images == null || value.texts == null)
                throw new InvalidOperationException("Original star-god page resources are unbound.");
            return value;
        }
    }
}
