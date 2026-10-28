// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;



namespace Zjcs.Official.World.Map13GridVisibility
{
public sealed class EcContainerStartEntityEventDelegate : EventDelegateBase<EcContainerStartEntityEventDelegate, IEcContainerStartEntityEventReceiver, VisibilityEntity, bool>
{
	protected override void FireOnReceiver(IEcContainerStartEntityEventReceiver receiver, VisibilityEntity entity, bool newEntity)
	{
		receiver.Container_OnEntityStart(entity, newEntity);
	}

	public override void FireEvent(VisibilityEntity entity, bool newEntity)
	{
		base.FireEvent(entity, newEntity);
	}
}

}
