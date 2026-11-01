using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    // Build-time references keep the current original dialog and its data local.
    public sealed class OfficialPartnerFriendshipAssetCatalog : ScriptableObject
    {
        [SerializeField] private TextAsset propertyConfiguration;
        [SerializeField] private TextAsset propertyTexts;
        [SerializeField] private TextAsset dialogTexts;
        [SerializeField] private GameObject dialogPrefab;
        public TextAsset PropertyConfiguration => propertyConfiguration;
        public TextAsset PropertyTexts => propertyTexts;
        public TextAsset DialogTexts => dialogTexts;
        public GameObject DialogPrefab => dialogPrefab;

        public static OfficialPartnerFriendshipAssetCatalog LoadDefault()
        {
            var assets = Resources.Load<OfficialPartnerFriendshipAssetCatalog>("Partners/OfficialPartnerFriendshipAssetCatalog");
            if (assets == null || assets.propertyConfiguration == null || assets.propertyTexts == null || assets.dialogTexts == null || assets.dialogPrefab == null)
                throw new InvalidOperationException("Official partner friendship dialog/data bindings are missing.");
            return assets;
        }
    }
}
