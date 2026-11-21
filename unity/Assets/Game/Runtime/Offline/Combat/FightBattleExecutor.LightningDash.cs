using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // Confirmed offline point/path selection. Damage and presentation still use 10680's graph.
        private static bool IsLightningDash(FightBattleSkillDefinition skill)
            => skill != null && skill.EntityId == 10680 && skill.ItemId == 10016;

        private static bool IsShieldCharge(FightBattleSkillDefinition skill)
            => skill != null && skill.EntityId == 11730 && skill.ItemId == 10073;

        private static bool IsStraightDash(FightBattleSkillDefinition skill)
            => IsLightningDash(skill) || IsShieldCharge(skill);

        private bool TryPlanStraightDash(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            out FightBattleUnitState position, out List<FightBattleUnitState> targets)
        {
            position = null;
            targets = new List<FightBattleUnitState>();
            if (checkpoint.Grid == null || statusCatalog == null) return false;
            var eligible = units.Where(unit => FightBattleTargeting.Select(actor,
                new[] { unit }, skill.TargetType, skill: skill).Count > 0).ToList();
            int moveId = skill.SourceMoves[0].MoveBehaviourId;
            var chargeHit = IsShieldCharge(skill)
                ? FightBattleHitCollector.CollectAll(skill).Single(hit => hit.EntityId == 11731) : null;
            foreach (var offset in skill.Range)
            {
                var landing = new FightGridPoint(actor.X + offset.X, actor.Y + offset.Y);
                if (!TryStraightDashPath(actor, landing, moveId, out var path)) continue;
                IEnumerable<FightGridPoint> hitCells = path.Skip(1).Take(path.Count - 2);
                if (chargeHit != null)
                {
                    var facing = new FightBattleSkillCastPresentation {
                        FacingIndex = offset.X < 0 ? 1 : offset.X > 0 ? 3 : offset.Y > 0 ? 2 : 0,
                        FlipX = offset.X == 0 ? actor.FlipX : offset.X < 0
                    };
                    var start = path[0];
                    hitCells = FightBattleTargeting.TransformScope(chargeHit.Scope,
                        chargeHit.ScopeTransform, actor, facing).Select(cell => start + cell);
                }
                var covered = eligible.Where(unit => hitCells
                    .Any(point => FightBattleTargeting.OccupiesPosition(unit, point))).ToList();
                // Stable official range order breaks ties; probing consumes no combat random draws.
                if (covered.Count <= targets.Count) continue;
                position = new FightBattleUnitState { X = landing.X, Y = landing.Y };
                targets = covered;
            }
            return position != null;
        }

        private bool TryStraightDashPath(FightBattleUnitState actor, FightGridPoint landing,
            int moveId, out List<FightGridPoint> path)
        {
            path = new List<FightGridPoint> { new FightGridPoint(actor.X, actor.Y) };
            var grid = checkpoint.Grid;
            int dx = landing.X - actor.X, dy = landing.Y - actor.Y;
            if (grid == null || dx != 0 && dy != 0 || Math.Abs(dx) + Math.Abs(dy) != 5) return false;
            var occupied = OccupiedPositions(actor.Id);
            var body = actor.BodyOffsets == null || actor.BodyOffsets.Count == 0
                ? new List<FightGridPoint> { new FightGridPoint() } : actor.BodyOffsets;
            foreach (var offset in body)
                if (!grid.CanStand(landing + offset, actor.MoveMask, occupied)) return false;
            int passHeight = statusCatalog.SkillMovePassHeight(moveId);
            for (int i = 1; i <= 5; i++)
            {
                var point = new FightGridPoint(actor.X + Math.Sign(dx) * i, actor.Y + Math.Sign(dy) * i);
                foreach (var offset in body)
                {
                    var cell = point + offset;
                    if (!grid.Contains(cell) || grid.BlockedPositions.Contains(cell)
                        || grid.Cells.Any(value => value.X == cell.X && value.Y == cell.Y && value.Height > passHeight)) return false;
                    var previous = path[path.Count - 1] + offset;
                    if (grid.BlockedTransitions.Any(value => value.From.Equals(previous) && value.To.Equals(cell)
                        || value.To.Equals(previous) && value.From.Equals(cell))) return false;
                    foreach (var unit in units)
                        if (unit.Id != actor.Id && FightBattleTargeting.IsSelectable(unit)
                            && FightBattleTargeting.OccupiesPosition(unit, cell)
                            && new HashSet<FightGridPoint>((unit.BodyOffsets ?? new List<FightGridPoint>()).Concat(new[] { new FightGridPoint() })).Count > 4)
                            return false;
                }
                path.Add(point);
            }
            return true;
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        internal int SkillMovePassHeight(int moveId)
            => Int(Component(RequiredEntity(moveId), "FightActionSkillMoveComponent"), "PassHeight");
    }
}
