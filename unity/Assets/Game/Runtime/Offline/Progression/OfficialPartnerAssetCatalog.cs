using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    /// <summary>Explicit bindings to the selected partner configuration and property binary projection.</summary>
    public sealed class OfficialPartnerAssetCatalog : ScriptableObject
    {
        [SerializeField] private TextAsset rules;
        [SerializeField] private TextAsset properties;
        private OfficialPartnerCatalog loaded;
        public OfficialPartnerCatalog Load()
        {
            if (rules == null || properties == null)
                throw new InvalidOperationException("Selected partner configuration bindings are missing.");
            return loaded ?? (loaded = OfficialPartnerCatalog.Parse(rules.text, properties.text));
        }
        public static OfficialPartnerCatalog LoadDefault()
        {
            var binding = Resources.Load<OfficialPartnerAssetCatalog>("Partners/OfficialPartnerAssetCatalog");
            if (binding == null) throw new InvalidOperationException("Selected partner asset catalog is missing.");
            return binding.Load();
        }
    }

    /// <summary>Selected official data; property lookup follows LevelPropParser.GetConfigProps/TryGetPropDict.</summary>
    public sealed class OfficialPartnerCatalog
    {
        private sealed class PropertyClass
        {
            public int Maximum;
            public Dictionary<int, IReadOnlyDictionary<int, long>> Rows;
        }
        private readonly Dictionary<string, List<Dictionary<string, object>>> tables;
        private readonly Dictionary<int, PropertyClass> classes;
        private OfficialPartnerCatalog(Dictionary<string, List<Dictionary<string, object>>> tables,
            Dictionary<int, PropertyClass> classes) { this.tables = tables; this.classes = classes; }
        public string Manifest => "884_156692";
        public IReadOnlyCollection<int> PropertyClassIds => classes.Keys;
        public IReadOnlyList<Dictionary<string, object>> Rows(string table) => tables.TryGetValue(table, out var rows)
            ? rows : throw new KeyNotFoundException("Selected partner table is missing: " + table);
        public bool ContainsPropertyClass(int classId) => classes.ContainsKey(classId);

        public IReadOnlyDictionary<int, long> GetLevelProps(int classId, int level)
        {
            if (!classes.TryGetValue(classId, out var group)) return null;
            if (group.Rows.TryGetValue(level, out var properties)) return properties;
            // Original parser clamps only beyond the configured maximum. A hole
            // below it remains absent; it does not select a preceding row.
            if (level < group.Maximum) return null;
            return group.Rows.TryGetValue(group.Maximum, out properties) ? properties : null;
        }

        public Dictionary<int, long> GetFriendshipLevelPropDiff(int classId, int fromLevel, int toLevel)
        {
            var result = new Dictionary<int, long>();
            var from = GetLevelProps(classId, fromLevel);
            var to = GetLevelProps(classId, toLevel);
            if (from == null || to == null) return result;
            foreach (var property in to)
                if (from.TryGetValue(property.Key, out long value))
                {
                    if (property.Value != value) result.Add(property.Key, property.Value - value);
                }
                else result.Add(property.Key, property.Value);
            return result;
        }

        public static OfficialPartnerCatalog Parse(string rulesJson, string propertiesJson)
        {
            var rules = Map(OfficialEquipmentJson.Parse(rulesJson));
            var properties = Map(OfficialEquipmentJson.Parse(propertiesJson));
            if ((string)rules["manifest"] != "884_156692" || (string)properties["manifest"] != "884_156692")
                throw new FormatException("Partner rules and properties must both be pinned to 884_156692.");
            var tables = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
            foreach (var entry in Map(rules["tables"]))
                tables.Add(entry.Key, entry.Value is List<object> rows ? rows.Select(Map).ToList()
                    : new List<Dictionary<string, object>> { Map(entry.Value) });
            foreach (var entry in Map(properties["tables"]))
                tables.Add(entry.Key, ((List<object>)entry.Value).Select(Map).ToList());
            var defaults = tables["level_prop_default_value"].ToDictionary(row => Int(row["ClassId"]));
            var classes = new Dictionary<int, PropertyClass>();
            foreach (var entry in Map(properties["level_prop"]))
            {
                int classId = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                var data = Map(entry.Value);
                var group = new PropertyClass { Maximum = Int(data["max"]), Rows = new Dictionary<int, IReadOnlyDictionary<int, long>>() };
                foreach (var row in Map(data["rows"]))
                {
                    var values = Map(row.Value).ToDictionary(value => int.Parse(value.Key, CultureInfo.InvariantCulture),
                        value => Convert.ToInt64(value.Value, CultureInfo.InvariantCulture));
                    if (defaults.TryGetValue(classId, out var defaultRow))
                        foreach (object property in (List<object>)defaultRow["PropTypes"])
                            if (!values.ContainsKey(Int(property))) values.Add(Int(property), Convert.ToInt64(defaultRow["DefaultValue"], CultureInfo.InvariantCulture));
                    group.Rows.Add(int.Parse(row.Key, CultureInfo.InvariantCulture), values);
                }
                classes.Add(classId, group);
            }
            foreach (var npc in tables["npc"])
                if (Int(npc["LevelPropId"]) > 0 && !classes.ContainsKey(Int(npc["LevelPropId"])))
                    throw new FormatException("Selected NPC friendship properties are incomplete: " + npc["Id"]);
            return new OfficialPartnerCatalog(tables, classes);
        }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object>
            ?? throw new FormatException("Expected an official partner data object.");
        private static int Int(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }
}
