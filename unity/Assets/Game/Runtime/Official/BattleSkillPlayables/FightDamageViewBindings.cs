using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.BattleSkillPlayables
{
    // Only state consumed by the original MyFightDamage client presentation.
    public abstract class FightDamageComponent : IEntityComponentInfoSource
    {
        public abstract EcEntityIdentifier TargetId { get; }
        public abstract EcEntityIdentifier SkillId { get; }
        public abstract DamageResultParameter DamageResult { get; }
        public abstract bool IsDamageTriggerStatus { get; }
        object IEntityComponentInfoSource.ComponentInfo => null;
    }

    public sealed class FightDamageComponentInfo { }
    // MyFightRoleFight forwards client events without reading shared role state.
    // Shared combat callbacks remain owned by the rule executor.
    public sealed class BattleRoleFightViewData { }
}
