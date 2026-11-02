using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerConfirmAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private TextAsset texts;
        public GameObject Prefab => prefab;
        public TextAsset Texts => texts;
        public static OfficialPartnerConfirmAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerConfirmAssetCatalog>("Partners/OfficialPartnerConfirmAssetCatalog");
            if (value == null || value.prefab == null || value.texts == null)
                throw new InvalidOperationException("Official partner confirm dialog resources are unbound.");
            return value;
        }
    }
}
