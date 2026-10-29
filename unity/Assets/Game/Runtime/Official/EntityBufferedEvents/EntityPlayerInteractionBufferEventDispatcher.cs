// Migrated complete original application buffer source; see Map14 buffered-event receipt.
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityBufferedEvents
{
public class EntityPlayerInteractionBufferEventDispatcher : EcEntityBufferEventDispatcherBase<BufferComponentData>
{
    private IDisposable subscription;
	protected override void Load()
	{
		subscription = mEcComponent.SubscribePlayerRemove(EntityPlayerInteractionComponent_Remove);
	}

	protected override void Unload()
	{
		subscription.Dispose(); subscription=null;
	}

	private void EntityPlayerInteractionComponent_Remove(BufferComponentData playerInteractionComponenet, long playerId)
	{
		EntityPlayerInteractionComponentPlayerRemoveEventMessage entityEventMessage = new EntityPlayerInteractionComponentPlayerRemoveEventMessage
		{
			PlayerId = playerId
		};
		Dispatch(entityEventMessage);
	}
}

}
