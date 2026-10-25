using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    /// <summary>
    /// Original <c>EquipPropChunk</c>: main/secondary/other equipment properties, upgraded and
    /// blessed values, and the active equipment suit thresholds.
    /// </summary>
    public sealed class OfflineEquipmentPropSource : IOfflineCharacterScoredPropSource
    {
        private static readonly int[] Positions = { 10, 11, 12, 13, 14 };

        private readonly OfficialEquipmentCatalog catalog;
        private readonly Func<int, int> rankOfSubRank;

        public OfflineEquipmentPropSource(OfficialEquipmentCatalog equipmentCatalog, Func<int, int> rankResolver)
        {
            catalog = equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            rankOfSubRank = rankResolver ?? (_ => 1);
        }

        public int HandleType => CharacterPropHandleType.Equip;

        public OfficialEquipmentCatalog Catalog => catalog;

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player)
        {
            return CalculateProps(player, null);
        }

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player,
            CharacterPropScoreContext context)
        {
            var result = new Dictionary<int, long>();
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null) continue;
                CharacterPropFormula.Merge(result, BaseItemProps(player, instance));
            }
            foreach (TermCandidate candidate in EffectiveTerms(player, context))
                if (candidate.Instance != null)
                    CharacterPropFormula.Merge(result, TermProperties(player, candidate.Term, candidate.Instance));
            CharacterPropFormula.Merge(result, SuitProps(player));
            return result;
        }

        public long CalculateCombatRating(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> props,
            CharacterPropScoreContext context)
        {
            long rating = 0L;
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null) continue;
                rating += context.Common(BaseItemProps(player, instance));
                rating += SuitCombatRating(player, instance, context);
            }
            foreach (TermCandidate candidate in EffectiveTerms(player, context))
                rating += TermCombatRating(player, candidate, context);
            return rating;
        }

        /// <summary>
        /// Official <c>EquipResonanceInfo</c>: the highest consecutive resonance level whose
        /// "equipped items at or above this level" count is met; its effect replaces lower levels.
        /// </summary>
        public Dictionary<int, long> ResonanceProps(OfflinePlayerProgress player)
        {
            var result = new Dictionary<int, long>();
            var levels = new List<int>();
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance != null) levels.Add(instance.ItemLevel + instance.BlessLevel);
            }
            Dictionary<string, object> active = null;
            foreach (Dictionary<string, object> row in catalog.Rows("equip_resonance")
                .OrderBy(value => Integer(value, "Level")))
            {
                int requiredLevel = Integer(row, "EquipLevel");
                int requiredCount = Integer(row, "EquipCount");
                if (requiredCount <= 0 || levels.Count(value => value >= requiredLevel) < requiredCount)
                    break;
                active = row;
            }
            if (active == null || !(active.TryGetValue("Effect", out object raw)
                && raw is Dictionary<string, object> effect)) return result;
            foreach (KeyValuePair<string, object> entry in effect)
                result[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>Combined properties of a single equipped item (without the suit bonus).</summary>
        public Dictionary<int, long> ItemProps(OfflinePlayerProgress player, OfflineEquipmentInstance instance)
        {
            return BuildItemProps(player, instance, true);
        }

        private Dictionary<int, long> BaseItemProps(OfflinePlayerProgress player, OfflineEquipmentInstance instance)
        {
            return BuildItemProps(player, instance, false);
        }

        private Dictionary<int, long> BuildItemProps(OfflinePlayerProgress player, OfflineEquipmentInstance instance,
            bool includeSpecialTerms)
        {
            var result = new Dictionary<int, long>();
            Dictionary<string, object> equip = catalog.Find("equip", instance.ItemId);
            Dictionary<string, object> item = catalog.Find("item", instance.ItemId);
            if (equip == null || item == null) return result;
            Dictionary<string, object> type = EquipType(item);
            if (type == null) return result;
            int level = EquipLevel(player, equip, instance);
            int upgradeLevel = UpgradeLevel(player, Integer(type, "EquipPos"));
            int rank = rankOfSubRank(player.SubRank);
            int equipRank = Integer(equip, "EquipRankIndex");
            MergeMain(result, equip, level, upgradeLevel, instance.BlessLevel, rank, true,
                Integer(equip, "MainPropType"));
            MergeMain(result, equip, level, upgradeLevel, instance.BlessLevel, rank, false,
                instance.SecondaryProperty);
            MergeOther(result, equip, instance, level, includeSpecialTerms ? rank : 0);
            return result;
        }

        public Dictionary<int, long> SuitProps(OfflinePlayerProgress player)
        {
            var counts = new Dictionary<int, int>();
            int rank = rankOfSubRank(player.SubRank);
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null || instance.SuitId == 0) continue;
                Dictionary<string, object> equip = catalog.Find("equip", instance.ItemId);
                if (equip == null || Integer(equip, "EquipRankIndex") != rank) continue;
                counts[instance.SuitId] = counts.TryGetValue(instance.SuitId, out int count) ? count + 1 : 1;
            }
            var result = new Dictionary<int, long>();
            foreach (KeyValuePair<int, int> suit in counts)
            {
                foreach (KeyValuePair<int, int> threshold in SuitAttributes(suit.Key)
                    .Where(value => value.Key <= suit.Value).OrderBy(value => value.Key))
                    CharacterPropFormula.Merge(result, SuitAttributeProps(threshold.Value, false));
            }
            return result;
        }

        private void MergeMain(Dictionary<int, long> result, Dictionary<string, object> equip, int level,
            int upgradeLevel, int blessLevel, int rank, bool main, int propType)
        {
            string prefix = main ? "Main" : "Sec";
            if (propType == 0) return;
            long baseValue = catalog.PropertyValue(Integer(equip, prefix + "PropId"), level, propType);
            double upgrade = catalog.PropertyValue(Integer(equip, prefix + "UpgradePropId"), upgradeLevel, propType)
                / 10000.0;
            double rankScale = catalog.PropertyValue(Integer(equip, prefix + "RankPropId"), level, propType)
                / 10000.0;
            long value = (long)(baseValue * (1.0 + upgrade) * (1.0 + rankScale));
            if (blessLevel > 0)
            {
                double bless = catalog.PropertyValue(Integer(equip, "Bless" + prefix + "PropId"), blessLevel,
                    propType) / 10000.0;
                long rawBless = (long)(value * bless);
                if (Integer(equip, "EquipRankIndex") == rank) value += rawBless;
            }
            CharacterPropFormula.Merge(result, new Dictionary<int, long> { { propType, value } });
        }

        private void MergeOther(Dictionary<int, long> result, Dictionary<string, object> equip,
            OfflineEquipmentInstance instance, int level, int playerRank)
        {
            int otherPropId = Integer(equip, "OtherPropId");
            foreach (OfflineEquipmentTerm term in instance.Terms ?? new List<OfflineEquipmentTerm>())
            {
                if (term.Type == 1)
                {
                    long value = (long)(catalog.PropertyValue(otherPropId, level, term.PropertyType) * term.Factor);
                    Add(result, term.PropertyType, value);
                }
                else if (term.Type == 2 || term.Type == 3)
                {
                    if (playerRank == 0) continue;
                    CharacterPropFormula.Merge(result, TermProperties(null, term, instance, playerRank));
                }
            }
        }

        private Dictionary<int, long> TermProperties(OfflinePlayerProgress player, OfflineEquipmentTerm term,
            OfflineEquipmentInstance instance)
        {
            return TermProperties(player, term, instance, player == null
                ? rankOfSubRank(1) : rankOfSubRank(player.SubRank));
        }

        private Dictionary<int, long> TermProperties(OfflinePlayerProgress player, OfflineEquipmentTerm term,
            OfflineEquipmentInstance instance, int playerRank)
        {
            var result = new Dictionary<int, long>();
            if (term == null || (term.Type != 2 && term.Type != 3)) return result;
            Dictionary<string, object> row = catalog.Find("equip_attributes_props", term.PropertyId, "Id");
            List<object> groups = ObjectList(row, "Props");
            if (groups.Count == 0) return result;
            int group = Math.Max(0, Math.Min(groups.Count - 1, term.Group));
            Dictionary<string, object> values = groups[group] as Dictionary<string, object>;
            if (values == null) return result;
            Dictionary<string, object> equip = instance == null ? null : catalog.Find("equip", instance.ItemId);
            int equipRank = Integer(equip, "EquipRankIndex");
            int blessLevel = instance == null ? 0 : instance.BlessLevel;
            double scale = equipRank == playerRank
                ? BlessScale(Integer(row, "PropBlessScaleFactorId"), equipRank, blessLevel) : 1.0;
            MergeAttribute(result, values, scale);
            return result;
        }

        private List<TermCandidate> EffectiveTerms(OfflinePlayerProgress player, CharacterPropScoreContext context)
        {
            var result = new List<TermCandidate>();
            var exclusive = new Dictionary<int, TermCandidate>();
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null) continue;
                foreach (OfflineEquipmentTerm term in instance.Terms ?? new List<OfflineEquipmentTerm>())
                    AddEffectiveTerm(player, context, result, exclusive,
                        new TermCandidate { Term = term, Instance = instance });
            }
            foreach (TermCandidate candidate in ActiveSuitTerms(player))
                AddEffectiveTerm(player, context, result, exclusive, candidate);
            return result;
        }

        private void AddEffectiveTerm(OfflinePlayerProgress player, CharacterPropScoreContext context,
            List<TermCandidate> effective, Dictionary<int, TermCandidate> exclusive, TermCandidate candidate)
        {
            if (candidate.Term == null || candidate.Term.PropertyId <= 0) return;
            Dictionary<string, object> attribute = catalog.Find("equip_attributes", candidate.Term.PropertyId, "Id");
            if (attribute == null) return;
            int group = Integer(attribute, "ExclusiveGroup");
            bool selfExclusive = Boolean(attribute, "SelfExclusive");
            if (group == 0 && !selfExclusive)
            {
                effective.Add(candidate);
                return;
            }
            int key = group != 0 ? group : candidate.Term.PropertyId;
            if (!exclusive.TryGetValue(key, out TermCandidate current))
            {
                exclusive.Add(key, candidate);
                effective.Add(candidate);
                return;
            }
            if (TermCombatRating(player, current, context) >= TermCombatRating(player, candidate, context)) return;
            effective.Remove(current);
            effective.Add(candidate);
            exclusive[key] = candidate;
        }

        private long TermCombatRating(OfflinePlayerProgress player, TermCandidate candidate,
            CharacterPropScoreContext context)
        {
            if (context == null || candidate == null || candidate.Term == null) return 0L;
            Dictionary<string, object> attribute = catalog.Find("equip_attributes", candidate.Term.PropertyId, "Id");
            if (attribute == null) return 0L;
            int type = Integer(attribute, "Type");
            int playerRank = rankOfSubRank(player.SubRank);
            Dictionary<string, object> equip = candidate.Instance == null ? null
                : catalog.Find("equip", candidate.Instance.ItemId);
            int equipRank = Integer(equip, "EquipRankIndex");
            int blessLevel = candidate.Instance == null ? 0 : candidate.Instance.BlessLevel;
            bool sameRank = equipRank == playerRank;
            Dictionary<int, long> equivalent = PropertyMap(attribute, "EquivalentProps");

            if (type == 2 || type == 3)
            {
                Dictionary<string, object> propRow = catalog.Find("equip_attributes_props",
                    candidate.Term.PropertyId, "Id");
                if (equivalent != null)
                {
                    long score = context.Common(equivalent);
                    double scale = sameRank
                        ? BlessScale(Integer(propRow, "PropBlessScaleFactorId"), equipRank, blessLevel) : 1.0;
                    return (long)(score * scale);
                }
                return context.Common(TermProperties(player, candidate.Term, candidate.Instance, playerRank));
            }
            if ((type != 4 && type != 5) || equivalent == null) return 0L;

            Dictionary<string, object> passive = catalog.Find("equip_attributes_passive_skill",
                candidate.Term.PropertyId, "Id");
            if (passive == null) return 0L;
            double propertyFactor = PassivePropertyFactor(passive, candidate.Term.Indices);
            int qualityIndex = IndexOf(Integers(passive, "Qualitys"), candidate.Term.SkillQuality);
            List<object> qualityFactors = ObjectList(passive, "QualityFactors");
            if (qualityIndex >= 0 && qualityIndex < qualityFactors.Count)
                propertyFactor *= Convert.ToInt64(qualityFactors[qualityIndex], CultureInfo.InvariantCulture) / 10000.0;
            double blessFactor = sameRank && blessLevel > 0
                ? PassiveBlessFactor(passive, equipRank, blessLevel) : 1.0;
            return (long)(context.Common(equivalent) * propertyFactor * blessFactor);
        }

        private double PassivePropertyFactor(Dictionary<string, object> passive, IReadOnlyList<int> indices)
        {
            List<object> factors = ObjectList(passive, "PropsFactors");
            double result = 1.0;
            for (int i = 0; i < (indices == null ? 0 : indices.Count); i++)
            {
                if (i >= factors.Count) return 0.0;
                Dictionary<string, object> factor = factors[i] as Dictionary<string, object>;
                List<object> values = ObjectList(factor, "Values");
                int index = indices[i];
                if (index < 0 || index >= values.Count) return 0.0;
                result *= Convert.ToInt64(values[index], CultureInfo.InvariantCulture) / 10000.0;
            }
            return result;
        }

        private double PassiveBlessFactor(Dictionary<string, object> passive, int equipRank, int blessLevel)
        {
            double result = 1.0;
            Dictionary<string, object> direct = ObjectMap(passive, "PropBlessScaleFactorIds")
                .ToDictionary(value => value.Key, value => value.Value);
            foreach (object value in direct.Values)
                result *= BlessScale(Convert.ToInt32(value, CultureInfo.InvariantCulture), equipRank, blessLevel);
            Dictionary<string, object> skillMaps = ObjectMap(passive, "SkillPropMapFactorIds");
            foreach (object value in skillMaps.Values)
                foreach (object factor in (value as Dictionary<string, object> ?? new Dictionary<string, object>()).Values)
                    result *= BlessScale(Convert.ToInt32(factor, CultureInfo.InvariantCulture), equipRank, blessLevel);
            return result;
        }

        private List<TermCandidate> ActiveSuitTerms(OfflinePlayerProgress player)
        {
            var result = new List<TermCandidate>();
            foreach (KeyValuePair<int, int> suit in SuitCounts(player))
            {
                int attributeId = ActiveSuitAttribute(suit.Key, suit.Value);
                Dictionary<string, object> attribute = catalog.Find("equip_attributes", attributeId, "Id");
                int type = Integer(attribute, "Type");
                if (type != 2 && type != 3 && type != 4 && type != 5) continue;
                var term = new OfflineEquipmentTerm { Type = type, PropertyId = attributeId };
                if (type == 4 || type == 5)
                {
                    Dictionary<string, object> passive = catalog.Find("equip_attributes_passive_skill", attributeId, "Id");
                    term.Indices = ObjectList(passive, "Props").Select(_ => 0).ToList();
                    List<object> qualities = ObjectList(passive, "Qualitys");
                    if (qualities.Count > 0)
                        term.SkillQuality = Convert.ToInt32(qualities[0], CultureInfo.InvariantCulture);
                }
                result.Add(new TermCandidate { Term = term });
            }
            return result;
        }

        private Dictionary<int, int> SuitCounts(OfflinePlayerProgress player)
        {
            var counts = new Dictionary<int, int>();
            int rank = rankOfSubRank(player.SubRank);
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null || instance.SuitId == 0) continue;
                Dictionary<string, object> equip = catalog.Find("equip", instance.ItemId);
                if (equip == null || Integer(equip, "EquipRankIndex") != rank) continue;
                counts[instance.SuitId] = counts.TryGetValue(instance.SuitId, out int count) ? count + 1 : 1;
            }
            return counts;
        }

        private IEnumerable<KeyValuePair<int, int>> SuitAttributes(int suitId)
        {
            Dictionary<string, object> row = catalog.Rows("equip_suit")
                .FirstOrDefault(value => Integer(value, "SuitId") == suitId);
            Dictionary<string, object> attributes = ObjectMap(row, "SuitAttributes");
            return attributes.Select(value => new KeyValuePair<int, int>(
                int.Parse(value.Key, CultureInfo.InvariantCulture),
                Convert.ToInt32(value.Value, CultureInfo.InvariantCulture)));
        }

        private int ActiveSuitAttribute(int suitId, int count)
        {
            int attributeId = 0;
            int threshold = 0;
            foreach (KeyValuePair<int, int> item in SuitAttributes(suitId))
                if (item.Key <= count && item.Key > threshold)
                {
                    threshold = item.Key;
                    attributeId = item.Value;
                }
            return attributeId;
        }

        private Dictionary<int, long> SuitAttributeProps(int attributeId, bool includeEquivalent)
        {
            var result = new Dictionary<int, long>();
            Dictionary<string, object> attribute = catalog.Find("equip_attributes", attributeId, "Id");
            int type = Integer(attribute, "Type");
            if (type == 2 || type == 3)
            {
                Dictionary<string, object> props = catalog.Find("equip_attributes_props", attributeId, "Id");
                List<object> groups = ObjectList(props, "Props");
                if (groups.Count > 0) MergeAttribute(result, groups[0] as Dictionary<string, object>);
            }
            else if (includeEquivalent)
            {
                Dictionary<int, long> equivalent = PropertyMap(attribute, "EquivalentProps");
                if (equivalent != null) CharacterPropFormula.Merge(result, equivalent);
            }
            return result;
        }

        private long SuitCombatRating(OfflinePlayerProgress player, OfflineEquipmentInstance instance,
            CharacterPropScoreContext context)
        {
            Dictionary<string, object> equip = catalog.Find("equip", instance.ItemId);
            int rank = rankOfSubRank(player.SubRank);
            if (instance.SuitId == 0 || equip == null || Integer(equip, "EquipRankIndex") != rank) return 0L;
            int count = CompatibleSuitPositionCount(instance.SuitId, player.Wardrobe.Profession);
            if (count == 0) return 0L;
            var props = new Dictionary<int, long>();
            foreach (KeyValuePair<int, int> threshold in SuitAttributes(instance.SuitId)
                .Where(value => value.Key <= count).OrderBy(value => value.Key))
                CharacterPropFormula.Merge(props, SuitAttributeProps(threshold.Value, true));
            return context.Common(props) / count;
        }

        private int CompatibleSuitPositionCount(int suitId, int profession)
        {
            var positions = new HashSet<int>();
            foreach (Dictionary<string, object> equip in catalog.Rows("equip"))
            {
                if (Integer(equip, "SuitId") != suitId) continue;
                int itemId = Integer(equip, "ClassId");
                Dictionary<string, object> item = catalog.Find("item", itemId);
                Dictionary<string, object> type = item == null ? null : EquipType(item);
                if (type != null && Integers(type, "Professions").Contains(profession))
                    positions.Add(Integer(type, "EquipPos"));
            }
            return positions.Count;
        }

        private double BlessScale(int factorId, int equipRank, int blessLevel)
        {
            if (factorId == 0) return 1.0;
            Dictionary<string, object> row = catalog.Rows("equip_attribute_bless_scale")
                .FirstOrDefault(value => Integer(value, "Id") == factorId
                    && Integer(value, "Rank") == equipRank && Integer(value, "Level") == blessLevel);
            return row == null ? 1.0 : Integer(row, "PropFactor") / 10000.0;
        }

        private static Dictionary<int, long> PropertyMap(Dictionary<string, object> row, string key)
        {
            if (row == null || !row.TryGetValue(key, out object raw)
                || !(raw is Dictionary<string, object> properties)) return null;
            var result = new Dictionary<int, long>();
            foreach (KeyValuePair<string, object> entry in properties)
                result[int.Parse(entry.Key, CultureInfo.InvariantCulture)]
                    = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
            return result;
        }

        private static Dictionary<string, object> ObjectMap(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? value as Dictionary<string, object> ?? new Dictionary<string, object>()
                : new Dictionary<string, object>();
        }

        private static List<object> ObjectList(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? value as List<object> ?? new List<object>() : new List<object>();
        }

        private static List<int> Integers(Dictionary<string, object> row, string key)
        {
            return ObjectList(row, key).Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToList();
        }

        private static int IndexOf(List<int> values, int value)
        {
            return values == null ? -1 : values.IndexOf(value);
        }

        private static bool Boolean(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static void MergeAttribute(Dictionary<int, long> result, Dictionary<string, object> props)
        {
            MergeAttribute(result, props, 1.0);
        }

        private static void MergeAttribute(Dictionary<int, long> result, Dictionary<string, object> props,
            double scale)
        {
            if (props == null) return;
            foreach (KeyValuePair<string, object> entry in props)
            {
                int propType = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                long value = (long)(Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture) * scale);
                Add(result, propType, value);
            }
        }

        private static void Add(Dictionary<int, long> result, int propType, long value)
        {
            if (propType == 0 || value == 0) return;
            result[propType] = result.TryGetValue(propType, out long current) ? current + value : value;
        }

        private int EquipLevel(OfflinePlayerProgress player, Dictionary<string, object> equip,
            OfflineEquipmentInstance instance)
        {
            int minimum = Math.Max(1, instance.ItemLevel);
            int maximum = minimum + Math.Max(0, Integer(equip, "LevelExtension"));
            return Math.Max(minimum, Math.Min(maximum, player.Level));
        }

        private int UpgradeLevel(OfflinePlayerProgress player, int position)
        {
            OfflineEquipmentSlotProgress slot = player.Equipment.SlotProgress
                .FirstOrDefault(value => value.Position == position);
            return slot == null ? 0 : slot.BaseLevel;
        }

        private Dictionary<string, object> EquipType(Dictionary<string, object> item)
        {
            int itemType = Integer(item, "ItemType");
            return catalog.Rows("equip_type").FirstOrDefault(value => Integer(value, "ItemType") == itemType);
        }

        private static OfflineEquipmentInstance Find(OfflinePlayerProgress player, long instanceId)
        {
            return player.Equipment.Instances.FirstOrDefault(value => value.InstanceId == instanceId);
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private sealed class TermCandidate
        {
            public OfflineEquipmentTerm Term;
            public OfflineEquipmentInstance Instance;
        }
    }

    public sealed class OfflineEquipmentResonancePropSource : IOfflineCharacterPropSource
    {
        private readonly OfflineEquipmentPropSource equipment;

        public OfflineEquipmentResonancePropSource(OfflineEquipmentPropSource equipmentSource)
        {
            equipment = equipmentSource ?? throw new ArgumentNullException(nameof(equipmentSource));
        }

        public int HandleType => CharacterPropHandleType.EquipResonance;

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player)
        {
            return equipment.ResonanceProps(player);
        }

        public long CalculateCombatRating(OfflinePlayerProgress player,
            IReadOnlyDictionary<int, long> props, CharacterPropScoreContext context)
        {
            return context?.Common(props) ?? 0L;
        }
    }
}
