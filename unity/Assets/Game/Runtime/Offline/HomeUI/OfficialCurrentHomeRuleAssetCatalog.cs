using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.HomeUI
{
    // This binding reads the selected raw tables. Individual home systems must
    // explicitly opt into these tables when their original runtime is migrated.
    public sealed class OfficialCurrentHomeRuleAssetCatalog : ScriptableObject
    {
        public TextAsset configuration;
        private Dictionary<string,object> tables;

        public static OfficialCurrentHomeRuleAssetCatalog LoadDefault()
            => Resources.Load<OfficialCurrentHomeRuleAssetCatalog>("Home/OfficialCurrentHomeRuleAssetCatalog")
                ?? throw new InvalidOperationException("Selected official home configuration is unavailable.");

        public IReadOnlyList<Dictionary<string,object>> Rows(string table)
        {
            EnsureLoaded();
            return tables.TryGetValue(table,out var value) && value is List<Dictionary<string,object>> rows ? rows
                : throw new KeyNotFoundException("Selected official home table is unavailable: "+table);
        }

        public IReadOnlyDictionary<string,object> Record(string table)
        {
            EnsureLoaded();
            return tables.TryGetValue(table,out var value) && value is Dictionary<string,object> row ? row
                : throw new KeyNotFoundException("Selected official home singleton is unavailable: "+table);
        }

        public HomeConditionRules CreateConditions(PartnerConditionRules partners,
            Func<IReadOnlyDictionary<string,object>,bool?> offlinePolicy=null)
            => new HomeConditionRules(Rows("conditions"),partners,offlinePolicy);

        private void EnsureLoaded()
        {
            if (tables!=null) return;
            if (configuration==null) throw new InvalidOperationException("Selected home configuration binding is absent.");
            var root=JObject.Parse(configuration.text);
            if ((string)root["manifest"]!="884_156692")
                throw new FormatException("Unexpected selected home configuration version.");
            var parsed=new Dictionary<string,object>(StringComparer.Ordinal);
            foreach (var table in ((JObject)root["tables"]).Properties())
            {
                if (table.Value is JArray rows && rows.Count>0)
                    parsed.Add(table.Name,rows.Select(row=>Value(row) as Dictionary<string,object>
                        ?? throw new FormatException("Selected home table row is not an object: "+table.Name)).ToList());
                else if (table.Value is JObject record && record.Count>0)
                    parsed.Add(table.Name,Value(record));
                else throw new FormatException("Selected home table is empty or invalid: "+table.Name);
            }
            tables=parsed;
        }

        private static object Value(JToken token)
        {
            if (token is JObject obj) return obj.Properties().ToDictionary(property=>property.Name,property=>Value(property.Value));
            if (token is JArray array) return array.Select(Value).ToList();
            return ((JValue)token).Value;
        }
    }
}
