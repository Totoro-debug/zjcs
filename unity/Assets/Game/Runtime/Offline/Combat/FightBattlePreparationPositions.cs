using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Offline position acceptance for the user-confirmed solo preparation policy. Range lookup
    /// uses the official client implementation; legal owned-unit swaps replace the server request.
    /// The owning mode supplies the controllable roster instead of inferring NPC ownership.
    /// </summary>
    public sealed class FightBattlePreparationPositions
    {
        private readonly FightBattleCheckpoint state;
        private readonly IReadOnlyList<FightBattlePreparationRange> ranges;
        private readonly HashSet<int> controlled;

        public FightBattlePreparationPositions(FightBattleCheckpoint state,
            IReadOnlyList<FightBattlePreparationRange> ranges, IEnumerable<int> controlledUnitIds)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.ranges = ranges ?? throw new ArgumentNullException(nameof(ranges));
            controlled = new HashSet<int>(controlledUnitIds ?? throw new ArgumentNullException(nameof(controlledUnitIds)));
        }

        public bool CanControl(int id)
        {
            FightBattleUnitState unit = Find(id);
            FightBattleUnitState player = Find(1);
            return controlled.Contains(id) && unit != null && player != null && unit.Side == player.Side
                && unit.Hp > 0 && !unit.Ended && (id == 1 || unit.CharacterType == 4);
        }

        public bool TryPreview(int id, FightGridPoint requested, out FightGridPoint grid)
        {
            FightBattleUnitState source = Find(id);
            grid = source == null ? default : new FightGridPoint(source.X, source.Y);
            if (!CanControl(id)) return false;
            if (grid.Equals(requested)) return true;
            return FightBattlePreparationRange.TryGetClosest(ranges, requested,
                candidate => CanMoveTo(source, candidate, out _), out grid);
        }

        public bool TryMove(int id, FightGridPoint destination, FightBattlePhase phase, bool ready)
        {
            if (phase != FightBattlePhase.Preparing || ready || state.Finished || !CanControl(id)) return false;
            FightBattleUnitState source = Find(id);
            if (source.X == destination.X && source.Y == destination.Y) return false;
            if (!CanMoveTo(source, destination, out FightBattleUnitState target)) return false;
            int oldX = source.X, oldY = source.Y;
            // Validate both destinations before mutating either position. Rejection leaves the
            // checkpoint untouched, so failed drops and cancelled drags can restore the old view.
            source.X = destination.X;
            source.Y = destination.Y;
            if (target != null) { target.X = oldX; target.Y = oldY; }
            return true;
        }

        private bool CanMoveTo(FightBattleUnitState source, FightGridPoint destination,
            out FightBattleUnitState target)
        {
            target = null;
            if (!FightBattlePreparationRange.TryGetCenter(ranges, destination, out _)) return false;
            foreach (FightBattleUnitState other in state.Units)
                if (other != source && other.Hp > 0 && !other.Ended
                    && other.X == destination.X && other.Y == destination.Y)
                {
                    if (target != null || !CanControl(other.Id)) return false;
                    target = other;
                }
            var occupied = new HashSet<FightGridPoint>();
            foreach (FightBattleUnitState other in state.Units)
                if (other != source && other != target && other.Hp > 0 && !other.Ended)
                    foreach (FightGridPoint offset in Body(other))
                        occupied.Add(new FightGridPoint(other.X + offset.X, other.Y + offset.Y));
            if (!CanFit(source, destination, occupied)) return false;
            if (target != null)
            {
                foreach (FightGridPoint offset in Body(source)) occupied.Add(destination + offset);
                if (!CanFit(target, new FightGridPoint(source.X, source.Y), occupied)) return false;
            }
            return true;
        }

        private bool CanFit(FightBattleUnitState unit, FightGridPoint position, ISet<FightGridPoint> occupied)
        {
            foreach (FightGridPoint offset in Body(unit))
            {
                FightGridPoint point = position + offset;
                if (occupied.Contains(point) || (state.Grid != null && !state.Grid.CanStand(point, unit.MoveMask, occupied)))
                    return false;
            }
            return true;
        }

        private static IReadOnlyList<FightGridPoint> Body(FightBattleUnitState unit) =>
            unit.BodyOffsets != null && unit.BodyOffsets.Count > 0 ? unit.BodyOffsets
                : (IReadOnlyList<FightGridPoint>)new[] { new FightGridPoint(0, 0) };

        private FightBattleUnitState Find(int id) => state.Units.Find(unit => unit != null && unit.Id == id);
    }
}
