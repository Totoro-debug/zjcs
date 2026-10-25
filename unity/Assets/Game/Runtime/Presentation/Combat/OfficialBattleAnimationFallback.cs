using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleAnimationFallback
    {
        public static string Resolve(FightBattleUnitState unit, FightBattleStepEvent stepEvent)
        {
            if (unit == null || stepEvent == null) return null;
            if (stepEvent.DamageEvents != null)
                foreach (FightBattleDamageEvent damage in stepEvent.DamageEvents)
                    if (damage != null && !damage.IsSkillCost && damage.TargetId == unit.Id)
                        return damage.Block ? "block_01" : "hit_01";
            if (stepEvent.Movements != null)
                foreach (FightBattleMovementEvent movement in stepEvent.Movements)
                    if (movement != null && movement.UnitId == unit.Id
                        && movement.Path != null && movement.Path.Count > 1)
                        return "run_01";
            return null;
        }
    }
}
