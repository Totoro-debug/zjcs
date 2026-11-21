using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleVortexSurgeHarness
{
    private sealed class ContactGate : IFightBattleImpactGate, IFightBattleBlockedMovementObserver,
        IFightBattleBlockedImpactObserver
    {
        public int Contacts;
        public bool ObservesBlockedMovements => true;
        public void AwaitImpact(FightBattlePendingImpact impact) { }
        public void BlockedMovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint blockedPosition) { }
        public void AwaitBlockedContact(FightBattleMovementEvent movement) { Contacts++; }
        public void BindBlockedImpact(FightBattleMovementEvent movement, FightBattleSkillCastEvent cast, int targetId)
        { Check(Contacts == 1, "collision damage ran before the original action contact"); }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Vortex surge: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, nominalMoved = 0, nominalStayed = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (int cultivation in new[] { 0, 1 })
        foreach (int distance in new[] { 2, 3 })
        foreach (int obstacle in new[] { 0, 1, 2 })
        foreach (int chance in new[] { -1, 0, 1 })
        foreach (int seed in Enumerable.Range(1, 8))
        {
            int dx = direction[0], dy = direction[1], tx = 10 + distance * dx, ty = 10 + distance * dy;
            var actor = Unit(1, 1, 10, 10, formula);
            var target = Unit(2, 2, tx, ty, formula);
            var blocker = Unit(3, 1, tx - dx, ty - dy, formula);
            var ally = Unit(4, 1, 10 + dy, 10 - dx, formula);
            var outside = Unit(5, 2, 10 + 5 * dx, 10 + 5 * dy, formula);
            actor.Properties.Add(new FightPropertyValue {
                Id = formula.PropertyId("EffectRateValue"), Value = chance * 100000
            });
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 8, RandomSeed = seed * 1000003 + 12345,
                Grid = new FightBattleGrid { Width = 22, Height = 22, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, target, ally, outside });
            if (obstacle == 1) checkpoint.Grid.BlockedPositions.Add(new FightGridPoint(tx - dx, ty - dy));
            if (obstacle == 2) checkpoint.Units.Add(blocker);
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 5 }); checkpoint.Waves.Add(wave);
            int rank = cultivation == 0 ? 4 : 5, level = cultivation == 0 ? 2 : 20;
            var props = catalog.CalculateSkillProps(12080, rank, level, 8);
            var skill = catalog.CreateSkill(12080, 0, 10099, rank, level, 8,
                props.Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            executor.ImpactGate = new ContactGate();
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            restored.ImpactGate = new ContactGate();
            FightBattleStepEvent step = null;
            FightBattleSkillCastEvent cast = null;
            while (cast == null && checkpoint.Counter < 8 && !checkpoint.Finished)
            {
                step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                restored.Step(copy.Sequence, copy.Counter + 1);
                cast = step.SkillCasts.SingleOrDefault(value => value.SkillEntityId == 12080);
            }
            Check(cast != null, "the original water surge did not release");
            var damage = step.DamageEvents.Single(value => value.SkillEntityId == 12080);
            long expectedDamage = -(long)((1000 * formula.ReadProperty(props.ToDictionary(p => p.Key, p => p.Value), 200)
                + formula.ReadProperty(props.ToDictionary(p => p.Key, p => p.Value), 190)) * 1000 / 1010);
            Check(damage.TargetId == target.Id && damage.HitEntityId == 12081 && damage.DamageEntityId == 12082
                && damage.ElementType == 101 && damage.DamageType == 1 && !damage.TrueDamage && !damage.IsCure
                && damage.Damage == expectedDamage
                && Math.Abs(damage.Delay - cast.Delay - .53f - (distance == 2 ? .165f : .19f)) < .0001f,
                "water damage, cultivation, ring delay or friendly/outside exclusion differs");
            var moves = step.Movements.Where(value => value.UnitId == target.Id && value.MoveBehaviourId == 12083).ToArray();
            if (chance < 0)
                Check(moves.Length == 0 && target.X == tx && target.Y == ty, "resisted or blocked target moved");
            else if (chance > 0)
                Check(moves.Length == 1, "guaranteed unobstructed pull did not move");
            if (moves.Length > 0)
            {
                var move = moves.Single();
                Check(target.X == tx - (obstacle == 0 ? dx : 0) && target.Y == ty - (obstacle == 0 ? dy : 0)
                    && move.Path.Count == 2
                    && move.Path[0].X == tx && move.Path[0].Y == ty
                    && Math.Abs(move.Delay - damage.Delay) < .0001f && move.MoveDuration == -1f
                    && Math.Abs(move.MoveSpeed - 2.5f) < .0001f,
                    "one-cell inward knockback differs: target=" + target.X + "/" + target.Y
                    + ", start=" + tx + "/" + ty + ", path=" + move.Path.Count
                    + ", delay=" + move.Delay + "/" + damage.Delay + ", duration=" + move.MoveDuration);
            }
            if (chance == 0 && obstacle == 0)
            {
                if (moves.Length > 0) nominalMoved++; else nominalStayed++;
            }
            var collisions = step.DamageEvents.Where(value => value.SkillEntityId == 10100).ToArray();
            if (chance > 0 && obstacle > 0)
                Check(collisions.Length == 1 && collisions[0].TargetId == target.Id && collisions[0].TrueDamage
                    && collisions[0].ElementType == 0 && collisions[0].Damage == -600,
                    "original body collision differs: " + string.Join(",", collisions.Select(value =>
                        value.TargetId + ":" + value.Damage + ":" + value.TrueDamage + ":" + value.ElementType))
                    + "; all=" + string.Join(",", step.DamageEvents.Select(value => value.SkillEntityId + "/" + value.TargetId + "/" + value.Damage)));
            if (chance < 0 || obstacle == 0) Check(collisions.Length == 0, "knockback created an unrelated collision hit");
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                "restore changed movement probability, collision or water damage");
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(checkpoint) == saved, "receipt replay repeated the inward knockback");
            cases++;
        }
        Check(nominalMoved > 0 && nominalStayed > 0, "forty-percent movement lacks both outcomes: "
            + nominalMoved + "/" + nominalStayed);
        return "Vortex surge passed: " + cases
            + " direction, cultivation, water damage, movement probability, obstruction, collision and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 4, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000
        };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new[] {
            new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 },
            new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp },
            new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed },
            new FightPropertyValue { Id = formula.PropertyId("BaseEffectRate"), Value = 1000 },
            new FightPropertyValue { Id = formula.PropertyId("BaseEffectDodge"), Value = 1000 }
        }) unit.Properties.Add(value);
        return unit;
    }
}
