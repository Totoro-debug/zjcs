using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerPanelAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class TextBinding { public string key, text; }
        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private TextBinding[] texts;
        public GameObject PanelPrefab => panelPrefab ? panelPrefab :
            throw new InvalidOperationException("Complete original partner panel prefab is unbound.");
        public Func<string, string> CreateLocalizer(Func<string, string> fallback)
        {
            if (texts == null || texts.Length == 0) throw new InvalidOperationException("Original partner panel language is unbound.");
            var words = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in texts) words.Add(row.key, row.text);
            return key => words.TryGetValue(key, out var word) ? word : fallback(key);
        }
        public static OfficialPartnerPanelAssetCatalog Load()
        {
            var value = Resources.Load<OfficialPartnerPanelAssetCatalog>("Partners/OfficialPartnerPanelAssetCatalog");
            if (!value || !value.PanelPrefab) throw new InvalidOperationException("Complete original partner panel is unavailable.");
            return value;
        }
    }
}
