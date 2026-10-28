using System;

namespace Zjcs.Combat.Official
{
    // Confirmed single-player replacement for the unavailable damage-test server.
    // Ordinary stages never consume these settings.
    [Serializable]
    public sealed class FightDamageTestRules
    {
        public int Difficulty;
        public bool EnableMonsterOneDamage;
        public bool EnableMonsterUnlimitHp;

        internal bool UnlimitedHp(int stageType, FightBattleUnitState target) =>
            stageType == 43 && EnableMonsterUnlimitHp && target.Side == 2;

        internal long Damage(int stageType, FightBattleUnitState source, FightBattleUnitState target, long damage) =>
            stageType == 43 && EnableMonsterOneDamage && source.Side == 2 && target.Side == 1 && damage < 0 ? -1 : damage;

        internal FightDamageEvent Settle(FightDamageSettlement settlement, int stageType, int damageId,
            FightEntityState source, FightEntityState target, FightBattleUnitState targetUnit, long damage) =>
            UnlimitedHp(stageType, targetUnit) && damage < 0
                ? settlement.ApplyPreservingHp(damageId, source, target, damage)
                : settlement.Apply(damageId, source, target, damage);
    }
}
