using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>The original LocalizationTextService lookup/fallback and ProcessText branches,
    /// supplied with the report's verified local zh_CN entries instead of injected data providers.</summary>
    public static class WorldBattleReportTextService
    {
        [Serializable] private sealed class Catalog { public string manifest; public Entry[] texts; }
        [Serializable] private sealed class Entry { public string key, text; }
        private static Dictionary<string, string> entries;

        public static string GetText(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (entries == null)
            {
                var asset = Resources.Load<TextAsset>("Combat/BattleReport/report-texts");
                if (asset == null) throw new InvalidOperationException("Original report language data is missing.");
                var catalog = JsonUtility.FromJson<Catalog>(asset.text);
                if (catalog == null || catalog.manifest != "854_156383" || catalog.texts == null)
                    throw new InvalidOperationException("Original report language manifest is invalid.");
                entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Entry entry in catalog.texts) entries.Add(entry.key, ProcessText(entry.text));
            }
            return entries.TryGetValue(key, out string text) ? text : key;
        }

        public static string ProcessText(string text)
        {
            if (text.Length > 0 && text[0] == '#') text = text.Remove(0, 1);
            return text.Replace("\\n", "\n").Replace("\"\"", "\"");
        }
    }
}
