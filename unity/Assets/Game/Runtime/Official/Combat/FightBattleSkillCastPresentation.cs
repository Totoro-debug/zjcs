using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>Original skill-view inputs at cast creation, independent of the settled roster.</summary>
    [Serializable]
    public sealed class FightBattleSkillCastPresentation
    {
        public int SourceX, SourceY, TargetX, TargetY, TargetId;
        // The executor indexes Pos2d.FourDirs (Down, Left, Up, Right), not Common.Direction.
        public int FacingIndex;
        public bool FlipX, FlipSource;
        public int SkillTargetType;
        public float Duration = -1f, ActionDuration = -1f;

        public static FightBattleSkillCastPresentation Capture(FightBattleUnitState source,
            FightBattleSkillDefinition skill, IList<FightBattleUnitState> targets,
            FightBattleUnitState targetPosition = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            FightBattleUnitState target = targets != null && targets.Count > 0 ? targets[0] : null;
            FightBattleUnitState position = targetPosition ?? target ?? source;
            return new FightBattleSkillCastPresentation
            {
                SourceX = source.X, SourceY = source.Y,
                TargetX = position.X, TargetY = position.Y, TargetId = target?.Id ?? 0,
                FacingIndex = source.Direction,
                FlipX = source.HasFlipX ? source.FlipX : source.Direction == 1,
                FlipSource = skill.FlipSource, SkillTargetType = skill.SkillTargetType
            };
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
