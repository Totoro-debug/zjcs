using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerDialoguePlaybackAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject panelPrefab;
        [Serializable] public sealed class TextBinding { public string key, text; }
        [Serializable] public sealed class SoundBinding { public uint eventId; public AudioClip clip; }
        [SerializeField] private TextBinding[] texts;
        [SerializeField] private SoundBinding[] sounds;
        public GameObject PanelPrefab => panelPrefab;
        public Func<string, string> CreateLocalizer(Func<string, string> fallback)
        {
            var words = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in texts ?? throw new InvalidOperationException("Original conversation text is unbound.")) words.Add(row.key, row.text);
            return key => words.TryGetValue(key, out var text) ? text : fallback(key);
        }
        public AudioClip GetSound(uint eventId)
        {
            foreach (var row in sounds ?? Array.Empty<SoundBinding>())
                if (row.eventId == eventId) return row.clip ? row.clip : throw new InvalidOperationException("Original conversation event PCM is unbound.");
            return null;
        }
        public static OfficialPartnerDialoguePlaybackAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerDialoguePlaybackAssetCatalog>("Partners/OfficialPartnerDialoguePlaybackAssetCatalog");
            if (!value || !value.panelPrefab) throw new InvalidOperationException("Original partner dialogue playback window is unbound.");
            return value;
        }
    }
}
