using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Equipment
{
    public enum OfficialEquipmentPropertyValueType { Long, Float }

    public sealed partial class OfficialEquipmentCatalog
    {
        // BattleFormulaHandler.CalcEquipTermsReplacePropDictByPropGroupIndices and
        // EquipAttributesPassiveSkillInfo.GetEquipAttributePassiveSkillProp.
        // These values retain their original fixed-point type for Status.ReplaceProps.
        public Dictionary<int, KeyValuePair<long, OfficialEquipmentPropertyValueType>>
            CalculateTermReplacementProperties(int id, int planesRank, int equipRank,
                int blessLevel, IList<int> propGroupIndices)
        {
            var result = new Dictionary<int, KeyValuePair<long, OfficialEquipmentPropertyValueType>>();
            var passive = Find("equip_attributes_passive_skill", id, "Id");
            if (passive == null || !passive.TryGetValue("Props", out object rawProps) || rawProps == null)
                return result;
            if (propGroupIndices == null) throw new ArgumentNullException(nameof(propGroupIndices));
            var props = Array(rawProps, "equipment passive Props");
            for (int i = 0; i < propGroupIndices.Count; i++)
            {
                int index = propGroupIndices[i];
                if (i >= props.Count) continue;
                var config = Object(props[i], "equipment passive property");
                if (!config.TryGetValue("Values", out object rawValues) || rawValues == null) continue;
                var values = Array(rawValues, "equipment passive property values");
                if (index >= values.Count) continue;
                long purePropVal = Convert.ToInt64(values[index], CultureInfo.InvariantCulture);
                long value = purePropVal;
                int key = Integer(config, "Key");
                if (planesRank == equipRank && blessLevel != 0
                    && passive.TryGetValue("PropBlessScaleFactorIds", out object rawFactors) && rawFactors != null
                    && Object(rawFactors, "equipment passive blessing factors").TryGetValue(
                        key.ToString(CultureInfo.InvariantCulture), out object rawFactor))
                {
                    int factorId = Convert.ToInt32(rawFactor, CultureInfo.InvariantCulture);
                    var scale = factorId == 0 ? null : Rows("equip_attribute_bless_scale").FirstOrDefault(row =>
                        Integer(row, "Id") == factorId && Integer(row, "Rank") == equipRank
                        && Integer(row, "Level") == blessLevel);
                    double scaleFactor = scale == null ? 1.0
                        : Convert.ToInt64(scale["PropFactor"], CultureInfo.InvariantCulture) / 10000.0;
                    value = (long)(scaleFactor * (double)purePropVal);
                }
                result.Add(key, new KeyValuePair<long, OfficialEquipmentPropertyValueType>(value,
                    (OfficialEquipmentPropertyValueType)Integer(config, "Type")));
            }
            return result;
        }
    }
}
