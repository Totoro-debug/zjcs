using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfflinePartnerLocalizationService : ILocalizationTextService
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        public LanguageType CurrentLanguage => LanguageType.zh_CN;
        public OfflinePartnerLocalizationService(params TextAsset[] documents)
        { foreach (var document in documents ?? Array.Empty<TextAsset>()) Add(document); }
        public void ReloadData() { }
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public bool TryGetOriginalText(string key, LanguageType language, out string result) => values.TryGetValue(key, out result);
        public bool TryGetText(string key, LanguageType language, out string result) => values.TryGetValue(key, out result);
        public bool TryGetText(string key, out string text) => values.TryGetValue(key, out text);
        public string GetText(string key, LanguageType language, params object[] paras)
        { string value = values.TryGetValue(key, out var text) ? text : key; return paras == null || paras.Length == 0 ? value : string.Format(value, paras); }
        public string GetTextFormated(string key, LanguageType language, params object[] args)
            => string.Format(GetText(key, language), args ?? Array.Empty<object>());
        public void SetReplacer(ITextReplacer textReplacer) { }
        public void AddDataProvider(ILanguageTextDataProvider languageTextDataProvider) { }
        private void Add(TextAsset document)
        {
            if (document == null) return;
            var data = JsonUtility.FromJson<TextCatalog>(document.text);
            foreach (var entry in data?.texts ?? Array.Empty<TextEntry>())
                if (entry != null && !string.IsNullOrEmpty(entry.key)) values[entry.key] = entry.text ?? string.Empty;
        }
        [Serializable] private sealed class TextCatalog { public TextEntry[] texts; }
        [Serializable] private sealed class TextEntry { public string key; public string text; }
    }
}
