using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>Original skill-view inputs at cast creation, independent of the settled roster.</summary>
    [Serializable]
    public sealed class FightBattleSkillCastPresentation
    {
        [Serializable]
        public sealed class SourceStatus
        {
            public int InstanceId, ClassId, StackCount;
        }

        [Serializable]
        public sealed class SourceSharedStack
        {
            public int ShareId, StackCount;
        }

        public int SourceX, SourceY, TargetX, TargetY, TargetId;
        // A hit-triggered skill can be created away from its source role.
        // Older receipts retain the source-position default.
        public bool HasEntityPosition;
        public int EntityX, EntityY;
        // The executor indexes Pos2d.FourDirs (Down, Left, Up, Right), not Common.Direction.
        public int FacingIndex;
        public bool FlipX, FlipSource;
        public int SkillTargetType;
        public float Duration = -1f, ActionDuration = -1f;
        // Original view queries must observe this cast, even when the executor
        // has already consumed stacks or settled another cast before rendering.
        public List<SourceStatus> SourceStatuses = new List<SourceStatus>();
        public List<SourceSharedStack> SourceSharedStacks = new List<SourceSharedStack>();

        public static FightBattleSkillCastPresentation Capture(FightBattleUnitState source,
            FightBattleSkillDefinition skill, IList<FightBattleUnitState> targets,
            FightBattleUnitState targetPosition = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            FightBattleUnitState target = targets != null && targets.Count > 0 ? targets[0] : null;
            FightBattleUnitState position = targetPosition ?? target ?? source;
            var result = new FightBattleSkillCastPresentation
            {
                SourceX = source.X, SourceY = source.Y,
                TargetX = position.X, TargetY = position.Y, TargetId = target?.Id ?? 0,
                FacingIndex = source.Direction,
                FlipX = source.HasFlipX ? source.FlipX : source.Direction == 1,
                FlipSource = skill.FlipSource, SkillTargetType = skill.SkillTargetType
            };
            if (source.Statuses != null)
            {
                var shared = new HashSet<int>();
                foreach (var status in source.Statuses)
                {
                    if (status == null) continue;
                    result.SourceStatuses.Add(new SourceStatus { InstanceId = status.InstanceId,
                        ClassId = status.ClassId, StackCount = status.StackCount });
                    if (status.SharedStackId <= 0 || !shared.Add(status.SharedStackId)) continue;
                    int total = FightStatusSharedStacks.Total(source, status.SharedStackId);
                    // Original ChangeShareStack removes zero totals from the map.
                    if (total > 0) result.SourceSharedStacks.Add(new SourceSharedStack {
                        ShareId = status.SharedStackId, StackCount = total });
                }
            }
            return result;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private static void CompleteCastPresentation(FightBattleSkillCastEvent cast,
            FightBattleSkillDefinition skill, float lastDelay)
        {
            cast.Presentation.Duration = ResolveDuration(skill, lastDelay);
            cast.Presentation.ActionDuration = ResolveActionDuration(skill, lastDelay);
        }
    }
}
