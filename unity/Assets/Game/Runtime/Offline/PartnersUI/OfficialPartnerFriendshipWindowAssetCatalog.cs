using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerFriendshipWindowAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject windowPrefab;
        [SerializeField] private GameObject blurMaskPrefab;
        [SerializeField] private Material blurMaterial;
        [SerializeField] private uint clickEventId;
        [SerializeField] private AudioClip clickClip;
        public GameObject WindowPrefab => windowPrefab;
        public GameObject BlurMaskPrefab => blurMaskPrefab;
        public Material BlurMaterial => blurMaterial;
        public uint ClickEventId => clickEventId;
        public AudioClip ClickClip => clickClip;
        public static OfficialPartnerFriendshipWindowAssetCatalog LoadDefault()
        {
            var assets = Resources.Load<OfficialPartnerFriendshipWindowAssetCatalog>("Partners/OfficialPartnerFriendshipWindowAssetCatalog");
            if (assets == null || assets.windowPrefab == null || assets.blurMaskPrefab == null || assets.blurMaterial == null
                || assets.clickEventId == 0 || assets.clickClip == null)
                throw new InvalidOperationException("Original partner friendship window and blur resources are unbound.");
            return assets;
        }
    }
}
