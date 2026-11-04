using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerDetailWindowAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private OfficialPartnerPanelAssetCatalog.TextBinding[] texts;
        public GameObject PanelPrefab => panelPrefab ? panelPrefab :
            throw new InvalidOperationException("Complete original partner detail prefab is unbound.");
        public OfficialPartnerPanelAssetCatalog.TextBinding[] Texts => texts;
        public static OfficialPartnerDetailWindowAssetCatalog Load()
        {
            var value = Resources.Load<OfficialPartnerDetailWindowAssetCatalog>("Partners/OfficialPartnerDetailWindowAssetCatalog");
            if (!value || !value.PanelPrefab) throw new InvalidOperationException("Complete original partner detail is unavailable.");
            return value;
        }
    }
}
