using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfficialCharacterCatalog
    {
        // Selected home property inputs live in a separate payload. Copy the
        // dictionaries so attaching them never changes another feature's catalog.
        public OfficialCharacterCatalog WithHomePropertyRules(string json, string expectedSha256)
        {
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant() != expectedSha256)
                    throw new FormatException("Home property configuration source hash differs.");
            var payload = Object(OfficialEquipmentJson.Parse(json), "home property rules");
            if (Text(payload, "manifest") != "884_156692") throw new FormatException("Home property configuration is not pinned.");
            var merged = new Dictionary<string, List<Dictionary<string, object>>>(tables, StringComparer.Ordinal);
            var supplied = Object(payload, "tables");
            foreach (var entry in supplied)
                merged[entry.Key] = entry.Value is List<object> rows
                    ? rows.Select(value => Object(value, entry.Key + " row")).ToList()
                    : new List<Dictionary<string, object>> { Object(entry.Value, entry.Key + " row") };
            var mergedItems = new Dictionary<int, Dictionary<string, object>>(items);
            foreach (var row in merged["item"]) mergedItems[Integer(row, "ClassId")] = row;
            merged["item"] = mergedItems.Values.ToList();
            var properties = new Dictionary<int, Dictionary<int, Dictionary<int, long>>>(levelProps);
            foreach (var group in Object(payload, "level_prop"))
            {
                var rows = Object(Object(group.Value, "home property group"), "rows");
                properties[int.Parse(group.Key, CultureInfo.InvariantCulture)] = rows.ToDictionary(
                    row => int.Parse(row.Key, CultureInfo.InvariantCulture),
                    row => Object(row.Value, "home property row").ToDictionary(
                        prop => int.Parse(prop.Key, CultureInfo.InvariantCulture),
                        prop => Convert.ToInt64(prop.Value, CultureInfo.InvariantCulture)));
            }
            return new OfficialCharacterCatalog(merged, mergedItems, skins, appearances, allowed,
                levelExperience, levelLimits, blessLevels, timedAppearanceItems, propertyIds, properties,
                wardrobeTables, wardrobeDefinitions, rankBySubRank, defaultAppearance, appearanceItemTypes,
                props, itemIcons, professionNames, professionIcons, skillMergePieces, partnerProperties)
            {
                skillPropertyRules = skillPropertyRules,
                skillPropertyItems = skillPropertyItems
            };
        }
    }
}
