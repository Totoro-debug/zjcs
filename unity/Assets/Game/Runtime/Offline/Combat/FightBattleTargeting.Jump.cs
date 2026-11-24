using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightBattleTargeting
    {
        internal static List<FightBattleUnitState> SelectAdjacentJumpTargets(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            Func<FightBattleUnitState, bool> canLand)
        {
            var candidates = SelectCandidates(actor, units, skill.TargetType, skill: skill,
                inRangeOnly: true, keepAllCandidates: true);
            candidates.RemoveAll(target => !canLand(target));
            RankOrdinaryAttackCandidates(actor, units, skill, candidates, 0, false);
            if (candidates.Count > 1) candidates.RemoveRange(1, candidates.Count - 1);
            if (candidates.Count > 0) actor.Direction = DirectionTo(actor, candidates[0]);
            return candidates;
        }

        internal static bool TryAdjacentJumpLanding(FightBattleUnitState actor,
            FightBattleUnitState target, FightBattleUnitState targetPosition,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            FightBattleGrid grid, out FightGridPoint landing)
        {
            landing = default;
            if (actor == null || target == null || targetPosition == null || grid == null) return false;
            var occupied = new HashSet<FightGridPoint>();
            foreach (var unit in units)
            {
                if (unit.Id == actor.Id || !IsSelectable(unit)) continue;
                occupied.Add(new FightGridPoint(unit.X, unit.Y));
                if (unit.BodyOffsets != null)
                    foreach (var body in unit.BodyOffsets)
                        occupied.Add(new FightGridPoint(unit.X + body.X, unit.Y + body.Y));
            }
            LandingScore best = null;
            int priority = AttackPriorityClass(actor, new List<FightBattleUnitState> { target }, skill);
            foreach (var offset in new[] { new FightGridPoint(0, -1), new FightGridPoint(-1, 0),
                new FightGridPoint(1, 0), new FightGridPoint(0, 1) })
            {
                var point = new FightGridPoint(targetPosition.X + offset.X, targetPosition.Y + offset.Y);
                if (point.X == actor.X && point.Y == actor.Y || !CanLand(actor, point, grid, occupied)) continue;
                var score = ScoreLanding(actor, actor.ProbeAt(point), target, units, skill);
                if (!score.PrimaryCovered || best != null && CompareLandings(score, best, skill, priority) >= 0) continue;
                best = score;
                landing = point;
            }
            return best != null;
        }
    }
}
