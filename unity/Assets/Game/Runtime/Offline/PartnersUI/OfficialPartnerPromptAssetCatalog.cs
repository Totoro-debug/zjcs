using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerPromptAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject inputPrefab;
        [SerializeField] private TextAsset texts;
        [SerializeField] private GameObject maskPrefab;
        public GameObject InputPrefab => inputPrefab;
        public TextAsset Texts => texts;
        public GameObject MaskPrefab => maskPrefab;
        public static OfficialPartnerPromptAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerPromptAssetCatalog>("Partners/OfficialPartnerPromptAssetCatalog");
            if (value == null || value.inputPrefab == null || value.texts == null || value.maskPrefab == null)
                throw new InvalidOperationException("Official partner input dialog resources are unbound.");
            return value;
        }
    }
}
