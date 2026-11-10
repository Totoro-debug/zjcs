using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public interface IFightMovementSkillTiming
    {
        float EntityEndDelay { get; }
        float ActionEndDelay { get; }
    }

    // Confirmed offline lifetime policy, restricted to the original FightSkill/Move subtree.
    public sealed class FightOrdinaryMovementTiming : IFightMovementSkillTiming
    {
        public float EntityEndDelay { get; }
        public float ActionEndDelay { get; }

        private FightOrdinaryMovementTiming(FightBattleSkillDefinition skill, float movementEndDelay)
        {
            EntityEndDelay = Earliest(skill.Duration, skill.AllEndedDuration,
                skill.AllRelevantEndedDuration, movementEndDelay);
            ActionEndDelay = Earliest(skill.ActionDuration, skill.AllEndedActionDuration,
                skill.AllRelevantEndedActionDuration, movementEndDelay);
        }

        public static bool Supports(int entityId)
        {
            switch (entityId)
            {
                case 15300: case 15310: case 15320: case 15330: case 15340:
                case 19800: case 19810: case 19820: case 19830: case 19840:
                case 19850: case 19870: case 19890: case 19900: case 19910:
                case 19920: case 19930: case 19950: case 19960: case 19970:
                case 19980: case 19990: return true;
                default: return false;
            }
        }

        public static bool Supports(FightBattleSkillDefinition skill) => skill != null
            && Supports(skill.EntityId) && skill.SkillType == 5 && skill.SourceMoves.Count == 1
            && skill.HitGraphEntities.Count == 0 && skill.Hits.Count == 0 && skill.ExtraSkills.Count == 0
            && !skill.HasReplay && !skill.HasLogicComponent;

        // The next parent starts after the preceding path; its own move prelude
        // still elapses before its action. All four rule/view schedulers use this boundary.
        public static double ActionStartDelay(FightBattleMovementEvent movement, double previousPathEnd)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            if (movement.OrdinarySkillPresentation == null)
                return Math.Max(previousPathEnd, Math.Max(0f, movement.Delay));
            return Math.Max(previousPathEnd, Math.Max(0f, movement.Delay - movement.OrdinaryMoveDelay))
                + movement.OrdinaryMoveDelay;
        }

        // End delay is measured from skill creation, including the original move delay,
        // the settled path's original duration, and DelayDestroyTime before action End.
        public static FightOrdinaryMovementTiming Create(FightBattleSkillDefinition skill,
            float movementEndDelay)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!Supports(skill))
                throw new ArgumentException("A configured ordinary movement skill is required.", nameof(skill));
            if (float.IsNaN(movementEndDelay) || float.IsInfinity(movementEndDelay) || movementEndDelay < 0f)
                throw new ArgumentOutOfRangeException(nameof(movementEndDelay));
            return new FightOrdinaryMovementTiming(skill, movementEndDelay);
        }

        public static FightOrdinaryMovementTiming ForPath(FightBattleSkillDefinition skill,
            IReadOnlyList<FightGridPoint> path, float delayDestroyTime)
        {
            if (!Supports(skill)) throw new ArgumentException("A configured ordinary movement skill is required.", nameof(skill));
            if (path == null || path.Count < 2) throw new ArgumentException("A settled path is required.", nameof(path));
            var move = skill.SourceMoves[0];
            float duration = 0f;
            if (move.MoveDuration >= 0f) duration = move.MoveDuration * (path.Count - 1);
            else
            {
                if (move.MoveSpeed <= 0f) throw new InvalidOperationException("Original movement has no duration or speed.");
                // Original FightActionSkillMoveComponent.InitializeMovement sums RealDistance,
                // then divides the complete path length by the configured speed.
                float distance = 0f;
                for (int index = 1; index < path.Count; index++)
                {
                    float x = path[index].X - path[index - 1].X;
                    float y = path[index].Y - path[index - 1].Y;
                    distance += (float)Math.Sqrt(x * x + y * y);
                }
                duration = distance / move.MoveSpeed;
            }
            return Create(skill, move.Delay + duration + delayDestroyTime);
        }

        private static float Earliest(float fixedDelay, float allEndedDelay,
            float allRelevantEndedDelay, float movementEndDelay)
        {
            float end = float.PositiveInfinity;
            if (fixedDelay >= 0f) end = fixedDelay;
            if (allEndedDelay >= 0f) end = Math.Min(end, movementEndDelay + allEndedDelay);
            if (allRelevantEndedDelay >= 0f)
                end = Math.Min(end, movementEndDelay + allRelevantEndedDelay);
            return float.IsPositiveInfinity(end) ? -1f : end;
        }
    }
}
