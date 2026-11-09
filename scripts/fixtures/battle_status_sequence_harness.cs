using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleStatusLogicRegression
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static FightBattleStatusState Status(string graph)
        => new FightBattleStatusState { ClassId = 24679, OwnerId = 1, CreatorId = 1,
            HasLogicComponent = true, LogicDefinitionResolved = true,
            LogicRulesSupported = true, LogicDefinitionJson = graph };

    private static List<FightBattleStatusSkillInvocation> Round(FightBattleUnitState owner,
        FightBattleStatusState status)
        => FightStatusLogicRuntime.Apply(owner, status, FightStatusLogicEvent.RoundStart,
            owner, owner, null, null, new[] { owner }, null, null);

    public static void CheckOfficial(string graph, int expectedSkill)
    {
        var owner = new FightBattleUnitState { Id = 1 };
        var status = Status(graph);
        status.ClassId = expectedSkill == 13625 ? 24679 : 24680;
        status.LogicBlackboard = new List<FightBattleLogicBlackboardValue> {
            new FightBattleLogicBlackboardValue { KeyId = 348958083, DoubleValue = 99d },
            new FightBattleLogicBlackboardValue { KeyId = 7777, LongValue = 99 } };
        owner.Statuses.Add(status);
        FightBattleStatusSkillInvocation first = null;
        for (int round = 0; round < 8; round++)
        {
            var casts = Round(owner, status);
            Require(casts.Count == 1 && casts[0].Trigger.EntityId == expectedSkill,
                "Official sequence must trigger exactly its authored skill each round.");
            Require(casts[0].DamageFactor == (round < 4 ? 1d : 2d),
                "Official sequence multiplier must change on the fifth trigger.");
            if (round == 0) first = casts[0];
            Require(first.DamageFactor == 1d, "Collected invocation changed after a later blackboard write.");
            Require(status.LogicRulesSupported, "Official sequence used an unsupported handle.");
            Require(!status.LogicBlackboard.Exists(value => value.KeyId == 348958083),
                "Legacy Runtime output must be removed from persistent state.");
            Require(status.LogicBlackboard.Find(value => value.KeyId == 7777).LongValue == 99,
                "Migration must preserve keys whose lifecycle is not declared by this graph.");
            if (round == 2)
            {
                // Use the same status copy operation used by lifecycle application.
                var restoredOwner = new FightBattleUnitState { Id = 1 };
                status = FightStatusLifecycle.Apply(restoredOwner, status, -1, 1);
                Require(!ReferenceEquals(owner.Statuses[0].LogicBlackboard, status.LogicBlackboard),
                    "Lifecycle restoration must copy the blackboard.");
                owner = restoredOwner;
            }
            if (round == 4)
            {
                owner.Turn = new FightTurnUnit { Id = 1 };
                var checkpoint = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 31,
                    Grid = new FightBattleGrid { Width = 20, Height = 20 },
                    ResultRules = new FightBattleResultRules() };
                checkpoint.Units.Add(owner);
                owner = FightBattleCheckpointSerializer.Load(
                    FightBattleCheckpointSerializer.Save(checkpoint)).Units[0];
                status = owner.Statuses[0];
                Require(status.LogicBlackboard.Find(value => value.KeyId == 348958083) == null
                    && status.LogicBlackboard.Find(value => value.KeyId == 2081371075).LongValue == 5,
                    "Checkpoint must preserve the counter and exclude the Runtime multiplier.");
            }
        }
        Require(!owner.Statuses.Contains(status), "Official counter-end flow must remove the status at eight.");
    }

    public static void CheckEdges(string graph)
    {
        foreach (long index in new[] { -1L, 0L, 7L, 8L, long.MaxValue })
        {
            // Isolate one round so the official counter filter/end flow cannot mask indexing.
            string oneRound = graph.Replace("\"Value\":8", "\"Value\":9223372036854775807")
                .Replace("\"_type\":\"CounterLessFilter\"", "\"_type\":\"CounterLessEqualFilter\"");
            var owner = new FightBattleUnitState { Id = 1 };
            var status = Status(oneRound);
            status.LogicBlackboard = new List<FightBattleLogicBlackboardValue> {
                new FightBattleLogicBlackboardValue { KeyId = 2081371075, LongValue = index } };
            var casts = Round(owner, status);
            Require(casts.Count == 1 && casts[0].DamageFactor == (index == 0 ? 1d : 2d),
                "Out-of-range sequence index must select the last entry.");
        }
        foreach (string values in new[] { "[]", "[0]", "[0.5]" })
        {
            var owner = new FightBattleUnitState { Id = 1 };
            var status = Status(graph.Replace("[1,1,1,1,2,2,2,2]", values));
            var casts = Round(owner, status);
            double expected = values == "[]" ? 1d : values == "[0]" ? 0d : 0.5d;
            Require(casts.Count == 1 && casts[0].DamageFactor == expected,
                "Empty, zero and fractional multipliers must retain their distinct values.");
        }
        // Remove only the producer; an unwritten input port must default to 1.
        string producer = graph.Substring(graph.IndexOf("{\"IndexKey\"", StringComparison.Ordinal));
        producer = producer.Substring(0, producer.IndexOf("},{\"TriggerSkillCfgs\"", StringComparison.Ordinal) + 2);
        var fallback = Round(new FightBattleUnitState { Id = 1 }, Status(graph.Replace(producer, "")));
        Require(fallback.Count == 1 && fallback[0].DamageFactor == 1d, "Unwritten DamageFactorKey must default to 1.");
    }

    public static void CheckEventScope()
    {
        // The same key ID deliberately exists in the persistent and Runtime boards.
        string graph = "{\"Triggers\":[{\"_type\":\"FightOnRoundStartTriggerCfg\",\"Flow\":{"
            + "\"FilterCondition\":{\"_type\":\"CounterEqualFilter\",\"CounterBBKey\":{\"Id\":42,\"IsPersistentBlackboard\":true},\"Value\":0},"
            + "\"HandleCfgs\":[{\"_type\":\"FightHandleCfgBBPickFloatByIndex\",\"IndexKey\":{\"Id\":42,\"IsPersistentBlackboard\":true},"
            + "\"StorageKey\":{\"Id\":42},\"Values\":[0.5]},{\"_type\":\"FightHandleCfgCounterAdd\","
            + "\"CounterBBKey\":{\"Id\":42,\"IsPersistentBlackboard\":true},\"DeltaValue\":1}]}},"
            + "{\"_type\":\"FightOnRoundStartTriggerCfg\",\"Flow\":{\"HandleCfgs\":[{\"_type\":\"FightHandleCfgTriggerSkills\","
            + "\"DamageFactorKey\":{\"Id\":42},\"TriggerSkillCfgs\":[{\"SkillId\":13625}]}]}}]}";
        var owner = new FightBattleUnitState { Id = 1 };
        var status = Status(graph);
        var first = Round(owner, status);
        Require(first.Count == 1 && first[0].DamageFactor == 0.5d,
            "Later flows in one event must share Runtime outputs.");
        Require(status.LogicBlackboard.Count == 1 && status.LogicBlackboard[0].LongValue == 1
            && status.LogicBlackboard[0].DoubleValue == 0d,
            "Runtime and persistent ports with the same ID must remain separate.");
        var next = Round(owner, status);
        Require(next.Count == 1 && next[0].DamageFactor == 1d && first[0].DamageFactor == 0.5d,
            "The next event must start with an empty Runtime board and retain earlier invocation values.");
    }
}
