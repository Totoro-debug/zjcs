using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerGiftPageAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject giftPrefab;
        [SerializeField] private TextAsset giftTexts;
        [SerializeField] private TextAsset relationshipTexts;
        [SerializeField] private OfflineProfessionImageCatalog images;
        [SerializeField] private ItemIconConfig iconConfig;
        [SerializeField] private ItemIconAttachmentConfig attachmentConfig;
        [SerializeField] private ScriptableObject regionFont;
        [SerializeField] private Font sourceFont;
        public GameObject GiftPrefab => giftPrefab;
        public TextAsset GiftTexts => giftTexts;
        public TextAsset RelationshipTexts => relationshipTexts;
        public OfflineProfessionImageCatalog Images => images;
        public ItemIconConfig IconConfig => iconConfig;
        public ItemIconAttachmentConfig AttachmentConfig => attachmentConfig;
        public ScriptableObject RegionFont => regionFont;
        public Font SourceFont => sourceFont;
        public static OfficialPartnerGiftPageAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerGiftPageAssetCatalog>("Partners/OfficialPartnerGiftPageAssetCatalog");
            if (value == null || value.giftPrefab == null || value.giftTexts == null || value.relationshipTexts == null
                || value.images == null || value.iconConfig == null || value.attachmentConfig == null
                || value.regionFont == null || value.sourceFont == null)
                throw new InvalidOperationException("Official partner gift page resources are unbound.");
            return value;
        }
    }
}
