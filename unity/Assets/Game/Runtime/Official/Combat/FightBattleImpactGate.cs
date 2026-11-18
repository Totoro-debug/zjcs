namespace Zjcs.Combat.Official
{
    /// <summary>A pending original hit whose local settlement has not started.</summary>
    public sealed class FightBattlePendingImpact
    {
        public int SourceId, TargetId, SkillEntityId, SkillItemId, HitEntityId, DamageEntityId;
        public int GridX, GridY;
        // Indices belong to HitSelection when that original selection is available.
        public int EffectCenterIndex, OccurrenceIndex;
        public bool HasScopeIndex;
        public int ScopeIndex;
        // Some aggregate hits have authored scope cells but no unique target-to-cell mapping.
        public bool ScopeIdentityUnresolved;
        public FightBattleSkillCastEvent Cast;
        public FightBattleHitSelectionSnapshot HitSelection;
    }

    /// <summary>
    /// A host returns from this call only when the original hit presentation reaches this target.
    /// The host must run the battle step outside its presentation thread if waiting is required.
    /// </summary>
    public interface IFightBattleImpactGate
    {
        void AwaitImpact(FightBattlePendingImpact impact);
    }

    /// <summary>
    /// Optional preparation notification after skill cost and before nested skill logic.
    /// Presentation offsets and durations may still be incomplete; the host must not
    /// treat this callback as the actual animation start. Capture fields before returning.
    /// </summary>
    public interface IFightBattleCastPreparationObserver
    {
        void CastPrepared(FightBattleSkillCastEvent cast);
    }

    /// <summary>
    /// Optional handoff after the original movement path and action facing are known,
    /// before action-start listeners can create nested effects. Timing offsets may
    /// still be adjusted by an enclosing cast or ordinary approach movement.
    /// </summary>
    public interface IFightBattleMovementPreparationObserver
    {
        void MovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement);
    }

    /// <summary>
    /// Optional notification when a cast's duration is final. Its enclosing step may
    /// still apply later offsets, so this is not an absolute playback start time.
    /// </summary>
    public interface IFightBattleCastCompletionObserver
    {
        void CastCompleted(FightBattleSkillCastEvent cast);
    }

    /// <summary>
    /// Optional notification once an original hit has selected its targets and centers.
    /// This also covers view-only and summon hits. Capture needed fields before returning;
    /// subsequent effects may add spawn information to the selection.
    /// </summary>
    public interface IFightBattleHitSelectionObserver
    {
        void HitSelected(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection);
    }

    /// <summary>
    /// Optional notification immediately after a hit changes local HP, before later
    /// hit effects and nested casts. Capture the damage fields before returning.
    /// </summary>
    public interface IFightBattleImpactSettlementObserver
    {
        void ImpactSettled(FightBattlePendingImpact impact, FightBattleDamageEvent damage);
    }

    /// <summary>
    /// Optional notification after this occurrence's configured status applications have
    /// actually completed. Earlier callbacks may run nested skills before this result exists.
    /// </summary>
    public interface IFightBattleImpactStatusResolutionObserver
    {
        void ImpactStatusResolved(FightBattlePendingImpact impact, FightBattleDamageEvent damage);
    }
}
