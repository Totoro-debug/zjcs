
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public sealed class EcEntityWorldStageComponentStateUpdateEventDelegate : EventDelegateBase<EcEntityWorldStageComponentStateUpdateEventDelegate, IEcEntityWorldStageComponentStateUpdateEventReceiver, IMiniMapManagedStage, int, object>
{
	protected override void FireOnReceiver(IEcEntityWorldStageComponentStateUpdateEventReceiver receiver, IMiniMapManagedStage stageComp, int preState, object param)
	{
		receiver.WorldStageComponent_OnStateUpdate(stageComp, preState, param);
	}

	public override void FireEvent(IMiniMapManagedStage stageComp, int preState, object param)
	{
		base.FireEvent(stageComp, preState, param);
	}
}

}
