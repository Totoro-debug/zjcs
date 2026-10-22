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
    public sealed class OfflineEquipmentPropSource : IOfflineCharacterPropSource
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
            var result = new Dictionary<int, long>();
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null) continue;
                CharacterPropFormula.Merge(result, ItemProps(player, instance));
            }
            CharacterPropFormula.Merge(result, SuitProps(player));
            CharacterPropFormula.Merge(result, ResonanceProps(player));
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
                Dictionary<int, long> item = ItemProps(player, instance);
                rating += context.Common(item);
            }
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
            MergeOther(result, equip, instance, level);
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
                Dictionary<string, object> row = catalog.Rows("equip_suit")
                    .FirstOrDefault(value => Integer(value, "SuitId") == suit.Key);
                if (row == null || !(row.TryGetValue("SuitAttributes", out object raw)
                    && raw is Dictionary<string, object> attributes)) continue;
                int threshold = 0;
                int attributeId = 0;
                foreach (KeyValuePair<string, object> entry in attributes)
                {
                    int pieces = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                    if (suit.Value >= pieces && pieces > threshold)
                    {
                        threshold = pieces;
                        attributeId = Convert.ToInt32(entry.Value, CultureInfo.InvariantCulture);
                    }
                }
                if (attributeId == 0) continue;
                Dictionary<string, object> props = catalog.Find("equip_attributes_props", attributeId, "Id");
                if (props == null) continue;
                object groups = props.TryGetValue("Props", out object rawProps) ? rawProps : null;
                if (!(groups is List<object> list) || list.Count == 0) continue;
                MergeAttribute(result, list[0] as Dictionary<string, object>);
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
            OfflineEquipmentInstance instance, int level)
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
                    Dictionary<string, object> row = catalog.Find("equip_attributes_props", term.PropertyId, "Id");
                    if (row == null || !(row.TryGetValue("Props", out object raw) && raw is List<object> groups)
                        || groups.Count == 0) continue;
                    int group = Math.Max(0, Math.Min(groups.Count - 1, term.Group));
                    MergeAttribute(result, groups[group] as Dictionary<string, object>);
                }
            }
        }

        private static void MergeAttribute(Dictionary<int, long> result, Dictionary<string, object> props)
        {
            if (props == null) return;
            foreach (KeyValuePair<string, object> entry in props)
            {
                int propType = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                long value = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
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
            return row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }
    }
}
