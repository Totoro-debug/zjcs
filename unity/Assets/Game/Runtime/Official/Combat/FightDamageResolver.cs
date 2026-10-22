using System;

namespace Zjcs.Combat.Official
{
    public sealed class FightResolvedHit
    {
        public FightDamageTypeData Type { get; }
        public FightDamageFormulaResult Formula { get; }
        public FightDamageEvent Settlement { get; }

        internal FightResolvedHit(FightDamageTypeData type, FightDamageFormulaResult formula,
            FightDamageEvent settlement)
        {
            Type = type;
            Formula = formula;
            Settlement = settlement;
        }
    }

    public sealed class FightDamageResolver
    {
        private readonly FightDamageFormula formula;
        private readonly FightDamageSettlement settlement;

        public FightDamageResolver(FightDamageFormula formula, FightDamageSettlement settlement)
        {
            this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
        }

        public FightResolvedHit Resolve(FightRandomStream random, FightDamageFormulaRequest request,
            int damageId, FightEntityState source, FightEntityState target, int hitType,
            long? customDamage = null, double elementConfrontation = 0)
        {
            FightDamageTypeData type = formula.CalculateDamageType(random,
                request.SourceProps, request.TargetProps, hitType);
            FightDamageFormulaResult damage = formula.Calculate(request, type, customDamage, elementConfrontation);
            FightDamageEvent applied = settlement.Apply(damageId, source, target, damage.Damage);
            return new FightResolvedHit(type, damage, applied);
        }
    }
}
