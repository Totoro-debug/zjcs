using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleDoomTorrentHarness
{
    private sealed class ContactGate : IFightBattleImpactGate, IFightBattleBlockedMovementObserver,
        IFightBattleBlockedImpactObserver
    {
        private readonly HashSet<FightBattleMovementEvent> contacts = new HashSet<FightBattleMovementEvent>();
        public int Contacts => contacts.Count;
        public bool ObservesBlockedMovements => true;
        public void AwaitImpact(FightBattlePendingImpact impact) { }
        public void BlockedMovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint blockedPosition) { }
        public void AwaitBlockedContact(FightBattleMovementEvent movement) { contacts.Add(movement); }
        public void BindBlockedImpact(FightBattleMovementEvent movement, FightBattleSkillCastEvent cast, int targetId)
        { Check(contacts.Contains(movement), "collision damage preceded its actual movement contact"); }
    }

    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Doom torrent: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, randomMoved = 0, randomStayed = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (bool large in new[] { false, true })
        {
            int dx = direction[0], dy = direction[1];
            var state = State(rank * 100 + level, formula);
            var primary = Unit(2, 2, 10 + dx, 10 + dy, formula);
            if (large) primary.BodyOffsets.Add(new FightGridPoint(dx, dy));
            state.Units.Add(primary);
            state.Units.Add(Unit(3, 2, 10 + dx * 5 - dy, 10 + dy * 5 + dx, formula));
            state.Units.Add(Unit(4, 2, 10 + dx * 3 - dy * 2, 10 + dy * 3 + dx * 2, formula));
            state.Units.Add(Unit(5, 1, 10 + dx * 2 + dy, 10 + dy * 2 - dx, formula));
            var step = Step(state, catalog, formula, items, rank, level, -10000);
            var cast = step.SkillCasts.Single(value => value.SkillEntityId == 11080);
            var hits = step.DamageEvents.Where(value => value.SkillEntityId == 11080).ToArray();
            var props = catalog.CalculateSkillProps(11080, rank, level, 8);
            long expected = (long)((1000m * props[200] / 10000m + props[190]) * 1000m / 1010m);
            Check(hits.Length == (large ? 9 : 6) && hits.Count(hit => hit.TargetId == 2) == (large ? 6 : 3)
                && hits.Count(hit => hit.TargetId == 3) == 3,
                "the original three waves lost their front 3x5 scope/body cells: " + dx + "," + dy
                    + " hits=" + string.Join(";", hits.Select(hit => hit.TargetId + ":" + hit.DamageX + "," + hit.DamageY)));
            Check(hits.All(hit => hit.SourceId == 1 && hit.HitEntityId == 11081 && hit.DamageEntityId == 11082
                && hit.SkillItemId == 10056 && hit.ElementType == 101 && hit.DamageType == 1
                && !hit.TrueDamage && !hit.IsCure && hit.Damage == -expected),
                "cultivated fixed/percentage water damage changed");
            var nearest = hits.Where(hit => hit.TargetId == 2
                && hit.DamageX == primary.X && hit.DamageY == primary.Y).OrderBy(hit => hit.Delay).ToArray();
            Check(nearest.Length == 3 && nearest.Select((hit, index) =>
                Math.Abs(hit.Delay - cast.Delay - .68f - index * .4f) < .0001f).All(value => value),
                "the original .6/1/1.4 second waves or .08 near-cell delay changed");
            Check(state.Units.Single(unit => unit.Id == 4).Hp == 1000000
                && state.Units.Single(unit => unit.Id == 5).Hp == 1000000 && step.Movements.Count == 0,
                "the torrent hit an excluded unit or ignored zero effect chance");
            cases++;
        }

        foreach (int rank in new[] { 1, 7 })
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (int rate in new[] { -10000, 0, 100000 })
        for (int seed = 1; seed <= 4; seed++)
        {
            var state = State(seed * 7919 + rank, formula);
            var primary = Unit(2, 2, 10 + direction[0], 10 + direction[1], formula);
            state.Units.Add(primary);
            var step = Step(state, catalog, formula, items, rank, 2, rate);
            var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 11080).OrderBy(hit => hit.Delay).ToArray();
            var moves = step.Movements.Where(move => move.UnitId == 2 && move.MoveBehaviourId == 11083).ToArray();
            Check(hits.Length == 3, "a successful push removed the target from a later wave");
            if (rate == -10000) Check(moves.Length == 0, "zero effect chance still pushed");
            if (rate == 100000) Check(moves.Length == 3, "increased effect hit did not affect each 50% baseline push");
            Check(primary.X == 10 + direction[0] * (1 + moves.Length)
                && primary.Y == 10 + direction[1] * (1 + moves.Length),
                "each successful wave did not push one cell away from its caster");
            Check(moves.All(move => move.Path.Count == 2 && Math.Abs(move.MoveDuration - .25f) < .0001f
                && hits.Any(hit => Math.Abs(move.Delay - hit.Delay - .05f) < .0001f)),
                "the original knockback path, duration or post-hit delay changed");
            if (rate == 0) { randomMoved += moves.Length; randomStayed += 3 - moves.Length; }
            cases++;
        }
        Check(randomMoved > 0 && randomStayed > 0, "the original 50% knockback became unconditional");
        foreach (var direction in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
        foreach (bool terrain in new[] { false, true })
        {
            int dx = direction[0], dy = direction[1];
            var state = State(37, formula);
            var primary = Unit(2, 2, 10 + dx, 10 + dy, formula); state.Units.Add(primary);
            if (terrain) state.Grid.BlockedPositions.Add(new FightGridPoint(10 + 2 * dx, 10 + 2 * dy));
            else state.Units.Add(Unit(3, 1, 10 + 2 * dx, 10 + 2 * dy, formula));
            var gate = new ContactGate();
            var step = Step(state, catalog, formula, items, 7, 2, 100000, gate);
            var collisions = step.DamageEvents.Where(hit => hit.SkillEntityId == 10100).ToArray();
            Check(gate.Contacts == 3 && collisions.Length == 3 && collisions.All(hit => hit.TargetId == 2
                && hit.TrueDamage && hit.ElementType == 0 && hit.Damage == -600)
                && primary.X == 10 + dx && primary.Y == 10 + dy,
                "a blocked wave lost its contact or user-confirmed capped collision damage");
            if (!terrain) Check(state.Units.Single(unit => unit.Id == 3).Hp == 1000000,
                "collision damaged a friendly body blocker");
            cases++;
        }
        return "Doom torrent passed: " + cases + " cultivated three-wave water, front/body scope, timing, effect-hit knockback, blocked contact and restore cases.";
    }

    private static FightBattleStepEvent Step(FightBattleCheckpoint state, FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items, int rank, int level, int effectRate,
        ContactGate gate = null)
    {
        var source = state.Units[0];
        source.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRate"), Value = effectRate });
        var skill = catalog.CreateSkill(11080, 0, 10056, rank, level, 8, catalog.CalculateSkillProps(11080, rank, level, 8)
            .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        source.Skills.Add(skill); source.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(state.Units.Where(unit => unit.Side == 2).Select(unit => unit.Id));
        state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        executor.ImpactGate = gate;
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        if (gate != null) restored.ImpactGate = new ContactGate();
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        restored.Step(copy.Sequence, copy.Counter + 1);
        Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
            "restoration changed wave damage, scope or knockback rolls");
        string saved = FightBattleCheckpointSerializer.Save(state);
        Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
            "receipt replay applied a wave or push twice");
        return step;
    }

    private static FightBattleCheckpoint State(int seed, FightDamageFormula formula)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        state.Units.Add(Unit(1, 1, 10, 10, formula)); return state;
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 13,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
