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
        // The normal progression payload intentionally contains a subset of the
        // official tables. Battles add their pinned pet tables without mutating
        // that shared catalog, the save, or another feature's property sources.
        public OfficialCharacterCatalog WithBattlePetRules(string json, string expectedSha256)
        {
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
            if (hash != expectedSha256) throw new FormatException("Battle pet rule source hash differs.");
            var payload = Object(OfficialEquipmentJson.Parse(json), "battle pet rules");
            if (Text(payload, "manifest") != "854_156383") throw new FormatException("Battle pet rules are not pinned.");
            var merged = new Dictionary<string, List<Dictionary<string, object>>>(tables, StringComparer.Ordinal);
            var supplied = Object(payload, "tables");
            foreach (string name in new[] { "pet", "pet_evolution", "pet_ability_prop",
                "pet_ability_prop_scale_factor", "skill", "skill_rank", "item" })
                merged[name] = ((List<object>)supplied[name]).Select(value => Object(value, name + " row")).ToList();
            var mergedItems = new Dictionary<int, Dictionary<string, object>>(items);
            foreach (var row in merged["item"]) mergedItems[Integer(row, "ClassId")] = row;
            merged["item"] = mergedItems.Values.ToList();
            var properties = new Dictionary<int, Dictionary<int, Dictionary<int, long>>>(levelProps);
            foreach (var group in Object(payload, "level_prop"))
            {
                var rows = Object(Object(group.Value, "pet property group"), "rows");
                properties[int.Parse(group.Key, CultureInfo.InvariantCulture)] = rows.ToDictionary(
                    row => int.Parse(row.Key, CultureInfo.InvariantCulture),
                    row => Object(row.Value, "pet property row").ToDictionary(
                        prop => int.Parse(prop.Key, CultureInfo.InvariantCulture),
                        prop => Convert.ToInt64(prop.Value, CultureInfo.InvariantCulture)));
            }
            return new OfficialCharacterCatalog(merged, mergedItems, skins, appearances, allowed,
                levelExperience, levelLimits, blessLevels, timedAppearanceItems, propertyIds, properties,
                wardrobeTables, wardrobeDefinitions, rankBySubRank, defaultAppearance, appearanceItemTypes,
                props, itemIcons, professionNames, professionIcons, skillMergePieces, partnerProperties);
        }
    }
}
