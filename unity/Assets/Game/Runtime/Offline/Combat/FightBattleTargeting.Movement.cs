using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightBattleTargeting
    {
        /// <summary>
        /// Offline landing selection for ordinary role jumps. Official RealDistance bounds the
        /// landing circle; the confirmed offline priorities score actual casting positions.
        /// No action, damage, status or battle random draw is executed by a position probe.
        /// </summary>
        public static FightGridPoint PlanOrdinaryLanding(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            FightBattleGrid grid)
        {
            var start = new FightGridPoint(actor.X, actor.Y);
            if (grid == null || skill == null || actor.MoveDist <= 0) return start;
            bool attack = UsesEnemyAttackPriority(skill) && !HasActionStatus(actor, ConfusionActionType);
            var occupied = new HashSet<FightGridPoint>();
            foreach (FightBattleUnitState unit in units)
            {
                if (unit.Id == actor.Id || !IsSelectable(unit)) continue;
                occupied.Add(new FightGridPoint(unit.X, unit.Y));
                if (unit.BodyOffsets != null)
                    foreach (FightGridPoint body in unit.BodyOffsets)
                        occupied.Add(new FightGridPoint(unit.X + body.X, unit.Y + body.Y));
            }

            List<FightBattleUnitState> targets = SelectCandidates(actor, units, skill.TargetType,
                skill: skill, keepAllCandidates: attack);
            RestrictForcedTarget(actor, targets, skill);
            if (targets.Count == 0) return start;
            int priorityClass = AttackPriorityClass(actor, targets, skill);
            // Only legal full-body casting positions can attract an approach jump.
            var castingPositions = new HashSet<FightGridPoint>();
            IReadOnlyList<FightGridPoint> approachRange = ApproachRange(actor, skill);
            foreach (FightBattleUnitState target in targets)
            {
                if (!IsPriorityTarget(actor, target, skill, priorityClass)) continue;
                foreach (FightGridPoint point in CastingPositions(target, approachRange))
                    if (CanLand(actor, point, grid, occupied)) castingPositions.Add(point);
            }

            FightGridPoint best = start;
            LandingScore bestScore = null;
            LandingScore bestApproachScore = null;
            long bestRemaining = long.MaxValue;
            int radius = actor.MoveDist;
            for (int x = Math.Max(0, actor.X - radius); x <= Math.Min(grid.Width - 1, actor.X + radius); x++)
                for (int y = Math.Max(0, actor.Y - radius); y <= Math.Min(grid.Height - 1, actor.Y + radius); y++)
                {
                    var point = new FightGridPoint(x, y);
                    if (SquaredDistance(start, point) > (long)radius * radius
                        || !point.Equals(start) && !CanLand(actor, point, grid, occupied)) continue;
                    FightBattleUnitState probe = actor.ProbeAt(point);
                    List<FightBattleUnitState> available = SelectCandidates(probe, units, skill.TargetType,
                        skill: skill, inRangeOnly: true, keepAllCandidates: true);
                    available.RemoveAll(target => !targets.Exists(value => value.Id == target.Id));
                    if (available.Count == 0 && HasCasterAreaTargets(probe, units, skill))
                        available.AddRange(targets);
                    if (available.Count > 0)
                    {
                        foreach (FightBattleUnitState target in available)
                        {
                            if (priorityClass == 1 && !HasMoreHitStatus(target)) continue;
                            LandingScore score = attack ? ScoreLanding(actor, probe, target, units, skill)
                                : new LandingScore { MoveDistance = SquaredDistance(start, point),
                                    TargetId = target.Id, HpPercent = target.Hp / (double)Math.Max(1L, target.MaxHp) };
                            if (!MeetsAttackPriority(score, priorityClass)) continue;
                            if (bestScore == null || (attack ? CompareLandings(score, bestScore, skill, priorityClass)
                                : score.MoveDistance.CompareTo(bestScore.MoveDistance)) < 0)
                            {
                                best = point;
                                bestScore = score;
                            }
                        }
                    }
                    if (bestScore != null) continue;
                    long remaining = long.MaxValue;
                    foreach (FightGridPoint castPoint in castingPositions)
                        remaining = Math.Min(remaining, SquaredDistance(point, castPoint));
                    if (remaining == long.MaxValue) continue;
                    LandingScore approachScore = attack ? ScoreLanding(actor, probe, targets[0], units, skill) : null;
                    if (remaining < bestRemaining || remaining == bestRemaining
                        && (attack && bestApproachScore != null ? CompareSafety(approachScore, bestApproachScore) < 0
                            : SquaredDistance(start, point) < SquaredDistance(start, best)))
                    {
                        best = point;
                        bestRemaining = remaining;
                        bestApproachScore = approachScore;
                    }
                }
            return best;
        }

        private static IReadOnlyList<FightGridPoint> ApproachRange(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
        {
            var range = new HashSet<FightGridPoint>(skill.Range ?? new List<FightGridPoint>());
            if (skill.SkillTargetType == 0)
                foreach (FightBattleHitDefinition hit in FightBattleHitCollector.CollectAll(skill))
                {
                    bool random = hit.RandomTargetGroupId > 0 && hit.RandomTargetScope.Count > 0;
                    int anchor = random ? hit.RandomTargetAnchor : hit.Anchor;
                    if (hit.IgnoreAICount || !((FightHitAnchor)anchor).AIAssumesOnSourceCastPos()) continue;
                    var scope = random ? hit.RandomTargetScope : hit.Scope;
                    var transform = random ? hit.RandomTargetScopeTransform : hit.ScopeTransform;
                    for (int direction = 0; direction < 4; direction++)
                        foreach (FightGridPoint offset in TransformScope(scope, transform, actor,
                            new FightBattleSkillCastPresentation { FacingIndex = direction, FlipX = direction == 1 }))
                            range.Add(offset);
                }
            return new List<FightGridPoint>(range);
        }

        private static bool CanLand(FightBattleUnitState actor, FightGridPoint point,
            FightBattleGrid grid, ISet<FightGridPoint> occupied)
        {
            if (!grid.CanStand(point, actor.MoveMask, occupied)) return false;
            if (actor.BodyOffsets != null)
                foreach (FightGridPoint body in actor.BodyOffsets)
                    if (!grid.CanStand(point + body, actor.MoveMask, occupied)) return false;
            return true;
        }

        private sealed class LandingScore
        {
            public bool Forced, Marked;
            public int LargeBodyCells, TargetCount, TargetId;
            public long MoveDistance, NearestEnemyDistance;
            public double HpPercent, TotalEnemyDistance;
        }

        private static int AttackPriorityClass(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> targets, FightBattleSkillDefinition skill)
        {
            if (!UsesEnemyAttackPriority(skill)) return 0;
            foreach (FightBattleUnitState target in targets)
                if (target.Side != actor.Side && HasMoreHitStatus(target)) return 1;
            foreach (FightBattleUnitState target in targets)
                if (target.Side != actor.Side && HasLargeBody(target)) return 2;
            return 0;
        }

        private static bool UsesEnemyAttackPriority(FightBattleSkillDefinition skill)
            => skill.TargetType == 1 && (skill.SkillType == 0 || skill.SkillType == 1);

        private static void RestrictForcedTarget(FightBattleUnitState actor,
            List<FightBattleUnitState> targets, FightBattleSkillDefinition skill, int explicitTargetId = 0)
        {
            int id = explicitTargetId > 0 ? explicitTargetId
                : skill.ReleaseWhileRidicule ? RidiculeSourceId(actor) : 0;
            if (skill.TargetType == 1 && id > 0 && targets.Exists(target => target.Id == id))
                targets.RemoveAll(target => target.Id != id);
        }

        private static bool IsPriorityTarget(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, int priorityClass)
        {
            if (skill.TargetType == 1 && skill.ReleaseWhileRidicule && RidiculeSourceId(actor) > 0)
                return target.Id == RidiculeSourceId(actor);
            return priorityClass == 1 ? HasMoreHitStatus(target)
                : priorityClass != 2 || HasLargeBody(target);
        }

        private static bool HasLargeBody(FightBattleUnitState unit)
        {
            if (unit.BodyOffsets == null) return false;
            var cells = new HashSet<FightGridPoint> { new FightGridPoint(0, 0) };
            foreach (FightGridPoint body in unit.BodyOffsets) cells.Add(body);
            return cells.Count > 1;
        }

        private static bool MeetsAttackPriority(LandingScore score, int priorityClass)
            => priorityClass == 1 ? score.Marked : priorityClass != 2 || score.LargeBodyCells > 0;

        private static bool MeetsPreferredAreaTarget(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill)
        {
            if (HasActionStatus(actor, ConfusionActionType)) return true;
            var targets = SelectCandidates(actor, units, skill.TargetType, skill: skill, keepAllCandidates: true);
            RestrictForcedTarget(actor, targets, skill);
            int priorityClass = AttackPriorityClass(actor, targets, skill);
            if (priorityClass == 0) return true;
            foreach (FightBattleUnitState target in targets)
                if (IsPriorityTarget(actor, target, skill, priorityClass)
                    && MeetsAttackPriority(ScoreLanding(actor, actor, target, units, skill), priorityClass)) return true;
            return false;
        }

        private static void RankOrdinaryAttackCandidates(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            List<FightBattleUnitState> candidates, int ridiculeTargetId, bool confused)
        {
            if (!UsesEnemyAttackPriority(skill) || confused || HasActionStatus(actor, ConfusionActionType)) return;
            var targets = SelectCandidates(actor, units, skill.TargetType, skill: skill, keepAllCandidates: true);
            RestrictForcedTarget(actor, targets, skill, ridiculeTargetId);
            int priorityClass = AttackPriorityClass(actor, targets, skill);
            var scores = new Dictionary<int, LandingScore>();
            foreach (FightBattleUnitState target in candidates)
                scores[target.Id] = ScoreLanding(actor, actor, target, units, skill);
            candidates.RemoveAll(target => !targets.Exists(value => value.Id == target.Id)
                || priorityClass == 1 && !HasMoreHitStatus(target)
                || !MeetsAttackPriority(scores[target.Id], priorityClass));
            candidates.Sort((left, right) => CompareLandings(scores[left.Id], scores[right.Id], skill, priorityClass));
        }

        private static LandingScore ScoreLanding(FightBattleUnitState source,
            FightBattleUnitState probe, FightBattleUnitState primary,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill)
        {
            var result = new LandingScore { TargetId = primary.Id,
                Forced = skill.ReleaseWhileRidicule && primary.Id == RidiculeSourceId(source),
                Marked = skill.SkillTargetType != 0 && primary.Side != source.Side && HasMoreHitStatus(primary),
                HpPercent = primary.Hp / (double)Math.Max(1L, primary.MaxHp),
                NearestEnemyDistance = long.MaxValue };
            var start = new FightGridPoint(source.X, source.Y);
            var position = new FightGridPoint(probe.X, probe.Y);
            result.MoveDistance = SquaredDistance(start, position);
            var covered = new Dictionary<int, HashSet<FightGridPoint>>();
            var eligible = ScopeCandidates(probe, units, skill.TargetType, skill);
            var facing = new FightBattleSkillCastPresentation {
                FacingIndex = DirectionTo(probe, primary),
                FlipX = probe.FlipX };
            List<FightBattleHitDefinition> hits = FightBattleHitCollector.CollectAll(skill);
            foreach (FightBattleHitDefinition hit in hits)
            {
                if (hit.IgnoreAICount) continue;
                bool random = hit.RandomTargetGroupId > 0 && hit.RandomTargetScope.Count > 0;
                var centers = new List<FightGridPoint>();
                if (random)
                {
                    FightGridPoint parent = FightHitAnchorExtensions.ResolveCenter(hit.RandomTargetAnchor, probe, primary);
                    foreach (FightGridPoint offset in TransformScope(hit.RandomTargetScope,
                        hit.RandomTargetScopeTransform, probe, facing)) centers.Add(parent + offset);
                }
                else centers.Add(FightHitAnchorExtensions.ResolveCenter(hit.Anchor, probe, primary));
                foreach (FightGridPoint center in centers)
                {
                    if (hit.Scope == null || hit.Scope.Count == 0)
                    {
                        if (!random) AddCoverage(covered, primary.Id, new FightGridPoint(primary.X, primary.Y));
                        else foreach (FightBattleUnitState unit in eligible)
                            if (OccupiesPosition(unit, center)) AddCoverage(covered, unit.Id, center);
                        continue;
                    }
                    foreach (FightGridPoint offset in TransformScope(hit.Scope, hit.ScopeTransform, probe, facing))
                    {
                        FightGridPoint point = center + offset;
                        foreach (FightBattleUnitState unit in eligible)
                            if (OccupiesPosition(unit, point)) AddCoverage(covered, unit.Id, point);
                    }
                }
            }
            if (hits.Count == 0) AddCoverage(covered, primary.Id, new FightGridPoint(primary.X, primary.Y));
            foreach (FightBattleUnitState unit in units)
            {
                if (covered.TryGetValue(unit.Id, out HashSet<FightGridPoint> cells))
                {
                    result.TargetCount++;
                    if (unit.Side != source.Side && skill.SkillType != 3 && HasMoreHitStatus(unit)) result.Marked = true;
                    if (unit.Side != source.Side && HasLargeBody(unit)) result.LargeBodyCells += cells.Count;
                }
                if (!IsSelectable(unit) || unit.Side == source.Side) continue;
                long nearest = SquaredDistance(position, new FightGridPoint(unit.X, unit.Y));
                if (unit.BodyOffsets != null)
                    foreach (FightGridPoint body in unit.BodyOffsets)
                        nearest = Math.Min(nearest, SquaredDistance(position,
                            new FightGridPoint(unit.X + body.X, unit.Y + body.Y)));
                result.NearestEnemyDistance = Math.Min(result.NearestEnemyDistance, nearest);
                result.TotalEnemyDistance += Math.Sqrt(nearest);
            }
            return result;
        }

        private static void AddCoverage(Dictionary<int, HashSet<FightGridPoint>> covered,
            int targetId, FightGridPoint point)
        {
            if (!covered.TryGetValue(targetId, out HashSet<FightGridPoint> cells))
                covered.Add(targetId, cells = new HashSet<FightGridPoint>());
            cells.Add(point);
        }

        private static int CompareLandings(LandingScore left, LandingScore right,
            FightBattleSkillDefinition skill, int priorityClass)
        {
            if (left.Forced != right.Forced) return left.Forced ? -1 : 1;
            int compare;
            if (priorityClass == 1) compare = CompareSafety(left, right);
            else if (priorityClass == 2) compare = right.LargeBodyCells.CompareTo(left.LargeBodyCells);
            else if (Math.Max(left.TargetCount, right.TargetCount) > 1)
                compare = right.TargetCount.CompareTo(left.TargetCount);
            else
            {
                compare = skill.UseLastAiPriority ? CompareSafety(left, right)
                    : left.MoveDistance.CompareTo(right.MoveDistance);
                if (compare == 0) compare = left.HpPercent.CompareTo(right.HpPercent);
            }
            if (compare != 0) return compare;
            compare = CompareSafety(left, right);
            return compare != 0 ? compare : left.TargetId.CompareTo(right.TargetId);
        }

        private static int CompareSafety(LandingScore left, LandingScore right)
        {
            int compare = right.NearestEnemyDistance.CompareTo(left.NearestEnemyDistance);
            return compare != 0 ? compare : right.TotalEnemyDistance.CompareTo(left.TotalEnemyDistance);
        }

        private static long SquaredDistance(FightGridPoint left, FightGridPoint right)
        {
            long x = (long)left.X - right.X, y = (long)left.Y - right.Y;
            return x * x + y * y;
        }
    }
}
