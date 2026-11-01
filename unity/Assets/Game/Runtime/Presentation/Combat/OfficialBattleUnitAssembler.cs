using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Initializes the shared HP, movement and turn state for a battle unit.</summary>
    public static class OfficialBattleUnitAssembler
    {
        public static void Initialize(FightDamageFormula formula, FightBattleUnitState unit,
            IReadOnlyDictionary<int, long> properties, int maxHpPropertyId, int speedPropertyId,
            bool preserveZeroProperties = false)
        {
            if (formula == null) throw new ArgumentNullException(nameof(formula));
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            if (maxHpPropertyId <= 0 || speedPropertyId <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHpPropertyId));

            long maxHp = OfficialBattlePropertyAssembler.Value(properties, maxHpPropertyId);
            if (maxHp <= 0) throw new InvalidOperationException("Official battle unit MaxHp is not positive.");
            unit.Hp = maxHp;
            unit.MaxHp = maxHp;
            unit.LastMaxHp = maxHp;
            unit.Properties = preserveZeroProperties
                ? OfficialBattlePropertyAssembler.ToProperties(properties)
                : OfficialBattlePropertyAssembler.ToNonZeroProperties(properties);
            IDictionary<int, long> movementProperties = properties as IDictionary<int, long>;
            if (movementProperties == null)
            {
                movementProperties = new Dictionary<int, long>();
                foreach (KeyValuePair<int, long> value in properties)
                    movementProperties[value.Key] = value.Value;
            }
            unit.MoveDist = formula.CalculateMoveDist(movementProperties);
            unit.Turn.Id = unit.Id;
            unit.Turn.Hp = unit.Hp;
            unit.Turn.Speed = OfficialBattlePropertyAssembler.CheckedValue(properties, speedPropertyId);
        }
    }
}
