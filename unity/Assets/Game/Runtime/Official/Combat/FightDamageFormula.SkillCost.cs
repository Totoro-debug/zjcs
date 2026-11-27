using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightDamageFormula
    {
        // Original BattleFormulaHandler.CalcSkillCostHp: positive configured
        // current/max HP terms, then a one-HP floor and integer truncation.
        public long CalculateSkillCostHp(IList<FightPropertyValue> skillProperties,
            long currentHp, long maxHp)
        {
            if (skillProperties == null || skillProperties.Count == 0 || currentHp <= 0 || maxHp <= 0)
                return 0L;
            double cost = 0.0;
            double current = Prop(skillProperties, "SkillCostByCurHp");
            if (current > 0.0) cost += currentHp * current;
            double maximum = Prop(skillProperties, "SkillCostByMaxHp");
            if (maximum > 0.0) cost += maxHp * maximum;
            return (long)Math.Min(Math.Abs(cost), currentHp - 1);
        }
    }
}
