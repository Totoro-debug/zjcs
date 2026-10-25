using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    /// <summary>Original GemPropChunck: properties from gems in currently equipped items.</summary>
    public sealed class OfflineGemPropSource : IOfflineCharacterPropSource
    {
        private readonly OfficialEquipmentCatalog catalog;

        public OfflineGemPropSource(OfficialEquipmentCatalog equipmentCatalog)
        {
            catalog = equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        }

        public int HandleType => CharacterPropHandleType.Gem;

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player)
        {
            var result = new Dictionary<int, long>();
            if (player?.Equipment?.Equipped == null || player.Equipment.Instances == null) return result;
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = player.Equipment.Instances.FirstOrDefault(value =>
                    value != null && value.InstanceId == slot.InstanceId);
                if (instance?.Gems == null) continue;
                foreach (OfflineEquipmentGem equippedGem in instance.Gems)
                {
                    if (equippedGem == null || equippedGem.ItemId <= 0) continue;
                    Dictionary<string, object> gem = catalog.Find("gem", equippedGem.ItemId);
                    if (gem == null) continue;
                    int specialPropId = Integer(gem, "SpecialPropId");
                    if (specialPropId > 0)
                    {
                        Dictionary<string, object> special = catalog.Find("equip_attributes_props", specialPropId, "Id");
                        if (special == null || !(special.TryGetValue("Props", out object groups)
                            && groups is List<object> rows) || rows.Count == 0) continue;
                        Merge(result, rows[0] as Dictionary<string, object>);
                        continue;
                    }

                    Dictionary<string, object> item = catalog.Find("item", equippedGem.ItemId);
                    int propType = Integer(gem, "PropType");
                    int propId = Integer(gem, "PropId");
                    if (item == null || propType <= 0 || propId <= 0) continue;
                    Add(result, propType, catalog.PropertyValue(propId, Integer(item, "Rank"), propType));
                }
            }
            return result;
        }

        public long CalculateCombatRating(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> props,
            CharacterPropScoreContext context)
        {
            return context?.Common(props) ?? 0L;
        }

        private static void Merge(Dictionary<int, long> target, Dictionary<string, object> values)
        {
            if (values == null) return;
            foreach (KeyValuePair<string, object> entry in values)
                Add(target, int.Parse(entry.Key, CultureInfo.InvariantCulture),
                    Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture));
        }

        private static void Add(Dictionary<int, long> target, int property, long value)
        {
            if (property <= 0 || value == 0) return;
            target[property] = target.TryGetValue(property, out long current) ? current + value : value;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }
    }
}
