using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.BattleSkillPlayables
{
    // Consumed shared data, not a reimplementation of Common's server entities.
    // A host forwards its existing state and notifications at their transitions.
    public abstract class FightSkillComponent : IEntityComponentInfoSource
    {
        public GAction<FightSkillComponent> OnInterruped = new GAction<FightSkillComponent>();
        public abstract EcEntityIdentifier SrcEntityId { get; }
        public abstract EcEntityIdentifier TargetId { get; }
        public abstract Pos2d TargetPos { get; }
        public abstract int SkillSlot { get; }
        public abstract bool Interrupted { get; }
        public abstract ECViewEntityData Entity { get; }
        public abstract FightSkillComponentInfo Info { get; }
        object IEntityComponentInfoSource.ComponentInfo => Info;
    }

    public abstract class FightSkillComponentInfo
    {
        public abstract bool FlipSource { get; }
        public abstract bool IsEntityTargetType();
    }

    // The client event agent does not read shared agent state.
    public abstract class FightSkillAgentComponent { }

    public interface IFightSkillViewServices
    {
        MyEntity GetEntity(EcEntityIdentifier id);
        Vector3 GridToPos(Pos2d position);
    }
}
