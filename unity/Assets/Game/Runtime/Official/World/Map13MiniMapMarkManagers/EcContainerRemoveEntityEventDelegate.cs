
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public sealed class EcContainerRemoveEntityEventDelegate : EventDelegateBase<EcContainerRemoveEntityEventDelegate, IEcContainerRemoveEntityEventReceiver, IMiniMapSpawnEntity, object>
{
	protected override void FireOnReceiver(IEcContainerRemoveEntityEventReceiver receiver, IMiniMapSpawnEntity entity, object data)
	{
		receiver.Container_OnEntityRemove(entity, data);
	}

	public override void FireEvent(IMiniMapSpawnEntity entity, object userData)
	{
		base.FireEvent(entity, userData);
	}
}

}
