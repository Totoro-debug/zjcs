using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>A blocked one-cell push and the enemy units which actually collided.</summary>
    public sealed class FightKnockbackCollision
    {
        public FightGridPoint BlockedPosition { get; private set; }
        public bool TerrainBlocked { get; private set; }
        public IReadOnlyList<FightBattleUnitState> Victims => victims;
        private readonly List<FightBattleUnitState> victims = new List<FightBattleUnitState>();

        /// <summary>
        /// Resolves the actual attempted adjacent position after the movement rule could not
        /// enter it. A destination with no blocking cell or transition is not a collision.
        /// </summary>
        public static bool TryCreate(FightBattleGrid grid, IReadOnlyList<FightBattleUnitState> units,
            FightBattleUnitState source, FightBattleUnitState mover, FightGridPoint destination,
            out FightKnockbackCollision collision)
        {
            if (mover == null) throw new ArgumentNullException(nameof(mover));
            return TryCreateAt(grid, units, source, mover, new FightGridPoint(mover.X, mover.Y), destination, out collision);
        }

        public static List<FightGridPoint> Trace(FightBattleGrid grid, IReadOnlyList<FightBattleUnitState> units,
            FightBattleUnitState source, FightBattleUnitState mover, int stepX, int stepY, int distance,
            out FightKnockbackCollision collision)
        {
            if (distance <= 0 || Math.Abs(stepX) > 1 || Math.Abs(stepY) > 1 || stepX == 0 && stepY == 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            var path = new List<FightGridPoint> { new FightGridPoint(mover.X, mover.Y) };
            collision = null;
            for (int index = 0; index < distance; index++)
            {
                var start = path[path.Count - 1];
                var next = new FightGridPoint(start.X + stepX, start.Y + stepY);
                if (TryCreateAt(grid, units, source, mover, start, next, out collision)) break;
                path.Add(next);
            }
            return path;
        }

        private static bool TryCreateAt(FightBattleGrid grid, IReadOnlyList<FightBattleUnitState> units,
            FightBattleUnitState source, FightBattleUnitState mover, FightGridPoint start, FightGridPoint destination,
            out FightKnockbackCollision collision)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (units == null) throw new ArgumentNullException(nameof(units));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (mover == null) throw new ArgumentNullException(nameof(mover));
            collision = null;
            long dx = (long)destination.X - start.X, dy = (long)destination.Y - start.Y;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || dx == 0 && dy == 0)
                throw new ArgumentException("A collision requires the next adjacent push position.", nameof(destination));

            var originalBody = BodyAt(mover, start);
            var nextBody = BodyAt(mover, destination);
            nextBody.ExceptWith(originalBody);
            bool terrain = TransitionBlocked(grid, start, destination);
            foreach (FightGridPoint cell in nextBody)
                if (!grid.CanStand(cell, mover.MoveMask, null)) terrain = true;

            var blockers = new List<FightBattleUnitState>();
            var blockerIds = new HashSet<int>();
            foreach (FightBattleUnitState unit in units)
            {
                if (unit == null || unit.Id == mover.Id || !FightBattleTargeting.IsSelectable(unit)) continue;
                foreach (FightGridPoint cell in nextBody)
                    if (FightBattleTargeting.OccupiesPosition(unit, cell))
                    {
                        if (blockerIds.Add(unit.Id)) blockers.Add(unit);
                        break;
                    }
            }
            if (!terrain && blockers.Count == 0) return false;

            var result = new FightKnockbackCollision { BlockedPosition = destination, TerrainBlocked = terrain };
            var victimIds = new HashSet<int>();
            void AddEnemy(FightBattleUnitState unit)
            {
                if (FightBattleTargeting.IsSelectable(unit) && unit.Side != source.Side && victimIds.Add(unit.Id))
                    result.victims.Add(unit);
            }
            AddEnemy(mover);
            foreach (var blocker in blockers) AddEnemy(blocker);
            collision = result;
            return true;
        }

        /// <summary>The user-confirmed ten percent maximum-HP damage with a sixty percent attack cap.</summary>
        public static long Damage(long targetMaximumHp, long sourceAttack)
        {
            long hp = Math.Max(0L, targetMaximumHp);
            long attack = Math.Max(0L, sourceAttack);
            // Dividing first keeps both percentages exact for the full positive Int64 range.
            long cap = attack / 5 * 3 + attack % 5 * 3 / 5;
            return Math.Min(hp / 10, cap);
        }

        private static HashSet<FightGridPoint> BodyAt(FightBattleUnitState unit, FightGridPoint anchor)
        {
            var result = new HashSet<FightGridPoint> { anchor };
            if (unit.BodyOffsets != null)
                foreach (var offset in unit.BodyOffsets) result.Add(anchor + offset);
            return result;
        }

        private static bool TransitionBlocked(FightBattleGrid grid, FightGridPoint from, FightGridPoint to)
        {
            if (grid.BlockedTransitions == null) return false;
            foreach (FightGridTransition edge in grid.BlockedTransitions)
                if (edge != null && (edge.From.Equals(from) && edge.To.Equals(to)
                    || edge.From.Equals(to) && edge.To.Equals(from))) return true;
            return false;
        }
    }
}
