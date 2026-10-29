// Migrated complete original application buffer source; see Map14 buffered-event receipt.
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityBufferedEvents
{
public class WorldStageBufferEventDispatcher : EcEntityBufferEventDispatcherBase<BufferComponentData>
{
    private IDisposable subscription;
	protected override void Load()
	{
		subscription = mEcComponent.SubscribeStage(WorldStageComponent_OnStateUpdate);
	}

	protected override void Unload()
	{
		subscription.Dispose(); subscription=null;
	}

	private void WorldStageComponent_OnStateUpdate(ECWorldStageStateData stageComp, int preState, object param)
	{
		EntityWorldStageUpdateEventMessage entityEventMessage = new EntityWorldStageUpdateEventMessage
		{
			StageComp = stageComp,
			PreState = preState,
			Param = param
		};
		Dispatch(entityEventMessage);
	}
}

}
