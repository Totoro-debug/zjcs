// Migrated original application source; see Map14 visibility-handlers receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver=Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Awaiter=Cysharp.Threading.Tasks.UniTask.Awaiter;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StorySourceFade;


namespace Zjcs.Official.EntityVisibilityHandlers
{
public sealed class EcEntityStageResultItemUpdatedEventDelegate : EventDelegateBase<EcEntityStageResultItemUpdatedEventDelegate, IEcEntityStageResultItemUpdatedEventReceiver, VisibilityAwardItemData>
{
	protected override void FireOnReceiver(IEcEntityStageResultItemUpdatedEventReceiver receiver, VisibilityAwardItemData stageResultAwardItemComponent)
	{
		receiver.EntityStageResultItemComponent_Updated(stageResultAwardItemComponent);
	}

	public override void FireEvent(VisibilityAwardItemData stageResultAwardItemComponent)
	{
		base.FireEvent(stageResultAwardItemComponent);
	}
}

}
