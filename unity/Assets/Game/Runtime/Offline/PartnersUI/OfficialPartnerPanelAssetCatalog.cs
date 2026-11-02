using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerPanelAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class TextBinding { public string key, text; }
        [Serializable] public sealed class SoundBinding { public uint eventId; public AudioClip clip; }
        [SerializeField] private GameObject panelPrefab;
        [SerializeField] private TextBinding[] texts;
        [SerializeField] private SoundBinding[] sounds;
        public AudioClip GetSound(uint eventId)
        {
            foreach (var row in sounds ?? Array.Empty<SoundBinding>())
                if (row.eventId == eventId) return row.clip ? row.clip : throw new InvalidOperationException("Original partner event PCM is unbound: " + eventId);
            return null;
        }
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
