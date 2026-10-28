// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;



namespace Zjcs.Official.World.Map13GridVisibility
{
public interface IEcContainerStartEntityEventReceiver : IEventReceiver
{
	void Container_OnEntityStart(VisibilityEntity entity, bool newEntity);
}

}
