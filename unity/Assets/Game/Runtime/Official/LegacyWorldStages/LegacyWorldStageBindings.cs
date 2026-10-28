using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.LegacyWorldStages
{
    // Consumed original shared-component properties. No state rules are inferred.
    public abstract class LegacyWorldStageComponentData : ECWorldStageStateData
    {
        public abstract LegacyWorldStageComponentData InteractionRedirectStage { get; }
        public abstract ECViewEntityData Entity { get; }
    }
    public interface ILegacyWorldStageVisibilitySource
    {
        IClientVisibilitySupporter ClientVisibilitySupporter { get; }
    }
    public interface ILegacyWorldStageEntityLookup
    {
        ECViewEntityBinding GetEntity(EcEntityIdentifier id);
    }
}
