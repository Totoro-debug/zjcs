using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerDialogueAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject entryPrefab;
        public GameObject EntryPrefab => entryPrefab;
        public static OfficialPartnerDialogueAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerDialogueAssetCatalog>("Partners/OfficialPartnerDialogueAssetCatalog");
            if (!value || !value.entryPrefab) throw new InvalidOperationException("Original partner dialogue entry is unbound.");
            return value;
        }
    }
}
