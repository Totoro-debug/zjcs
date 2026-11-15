using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleHitPullHarness
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static FightBattleHitDefinition Hit(int id, int action, int offsetY = 0)
    {
        return new FightBattleHitDefinition { DamageId = id, HitMove = new FightBattleSkillMove {
            MoveBehaviourId = action, Distance = -1, Direction = 0, TowardsTargetPos = true, OffsetY = offsetY
        }};
    }

    public static void Run()
    {
        var source = new FightBattleUnitState { X = 1, Y = 2, MoveMask = 1 << 2 };
        var target = new FightBattleUnitState { X = 5, Y = 2, MoveMask = 1 << 2 };
        var grid = new FightBattleGrid { Width = 8, Height = 6, DefaultGroundType = 2 };
        var occupied = new HashSet<FightGridPoint> { new FightGridPoint(1, 2) };
        var hit = Hit(12877, 12878);
        var rule = FightHitMovementRules.For(hit);
        Require(rule != null, "The confirmed source-pull configuration was not recognized.");
        var context = new FightHitMovementContext { Source = source, Target = target, Grid = grid, Occupied = occupied, Hit = hit };
        var path = rule.Plan(context);
        Require(path != null && path[0].Equals(new FightGridPoint(5, 2))
            && path[path.Count - 1].Equals(new FightGridPoint(2, 2)), "Pull did not reach the nearest available cell.");
        Require(target.X == 5 && target.Y == 2, "Planning changed the target before settlement.");

        context.Hit = Hit(57594, 57595, 1);
        path = FightHitMovementRules.For(context.Hit).Plan(context);
        Require(path != null && path[path.Count - 1].Equals(new FightGridPoint(1, 3)), "Offset pull did not reach its exact target.");
        occupied.Add(new FightGridPoint(1, 3));
        Require(FightHitMovementRules.For(context.Hit).Plan(context) == null, "Blocked offset target accepted a fallback.");
        occupied.Remove(new FightGridPoint(1, 3));

        target.BodyOffsets.Add(new FightGridPoint(0, 1));
        occupied.Add(new FightGridPoint(2, 3));
        context.Hit = hit;
        path = rule.Plan(context);
        Require(path != null, "A valid alternative for the two-cell target was not found.");
        foreach (var point in path)
            if (!point.Equals(path[0]))
                Require(grid.CanStand(point, target.MoveMask, occupied)
                    && grid.CanStand(point + new FightGridPoint(0, 1), target.MoveMask, occupied), "Pull crossed an occupied body cell.");

        target.BodyOffsets.Clear();
        target.X = 2;
        target.Y = 2;
        Require(rule.Plan(context) == null, "An already-adjacent target moved without getting closer.");
        Require(FightHitMovementRules.For(Hit(12877, 1)) == null && FightHitMovementRules.For(Hit(99999, 12878)) == null,
            "An unconfirmed damage/action pair was accepted.");
        var selected = new FightBattleUnitState { X = 6, Y = 4 };
        target.X = 3; target.Y = 4;
        context.SkillTarget = selected;
        context.Hit = Hit(59972, 59973);
        context.Hit.HitMove.Direction = 1;
        occupied.Add(new FightGridPoint(6, 4));
        path = FightHitMovementRules.For(context.Hit).Plan(context);
        Require(path != null && path[path.Count - 1].Equals(new FightGridPoint(5, 4)),
            "Target-relative pull did not approach the selected skill target.");
        context.SkillTarget = null;
        Require(FightHitMovementRules.For(context.Hit).Plan(context) == null,
            "Target-relative pull guessed a missing selected target.");
        context.Hit.HitMove.TowardsTargetPos = false;
        Require(FightHitMovementRules.For(context.Hit) == null, "Unsupported negative-distance repulsion moved.");
        var push = Hit(13214, 13215);
        push.HitMove.Distance = 1; push.HitMove.TowardsTargetPos = false; push.HitMove.CancelDiagonally = true;
        int[] expectedX = { 0, -1, 0, 1 }, expectedY = { -1, 0, 1, 0 };
        for (int facing = 0; facing < 4; facing++)
            Require(FightHitMovementRules.TryGetFacingPush(push, facing, out int x, out int y)
                && x == expectedX[facing] && y == expectedY[facing], "Confirmed push facing mismatch: " + facing);
        Require(!FightHitMovementRules.TryGetFacingPush(push, 4, out _, out _), "Invalid facing was accepted.");
        push.DamageId = 99999;
        Require(!FightHitMovementRules.TryGetFacingPush(push, 0, out _, out _), "Unconfirmed facing push was accepted.");
        var targetMove = Hit(64362, 64363);
        targetMove.HitMove.Distance = 2; targetMove.HitMove.Direction = 1;
        Require(FightHitMovementRules.UsesSkillTarget(targetMove), "Confirmed target-relative distance was not recognized.");
        targetMove.HitMove.MoveBehaviourId = 1;
        Require(!FightHitMovementRules.UsesSkillTarget(targetMove), "Unconfirmed target-relative action was accepted.");
        Console.WriteLine("Confirmed pull landing, selected target, offset, occupancy, full body and exclusions passed.");
    }
}
