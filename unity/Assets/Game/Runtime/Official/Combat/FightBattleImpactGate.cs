using System;

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
    /// Handoffs for an action that is blocked before reaching its destination.
    /// The first call publishes its actual blocked cell before action-start effects;
    /// the second waits until the original collision track reaches that cell.
    /// </summary>
    public interface IFightBattleBlockedMovementObserver
    {
        bool ObservesBlockedMovements { get; }
        void BlockedMovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint blockedPosition);
        void AwaitBlockedContact(FightBattleMovementEvent movement);
    }

    /// <summary>Associates one collision child hit with its already-consumed action contact.</summary>
    public interface IFightBattleBlockedImpactObserver
    {
        void BindBlockedImpact(FightBattleMovementEvent movement, FightBattleSkillCastEvent cast, int targetId);
    }

    /// <summary>Lets another hit continue while an already-started blocked action approaches contact.</summary>
    public interface IFightBattleDeferredBlockedImpactObserver
    {
        bool TryDeferBlockedContact(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement, Action onContact);
        void AwaitDeferredContacts(FightBattleSkillCastEvent cast);
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

    /// <summary>Receives a character-status view transition at its actual local rule boundary.</summary>
    public interface IFightBattleStatusViewObserver
    {
        void StatusViewChanged(FightBattleStatusViewEvent transition);
    }

    /// <summary>Receives a successful dispel after its actual status removals.</summary>
    public interface IFightBattleDisperseObserver
    {
        void DisperseResolved(FightBattleDamageEvent damage);
    }

    /// <summary>Coordinates a supported summon with its original hit entity.</summary>
    public interface IFightBattleSummonObserver
    {
        bool ObservesSummon(FightBattleSkillCastEvent cast, FightBattleHitDefinition definition, FightBattleHitSelectionSnapshot hit, int centerIndex, int scopeIndex);
        void AwaitSummonArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot hit, int centerIndex);
        void SummonCreated(FightBattleUnitState unit, FightBattleHitSelectionSnapshot.SpawnedUnit receipt);
    }

    /// <summary>Receives a confirmed death or revival after the hit's immediate effects.</summary>
    public interface IFightBattleUnitLifeObserver
    {
        void UnitLifeChanged(FightBattleUnitLifeEvent transition);
    }
}
