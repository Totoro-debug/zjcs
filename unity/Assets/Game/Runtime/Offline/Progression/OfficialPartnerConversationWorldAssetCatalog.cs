using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    public sealed class OfficialPartnerConversationWorldAssetCatalog : ScriptableObject
    {
        [SerializeField] private TextAsset rules;
        public OfficialPartnerConversationWorldData Load()
        {
            if (rules == null) throw new InvalidOperationException("Original conversation map bindings are missing.");
            return OfficialPartnerConversationWorldData.Parse(rules.text);
        }
        public static OfficialPartnerConversationWorldData LoadDefault()
        {
            var asset = Resources.Load<OfficialPartnerConversationWorldAssetCatalog>("Partners/OfficialPartnerConversationWorldAssetCatalog");
            if (asset == null) throw new InvalidOperationException("Original conversation world catalog is missing.");
            return asset.Load();
        }
    }

    public sealed class OfficialPartnerConversationWorldData
    {
        private readonly Dictionary<int, Dictionary<int, int>> maxima = new Dictionary<int, Dictionary<int, int>>();
        private readonly Dictionary<(int, int), int> goddesses = new Dictionary<(int, int), int>();
        public IEnumerable<int> MapClassIds => maxima.Keys;
        public int GetMaximum(int mapClassId, int region)
        {
            if (!maxima.TryGetValue(mapClassId, out var values))
                throw new KeyNotFoundException("Map is outside the selected conversation closure: " + mapClassId);
            return values.TryGetValue(region, out int value) ? value : 0;
        }
        public int GetGoddessEntityId(int mapClassId, int region)
        {
            if (!goddesses.TryGetValue((mapClassId, region), out int entity))
                throw new KeyNotFoundException("Goddess query is outside the selected conversation closure: " + mapClassId + ":" + region);
            return entity;
        }
        public static OfficialPartnerConversationWorldData Parse(string json)
        {
            var root = Map(OfficialEquipmentJson.Parse(json));
            if ((string)root["manifest"] != "884_156692") throw new FormatException("Conversation map source version differs.");
            var result = new OfficialPartnerConversationWorldData();
            foreach (var row in ((List<object>)root["maps"]).Select(Map))
            {
                int id = Int(row["id"]);
                if (id <= 0) throw new FormatException("Invalid original conversation map identity.");
                var regions = new Dictionary<int, int>();
                foreach (var region in ((List<object>)row["regions"]).Select(Map))
                {
                    int key = Int(region["region"]), maximum = Int(region["maximum"]);
                    if (key < 0 || maximum < 0) throw new FormatException("Invalid original region progress maximum.");
                    regions.Add(key, maximum);
                }
                result.maxima.Add(id, regions);
                foreach (var goddess in ((List<object>)row["goddesses"]).Select(Map))
                {
                    int region = Int(goddess["region"]), entity = Int(goddess["entity"]);
                    if (region < 0 || entity < 0) throw new FormatException("Invalid original goddess identity.");
                    result.goddesses.Add((id, region), entity);
                }
            }
            return result;
        }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object>
            ?? throw new FormatException("Expected original conversation world object.");
        private static int Int(object value) => Convert.ToInt32(value);
    }
}
