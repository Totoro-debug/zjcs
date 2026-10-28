// Migrated original application/read-query code; see Map13 visibility-view receipt.
using Zjcs.Official.World.Map13EntityEvents;



namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public sealed class EcEntityPlayerInteractionUpdateEventDelegate : EventDelegateBase<EcEntityPlayerInteractionUpdateEventDelegate, IEcEntityPlayerInteractionUpdateEventReceiver, Map13EntityInteractionQuery>
{
	protected override void FireOnReceiver(IEcEntityPlayerInteractionUpdateEventReceiver receiver, Map13EntityInteractionQuery playerInteractionComp)
	{
		receiver.EntityPlayerInteractionComponent_Update(playerInteractionComp);
	}

	public override void FireEvent(Map13EntityInteractionQuery playerInteractionComp)
	{
		base.FireEvent(playerInteractionComp);
	}
}

}
