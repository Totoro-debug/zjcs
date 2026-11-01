using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightStatusPropertyScale
    {
        public int EntityId;
        public int PropertyId;
        public double Factor = 1d;
    }

    public static class FightStatusPropertyModifiers
    {
        public static List<FightStatusReplacementValue> Copy(
            IEnumerable<FightStatusReplacementValue> values)
        {
            if (values == null) return null;
            var result = new List<FightStatusReplacementValue>();
            foreach (var value in values)
                if (value != null) result.Add(new FightStatusReplacementValue {
                    PropertyId = value.PropertyId, RawValue = value.RawValue, IsFloat = value.IsFloat });
            return result;
        }

        public static List<FightStatusPropertyScale> Copy(IEnumerable<FightStatusPropertyScale> values)
        {
            if (values == null) return null;
            var result = new List<FightStatusPropertyScale>();
            foreach (var value in values)
                if (value != null) result.Add(new FightStatusPropertyScale {
                    EntityId = value.EntityId, PropertyId = value.PropertyId, Factor = value.Factor });
            return result;
        }
    }
}
