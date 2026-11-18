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
}
