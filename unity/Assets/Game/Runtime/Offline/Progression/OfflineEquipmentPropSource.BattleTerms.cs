using System;
using System.Collections.Generic;
using System.Globalization;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineEquipmentBattleTerm
    {
        public int AttributeId;
        public int SkillItemId;
        public int SkillQuality;
        public int SkillRank;
        public int SkillLevel;
        public long EquipmentInstanceId;
        public int EquipmentItemId;
        public int EquipmentRank;
        public int BlessLevel;
        public int SuitId;
        public List<OfflineEquipmentBattleParameter> Parameters = new List<OfflineEquipmentBattleParameter>();
        public List<OfflineEquipmentBattlePropertyScale> PropertyScales = new List<OfflineEquipmentBattlePropertyScale>();
    }

    public sealed class OfflineEquipmentBattleParameter
    {
        public int PropertyId;
        public long RawValue;
        public bool IsFloat;
    }

    public sealed class OfflineEquipmentBattlePropertyScale
    {
        public int EntityId;
        public int PropertyId;
        public double Factor;
    }

    public sealed partial class OfflineEquipmentPropSource
    {
        // Official CheckEquipsAllEffectTerm filters before exclusivity. Keep all
        // implementation types in the comparison, then project the passive terms.
        public List<OfflineEquipmentBattleTerm> CollectBattlePassiveTerms(OfflinePlayerProgress player,
            CharacterPropScoreContext context)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (context == null) throw new ArgumentNullException(nameof(context));
            int playerRank = rankOfSubRank(player.SubRank);
            var result = new List<OfflineEquipmentBattleTerm>();
            foreach (TermCandidate candidate in EffectiveTerms(player, context, true))
            {
                Dictionary<string, object> attribute = catalog.Find("equip_attributes", candidate.Term.PropertyId, "Id");
                int type = Integer(attribute, "Type");
                if (type != 4 && type != 5) continue;
                Dictionary<string, object> passive = catalog.Find("equip_attributes_passive_skill", candidate.Term.PropertyId, "Id");
                if (passive == null) continue;
                OfflineEquipmentInstance instance = candidate.Instance;
                int equipRank = ItemLimitRank(instance);
                int blessLevel = instance?.BlessLevel ?? 0;
                var term = new OfflineEquipmentBattleTerm
                {
                    AttributeId = candidate.Term.PropertyId,
                    SkillItemId = Integer(passive, "SkillId"),
                    SkillQuality = candidate.Term.SkillQuality,
                    SkillLevel = instance == null ? SuitSkillLevel(player, candidate.SuitId, playerRank)
                        : EquipLevel(player, catalog.Find("equip", instance.ItemId), instance),
                    EquipmentInstanceId = instance?.InstanceId ?? 0,
                    EquipmentItemId = instance?.ItemId ?? 0,
                    EquipmentRank = equipRank,
                    BlessLevel = blessLevel,
                    SuitId = candidate.SuitId
                };
                foreach (KeyValuePair<int, KeyValuePair<long, OfficialEquipmentPropertyValueType>> value in
                    catalog.CalculateTermReplacementProperties(candidate.Term.PropertyId, playerRank,
                        equipRank, blessLevel, candidate.Term.Indices))
                    term.Parameters.Add(new OfflineEquipmentBattleParameter
                    {
                        PropertyId = value.Key,
                        RawValue = value.Value.Key,
                        IsFloat = value.Value.Value == OfficialEquipmentPropertyValueType.Float
                    });
                if (equipRank == playerRank && blessLevel > 0)
                    foreach (KeyValuePair<string, object> entity in ObjectMap(passive, "SkillPropMapFactorIds"))
                        foreach (KeyValuePair<string, object> property in (Dictionary<string, object>)entity.Value)
                            term.PropertyScales.Add(new OfflineEquipmentBattlePropertyScale
                            {
                                EntityId = int.Parse(entity.Key, CultureInfo.InvariantCulture),
                                PropertyId = int.Parse(property.Key, CultureInfo.InvariantCulture),
                                Factor = BlessScale(Convert.ToInt32(property.Value, CultureInfo.InvariantCulture),
                                    equipRank, blessLevel)
                            });
                result.Add(term);
            }
            return result;
        }

        private int ItemLimitRank(OfflineEquipmentInstance instance)
        {
            return instance == null ? 0 : Integer(catalog.Find("item", instance.ItemId), "LimitRank");
        }

        // Confirmed offline rule: growing suit effects use the mean effective
        // equipment level. The existing integer skill-table index truncates it.
        // Raw, fixed term parameters are not multiplied by this level.
        private int SuitSkillLevel(OfflinePlayerProgress player, int suitId, int playerRank)
        {
            long total = 0;
            int count = 0;
            foreach (OfflineEquippedSlot slot in player.Equipment.Equipped)
            {
                OfflineEquipmentInstance instance = Find(player, slot.InstanceId);
                if (instance == null || instance.SuitId != suitId || ItemLimitRank(instance) != playerRank) continue;
                total += EquipLevel(player, catalog.Find("equip", instance.ItemId), instance);
                count++;
            }
            if (count == 0) throw new InvalidOperationException("Active suit has no matching equipped items.");
            return checked((int)(total / count));
        }
    }
}
