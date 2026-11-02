using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerPanelAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject panelPrefab;
        public GameObject PanelPrefab => panelPrefab ? panelPrefab :
            throw new InvalidOperationException("Complete original partner panel prefab is unbound.");
        public static OfficialPartnerPanelAssetCatalog Load()
        {
            var value = Resources.Load<OfficialPartnerPanelAssetCatalog>("Partners/OfficialPartnerPanelAssetCatalog");
            if (!value || !value.PanelPrefab) throw new InvalidOperationException("Complete original partner panel is unavailable.");
            return value;
        }
    }
}
