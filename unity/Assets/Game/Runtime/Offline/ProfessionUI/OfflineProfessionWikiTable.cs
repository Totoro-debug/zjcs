using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Only the client-side region selection consumed by WikiEntry is adapted.
    public sealed class OfflineProfessionWikiTable
    {
        [Serializable] private sealed class Payload { public string manifest; public int clientRegion; public Row[] rows; }
        [Serializable] private sealed class Row { public string Type, URL; public int Region, LimitConditionId; }
        private readonly Dictionary<string, ProfessionWikiInfo> entries = new Dictionary<string, ProfessionWikiInfo>();
        public OfflineProfessionWikiTable(TextAsset originalTable)
        {
            if (originalTable == null) throw new ArgumentNullException(nameof(originalTable));
            Payload data = JsonUtility.FromJson<Payload>(originalTable.text);
            if (data == null || data.manifest != "871_156598" || data.clientRegion != 0 || data.rows == null)
                throw new InvalidOperationException("Original profession wiki table/region is invalid.");
            foreach (Row row in data.rows)
            {
                if (row.Region != data.clientRegion) continue;
                if (entries.ContainsKey(row.Type)) Debug.LogWarning("WikiInfoParser Duplicate wiki info type: " + row.Type + " Region: CN");
                else entries[row.Type] = new ProfessionWikiInfo(row.Type, row.Region, row.LimitConditionId, row.URL);
            }
        }
        public ProfessionWikiInfo GetWikiInfo(string type) => entries.TryGetValue(type, out var value) ? value : null;
        public string GetURL(string type) => GetWikiInfo(type)?.URL;
        public string GetTitleKey(string type) => "wiki_title_" + type;
    }
}
