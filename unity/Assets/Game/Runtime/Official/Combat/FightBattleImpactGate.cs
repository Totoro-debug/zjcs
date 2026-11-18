namespace Zjcs.Combat.Official
{
    /// <summary>A pending original hit whose local settlement has not started.</summary>
    public sealed class FightBattlePendingImpact
    {
        public int SourceId, TargetId, SkillEntityId, SkillItemId, HitEntityId, DamageEntityId;
        public int GridX, GridY;
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
}
