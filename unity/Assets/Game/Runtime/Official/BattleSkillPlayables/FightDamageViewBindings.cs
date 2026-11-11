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
    public struct DamageResultParameter
    {
        public DamageTypeData DamageTypeData;
    }
    public struct DamageTypeData
    {
        public bool IsBlock, IsCrit;
    }

    public interface IFightDamageViewServices : IFightSkillViewServices
    {
        // Preserve the original target notification before skill/source lookup,
        // and the source notification before any damage graph starts.
        void FireDamageEvent(MyEntity target, MyFightDamage damage);
        void FireHitEvent(MyEntity source, MyFightDamage damage);
    }
}
