namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // Official FightRoundComponent.CalcRoundInterval reads PropComponent.FinalProps
        // whenever the existing queue inserts a new interval. It does not read a
        // second, stale speed snapshot from the unit's initial turn description.
        private long FinalTurnSpeed(FightTurnUnit turn)
        {
            if (turn != null && unitById.TryGetValue(turn.Id, out var unit) && unit.Properties != null)
            {
                int speed = formula.PropertyId("Speed");
                foreach (var property in unit.Properties)
                    if (property.Id == speed) return property.Value;
            }
            return turn?.Speed ?? 1;
        }
    }
}
