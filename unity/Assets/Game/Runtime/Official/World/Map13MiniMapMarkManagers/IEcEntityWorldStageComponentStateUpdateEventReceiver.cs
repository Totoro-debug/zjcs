
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public interface IEcEntityWorldStageComponentStateUpdateEventReceiver : IEventReceiver
{
	void WorldStageComponent_OnStateUpdate(IMiniMapManagedStage stageComp, int preState, object param);
}

}
