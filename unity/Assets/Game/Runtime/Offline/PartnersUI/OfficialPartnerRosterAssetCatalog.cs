using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    /// <summary>Explicit runtime bindings for the original partner roster subtree and its card payload.</summary>
    public sealed class OfficialPartnerRosterAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class TextEntry
        {
            public string key;
            public string text;
        }

        [Serializable]
        private sealed class TextDocument
        {
            public TextEntry[] texts;
        }

        [SerializeField] private GameObject rosterPrefab;
        [SerializeField] private OfflineProfessionImageCatalog images;
        [SerializeField] private TextAsset cardTexts;
        [SerializeField] private TextAsset rosterTexts;

        public GameObject RosterPrefab => rosterPrefab;
        public OfflineProfessionImageCatalog Images => images;

        public static OfficialPartnerRosterAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialPartnerRosterAssetCatalog>("Partners/OfficialPartnerRosterAssetCatalog");
            if (value == null || value.rosterPrefab == null || value.images == null
                || value.cardTexts == null || value.rosterTexts == null)
                throw new InvalidOperationException("Official partner roster runtime bindings are missing.");
            return value;
        }

        public Func<string, string> CreateLocalizer()
        {
            var values = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            Add(values, cardTexts);
            Add(values, rosterTexts);
            return key => values.TryGetValue(key, out var value) ? value : key;
        }

        private static void Add(System.Collections.Generic.Dictionary<string, string> values, TextAsset source)
        {
            var document = JsonUtility.FromJson<TextDocument>(source.text);
            foreach (var entry in document?.texts ?? Array.Empty<TextEntry>())
                if (entry != null && !string.IsNullOrEmpty(entry.key)) values[entry.key] = entry.text ?? string.Empty;
        }
    }
}
