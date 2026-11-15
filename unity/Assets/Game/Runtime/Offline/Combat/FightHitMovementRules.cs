using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightHitMovementContext
    {
        public FightBattleUnitState Source;
        public FightBattleUnitState Target;
        public FightBattleUnitState SkillTarget;
        public FightBattleHitDefinition Hit;
        public FightBattleGrid Grid;
        public ISet<FightGridPoint> Occupied;
    }

    /// <summary>Plans a settled hit's movement without changing units or drawing random numbers.</summary>
    public interface IFightHitMovementRule
    {
        IReadOnlyList<FightGridPoint> Plan(FightHitMovementContext context);
    }

    public static class FightHitMovementRules
    {
        // Only these original damage/action pairs use the confirmed offline landing rule.
        private static readonly Dictionary<int, int> SourcePullActions = new Dictionary<int, int>
        {
            {12877, 12878}, {13127, 13128}, {57594, 57595}, {57713, 57714},
            {58642, 58643}, {58927, 58928}, {59274, 59275}, {59494, 59495},
            {59634, 59635}, {61564, 59635}, {61647, 61648}, {61994, 61995},
            {65014, 65015}, {16452, 16453}, {16642, 16643}, {10702, 10703},
            {10972, 10973}, {11832, 11833}
        };
        private static readonly IFightHitMovementRule Approach = new ApproachRule();
        private static readonly Dictionary<int, int> TargetPullActions = new Dictionary<int, int>
        {
            {59972, 59973}, {15543, 10584}, {10583, 10584}, {11342, 11343}, {11352, 11353}
        };
        private static readonly Dictionary<int, int> FacingPushActions = new Dictionary<int, int>
        {
            {13214, 13215}, {57522, 57523}, {59072, 59073}, {17612, 17613}
        };
        private static readonly Dictionary<int, int> TargetDistanceActions = new Dictionary<int, int>
        {
            {12322, 12323}, {13802, 10873}, {60043, 60044}, {64362, 64363},
            {15984, 15983}, {16373, 16374}, {18324, 15983}, {10872, 10873},
            {19364, 19363}, {11122, 11123}
        };
        private static readonly Dictionary<int, int> SideActions = new Dictionary<int, int>
        {
            {56233, 56234}, {56323, 56324}, {56524, 56523}, {56834, 56523},
            {57623, 57624}, {58063, 58064}, {59153, 59154}, {59733, 59734},
            {59753, 59754}, {62403, 62404}, {62443, 59073}, {62448, 59073},
            {63573, 57624}, {63883, 57624}, {64813, 62404}, {17143, 17144},
            {19623, 19624}, {11733, 11734}
        };

        public static bool UsesConfirmedSideMove(FightBattleHitDefinition hit)
        {
            FightBattleSkillMove move = hit?.HitMove;
            return move != null && move.Distance > 0 && move.Direction == 2
                && move.OffsetX == 0 && move.OffsetY == 0
                && SideActions.TryGetValue(hit.DamageId, out int action) && action == move.MoveBehaviourId;
        }

        public static long SideOfLine(FightBattleUnitState source, FightBattleUnitState skillTarget, int x, int y)
            => ((long)skillTarget.X - source.X) * (y - source.Y)
                - ((long)skillTarget.Y - source.Y) * (x - source.X);

        public static bool UsesSkillTarget(FightBattleHitDefinition hit)
        {
            FightBattleSkillMove move = hit?.HitMove;
            return move != null && move.Distance > 0 && move.Direction == 1
                && TargetDistanceActions.TryGetValue(hit.DamageId, out int action)
                && action == move.MoveBehaviourId;
        }

        public static bool UsesFacingPush(FightBattleHitDefinition hit)
        {
            FightBattleSkillMove move = hit?.HitMove;
            return move != null && move.CancelDiagonally && !move.TowardsTargetPos && move.Distance > 0
                && move.Direction == 0 && FacingPushActions.TryGetValue(hit.DamageId, out int action)
                && action == move.MoveBehaviourId;
        }

        public static bool TryGetFacingPush(FightBattleHitDefinition hit, int facing, out int x, out int y)
        {
            x = 0; y = 0;
            if (!UsesFacingPush(hit)) return false;
            switch (facing)
            {
                case 0: y = -1; return true;
                case 1: x = -1; return true;
                case 2: y = 1; return true;
                case 3: x = 1; return true;
                default: return false;
            }
        }

        public static IFightHitMovementRule For(FightBattleHitDefinition hit)
        {
            FightBattleSkillMove move = hit?.HitMove;
            if (move == null || move.Distance != -1 || !move.TowardsTargetPos
                || move.CanExpandOffset || move.CancelDiagonally) return null;
            Dictionary<int, int> actions = move.Direction == 0 ? SourcePullActions
                : move.Direction == 1 ? TargetPullActions : null;
            return actions != null && actions.TryGetValue(hit.DamageId, out int actionId)
                && move.MoveBehaviourId == actionId ? Approach : null;
        }

        private sealed class ApproachRule : IFightHitMovementRule
        {
            public IReadOnlyList<FightGridPoint> Plan(FightHitMovementContext context)
            {
                if (context?.Source == null || context.Target == null || context.Grid == null
                    || context.Occupied == null
                    || For(context.Hit) != this) return null;
                FightBattleSkillMove move = context.Hit.HitMove;
                FightBattleUnitState target = context.Target;
                FightBattleUnitState referenceUnit = move.Direction == 0 ? context.Source : context.SkillTarget;
                if (referenceUnit == null) return null;
                var start = new FightGridPoint(target.X, target.Y);
                var reference = new FightGridPoint(referenceUnit.X + move.OffsetX,
                    referenceUnit.Y + move.OffsetY);
                bool exact = move.OffsetX != 0 || move.OffsetY != 0;
                // Include the origin even when a legacy body's offsets omitted it.
                var body = new List<FightGridPoint> { new FightGridPoint(0, 0) };
                if (target.BodyOffsets != null)
                    foreach (FightGridPoint offset in target.BodyOffsets)
                        if (!body.Contains(offset)) body.Add(offset);
                var path = context.Grid.FindPath(start, reference, target.MoveMask,
                    0, 0, true, body, context.Occupied);
                if (path == null || path.Count < 2) return null;
                FightGridPoint end = path[path.Count - 1];
                if (exact && !end.Equals(reference)) return null;
                // FindPath preserves its original nearest fallback and equal-distance order.
                // A zero-offset pull must improve distance; it cannot wander on a tie.
                if (!exact && Distance(end, reference) >= Distance(start, reference)) return null;
                for (int i = 1; i < path.Count; i++)
                    foreach (FightGridPoint offset in body)
                        if (!context.Grid.CanStand(path[i] + offset, target.MoveMask, context.Occupied))
                            return null;
                return path;
            }

            private static int Distance(FightGridPoint left, FightGridPoint right)
                => System.Math.Abs(left.X - right.X) + System.Math.Abs(left.Y - right.Y);
        }
    }
}
