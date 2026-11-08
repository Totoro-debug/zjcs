using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // The APK retains configuration only. Ordered action scheduling and one-shot
        // random fallback follow the explicitly confirmed offline rules.
        private void ApplySkillCombo(FightBattleUnitState actor, FightBattleSkillDefinition root,
            List<FightBattleUnitState> originalTargets, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            float lastDelay, ref float maxDuration, ref float maxActionDuration)
        {
            if (root.ComboSkills == null || root.ComboSkills.Count == 0) return;
            if (statusCatalog == null) throw new InvalidOperationException("A configured combo requires its official catalog.");
            float start = Math.Max(0f, ResolveActionDuration(root, lastDelay));
            foreach (int entityId in root.ComboSkills)
            {
                if (actor.Hp <= 0 || actor.Ended || !units.Any(value => value.Side != actor.Side
                    && value.Hp > 0 && !value.Ended)) break;
                var next = CreateComboSegment(actor, root, entityId);
                var targets = originalTargets.Where(value => ComboTargetAllowed(actor, next, value)).ToList();
                if (targets.Count == 0 && !TryComboFallback(actor, root, next, out next, out targets)) continue;
                if (!TryComboTargetPosition(actor, next, targets[0], out var targetPosition))
                    throw new InvalidOperationException("A selected combo target lost its legal body anchor.");
                ApplyComboFacing(actor, next);
                int castStart = casts.Count, damageStart = damageEvents.Count, movementStart = movements.Count;
                // A segment has its own authored action type, hit graph, view and trigger gates,
                // but no equipped slot: it cannot spend the ordinary release/CD a second time.
                var cast = new FightBattleSkillCastEvent { ActorId = actor.Id, Slot = -1,
                    SkillEntityId = next.EntityId, SkillItemId = next.ItemId,
                    Presentation = FightBattleSkillCastPresentation.Capture(actor, next, targets, targetPosition) };
                foreach (var target in targets) cast.TargetIds.Add(target.Id);
                casts.Add(cast);
                ExecuteSkill(actor, next, targets, casts, damageEvents, movements,
                    out float delay, out float nestedDuration, out float nestedActionDuration,
                    targetPosition: targetPosition, presentationCast: cast);
                CompleteCastPresentation(cast, next, delay);
                OffsetSkillEvents(casts, castStart, damageEvents, damageStart, movements, movementStart, start);
                maxDuration = Math.Max(maxDuration, start + Math.Max(ResolveDuration(next, delay), nestedDuration));
                float action = ResolveActionDuration(next, delay);
                maxActionDuration = Math.Max(maxActionDuration, start + Math.Max(action, nestedActionDuration));
                start += Math.Max(0f, action);
            }
        }

        private FightBattleSkillDefinition CreateComboSegment(FightBattleUnitState actor,
            FightBattleSkillDefinition root, int entityId)
        {
            int rank = Math.Max(1, root.Rank), level = Math.Max(1, root.Level), subRank = Math.Max(1, root.SubRank);
            var next = statusCatalog.CreateSkill(entityId, -1, root.ItemId, rank, level,
                subRank, ToPropertyList(statusCatalog.CalculateSkillProps(entityId, rank, level, subRank)));
            next.RootEntityId = root.RootEntityId > 0 ? root.RootEntityId : root.EntityId;
            next.StatusOrigin = FightBattleStatusOrigin.Resolve(root, actor.Id).Copy();
            next.UseItemId = root.UseItemId;
            next.SkillSkinId = root.SkillSkinId;
            next.ProfessionAvailability = root.ProfessionAvailability;
            next.SkillCost = root.SkillCost;
            InheritBlessingScales(next, root.BlessPropertyScales);
            return next;
        }

        private bool TryComboFallback(FightBattleUnitState actor, FightBattleSkillDefinition root,
            FightBattleSkillDefinition failed, out FightBattleSkillDefinition selected,
            out List<FightBattleUnitState> targets)
        {
            selected = null; targets = null;
            var candidates = new List<(FightBattleSkillDefinition Skill, List<FightBattleUnitState> Targets)>();
            foreach (int entityId in failed.FallbackSkills.Distinct())
            {
                var next = CreateComboSegment(actor, root, entityId);
                var eligible = units.Where(value => InComboRange(actor, value, failed.FallbackRange)
                    && ComboTargetAllowed(actor, next, value)).OrderBy(value => value.Id).ToList();
                if (eligible.Count > 0) candidates.Add((next, eligible));
            }
            if (candidates.Count == 0) return false;
            var choice = candidates[(int)(random.Next() * candidates.Count)];
            selected = choice.Skill;
            targets = new List<FightBattleUnitState> { choice.Targets[(int)(random.Next() * choice.Targets.Count)] };
            return true;
        }

        private bool ComboTargetAllowed(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            FightBattleUnitState target)
        {
            if (!FightBattleTargeting.IsSelectable(target)
                || !FightBattleTargeting.CanReleaseUnderControl(actor, skill)
                || !statusCatalog.CanStartAction(actor, skill.ActionType)) return false;
            if (FightBattleTargeting.Select(actor, new[] { target }, skill.TargetType, skill: skill).Count == 0) return false;
            return TryComboTargetPosition(actor, skill, target, out _);
        }

        private bool TryComboTargetPosition(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            FightBattleUnitState target, out FightBattleUnitState targetPosition)
        {
            targetPosition = null;
            if (target == null || skill.Range == null || skill.Range.Count == 0) return false;
            var body = target.BodyOffsets == null || target.BodyOffsets.Count == 0
                ? new List<FightGridPoint> { new FightGridPoint() } : target.BodyOffsets;
            // OrderBy is stable: equal Manhattan distances retain the official body order.
            // Body anchors never participate in the battle random stream.
            foreach (var point in body.Select(offset => new FightGridPoint(target.X, target.Y) + offset)
                .Where(point => skill.Range.Contains(new FightGridPoint(point.X - actor.X, point.Y - actor.Y)))
                .OrderBy(point => Math.Abs(point.X - actor.X) + Math.Abs(point.Y - actor.Y)))
            {
                var position = new FightBattleUnitState { Id = target.Id, X = point.X, Y = point.Y };
                if (skill.SourceMoves.Any(move => move.AiMustMoveTo && !move.AIIgnore
                    && (!MoveConditionsSatisfied(move, actor, target)
                        || !TryRequiredSourceLanding(actor, target, skill, move, out _, position)))) continue;
                targetPosition = position;
                return true;
            }
            return false;
        }

        private void ApplyComboFacing(FightBattleUnitState actor, FightBattleSkillDefinition skill)
        {
            if (!statusCatalog.TryGetFixedSkillFacing(skill.EntityId, out int facing)) return;
            // GridTransform.Direction and FlipX are independent in the official setter.
            if (!actor.HasFlipX) { actor.FlipX = actor.Direction == 1; actor.HasFlipX = true; }
            actor.Direction = facing;
        }

        private static bool InComboRange(FightBattleUnitState actor, FightBattleUnitState target,
            List<FightGridPoint> range)
        {
            if (target == null || range == null || range.Count == 0) return false;
            return range.Any(offset => FightBattleTargeting.OccupiesPosition(target,
                new FightGridPoint(actor.X + offset.X, actor.Y + offset.Y)));
        }

        private bool TryRequiredSourceLanding(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleSkillMove move, out FightGridPoint landing,
            FightBattleUnitState targetPosition = null)
        {
            landing = default;
            // Only the verified target-relative, fixed-offset pull is used by this combo.
            // Unsupported mandatory movement cannot silently release a segment.
            if (checkpoint.Grid == null || move.Distance != -1 || move.Direction != 1
                || !move.TowardsTargetPos || move.CanExpandOffset
                || !statusCatalog.CanStartEntityAction(actor, move.MoveBehaviourId)) return false;
            targetPosition = targetPosition ?? target;
            var positions = new[] { new FightGridPoint(targetPosition.X + move.OffsetX, targetPosition.Y + move.OffsetY) };
            var occupied = OccupiedPositions(actor.Id);
            foreach (var position in positions)
            {
                var body = actor.BodyOffsets == null || actor.BodyOffsets.Count == 0
                    ? new List<FightGridPoint> { new FightGridPoint() } : actor.BodyOffsets;
                if (body.Any(offset => !checkpoint.Grid.CanStand(position + offset, actor.MoveMask, occupied))) continue;
                if (skill.MoveByTeleport || position.Equals(new FightGridPoint(actor.X, actor.Y)))
                { landing = position; return true; }
                var path = checkpoint.Grid.FindPath(new FightGridPoint(actor.X, actor.Y), position,
                    actor.MoveMask, 65, 0, true, actor.BodyOffsets, occupied);
                if (path != null && path.Count > 1 && path[path.Count - 1].Equals(position))
                { landing = position; return true; }
            }
            return false;
        }
    }
}
