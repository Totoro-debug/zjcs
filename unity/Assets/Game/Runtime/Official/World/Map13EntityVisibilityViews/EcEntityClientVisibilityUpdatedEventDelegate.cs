// Migrated original application/read-query code; see Map13 visibility-view receipt.
using Zjcs.Official.World.Map13EntityEvents;



namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
public sealed class EcEntityClientVisibilityUpdatedEventDelegate : EventDelegateBase<EcEntityClientVisibilityUpdatedEventDelegate, IEcEntityClientVisibilityUpdatedEventReceiver, Map13VisibilityData>
{
	protected override void FireOnReceiver(IEcEntityClientVisibilityUpdatedEventReceiver receiver, Map13VisibilityData visibilityComponent)
	{
		receiver.EntityClientVisibilityComponent_Updated(visibilityComponent);
	}

	public override void FireEvent(Map13VisibilityData visibilityComponent)
	{
		base.FireEvent(visibilityComponent);
	}
}

}
