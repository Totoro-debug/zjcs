using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerStarGodAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class ImageBinding { public string path; public Sprite sprite; }
        [Serializable] public sealed class TextBinding { public string key, text; }
        [SerializeField] private GameObject pagePrefab;
        [SerializeField] private PartnerStarGodNpcDefinition[] definitions;
        [SerializeField] private ImageBinding[] images;
        [SerializeField] private TextBinding[] texts;
        public GameObject PagePrefab => pagePrefab;
        public PartnerStarGodNpcDefinition[] Definitions => definitions;
        public ImageBinding[] Images => images;
        public TextBinding[] Texts => texts;
        public static OfficialPartnerStarGodAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerStarGodAssetCatalog>("Partners/OfficialPartnerStarGodAssetCatalog");
            if (!value || !value.pagePrefab || value.definitions == null || value.images == null || value.texts == null)
                throw new InvalidOperationException("Original star-god page resources are unbound.");
            return value;
        }
    }
}
