using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStatusLogicHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static FightBattleUnitState Unit(int id, int side)
    {
        return new FightBattleUnitState
        {
            Id = id, Side = side, CharacterType = side == 1 ? 1 : 2,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000,
            X = id == 1 ? 4 : 8, Y = 4,
            Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 }
        };
    }

    public static void Run(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        // 24669 contains the original FightOnStatusSelfInitTriggerCfg and initializes its
        // persistent AbsorbedCount blackboard value.  No client callback exists for this
        // component; the normal executor startup path must still run it once.
        var player = Unit(1, 1);
        var enemy = Unit(2, 2);
        var logic = catalog.CreateStatus(24669, 1, 1, 24669001, 0, 0, 5, 20, 1);
        player.Statuses.Add(logic);
        var checkpoint = new FightBattleCheckpoint
        {
            StageId = 1001, RandomSeed = 31,
            Grid = new FightBattleGrid { Width = 20, Height = 20 },
            ResultRules = new FightBattleResultRules()
        };
        checkpoint.Units.Add(player); checkpoint.Units.Add(enemy);
        var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(),
            catalog, items);
        runner.Step(0, 1);

        Check(logic.HasLogicComponent && logic.LogicDefinitionResolved
            && !string.IsNullOrEmpty(logic.LogicDefinitionJson),
            "FightStatusLogicComponent was not retained from the official definition.");
        Check(logic.LogicSelfInitialized && logic.LogicStartInitialized,
            "Status logic startup was not dispatched exactly through the executor lifecycle.");
        var blackboard = logic.LogicBlackboard.SingleOrDefault(value => value.KeyId == 619624960);
        Check(blackboard != null && blackboard.LongValue == -1,
            "Official FightOnStatusSelfInitTriggerCfg did not initialize AbsorbedCount.");

        var restored = FightBattleCheckpointSerializer.Load(
            FightBattleCheckpointSerializer.Save(checkpoint));
        var restoredLogic = restored.Units.Single(value => value.Id == 1).Statuses
            .Single(value => value.ClassId == 24669);
        Check(restoredLogic.LogicDefinitionResolved && restoredLogic.LogicSelfInitialized
            && restoredLogic.LogicStartInitialized && restoredLogic.LogicBlackboard.Count == 1
            && restoredLogic.LogicBlackboard[0].LongValue == -1,
            "Status logic definition or persistent blackboard was lost in the checkpoint.");
        var restoredRunner = FightBattleExecutor.Restore(restored, formula,
            new FightDamageSettlement(), catalog, items);
        Check(restoredRunner != null && restoredLogic.LogicTriggeredCount >= 0,
            "Status logic checkpoint could not be restored by the official executor.");
    }
}
