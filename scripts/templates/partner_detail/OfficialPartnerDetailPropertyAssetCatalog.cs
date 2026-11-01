using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerDetailPropertyAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject propertyPrefab;
        [SerializeField] private TextAsset texts;
        [SerializeField] private OfficialPartnerGiftPageAssetCatalog common;
        public GameObject PropertyPrefab => propertyPrefab;
        public TextAsset Texts => texts;
        public OfficialPartnerGiftPageAssetCatalog Common => common;
        public static OfficialPartnerDetailPropertyAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerDetailPropertyAssetCatalog>("Partners/OfficialPartnerDetailPropertyAssetCatalog");
            if (value == null || value.propertyPrefab == null || value.texts == null || value.common == null)
                throw new InvalidOperationException("Original partner detail-property resources are unbound.");
            return value;
        }
    }
}
