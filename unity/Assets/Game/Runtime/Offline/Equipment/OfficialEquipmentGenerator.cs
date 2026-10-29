using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Equipment
{
    public sealed class EquipmentRollContext
    {
        public int DropLevel;
        public int PlayerLevel;
        public int SubRank;
        public int SourceOperate;
        public bool Daily;
        public int MinimumLevel;
        public int MaximumLevel;
        public int MinimumBlessLevel;
        public int MaximumBlessLevel;
    }

    public static class OfficialEquipmentGenerator
    {
        public static OfflineEquipmentInstance Generate(OfficialEquipmentCatalog catalog, int itemId,
            long instanceId, EquipmentRollContext context, Func<float> random)
        {
            if (catalog == null || context == null || random == null || itemId <= 0 || instanceId <= 0)
                throw new ArgumentException("Official equipment generation requires rules, identity, context and random source.");
            Dictionary<string, object> equip = Require(catalog.Find("equip", itemId), "equip", itemId);
            Dictionary<string, object> item = Require(catalog.Find("item", itemId), "item", itemId);
            Dictionary<string, object> rank = RankRow(catalog, itemId, equip, item);

            int level = Int(equip, "Level");
            if (level <= 0)
            {
                Dictionary<string, object> settings = catalog.GameSettings as Dictionary<string, object>;
                level = context.DropLevel + Int(settings, "EquipDropLevelOffsetLower")
                    + Pick(Int(settings, "EquipDropLevelOffsetUpper") - Int(settings, "EquipDropLevelOffsetLower") + 1, random);
                if (context.Daily) level = Clamp(level, context.MinimumLevel, context.MaximumLevel);
                level = Math.Min(level, Int(settings, "EquipDropMaxLevel"));
                int levelLimit = Int(equip, "LevelLimit");
                if (levelLimit > 0) level = Math.Min(level, levelLimit);
                level = Math.Max(1, level);
            }

            int blessLevel = DropBlessLevel(catalog, equip, context, random);
            int secondary = WeightedList(Ints(equip, "SecPropTypes"), Ints(equip, "SecPropWeights"), random);
            var terms = new List<OfflineEquipmentTerm>();
            if (Bool(equip, "OtherPropFix"))
            {
                foreach (int property in Ints(equip, "OtherPropTypes")) terms.Add(Normal(catalog, itemId, property, rank, random));
                foreach (int attribute in Ints(equip, "OtherPropAttributes"))
                    terms.Add(Attribute(catalog, attribute, random));
            }
            else
            {
                List<int> limits = Ints(rank, "RankOtherPropNums");
                if (limits.Count > 0)
                {
                    int count = limits[0] + Pick(limits[limits.Count - 1] - limits[0] + 1, random);
                    for (int index = 0; index < count; index++)
                        terms.Add(RandomTerm(catalog, itemId, terms, random));
                }
            }
            terms = terms.Where(value => value != null).OrderBy(value => value.Type).ToList();
            return new OfflineEquipmentInstance
            {
                InstanceId = instanceId,
                ItemId = itemId,
                ItemLevel = level,
                BlessLevel = blessLevel,
                SecondaryProperty = secondary,
                SuitId = WeightedMap(equip, "SuitId", random),
                Terms = terms
            };
        }

        public static OfflineEquipmentTerm GenerateReplacement(OfficialEquipmentCatalog catalog, int itemId,
            IReadOnlyList<OfflineEquipmentTerm> existingTerms, Func<float> random)
        {
            if (catalog == null || random == null) throw new ArgumentNullException();
            return RandomTerm(catalog, itemId,
                existingTerms == null ? new List<OfflineEquipmentTerm>() : existingTerms.ToList(), random);
        }

        private static OfflineEquipmentTerm RandomTerm(OfficialEquipmentCatalog catalog, int itemId,
            IReadOnlyList<OfflineEquipmentTerm> previous, Func<float> random)
        {
            Dictionary<string, object> equip = catalog.Find("equip", itemId);
            Dictionary<string, object> item = catalog.Find("item", itemId);
            if (equip == null || item == null) return null;
            Dictionary<int, List<int>> eligible = new Dictionary<int, List<int>>();
            Dictionary<string, object> weights = equip.TryGetValue("AttributeTypeWeights", out object rawWeights)
                ? rawWeights as Dictionary<string, object> : null;
            List<int> types = new List<int>();
            List<int> typeWeights = new List<int>();
            if (weights != null)
                foreach (KeyValuePair<string, object> entry in weights)
                {
                    int type = Int(entry.Key);
                    if (type != 1)
                    {
                        eligible[type] = Candidates(catalog, item, equip, type, previous);
                        if (eligible[type].Count == 0) continue;
                    }
                    types.Add(type);
                    typeWeights.Add(Convert.ToInt32(entry.Value, CultureInfo.InvariantCulture));
                }
            int selected = types.Count == 0 ? 1 : WeightedList(types, typeWeights, random);
            if (selected == 1)
            {
                int itemType = Int(item, "ItemType");
                Dictionary<string, object> type = catalog.Rows("equip_type")
                    .FirstOrDefault(value => Int(value, "ItemType") == itemType);
                List<int> properties = Ints(type, "OtherPropTypes");
                return properties.Count == 0 ? null : Normal(catalog, itemId, properties[Pick(properties.Count, random)],
                    RankRow(catalog, itemId, equip, item), random);
            }
            if (!eligible.TryGetValue(selected, out List<int> candidates) || candidates.Count == 0) return null;
            List<int> attributeWeights = candidates.Select(id => Int(catalog.Find("equip_attributes", id, "Id"), "Weight")).ToList();
            return Attribute(catalog, WeightedList(candidates, attributeWeights, random), random);
        }

        private static List<int> Candidates(OfficialEquipmentCatalog catalog, Dictionary<string, object> item,
            Dictionary<string, object> equip, int type, IReadOnlyList<OfflineEquipmentTerm> previous)
        {
            List<int> fixedAttributes = Ints(equip, "OtherPropAttributes");
            int itemQuality = Int(item, "Quality");
            int itemRank = Int(item, "LimitRank");
            int itemType = Int(item, "ItemType");
            var result = new List<int>();
            foreach (Dictionary<string, object> attribute in catalog.Rows("equip_attributes"))
            {
                int id = Int(attribute, "Id");
                if (Int(attribute, "Type") != type || Int(attribute, "Weight") <= 0) continue;
                if (equip.TryGetValue("OtherPropAttributes", out object fixedList) && fixedList is List<object>)
                {
                    if (!fixedAttributes.Contains(id)) continue;
                }
                else if (Int(attribute, "SystemType") != 1
                    || attribute.TryGetValue("EquipType", out object typeList) && typeList is List<object>
                        && !Ints(attribute, "EquipType").Contains(itemType)) continue;
                if (itemQuality < Int(attribute, "MinQuality") || itemQuality > Int(attribute, "MaxQuality")
                    || itemRank < Int(attribute, "MinRank") || itemRank > Int(attribute, "MaxRank")) continue;
                bool excluded = false;
                foreach (OfflineEquipmentTerm prior in previous)
                {
                    if (prior == null || prior.Type != type || type == 1) continue;
                    Dictionary<string, object> other = catalog.Find("equip_attributes", prior.PropertyId, "Id");
                    if (other == null) continue;
                    if (Int(other, "Id") == id)
                    {
                        if (Bool(other, "SelfExclusive")) { excluded = true; break; }
                    }
                    else if (Int(other, "ExclusiveGroup") != 0
                        && Int(other, "ExclusiveGroup") == Int(attribute, "ExclusiveGroup"))
                    {
                        excluded = true;
                        break;
                    }
                }
                if (!excluded) result.Add(id);
            }
            return result;
        }

        private static OfflineEquipmentTerm Attribute(OfficialEquipmentCatalog catalog, int id, Func<float> random)
        {
            Dictionary<string, object> config = catalog.Find("equip_attributes", id, "Id");
            if (config == null) return null;
            int type = Int(config, "Type");
            var result = new OfflineEquipmentTerm { Type = type, PropertyId = id };
            if (type == 2 || type == 3)
            {
                Dictionary<string, object> props = catalog.Find("equip_attributes_props", id, "Id");
                List<object> groups = Objects(props, "Props");
                result.Group = Pick(groups.Count, random);
            }
            else
            {
                Dictionary<string, object> passive = catalog.Find("equip_attributes_passive_skill", id, "Id");
                if (passive == null) return null;
                List<object> propGroups = Objects(passive, "Props");
                foreach (object value in propGroups)
                {
                    Dictionary<string, object> prop = value as Dictionary<string, object>;
                    int count = Objects(prop, "Values").Count;
                    result.Indices.Add(Pick(count, random));
                }
                List<int> qualities = Ints(passive, "Qualitys");
                List<int> weights = Ints(passive, "QualityWeights");
                result.SkillQuality = qualities.Count == 0 ? 0 : weights.Count == qualities.Count
                    && weights.Any(value => value > 0) ? WeightedList(qualities, weights, random)
                    : qualities[Pick(qualities.Count, random)];
            }
            return result;
        }

        private static OfflineEquipmentTerm Normal(OfficialEquipmentCatalog catalog, int itemId,
            int propertyType, Dictionary<string, object> rank, Func<float> random)
        {
            List<object> limits = Objects(rank, "RankOtherPropRange");
            if (limits.Count == 0) return null;
            float minimum = Convert.ToSingle(limits[0], CultureInfo.InvariantCulture);
            float factor = minimum;
            if (limits.Count > 1)
            {
                float maximum = Convert.ToSingle(limits[1], CultureInfo.InvariantCulture);
                factor = (float)(factor + (float)random() * (maximum - factor));
            }
            return new OfflineEquipmentTerm { Type = 1, PropertyId = -1, PropertyType = propertyType, Factor = factor };
        }

        private static int DropBlessLevel(OfficialEquipmentCatalog catalog, Dictionary<string, object> equip,
            EquipmentRollContext context, Func<float> random)
        {
            int value = Int(equip, "BlessLevel");
            if (value < 0)
            {
                value = 0;
                int playerRank = Rank(context.SubRank);
                int baseLimit = catalog.Rows("system_level_limit").Where(row => Rank(Int(row, "SubRank")) == playerRank
                        && Int(row, "PlayerBlessLevel") == 0)
                    .Select(row => Int(row, "PlayerLevel")).DefaultIfEmpty(0).Max();
                int playerBless = Math.Max(0, context.PlayerLevel - baseLimit);
                List<int> groups = catalog.Rows("source_operate_group")
                    .Where(row => Ints(row, "SourceOperates").Contains(context.SourceOperate))
                    .Select(row => Int(row, "GroupId")).ToList();
                foreach (int group in groups)
                {
                    List<Dictionary<string, object>> rows = catalog.Rows("equip_drop_bless_level")
                        .Where(row => Int(row, "SourceOperateGroupId") == group).ToList();
                    if (rows.Count == 0) continue;
                    foreach (Dictionary<string, object> row in rows)
                        if (Int(row, "PlayerRank") == playerRank && Int(row, "PlayerBlessLevel") == playerBless)
                            value = Int(row, "DropEquipBlessMin")
                                + Pick(Int(row, "DropEquipBlessMax") - Int(row, "DropEquipBlessMin") + 1, random);
                    break;
                }
                if (context.Daily) value = Clamp(value, context.MinimumBlessLevel, context.MaximumBlessLevel);
            }
            else if (context.Daily) return value;
            return Math.Min(Int(equip, "BlessLevelLimit"), value);
        }

        private static Dictionary<string, object> RankRow(OfficialEquipmentCatalog catalog, int itemId,
            Dictionary<string, object> equip, Dictionary<string, object> item)
        {
            int quality = Int(item, "Quality");
            int index = Int(equip, "EquipRankIndex");
            return catalog.Rows("equip_rank").FirstOrDefault(row => Int(row, "Quality") == quality
                && Int(row, "Index") == index) ?? Require(null, "equip_rank", itemId);
        }

        private static int WeightedMap(Dictionary<string, object> row, string key, Func<float> random)
        {
            if (row == null || !row.TryGetValue(key, out object value)
                || !(value is Dictionary<string, object> weights) || weights.Count == 0) return 0;
            List<string> ids = weights.Keys.ToList();
            List<int> values = weights.Values.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToList();
            int total = values.Sum();
            if (total <= 0) return 0;
            int selected = Pick(total, random);
            for (int index = 0; index < ids.Count; index++)
            {
                selected -= values[index];
                if (selected < 0) return Int(ids[index]);
            }
            return Int(ids[ids.Count - 1]);
        }

        private static int WeightedList(IReadOnlyList<int> values, IReadOnlyList<int> weights, Func<float> random)
        {
            if (values == null || weights == null || values.Count == 0 || values.Count != weights.Count) return 0;
            int total = weights.Sum();
            if (total <= 0) return 0;
            int selected = Pick(total, random);
            for (int index = 0; index < values.Count; index++)
            {
                selected -= weights[index];
                if (selected < 0) return values[index];
            }
            return values[values.Count - 1];
        }

        private static int Pick(int count, Func<float> random)
        {
            if (count <= 0) return 0;
            float value = random();
            if (float.IsNaN(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(random));
            return Math.Max(0, Math.Min(count - 1, (int)(value * count)));
        }

        private static int Rank(int subRank) => subRank < 2 ? 1 : 2 + (subRank - 2) / 3;
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static Dictionary<string, object> Require(Dictionary<string, object> value, string table, int id)
        {
            return value ?? throw new KeyNotFoundException("Official " + table + " row is missing: " + id);
        }

        private static int Int(Dictionary<string, object> value, string key) =>
            value != null && value.TryGetValue(key, out object item) ? Convert.ToInt32(item, CultureInfo.InvariantCulture) : 0;

        private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);

        private static bool Bool(Dictionary<string, object> value, string key) =>
            value != null && value.TryGetValue(key, out object item) && Convert.ToBoolean(item, CultureInfo.InvariantCulture);

        private static List<int> Ints(Dictionary<string, object> value, string key)
        {
            if (value == null || !value.TryGetValue(key, out object raw) || raw == null) return new List<int>();
            if (raw is List<object> list) return list.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToList();
            return new List<int>();
        }

        private static List<object> Objects(Dictionary<string, object> value, string key) =>
            value != null && value.TryGetValue(key, out object raw) && raw is List<object> list
                ? list : new List<object>();
    }
}
