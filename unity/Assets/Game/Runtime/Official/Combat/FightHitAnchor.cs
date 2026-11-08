namespace Zjcs.Combat.Official
{
    // Migrated from Common.HitAnchor, HitExecAnchor and HitAnchorExtensions.
    public enum FightHitAnchor { OnTarget, OnSourceCastPos, OnSourceCastPosAIAsTarget, OnTargetAIPickFacing }
    public enum FightHitExecAnchor { TargetPos, SourceCastPosSnapshot, SourceCurrentPos }
    public static class FightHitAnchorExtensions
    {
        public static FightHitExecAnchor ResolveExecAnchor(this FightHitAnchor anchor)
        {
            switch (anchor)
            {
                case FightHitAnchor.OnSourceCastPos: return FightHitExecAnchor.SourceCastPosSnapshot;
                case FightHitAnchor.OnSourceCastPosAIAsTarget: return FightHitExecAnchor.SourceCurrentPos;
                default: return FightHitExecAnchor.TargetPos;
            }
        }
        public static bool AIAssumesOnSourceCastPos(this FightHitAnchor anchor)
            => anchor == FightHitAnchor.OnSourceCastPos;

        public static FightGridPoint ResolveCenter(int anchor, FightBattleUnitState source,
            FightBattleUnitState target, FightBattleSkillCastPresentation cast = null)
        {
            switch (((FightHitAnchor)anchor).ResolveExecAnchor())
            {
                case FightHitExecAnchor.SourceCastPosSnapshot:
                    return cast == null ? new FightGridPoint(source.X, source.Y) : new FightGridPoint(cast.SourceX, cast.SourceY);
                case FightHitExecAnchor.SourceCurrentPos: return new FightGridPoint(source.X, source.Y);
                default: return target == null ? new FightGridPoint(source.X, source.Y) : new FightGridPoint(target.X, target.Y);
            }
        }
    }
}
