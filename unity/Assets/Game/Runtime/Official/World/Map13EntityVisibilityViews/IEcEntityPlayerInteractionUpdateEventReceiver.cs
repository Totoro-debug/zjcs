// Migrated original application/read-query code; see Map13 visibility-view receipt.
using Zjcs.Official.World.Map13EntityEvents;



namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public interface IEcEntityPlayerInteractionUpdateEventReceiver : IEventReceiver
{
	void EntityPlayerInteractionComponent_Update(Map13EntityInteractionQuery playerInteractionComponenet);
}

}
