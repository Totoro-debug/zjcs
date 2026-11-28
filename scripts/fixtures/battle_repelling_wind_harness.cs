using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleRepellingWindHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Repelling wind: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, procs = 0, misses = 0, repeats = 0;
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (bool blocked in new[] { false, true })
        foreach (int seed in new[] { 13, 91, 104729, 209458, 58319, 37199, 91271, 170003 })
        {
            var owner = Unit(1, 1, 5, 5, formula);
            var enemy = Unit(2, 2, 8, 5, formula);
            var listener = catalog.CreateStatus(25032, 1, 1, 1, 25032, 10566, rank, level, 8, owner.Properties);
            owner.Statuses.Add(listener);
            Check(listener.HasMoveNear && listener.MoveNearRulesSupported && listener.DurationRound == -1
                && listener.MoveNearEnterRangeCauseFilter == 1 && listener.MoveNearRate == .75f
                && listener.MoveNearTryAtEnterRange && !listener.MoveNearTryAtStandRound
                && !listener.MoveNearTryAtInRangeMove && !listener.MoveNearTryAtStart
                && listener.MoveNearRange.Count == 12 && listener.MoveNearMaxCount == -1,
                "the original permanent active-entry listener changed");
            var movement = new FightBattleMovementEvent { UnitId = 2, MoveCause = EEntityMoveCause.SelfMove };
            movement.Path.Add(new FightGridPoint(8, 5)); movement.Path.Add(new FightGridPoint(7, 5));
            enemy.X = 7;
            var units = new[] { owner, enemy };
            var stream = new FightRandomStream(seed);
            var expectedStream = new FightRandomStream(seed);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                bool expected = expectedStream.Next() < .75;
                var invocations = FightStatusTriggerRuntime.CollectMoveNearSkills(enemy, units,
                    FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, stream);
                Check(invocations.Count == (expected ? 1 : 0), "repeated entry did not make an independent 75-percent roll");
                if (expected)
                    Check(invocations[0].SourceUnitId == 1 && invocations[0].TargetUnitId == 2
                        && invocations[0].Trigger.EntityId == 11370, "entry lost its configured source, target or child");
            }
            movement.MoveCause = EEntityMoveCause.Displaced;
            Check(FightStatusTriggerRuntime.CollectMoveNearSkills(enemy, units,
                FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, new FightRandomStream(seed)).Count == 0,
                "forced displacement triggered an active-entry listener");
            movement.MoveCause = EEntityMoveCause.SelfMove;
            movement.Path[0] = new FightGridPoint(6, 5);
            Check(FightStatusTriggerRuntime.CollectMoveNearSkills(enemy, units,
                FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, new FightRandomStream(seed)).Count == 0,
                "movement wholly inside the range triggered another entry");
            movement.Path[0] = new FightGridPoint(8, 5);
            enemy.Side = 1;
            Check(FightStatusTriggerRuntime.CollectMoveNearSkills(enemy, units,
                FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, new FightRandomStream(seed)).Count == 0,
                "friendly entry triggered wind damage");
            enemy.Side = 2;
            listener.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
            Check(FightStatusTriggerRuntime.CollectMoveNearSkills(enemy, units,
                FightStatusTriggerRuntime.MoveNearEventEnterRange, movement, new FightRandomStream(seed)).Count == 0,
                "a forbidden listener still triggered");
            listener.ForbiddenType = 0;
            listener.MoveNearTriggeredCount = listener.MoveNearRoundTriggeredCount = 0;
            enemy.X = 8;
            owner.MoveDist = 0;
            enemy.MoveDist = 1;
            enemy.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MoveDist"), Value = 1 });
            enemy.BaseProperties = enemy.Properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList();
            AddSkill(owner, catalog, 10800, 10028, 1, 1);
            AddSkill(enemy, catalog, 10610, 10009, 1, 1);
            var checkpoint = new FightBattleCheckpoint { StageId = 1, MaxRound = 20, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            if (blocked) checkpoint.Grid.BlockedPositions.Add(new FightGridPoint(9, 5));
            checkpoint.Units.AddRange(units);
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            int casts = 0;
            var props = catalog.CalculateSkillProps(11370, rank, level, 8);
            long expectedDamage = -(long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m);
            for (int turn = 0; turn < 24 && !checkpoint.Finished; turn++)
            {
                var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                    "save restoration changed entry rolls, damage, knockback or the listener counter");
                var hits = step.DamageEvents.Where(hit => hit.SkillItemId == 10566).ToArray();
                foreach (var hit in hits)
                {
                    Check(hit.SourceId == 1 && hit.TargetId == 2 && hit.SkillEntityId == 11370
                        && hit.HitEntityId == 11371 && hit.DamageEntityId == 11372 && hit.ElementType == 100
                        && hit.DamageType == 1 && hit.Damage == expectedDamage && !hit.IsCure && !hit.TrueDamage,
                        "cultivated wind damage or inherited passive identity changed: " + hit.Damage + "/" + expectedDamage);
                    var push = step.Movements.Single(value => value.UnitId == 2 && value.MoveBehaviourId == 11373);
                    Check(push.MoveCause == EEntityMoveCause.Displaced && push.MoveSpeed == 2.5f
                        && push.Path.Count == (blocked ? 2 : 3)
                        && push.Path[push.Path.Count - 1].X - push.Path[0].X == (blocked ? 1 : 2)
                        && push.Path.All(position => position.Y == 5),
                        "the original outward two-cell move or active/passive cause was lost");
                    var collision = step.DamageEvents.Where(value => value.SkillEntityId == 10100).ToArray();
                    Check(blocked ? collision.Length == 1 && collision[0].TargetId == 2 && collision[0].Damage == -600
                        : collision.Length == 0,
                        "a blocked two-cell push bypassed the obstacle or lost the shared collision damage");
                    casts++;
                }
                string state = FightBattleCheckpointSerializer.Save(checkpoint);
                Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(checkpoint) == state,
                    "receipt replay repeated a passive entry roll or knockback");
            }
            if (casts > 0) procs++; else misses++;
            if (casts > 1) repeats++;
            cases++;
        }
        Check(procs > 0 && misses > 0 && repeats > 0, "both entry outcomes and a repeated approach were not exercised: "
            + procs + "/" + misses + "/" + repeats);
        return "Repelling wind active-entry filters, independent rolls, cultivation, wind damage, two-cell knockback and restore passed: "
            + cases + ", procs=" + procs + ", misses=" + misses + ", repeated approaches=" + repeats;
    }

    static void AddSkill(FightBattleUnitState unit, FightOfficialCombatCatalog catalog, int entity, int item, int rank, int level)
    {
        var props = catalog.CalculateSkillProps(entity, rank, level, 8);
        var skill = catalog.CreateSkill(entity, 0, item, rank, level, 8,
            props.Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
        skill.ResetCooldownAtStart = true;
        unit.Skills.Add(skill); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
    }

    static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = id == 1 ? 12 : 1,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 2 ? 1000 : 900;
        foreach (var p in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(p.Key), Value = p.Value });
        unit.BaseProperties = unit.Properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList();
        return unit;
    }
}
