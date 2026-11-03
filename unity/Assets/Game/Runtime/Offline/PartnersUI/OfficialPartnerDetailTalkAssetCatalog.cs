using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerDetailTalkAssetCatalog : ScriptableObject
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class TextRows { public string manifest; public TextRow[] texts; }
        [SerializeField] private GameObject talkPrefab;
        [SerializeField] private TextAsset texts;
        private Dictionary<string, string> dictionary;
        public GameObject TalkPrefab => talkPrefab;

        public string Localize(string key, params object[] arguments)
        {
            if (dictionary == null)
            {
                var source = JsonUtility.FromJson<TextRows>(texts.text);
                if (source.manifest != "884_156692" || source.texts == null)
                    throw new InvalidOperationException("Original detail talk language version differs.");
                dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in source.texts) dictionary.Add(row.key, row.text);
            }
            if (!dictionary.TryGetValue(key, out string value))
                throw new InvalidOperationException("Original detail talk language is unbound: " + key);
            return arguments == null || arguments.Length == 0 ? value : string.Format(value, arguments);
        }

        public static OfficialPartnerDetailTalkAssetCatalog Load()
        {
            var value = Resources.Load<OfficialPartnerDetailTalkAssetCatalog>("Partners/OfficialPartnerDetailTalkAssetCatalog");
            if (!value || !value.talkPrefab || !value.texts)
                throw new InvalidOperationException("Original detail talk assets are unbound.");
            return value;
        }
    }
}
