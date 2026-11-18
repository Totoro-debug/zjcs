using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class KnockbackCollisionHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Knockback collision: " + message); }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, long hp = 1000)
        => new FightBattleUnitState { Id = id, Side = side, X = x, Y = y, Hp = hp, MaxHp = hp, MoveMask = 4 };
    private static FightBattleGrid Grid() => new FightBattleGrid { Width = 10, Height = 10, DefaultGroundType = 2 };
    private static int[] Ids(FightKnockbackCollision result) => result.Victims.Select(unit => unit.Id).ToArray();
    public static void Run()
    {
        var source = Unit(1, 1, 1, 5); var mover = Unit(2, 2, 3, 5); var blocker = Unit(3, 2, 4, 5);
        var units = new List<FightBattleUnitState> { source, mover, blocker };
        Check(FightKnockbackCollision.TryCreate(Grid(), units, source, mover, new FightGridPoint(4, 5), out var result)
            && !result.TerrainBlocked && result.BlockedPosition.Equals(new FightGridPoint(4, 5))
            && Ids(result).SequenceEqual(new[] { 2, 3 }), "both colliding enemies must be selected once");
        blocker.Side = 1;
        Check(FightKnockbackCollision.TryCreate(Grid(), units, source, mover, new FightGridPoint(4, 5), out result)
            && Ids(result).SequenceEqual(new[] { 2 }), "a friendly obstacle must not receive enemy collision damage");
        blocker.Side = 2; mover.Side = 1;
        Check(FightKnockbackCollision.TryCreate(Grid(), units, source, mover, new FightGridPoint(4, 5), out result)
            && Ids(result).SequenceEqual(new[] { 3 }), "a friendly mover must not receive enemy collision damage");
        mover.Side = 2; blocker.Targetable = false;
        Check(!FightKnockbackCollision.TryCreate(Grid(), units, source, mover, new FightGridPoint(4, 5), out result),
            "a unit absent from the current occupancy rules cannot fabricate a collision");
        blocker.Targetable = true;

        var wall = Grid(); wall.BlockedPositions.Add(new FightGridPoint(4, 5));
        Check(FightKnockbackCollision.TryCreate(wall, units, source, mover, new FightGridPoint(4, 5), out result)
            && result.TerrainBlocked && Ids(result).SequenceEqual(new[] { 2, 3 }),
            "terrain and a unit on the same blocked cell must both be identified");
        var edge = Grid(); edge.BlockedTransitions.Add(new FightGridTransition { From = new FightGridPoint(4, 5), To = new FightGridPoint(3, 5) });
        Check(FightKnockbackCollision.TryCreate(edge, new[] { source, mover }, source, mover, new FightGridPoint(4, 5), out result)
            && result.TerrainBlocked && Ids(result).SequenceEqual(new[] { 2 }), "a blocked transition must work in both directions");
        var rim = Unit(5, 2, 9, 5);
        Check(FightKnockbackCollision.TryCreate(Grid(), new[] { source, rim }, source, rim, new FightGridPoint(10, 5), out result)
            && result.TerrainBlocked && Ids(result).SequenceEqual(new[] { 5 }), "the map edge must block an enemy mover");
        var land = Grid(); land.Cells.Add(new FightBattleGridCell { X = 4, Y = 5, GroundType = 1 });
        Check(FightKnockbackCollision.TryCreate(land, new[] { source, mover }, source, mover, new FightGridPoint(4, 5), out result)
            && result.TerrainBlocked, "unstandable ground must block the push");

        mover.BodyOffsets.Add(new FightGridPoint(0, 1));
        blocker.BodyOffsets.Add(new FightGridPoint(0, 1));
        var other = Unit(6, 2, 4, 6);
        Check(FightKnockbackCollision.TryCreate(Grid(), new[] { source, mover, blocker, other }, source, mover,
            new FightGridPoint(4, 5), out result) && Ids(result).SequenceEqual(new[] { 2, 3, 6 }),
            "multi-cell bodies must identify all colliding enemies without duplicate damage");
        mover.BodyOffsets.Clear(); mover.BodyOffsets.Add(new FightGridPoint(0, 0));
        Check(!FightKnockbackCollision.TryCreate(Grid(), new[] { source, mover }, source, mover,
            new FightGridPoint(4, 5), out result), "an open destination must not fabricate contact");
        bool rejected = false;
        try { FightKnockbackCollision.TryCreate(Grid(), units, source, mover, new FightGridPoint(5, 5), out _); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "collision detection must receive the next adjacent movement position");

        Check(FightKnockbackCollision.Damage(1000, 5000) == 100, "maximum-HP percentage");
        Check(FightKnockbackCollision.Damage(10000, 150) == 90, "attack cap");
        Check(FightKnockbackCollision.Damage(999, 151) == 90, "fractional values round down without exceeding the cap");
        Check(FightKnockbackCollision.Damage(long.MaxValue, long.MaxValue) == long.MaxValue / 10,
            "large health and attack must not overflow");
        Check(FightKnockbackCollision.Damage(1000, long.MaxValue) == 100
            && FightKnockbackCollision.Damage(1000, -1) == 0, "attack bounds");
        Console.WriteLine("Knockback collision units, terrain, body footprints and damage cap passed.");
    }
}
