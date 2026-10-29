using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Equipment
{
    public sealed class OfficialEquipmentCatalog
    {
        private readonly Dictionary<string, List<Dictionary<string, object>>> tables;
        private readonly Dictionary<int, Dictionary<string, object>> propertyClasses;
        private readonly Dictionary<string, object> gameSettings;

        private OfficialEquipmentCatalog(Dictionary<string, object> root)
        {
            if (Text(root, "manifest") != "854_156383")
                throw new FormatException("Equipment rules are not pinned to 854_156383.");
            tables = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.Ordinal);
            Dictionary<string, object> rules = Object(root, "rules");
            foreach (KeyValuePair<string, object> pair in rules)
            {
                if (pair.Key == "game_settings")
                {
                    gameSettings = Object(pair.Value, "rules.game_settings");
                    continue;
                }
                tables.Add(pair.Key, Array(pair.Value, "rules." + pair.Key).Select((value, index) =>
                    Object(value, "rules." + pair.Key + "[" + index + "]")).ToList());
            }

            Dictionary<string, object> extra = Object(root, "tables");
            foreach (KeyValuePair<string, object> pair in extra)
                tables.Add(pair.Key, Array(pair.Value, "tables." + pair.Key).Select((value, index) =>
                    Object(value, "tables." + pair.Key + "[" + index + "]")).ToList());

            propertyClasses = new Dictionary<int, Dictionary<string, object>>();
            foreach (KeyValuePair<string, object> pair in Object(root, "props"))
                propertyClasses.Add(Int(pair.Key, "property class id"), Object(pair.Value, "property class"));
        }

        public static OfficialEquipmentCatalog Load(string json)
        {
            return new OfficialEquipmentCatalog(Object(OfficialEquipmentJson.Parse(json), "equipment runtime rules"));
        }

        public IReadOnlyList<Dictionary<string, object>> Rows(string table)
        {
            return tables.TryGetValue(table, out List<Dictionary<string, object>> rows)
                ? rows : System.Array.Empty<Dictionary<string, object>>();
        }

        public IReadOnlyDictionary<string, object> GameSettings => gameSettings;

        public Dictionary<string, object> Find(string table, int id, string key = "ClassId")
        {
            return Rows(table).FirstOrDefault(row => Integer(row, key) == id);
        }

        public int PropertyValue(int classId, int level, int propertyId)
        {
            if (!propertyClasses.TryGetValue(classId, out Dictionary<string, object> propertyClass)) return 0;
            Dictionary<string, object> rows = Object(propertyClass, "rows");
            int minimum = Integer(propertyClass, "min");
            int maximum = Integer(propertyClass, "max");
            string levelKey = Math.Max(minimum, Math.Min(maximum, level)).ToString(CultureInfo.InvariantCulture);
            if (!rows.TryGetValue(levelKey, out object value)) return DefaultPropertyValue(classId, propertyId);
            Dictionary<string, object> values = Object(value, "property values");
            return values.TryGetValue(propertyId.ToString(CultureInfo.InvariantCulture), out object amount)
                ? Convert.ToInt32(amount, CultureInfo.InvariantCulture)
                : DefaultPropertyValue(classId, propertyId);
        }

        public int PositionForItem(int itemId)
        {
            Dictionary<string, object> item = Find("item", itemId);
            if (item == null) return 0;
            Dictionary<string, object> itemType = Rows("equip_type").FirstOrDefault(row =>
                Integer(row, "ItemType") == Integer(item, "ItemType"));
            return itemType == null ? 0 : Integer(itemType, "EquipPos");
        }

        public bool CanWear(int itemId, int professionId)
        {
            Dictionary<string, object> item = Find("item", itemId);
            if (item == null) return false;
            Dictionary<string, object> itemType = Rows("equip_type").FirstOrDefault(row =>
                Integer(row, "ItemType") == Integer(item, "ItemType"));
            return itemType != null && Integers(itemType, "Professions").Contains(professionId);
        }

        private int DefaultPropertyValue(int classId, int propertyId)
        {
            Dictionary<string, object> row = Rows("level_prop_default_value").FirstOrDefault(value =>
                Integer(value, "ClassId") == classId && Integers(value, "PropTypes").Contains(propertyId));
            return row == null ? 0 : Integer(row, "DefaultValue");
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> value, string key)
        {
            return Object(value.TryGetValue(key, out object result) ? result : null, key);
        }

        private static Dictionary<string, object> Object(object value, string context)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(context + " must be an object.");
        }

        private static List<object> Array(object value, string context)
        {
            if (value is List<object> result) return result;
            throw new FormatException(context + " must be an array.");
        }

        private static string Text(Dictionary<string, object> value, string key)
        {
            return Convert.ToString(value.TryGetValue(key, out object result) ? result : null, CultureInfo.InvariantCulture);
        }

        private static int Integer(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object result) ? Convert.ToInt32(result, CultureInfo.InvariantCulture) : 0;
        }

        private static int Int(string value, string context)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) return result;
            throw new FormatException(context + " is invalid.");
        }

        private static IEnumerable<int> Integers(Dictionary<string, object> value, string key)
        {
            return value.TryGetValue(key, out object result) && result is List<object> values
                ? values.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)) : Enumerable.Empty<int>();
        }
    }
}
