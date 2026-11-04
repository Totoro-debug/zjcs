using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13MiniMap;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // A host supplies one entity source so marks, areas, occupancy and lifecycle
    // notifications retain the same entity identities and original update order.
    public interface IMainlineMiniMapEntities : IMiniMapManagedAreaAgent, IMiniMapMarkGridGround
    {
        MiniMapManagedEvents Events { get; }
        IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id);
    }
}
